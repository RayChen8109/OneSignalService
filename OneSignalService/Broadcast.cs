using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneSignalService
{
    public class Broadcast
    {
        public int id { get; set; }
        public string typeName { get; set; }
        public string titleName { get; set; }
        public string sendObject { get; set; }
        public string txtContent { get; set; }
        public Nullable<System.DateTime> sendTime { get; set; }
        public Nullable<int> isNowSend { get; set; }
        public Nullable<int> sendOkNum { get; set; }
        public Nullable<int> sendOK { get; set; }
        public string responseContent { get; set; }
        public string onesignalID { get; set; }
        public Nullable<int> isTiming { get; set; }
    }
}
