//#define LEGACY
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using OTA;
using OTA.Command;
using OTA.Misc;
using OTA.Plugin;

//using TDSM.Core.ServerCore;
using OTA.Logging;
using OTA.Data;
using TDSM.Core.Plugin.Hooks;
using TDSM.Core.Data;
using TDSM.Core;
using TDSM.Core.Command;
using TDSM.Core.Data.Permissions;
using OTA.Config;
using OTA.Commands;
using TDSM.Core.Data.Models;
using OTA.Permissions;

[assembly: PluginDependency("OTA.Commands")]
[assembly: PluginDependency("TDSM.Core")]

namespace RestrictPlugin
{
    [OTAVersion(1, 0)]
    public partial class RestrictPlugin : BasePlugin
    {
        class RegistrationRequest
        {
            public string name;
            public string address;
            public string password;
        }

        PropertiesFile properties;
        UserDB users;

        Dictionary<int, RegistrationRequest> requests;
        int requestCount = 0;

        bool verbose
        {
            get { return properties.GetValue("verbose", false); }
        }

        bool allowGuests
        {
            get { return properties.GetValue("allow-guests", false); }
        }

        bool restrictGuests
        {
            get { return properties.GetValue("restrict-guests", true); }
        }

        bool restrictGuestsDoors
        {
            get { return properties.GetValue("restrict-guests-doors", true); }
        }

        bool restrictGuestsNPCs
        {
            get { return properties.GetValue("restrict-guests-npcs", true); }
        }

        string serverId
        {
            get { return properties.GetValue("server-id", "tdsm"); }
        }

        bool enableDefaultPassword
        {
            get { return properties.GetValue("enable-default-password", false); }
        }

        string message1
        {
            get { return properties.GetValue("altermessage", "Type \"/reg password\" to request registration."); }
        }

        string guestmessage
        {
            get { return properties.GetValue("guestmessage", "You are a guest, to register type: /reg yourpassword"); }
        }

        string regmessage
        {
            get { return properties.GetValue("regmessage", "Welcome back, registered user."); }
        }

        string opmessage
        {
            get { return properties.GetValue("opmessage", "This humble server welcomes back Their Operating Highness."); }
        }

        public const String ChestBreak = "restrict.chestbreak";
        public const String ChestOpen = "restrict.chestopen";
        public const String DoorChange = "restrict.doorchange";
        public const String LiquidFlow = "restrict.liquidflow";
        public const String NpcHurt = "restrict.npchurt";
        public const String ProjectileUse = "restrict.projectileuse";
        public const String SignEdit = "restrict.signedit";
        public const String WorldAlter = "restrict.worldalter";

        public RestrictPlugin()
        {
            Name = "Restrict";
            Description = "Restrict access to the server or character names.";
            Author = "UndeadMiner";
            Version = "0.39.0";
        }

