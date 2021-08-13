using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml;
using BCrypt;
using System.Linq;

namespace TcpServer
{



    class Program
    {
        static TcpListener server = null;
        static UInt16 port = 8081;
        static Dictionary<string, TcpClient> clients = new();
        static string helpList = "/help - shows this list\n/members - display the amount of members in the chat.";
        static XmlDocument doc = new XmlDocument();

        static string messageLog = "";

        /*
            *  Accepting Headers:
            *  +++++++++++++++++++++++++++++++++++++
            *  
            *  Action - string: login/signup/sendmsg
            *  Token - string: token
            *  
            *  Used for signup/login
            *  ---------------------------
            *  Username - string: username
            *  Password - string: password
            *  ---------------------------
            *  
            *  Message - string: message
            *  
            *  +++++++++++++++++++++++++++++++++++++
            *  
            *  Outgoing Headers:
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

        public static void SendToAll(string senderToken, string message)
        {
            message = (senderToken != null ? $"{doc.SelectSingleNode($"//users/*[token='{senderToken}']/token").ParentNode.Name}: " : "") + message;

            // Log the recieved message in the console
            Console.WriteLine(message);

            // Save the recieved message for new clients
            messageLog += message + "\n";

            byte[] msg = Encoding.ASCII.GetBytes(message);
            foreach (string token in clients.Keys)
            {
                if (token == senderToken) continue;
                if (!clients[token].Connected)
                {
                    clients.Remove(token);
                    continue;

                }

                NetworkStream stream = clients[token].GetStream();
                stream.Write(msg, 0, msg.Length);
            }
        }

        class ChatLog : IDisposable
        {
            void IDisposable.Dispose() { }
            public void Dispose() { }

            public byte[] log;
        }

        public static Dictionary<string, string> SplitIntoHeaders(string data)
        {
            Dictionary<string, string> dict = new();

            foreach (string header in data.Split("\n"))
            {
                string[] headerArr = header.Split(":");
                headerArr[1] = headerArr[1].StartsWith(" ") ? headerArr[1].Substring(1) : headerArr[1];

                dict[headerArr[0]] = headerArr[1];
            }

            return dict;
        }

        public static void SendToClient(ref NetworkStream stream, string message) => SendToClient(ref stream, ref message);

        public static void SendToClient(ref NetworkStream stream, ref string message)
        {
            try
            {
                byte[] byteMessage = Encoding.ASCII.GetBytes(message);
                stream.Write(byteMessage, 0, byteMessage.Length);
            }
            catch { }
        }

        public static void HandleClient(TcpClient client)
        {

            byte[] incomingBytes = new byte[256];
            NetworkStream stream = client.GetStream();

            int i;

            string token = "";

            try
            {
                // Do this forever until client disconnects. Allows to break nested loop.
                while (true)
                {
                    // read for incoming data
                    while ((i = stream.Read(incomingBytes, 0, incomingBytes.Length)) != 0)
                    {
                        string data = Encoding.ASCII.GetString(incomingBytes, 0, i);

                        Dictionary<string, string> headers = SplitIntoHeaders(data);

                        if (!headers.ContainsKey("Action"))
                        {
                            SendToClient(ref stream, "Status: error\nMessage: Action Header Missing!");
                            break;
                        }

                        string[] msg = new string[0];
                        

                        switch (headers["Action"])
                        {
                            case "login":
                                if (!headers.ContainsKey("Username") || !headers.ContainsKey("Password"))
                                {
                                    if (!headers.ContainsKey("Token")) SendToClient(ref stream, "Status: error\nMessage: Login headers missing!");
                                    else
                                    {
                                        msg = AuthUser(headers["Token"]);
                                        if (msg[0].Contains("success"))
                                        {
                                            token = msg[1];
                                            msg[1] = doc.SelectSingleNode($"//users/*[token='{msg[1]}']/token").ParentNode.Name;
                                        }
                                    }
                                }
                                else
                                {
                                    msg = AuthUser(headers["Username"], headers["Password"]);
                                    if (msg[0].Contains("success"))
                                    {
                                        token = msg[1];
                                    }
                                }
                                break;
                            case "signup":
                                if (!headers.ContainsKey("Username") || !headers.ContainsKey("Password")) SendToClient(ref stream, "Status: error\nMessage: Signup headers missing!");
                                else
                                {
                                    msg = CreateUser(headers["Username"], headers["Password"]);
                                    if (msg[0].Contains("success"))
                                    {
                                        token = msg[1];
                                    }
                                }
                                break;
                            case "sendmsg":

                                if (!headers.ContainsKey("Message")) SendToClient(ref stream, "Status: error\nMessage: Message header missing!");
                                //else if (!headers.ContainsKey("Token")) SendToClient(ref stream, "Status: error\nMessage: Token header missing!");

                                else
                                {
                                    SendToAll(token, headers["Message"]);
                                }
                                break;

                            default:
                                SendToClient(ref stream, "Status: error\nMessage: Unknown action!");
                                break;
                        }

                        

                        

                        if (!clients.ContainsValue(client) && headers.ContainsKey("Action") && (headers["Action"] == "signup" || headers["Action"] == "login") && msg[0].Contains("success"))
                        {
                            if (clients.ContainsKey(token))
                            {
                                msg[0] = "Status: error";
                                msg[0] = "Message: This user is already logged in!";
                                string message = string.Join("\n", msg);
                                SendToClient(ref stream, ref message);
                            }
                            else
                            {
                                clients.Add(token, client);


                                msg[0] = $"Status: {msg[0]}";
                                msg[1] = $"Message: {msg[1]}";
                                string message = string.Join("\n", msg);
                                SendToClient(ref stream, ref message);

                                using (ChatLog log = new())
                                {
                                    log.log = Encoding.ASCII.GetBytes(messageLog);
                                    stream.Write(log.log);
                                }
                                SendToAll(null, $"{doc.SelectSingleNode($"//users/*[token='{token}']/token").ParentNode.Name} has joined the chat!");
                            }
                        }
                        else if (msg.Length == 2)
                        {
                            msg[0] = $"Status: {msg[0]}";
                            msg[1] = $"Message: {msg[1]}";
                            string message = string.Join("\n", msg);
                            SendToClient(ref stream, ref message);
                        }

                    }
                }

            }
            // If user disconnected.
            catch (System.IO.IOException e)
            {
                if (((SocketException)e.InnerException).NativeErrorCode == 10054 && clients.Values.ToList().Contains(client))
                {
                    stream.Close();


                    SendToAll(null, $"{doc.SelectSingleNode($"//users/*[token='{token}']/token").ParentNode.Name} has left the chat!");
                    

                    clients.Remove(token);
                }
            }
        }

        static void Main(string[] args)
        {
            doc.Load(@"F:\Projects - Elias\.NET\TcpServer\TcpServer\userdata.xml");

            try
            {
                enterIP:
                Console.Write("IP Address to serve on: ");
                IPAddress addr = IPAddress.Parse(Console.ReadLine());
                //IPAddress addr = IPAddress.Parse("192.168.1.1");

                enterPort:
                Console.Write("Port to serve on (1024 - 65535): ");
                bool success = UInt16.TryParse(Console.ReadLine(), out port);

                if (port <= 1023 || port > 65535)
                {
                    Console.WriteLine("Please enter a port in the valid range! (1024 - 65535)");
                    System.Threading.Thread.Sleep(1000);
                    goto enterPort;
                } 

                Console.WriteLine("Starting Server...");

                // Create TCP Server
                server = new TcpListener(addr, port);

                // Start listening to incoming client requests
                server.Start();

                server.Server.NoDelay = true;

                Console.WriteLine("Server has started!");

                // Hnadle Connecting Clients
                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    new Task(() => HandleClient(client)).Start();
                }

            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e}");
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }


        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_-+=.";
        static Random random = new Random();

        static string[] AuthUser(string user, string pass)
        {
            string[] msg = new string[2];

            try
            {
                XmlNode node = doc.GetElementsByTagName(user)[0];
                Console.WriteLine(node);
                if (node == null)
                {
                    msg = new string[] {"error", "This username doesn't exist!"};
                }

                else if (BCrypt.Net.BCrypt.HashPassword(pass, node.SelectSingleNode("salt").InnerText) == node.SelectSingleNode("pass").InnerText)
                {
                    DateTime tokenExpDate;
                    bool success = DateTime.TryParse(node.SelectSingleNode("tokenExp")?.InnerText, out tokenExpDate);
                    if (!success || tokenExpDate <= DateTime.Now)
                    {
                        string genToken = new(Enumerable.Repeat(chars, 64).Select(s => s[random.Next(s.Length)]).ToArray());
                        node.SelectSingleNode("token").InnerText = genToken;
                        node.SelectSingleNode("tokenExp").InnerText = DateTime.Now.AddDays(14).ToString();

                        msg = new string[] { "success", $"{genToken}" };
                    }
                    else
                    {
                        msg = new string[] { "success", $"{node.SelectSingleNode("token")?.InnerText}"};
                    }
                }
                else
                {
                    msg = new string[] { "error", "Incorrect Password!" };
                }
            }
            catch {
                msg = new string[] { "error", "Something went wrong!" };
            }

            return msg;
        }

        static string[] AuthUser(string token)
        {
            string[] msg = new string[2];
            if (doc.SelectSingleNode($"//users/*[token='{token}']") != null)
            {
                msg = new string[] { "success", token };
            }
            else
            {
                msg = new string[] { "error", "Invalid Token!" };
            }
            return msg;

        }

        static string[] CreateUser(string user, string pass) {

            string[] msg = new string[2];

            string genToken = new(Enumerable.Repeat(chars, 64).Select(s => s[random.Next(s.Length)]).ToArray());

            try
            {
                if (doc.GetElementsByTagName(user)?.Count > 0)
                {
                    msg = new string[] { "error", "This name was already taken!" };
                }
                else
                {
                    // Generate salt
                    string salt = BCrypt.Net.BCrypt.GenerateSalt(12);

                    string encPass = BCrypt.Net.BCrypt.HashPassword(pass, salt);


                    XmlElement userElm = doc.CreateElement(user);

                    XmlElement temp = doc.CreateElement("pass");
                    temp.InnerText = encPass;

                    userElm.AppendChild(temp);

                    temp = doc.CreateElement("salt");
                    temp.InnerText = salt;

                    userElm.AppendChild(temp);

                    temp = doc.CreateElement("token");
                    temp.InnerText = genToken;

                    userElm.AppendChild(temp);

                    temp = doc.CreateElement("tokenExp");
                    temp.InnerText = DateTime.Now.AddDays(14).ToString();

                    userElm.AppendChild(temp);

                    doc.DocumentElement.SelectSingleNode("/users").AppendChild(userElm);
                    doc.Save(@"f:\projects - elias\.net\tcpserver\TcpServer\userdata.xml");

                    msg = new string[] { "success", $"{genToken}" };
                }
            }
            catch (System.IO.IOException) { }
            catch (Exception e)
            {
                msg = new string[] { "error", "Something went wrong!" };
            }
            return msg;
        }
    }



}
