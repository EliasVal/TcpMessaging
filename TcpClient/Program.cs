using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Win32;
using System.IO;

namespace TcpClientApp
{
    class Program
    {
        static TcpClient client = null;
        static UInt16 port = 8081;
        static string name;

        /*
            *  Outgoing Headers:
            *  +++++++++++++++++++++++++++++++++++++
            *  
            *  Action - string: login/signup/sendmsg
            *  Token - string: token
            *  
            *  Used for signup/login
            *  ---------------------------
            *  Username - string: username
            *  Password - string: password
            *  Token - string: token
            *  ---------------------------
            *  
            *  Message - string: message
            *  
            *  +++++++++++++++++++++++++++++++++++++
            *  
            *  Accepting  Headers:
            *  +++++++++++++++++++++++++++++++++++++
            *  Status - string: success/error
            *  Message - string: message following up to action, can be token.
            *  +++++++++++++++++++++++++++++++++++++
            *  
            *  Format:
            *  ---------------------------
            *  Header: data\n
            *  Header: data
            *  
            *  must have the colon there, space is not mandatory.
            *  Do not forget to escape newlines!!! (\n)
            *  Headers are case sensitive.
            *  ---------------------------
        */

        public static void WaitForInput(NetworkStream stream)
        {
            while (true && client != null && allGood)
            {
                string message = Console.ReadLine();
                if (string.IsNullOrEmpty(message)) continue;

                try
                {

                    if (!message.StartsWith("/"))
                    {
                        Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
                        Console.WriteLine($"{name}: {message}");
                    }
                    else { Console.WriteLine(""); }

                    new Task(() => SendToServer(stream, new string[] { "Action: sendmsg", $"Message: {message}" })).Start();
                }
                catch { };
            }
        }

        public static string SendToServer(NetworkStream stream, string[] headers) => SendToServer(stream, string.Join("\n", headers));
        public static string SendToServer(NetworkStream stream, string headers)
        {
            try
            {

                byte[] data = Encoding.ASCII.GetBytes(headers);
                stream.Write(data, 0, data.Length);

                byte[] buffer = new byte[256];

                // Read the first batch of the TcpServer response bytes.
                Int32 bytes = stream.Read(buffer, 0, buffer.Length);

                return Encoding.ASCII.GetString(buffer, 0, bytes);
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, string> SplitIntoHeaders(string data)
        {
            Console.WriteLine(data);
            Dictionary<string, string> dict = new();

            foreach (string header in data.Split("\n"))
            {
                string[] headerArr = header.Split(":");
                headerArr[1] = headerArr[1].StartsWith(" ") ? headerArr[1].Substring(1) : headerArr[1];

                dict[headerArr[0]] = headerArr[1];
            }

            return dict;
        }


        static void Main(string[] args)
        {

            enterIP:
            Console.Write("IP Address to connect to: ");
            string addr = Console.ReadLine();

            enterPort:
            Console.Write("Port to connect to (1024 - 65535): ");
            bool success = UInt16.TryParse(Console.ReadLine(), out port);

            if (port <= 1023 || port > 65535)
            {
                Console.WriteLine("Please enter a port in the valid range! (1024 - 65535)");
                Thread.Sleep(1000);
                goto enterPort;
            }

            client = new TcpClient(addr, port);

            NetworkStream stream = client.GetStream();

            string token = null;
            if (OperatingSystem.IsWindows()) token = (string)Registry.GetValue($"{Registry.CurrentUser}\\ChatApp", "Token", null);

            if (token != "null" && OperatingSystem.IsWindows())
            {
                Dictionary<string, string> res = SplitIntoHeaders(SendToServer(stream, new string[] { "Action: login", $"Token: {token}" }));
                if (res["Status"] == "success") name = res["Message"];
                else {

                    Registry.SetValue($"{Registry.CurrentUser}\\ChatApp", "Token", "null");
                    Main(null);
                }
            }
            else
            {
                init:

                Console.WriteLine("1) Login\n2) Sign Up");

                int choice;
                bool successChoice = int.TryParse(Console.ReadLine(), out choice);

                if (!successChoice || choice < 1 || choice > 2)
                {
                    Console.Clear();
                    goto init;
                }

                Console.Write("Username: ");
                string username = Console.ReadLine();
                Console.Write("Password: ");
                string password = Console.ReadLine();

                Dictionary<string, string> res = SplitIntoHeaders(SendToServer(stream, new string[] { $"Action: {(choice == 1 ? "login" : "signup")}", $"Username: {username}", $"Password: {password}" }));
                if (res["Status"] == "success")
                {
                    name = username;
                    if (OperatingSystem.IsWindows()) Registry.SetValue($"{Registry.CurrentUser}\\ChatApp", "Token", res["Message"]);
                }
                else
                {
                    Console.WriteLine(res["Message"]);
                    Thread.Sleep(1500);
                    goto init;
                }

            }

            Console.Clear();
            Console.WriteLine("You have joined the chat.");

            client.NoDelay = true;
            client.Client.NoDelay = true;

            Task t = new Task(() => WaitForInput(stream));
            t.Start();

            while (allGood)
            {
                StartRead(stream, t);
            }
            

            Console.ReadKey();
        }

        static bool allGood = true;

        public static async void StartRead(NetworkStream stream, Task t)
        {
            try
            {

                byte[] buffer = new byte[1024];
                Int32 bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine(message);
            }
            
            catch (IOException e)
            {
                if (!allGood) return;

                if (((SocketException)e.InnerException).NativeErrorCode == 10054)
                {
                    Console.WriteLine("The server was shut down.");

                    try
                    {
                        client.Close();
                    }
                    catch { }

                    client = null;

                    allGood = false;
                }
            }
            catch (Exception E)
            {
                Console.WriteLine(E.Message);
                if (!allGood) return;
                allGood = false;
            }
            
        }
    }
}
