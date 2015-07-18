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
#if !API_Storage
        PropertiesFile users;
#endif

        public void Initialise()
        {
#if !API_Storage
            string pluginFolder = Globals.DataPath + Path.DirectorySeparatorChar + "Restrict";
            users = new PropertiesFile(pluginFolder + Path.DirectorySeparatorChar + "restrict_users.properties", false);
            users.Save();
#endif
        }

        /// <summary>
        /// The total count of registered users
        /// </summary>
        /// <value>The count.</value>
        public int Count
        {
            get
            {
#if API_Storage
                return AuthenticatedUsers.UserCount;
#else
                return users.Count;
#endif
            }
        }

        public bool Update(string username, string password, bool op)
        {
#if API_Storage
            if (AuthenticatedUsers.UserExists(username))
            {
                return AuthenticatedUsers.UpdateUser(username, password, op);
            }
            else
            {
                return AuthenticatedUsers.CreateUser(username, password, op);
            }
#else
            if (op) username += ":op";
            return users.Update(username, password);
#endif
        }

        public void Load()
        {
#if API_Storage
#else
            users.Load();
#endif
        }

        public void Save()
        {
#if API_Storage
#else
            users.Save();
#endif
        }

        public string Find(string username)
        {
#if API_Storage
            return AuthenticatedUsers.GetUserPassword(username);
#else
            return users.Find(username);
#endif
        }


        #if API_Storage

        #endif
    }
}

