using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using Terraria_Server.Plugin;
using Terraria_Server;
using Terraria_Server.Definitions;
using Terraria_Server.Misc;
using Terraria_Server.Events;
using System.IO;

using NDesk.Options;

namespace RestrictPlugin
{
    public partial class RestrictPlugin : Plugin
    {
		class RegistrationRequest
		{
			public string name;
			public string address;
			public string password;
		}
		
		PropertiesFile properties;
		PropertiesFile users;
		
		bool isEnabled = false;
		Dictionary<int, RegistrationRequest> requests;
		int requestsBase = 0;
		int requestCount = 0;
		
		bool allowGuests
		{
			get { return properties.getValue ("allow-guests", false); }
		}
		
		bool restrictGuests
		{
			get { return properties.getValue ("restrict-guests", true); }
		}
		
		bool restrictGuestsDoors
		{
			get { return properties.getValue ("restrict-guests-doors", true); }
		}
		
		string serverId
		{
			get { return properties.getValue ("server-id", "tdsm"); }
		}
		       
		public override void Load()
		{
			Name = "Restrict";
			Description = "Restrict access to the server or character names.";
			Author = "UndeadMiner";
			Version = "1";
			TDSMBuild = 26;
			
			requests = new Dictionary <int, RegistrationRequest> ();
			
			string pluginFolder = Statics.PluginPath + Path.DirectorySeparatorChar + "Restrict";

			CreateDirectory(pluginFolder);
			
			properties = new PropertiesFile (pluginFolder + Path.DirectorySeparatorChar + "restrict.properties");
			properties.Load();
			properties.Save();
			
			users = new PropertiesFile (pluginFolder + Path.DirectorySeparatorChar + "restrict_users.properties");
			users.Load();
			users.Save();
			
			isEnabled = true;
		}
        
        public override void Enable()
        {
            Program.tConsole.WriteLine(base.Name + " enabled.");
            //Register Hooks
            this.registerHook(Hooks.PLAYER_AUTH_QUERY);
            this.registerHook(Hooks.PLAYER_AUTH_REPLY);
            this.registerHook(Hooks.CONSOLE_COMMAND);
            this.registerHook(Hooks.PLAYER_COMMAND);
            this.registerHook(Hooks.PLAYER_TILECHANGE);
            this.registerHook(Hooks.PLAYER_FLOWLIQUID);
            this.registerHook(Hooks.PLAYER_PROJECTILE);
            this.registerHook(Hooks.PLAYER_CHEST);
            this.registerHook(Hooks.PLAYER_LOGIN);
            this.registerHook(Hooks.PLAYER_LOGOUT);
        }

        public override void Disable()
        {
            Program.tConsole.WriteLine(base.Name + " disabled.");
        }
        
		public override void onPlayerAuthQuery (PlayerLoginEvent ev)
		{
			ev.Action = PlayerLoginAction.REJECT;
			
			if (ev.Player.Name == null)
			{
				ev.Slot.Kick ("Null player name");
				return;
			}
			
			var name = ev.Player.Name;
			var pname = NameTransform (name);
			var entry = users.getValue (pname);
			
			if (entry == null)
			{
				if (allowGuests)
				{
					ev.Action = PlayerLoginAction.ACCEPT;
					ev.Player.AuthenticatedAs = null;
					
					Program.tConsole.WriteLine ("<Restrict> Letting user {0} in slot {1} in as guest.", name, ev.Slot.whoAmI);
				}
				else
				{
					Program.tConsole.WriteLine ("<Restrict> Unregistered user {0} in slot {1} connection attempt.", name, ev.Slot.whoAmI);
					ev.Slot.Kick ("Only registered users are allowed.");
				}
				return;
			}
			
			Program.tConsole.WriteLine ("<Restrict> Expecting password for user {0} in slot {1}.", name, ev.Slot.whoAmI);
			ev.Action = PlayerLoginAction.ASK_PASS;
		}

