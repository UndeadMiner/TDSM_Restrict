//#define API_Storage
using System;
using TDSM.API;
using System.IO;
using TDSM.API.Misc;
using TDSM.API.Data;
using TDSM.API.Logging;

namespace RestrictPlugin
{
	public class UserDB
	{
		PropertiesFile users;

		public bool CaseSensitive { get; set; }

		public void Initialise ()
		{
			if (!Storage.IsAvailable)
			{
				string pluginFolder = Globals.DataPath + Path.DirectorySeparatorChar + "Restrict";
				users = new PropertiesFile (pluginFolder + Path.DirectorySeparatorChar + "restrict_users.properties", !CaseSensitive);
				users.Save ();
			}
		}

		/// <summary>
		/// The total count of registered users
		/// </summary>
		/// <value>The count.</value>
		public int Count {
			get {
				if (Storage.IsAvailable)
				{
					return AuthenticatedUsers.UserCount;
				}
				else
				{
					return users.Count;
				}
			}
		}

		public bool Update (string username, string password, bool op)
		{
			if (Storage.IsAvailable)
			{
				if (AuthenticatedUsers.UserExists (username))
				{
					return AuthenticatedUsers.UpdateUser (username, password, op);
				}
				else
				{
					return AuthenticatedUsers.CreateUser (username, password, op);
				}
			}
			else
			{
				if (op) password += ":op";
				return users.Update (username, password);
			}
		}

		public void Load ()
		{
			if (!Storage.IsAvailable)
			{
				users.Load ();
			}
		}

		public void Save ()
		{
			if (!Storage.IsAvailable)
			{
				users.Save ();
			}
		}

		public UserDetails? Find (string username)
		{
			if (Storage.IsAvailable)
			{
				return AuthenticatedUsers.GetUser (username);
			}
			else
			{
//				ProgramLog.Log ("Looking for: " + username);
				var pw = users.Find (username);

//				ProgramLog.Log ("pw: " + (pw ?? "EMPTY"));
				if (pw != null)
				{
					var sp = pw.Split (':');

//					if (sp.Length > 1)
//					{
//						ProgramLog.Log ("first {0}, second {1}", sp[0], sp[1]);
//					}
//					else
//					{
//						ProgramLog.Log ("first {0}", sp[0]);
//					}

					return new UserDetails () {
						Password = sp.Length == 1 ? pw : sp [0],
						Operator = sp.Length == 1 ? false : sp [1] == "op",
						Username = username
					};
				}

				return null;
			}
		}
	}
}

