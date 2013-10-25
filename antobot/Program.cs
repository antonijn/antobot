using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Numerics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;

namespace antobot
{
	class SwearingStats
	{
		public int swearWords;
		public int totalWords;

		public Dictionary<string, int> popularity = new Dictionary<string, int>();
	}

	public class Program : Irc
	{
		public Program()
			: base("irc.afternet.org", "antobot", "#GamedevTeam")
		{
			swearStats = XElement.Load("log.xml").Elements().ToDictionary(x => x.Name.LocalName, x => getSwearStatsFromXml(x.Elements().ToArray()));

			Thread t = new Thread(writeLogs);
			t.Start();
		}

		private Dictionary<string, SwearingStats> swearStats = new Dictionary<string, SwearingStats>();
		private readonly List<string> away = new List<string>();
		private string lastMsgSaid = string.Empty;

		private Random rand = new Random();

		private void handleDice(string msg)
		{
			string val = msg.Substring("dice ".Length).Trim();
			ulong bint;
			if (ulong.TryParse(val, out bint) && bint > 0)
			{
				ulong res = ((ulong)rand.Next() << 32) | (ulong)(rand.Next());
				res %= bint;
				++res;
				PostMessage("You rolled {0}", res);
			}
			else
			{
				PostMessage("Invalid number: {0}", val);
			}
		}

		private void handleCmd(string msg, string user)
		{
			if (msg.ToLowerInvariant().StartsWith("dice "))
			{
				handleDice(msg);
			}
			else if (msg.ToLowerInvariant().StartsWith("time"))
			{
				int time = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
				PostMessage("It's currently: {0}", time);
			}
			else if (msg.ToLowerInvariant().StartsWith("objection"))
			{
				bool b = rand.Next(0, 2) == 1;
				if (b)
				{
					PostMessage("{0}: objection sustained!", user);
				}
				else
				{
					PostMessage("{0}: objection overruled!", user);
				}
			}
			else if (msg.ToLowerInvariant().StartsWith("cookie"))
			{
				handleCookie(user);
			}
			else if (msg.ToLowerInvariant().StartsWith("dance"))
			{
				PostAction("dances \\o/");
			}
			else if (msg.ToLowerInvariant().StartsWith("swearstats"))
			{
				lock (swearStats)
				{
					if (swearStats.ContainsKey(user))
					{
						SwearingStats stats = swearStats[user];
						if (stats.swearWords == 0)
						{
							PostMessage("{0}: you've been an angel so far.", user);
						}
						else
						{
							double prop = stats.totalWords / (double)stats.swearWords;
							PostMessage("{0}: 1 in every {1} words you used was a swear word.", user, prop.ToString(CultureInfo.InvariantCulture));
						}
					}
					else
					{
						PostMessage("{0}: you haven't said anything yet!", user);
					}
				}
			}
			else if (msg.ToLowerInvariant().StartsWith("away"))
			{
				if (!away.Contains(user))
				{
					away.Add(user);
					PostMessage("{0} is away.", user);
				}
			}
			else if (msg.ToLowerInvariant().StartsWith("back"))
			{
				if (away.Contains(user))
				{
					away.Remove(user);
					PostMessage("{0} is back.", user);
				}
			}
		}

		private void handleCookie(string user)
		{
			int r = rand.Next(4);
			switch (r)
			{
				case 0: PostMessage("{0}: thanks!", user);
					break;
				case 1: PostMessage("{0}: yum!", user);
					break;
				case 2: PostMessage("{0}: om nom nom!", user);
					break;
				case 3: PostMessage("{0}: thank you very much!", user);
					break;
			}
		}

		private static Queue<string> lastMsg = new Queue<string>(2);

		private static void log(string msg, string user)
		{
			File.AppendAllText("log.log", string.Format("[{0}] <{1}> {2}\n", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture), user, msg));
		}

		private void handlePrivMsg(string msg, string user)
		{
			string msgTrim = msg.Trim().ToLowerInvariant();
			if (msgTrim == "hi antobot" || msgTrim == "hello antobot" ||
				msgTrim == "hi antobot!" || msgTrim == "hello antobot!" ||
				msgTrim == "hi antobot." || msgTrim == "hello antobot." ||
				msgTrim == "antobot: hi" || msgTrim == "antobot: hello" ||
				msgTrim == "antobot: hi!" || msgTrim == "antobot: hello!" ||
				msgTrim == "antobot: hi." || msgTrim == "antobot: hello.")
			{
				PostMessage("Hi {0}!", user);
			}
			else if (msg.StartsWith("\x0001ACTION "))
			{
				string action = msg.Substring("\x0001ACTION ".Length, msg.Length - "\x0001ACTION ".Length - 1).Trim();
				handleAction(user, action);
			}
			else if (msg.StartsWith("!"))
			{
				handleCmd(msg.Substring(1), user);
			}
			else
			{
				HandleNrmlMsg(msg, user);
			}
		}