        protected override void Initialized(object state)
        {
            ResetUsers();
            //Probably should check for existing login systems, But i'm not sure what undead would prefer atm.
            //Server.UsingLoginSystem = true;

            requests = new Dictionary<int, RegistrationRequest>();

            string pluginFolder = Globals.DataPath + Path.DirectorySeparatorChar + "Restrict";

            CreateDirectory(pluginFolder);

            properties = new PropertiesFile(pluginFolder + Path.DirectorySeparatorChar + "restrict.properties", false);
            //properties.Load();
            var dummy1 = allowGuests;
            var dummy2 = restrictGuests;
            var dummy3 = restrictGuestsDoors;
            var dummy4 = serverId;
            var dummy5 = restrictGuestsNPCs;
            var dummy6 = enableDefaultPassword;
            var dummy7 = verbose;
            var dummy8 = message1;
            var dummy9 = guestmessage;
            var dummy10 = regmessage;
            var dummy11 = opmessage;
            properties.Save();

            this.AddCommand<TDSMCommandInfo>("ru")
                .WithAccessLevel(AccessLevel.OP)
                .WithDescription("Register users or change their accounts")
                .SetOldHelpStyle()
                .WithHelpText("Adding users or changing passwords:")
                .WithHelpText("    ru [-o] [-f] <name> <hash>")
                .WithHelpText("    ru [-o] [-f] <name> -p <password>")
                .WithHelpText("Changing op status:")
                .WithHelpText("    ru -o [-f] <name>")
                .WithHelpText("    ru    [-f] <name>")
                .WithHelpText("Options:")
                .WithHelpText("    -o    make the player an operator")
                .WithHelpText("    -f    force action even if player isn't online")
                .WithPermissionNode("restrict.ru")
                .Calls(LockUsers<ISender, ArgumentList>(this.RegisterCommand));

            this.AddCommand<TDSMCommandInfo>("ur")
                .WithAccessLevel(AccessLevel.OP)
                .WithDescription("Unregister users")
                .SetOldHelpStyle()
                .WithHelpText("Deleting users:")
                .WithHelpText("    ur [-f] <name>")
                .WithHelpText("Options:")
                .WithHelpText("    -f    force action even if player isn't online")
                .WithPermissionNode("restrict.ur")
                .Calls(LockUsers<ISender, ArgumentList>(this.UnregisterCommand));

            this.AddCommand<TDSMCommandInfo>("ro")
                .WithAccessLevel(AccessLevel.OP)
                .WithDescription("Configure Restrict")
                .SetOldHelpStyle()
                .WithHelpText("Displaying options:")
                .WithHelpText("    ro")
                .WithHelpText("Setting options:")
                .WithHelpText("    ro [-f] [-g {true|false}] [-r {true|false}] [-s <serverId>] [-L]")
                .WithHelpText("Options:")
                .WithHelpText("    -f    force action")
                .WithHelpText("    -g    allow guests to enter the game")
                .WithHelpText("    -r    restrict guests' ability to alter tiles")
                .WithHelpText("    -s    set the server identifier used in hashing passwords")
                .WithHelpText("    -L    reload the user database from disk")
                .WithPermissionNode("restrict.ro")
                .Calls(LockUsers<ISender, ArgumentList>(this.OptionsCommand));

            this.AddCommand<TDSMCommandInfo>("rr")
                .WithAccessLevel(AccessLevel.OP)
                .WithDescription("Manage registration requests")
                .WithHelpText("         list registration requests")
                .WithHelpText("-g #     grant a registration request")
                .WithHelpText("grant #")
                .WithHelpText("-d #     deny a registration request")
                .WithHelpText("deny #")
                .WithPermissionNode("restrict.rr")
                .Calls(LockUsers<ISender, ArgumentList>(this.RequestsCommand));

            this.AddCommand<TDSMCommandInfo>("pass")
                .WithDescription("Change your password")
                .WithAccessLevel(AccessLevel.PLAYER)
                .WithHelpText("yourpassword")
                .WithPermissionNode("restrict.pass")
                .Calls(LockUsers<ISender, string>(this.PlayerPassCommand));

            this.AddCommand<TDSMCommandInfo>("reg")
                .WithDescription("Submit a registration request")
                .WithAccessLevel(AccessLevel.PLAYER)
                .WithHelpText("yourpassword")
                .WithPermissionNode("restrict.reg")
                .Calls(LockUsers<ISender, string>(this.PlayerRegCommand));

            this.AddCommand<TDSMCommandInfo>("login")
                .WithDescription("Allows a user to sign in after a reload")
                .WithAccessLevel(AccessLevel.PLAYER)
                .WithHelpText("yourpassword")
                .WithPermissionNode("restrict.login")
                .Calls(LockUsers<ISender, string>(this.PlayerLoginCommand));

            if (!enableDefaultPassword)
                Terraria.Netplay.ServerPassword = String.Empty;
        }

        Action<T, U> LockUsers<T, U>(Action<T, U> callback)
        {
            return delegate (T t, U u)
            {
                lock (users)
                {
                    callback(t, u);
                }
            };
        }

        /// <summary>
        /// Resets the restrict authenticated users upon reload
        /// </summary>
        void ResetUsers()
        {
            foreach (var plr in Terraria.Main.player)
            {
                if (plr != null && plr.GetAuthenticatedBy() == this.Name)
                {
                    //plr.AuthenticatedAs = null;
                    plr.SetAuthentication(null, this.Name);
                }
            }
        }

        protected override void Disposed(object state)
        {

        }

        protected override void Enabled()
        {
            ProgramLog.Log(base.Name + " enabled.");
        }

        protected override void Disabled()
        {
            ProgramLog.Log(base.Name + " disabled.");
        }

        [Hook]
        void OnStateChange(ref HookContext ctx, ref HookArgs.ServerStateChange args)
        {
            if (args.ServerChangeState == OTA.ServerState.Initialising)
            {
                //Data connectors must have loaded by now

                users = new UserDB()
                {
                    CaseSensitive = false
                };
                users.Initialise();
            }
        }

