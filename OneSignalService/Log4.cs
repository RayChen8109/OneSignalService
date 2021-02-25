using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OneSignalService
{
    public static class Log4
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static void Info(string info)
        {
            log.Info(info);
        }

        public static void Error(string error)
        {
            log.Error(error);
        }
    }
}
