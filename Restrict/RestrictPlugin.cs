﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using Terraria_Server.Plugin;
using Terraria_Server;
using Terraria_Server.Commands;
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
			Version = "0.32.0";
			TDSMBuild = 32;
			
			requests = new Dictionary <int, RegistrationRequest> ();
			
			string pluginFolder = Statics.PluginPath + Path.DirectorySeparatorChar + "Restrict";

			CreateDirectory(pluginFolder);
			
			properties = new PropertiesFile (pluginFolder + Path.DirectorySeparatorChar + "restrict.properties");
			properties.Load();
			var dummy1 = allowGuests;
			var dummy2 = restrictGuests;
			var dummy3 = restrictGuestsDoors;
			var dummy4 = serverId;
			properties.Save();
			
			users = new PropertiesFile (pluginFolder + Path.DirectorySeparatorChar + "restrict_users.properties");
			users.Load();
			users.Save();
			
			isEnabled = true;
			
			AddCommand ("ru")
				.WithDescription ("Register users or change their accounts")
				.WithHelpText ("Adding users or changing passwords:")
				.WithHelpText ("    ru [-o] [-f] <name> <hash>")
				.WithHelpText ("    ru [-o] [-f] <name> -p <password>")
				.WithHelpText ("Changing op status:")
				.WithHelpText ("    ru -o [-f] <name>")
				.WithHelpText ("    ru    [-f] <name>")
				.WithHelpText ("Options:")
				.WithHelpText ("    -o    make the player an operator")
				.WithHelpText ("    -f    force action even if player isn't online")
				.Calls (this.RegisterCommand);

			
			AddCommand ("ur")
				.WithDescription ("Unregister users")
				.WithHelpText ("Deleting users:")
				.WithHelpText ("    ur [-f] <name>")
				.WithHelpText ("Options:")
				.WithHelpText ("    -f    force action even if player isn't online")
				.Calls (this.UnregisterCommand);
			
			AddCommand ("ro")
				.WithDescription ("Configure Restrict")
				.WithHelpText ("Displaying options:")
				.WithHelpText ("    ro")
				.WithHelpText ("Setting options:")
				.WithHelpText ("    ro [-f] [-g {true|false}] [-r {true|false}] [-s <serverId>] [-L]")
				.WithHelpText ("Options:")
				.WithHelpText ("    -f    force action")
				.WithHelpText ("    -g    allow guests to enter the game")
				.WithHelpText ("    -r    restrict guests' ability to alter tiles")
				.WithHelpText ("    -s    set the server identifier used in hashing passwords")
				.WithHelpText ("    -L    reload the user database from disk")
				.Calls (this.OptionsCommand);
			
			AddCommand ("rr")
				.WithDescription ("Manage registration requests")
				.WithHelpText ("Usage: rr          list registration requests")
				.WithHelpText ("       rr -g #     grant a registration request")
				.WithHelpText ("       rr grant #")
				.WithHelpText ("       rr -d #     deny a registration request")
				.WithHelpText ("       rr deny #")
				.Calls (this.RequestsCommand);
			
			AddCommand ("pass")
				.WithDescription ("Change your password")
				.WithAccessLevel (AccessLevel.PLAYER)
				.WithHelpText ("Usage: /pass yourpassword")
				.Calls (this.PlayerPassCommand);
				
			AddCommand ("reg")
				.WithDescription ("Submit a registration request")
				.WithAccessLevel (AccessLevel.PLAYER)
				.WithHelpText ("Usage: /reg yourpassword")
				.Calls (this.PlayerRegCommand);
		}
		
		public override void Enable()
		{
			Program.tConsole.WriteLine(base.Name + " enabled.");

			this.registerHook(Hooks.PLAYER_AUTH_QUERY);
			this.registerHook(Hooks.PLAYER_AUTH_REPLY);
			this.registerHook(Hooks.PLAYER_TILECHANGE);
			this.registerHook(Hooks.PLAYER_EDITSIGN);
			this.registerHook(Hooks.PLAYER_CHESTBREAK);
			this.registerHook(Hooks.PLAYER_FLOWLIQUID);
			this.registerHook(Hooks.DOOR_STATECHANGE);
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
				ev.Player.Kick ("Null player name");
				return;
			}
			
			var name = ev.Player.Name;
			var pname = NameTransform (name);
			var oname = OldNameTransform (name);
			var entry = users.getValue (pname) ?? users.getValue (oname);
			
			if (entry == null)
			{
				if (allowGuests)
				{
					ev.Action = PlayerLoginAction.ACCEPT;
					ev.Player.AuthenticatedAs = null;
					ev.Priority = LoginPriority.QUEUE_LOW_PRIO;
					
					Program.tConsole.WriteLine ("<Restrict> Letting user {0} from {1} in as guest.", name, ev.Player.IPAddress);
				}
				else
				{
					Program.tConsole.WriteLine ("<Restrict> Unregistered user {0} from {1} attempted to connect.", name, ev.Player.IPAddress);
					ev.Player.Kick ("Only registered users are allowed.");
				}
				return;
			}
			
			Program.tConsole.WriteLine ("<Restrict> Expecting password for user {0} from {1}.", name, ev.Player.IPAddress);
			ev.Action = PlayerLoginAction.ASK_PASS;
		}

		public override void onPlayerAuthReply (PlayerLoginEvent ev)
		{
			ev.Action = PlayerLoginAction.REJECT;
			
			if (ev.Player.Name == null)
			{
				ev.Player.Kick ("Null player name");
				return;
			}
			
			var name = ev.Player.Name;
			var pname = NameTransform (name);
			var oname = OldNameTransform (name);
			var entry = users.getValue (pname) ?? users.getValue (oname);
			
			if (entry == null)
			{
				if (allowGuests)
				{
					ev.Action = PlayerLoginAction.ACCEPT;
					ev.Player.AuthenticatedAs = null;
					ev.Priority = LoginPriority.QUEUE_LOW_PRIO;
				}
				else
					ev.Player.Kick ("Only registered users are allowed.");
				return;
			}
			
			var split = entry.Split (':');
			var hash  = split[0];
			var hash2 = Hash (name, ev.Password);
			
			if (hash != hash2)
			{
				ev.Player.Kick ("Incorrect password for user: " + name);
				return;
			}
			
			if (split.Length > 1 && split[1] == "op")
			{
				ev.Player.Op = true;
				ev.Priority = LoginPriority.BYPASS_QUEUE;
			}
			else
			{
				ev.Priority = LoginPriority.QUEUE_MEDIUM_PRIO;
			}
			
			ev.Action = PlayerLoginAction.ACCEPT;
			ev.Player.AuthenticatedAs = name;
		}

		public override void onPlayerJoin (PlayerLoginEvent ev)
		{
			var player = ev.Player;
			
			if (player.Name == null) return;

			if (player.AuthenticatedAs == null)
				ev.Player.sendMessage ("You are a guest, to register type: /reg yourpassword");
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

		public override void onPlayerEditSign (PlayerEditSignEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerEditSign.");
				ev.Cancelled = true;
				return;
			}
			
			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to edit signs as a guest.");
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
			}
		}

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
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
			}
		}

		public override void onPlayerChestBreak (PlayerChestBreakEvent ev)
		{
			var player = ev.Sender as Player;
			
			if (player == null || player.Name == null)
			{
				Program.tConsole.WriteLine ("<Restrict> Invalid player in onPlayerChestBreak.");
				ev.Cancelled = true;
				return;
			}
			
			if (! restrictGuests) return;
			
			if (player.AuthenticatedAs == null)
			{
				ev.Cancelled = true;
				player.sendMessage ("<Restrict> You are not allowed to alter the world as a guest.");
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
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
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
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
						player.sendMessage ("<Restrict> You are not allowed to use this projectile as a guest.");
						player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
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
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
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
				player.sendMessage ("<Restrict> Type \"/reg password\" to request registration.");
			}
		}
    }
}
