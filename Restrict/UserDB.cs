﻿//#define API_Storage
using OTA;
using System.IO;
using TDSM.Core.Data;
using TDSM.Core.Data.Models;
using OTA.Config;

namespace RestrictPlugin
{
    public class UserDB
    {
        PropertiesFile users;

        public bool CaseSensitive { get; set; }

        public void Initialise()
        {
            if (!Storage.IsAvailable)
            {
                string pluginFolder = Globals.DataPath + Path.DirectorySeparatorChar + "Restrict";
                users = new PropertiesFile(pluginFolder + Path.DirectorySeparatorChar + "restrict_users.properties", !CaseSensitive);
                users.Save();
            }
        }

        /// <summary>
        /// The total count of registered users
        /// </summary>
        /// <value>The count.</value>
        public long Count
        {
            get
            {
                if (Storage.IsAvailable)
                {
                    return Authentication.PlayerCount;
                }
                else
                {
                    return users.Count;
                }
            }
        }

        public bool Update(string username, string password, bool op)
        {
            if (Storage.IsAvailable)
            {
                if (Authentication.PlayerExists(username))
                {
                    return Authentication.UpdatePlayer(username, password, op);
                }
                else
                {
                    return Authentication.CreatePlayer(username, password, op) != null;
                }
            }
            else
            {
                if (op) password += ":op";
                return users.Update(username, password);
            }
        }

        public void Load()
        {
            if (!Storage.IsAvailable)
            {
                users.Load();
            }
        }

        public void Save()
        {
            if (!Storage.IsAvailable)
            {
                users.Save();
            }
        }

        public DbPlayer Find(string username)
        {
            if (Storage.IsAvailable)
            {
                return Authentication.GetPlayer(username);
            }
            else
            {
                //				ProgramLog.Log ("Looking for: " + username);
                var pw = users.Find(username);

                //				ProgramLog.Log ("pw: " + (pw ?? "EMPTY"));
                if (pw != null)
                {
                    var sp = pw.Split(':');

                    //					if (sp.Length > 1)
                    //					{
                    //						ProgramLog.Log ("first {0}, second {1}", sp[0], sp[1]);
                    //					}
                    //					else
                    //					{
                    //						ProgramLog.Log ("first {0}", sp[0]);
                    //					}

                    return new DbPlayer()
                    {
                        Password = sp.Length == 1 ? pw : sp[0],
                        Operator = sp.Length == 1 ? false : sp[1] == "op",
                        Name = username,
                        DateAdded = System.DateTime.UtcNow
                    };
                }

                return null;
            }
        }
    }
}

