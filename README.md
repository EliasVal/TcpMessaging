# 💬 TCP Messaging App 💬

#### It has many security flaws (probably) and probably won't perform well. It was just me fiddling with TCP to get a grasp of it.
#### You can use the code from this project to your heart's content, learn from it or point out my mistakes.

This repo has 3 branches, `main`, `client`, and `server`. The names are self-explanatory, but the `main` branch only contains this `README.md` file.

## 🔧 How does it work?
Basically, it uses bootleg HTTP requests. That is the only way I could've thought of to make this scale well and work well.
When a client attempts to log in, the server checks the username and password and returns a token, which will be used to flawlessly log in the next time the client opens the app.
The process is the same for sign-up, just without password checking.

When a client sends a message, the server receives it and forwards it to the rest of the clients that are connected.

And that's basically it, (after grossly oversimplifying) I suppose.

## NOTE:
If you are gonna use the code, on the first line of the `Main()` method and at the end of the `CreateUser()` method in `Program.cs` in the `server` branch, change the path to your XML file.
Structure should be:
```xml
<users>
 <username>
  <token> ... </token>
  <tokenExp> ... </tokenExp>
  <salt> ... </salt>
  <pass> ... </pass>
 </username>
</users>
```