        [Hook(HookOrder.EARLY)]
        void OnPlayerDataReceived(ref HookContext ctx, ref TDSMHookArgs.PlayerDataReceived args)
        {
            if (ctx.Player != null && ctx.Player.IsAuthenticated())
                return;
            ctx.SetKick("Malfunction during login process, try again.");

            if (!args.NameChecked)
            {
                string error;
                if (!args.CheckName(out error))
                {
                    ctx.SetKick(error);
                    return;
                }
            }

            var player = ctx.Player;
            if (player == null)
            {
                ProgramLog.Error.Log("Null player passed to Restrict.OnPlayerDataReceived.");
                return;
            }

            var name = args.Name;
            //			var pname = NameTransform (name);
#if LEGACY
            var oname = OldNameTransform(name);
#endif
            DbPlayer entry = null;

            lock (users)
            {
#if LEGACY
                entry = users.Find(pname) ?? users.Find(oname);
#else
                entry = users.Find(name);
#endif
            }

            if (entry == null)
            {
                if (allowGuests)
                {
                    ctx.SetResult(HookResult.DEFAULT);
                    //player.AuthenticatedAs = null;
                    player.SetAuthentication(null, this.Name);
#if TDSM_QUEUE
                    (ctx.Connection as ClientConnection).DesiredQueue = 0; //(int)LoginPriority.QUEUE_LOW_PRIO;
#endif
                    ProgramLog.Log("<Restrict> Letting user {0} from {1} in as guest.", name, player.IPAddress);
                }
                else
                {
                    ProgramLog.Log("<Restrict> Unregistered user {0} from {1} attempted to connect.", name, player.IPAddress);
                    ctx.SetKick("Only registered users are allowed.");
                    return;
                }
                return;
            }

            ProgramLog.Log("<Restrict> Expecting password for user {0} from {1}.", name, player.IPAddress);
            ctx.SetResult(HookResult.ASK_PASS);
        }

        [Hook(HookOrder.EARLY)]
        void OnPlayerPassReceived(ref HookContext ctx, ref TDSMHookArgs.PlayerPassReceived args)
        {
            ctx.SetKick("Malfunction during login process, try again.");

            var player = ctx.Player;
            if (player == null)
            {
                ProgramLog.Error.Log("Null player passed to Restrict.OnPlayerPassReceived.");
                return;
            }

            var name = player.name;
            //			var pname = NameTransform (name);
#if LEGACY
            var oname = OldNameTransform(name);
#endif
            DbPlayer entry = null;

            //			String.Format ("User: {0}, Pass: {1}, pname: {2}, player.name: {3}", name, args.Password, pname, player.name);

            lock (users)
            {
#if LEGACY
                entry = users.Find(pname) ?? users.Find(oname);
#else
                entry = users.Find(name);
#endif
            }

            if (entry == null)
            {
                if (allowGuests)
                {
                    ctx.SetResult(HookResult.DEFAULT);
                    //player.AuthenticatedAs = null;
                    player.SetAuthentication(null, this.Name);
#if TDSM_QUEUE
                    (ctx.Connection as ClientConnection).DesiredQueue = 0;
#endif
                }
                else
                    ctx.SetKick("Only registered users are allowed.");

                return;
            }

            //            var split = entry.Split(':');
            //            var hash = split[0];
            var hash = entry.Password;
            var hash2 = Hash(name, args.Password);

            //			string db;
            //			{
            //				var mh = System.Security.Cryptography.SHA256.Create();
            //				var sb = new System.Text.StringBuilder(64);
            //				var bytes = mh.ComputeHash(System.Text.Encoding.ASCII.GetBytes(name + ":" + hash2));
            //				foreach (var b in bytes)
            //					sb.Append(b.ToString("x2"));
            //				db = sb.ToString();
            //			}
            //
            //			Console.WriteLine ("User: {0}, Pass: {1}, Hash: {3}, Hash2: {2}, UN: {4}, db: {5}", name, args.Password, hash2, hash, entry.Username, db);

            //            if (hash != hash2)

            //			ProgramLog.Log ("pw: {0}, hash2: {1}", entry.Password, hash2);
            //			if ((Storage.IsAvailable && !entry.ComparePassword (entry.Name, entry.Password))
            //			    ||
            //			    (!Storage.IsAvailable && entry.Password != hash2))
            //			{
            //				ctx.SetKick ("Incorrect password for user: " + name);
            //				return;
            //			}

            var authenticated = false;
            if (Storage.IsAvailable)
            {
                authenticated = entry.ComparePassword(entry.Name, args.Password);
            }
            else
            {
                authenticated = entry.Password == hash2;
            }

            if (!authenticated)
            {
                ctx.SetKick("Incorrect password for user: " + name);
                return;
            }

            if (entry.Operator)
                player.SetOp(true);

            //            if (split.Length > 1 && split[1] == "op")
            //            {
            //#if TDSM_QUEUE
            //                player.Op = true;
            //				(ctx.Connection as ClientConnection).DesiredQueue = 3;
            //#endif
            //            }
            //            else
            //            {
            //#if TDSM_QUEUE
            //				(ctx.Connection as ClientConnection).DesiredQueue = 1;
            //#endif
            //            }

            player.SetAuthentication(name, this.Name);
            ctx.SetResult(HookResult.DEFAULT);
        }