		private void HandleNrmlMsg(string msg, string user)
		{
			string[] parts = msg
							.Replace(",", string.Empty)
							.Replace(":", string.Empty)
							.Replace(".", string.Empty)
							.Replace(";", string.Empty)
							.Split(' ');

			lock (swearStats)
			{
				if (!swearStats.ContainsKey(user))
				{
					swearStats.Add(user, new SwearingStats());
				}

				SwearingStats stat = swearStats[user];

				stat.totalWords += parts.Count(x => !string.IsNullOrEmpty(x));

				IEnumerable<string> swearW = parts.Where(x => SwearWords.Words.Contains(x.Trim().ToLowerInvariant()));
				stat.swearWords += swearW.Count();
				foreach (string sw in swearW)
				{
					int value = 1;
					if (stat.popularity.ContainsKey(sw))
					{
						value += stat.popularity[sw];
					}

					stat.popularity[sw] = value;
				}
			}

			if (msg.Trim() == "o/")
			{
				PostMessage("\\o");
			}

			if (away.Contains(user))
			{
				away.Remove(user);
				PostMessage("{0} is back.", user);
			}

			foreach (string uname in away)
			{
				if (msg.StartsWith(uname + ":") || msg.StartsWith(uname + ","))
				{
					PostMessage("{0}: {1} is away at the moment!", user, uname);
					break;
				}
			}

			lastMsg.Enqueue(msg);
			if (lastMsg.Count > 2)
			{
				lastMsg.Dequeue();
			}

			string[] msgs = lastMsg.ToArray();
			if (msgs.Length >= 2 && msgs.First() == msgs.Last())
			{
				PostMessage(msgs.First() + "!");
			}
		}

		private void handleAction(string user, string action)
		{
			switch (action)
			{
				case "hugs antobot":
					PostAction("hugs {0}", user);
					break;
				case "kisses antobot":
					PostAction("kisses {0}", user);
					break;
			}
		}

		protected override void OnMsgGet(string msg)
		{
			base.OnMsgGet(msg);

			int idxExcl = msg.IndexOf('!');
			string user = null;
			if (idxExcl != -1)
			{
				user = msg.Substring(1, msg.IndexOf('!') - 1);
			}
			msg = msg.Substring(msg.IndexOf(' ') + 1);

			if (msg.StartsWith("PRIVMSG #GamedevTeam :"))
			{
				string privm = msg.Substring("PRIVMSG #GamedevTeam :".Length);
				log(privm, user);
				handlePrivMsg(privm, user);
			}
			else if (msg.StartsWith("JOIN"))
			{
				log("* has joined", user);
			}
		}

		public override void PostMessage(string msg, params object[] format)
		{
			string str = string.Format(msg, format);
			if (str != lastMsgSaid)
			{
				base.PostMessage(str);
				log(str, BotName);

				lastMsgSaid = str;
			}
		}

		private static IEnumerable<XElement> getSwearStatsXml(SwearingStats sstats)
		{
			return new XElement[]
			{
				new XElement("totalwords", sstats.totalWords),
				new XElement("swearwords", sstats.swearWords),
				new XElement("popularity",
					sstats.popularity.Select(x => new XElement(x.Key, x.Value)))
			};
		}

		private static SwearingStats getSwearStatsFromXml(XElement[] elements)
		{
			XElement twords = elements[0];
			XElement swords = elements[1];
			XElement pop = elements[2];

			int twordsI = int.Parse(twords.Value);
			int swordsI = int.Parse(swords.Value);
			Dictionary<string, int> popularity =
				pop.Elements().ToDictionary(x => x.Name.LocalName, x => int.Parse(x.Value));

			SwearingStats sstats = new SwearingStats();
			sstats.popularity = popularity;
			sstats.swearWords = swordsI;
			sstats.totalWords = twordsI;

			return sstats;
		}

		private void writeLogs()
		{
			while (true)
			{
				Thread.Sleep(10 * 1000);

				lock (swearStats)
				{
					new XElement("log",
						swearStats.Select(x => new XElement(x.Key,
							getSwearStatsXml(x.Value)))).Save("log.xml");
				}
			}
		}

		private static void Main(string[] args)
		{
			Irc irc = new Program();

			while (true)
			{
				string str = Console.ReadLine();
				irc.PostMessageDirect(str);
				if (str == "QUIT")
				{
					break;
				}
			}
		}
	}
}
