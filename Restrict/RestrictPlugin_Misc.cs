//#define LEGACY
using System.IO;
using System.Security.Cryptography;
using System.Text;

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

        protected string Hash(string username, string password)
        {
            var hash = SHA256.Create();
            var sb = new StringBuilder(64);
            var bytes = hash.ComputeHash(Encoding.ASCII.GetBytes(username + ":" + serverId + ":" + password));
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        //static Player FindPlayer (string name)
        //{
        //    name = name.ToLower();

        //    foreach (var p in Main.player)
        //    {
        //        if (p != null && p.Name != null && p.Name.ToLower() == name)
        //            return p;
        //    }

        //    return null;
        //}

        static string NameTransform(string name)
        {
            #if LEGACY
            return name.ToLower().Replace("=", "_EQUAL_");
            #else
            return name.ToLower();
            #endif
        }

        #if LEGACY
        static string OldNameTransform(string name)
        {
            return string.Concat("<", name.Replace("=", "_EQUAL_"), ">");
        }
        #endif
    }
}

