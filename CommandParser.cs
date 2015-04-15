/*
mscorlib.dll
Microsoft.CSharp.dll
System.dll
System.Core.dll
System.Data.dll
System.Web.dll
System.Xml.dll
System.Xml.Linq.dll
Newtonsoft.Json.dll
Jurassic.dll
*/


using System;
using System.Collections.Generic;
using System.Net;
using System.Data;
using Newtonsoft.Json;
using System.Web;
using System.Linq;
using System.ComponentModel;
using System.Reflection;
using System.Dynamic;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class CommandParser {


	public DataSet dsData;


	public string username, desc, admin, SYNTAX_ERROR, AUTH_ERROR, CommandCharacter, CryptoJS, password;


	public WebClient wc;

	public List<string> AlertUsers, JSFunctions, Trolls, AllowedUsers, IgnoredUsers, PendingOutput;
	public Dictionary<string, User> AuthedUsers;
	public List<KarmaObject> KarmaList = new List<KarmaObject>();
	public Dictionary<string, List<MessageHistory>> ChannelHistory;
	public Jurassic.ScriptEngine JSEngine = new Jurassic.ScriptEngine();
	public List<UserObject> users = new List<UserObject>();
	public DataTable hsData;

	public CommandParser() {
		wc = new WebClient();
		wc.Encoding = System.Text.Encoding.UTF8;

		JSEngine.SetGlobalValue("console", new Jurassic.Library.FirebugConsole(JSEngine));

		dsData = new DataSet();
		dsData.ReadXml(Environment.CurrentDirectory + "\\data.xml");

		CryptoJS = File.ReadAllText(Environment.CurrentDirectory + "\\cryptbin.js");

		AlertUsers = new List<string>();
		JSFunctions = new List<string>();
		Trolls = new List<string>();
		PendingOutput = new List<string>();
		

		ChannelHistory = new Dictionary<string, List<MessageHistory>>();

		
		try {
			AlertUsers.AddRange((from DataRow dr in dsData.Tables["Alerts"].Rows select dr["TargetUser"].ToString().ToLower()).ToList().Distinct());
			Trolls.AddRange((from DataRow dr in dsData.Tables["Trolls"].Rows select dr["Mask"].ToString().ToLower()).ToList().Distinct());
		}
		catch {
		}
		username = "timebot2";
		desc = "timebot 2.0";
		admin = "timeshifter";
		SYNTAX_ERROR = "Invalid syntax.";
		AUTH_ERROR = "You are not allowed to do that!";
		CommandCharacter = "!";

		username = dsData.Tables["Config"].Rows[0]["UserName"].ToString();
		desc = dsData.Tables["Config"].Rows[0]["Description"].ToString();
		SYNTAX_ERROR = dsData.Tables["Config"].Rows[0]["SyntaxError"].ToString();
		AUTH_ERROR = dsData.Tables["Config"].Rows[0]["AuthError"].ToString();
		admin = dsData.Tables["Config"].Rows[0]["Admin"].ToString();
		CommandCharacter = dsData.Tables["Config"].Rows[0]["CommandCharacter"].ToString();
		password = dsData.Tables["Config"].Rows[0]["Password"].ToString();

		/*AuthedUsers = new List<string>();*/
		AllowedUsers = new List<string>();
		AllowedUsers.AddRange((from DataRow dr in dsData.Tables["AuthedUsers"].Rows select dr["User"].ToString().ToLower()).ToList().Distinct());
		AuthedUsers = new Dictionary<string, User>();
		foreach (DataRow drUser in dsData.Tables["AuthedUsers"].Rows) {
			User u = new User() { Level = int.Parse(drUser["Level"].ToString()), IsAuthed = false };
			AuthedUsers.Add(drUser["User"].ToString().ToLower(), u);
		}

		IgnoredUsers = new List<string>();
		try {
			IgnoredUsers.AddRange((from DataRow dr in dsData.Tables["IgnoreUsers"].Rows select dr["User"].ToString().ToLower()).ToList().Distinct());
		}
		catch { }

		KarmaList = new List<KarmaObject>();
		if (dsData.Tables.Contains("Karma")) {
			foreach (DataRow dr in dsData.Tables["Karma"].Rows) {
				KarmaObject k = new KarmaObject(dr["User"].ToString(), dr["Channel"].ToString());
				k.Score = int.Parse(dr["Score"].ToString());
				KarmaList.Add(k);
			}
		}


		LoadHSData();


	}

	public string GetNextMessage() {
		if (dsData.Tables.Contains("Reminders")) {
			for (int i = dsData.Tables["Reminders"].Rows.Count - 1; i >= 0; i--) {
				if (DateTime.Now >= DateTime.Parse(dsData.Tables["Reminders"].Rows[i]["Time"].ToString())) {
					PendingOutput.Add(SendMessage(dsData.Tables["Reminders"].Rows[i]["User"].ToString(), dsData.Tables["Reminders"].Rows[i]["Message"].ToString()));
					dsData.Tables["Reminders"].Rows.RemoveAt(i);
					Save();
				}
			}
		}
		if (PendingOutput.Count > 0) {
			string s = PendingOutput[0];
			PendingOutput.RemoveAt(0);
			return s;
		}
		else {
			return "";
		}
	}

	public void Parse(string data) {

		List<string> parts = data.Split(' ').ToList();

		string user = parts[0].Split('!')[0].Substring(1),
			hostmask = parts[0].Contains("!") ? parts[0].Split('!')[1] : "",
			syscmd = parts[1].ToLower(),
			target = parts[2],
			originalMessage = "";

		try {
			if (!ChannelHistory.ContainsKey(target)) {
				ChannelHistory.Add(target, new List<MessageHistory>());
			}

			if (!IgnoredUsers.Contains(user.ToLower())) {

				parts.RemoveRange(0, 3);

				if (!target.StartsWith("#")) {
					target = user;
				}

				originalMessage = string.Join(" ", parts);

				/*
				for (int i = 0; i < parts.Count; i++)
					origiginalMessage += parts[i] + " ";
				 * */
				if (originalMessage.Length > 0) {
					originalMessage = originalMessage.Substring(1);
				}

				switch (syscmd) {
					case "353":
						/*ret.Add(SendMessage(admin, msgText));*/

						break;
					case "433":
						PendingOutput.Add("NICK timebot2_1");
						PendingOutput.Add(SendMessage("NickServ", "ghost " + username + " " + password));
						PendingOutput.Add("NICK " + username);
						PendingOutput.Add(SendMessage(admin, "I'm online."));
						break;
					case "join":
						/*ret.Add(SendMessage(target, "Welcome to " + target + ", " + user + "!"));*/

						PendingOutput.AddRange(CheckAlerts(user));


						foreach (string t in Trolls) {
							if (hostmask.ToLower().Contains(t)) {
								string nick = (from DataRow dr in dsData.Tables["Trolls"].Rows where dr["Mask"].ToString() == t select dr["User"].ToString()).First();
								if (!user.ToLower().Contains(nick.ToLower())) {
									PendingOutput.Add(SendMessage(target, "Troll detected! (" + (from DataRow dr in dsData.Tables["Trolls"].Rows where dr["Mask"].ToString() == t select dr["User"].ToString()).First() + ")"));
									break;
								}
							}
						}


						if (user == username) {
							PendingOutput.Add(SendMessage(admin, "I've joined " + target));
							CreateTable("ChannelSettings", new string[] { "Channel", "IsOp", "CommandCharacter", "GetLinkURL" });
							if (GetRowIndex("ChannelSettings", "Channel", target, false) == -1) {
								DataRow drSettings = dsData.Tables["ChannelSettings"].NewRow();
								drSettings["Channel"] = target;
								drSettings["IsOp"] = "false";
								drSettings["CommandCharacter"] = CommandCharacter;
								drSettings["GetLinkURL"] = "true";
								dsData.Tables["ChannelSettings"].Rows.Add(drSettings);
								Save();
								PendingOutput.Add(SendMessage(admin, "New channel " + target + " settings created."));
							}
							PendingOutput.Add(SendMessage("ChanServ", "status " + target));
						}


						break;
					case "nick":

						if (AuthedUsers.ContainsKey(user.ToLower())) {

							/*int authIdx = AuthedUsers2.Keys(user.ToLower());
							AuthedUsers[authIdx] = target.Substring(1).ToLower();*/

							User u = AuthedUsers[user.ToLower()];

							AuthedUsers.Add(target.Substring(1).ToLower(), new User() { IsAuthed = u.IsAuthed, Level = u.Level });
							AuthedUsers.Remove(user.ToLower());
							
						}

						break;
					case "part":
						if (user == username) {
							PendingOutput.Add(SendMessage(admin, "I've left " + target));
						}
						break;
					case "quit":
						/*AuthedUsers.Remove(user.ToLower());*/
						if (AuthedUsers.ContainsKey(user.ToLower())) {
							AuthedUsers[user.ToLower()].IsAuthed = false;
						}
						break;
					case "invite":
						/*PendingOutput.Add("JOIN " + originalMessage);*/
						PendingOutput.Add(SendMessage(admin, "I've been invited to " + originalMessage));
						break;
					case "notice":
						if (parts[1].ToLower() == "acc") {
							string usr = parts[0].Substring(1).ToLower();
							if (parts[2] == "3" && AuthedUsers.ContainsKey(usr)) {
								/*AuthedUsers.Add(parts[0].Substring(1));*/
								AuthedUsers[usr].IsAuthed = true;
								PendingOutput.Add(SendMessage(usr, "You have been authenticated."));
							}

						}
						if (originalMessage.Contains("You have access flags")) {
							try {
								int plus = originalMessage.IndexOf('+');
								string perms = originalMessage.Substring(plus, originalMessage.IndexOf(' ', plus));
								bool isOp = perms.Contains("o") || perms.Contains("O");
								if (isOp) {
									plus = originalMessage.IndexOf('#');
									string chan = originalMessage.Substring(plus, originalMessage.Length - plus - 2);
									PendingOutput.Add(SendMessage(admin, chan));
									/*int rowId = GetRowIndex("ChannelSettings", "Channel", chan, false);
									dsData.Tables["ChannelSettings"].Rows[rowId]["IsOp"] = "true";
									Save();*/
								}
							}
							catch { }
						}

						if (originalMessage.Contains("You are now identified for ")) {
							SendMessage(admin, "I'm authenticated.");
							foreach (DataRow dr in dsData.Tables["AutoJoin"].Rows) {
								if (bool.Parse(dr["Active"].ToString()))
									PendingOutput.Add("JOIN " + dr["Channel"].ToString());
							}

						}

						PendingOutput.Add(SendMessage(admin, user + ": " + originalMessage));
						break;
					case "privmsg":
						if (originalMessage.Trim() == "") {
							break;
						}

						List<string> segments = originalMessage.Split(' ').ToList();

						if (user == "EntBot") {
							user = segments[0].TrimStart('<').TrimEnd('>');
							segments.RemoveAt(0);
						}
						for (int i = segments.Count - 1; i >= 0; i--) {
							if (segments[i].Trim() == "") {
								segments.RemoveAt(i);
							}
						}

						originalMessage = string.Join(" ", segments.ToArray());

						if (originalMessage.StartsWith(CommandCharacter)) {
							string cmd = segments[0];
							segments.RemoveAt(0);

							string msgText = string.Join(" ", segments.ToArray());
							cmd = cmd.TrimStart(CommandCharacter.ToCharArray());




							MethodInfo[] methods = (typeof(CommandParser)).GetMethods();
							methods = (from MethodInfo m in methods where m.GetCustomAttributes(typeof(CommandSettings), false).Count() > 0 select m).ToArray();

							var currMethod = (from MethodInfo m in methods where m.Name.TrimStart("_".ToCharArray()) == cmd select m).ToList();

							if (currMethod.Count == 1) {
								/* command exists, check permissions and invoke */
								CommandSettings methodSettings = (CommandSettings)(currMethod[0].GetCustomAttributes(typeof(CommandSettings), false)[0]);

								if (methodSettings.Public || methodSettings.Level == 0) {
									/* public command */
									var results = currMethod[0].Invoke(this, new object[] { target, user, msgText });
									if (results != null) {
										if (results.GetType() == typeof(String)) {
											PendingOutput.Add((string)results);
										}
										else {
											PendingOutput.AddRange((List<string>)results);
										}
									}
								}
								else {
									/* restricted command */
									if (!AuthedUsers.ContainsKey(user.ToLower())) {
										PendingOutput.Add(SendMessage(target, AUTH_ERROR));
									}
									else {
										if (AuthedUsers[user.ToLower()].Level < methodSettings.Level || !AuthedUsers[user.ToLower()].IsAuthed) {
											PendingOutput.Add(SendMessage(target, AUTH_ERROR));
										}
										else {
											var results = currMethod[0].Invoke(this, new object[] { target, user, msgText });
											if (results != null) {
												if (results.GetType() == typeof(String)) {
													PendingOutput.Add((string)results);
												}
												else {
													PendingOutput.AddRange((List<string>)results);
												}
											}
										}
									}
								}



							}
							else {
								/* command does not exist, check for alias */
								int idx = -1;
								for (int i = 0; i < dsData.Tables["Alias"].Rows.Count; i++) {
									if (dsData.Tables["Alias"].Rows[i]["Keyword"].ToString().ToLower() == cmd.ToLower()) {
										if (dsData.Tables["Alias"].Rows[i]["Channel"].ToString() == target) {
											idx = i;
											break;
										}
										else if (idx == -1) {
											idx = i;
										}


									}
								}

								if (idx > -1) {
									/* alias exists, send */
									string msg = dsData.Tables["Alias"].Rows[idx]["Message"].ToString();
									string[] prms = new string[1];
									prms[0] = "";
									if (msg.Contains("{")) {
										if (msg.IndexOf("{") != msg.LastIndexOf("{")) {
											prms = msgText.Split(',');

										}
										else {
											prms[0] = msgText;
										}
										PendingOutput.Add(SendMessage(target, string.Format(msg, prms)));
									}
									else {
										PendingOutput.Add(SendMessage(target, msg));
									}


								}

							}







						}
						else { /*any non-command text*/

							/*
							MatchCollection matches;
							try {
								if (target == "#/r/webdev") {
									matches = Regex.Matches(originalMessage, @"pastebin\.com\/([A-Za-z0-9]+)");
									foreach (Match m in matches) {

										string iframe = wc.DownloadString("http://pastebin.com/embed_iframe.php?i=" + m.Groups[1].Value),
											raw = wc.DownloadString("http://pastebin.com/raw.php?i=" + m.Groups[1].Value).Replace(Environment.NewLine, "\\n");
										string mode = iframe.ToLower().Contains("<div class=\"text\">") ? "plaintext" : "code";

										string[] result = Encrypt(raw, mode);

										if (result.Length == 2) {
											ret.Add(SendMessage(target, user + ", your paste is now encrypted (via cryptbin)! " + result[0]));
											ret.Add(SendMessage(user, "Your Cryptbin delete link: https://cryptbin.com/delete/" + result[1]));
										}
										else {
											ret.Add(SendMessage(target, result[0]));
										}

									}
								}
							}
							catch (Exception ex) {
								ret.Add(SendMessage(target, ex.Message));
							}
							matches = null;
							*/

								bool hasReplaceMatch = false;
							if (target.StartsWith("#")) {

								int id = -1;
								try {
									id = users.IndexOf(users.Where(u => u.UserName == user).First());
								}
								catch { }
								if (id == -1) {
									users.Add(new UserObject(user));
									id = users.Count - 1;
								}
								UserObject usr = users[id];


								usr.LastSeenTimestamp = DateTime.Now;
								usr.LastSeenMessage = originalMessage;
								usr.LastSeenChannel = target;

								users[id] = usr;

								foreach (UserObject u in users) {
									if (originalMessage.ToLower().Contains(u.UserName)) {
										u.History.Add(new HistoryObject() { Timestamp = DateTime.Now, Message = "(" + target + ") " + user + ": " + originalMessage });
									}
									if (u.History.Count > 10) {
										u.History.RemoveAt(0);
									}
								}







								Regex regReplace = new Regex(@"^s\/(?<find>.*)\/(?<replace>.*)");
								if (regReplace.IsMatch(originalMessage)) {
									MatchCollection mCol = regReplace.Matches(originalMessage);
									/*PendingOutput.Add(SendMessage(admin, "match found " + mCol[0].Groups["find"].Value));*/
									MessageHistory matchMsg = new MessageHistory();
									foreach (MessageHistory msg in ChannelHistory[target]) {
										if (msg.Message.Contains(mCol[0].Groups["find"].Value)) {
											matchMsg = msg;
											hasReplaceMatch = true;
											break;
										}
									}
									if (hasReplaceMatch) {
										PendingOutput.Add(SendMessage(target, string.Format("<{0}> {1}", matchMsg.User, matchMsg.Message.Replace(mCol[0].Groups["find"].Value, "\x02" + mCol[0].Groups["replace"].Value + "\x0F"))));
									}
									else {
										PendingOutput.Add(SendMessage(target, "No matches found."));
									}
								}
								else {
									ChannelHistory[target].Insert(0, new MessageHistory() { Timestamp = DateTime.Now, User = user, Message = originalMessage });
									if (ChannelHistory[target].Count >= 200) {
										ChannelHistory[target].RemoveRange(200, ChannelHistory[target].Count() - 200);
									}
								}
							}

							if (!hasReplaceMatch) {
								bool save = false;
								foreach (string s in segments) {

									if ((s.ToLower().StartsWith("http://") || s.ToLower().StartsWith("https://") || s.ToLower().StartsWith("www."))) {
										string url = s;
										if (s.ToLower().StartsWith("www.")) {
											url = "http://" + s;
										}

										if (s.ToLower().Contains("youtube.com") || s.ToLower().Contains("youtu.be")) {
											/* yt api key: AIzaSyAZHQtN9tSr8FX7bvgvY7JDSLn3eA4nJFM 
											https://www.youtube.com/watch?v=dOyJqGtP-wU
											http://youtu.be/dOyJqGtP-wU
											https://www.googleapis.com/youtube/v3/videos?id=dOyJqGtP-wU&key=AIzaSyAZHQtN9tSr8FX7bvgvY7JDSLn3eA4nJFM&part=snippet,contentDetails,statistics
											*/
											string vid = "";
											if (s.ToLower().Contains("youtube.com")) {

												var queryString = string.Join(string.Empty, s.Split('?')[1].Split('#')[0]);
												vid = System.Web.HttpUtility.ParseQueryString(queryString)["v"];
											}
											else {
												if (s.Contains("?")) {
													vid = s.Substring(s.LastIndexOf('/') + 1);
													vid = vid.Substring(0, vid.IndexOf("?")).Split('#')[0];
												}
												else {
													vid = s.Substring(s.LastIndexOf('/') + 1);
												}
											}
											string json = wc.DownloadString("https://www.googleapis.com/youtube/v3/videos?id=" + vid + "&key=AIzaSyAZHQtN9tSr8FX7bvgvY7JDSLn3eA4nJFM&part=snippet,contentDetails,statistics");

											dynamic jsResult = JsonConvert.DeserializeObject(json);


											PendingOutput.Add(SendMessage(target, string.Format("({0}) {1} ({2}, {3:N0} views, by {4} on {5})",
												user,
												jsResult.items[0].snippet.title.Value,
												jsResult.items[0].contentDetails.duration.Value.Substring(2).ToLower(),
												jsResult.items[0].statistics.viewCount.Value,
												jsResult.items[0].snippet.channelTitle.Value,
												jsResult.items[0].snippet.publishedAt.Value.ToShortDateString()
												)));



										}
										else {



											string site = wc.DownloadString(url);

											if (wc.ResponseHeaders.GetValues("Content-Type")[0].ToLower().Contains("text/html")) {
												int start = site.ToLower().IndexOf("<title");
												start = site.ToLower().IndexOf(">", start) + 1;

												int end = site.ToLower().IndexOf("</title>", start);

												PendingOutput.Add(SendMessage(target, HttpUtility.HtmlDecode(site.Substring(start, end - start).Replace("\n", "").Replace("\t", "").Trim())));
											}
										}
									}
									else if (target.StartsWith("#") && (s.EndsWith("++") || s.EndsWith("--") || s.StartsWith("++") || s.EndsWith("--"))) {
										string obj = s.Trim("+-".ToCharArray()).ToLower();
										if (obj != user.ToLower()) {
											int id = -1;

											save = true;
											for (int i = 0; i < KarmaList.Count; i++) {
												if (KarmaList[i].ObjectName == obj && KarmaList[i].Channel == target) {
													id = i;
												}
											}

											KarmaObject k = new KarmaObject();
											if (id == -1) {
												k = new KarmaObject(obj, target);
											}
											else {
												k = KarmaList[id];
											}

											if (s.EndsWith("++") || s.StartsWith("++")) {
												k.Score++;
											}
											else {
												k.Score--;
											}

											if (id == -1) {
												KarmaList.Add(k);
											}
											else {
												KarmaList[id] = k;
											}

											break;
										}


									}
								}

								if (save) {
									Save();
								}
							}

						}



						PendingOutput.AddRange(CheckAlerts(user));

						if (!target.StartsWith("#") && user != admin) {
							PendingOutput.Add(SendMessage(admin, user + ": " + originalMessage));
						}


						break;
				}




			}

		}
		catch (Exception ex) {
			PendingOutput.Add(SendMessage(admin, ex.Message + " (" + originalMessage + ")"));
			Console.WriteLine(data);
			Console.WriteLine(ex.ToString());
		}

		for (int iOutput = PendingOutput.Count - 1; iOutput >= 0; iOutput--) {
			if (PendingOutput[iOutput].Contains(":")) {
				string[] pieces = PendingOutput[iOutput].Split(':');
				if (
					(pieces[1].Trim().StartsWith("!") || pieces[1].Trim().StartsWith("."))
					|| (PendingOutput[iOutput].ToLower().Contains("https://www.youtube.com/watch?v=kfVsfOSbJY0".ToLower()))
				) {
					PendingOutput.RemoveAt(iOutput);
				}
			}
		}

		PendingOutput = PendingOutput.Distinct().ToList();
		

	}



	public void LoadHSData() {

		hsData = new DataTable("HSData");
		hsData.Columns.Add("Name");
		hsData.Columns.Add("Class");
		hsData.Columns.Add("Rarity");
		hsData.Columns.Add("Type");
		hsData.Columns.Add("Race");
		hsData.Columns.Add("Cost");
		hsData.Columns.Add("Attack");
		hsData.Columns.Add("Health");
		hsData.Columns.Add("Description");
		hsData.Columns.Add("Name lower");
		hsData.Columns.Add("Collectible");
		
		/* http://www.hearthhead.com/cards
		http://www.hearthhead.com/cards?filter=uc=on */
		string[] urls = new string[] { "http://www.hearthhead.com/cards", "http://www.hearthhead.com/cards?filter=uc=on" };
		List<Card> cards = new List<Card>();


		string[] data1 = wc.DownloadString("http://www.hearthhead.com/cards").Split('\n');
		string datastr1 = "";
		for (int i = 0; i < data1.Length; i++) {
			if (data1[i].Contains("var hearthstoneCards")) {
				datastr1 = data1[i];
			}
		}
		int start = datastr1.IndexOf("var hearthstoneCards");
		start = datastr1.IndexOf("[", start);
		int end = datastr1.IndexOf(";new", start);

		string json = datastr1.Substring(start, end - start);

		List<Card> tmp = JsonConvert.DeserializeObject<List<Card>>(json);
		foreach (Card crd in tmp) {
			crd.Collectible = true;
		}
		cards.AddRange(tmp);

		data1 = wc.DownloadString("http://www.hearthhead.com/cards?filter=uc=on").Split('\n');
		datastr1 = "";
		for (int i = 0; i < data1.Length; i++) {
			if (data1[i].Contains("var hearthstoneCards")) {
				datastr1 = data1[i];
			}
		}
		start = datastr1.IndexOf("var hearthstoneCards");
		start = datastr1.IndexOf("[", start);
		end = datastr1.IndexOf(";new", start);

		json = datastr1.Substring(start, end - start);

		tmp = JsonConvert.DeserializeObject<List<Card>>(json);
		foreach (Card crd in tmp) {
			crd.Collectible = false;
		}
		cards.AddRange(tmp);



		Debug.WriteLine("---Starting Hearthstone Update----");
		string[] classes = new string[] { "1", "2", "3", "4", "5", "7", "8", "9", "11" };
		string[] types = new string[] { "4", "5", "7" };
		string[] qualities = new string[] { "0", "1", "2", "3", "4", "5" };
		int row = 0;
		foreach (Card c in cards) {
			string issue = "";
			if (c.name == null) {
				issue += "Missing name. ";
			}
			if (c.classs != null && !classes.Contains(c.classs)) {
				issue += "Invalid class (" + (c.classs == null ? "null" : c.classs) + "). ";
			}
			if (c.type == null || !types.Contains(c.type)) {
				issue += "Invalid type. ";
			}
			if (c.quality == null || !qualities.Contains(c.quality)) {
				issue += "Invalid quality. ";
			}


			if (issue != "") {
				Debug.WriteLine(">id " + c.id + ": " + issue + " ");
			}
			else {
				try {
					DataRow dr = hsData.NewRow();
					dr["Name"] = c.name;
					dr["Name lower"] = c.name.ToLower();
					dr["Class"] = (c.classs == null ? "All" :
						c.classs == "1" ? "Warrior" :
						c.classs == "2" ? "Paladin" :
						c.classs == "3" ? "Hunter" :
						c.classs == "4" ? "Rogue" :
						c.classs == "5" ? "Priest" :
						c.classs == "7" ? "Shaman" :
						c.classs == "8" ? "Mage" :
						c.classs == "9" ? "Warlock" :
						c.classs == "11" ? "Druid" :
						"Unknown");
					dr["Rarity"] = (c.quality == "0" ? "Basic" :
						c.quality == "1" ? "Common" :
						c.quality == "3" ? "Rare" :
						c.quality == "4" ? "Epic" :
						c.quality == "5" ? "Legendary" :
						"Unknown");
					dr["Type"] = (c.type == "4" ? "Minion" :
						c.type == "5" ? "Spell" :
						c.type == "7" ? "Weapon" :
						"Unknown");
					dr["Race"] = (c.race == null ? "None" :
						c.race == "14" ? "Murloc" :
						c.race == "15" ? "Demon" :
						c.race == "17" ? "Mech" :
						c.race == "20" ? "Beast" :
						c.race == "21" ? "Totem" :
						c.race == "23" ? "Pirate" :
						c.race == "24" ? "Dragon" :
						"Unknown");
					dr["Cost"] = c.cost ?? "0";
					dr["Attack"] = c.attack ?? "0";
					dr["Health"] = c.type == "7" ? c.durability ?? "0" : c.health ?? "0";
					dr["Description"] = c.description ?? "";
					dr["Collectible"] = c.Collectible.ToString();
					hsData.Rows.Add(dr);
				}
				catch (Exception ex) {
					PendingOutput.Add(SendMessage(admin, ex.Message));

				}
			}
			row++;
		}

	}

	public List<DataRow> CardSearch(string term, int page) {

		return (from DataRow drTemp in hsData.Rows where 
					(drTemp["Name lower"].ToString().Contains(term) ||
					drTemp["Race"].ToString().ToLower().Contains(term) ||
					drTemp["Description"].ToString().ToLower().Contains(term))
				orderby drTemp["Name"].ToString()
				select drTemp).ToList();
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Hearthstone card lookup.")]
	public string card(string target, string user, string msg) {
		string term = "", ret = "";
		try {
			int page = 1, pageSize = 10;
			bool search = false;
			string[] parts = msg.Split(' ');
			
			if (Regex.Match(parts[0], @"p\d+").Success) {

				if (parts.Length == 1) {
					return SendMessage(target, "Please provide a search term.");
				}
				else {
					search = true;
					for (int i = 1; i < parts.Length; i++) {
						term += parts[i] + " ";
					}
					term = term.TrimEnd(' ');
					page = int.Parse(Regex.Match(parts[0], @"\d+").Value);
				}
			}
			else {
				term = msg.ToLower();
			}

			List<DataRow> drs = CardSearch(term, page);
			int exactMatch = (from DataRow r2 in drs where r2["Name lower"].ToString() == term select r2).Count();
			bool hasExact = exactMatch >= 1 && !search;

			if (exactMatch > 1) {
				if (drs.Where(r3 => r3["Description"].ToString().ToLower().Contains("choose one:")).Count() == 1) {
					for (int i = drs.Count-1; i >= 0; i--) {
						if (!drs[i]["Description"].ToString().ToLower().Contains("Choose one:")) {
							drs.RemoveAt(i);
						}
					}
				}
			}

			if (drs.Count == 0) {
				ret= "No matches.";
			}
			else if (drs.Count > 1 && !hasExact) {
				ret = drs.Count.ToString() + " results.";
				ret += " Page " + page.ToString() + " of " + Math.Round(Math.Ceiling((double)drs.Count / (double)pageSize), 0).ToString() + ": ";
				int start = (page - 1) * pageSize,
					end = Math.Min(drs.Count, page * pageSize);
				for (int i = start; i < end; i++) {
					ret += drs[i]["Name"].ToString() + ", ";
				}
				ret = ret.Substring(0, ret.Length - 2);
			}
			else {
				try {
					DataRow dr = hasExact ? (from DataRow r2 in drs where r2["Name lower"].ToString() == term select r2).First() : drs[0];
					switch (dr["Type"].ToString()) {
						case "Minion":
							ret = string.Format("{0} ({1} {7}, class: {2}): {3} mana, {4}/{5} - {6}",
								dr["Name"],
								dr["Rarity"].ToString() + (bool.Parse(dr["Collectible"].ToString()) ? "" : " uncollectible"),
								dr["Class"],
								dr["Cost"],
								dr["Attack"],
								dr["Health"],
								dr["Description"],
								dr["Race"].ToString() == "None" ? "minion" : dr["Race"].ToString()
								);
							break;
						case "Spell":
							ret = string.Format("{0} ({1} spell, class: {2}): {3} mana - {4}",
								dr["Name"],
								dr["Rarity"].ToString() + (bool.Parse(dr["Collectible"].ToString()) ? "" : " uncollectible"),
								dr["Class"],
								dr["Cost"],
								dr["Description"]
								);
							break;
						case "Weapon":
							ret = string.Format("{0} ({1} weapon, class: {2}): {3} mana, {4}/{5} - {6}",
								dr["Name"],
								dr["Rarity"].ToString() + (bool.Parse(dr["Collectible"].ToString()) ? "" : " uncollectible"),
								dr["Class"],
								dr["Cost"],
								dr["Attack"],
								dr["Health"],
								dr["Description"]
								);

							break;
					}
				}
				catch {
					ret = "I don't know that card.";
				}
			}
		}
		catch (Exception ex) {
			ret = ex.Message;
		}
		return SendMessage(target, ret);
	}

	public List<string> CheckAlerts(string user) {
		List<string> ret = new List<string>();
		if (AlertUsers.Contains(user.ToLower())) {
			for (int i = dsData.Tables["Alerts"].Rows.Count - 1; i >= 0; i--) {
				if (dsData.Tables["Alerts"].Rows[i]["TargetUser"].ToString().ToLower() == user.ToLower()) {
					ret.Add(SendMessage(user, dsData.Tables["Alerts"].Rows[i]["Message"].ToString()));
					dsData.Tables["Alerts"].Rows.RemoveAt(i);
				}
			}
			AlertUsers.Remove(user.ToLower());
			Save();
		}
		return ret;
	}


	public int GetRowIndex(string table, string column, string value, bool matchCase) {
		int ret = -1;
		if (matchCase) {
			value = value.ToLower();
		}
		if (dsData.Tables.Contains(table)) {
			if (dsData.Tables[table].Columns.Contains(column)) {
				for (int i = 0; i < dsData.Tables[table].Rows.Count; i++) {
					if ((matchCase ? dsData.Tables[table].Rows[i][column].ToString() : dsData.Tables[table].Rows[i][column].ToString().ToLower()) == value) {
						ret = i;
					}
				}
			}
		}
		return ret;
	}


	public void CreateTable(string name, string[] columns) {
		if (!dsData.Tables.Contains(name)) {
			DataTable dt = new DataTable(name);
			for (int i = 0; i < columns.Length; i++) {
				dt.Columns.Add(columns[i]);
			}
			dsData.Tables.Add(dt);
			Save();
		}
	}


	public bool IsOp(string channel) {
		int idx = GetRowIndex("ChannelSettings", "IsOp", channel, false);
		return bool.Parse(dsData.Tables["ChannelSettings"].Rows[idx]["IsOp"].ToString());
	}


	public string[] Encrypt(string message, string mode) {
		var engine = new Jurassic.ScriptEngine();
		engine.Evaluate(CryptoJS);
		string key = engine.Evaluate("CryptoJS.MD5(CryptoJS.lib.WordArray.randomWord(16).toString(CryptoJS.enc.Hex)).toString(CryptoJS.enc.Hex);").ToString();
		string result = engine.Evaluate("var msg = '" + message.Replace("'", "\\'") + "'; CryptoJS.AES.encrypt(msg, '" + key + "');").ToString();

		HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://cryptbin.com/api/create/");
		req.Method = "POST";
		req.ContentType = "application/x-www-form-urlencoded";
		string post = "message=" + HttpUtility.UrlEncode(result) + "&api_key=LCYqLMiPkTPtNABHZtjbji5X45zIwurwF2V6zMV4wM5d3pXwZOlUDQOSSdKGQ4w5&options[mode]=" + mode + "&options[expire]=0";
		byte[] byteArr = Encoding.UTF8.GetBytes(post);
		req.ContentLength = byteArr.Length;

		Stream str = req.GetRequestStream();
		str.Write(Encoding.UTF8.GetBytes(post), 0, byteArr.Length);
		str.Close();

		WebResponse resp = req.GetResponse();

		str = resp.GetResponseStream();
		StreamReader sr = new StreamReader(str);
		string response = sr.ReadToEnd();
		sr.Close();
		str.Close();
		resp.Close();

		dynamic jsResult = JsonConvert.DeserializeObject(response);

		string[] ret;

		if (jsResult.error != null) {
			ret = new string[] { "error: " + jsResult.error };
		}
		else {
			ret = new string[] { "https://cryptbin.com/" + jsResult.id + "#" + key, jsResult.delete };

		}
		engine = null;

		return ret;
	}


	public void Save() {
		
		CreateTable("Karma", new string[] { "User", "Score", "Channel" });
		dsData.Tables["Karma"].Rows.Clear();
		foreach (KarmaObject k in KarmaList) {
			DataRow dr = dsData.Tables["Karma"].NewRow();
			dr["User"] = k.ObjectName;
			dr["Score"] = k.Score;
			dr["Channel"] = k.Channel;
			dsData.Tables["Karma"].Rows.Add(dr);
		}
		dsData.WriteXml(Environment.CurrentDirectory + "\\data.xml");

	}


	public string SendMessage(string target, string msg) {
		if (msg.StartsWith("/me")) {
			return "PRIVMSG " + target + " :" + "\x01" + "ACTION" + msg.Replace("/me", "") + "\x01";
		}
		else {
			return "PRIVMSG " + target + " :" + msg;
		}
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "List commands I know, or get details on a specific command.")]
	public string help(string target, string user, string msg) {
		MethodInfo[] methods = (typeof(CommandParser)).GetMethods();
		methods = (from MethodInfo m in methods where m.GetCustomAttributes(typeof(CommandSettings), false).Count() > 0 select m).ToArray();
		string output = "";
		if (msg == "") {
			output = "Commands I know: ";
			List<string> methodNames = new List<string>();
			foreach (MethodInfo mi in methods) {
				if (((CommandSettings)mi.GetCustomAttributes(typeof(CommandSettings), false)[0]).Public) {
					methodNames.Add(mi.Name.TrimStart("_".ToCharArray()));
				}
			}
			methodNames.Sort();
			output += string.Join("  ", methodNames.ToArray()) + ". Use !help <command> for more information.";
		}
		else {
			foreach (MethodInfo mi in methods) {
				if (msg.ToLower() == mi.Name.TrimStart("_".ToCharArray()).ToLower()) {

					output = ((CommandSettings)mi.GetCustomAttributes(typeof(CommandSettings), false)[0]).HelpText;

					break;
				}
			}

		}
		return SendMessage(target, output);
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "List commands I know, or get details on a specific command.")]
	public string halp(string target, string user, string msg) {
		return help(target, user, msg);
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Take your chances with the magical, fantastical 8 ball!")]
	public string _8ball(string target, string user, string msg) {
		Random R = new Random();
		return SendMessage(target, dsData.Tables["Magic8Ball"].Rows[R.Next(dsData.Tables["Magic8Ball"].Rows.Count)]["Answer"].ToString());
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "There's always a relevant XKCD.")]
	public string xkcd(string target, string user, string msg) {
		return g(target, user, "site:http://xkcd.com " + msg)[0];
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Set an alert for when an idle user returns. Usage: !alert [target user] <message>")]
	public string alert(string target, string user, string msg) {
		List<string> parts = msg.Split(' ').ToList();

		if (parts.Count < 2) {
			return SendMessage(target, "Not enough parameters.");
		}
		else {
			CreateTable("Alerts", new string[] { "TargetUser", "Owner", "Message" });


			DataRow dr = dsData.Tables["Alerts"].NewRow();
			dr["TargetUser"] = parts[0];
			dr["Owner"] = user;

			if (!AlertUsers.Contains(parts[0].ToLower()))
				AlertUsers.Add(parts[0].ToLower());

			parts.RemoveAt(0);
			dr["Message"] = "(" + target + ") " + user + ": " + string.Join(" ", parts.ToArray());
			dsData.Tables["Alerts"].Rows.Add(dr);

			Save();
			return SendMessage(target, user + ": Alert set.");

		}
	}



	/*alias plans
	 *	!alias set [keyword] [message]
	 *		only one entry
	 *		owned by the creator, can only be removed or changed by creator or admin
	 *		channel preference
	 *		
	 *	!alias addlist [listname] [first message]
	 *		creates an open alias list that anyone can add to
	 *		channel-agnostic
	 *		
	 *	!alias add [list] [message]
	 *		adds an item to a list
	 *		
	 *	!alias remove [item]
	 *		owner of a set alias or list can remove it
	 *		
	 *	!alias remove [list] [item]
	 *		owner of a list can remove an item from that list
	 * 
	 * */

	[CommandSettings(Public = true, Level = 0, HelpText = "Usage: !alias [add|remove] [keyword] <message>")]
	public string alias(string target, string user, string msg) {
		List<string> parts = msg.Split(' ').ToList();
		string output = "";

		bool isAdmin = false;
		if (AuthedUsers.ContainsKey(user.ToLower())) {
			if (AuthedUsers[user.ToLower()].IsAuthed && AuthedUsers[user.ToLower()].Level >= 1) {
				isAdmin = true;
			}
		}
		if (parts[0].ToLower() == "add") {
			if (parts.Count > 2) {
				string kwd = parts[1].TrimStart("!".ToCharArray());
				bool add = true;
				try {
					if (!dsData.Tables.Contains("Alias")) {
						DataTable dt = new DataTable("Alias");
						dt.Columns.Add("Keyword");
						dt.Columns.Add("Message");
						dt.Columns.Add("Owner");
						dt.Columns.Add("Channel");
						dsData.Tables.Add(dt);
					}
					else {
						for (int i = 0; i < dsData.Tables["Alias"].Rows.Count; i++) {
							if (dsData.Tables["Alias"].Rows[i]["Keyword"].ToString().ToLower() == kwd.ToLower() && dsData.Tables["Alias"].Rows[i]["Channel"].ToString() == target) {
								if (dsData.Tables["Alias"].Rows[i]["Owner"].ToString().ToLower() == user.ToLower()) {
									dsData.Tables["Alias"].Rows.RemoveAt(i);
								}
								else {
									add = false;
									output = "Cannot replace alias owned by " + dsData.Tables["Alias"].Rows[i]["Owner"].ToString();
								}
								
								break;
							}
						}

					}
					if (add) {
						int idx = parts[0].Length + parts[1].Length + 2;
						DataRow dr = dsData.Tables["Alias"].NewRow();
						dr["Keyword"] = kwd.ToLower();
						dr["Owner"] = user;
						dr["Message"] = msg.Substring(idx);
						dr["Channel"] = target;
						dsData.Tables["Alias"].Rows.Add(dr);
						Save();

						output = "Alias added.";
					}
				}
				catch (Exception ex) {
					output = ex.Message;
				}
			}
			else {
				output = SYNTAX_ERROR;
			}
		}
		else if (parts[0].ToLower() == "remove") {
			string kwd = parts[1].TrimStart("!".ToCharArray());

			for (int i = 0; i < dsData.Tables["Alias"].Rows.Count; i++) {
				if (dsData.Tables["Alias"].Rows[i]["Keyword"].ToString().ToLower() == kwd.ToLower()
						&& (dsData.Tables["Alias"].Rows[i]["Channel"].ToString() == target)
						&& (
							dsData.Tables["Alias"].Rows[i]["Owner"].ToString().ToLower() == user.ToLower() || isAdmin
							)
						) {
					dsData.Tables["Alias"].Rows.RemoveAt(i);
					Save();
					output = "Alias removed.";
					break;
				}
			}
		}
		else if (parts[0].ToLower() == "random") {
			Random R = new Random();
			int idx = R.Next(dsData.Tables["Alias"].Rows.Count);
			DataRow dr=dsData.Tables["Alias"].Rows[idx];
			return SendMessage(target, "(" + dr["Keyword"].ToString() + ") " + dr["Message"].ToString());
		}
		else {
			output = SYNTAX_ERROR;
		}
		return SendMessage(target, output);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Info about me.")]
	public string info(string target, string user, string msg) {
		return SendMessage(target, "I am timebot 2.3, created by timeshifter to take over the wo*cough* I mean, to serve and protect.");
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Be nice and say hello.")]
	public string hello(string target, string user, string msg) {
		return SendMessage(target, "Greetings, " + user + "!");
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search google.com. Usage: !g <query>")]
	public List<string> g(string target, string user, string msg) {
		List<string> ret = new List<string>();
		/*string json = wc.DownloadString("http://ajax.googleapis.com/ajax/services/search/web?v=1.0&q=" + HttpUtility.UrlEncode(msg).TrimEnd('+'));*/
		string json = wc.DownloadString("https://www.googleapis.com/customsearch/v1?key=AIzaSyAZHQtN9tSr8FX7bvgvY7JDSLn3eA4nJFM&cx=009646413115213141219:ge2xcjigsda&q=" + HttpUtility.UrlEncode(msg).TrimEnd('+'));
		dynamic jsResult = JsonConvert.DeserializeObject(json);
		int ct = 0;
		foreach (dynamic d in jsResult.items) {
			ret.Add(SendMessage(target, HttpUtility.HtmlDecode(HttpUtility.UrlDecode(d.title.Value)) + " - " + HttpUtility.UrlDecode(d.link.Value)));
			if (++ct == 3) {
				break;
			}
		}
		return ret;
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for results from stackoverflow.com. Usage: !so <query>")]
	public List<string> so(string target, string user, string msg) {
		return g(target, user, "site:stackoverflow.com " + msg);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for results from developer.mozilla.org. Usage: !mdn <query>")]
	public List<string> mdn(string target, string user, string msg) {
		return g(target, user, "site:developer.mozilla.org " + msg);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "lern2google")]
	public string lmgtfy(string target, string user, string msg) {
		return SendMessage(target, "http://lmgtfy.com/?q=" + HttpUtility.UrlEncode(msg).TrimEnd('+'));
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for results from youtube.com. Usage: !yt <query>")]
	public List<string> yt(string target, string user, string msg) {
		return g(target, user, "site:youtube.com " + msg);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for results from en.wikipedia.org. Usage: !wiki <query>")]
	public List<string> wiki(string target, string user, string msg) {
		return g(target, user, "site:en.wikipedia.org " + msg);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for results from imdb.com. Usage: !imdb <query>")]
	public List<string> imdb(string target, string user, string msg) {
		return g(target, user, "site:imdb.com " + msg);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Define a word. Source: dictionary.com. Usage: !define <query>")]
	public string define(string target, string user, string msg) {
		string ret = "";

		XDocument xdoc = XDocument.Load("http://www.dictionaryapi.com/api/v1/references/collegiate/xml/" + msg + "?key=0a47eca3-b27c-42bb-ac6c-d7787a56c8dc");
		List<XElement> xe = (from x in xdoc.Descendants("entry") select x).ToList();
		if (xe.Count == 0) {
			xe = (from x in xdoc.Descendants("suggestion") select x).ToList();
			if (xe.Count == 0) {
				ret = "No matches or suggestions.";
			}
			else {
				ret = "Suggestions: ";
				for (int i = 0; i < 10 && i < xe.Count; i++) {
					ret += xe[i].Value + "  ";
				}
			}
		}
		else {
			ret = xe.First().Descendants("ew").First().Value + ": ";
			foreach (XElement x in xe) {
				ret += x.Descendants("fl").First().Value + " ";
				foreach (XElement x2 in x.Descendants("def").Elements()) {
					if (x2.Name.LocalName == "date") {
						ret += x2.Value + ", ";
					}
					else if (x2.Name.LocalName == "sn") {
						ret += x2.Value + ": ";
					}
					else if (x2.Name.LocalName == "dt") {
						ret += x2.Value.Trim(":".ToCharArray()) + ". ";
					}
				}
			}
		}
		return SendMessage(target, ret);
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Query WolframAlpha.com")]
	public string wolfram(string target, string user, string msg) {
		/* app-id: QEQ42U-HA7RRR8J6P */
		try {
			XDocument xdoc = XDocument.Load("http://api.wolframalpha.com/v2/query?appid=QEQ42U-HA7RRR8J6P&input=" + HttpUtility.UrlEncode(msg) + "&format=plaintext");
			List<XElement> pods = (from x in xdoc.Descendants("pod") select x).ToList();
			string input = "", result = "";
			foreach (XElement x in pods) {
				if (x.Attribute("id").Value == "Input") {
					input = x.Descendants("plaintext").First().Value.Split("\n".ToCharArray())[0];
				}
				else if (x.Attribute("id").Value == "Result") {
					result = x.Descendants("plaintext").First().Value.Split("\n".ToCharArray())[0];
				}
			}

			return SendMessage(target, string.Format("Input interpretation: {0}; result: {1}", input, result));


		}
		catch (Exception ex) {
			return SendMessage(target, "An error ocurred.");
		}

	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Specify languages you're familiar with (!langs set <item1> <item2> ...), add a proficiency (!langs add <item>), or look up a user's proficiencies (!langs <user>)")]
	public string langs(string target, string user, string msg) {
		List<string> parts = msg.Split(' ').ToList();
		if (parts.Count == 0 || parts[0].Trim() == "") {
			return langs(target, user, user);

		}
		else {
			if (parts[0].ToLower() == "set") {
				parts.RemoveAt(0);
				if (!dsData.Tables.Contains("UserLangs")) {
					DataTable dt = new DataTable("UserLangs");
					dt.Columns.Add("User");
					dt.Columns.Add("Lang");
					dsData.Tables.Add(dt);
				}
				for (int i = dsData.Tables["UserLangs"].Rows.Count - 1; i >= 0; i--) {
					if (dsData.Tables["UserLangs"].Rows[i]["User"].ToString().ToLower() == user.ToLower()) {
						dsData.Tables["UserLangs"].Rows.RemoveAt(i);

					}
				}
				foreach (string s in parts) {
					DataRow dr = dsData.Tables["UserLangs"].NewRow();
					dr["User"] = user.Trim();
					dr["Lang"] = s.Trim(", ".ToCharArray());
					dsData.Tables["UserLangs"].Rows.Add(dr);
				}
				Save();
				return SendMessage(target, "Languages set.");
			}
			else if (parts[0].ToLower() == "add") {
				parts.RemoveAt(0);
				if (!dsData.Tables.Contains("UserLangs")) {
					DataTable dt = new DataTable("UserLangs");
					dt.Columns.Add("User");
					dt.Columns.Add("Lang");
					dsData.Tables.Add(dt);
				}
				for (int i = dsData.Tables["UserLangs"].Rows.Count - 1; i >= 0; i--) {
					if (dsData.Tables["UserLangs"].Rows[i]["User"].ToString().ToLower() == user.ToLower()) {
						if (dsData.Tables["UserLangs"].Rows[i]["Lang"].ToString().ToLower() == parts[0].ToLower()) {
							return SendMessage(target, user + ", you have already specified that proficiency.");
						}

					}
				}
				DataRow dr = dsData.Tables["UserLangs"].NewRow();
				dr["User"] = user.Trim();
				dr["Lang"] = parts[0].Trim(", ".ToCharArray());
				dsData.Tables["UserLangs"].Rows.Add(dr);
				Save();
				return SendMessage(target, user + ", proficiency added.");

			}
			else if (parts[0].ToLower() == "remove") {

				return SendMessage(target, "Not implemented.");
			}
			else {
				List<string> langs = new List<string>();
				for (int i = 0; i < dsData.Tables["UserLangs"].Rows.Count; i++) {
					if (dsData.Tables["UserLangs"].Rows[i]["User"].ToString().ToLower() == parts[0].ToLower()) {
						langs.Add(dsData.Tables["UserLangs"].Rows[i]["Lang"].ToString());
					}
				}
				return SendMessage(target, parts[0] + " is proficient in: " + string.Join(" ", langs.ToArray()));
			}
		}
	}

	[CommandSettings(Public = false, Level = 0, HelpText = "no")]
	public string told(string target, string user, string msg) {
		return SendMessage(target, "damnit dtzitz");
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Roll some dice! ex: !roll 2d12")]
	public string roll(string target, string user, string msg) {
		string[] parts = msg.Split(' ');
		string ret = "";
		try {
			if (parts.Length == 0) {
				ret = "Incorrect syntax. Try \"!roll 2d12\"";
			}
			else {
				string[] vals = parts[0].ToLower().Split('d');
				if (vals.Length != 2) {
					ret = "Incorrect syntax. Try \"!roll 2d12\"";
				}
				else {
					int qty = 1, size = 1;
					if (vals[0] == "") {
						size = int.Parse(vals[1]);
					}
					else {
						qty = int.Parse(vals[0]);
						size = int.Parse(vals[1]);
					}
					bool showResults = true;
					if (qty > 1000) {
						ret = "I can't roll more than 1,000 die at once.";
					}
					else {
						if (qty > 10) {
							showResults = false;
						}
						Random R = new Random();
						int total = 0;
						string output = user + " rolled " + qty.ToString() + "d" + size.ToString() + " and got {0:N0}.";
						if (showResults) {
							output += " (";
						}
						for (int i = 0; i < qty; i++) {
							int j = R.Next(size) + 1;
							total += j;
							if (showResults) {
								output += j.ToString() + ", ";
							}
							System.Threading.Thread.Sleep(1);
						}
						output = string.Format(output, total);
						if (showResults) {
							output = output.Substring(0, output.Length - 2) + ")";
						}
						ret = output;
					}
				}
			}
		}
		catch (Exception ex) {
			ret = "Error: " + ex.Message;
		}
		return SendMessage(target, ret);
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Search for users listed as familiar with a specified language (!langhelp <language>)")]
	public string langhelp(string target, string user, string msg) {
		if (msg.Trim() == "") {
			return SendMessage(target, "No language specified.");
		}
		else {
			string lang = msg.Split(' ')[0].Trim(" ,".ToCharArray()).ToLower();
			List<string> output = new List<string>();
			if (dsData.Tables.Contains("UserLangs")) {
				for (int i = 0; i < dsData.Tables["UserLangs"].Rows.Count; i++) {
					if (dsData.Tables["UserLangs"].Rows[i]["Lang"].ToString().ToLower().Trim(" ,".ToCharArray()) == lang) {
						output.Add(dsData.Tables["UserLangs"].Rows[i]["User"].ToString());
					}
				}
				if (output.Count > 0) {
					output = output.Distinct().ToList();
					output.Sort();
					return SendMessage(target, "Users proficient in " + lang + ": " + string.Join(" ", output.ToArray()));
				}
				else {
					return SendMessage(target, "No data found.");
				}
			}
			else {
				return SendMessage(target, "No data found.");
			}
		}
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "It's a coin.")]
	public string flip(string target, string user, string msg) {
		Random R = new Random();
		return SendMessage(target, user + " flipped " + (R.Next(2) == 0 ? "heads." : "tails."));
	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Display my local time.")]
	public string time(string target, string user, string msg) {
		return SendMessage(target, "My local time is " + DateTime.Now.ToShortTimeString() + " (" + DateTime.UtcNow.ToShortTimeString() + " UTC)");
	}


	[CommandSettings(Public = false, Level = 0, HelpText = "Totally rekt.")]
	public string rekt(string target, string user, string msg) {
		Random R = new Random();
		return SendMessage(target, "☐Not REKT ☐REKT ☑" + dsData.Tables["Rekt"].Rows[R.Next(dsData.Tables["Rekt"].Rows.Count)]["Value"].ToString());
	}


	[CommandSettings(Public = false, Level = 0, HelpText = "Authenticate to use secure commands.")]
	public string auth(string target, string user, string msg) {
		return SendMessage("nickserv", "acc " + user);
	}


	[CommandSettings(Public = false, Level = 0, HelpText = "List authorized users.")]
	public string whoisauthed(string target, string user, string msg) {
		return SendMessage(target, AuthedUsers.Where(u=>u.Value.IsAuthed).Count() > 0 ? string.Join(", ", AuthedUsers.Where(u=>u.Value.IsAuthed).Select(u=>u.Key).ToArray()) : "Nobody is currently authed.");
	}



	[CommandSettings(Public = true, Level = 0, HelpText = "Rules for the aspiring Ferengi to live by.")]
	public string roa(string target, string user, string msg) {
		Random R = new Random();
		return SendMessage(target, dsData.Tables["RulesOfAcquisition"].Rows[R.Next(dsData.Tables["RulesOfAcquisition"].Rows.Count)]["Rule"].ToString());

	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Dude.")]
	public string bro(string target, string user, string msg) {
		Random R = new Random();
		return SendMessage(target, dsData.Tables["Bros"].Rows[R.Next(dsData.Tables["Bros"].Rows.Count)]["Value"].ToString());

	}


	[CommandSettings(Public = true, Level = 0, HelpText = "Set a reminder for yourself. Time format: #d#h#m#s, at least one time unit required. If no unit is specified, minutes will be used. Usage: !remindme [time from now] <message>")]
	public string remindme(string target, string user, string msg) {
		int idx = msg.IndexOf(' ');
		if (idx == -1) {
			return "Invalid syntax";
		}

		string time = msg.Split(" ".ToCharArray())[0].ToLower();
		int dys = 0, hrs = 0, mins = 0, secs = 0;
		bool hasUnit = false;
		if (time.Contains("d")) {
			string t = time.Split("d".ToCharArray())[0];
			time = time.Split("d".ToCharArray())[1];
			dys = int.Parse(t);
			hasUnit = true;
		}
		if (time.Contains("h")) {
			string t = time.Split("h".ToCharArray())[0];
			time = time.Split("h".ToCharArray())[1];
			hrs = int.Parse(t);
			hasUnit = true;
		}
		if (time.Contains("m")) {
			string t = time.Split("m".ToCharArray())[0];
			time = time.Split("m".ToCharArray())[1];
			mins = int.Parse(t);
			hasUnit = true;
		}
		if (time.Contains("s")) {
			string t = time.Split("s".ToCharArray())[0];
			time = time.Split("s".ToCharArray())[1];
			secs = int.Parse(t);
			hasUnit = true;
		}
		if (!hasUnit) {
			mins = int.Parse(time);
		}
		msg = msg.Substring(idx).Trim();
		CreateTable("Reminders", new string[] { "User", "Time", "Message" });
		DataRow dr = dsData.Tables["Reminders"].NewRow();
		dr["User"] = user;
		dr["Message"] = msg;
		dr["Time"] = DateTime.Now.AddSeconds(secs).AddMinutes(mins).AddHours(hrs).AddDays(dys).ToString();
		dsData.Tables["Reminders"].Rows.Add(dr);
		Save();

		return SendMessage(target, "Reminder set.");
	}

	[CommandSettings(Public=true, Level=0, HelpText="Returns how long I've been running.")]
	public string uptime(string target, string user, string msg) {
		return "UPTIME " + target;
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Learn things you never knew you wanted to know!")]
	public string trivia(string target, string user, string msg) {
		bool err = true;
		string ret = "";
		while (err) {
			try {
				string json = wc.DownloadString("http://mentalfloss.com/api/1.0/views/amazing_facts.json?limit=1");
				dynamic jsResult = JsonConvert.DeserializeObject(json);
				ret= SendMessage(target, Regex.Replace(jsResult[0].nid.Value.Replace("\\n", "").Replace("<p>", "").Replace("</p>", ""), "<.*?>", string.Empty));
				err = false;
			}
			catch {
				
			}
		}
		return ret;
	}
	
	[CommandSettings(Public = true, Level = 0, HelpText = "You probably deserved it.")]
	public string karma(string target, string user, string msg) {
		int id = -1;
		string ret = "";
		string[] parts = msg.Split(' ');
		if (parts.Length >= 1) {
			if (parts[0].ToLower() == "top") {
				KarmaList.Sort();
				bool goodInt = false;
				int topNum = 0;
				if (parts.Length == 3) {
					goodInt = int.TryParse(parts[1], out topNum);
				}
				if (parts.Length == 2 || !goodInt) {
					foreach (KarmaObject k in KarmaList) {
						if (k.Channel == target) {
							ret = "Top karma is \"" + k.ObjectName + "\" with a score of " + k.Score.ToString() + ".";
							break;
						}
					}
				}
				else if (goodInt) {
					if (topNum > 20) {
						ret = "Please specify a number less than or equal to 20.";
					}
					else {
						ret = "Top karma: ";
						foreach (KarmaObject k in KarmaList) {
							if (k.Channel == target) {
								ret += k.ObjectName + ": " + k.Score.ToString();
								topNum--;
								if (topNum > 0) {
									ret += ", ";
								}
								else {
									break;
								}
							}
						}
					}
				}

			}
			else {
				for (int i = 0; i < KarmaList.Count; i++) {
					if (KarmaList[i].ObjectName.ToLower() == parts[0].ToLower() && KarmaList[i].Channel.ToLower() == target) {
						id = i;
					}
				}
				if (id == -1) {
					ret = "No karma given for that yet!";
				}
				else {
					ret = parts[0] + " has " + KarmaList[id].Score.ToString() + " karma.";
				}
			}
		}
		else {
			ret = "Correct usage: !karma [query|top [#]]";
		}
		return SendMessage(target, ret);
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "List recent messages that included your nick.")]
	public void history(string target, string user, string msg) {
		int idx = users.IndexOf(users.Where(u => u.UserName.ToLower() == user.ToLower()).First());
		if (users[idx].History.Count == 0) {
			PendingOutput.Add(SendMessage(user, "No messages logged."));
		}
		else {
			foreach (HistoryObject h in users[idx].History) {
				TimeSpan ts = DateTime.Now - h.Timestamp;

				PendingOutput.Add(SendMessage(user, string.Format("{0}{1}{2}{3}",
						ts.Days > 0 ? ts.Days.ToString() + "d " : "",
						ts.Hours > 0 ? ts.Hours.ToString() + "h " : "",
						ts.Minutes > 0 ? ts.Minutes.ToString() + "m " : "",
						ts.Seconds.ToString() + "s"
						) + " ago: " + h.Message));
			}
			UserObject us = users[idx];
			us.History = new List<HistoryObject>();
			users[idx] = us;
		}
	}

	[CommandSettings(Public = true, Level = 0, HelpText = "Show the last time I saw a user.")]
	public void seen(string target, string user, string msg) {
		int id = -1;
		for (int i = 0; i < users.Count; i++) {
			if (users[i].UserName.ToLower() == msg.ToLower()) {
				id = i;
			}
		}
		if (id == -1) {
			PendingOutput.Add(SendMessage(target, "I have not seen " + msg + " yet."));
		}
		else {
			UserObject so = users[id];
			string timestr = (so.LastSeenAgo.Days > 0 ? so.LastSeenAgo.Days.ToString() + "d " : "") +
				(so.LastSeenAgo.Hours > 0 ? so.LastSeenAgo.Hours.ToString() + "h " : "") +
				(so.LastSeenAgo.Minutes > 0 ? so.LastSeenAgo.Minutes.ToString() + "m " : "") +
				(so.LastSeenAgo.Seconds > 0 ? so.LastSeenAgo.Seconds.ToString() + "s" : "");
			PendingOutput.Add(SendMessage(target, msg + " (" + timestr + " ago in " + users[id].LastSeenChannel + "): " + users[id].LastSeenMessage));
		}

	}

	[CommandSettings(Public = false, Level = 1, HelpText = "Ignore the specified user.")]
	public string ignore(string target, string user, string msg) {
		CreateTable("IgnoreUsers", new string[] { "User" });
		if (!IgnoredUsers.Contains(msg.ToLower())) {
			DataRow drIgnore = dsData.Tables["IgnoreUsers"].NewRow();
			drIgnore["User"] = msg.ToLower();
			dsData.Tables["IgnoreUsers"].Rows.Add(drIgnore);
			Save();
			IgnoredUsers.Add(msg.ToLower());
			return SendMessage(target, "I will now ignore " + msg);
		}
		else {
			return SendMessage(target, "I am already ignoring " + msg);
		}
	}


	[CommandSettings(Public = false, Level = 1, HelpText = "Unignore the specified user.")]
	public string unignore(string target, string user, string msg) {
		if (IgnoredUsers.Contains(msg.ToLower())) {
			for (int i = 0; i < dsData.Tables["IgnoreUsers"].Rows.Count; i++) {
				if (dsData.Tables["IgnoreUsers"].Rows[i]["User"].ToString().ToLower() == msg.ToLower()) {
					dsData.Tables["IgnoreUsers"].Rows.RemoveAt(i);
					break;
				}
			}
			Save();
			IgnoredUsers.Remove(msg.ToLower());
			return SendMessage(target, "I will no longer ignore " + msg);
		}
		else {
			return SendMessage(target, "I'm not currently ignoring " + msg);
		}
	}


	[CommandSettings(Public = false, Level = 2, HelpText = "Claim or set channel voice.")]
	public List<string> voice(string target, string user, string msg) {
		return new List<string>() {
			string.Format("MODE {0} +v {1}", target, msg.Trim() == "" ? user : msg),
			SendMessage("ChanServ", string.Format("flags {0} {1} +V", target, msg.Trim() == "" ? user : msg))
		};
	}


	[CommandSettings(Public = false, Level = 2, HelpText = "Remove or revoke channel voice.")]
	public List<string> devoice(string target, string user, string msg) {
		return new List<string>() {
			SendMessage("ChanServ", string.Format("flags {0} {1} -V", target, msg.Trim() == "" ? user : msg)),
			string.Format("MODE {0} -v {1}", target, msg.Trim() == "" ? user : msg)
		};
	}

	[CommandSettings(Public = false, Level = 4, HelpText = "Change the channel topic.")]
	public string topic(string target, string user, string msg) {
		return string.Format("TOPIC {0} :{1}", target, msg);
	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Kick a user.")]
	public string kick(string target, string user, string msg) {
		return string.Format("KICK {0} {1}", target, msg);
	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Ban a user.")]
	public List<string> ban(string target, string user, string msg) {
		return new List<string>() {
			string.Format("MODE {0} +b {1}", target, msg),
			string.Format("KICK {0} {1}", target, msg)
		};
	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Unban a user.")]
	public string unban(string target, string user, string msg) {
		return string.Format("MODE {0} -b {1}", target, msg);
	}



	[CommandSettings(Public = false, Level = 5, HelpText = "Mute a user.")]
	public string mute(string target, string user, string msg) {
		return string.Format("MODE {0} +q {1}", target, msg);
	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Unmute a user.")]
	public string unmute(string target, string user, string msg) {
		return string.Format("MODE {0} -q {1}", target, msg);
	}



	[CommandSettings(Public = false, Level = 5, HelpText = "Claim or set channel op status.")]
	public List<string> op(string target, string user, string msg) {
		return new List<string>() {
			string.Format("MODE {0} +o {1}", target, msg.Trim() == "" ? user : msg),
			SendMessage("ChanServ", string.Format("flags {0} {1} +O", target, msg.Trim() == "" ? user : msg))
		};

	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Remove or revoke channel op status.")]
	public List<string> deop(string target, string user, string msg) {
		if (msg.Trim() != "") {
			if (AuthedUsers.ContainsKey(msg.ToLower())) {
				if (AuthedUsers[msg.ToLower()].Level > AuthedUsers[user.ToLower()].Level) {
					return new List<string>() { SendMessage(target, "You are not allowed to deop that person.") };
				}
			}

		}
		return new List<string>() {
			SendMessage("ChanServ", string.Format("flags {0} {1} -O", target, msg.Trim() == "" ? user : msg)),
			string.Format("MODE {0} -o {1}", target, msg.Trim() == "" ? user : msg)
		};
	}


	[CommandSettings(Public = false, Level = 5, HelpText = "Returns the number of records in the specified table.")]
	public string tablecount(string target, string user, string msg) {
		if (dsData.Tables.Contains(msg)) {
			return SendMessage(target, "Table " + msg + " contains " + dsData.Tables[msg].Rows.Count.ToString() + " records.");
		}
		else {
			return SendMessage(target, "Table not found.");
		}
	}


	[CommandSettings(Public = false, Level = 10, HelpText = "Join a channel.")]
	public string join(string target, string user, string msg) {
		return "JOIN " + msg;
	}


	[CommandSettings(Public = false, Level = 10, HelpText = "Leave a channel.")]
	public string leave(string target, string user, string msg) {
		if (msg.Trim() == "") {
			return "PART " + target;
		}
		else {
			return "PART " + msg;
		}
	}

	[CommandSettings(Public = false, Level = 10, HelpText = "Meanie.")]
	public string quit(string target, string user, string msg) {
		return "QUIT :I must return to my people!";
	}


	[CommandSettings(Public = false, Level = 10, HelpText = "Send a message to a user or channel.")]
	public string say(string target, string user, string msg) {
		List<string> segments = msg.Split(' ').ToList();
		string trg = segments[0];
		segments.RemoveAt(0);
		return SendMessage(trg, string.Join(" ", segments.ToArray()));
	}


	[CommandSettings(Public=false, Level=10, HelpText="Reload the command processor.")]
	public string reload(string target, string user, string msg) {
		return "RELOAD " + target;
	}

	[CommandSettings(Public = false, Level = 10, HelpText = "Reload data file.")]
	public string loaddata(string target, string user, string msg) {

		dsData = new DataSet();
		dsData.ReadXml(Environment.CurrentDirectory + "\\data.xml");
		ChannelHistory = new Dictionary<string, List<MessageHistory>>();

		/*AuthedUsers = new List<string>();*/
		AllowedUsers = new List<string>();
		AllowedUsers.AddRange((from DataRow dr in dsData.Tables["AuthedUsers"].Rows select dr["User"].ToString().ToLower()).ToList().Distinct());
		AuthedUsers = new Dictionary<string, User>();
		foreach (DataRow drUser in dsData.Tables["AuthedUsers"].Rows) {
			User u = new User() { Level = int.Parse(drUser["Level"].ToString()), IsAuthed = false };
			AuthedUsers.Add(drUser["User"].ToString().ToLower(), u);
		}

		IgnoredUsers = new List<string>();
		try {
			IgnoredUsers.AddRange((from DataRow dr in dsData.Tables["IgnoreUsers"].Rows select dr["User"].ToString().ToLower()).ToList().Distinct());
		}
		catch { }
		KarmaList = new List<KarmaObject>();
		if (dsData.Tables.Contains("Karma")) {
			foreach (DataRow dr in dsData.Tables["Karma"].Rows) {
				KarmaObject k = new KarmaObject(dr["User"].ToString(), dr["Channel"].ToString());
				k.Score = int.Parse(dr["Score"].ToString());
				KarmaList.Add(k);
			}
		}

		return SendMessage(target, "Data reloaded.");
	}


	[CommandSettings(Public = false, Level = 10, HelpText = "Evaluate a JavaScript statement. Operation will time out in 5 seconds.")]
	public List<string> eval(string target, string user, string msg) {
		List<string> ret = new List<string>();
		try {
			var tokenSource = new CancellationTokenSource();
			CancellationToken token = tokenSource.Token;
			int timeout = 5000;

			var task = Task.Factory.StartNew(() => {
				/*var engine = new Jurassic.ScriptEngine();*/
				
				string result = JSEngine.Evaluate(string.Join(";", JSFunctions.ToArray()) + ";" + msg.Replace("\\", "")).ToString();
				
				ret.Add(SendMessage(target, result));
				/*engine = null;*/
			}, token);
			if (!task.Wait(timeout, token)) {
				ret.Add(SendMessage(target, "Request timed out."));
				
			}
		}
		catch (Exception ex) {
			ret.Add(SendMessage(target, ex.Message));
		}
		return ret;
	}


	[CommandSettings(Public = false, Level = 10, HelpText = "Set my command character.")]
	public string setcc(string target, string user, string msg) {
		if (msg.Trim() == "") {
			return SendMessage(target, "Please specify a new command character.");
		}
		else {
			if (msg.Trim().Length == 1) {
				CommandCharacter = msg.Trim();
				dsData.Tables["Config"].Rows[0]["CommandCharacter"] = msg.Trim();
				Save();
				return SendMessage(target, "Command character updated.");
			}
			else {
				return SendMessage(target, "Please specify a single character.");
			}
		}
	}

	[CommandSettings(Public = false, Level = 10, HelpText = "Send an arbitrary command message.")]
	public string cmd(string target, string user, string msg) {
		return msg;
	}

}


public class KarmaObject : IComparable {
	public int CompareTo(object obj) {
		if (obj is KarmaObject) {
			return ((KarmaObject)obj).Score.CompareTo(Score);
		}
		throw new ArgumentException("Object is not a KarmaObject.");
	}

	public string ObjectName;
	public int Score;
	public string Channel;
	public KarmaObject() {
	}

	public KarmaObject(string name, string chan) {
		ObjectName = name;
		Score = 0;
		Channel = chan;
	}
}

public class User {
	public int Level { get; set; }
	public bool IsAuthed { get; set; }
}

public class MessageHistory {
	public string User { get; set; }
	public string Message { get; set; }
	public DateTime Timestamp { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public class CommandSettings : Attribute {
	public int Level { get; set; }
	public bool Public { get; set; }
	public string HelpText { get; set; }

	public CommandSettings() {
		Public = false;
		Level = 0;
		HelpText = "No help text available.";
	}
}

public struct HistoryObject {
	public DateTime Timestamp;
	public string Message;
}

public struct UserObject {
	public string UserName;
	public string LastSeenMessage;
	public DateTime LastSeenTimestamp;
	public string LastSeenChannel;
	public string LastMessage;
	public List<HistoryObject> History;
	public TimeSpan LastSeenAgo {
		get {
			return DateTime.Now - LastSeenTimestamp;
		}
	}

	public UserObject(string name) {
		UserName = name;
		LastSeenMessage = "";
		LastSeenTimestamp = DateTime.Now;
		LastSeenChannel = "";
		LastMessage = "";
		History = new List<HistoryObject>();
	}
}


public class Card {
	public string set,
		quality,
		type,
		cost,
		health,
		attack,
		faction,
		classs,
		elite,
		name,
		description,
		createdAt,
		updatedAt,
		id,
		durability,
		race;
	public bool Collectible;
}