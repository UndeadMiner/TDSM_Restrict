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

            public static class ColumnNames
            {
                public const String Id = "Id";
                public const String Username = "Username";
                public const String Password = "Password";
                public const String Operator = "Operator";
                public const String DateAdded = "DateAdded";
            }

            public static readonly TableColumn[] Columns = new TableColumn[]
            {
                new TableColumn(ColumnNames.Id, typeof(Int32), true, true),
                new TableColumn(ColumnNames.Username, typeof(String), 255),
                new TableColumn(ColumnNames.Password, typeof(String), 255),
                new TableColumn(ColumnNames.Operator, typeof(Boolean)),
                new TableColumn(ColumnNames.DateAdded, typeof(DateTime))
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
                if (!UserExists(username))
                {
                    bl.InsertInto(UserTable.TableName, new DataParameter[] {
                        new DataParameter(UserTable.ColumnNames.Username, username),
                        new DataParameter(UserTable.ColumnNames.Password, password),
                        new DataParameter(UserTable.ColumnNames.Operator, false),
                        new DataParameter(UserTable.ColumnNames.DateAdded, DateTime.Now)
                    });
                }
                else
                {
                    bl.Update(UserTable.TableName, new DataParameter[] {
                            new DataParameter(UserTable.ColumnNames.Password, password)
                        },
                        new WhereFilter(UserTable.ColumnNames.Username, username)
                    );
                }

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
                bl.SelectFrom(UserTable.TableName, new string[] { UserTable.ColumnNames.Password }, new WhereFilter(UserTable.ColumnNames.Username, username));

                return Storage.ExecuteScalar<String>(bl);
            }
#else
            return users.Find(username);
#endif
        }

#if API_Storage
        public bool UserExists(string username)
        {
            using (var bl = Storage.GetBuilder(SQLSafePluginName))
            {
                bl.Select().Count().From(UserTable.TableName).Where(new WhereFilter(UserTable.ColumnNames.Username, username));

                return Storage.ExecuteScalar<Int64>(bl) > 0;
            }
        }
#endif


#if API_Storage

#endif
    }
}