		public override void onPlayerAuthReply (PlayerLoginEvent ev)
		{
			ev.Action = PlayerLoginAction.REJECT;
			
			if (ev.Player.Name == null)
			{
				ev.Slot.Kick ("Null player name");
				return;
			}
			
			var name = ev.Player.Name;
			var pname = NameTransform (name);
			var entry = users.getValue (pname);
			
			if (entry == null)
			{
				if (allowGuests)
				{
					ev.Action = PlayerLoginAction.ACCEPT;
					ev.Player.AuthenticatedAs = null;
				}
				else
					ev.Slot.Kick ("Only registered users are allowed.");
				return;
			}
			
			var split = entry.Split (':');
			var hash  = split[0];
			var hash2 = Hash (name, ev.Password);
			
			if (hash != hash2)
			{
				ev.Slot.Kick ("Incorrect password for user: " + name);
				return;
			}
			
			if (split.Length > 1 && split[1] == "op")
				ev.Player.Op = true;
			
			ev.Action = PlayerLoginAction.ACCEPT;
			ev.Player.AuthenticatedAs = name;
		}

		public override void onPlayerJoin (PlayerLoginEvent ev)
		{
			var player = ev.Player;
			
			if (player.Name == null) return;

			if (player.AuthenticatedAs == null)
				ev.Player.sendMessage ("You are a guest, to register type: /rr password");
			else if (ev.Player.Op)
				ev.Player.sendMessage ("This humble server welcomes back Their Operating Highness.", 255, 128, 128, 255);
			else
				ev.Player.sendMessage ("Welcome back, registered user.", 255, 128, 255, 128);
		}
		
//		public override void onPlayerLogout (PlayerLogoutEvent ev)
//		{
//			if (ev.Player.Name == null) return;
//
//			PlayerRecord record;
//			if (records.TryGetValue (ev.Player.Name, out record))
//			{
//				records.Remove (ev.Player.Name);
//			}
//		}
		
		public override void onPlayerTileChange (PlayerTileChangeEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerTileChange.");
				ev.Cancelled = true;
				return;
			}
			
			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to alter the world as a guest.");
			}
		}
		
		public override void onPlayerFlowLiquid (PlayerFlowLiquidEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerFlowLiquid.");
				ev.Cancelled = true;
				return;
			}
			
			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to alter the world as a guest.");
			}
		}
		
		public override void onPlayerProjectileUse (PlayerProjectileEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerProjectileUse.");
				ev.Cancelled = true;
				return;
			}
			
			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				switch (ev.Projectile.type)
				{
					case ProjectileType.POWDER_PURIFICATION:
					case ProjectileType.POWDER_VILE:
					case ProjectileType.BOMB:
					case ProjectileType.BOMB_STICKY:
					case ProjectileType.DYNAMITE:
					case ProjectileType.GRENADE:
					case ProjectileType.BALL_SAND_DROP:
					case ProjectileType.BALL_MUD:
					case ProjectileType.BALL_ASH:
					case ProjectileType.BALL_SAND_GUN:
					case ProjectileType.TOMBSTONE:
					case ProjectileType.GLOWSTICK:
					case ProjectileType.GLOWSTICK_STICKY:
						ev.Cancelled = true;
						player.sendMessage ("<Restrict> You are not allowed to alter the world as a guest.");
						break;
					default:
						break;
				}
			}
		}
		
		public override void onPlayerOpenChest (PlayerChestOpenEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerOpenChest.");
				ev.Cancelled = true;
				return;
			}

			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to open chests as a guest.");
			}
		}
		
		public override void onDoorStateChange (DoorStateChangeEvent ev)
		{
			if ((!restrictGuests) || (!restrictGuestsDoors)) return;
			
			var player = ev.Sender as Player;
			
			if (player == null) return;

			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to open and close doors as a guest.");
			}
		}
		
		public override void onConsoleCommand (ConsoleCommandEvent ev)
		{
			var tokens = Terraria_Server.Commands.CommandParser.Tokenize (ev.Message);
			
			SwitchCommand (ev, tokens);
		}
		
		public override void onPlayerCommand (PlayerCommandEvent ev)
		{
			var player = (Player) ev.Sender;
			
			if (! player.Op)
			{
				var split = ev.Message.Trim().Split(' ');
				if (split[0] == "/rr")
				{
					ev.Cancelled = true;
					lock(this) RequestPlayerCommand (ev, split.Length > 1 ? string.Join (" ", split.Skip(1)) : null);
				}
			}
			else
			{
				var tokens = Terraria_Server.Commands.CommandParser.Tokenize (ev.Message);
				if (tokens[0].StartsWith ("/"))
				{
					tokens[0] = tokens[0].Substring(1);
					SwitchCommand (ev, tokens);
				}
			}
		}
    }
}
