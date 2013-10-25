using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.Net.Sockets;

namespace antobot
{
	public delegate void respondHandler(Irc irc, string msg);

	public class Irc
	{
		private TcpClient client;
		private NetworkStream stream;
		
		public readonly string Network;
		public readonly string Channel;
		public readonly string BotName;

		private readonly object clientLock = new object();

		public Irc(string server, string botname, string channel)
		{
			this.Network = server;
			this.Channel = channel;
			this.BotName = botname;

			client = new TcpClient(server, 6667);
			stream = client.GetStream();

			PostMessageDirect("NICK {0}", botname);
			PostMessageDirect("USER {0} 0 * :{0} Bot", botname);

			Thread t = new Thread(msgLoop);
			t.Start();

			Thread.Sleep(11000);
			PostMessageDirect("JOIN {0}", channel);
		}

		private static byte[] getBytes(string str)
		{
			return new ASCIIEncoding().GetBytes(str);
		}

		private void msgLoop()
		{
			while (true)
			{
				lock (clientLock)
				{
					byte[] bytes = new byte[1024];
					int len = stream.Read(bytes, 0, 1024);

					if (len == 0)
					{
						break;
					}

					string str = new ASCIIEncoding().GetString(bytes, 0, len);

					foreach (string split in str.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
					{
						OnMsgGet(split);
					}
				}
			}
		}

		protected virtual void OnMsgGet(string msg)
		{
			Console.WriteLine(msg);

			if (msg.StartsWith("PING"))
			{
				PostMessageDirect("PONG " + msg.Substring("PING :".Length));
			}
		}

		public void PostMessageDirect(string str, params object[] format)
		{
			//lock (clientLock)
			{
				string strAct = string.Format(str + "\r\n", format);
				ConsoleColor backup = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine(strAct);
				Console.ForegroundColor = backup;
				stream.Write(getBytes(strAct), 0, strAct.Length);
			}
		}

		public void PostAction(string act, params object[] format)
		{
			string str = string.Format(act, format);
			PostMessageDirect("\x0001ACTION {0}\x0001", str);
		}

		public virtual void PostMessage(string msg, params object[] format)
		{
			msg = "PRIVMSG #GamedevTeam :" + string.Format(msg, format);
			PostMessageDirect(msg);
		}
	}
}
