using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.Administration;

namespace TheTool
{
    public static class IISManager
    {
        public static List<(string FullName, string Prefix, string Suffix)> GetAppPools()
        {
            var appPoolList = new List<(string FullName, string Prefix, string Suffix)>();


            //Separate app pool names into prefix and suffix for file manager
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    foreach (var pool in serverManager.ApplicationPools)
                    {
                        string name = pool.Name;

                        if (!string.IsNullOrEmpty(name) && name.Length >= 2)
                        {
                            string prefix = name.Substring(0, 2);
                            string suffix = name.Substring(2);
                            appPoolList.Add((name, prefix, suffix));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // You can log or rethrow depending on your needs
                Console.WriteLine("Error reading IIS app pools: " + ex.Message);
            }

            return appPoolList;
        }
    }
}
