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

using NDesk.Options;

namespace RestrictPlugin
{
	public partial class RestrictPlugin
	{
		void SwitchCommand (MessageEvent ev, IList<string> split)
		{
			switch (split[0])
			{
				case "ru":
				case "restrict.ru":
					ev.Cancelled = true;
					lock(this) RegisterCommand (ev, split.Skip(1));
					break;
					
				case "ur":
				case "restrict.ur":
					ev.Cancelled = true;
					lock(this) UnregisterCommand (ev, split.Skip(1));
					break;
					
				case "ro":
				case "restrict.ro":
					ev.Cancelled = true;
					lock(this) OptionsCommand (ev, split.Skip(1));
					break;
				
				case "rr":
				case "restrict.rr":
					ev.Cancelled = true;
					lock(this) RequestsCommand (ev, split.Skip(1));
					break;
				
				default:
					return;
			}
		}
		
		void RegisterCommand (MessageEvent ev, IEnumerable<string> rest)
		{
			try
			{
				var op = false;
				var force = false;
				string password = null;
				var options = new OptionSet ()
				{
					{ "o|op", v => op = true },
					{ "f|force", v => force = true },
					{ "p|password=", v => password = v },
				};
				
				var args = options.Parse (rest);
				
				if (args.Count == 0 || args.Count > 2)
					throw new OptionException ();
				
				var name = args[0];
				var player = FindPlayer (name);
				PlayerRecord record = null;
				
				if (player == null)
				{
					if (! force)
					{
						ev.Sender.sendMessage ("restrict.ru: Player not found online, use -f to assume name is correct.");
						return;
					}
				}
				else
				{
					name = player.Name;
					if (! records.TryGetValue (name, out record))
						record = null;
				}
				
				var pname = NameTransform (name);
				
				string hash = null;
				if (password != null)
					hash = Hash (name, password);
				else if (args.Count == 2)
					hash = args[1];
				
				if (hash != null)
				{
					var old = users.getValue (pname);
					
					var val = hash;
					if (op) val += ":op";
					users.setValue (pname, val);
					users.Save ();
					
					if (record != null)
						record.guest = false;

					if (player != null)
					{
						if (player != ev.Sender)
						{
							if (op)
								player.sendMessage ("<Restrict> You have been registered as an operator.");
							else
								player.sendMessage ("<Restrict> You have been registered.");
						}
						player.Op = op;
					}
					
					if (old != null)
						ev.Sender.sendMessage ("restrict.ru: Changed password for: " + name);
					else if (op)
						ev.Sender.sendMessage ("restrict.ru: Registered operator: " + name);
					else
						ev.Sender.sendMessage ("restrict.ru: Registered user: " + name);

				}
				else if (args.Count == 1)
				{
					var entry = users.getValue (pname);
					
					if (entry == null)
					{
						ev.Sender.sendMessage ("restrict.ru: No such user in database: " + name);
						return;
					}
					
					var split = entry.Split(':');
					var oldop = split.Length > 1 && split[1] == "op";
					
					if (player != null)
					{
						player.Op = op;
						if (player != ev.Sender)
						{
							if (op && !oldop)
								player.sendMessage ("<Restrict> You have been registered as an operator.");
							else if (oldop && !op)
								player.sendMessage ("<Restrict> You have been unregistered as an operator.");
						}
					}
					
					if (oldop != op)
					{
						var val = split[0];
						if (op) val += ":op";
						users.setValue (pname, val);
						users.Save ();
						
						if (oldop && !op)
							ev.Sender.sendMessage ("restrict.ru: De-opped: " + name);
						else if (op && !oldop)
							ev.Sender.sendMessage ("restrict.ru: Opped: " + name);
					}
				}
			}
			catch (OptionException)
			{
				ev.Sender.sendMessage (@"
restrict.ru (register user):
    Adding users or changing passwords:
        ru [-o] [-f] <name> <hash>
        ru [-o] [-f] <name> -p <password>
    Changing op status:
        ru -o [-f] <name>
        ru    [-f] <name>
    Options:
        -o    make the player an operator
        -f    force action even if player isn't online
    Hashes should be a SHA-256 checksum of: name:" + serverId + ":password");
			}
		}

		void UnregisterCommand (MessageEvent ev, IEnumerable<string> rest)
		{
			try
			{
				var force = false;
				var options = new OptionSet ()
				{
					{ "f|force", v => force = true },
				};
				
				var args = options.Parse (rest);
				
				if (args.Count == 0 || args.Count > 1)
					throw new OptionException ();
				
				var name = args[0];
				var player = FindPlayer (name);
				
				if (player == null)
				{
					if (! force)
					{
						ev.Sender.sendMessage ("restrict.ur: Player not found online, use -f to assume name is correct.");
						return;
					}
				}
				else
				{
					name = player.Name;
					player.Op = false;
					PlayerRecord record;
					if (records.TryGetValue (name, out record))
						record.guest = true;
					
					if (player != ev.Sender)
						player.sendMessage ("<Restrict> Your registration has been revoked.");
				}
				
				var pname = NameTransform (name);
				
				users.setValue (pname, null);
				users.Save ();
				
				ev.Sender.sendMessage ("restrict.ur: Unregistered user: " + name);
			}
			catch (OptionException)
			{
				ev.Sender.sendMessage (@"
restrict.ur (unregister user):
    Deleting users:
        ur [-f] <name>
    Options:
        -f    force action even if player isn't online");
			}
		}
		
		void OptionsCommand (MessageEvent ev, IEnumerable<string> rest)
		{
			try
			{
				var force = false;
				var ag = allowGuests;
				var rg = restrictGuests;
				var rd = restrictGuestsDoors;
				var si = serverId;
				var changed = false;
				var changed_si = false;
				var reload = false;
				
				var options = new OptionSet ()
				{
					{ "f|force", v => force = true },
					{ "g|allow-guests=", (bool v) => { ag = v; changed = true; } },
					{ "r|restrict-guests=", (bool v) => { rg = v; changed = true; } },
					{ "d|restrict-guests-doors=", (bool v) => { rd = v; changed = true; } },
					{ "L|reload-users", v => reload = true },
					{ "s|server-id=", v =>
						{
							si = v;
							changed = true; changed_si = true;
						}
					},
				};

				var args = options.Parse (rest);
				
				if (args.Count > 0)
					throw new OptionException ();
					
				if (changed_si && users.Count > 0)
				{
					ev.Sender.sendMessage ("restrict.ro: Warning: Changing the server id will invalidate existing password hashes. Use -f to do so anyway.");
					if (! force)
						return;
				}
				
				if (changed)
				{
					properties.setValue ("allow-guests", ag.ToString());
					properties.setValue ("restrict-guests", rg.ToString());
					properties.setValue ("restrict-guests-doors", rd.ToString());
					properties.setValue ("server-id", si.ToString());
					properties.Save ();
				}
				
				if (reload)
				{
					ev.Sender.sendMessage ("restrict.ro: Reloaded users database, entries: " + users.Count);
					users.Load ();
				}
				
				ev.Sender.sendMessage ("restrict.ro: Options set: server-id=" + si
					+ ", allow-guests=" + ag.ToString()
					+ ", restrict-guests=" + rg.ToString()
					+ ", restrict-guests-doors=" + rd.ToString());
			}
			catch (OptionException)
			{
				ev.Sender.sendMessage (@"
restrict.ro (options):
    Displaying options:
        ro
    Setting options:
        ro [-f] [-g {true|false}] [-r {true|false}] [-s <serverId>] [-L]
    Options:
        -f    force action
        -g    allow guests to enter the game
        -r    restrict guests' ability to alter tiles
        -s    set the server identifier used in hashing passwords
        -L    reload the user database from disk");
			}
		}

		void RequestsCommand (MessageEvent ev, IEnumerable<string> rest)
		{
			try
			{
				var grant = -1;
				var deny = -1;
				var options = new OptionSet ()
				{
					{ "g|grant=", (int v) => grant = v },
					{ "d|deny=", (int v) => deny = v },
				};
				
				var args = options.Parse (rest);
				
				if (args.Count != 0 || (grant >= 0 && deny >= 0))
					throw new OptionException ();
				
				if (grant == -1 && deny == -1)
				{
					ev.Sender.sendMessage ("restrict.rr: Pending requests:");
					int i = requestsBase;
					foreach (var rq in requests)
					{
						if (rq == null) continue;
						
						ev.Sender.sendMessage (string.Format ("{0,3} : {1} : {2}", i, rq.address, rq.name));
						i += 1;
					}
					return;
				}
				
				grant -= requestsBase; deny -= requestsBase;
				
				if (grant >= requests.Count || deny >= requests.Count || (grant < 0 && deny < 0))
				{
					ev.Sender.sendMessage ("restrict.rr: No such registration request");
					return;
				}
				
				if (deny >= 0)
				{
					var rq = requests[deny];
					
					if (rq == null)
					{
						ev.Sender.sendMessage ("restrict.rr: No such registration request");
						return;
					}
					
					requests[deny] = null;
					requestCount -= 1;
					CompactRequests ();
					
					Program.server.notifyOps ("<Restrict> Registration request denied for: " + rq.name);
					
					var player = FindPlayer (rq.name);
					if (player != null)
						player.sendMessage ("<Restrict> Your registration request has been denied.");
					
				}
				else if (grant >= 0)
				{
					var rq = requests[grant];
					
					if (rq == null)
					{
						ev.Sender.sendMessage ("restrict.rr: No such registration request");
						return;
					}
					
					requests[grant] = null;
					requestCount -= 1;
					CompactRequests ();
					
					var pname = NameTransform (rq.name);
					var hash = Hash (rq.name, rq.password);
					
					users.setValue (pname, hash);
					users.Save ();
					
					PlayerRecord record;
					if (records.TryGetValue (rq.name, out record))
						record.guest = false;
					
					var player = FindPlayer (rq.name);
					if (player != null)
						player.sendMessage ("<Restrict> You are now registered.");
					
					Program.server.notifyOps ("<Restrict> Registration request granted for: " + rq.name);
				}
				
			}
			catch (OptionException)
			{
				ev.Sender.sendMessage (@"
restrict.rr (registration requests):
    Listing:
        rr
    Granting:
        rr -g <number>
    Denying:
        rr -d <number>");
			}
		}

		void RequestPlayerCommand (MessageEvent ev, string password)
		{
			try
			{
				if (password == null)
					throw new OptionException ();
				
				password = password.Trim();
				
				if (password == "")
					throw new OptionException ();
				
				var player = (Player) ev.Sender;
				var name = player.Name;
				
				PlayerRecord record;
				if (records.TryGetValue (name, out record) && record.guest == false)
				{
					var pname = NameTransform (name);
					var split = users.getValue(pname).Split(':');
					var hash = Hash (name, password);
					
					if (hash == split[0])
					{
						ev.Sender.sendMessage ("<Restrict> Already registered.");
						return;
					}
					
					if (split.Length > 1 && split[1] == "op")
						hash = hash + ":op";
					
					users.setValue (pname, hash);
					users.Save ();
					
					ev.Sender.sendMessage ("<Restrict> Already registered, password changed.");
					return;
				}
					
				var address = Netplay.slots[player.whoAmi].remoteAddress.Split(':')[0];
				
				if (requests.Any (r => r != null && r.address == address && r.name == name))
				{
					ev.Sender.sendMessage ("<Restrict> Registration request pending.");
					return;
				}
				
				int num = requests.Count + requestsBase;
				requestCount += 1;
				requests.Add (new RegistrationRequest { name = name, address = address, password = password });
				
				ev.Sender.sendMessage ("<Restrict> Registration request submitted.");
				Program.server.notifyOps ("<Restrict> New registration request " + num + " for: " + name);
			}
			catch (OptionException)
			{
				ev.Sender.sendMessage ("<Restrict> Command usage: /rr password");
			}
		}
	}
}

