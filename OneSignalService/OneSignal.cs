using log4net.Config;
using System;
using System.Timers;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OneSignalService
{
    public class OneSignal
    {
        private readonly Timer timer;
        public OneSignal()
        {
            // Configure log4net from app.config file
            XmlConfigurator.Configure();
            timer = new Timer();
            timer.Interval = 300000;
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Elapsed += TimerHandler;
        }

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }

        private void TimerHandler(object sender, EventArgs e)
        {
            BroadcastMessageToSend();
        }

        private void BroadcastMessageToSend()
        {
            DateTime now5a = DateTime.Now.AddMinutes(-5);
            DateTime now5b = DateTime.Now.AddMinutes(5);
            using (IDbConnection db = new SqlConnection(DbHelper.ConnectionString))
            {
                string sqlcommand = "SELECT * FROM [Dajiang].[dbo].[Broadcast] WHERE isTiming = @isTiming AND sendOK = @sendOK AND sendTime > @now5a AND sendTime < @now5b";
                List<Broadcast> list = db.Query<Broadcast>(sqlcommand, new { isTiming = 1,
                                                                             sendOK = 0,
                                                                             now5a = now5a,
                                                                             now5b = now5b}).AsList();

                if(list!=null && list.Count > 0)
                {
                    foreach(Broadcast item in list)
                    {
                        if(item.typeName == "point")
                        {
                            SendDouble_point(item);
                        }
                        else if(item.typeName == "all")
                        {
                            SendAll(item);
                        }
                        else
                        {
                            SendDouble(item);
                        }
                    }
                }
            }
        }

        private bool SendAll(Broadcast model)
        {
            var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;

            request.KeepAlive = true;
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";

            request.Headers.Add("authorization", "Basic NmQyZGU4ZTktOTJkNS00NDE0LTgxN2ItZmU0YmI1MDcwY2Uw");

            string sendTime = string.Empty;
            //PeterYu 2018/04/01 改直接發送，已由系統排程執行
            /*if (model.isNowSend == 1)
            { }
            else
            {
                if (model.sendTime.HasValue)
                {
                    sendTime = "\"send_after\": \"" + Convert.ToDateTime(model.sendTime).AddHours(-15).ToString("yyyy-MM-dd HH:mm:ss") + " GMT-0700" + "\",";
                }
            }*/

            string sendJson = "{"
                                                    + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                    + "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                    + "\"contents\": {\"en\": \"" + model.txtContent + "\"},"
                                                    + "\"content_available\": \"true\","
                                                    + "\"mutable_content\": \"true\","
                                                    + sendTime
                                                    + "\"included_segments\": [\"All\"]}";

            Log4.Info("[Before]Onesignal推播All 反饋 發送json：" + sendJson);

            byte[] byteArray = Encoding.UTF8.GetBytes("{"
                                                    + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                    //+ "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                    + "\"contents\": {\"en\": \"" + model.titleName + "\"},"
                                                    + "\"content_available\": \"true\","
                                                    + "\"mutable_content\": \"true\","
                                                    + sendTime
                                                    + "\"included_segments\": [\"All\"]}");

            string responseContent = null;

            bool sendok = true;

            try
            {
                using (var writer = request.GetRequestStream())
                {
                    writer.Write(byteArray, 0, byteArray.Length);
                }

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        responseContent = reader.ReadToEnd();
                    }
                }
                sendok = true;
            }
            catch (WebException ex)
            {
                sendok = false;

                string _streamResponse = "NULL";

                if (ex.Response != null && ex.Response.GetResponseStream() != null)
                    _streamResponse = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();

                Log4.Error("Onesignal推播All 錯誤： 時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ex.Message + " Response:" + _streamResponse + " 發送json：" + sendJson);
            }

            Log4.Info("Onesignal推播All 反饋：時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + responseContent + " 發送json：" + sendJson);
            if (sendok)
            {
                try
                {
                    OnesignalReturnJson objJson = JsonConvert.DeserializeObject<OnesignalReturnJson>(responseContent);
                    if (objJson != null)
                    {
                        model.onesignalID = objJson.id;
                        if (!string.IsNullOrEmpty(objJson.recipients))
                        {
                            model.sendOkNum = int.Parse(objJson.recipients);
                        }
                    }
                }
                catch { }

                model.sendOK = 1;
                model.responseContent = responseContent;
                DbHelper.Update(model.id, model, "sendOK", "responseContent", "onesignalID", "sendOkNum");
            }
            else
            {
                model.sendOK = 0;
                DbHelper.Update(model.id, model, "sendOK");
            }
            return true;
        }

        /// <summary>
        /// 傳送推播訊息至卡號列表的會員
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private bool SendDouble(Broadcast model)
        {
            var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;
            request.KeepAlive = true;
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("authorization", "Basic NmQyZGU4ZTktOTJkNS00NDE0LTgxN2ItZmU0YmI1MDcwY2Uw");
            string sendTime = string.Empty;

            if (!string.IsNullOrEmpty(model.sendObject))
            {
                string sdObj = string.Empty;
                StringBuilder strBd = new StringBuilder();

                string[] split = model.sendObject.Replace("，", ",").Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (split != null && split.Count() > 0)
                {
                    foreach (string str in split)
                    {
                        if (!string.IsNullOrEmpty(str))
                        {
                            if (strBd.Length > 0)
                                strBd.Append(",{\"operator\": \"OR\"},{\"field\": \"tag\", \"key\": \"cardno\", \"relation\": \"=\", \"value\": \"" + str + "\"}");
                            else
                                strBd.Append("{\"field\": \"tag\", \"key\": \"cardno\", \"relation\": \"=\", \"value\": \"" + str + "\"}");
                        }
                    }

                    sdObj = strBd.ToString();

                    string sendJson = "{"
                                                                       + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                                       + "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                                       + "\"contents\": {\"en\": \"" + model.txtContent + "\"},"
                                                                       + "\"content_available\": \"true\","
                                                                       + "\"mutable_content\": \"true\","
                                                                       + sendTime
                                                                       + "\"filters\": [" + sdObj + "]}";

                    Log4.Info("[Before]Onesignal推播Double 反饋 發送json：" + sendJson);

                    byte[] byteArray = Encoding.UTF8.GetBytes("{"
                                                                       + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                                       //+ "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                                       + "\"contents\": {\"en\": \"" + model.titleName + "\"},"
                                                                       + "\"content_available\": \"true\","
                                                                       + "\"mutable_content\": \"true\","
                                                                       + sendTime
                                                                       + "\"filters\": [" + sdObj + "]}");

                    string responseContent = null;
                    bool sendok = true;

                    try
                    {
                        using (var writer = request.GetRequestStream())
                        {
                            writer.Write(byteArray, 0, byteArray.Length);
                        }

                        using (var response = request.GetResponse() as HttpWebResponse)
                        {
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                responseContent = reader.ReadToEnd();
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        sendok = false;
                        string _streamResponse = "NULL";

                        if (ex.Response != null && ex.Response.GetResponseStream() != null)
                            _streamResponse = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();

                        Log4.Error("Onesignal推播Double 錯誤： 時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ex.Message + " Response:" + _streamResponse + " 發送json：" + sendJson);
                    }

                    Log4.Info("Onesignal推播Double 反饋：時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + responseContent + " 發送json：" + sendJson);
                    if (sendok)
                    {
                        try
                        {
                            OnesignalReturnJson objJson = JsonConvert.DeserializeObject<OnesignalReturnJson>(responseContent);
                            if (objJson != null)
                            {
                                model.onesignalID = objJson.id;
                                if (!string.IsNullOrEmpty(objJson.recipients))
                                {
                                    model.sendOkNum = int.Parse(objJson.recipients);
                                }
                            }
                        }
                        catch { }
                        model.sendOK = 1;
                        model.responseContent = responseContent;
                        DbHelper.Update(model.id, model, "sendOK", "responseContent", "onesignalID", "sendOkNum");
                    }
                    else
                    {
                        model.sendOK = 0;
                        DbHelper.Update(model.id, model, "sendOK");
                    }
                }

            }
            else
            {
            }
            return true;
        }
        //測試超過200筆的推播

        //int _count = 0; //暫存目前推播到的會員數字
        //int limit = 100; //每次推播的數量上限(不能超過200個)
        //int _limit = 100; //每次推播的數量上限(不能超過200個)
        //int multiply = 1; //推播的次數
        //int _sandOkNum = 0; //送出推播的設備數量
        //string onesignal_id = ""; //推播ID
        //public bool SendDouble(MODEL.Broadcast model)
        //{
        //    var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;

        //    request.KeepAlive = true;
        //    request.Method = "POST";
        //    request.ContentType = "application/json; charset=utf-8";

        //    request.Headers.Add("authorization", "Basic NmQyZGU4ZTktOTJkNS00NDE0LTgxN2ItZmU0YmI1MDcwY2Uw");

        //    string sendTime = string.Empty;
        //    //PeterYu 2018/04/01 改直接發送，已由系統排程執行
        //    /*if (model.isNowSend == 1)
        //    { }
        //    else
        //    {
        //        if (model.sendTime.HasValue)
        //        {
        //            sendTime = "\"send_after\": \"" + Convert.ToDateTime(model.sendTime).AddHours(-15).ToString("yyyy-MM-dd HH:mm:ss") + " GMT-0700" + "\",";
        //        }
        //    }*/
        //    if (!string.IsNullOrEmpty(model.sendObject))
        //    {
        //        string sdObj = string.Empty;
        //        StringBuilder strBd = new StringBuilder();
        //        limit = multiply * _limit;
        //        string[] split = model.sendObject.Replace("，", ",").Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        //        int split_count = split.Length;
        //        int _num = 0;
        //        if (split != null && split.Count() > 0)
        //        {
        //            foreach (string str in split)
        //            {
        //                if (!string.IsNullOrEmpty(str))
        //                {
        //                    if (_num == limit)
        //                    {
        //                        multiply++;
        //                        _count = _num;
        //                        break;
        //                    }
        //                    _num++;
        //                    if (_num > _count)
        //                    {
        //                        if (strBd.Length > 0)
        //                            strBd.Append(",{\"operator\": \"OR\"},{\"field\": \"tag\", \"key\": \"cardno\", \"relation\": \"=\", \"value\": \"" + str + "\"}");
        //                        else
        //                            strBd.Append("{\"field\": \"tag\", \"key\": \"cardno\", \"relation\": \"=\", \"value\": \"" + str + "\"}");
        //                    }
        //                }
        //            }

        //            sdObj = strBd.ToString();

        //            string sendJson = "{"
        //                                                               + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
        //                                                               + "\"headings\": {\"en\": \"" + model.titleName + "\"},"
        //                                                               + "\"contents\": {\"en\": \"" + model.txtContent + "\"},"
        //                                                               + "\"content_available\": \"true\","
        //                                                               + "\"mutable_content\": \"true\","
        //                                                               + sendTime
        //                                                               + "\"filters\": [" + sdObj + "]}";

        //            Log4.Info("[Before]Onesignal推播Double 反饋 發送json：" + sendJson);

        //            byte[] byteArray = Encoding.UTF8.GetBytes("{"
        //                                                               + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
        //                                                               //+ "\"headings\": {\"en\": \"" + model.titleName + "\"},"
        //                                                               + "\"contents\": {\"en\": \"" + model.titleName + "\"},"
        //                                                               + "\"content_available\": \"true\","
        //                                                               + "\"mutable_content\": \"true\","
        //                                                               + sendTime
        //                                                               + "\"filters\": [" + sdObj + "]}");

        //            string responseContent = null;
        //            bool sendok = true;

        //            //以機器識別碼傳送推播訊息
        //            //byteArray = Encoding.UTF8.GetBytes("{"
        //            //                                       + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
        //            //                                       //+ "\"headings\": {\"en\": \"" + model.titleName + "\"},"
        //            //                                       + "\"contents\": {\"en\": \"" + model.titleName + "\"},"
        //            //                                       + "\"content_available\": \"true\","
        //            //                                       + "\"mutable_content\": \"true\","
        //            //                                       + sendTime
        //            //                                       + "\"include_player_ids\": [\"bda38b21-7a9d-4bb9-bcba-16748eb3b40b\"]}");
        //            try
        //            {
        //                using (var writer = request.GetRequestStream())
        //                {
        //                    writer.Write(byteArray, 0, byteArray.Length);
        //                }

        //                using (var response = request.GetResponse() as HttpWebResponse)
        //                {
        //                    using (var reader = new StreamReader(response.GetResponseStream()))
        //                    {
        //                        responseContent = reader.ReadToEnd();
        //                    }
        //                }
        //            }
        //            catch (WebException ex)
        //            {
        //                sendok = false;
        //                string _streamResponse = "NULL";

        //                if (ex.Response != null && ex.Response.GetResponseStream() != null)
        //                    _streamResponse = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();

        //                Log4.Error("Onesignal推播Double 錯誤： 時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ex.Message + " Response:" + _streamResponse + " 發送json：" + sendJson );
        //            }

        //            Log4.Info("Onesignal推播Double 反饋：時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + responseContent + " 發送json：" + sendJson);

        //            if (sendok)
        //            {
        //                try
        //                {
        //                    MODEL.ViewModel.OnesignalReturnJson objJson = JsonConvert.DeserializeObject<MODEL.ViewModel.OnesignalReturnJson>(responseContent);
        //                    if (objJson != null)
        //                    {
        //                        onesignal_id = objJson.id;
        //                        if (!string.IsNullOrEmpty(objJson.recipients))
        //                        {
        //                            _sandOkNum += int.Parse(objJson.recipients);
        //                        }
        //                    }
        //                }
        //                catch { }
        //            }

        //            bool _next = false;
        //            if (_num < split_count)
        //            {

        //                _next = true;
        //                SendDouble(model);
        //                return false;
        //            }
        //            if (sendok && !_next)
        //            {
        //                //try
        //                //{
        //                //    MODEL.ViewModel.OnesignalReturnJson objJson = JsonConvert.DeserializeObject<MODEL.ViewModel.OnesignalReturnJson>(responseContent);
        //                //    if (objJson != null)
        //                //    {
        //                //        model.onesignalID = objJson.id;
        //                //        if (!string.IsNullOrEmpty(objJson.recipients))
        //                //        {
        //                //            model.sendOkNum = int.Parse(objJson.recipients) + _sandOkNum;
        //                //        }
        //                //    }
        //                //}
        //                //catch { }
        //                model.onesignalID = onesignal_id;
        //                model.sendOkNum = _sandOkNum;
        //                model.sendOK = 1;
        //                model.responseContent = responseContent;                        
        //                OperateContext.Current.BLLSession.IBroadcastBLL.ModifyID(model, model.id, "sendOK", "responseContent");
        //            }
        //            else
        //            {
        //                model.sendOK = 0;
        //                OperateContext.Current.BLLSession.IBroadcastBLL.ModifyID(model, model.id, "sendOK");
        //            }
        //        }
        //    }
        //    else
        //    {
        //    }
        //    return true;
        //}

        ServiceReference1.MetroAppSoapClient client = new ServiceReference1.MetroAppSoapClient();
        /// <summary>
        /// 傳送點數到期推播，每個人因為點數不同，所以個別推送
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool SendDouble_point(Broadcast model)
        {
            string sendTime = string.Empty;
            //PeterYu 2018/04/01 改直接發送，已由系統排程執行
            /*if (model.isNowSend == 1)
            { }
            else
            {
                if (model.sendTime.HasValue)
                {
                    sendTime = "\"send_after\": \"" + Convert.ToDateTime(model.sendTime).AddHours(-15).ToString("yyyy-MM-dd HH:mm:ss") + " GMT-0700" + "\",";
                }
            }*/
            if (!string.IsNullOrEmpty(model.sendObject))
            {
                string sdObj = string.Empty;

                string[] split = model.sendObject.Replace("，", ",").Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                string saveContect = string.Empty;

                if (split != null && split.Count() > 0)
                {
                    int isOK = 0;
                    foreach (string str in split)
                    {
                        string sendContect = string.Empty;

                        if (!string.IsNullOrEmpty(str))
                        {
                            sdObj = "{\"field\": \"tag\", \"key\": \"cardno\", \"relation\": \"=\", \"value\": \"" + str + "\"}";
                        }
                        else
                        {
                            continue;
                        }
                        try
                        {
                            string SecretKey = MD5Hash.MD5("MetroWalk2016$").ToLower();
                            DateTime time = DateTime.Now;
                            string EncryptionStrStr = "@MetroWalk!" + SecretKey + time.ToString("yyyyMMddHHmmss");
                            string EncryptionStrMD5 = MD5Hash.MD5(EncryptionStrStr).ToLower();
                            byte[] bytes = Encoding.UTF8.GetBytes(EncryptionStrMD5);
                            string EncryptionStr = Convert.ToBase64String(bytes);
                            //Log4.Info(EncryptionStr + " " + time.ToString("yyyyMMddHHmmss"));

                            string reJson = client.getChaPiPointByCardnoJSON(str, EncryptionStr, time.ToString("yyyyMMddHHmmss"));
                            PointJSON pJson = new PointJSON();

                            pJson = JsonConvert.DeserializeObject<PointJSON>(reJson);//反序列化

                            if (pJson != null && !string.IsNullOrEmpty(pJson.CLEAR_POINT))
                            {
                                saveContect = saveContect + " " + pJson.CLEAR_POINT;
                                sendContect = model.txtContent.Replace("{0}", pJson.CLEAR_POINT);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            saveContect = saveContect + " " + "獲取失敗";
                            continue;
                        }

                        string sendJson = "{"
                                                                       + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                                       + "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                                       + "\"contents\": {\"en\": \"" + sendContect + "\"},"
                                                                       + "\"content_available\": \"true\","
                                                                       + "\"mutable_content\": \"true\","
                                                                       + sendTime
                                                                       + "\"filters\": [" + sdObj + "]}";

                        Log4.Info("[Before]Onesignal推播Double 反饋 發送json：" + sendJson);

                        byte[] byteArray = Encoding.UTF8.GetBytes("{"
                                                                           + "\"app_id\": \"a648fea6-6cbe-4004-9983-d698cd120b66\","
                                                                           + "\"headings\": {\"en\": \"" + model.titleName + "\"},"
                                                                           + "\"contents\": {\"en\": \"" + sendContect + "\"},"
                                                                           + "\"content_available\": \"true\","
                                                                           + "\"mutable_content\": \"true\","
                                                                           + sendTime
                                                                           + "\"filters\": [" + sdObj + "]}");

                        string responseContent = null;
                        bool sendok = true;

                        try
                        {
                            var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;

                            request.KeepAlive = true;
                            request.Method = "POST";
                            request.ContentType = "application/json; charset=utf-8";

                            request.Headers.Add("authorization", "Basic NmQyZGU4ZTktOTJkNS00NDE0LTgxN2ItZmU0YmI1MDcwY2Uw");

                            using (var writer = request.GetRequestStream())
                            {
                                writer.Write(byteArray, 0, byteArray.Length);
                            }
                            //var writer = request.GetRequestStream();
                            //writer.Write(byteArray, 0, byteArray.Length);

                            using (var response = request.GetResponse() as HttpWebResponse)
                            {
                                using (var reader = new StreamReader(response.GetResponseStream()))
                                {
                                    responseContent = reader.ReadToEnd();
                                }
                            }
                        }
                        catch (WebException ex)
                        {
                            sendok = false;

                            string _streamResponse = "NULL";

                            if (ex.Response != null && ex.Response.GetResponseStream() != null)
                                _streamResponse = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();

                            Log4.Error("Onesignal推播Double 錯誤： 時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ex.Message + " Response:" + _streamResponse + " 發送json：" + sendJson);
                        }

                        Log4.Info("Onesignal推播Double 反饋：時間-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + responseContent + " 發送json：" + sendJson);
                        if (sendok)
                        {
                            try
                            {
                                OnesignalReturnJson objJson = JsonConvert.DeserializeObject<OnesignalReturnJson>(responseContent);
                                if (objJson != null)
                                {
                                    model.onesignalID = objJson.id;
                                    if (!string.IsNullOrEmpty(objJson.recipients))
                                    {
                                        model.sendOkNum = model.sendOkNum.HasValue ? (model.sendOkNum + int.Parse(objJson.recipients)) : int.Parse(objJson.recipients);
                                    }
                                }
                            }
                            catch { }
                            //model.sendOK = 1;
                            isOK++;
                        }
                        else
                        {
                            //model.sendOK = 0;
                        }

                    }
                    model.sendOK = 1;

                    model.responseContent = saveContect + " 對象發送成功數：" + isOK;
                    DbHelper.Update(model.id, model, "sendOK", "responseContent", "onesignalID", "sendOkNum");
                }
            }
            else
            {
            }


            return true;
        }
    }
}
