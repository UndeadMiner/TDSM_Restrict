using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using Terraria_Server.Plugin;
using Terraria_Server;
using Terraria_Server.Misc;
using Terraria_Server.Events;
using System.IO;

namespace RestrictPlugin
{
	public partial class RestrictPlugin
	{
        private static void CreateDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }

		protected string Hash (string username, string password)
		{
			var hash = SHA256.Create ();
			var sb = new StringBuilder (64);
			var bytes = hash.ComputeHash (Encoding.ASCII.GetBytes (username + ":" + serverId + ":" + password));
			foreach (var b in bytes)
				sb.Append (b.ToString ("x2"));
			return sb.ToString ();
		}
		        
		static List<string> SplitCommand (string command)
		{
			char l = '\0';
			var result = new List<string> ();
			var b = new StringBuilder ();
			int s = 0;
			
			foreach (char cc in command.Trim())
			{
				char c = cc;
				switch (s)
				{
					case 0: // base state
					{
						if (c == '"' && l != '\\')
							s = 1;
						else if (c == ' ' && b.Length > 0)
						{
							result.Add (b.ToString());
							b.Length = 0;
						}
						else if (c != '\\' || l == '\\')
						{
							b.Append (c);
							c = '\0';
						}
					}
					break;
					
					case 1: // inside quotes
					{
						if (c == '"' && l != '\\')
							s = 0;
						else if (c != '\\' || l == '\\')
						{
							b.Append (c);
							c = '\0';
						}
					}
					break;
				}
				l = c;
			}
			
			if (b.Length > 0)
				result.Add (b.ToString());
			
			return result;
		}
		
		static Player FindPlayer (string name)
		{
			name = name.ToLower();
			
			foreach (var p in Main.players)
			{
				if (p != null && p.Name != null && p.Name.ToLower() == name)
					return p;
			}
			
			return null;
		}

		static string NameTransform (string name)
		{
			return string.Concat ("<", name.Replace ("=", "_EQUAL_"), ">");
		}
		
		void CompactRequests ()
		{
			if (requests.Count > 100 && requestCount < requests.Count / 2)
			{
				int d = 0;
				for (int s = 0; s < requests.Count; s++)
				{
					if (requests[s] != null)
					{
						requests[d] = requests[s];
						requests[s] = null;
						d += 1;
					}
				}
				requestsBase += d;
				requestCount = d;
				requests.RemoveRange (d, requests.Count - d);
			}
		}
	}
}

