using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace timebot {
	public class Program {
		public static TcpClient socket;
		public static StreamReader reader;
		public static StreamWriter writer;
		public static NetworkStream stream;

		static bool running = true;

		static DateTime dtLaunched = DateTime.Now;
		public static DataSet dsData;

		static string
			username = "timebot2",
			host = "irc.freenode.net",
			desc = "timebot 2.0",
			password = "hunter2",
			admin = "timeshifter";

		static Type cpType;
		static object cpObj;
		static MethodInfo cpParse, cpGetNextMessage;
		static Timer tmrTick = new Timer(GetNextMessage);

		static void Main() {
			LoadData();

			try {
				Init();
				ReloadProcessor(admin);
				tmrTick.Change(0, 1000);
				ReadMessage(reader);
				reader.Close();
				writer.Close();
				stream.Close();
			}
			catch (Exception ex) {
				Console.WriteLine("Error: " + ex.ToString());
				System.Threading.Thread.Sleep(15000);
				Console.ReadLine();
			}
		}

		static void Init() {
			try {
				socket = null;
			}
			catch { }
			try {
				stream = null;
			}
			catch { }
			try {
				reader = null;
			}
			catch { }
			try {
				writer = null;
			}
			catch { }

			socket = new TcpClient(host, 6667);
			socket.ReceiveBufferSize = 1024;
			Console.WriteLine("Connected");
			stream = socket.GetStream();
			reader = new StreamReader(stream);
			writer = new StreamWriter(stream);
			Send("USER " + username + " 8 * :" + desc);
			Send("NICK " + username);
			Send("PRIVMSG NickServ :identify " + password);

		}

		static void ReadMessage(StreamReader reader) {
			try {
				while (running) {
					string data = reader.ReadLine();
					Console.WriteLine(DateTime.Now.ToString("HH:mm:ss ") + data);
					if (data.Split(' ')[0].ToLower() == "ping") {
						Send("PONG " + data.Split(' ')[1]);
					}
					else {
						cpParse.Invoke(cpObj, new object[] { data });
					}
				}
			}
			catch (IOException ioEx) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write("Read Error: ");
				Console.ResetColor();
				Console.Write(ioEx.GetType().ToString() + ": " + ioEx.ToString());
				Console.WriteLine();


				System.Threading.Thread.Sleep(15000);
				
				
				

				Init();
				ReadMessage(reader);

			}
			catch (Exception ex) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write("Error: ");
				Console.ResetColor();
				Console.Write(ex.GetType().ToString() + ": " + ex.ToString());
				Console.WriteLine();
				System.Threading.Thread.Sleep(15000);
				Init();
				ReadMessage(reader);
			}
		}

		static void LoadData() {
			dsData = new DataSet();
			dsData.ReadXml(Environment.CurrentDirectory + "\\data.xml");

			username = dsData.Tables["Config"].Rows[0]["UserName"].ToString();
			host = dsData.Tables["Config"].Rows[0]["Host"].ToString();
			desc = dsData.Tables["Config"].Rows[0]["Description"].ToString();
			password = dsData.Tables["Config"].Rows[0]["Password"].ToString();
		}

		static void ReloadProcessor(string target) {
			try {
				using (Microsoft.CSharp.CSharpCodeProvider cscp = new Microsoft.CSharp.CSharpCodeProvider()) {
					string dir = Environment.CurrentDirectory.Replace("\\x86", "");
					int idx = dir.IndexOf("timebot");
					dir = dir.Substring(0, idx + 7) + "\\CommandParser\\CommandParser.cs";
					List<string> fileLines = File.ReadAllLines(dir).ToList();

					System.CodeDom.Compiler.CompilerParameters cp = new System.CodeDom.Compiler.CompilerParameters();
					cp.IncludeDebugInformation = true;
					
					fileLines.RemoveAt(0);
					while (fileLines[0].Trim() != "*/") {
						cp.ReferencedAssemblies.Add(fileLines[0]);
						fileLines.RemoveAt(0);
					}
					fileLines.RemoveAt(0);

					string file = string.Join("", fileLines).Replace("\t", "");

					var res = cscp.CompileAssemblyFromSource(cp, file);

					if (res.Errors.Count > 0) {
						foreach (System.CodeDom.Compiler.CompilerError e in res.Errors) {
							SendMessage(admin, e.ErrorText + " - " + e.FileName + ", line " + e.Line.ToString());
						}
					}
					else {
						cpType = res.CompiledAssembly.GetType("CommandParser");
						cpObj = Activator.CreateInstance(cpType);
						cpParse = cpType.GetMethod("Parse");
						cpGetNextMessage = cpType.GetMethod("GetNextMessage");
					}
				}

				SendMessage(target, "Command processor loaded.");
			}
			catch (Exception ex) {
				if (target != admin)
					SendMessage(target, "Command processor errored on reload.");
				SendMessage(admin, "Error loading command processor: " + ex.Message);
			}
		}


		static void SendMessage(string target, string data) {
			Send("PRIVMSG " + target + " :" + data);
		}

		static void Send(string data) {
			try {
				writer.WriteLine(data);
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.Write(">>> ");
				Console.ResetColor();
				Console.Write(data);
				Console.WriteLine();
				writer.Flush();
			}
			catch (Exception ex) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write("Send error: ");
				Console.ResetColor();
				Console.Write(ex.Message);
				Console.WriteLine();
			}
		}



		static void GetNextMessage(object o) {
			if (cpGetNextMessage != null) {
				string res = (string)cpGetNextMessage.Invoke(cpObj, null);
				if (res.StartsWith("QUIT")) {
					Send(res);
					running = false;
				}
				else if (res.StartsWith("RELOAD")) {
					ReloadProcessor(res.Split(' ')[1]);
				}
				else if (res.StartsWith("UPTIME")) {
					TimeSpan ts = DateTime.Now - dtLaunched;
					SendMessage(res.Split(' ')[1], string.Format("I have been running for {0}{1}{2}{3}",
						ts.Days > 0 ? ts.Days.ToString() + "d " : "",
						ts.Hours > 0 ? ts.Hours.ToString() + "h " : "",
						ts.Minutes > 0 ? ts.Minutes.ToString() + "m " : "",
						ts.Seconds.ToString() + "s"
						));
				}
				if (res != "")
					Send(res);
			}
		}

	}
}