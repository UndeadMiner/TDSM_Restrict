#define API_Storage
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
        #if API_Storage
        const String SQLSafePluginName = "Restrict";

        private class UserTable
        {
            public const String TableName = "Users";

            public static readonly TableColumn[] Columns = new TableColumn[]
            {
                new TableColumn("Id", typeof(Int32), true, true),
                new TableColumn("Username", typeof(String), 255),
                new TableColumn("Password", typeof(String), 255),
                new TableColumn("Operator", typeof(Boolean)),
                new TableColumn("DateAdded", typeof(DateTime))
            };

            public static bool Exists()
            {
                using (var bl = Storage.GetBuilder(SQLSafePluginName))
                {
                    bl.TableExists(TableName);

                    return Storage.Execute(bl);
                }
            }

            public static bool Create()
            {
                using (var bl = Storage.GetBuilder(SQLSafePluginName))
                {
                    bl.TableCreate(TableName, Columns);

                    return Storage.ExecuteNonQuery(bl) > 0;
                }
            }
        }
        
        #else
        PropertiesFile users;
        #endif

        public void Initialise()
        {
            #if API_Storage
            //Check to see if table exists

            if (!UserTable.Exists())
            {
                ProgramLog.Admin.Log("Restrict user table does not exist and will now be created");
                UserTable.Create();
            }

            #else
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
                using (var bl = Storage.GetBuilder(SQLSafePluginName))
                {
                    bl
                        .Select()
                        .Count()
                        .From(UserTable.TableName);

                    return Storage.ExecuteScalar<Int32>(bl);
                }
                #else
                return users.Count;
                #endif
            }
        }

        public bool Update(string username, string password)
        { 
            #if API_Storage
            using (var bl = Storage.GetBuilder(SQLSafePluginName))
            {
                bl.Update(UserTable.TableName, new DataParameter[]
                    { 
                        new DataParameter("Password", password)
                    }, 
                    new WhereFilter("Username", username)
                );

                return Storage.ExecuteNonQuery(bl) > 0;
            }
            #else
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
            using (var bl = Storage.GetBuilder(SQLSafePluginName))
            {
                bl.SelectFrom(UserTable.TableName, new string[] { "Password" }, new WhereFilter("Username", username));

                var results = Storage.ExecuteArray<String>(bl);
                if (results.Length > 0)
                    return results[0];
            }

            return null;
            #else
            return users.Find(username);
            #endif
        }


        #if API_Storage
        
        #endif
    }
}

