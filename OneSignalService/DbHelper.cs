using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace OneSignalService
{
    public static class DbHelper
    {
        public static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["DajiangDapper"].ConnectionString; }
        }

        public static bool Update(int id, Broadcast broadcast, params string[] proNames)
        {
            bool IsCorrect = false;
            string sqlcommand = "UPDATE [Dajiang].[dbo].[Broadcast] SET ";
            StringBuilder sb = new StringBuilder(sqlcommand);
            try
            {
                string lastParam = proNames.Last();
                foreach(string proName in proNames)
                {
                    if (proName.Equals(lastParam))
                        sb.Append($"{ proName } = @{ proName }");
                    else
                        sb.Append($"{ proName } = @{ proName },");
                }
                sb.Append($" WHERE id = {id};");

                using(IDbConnection db = new SqlConnection(ConnectionString))
                {
                    int result = db.Execute(sb.ToString(), broadcast);
                    IsCorrect = result > 0 ? true : false;
                }
            }
            catch(Exception e)
            {
                Log4.Error(e.ToString());
            }
            return IsCorrect;
        }
    }
}
