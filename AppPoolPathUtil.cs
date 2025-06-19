using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheTool
{
    public static class AppPoolPathUtil
    {
        public static string GetSitePath(string appPoolName)
        {
            if (string.IsNullOrWhiteSpace(appPoolName) || appPoolName.Length < 3)
                return null;

            string prefix = appPoolName.Substring(0, 2);
            string suffix = appPoolName.Substring(2);

            return $"f:\\inetpub\\wwwroot\\{prefix}\\{suffix}";
        }
    }
}