        [Hook(HookOrder.TERMINAL)] //might need to be HookOrder.NORMAL to work. (untested)
        void OnJoin(ref HookContext ctx, ref HookArgs.PlayerEnteredGame args)
        {
            var player = ctx.Player;

            if (player.name == null)
                return;

            if (!player.IsAuthenticated())
                player.Message(255, guestmessage);
            else if (player.IsOp())
                player.Message(255, new Color(128, 128, 255), opmessage);
            else
                player.Message(255, new Color(128, 255, 128), regmessage);

            return;
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

        [Hook(HookOrder.EARLY)]
        void OnSignTextSet(ref HookContext ctx, ref TDSMHookArgs.SignTextSet args)
        {
            var player = ctx.Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Log("<Restrict> Invalid player in OnSignTextSet.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (!restrictGuests)
                return;

            if (!player.IsAuthenticated())
            {
                ctx.SetResult(HookResult.IGNORE);
                player.SendTimed("<Restrict> You are not allowed to edit signs as a guest.");
                player.SendTimed("<Restrict> " + message1);
            }
            else if (IsRestrictedForUser(ctx.Player, SignEdit))
            {
                ctx.SetResult(HookResult.IGNORE);
                player.SendTimed("<Restrict> You are not allowed to edit signs without permissions.");
            }
        }

        [Hook(HookOrder.EARLY)]
        void OnAlter(ref HookContext ctx, ref TDSMHookArgs.PlayerWorldAlteration args)
        {
            var player = ctx.Player;
            //TODO
            //if (player == null && ctx.Sender is Projectile)
            //    player = (ctx.Sender as Projectile).Creator as Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Error.Log("<Restrict> Invalid player in OnAlter.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            //if (!restrictGuests) return;

            //if (player.AuthenticatedAs == null)
            //{
            //    ctx.SetResult(HookResult.RECTIFY);
            //    player.SendTimed("<Restrict> You are not allowed to alter the world as a guest.");
            //    player.SendTimed("<Restrict> " + message1);
            //}
            //else if (IsRestrictedForUser(ctx.Player, WorldAlter))
            //{
            //    ctx.SetResult(HookResult.RECTIFY);
            //    player.SendTimed("<Restrict> You are not allowed to alter the world without permissions.");
            //}

            if (IsRestrictedForUser(ctx.Player, WorldAlter))
            {
                ctx.SetResult(HookResult.RECTIFY);
                if (!player.IsAuthenticated())
                {
                    player.SendTimed("<Restrict> You are not allowed to alter the world as a guest.");
                    player.SendTimed("<Restrict> " + message1);
                }
                else
                {
                    player.SendTimed("<Restrict> You are not allowed to alter the world without permissions.");
                }
            }
        }

        [Hook(HookOrder.EARLY)]
        void OnSectionAlter(ref HookContext ctx, ref TDSMHookArgs.TileSquareReceived args)
        {
            var player = ctx.Player;
            if (player == null || player.name == null)
            {
                ProgramLog.Error.Log("<Restrict> Invalid player in OnAlter.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (IsRestrictedForUser(ctx.Player, WorldAlter))
            {
                ctx.SetResult(HookResult.RECTIFY);
                if (!player.IsAuthenticated())
                {
                    player.SendTimed("<Restrict> You are not allowed to alter the world as a guest.");
                    player.SendTimed("<Restrict> " + message1);
                }
                else
                {
                    player.SendTimed("<Restrict> You are not allowed to alter the world without permissions.");
                }
            }
        }

        [Hook(HookOrder.EARLY)]
        void OnChestBreak(ref HookContext ctx, ref HookArgs.ChestBreakReceived args)
        {
            var player = ctx.Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Log("<Restrict> Invalid player in OnChestBreak.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (!restrictGuests)
                return;

            if (!player.IsAuthenticated())
            {
                ctx.SetResult(HookResult.RECTIFY);
                player.SendTimed("<Restrict> You are not allowed to alter the world as a guest.");
                player.SendTimed("<Restrict> " + message1);
            }
            else if (IsRestrictedForUser(ctx.Player, ChestBreak))
            {
                ctx.SetResult(HookResult.RECTIFY);
                player.SendTimed("<Restrict> You are not allowed to alter the world without permissions.");
            }
        }

        [Hook(HookOrder.EARLY)]
        void OnChestOpen(ref HookContext ctx, ref TDSMHookArgs.ChestOpenReceived args)
        {
            var player = ctx.Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Log("<Restrict> Invalid player in OnChestOpen.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (!restrictGuests)
                return;

            if (!player.IsAuthenticated())
            {
                ctx.SetResult(HookResult.IGNORE);
                player.SendTimed("<Restrict> You are not allowed to open chests as a guest.");
                player.SendTimed("<Restrict> " + message1);
            }
            else if (IsRestrictedForUser(ctx.Player, ChestOpen))
            {
                ctx.SetResult(HookResult.RECTIFY);
                player.SendTimed("<Restrict> You are not allowed to open chests without permissions.");
            }
        }

        [Hook(HookOrder.LATE)]
        void OnLiquidFlow(ref HookContext ctx, ref TDSMHookArgs.LiquidFlowReceived args)
        {
            var player = ctx.Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Log("<Restrict> Invalid player in OnLiquidFlow.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (!restrictGuests)
                return;

            if (!player.IsAuthenticated())
            {
                ctx.SetResult(HookResult.RECTIFY);
                player.SendTimed("<Restrict> You are not allowed to alter the world as a guest.");
                player.SendTimed("<Restrict> " + message1);
            }
            else if (IsRestrictedForUser(ctx.Player, LiquidFlow))
            {
                ctx.SetResult(HookResult.RECTIFY);
                player.SendTimed("<Restrict> You are not allowed to alter the world without permissions.");
            }
        }

        [Hook(HookOrder.EARLY)]
        void
        OnProjectile(ref HookContext ctx, ref TDSMHookArgs.ProjectileReceived args)
        {
            var player = ctx.Player;
            //TODO
            //if (player == null && ctx.Sender is Projectile)
            //    player = (ctx.Sender as Projectile).Creator as Player;

            if (player == null || player.name == null)
            {
                ProgramLog.Error.Log("<Restrict> Invalid player in OnProjectile.");
                ctx.SetResult(HookResult.IGNORE);
                return;
            }

            if (!restrictGuests)
                return;

            //if (player.AuthenticatedAs == null)
            {
                switch (args.Type)
                {
                    /*case ProjectileType.N10_PURIFICATION_POWDER:
                            case ProjectileType.N11_VILE_POWDER:
                            case ProjectileType.N28_BOMB:
                            case ProjectileType.N37_STICKY_BOMB:
                            case ProjectileType.N29_DYNAMITE:
                            case ProjectileType.N30_GRENADE:
                            case ProjectileType.N31_SAND_BALL:
                            case ProjectileType.N39_MUD_BALL:
                            case ProjectileType.N40_ASH_BALL:
                            case ProjectileType.N42_SAND_BALL:
                            case ProjectileType.N43_TOMBSTONE:
                            case ProjectileType.N50_GLOWSTICK:
                            case ProjectileType.N53_STICKY_GLOWSTICK:*/
                    case 10:
                    case 11:
                    case 28:
                    case 37:
                    case 29:
                    case 30:
                    case 31:
                    case 39:
                    case 40:
                    case 42:
                    case 43:
                    case 50:
                    case 53:
                    case 439:
                    case 440:
                    case 453:
                        ctx.SetResult(HookResult.ERASE);
                        {
                            player.SendTimed("<Restrict> You are not allowed to use this projectile as a guest.");
                            player.SendTimed("<Restrict> " + message1);
                        }
                        else if (IsRestrictedForUser(ctx.Player, ProjectileUse))
                            player.SendTimed("<Restrict> You are not allowed to use this projectile without permissions.");

                        return;
                    default:
                        if (verbose)
                            ProgramLog.Debug.Log("Non blocked projectile: " + args.Type);
                        break;
                }
            }

            return;
        }

        //		[Hook (HookOrder.EARLY)]
        //		void OnDoorStateChanged (ref HookContext ctx, ref HookArgs.DoorStateChanged args)
        //		{
        //			if ((!restrictGuests) || (!restrictGuestsDoors))
        //				return;
        //
        //			var player = ctx.Player;
        //
        //			if (player == null)
        //				return;
        //
        //			if (player.AuthenticatedAs == null)
        //			{
        //				ctx.SetResult (HookResult.RECTIFY);
        //				player.SendTimed ("<Restrict> You are not allowed to open and close doors as a guest.");
        //				player.SendTimed ("<Restrict> " + message1);
        //			}
        //			else if (IsRestrictedForUser (ctx.Player, DoorChange))
        //			{
        //				ctx.SetResult (HookResult.RECTIFY);
        //				player.SendTimed ("<Restrict> You are not allowed to open and close doors without permissions.");
        //			}
        //		}

        [Hook(HookOrder.EARLY)]
        void OnNPCHurt(ref HookContext ctx, ref TDSMHookArgs.NpcHurtReceived args)
        {
            if ((!restrictGuests) || (!restrictGuestsNPCs))
                return;

            var player = ctx.Player;

            if (player == null)
                return;

            if (!player.IsAuthenticated())
            {
                ctx.SetResult(HookResult.IGNORE);
                player.SendTimed("<Restrict> You are not allowed to hurt NPCs as a guest.");
                player.SendTimed("<Restrict> " + message1);
            }
            else if (IsRestrictedForUser(ctx.Player, NpcHurt))
            {
                ctx.SetResult(HookResult.IGNORE);
                player.SendTimed("<Restrict> You are not allowed to hurt NPCs without permissions.");
            }
        }

        #region Permissions

        public bool IsRestrictedForUser(BasePlayer player, string node)
        {
#if LEGACY
            if (!player.Op && OTA.Permissions.PermissionsManager.IsSet)
            {
                var isRegistered = player.AuthenticatedAs != null;
                if (isRegistered)
                {
                    var user = OTA.Permissions.PermissionsManager.IsPermitted(node, player);
                    var grp = OTA.Permissions.PermissionsManager.IsPermittedForGroup(node, (attributes) =>
                        {
                            return attributes.ContainsKey("ApplyToRegistered") && attributes["ApplyToRegistered"].ToLower() == "true";
                        });

                    if (user == OTA.Permissions.Permission.Denied)
                        return true;
                    else if (user == OTA.Permissions.Permission.Permitted)
                        return false;

                    return grp != OTA.Permissions.Permission.Permitted;
                }
                else
                {
                    var grp = OTA.Permissions.PermissionsManager.IsPermittedForGroup(node, (attributes) =>
                        {
                            return attributes.ContainsKey("ApplyToGuests") && attributes["ApplyToGuests"].ToLower() == "true";
                        });

                    return grp != OTA.Permissions.Permission.Permitted;
                }
            }
#else
            if (!player.IsOp() && Storage.IsAvailable)
                return Storage.IsPermitted(node, player) != Permission.Permitted;
#endif

            return !player.IsOp();
        }

        #endregion
    }

    internal static class PlayerExtensions
    {
        public static void SendTimed(this BasePlayer player, string message, byte A = 255, byte R = 255, byte G = 0, byte B = 0)
        {
            const Int32 TimeInSeconds = 2; //TODO config
            const String TimerKey = "restrict-msg-timer";

            var key = TimerKey + message;
            if (player.PluginData == null)
                player.PluginData = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();
            if (player.PluginData.ContainsKey(key))
            {
                var date = DateTime.Now - (DateTime)player.PluginData[key];
                if (date.TotalSeconds >= TimeInSeconds)
                {
                    player.PluginData[key] = DateTime.Now;
                    player.SendMessage(message, A, R, G, B);
                }
            }
            else
            {
                player.PluginData.TryAdd(key, DateTime.Now);
                player.SendMessage(message, A, R, G, B);
            }
        }
    }
}
