using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using System.Net;
using System.Timers;
using System.Net.Mail;

using System.Configuration;

using System.IO;

using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

namespace xmmapMonitor
{
    public partial class ssmapMonitor : ServiceBase
    {
        #region attr
        string[] MonitorUrl;//监控的访问地址
        string[] webName;//网站名
        string[] webUrl;//网站链接
        string[] mapSourceLink;//地图源链接
        string[] dataBase;//数据库

        int webTimeInterval = 300000;
        int mapTimeInterval = 300000;
        int dbTimeInterval = 300000;

        int count = 5;

        string toAddress;//收件人列表
        string MessageSubject = "";//主题
        string MessageBody = "";//正文

        string host = "";//什么邮箱
        string emailAddress = "";//邮箱账号
        string emailName = "";//邮箱名
        string emailPs = "";//邮箱秘钥

        string comment = "";//备注

        HttpListener listener = null;//http监听协议

        //发送邮件机制 失败只发送一次，直到下一次监测到成功后重置
        static Dictionary<string, bool> isMessage = new Dictionary<string, bool>();
        static Dictionary<string, int> isMessageCount = new Dictionary<string, int>();//计数
        #endregion

        public ssmapMonitor()
        {
            // InitializeComponent();
        }

        #region 启动 ，关闭 线程
        protected override void OnStart(string[] args) //public void OnStart()//
        {
            ThreadPool.SetMaxThreads(5, 10);
            init();
            if (webUrl != null)
            {
                ThreadPool.QueueUserWorkItem(webTimeThread, "webTime");
            }
            if (mapSourceLink != null)
            {
                ThreadPool.QueueUserWorkItem(mapTimeThread, "mapTime");
            }
            if (dataBase != null)
            {
                ThreadPool.QueueUserWorkItem(dbTimeThread, "dbTime");
            }
        }

        protected override void OnStop()
        {
            // sendMessage("监测程序已经关闭");
            listener.Stop();
            listener = null;
        }

        #endregion

        #region 初始化赋值
        //参数的初始化
        private void init()
        {
            //地址链接
            webName = ConfigurationManager.AppSettings["webName"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["webName"].ToString().Split(',');
            webUrl = ConfigurationManager.AppSettings["webUrl"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["webUrl"].ToString().Split(',');
            mapSourceLink = ConfigurationManager.AppSettings["mapSourceLink"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["mapSourceLink"].ToString().Split(',');
            dataBase = ConfigurationManager.AppSettings["dataBase"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["dataBase"].ToString().Split(',');
            //时间间隔
            webTimeInterval = timeInterval("webTimeInterval");

            mapTimeInterval = timeInterval("mapTimeInterval");

            dbTimeInterval = timeInterval("dbTimeInterval");

            //连续次数
            try
            {
                count = (ConfigurationManager.AppSettings["count"].ToString() == null || ConfigurationManager.AppSettings["count"].ToString() == "") ? 5 : int.Parse(ConfigurationManager.AppSettings["count"].ToString());
            }
            catch (Exception ex)
            {
                ErrorLog.RecordExceptionToFile(ex);
                count = 5;
            }
            //邮件
            toAddress = ConfigurationManager.AppSettings["toAddress"].ToString() == null ? "" : ConfigurationManager.AppSettings["toAddress"].ToString();

            host = ConfigurationManager.AppSettings["host"].ToString() == null ? "" : ConfigurationManager.AppSettings["host"].ToString();

            MessageSubject = ConfigurationManager.AppSettings["MessageSubject"].ToString() == null ? "" : ConfigurationManager.AppSettings["MessageSubject"].ToString();
            MessageBody = ConfigurationManager.AppSettings["MessageBody"].ToString() == null ? "" : ConfigurationManager.AppSettings["MessageBody"].ToString();

            emailAddress = ConfigurationManager.AppSettings["emailAddress"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailAddress"].ToString();
            emailName = ConfigurationManager.AppSettings["emailName"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailName"].ToString();
            emailPs = ConfigurationManager.AppSettings["emailPs"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailPs"].ToString();

            comment = ConfigurationManager.AppSettings["comment"].ToString() == null ? "" : ConfigurationManager.AppSettings["comment"].ToString();

            isMessageInit();
            //listener part
            listenerPrefixes();
        }
        //监测时间间隔
        private int timeInterval(string appString)
        {
            try
            {
                return (ConfigurationManager.AppSettings[appString].ToString() == null || ConfigurationManager.AppSettings[appString].ToString() == "") ? 300000 : int.Parse(ConfigurationManager.AppSettings[appString].ToString());
            }
            catch (Exception ex)
            {
                ErrorLog.RecordExceptionToFile(ex);
                return 300000;
            }
        }
        //是否发送邮件状态的初始化
        private void isMessageInit()
        {
            //init 发送邮件状态
            foreach (string url in webUrl)
            {//网站状态
                isMessage.Add(url, true);
                isMessageCount.Add(url,count);
            }
            foreach (string link in mapSourceLink)//地图源状态
            {
                isMessage.Add(link, true);
                isMessageCount.Add(link, count);
            }
            foreach (string db in dataBase)//数据库状态
            {
                isMessage.Add(db, true);
                isMessageCount.Add(db, count);
            }
        }
        //添加监测路径
        private void listenerPrefixes()
        {
            listener = new HttpListener();
            MonitorUrl = ConfigurationManager.AppSettings["MonitorUrl"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["MonitorUrl"].ToString().Split(',');
            foreach (string monitorurl in MonitorUrl)
            {
                listener.Prefixes.Add(monitorurl);
            }
        }
        #endregion

        #region webTime Thread
        private void webTimeThread(object b)
        {
            System.Timers.Timer webTime = new System.Timers.Timer(webTimeInterval);//单位毫秒
            webTime.Elapsed += new ElapsedEventHandler(webTime_Elapsed);
            webTime.AutoReset = true;//多次执行true，只执行一次false，若使用需要再次t.start才可以再次执行
            webTime.Enabled = true;//将 Enabled 设置为 true 与调用 Start 相同，而将 Enabled 设置为 false 则与调用 Stop 相同。Elapsed 事件在 ThreadPool 线程上引发
        }
        #endregion

        #region mapSourceLink thread
        private void mapTimeThread(object b)
        {
            System.Timers.Timer mapTime = new System.Timers.Timer(mapTimeInterval);
            mapTime.Elapsed += new ElapsedEventHandler(mapTime_elapsed);
            mapTime.AutoReset = true;
            mapTime.Enabled = true;
        }
        #endregion

        #region database thread
        private void dbTimeThread(object b)
        {
            System.Timers.Timer dbTime = new System.Timers.Timer(dbTimeInterval);
            dbTime.Elapsed += new ElapsedEventHandler(dataBaseTime_elapsed);
            dbTime.AutoReset = true;
            dbTime.Enabled = true;
        }
        #endregion

        #region web的监控
        private void webTime_Elapsed(object sender, ElapsedEventArgs e)
        {
            //监测网站
            for (int i = 0; i < webUrl.Length; i++)
            {
                try
                {
                    HttpWebRequest hwr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(webUrl[i]);
                    System.Net.HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(webUrl[i]);
                    System.Net.HttpWebResponse _HttpWebResponse = (System.Net.HttpWebResponse)_HttpWebRequest.GetResponse();
                    System.IO.Stream _Stream = _HttpWebResponse.GetResponseStream();//得到回写的字节流
                    _HttpWebResponse.Close();
                    isMessage[webUrl[i]] = true;
                    isMessageCount[webUrl[i]] = count;
                }
                catch (Exception ex)
                {
                    if (isMessage[webUrl[i]] == false || isMessageCount[webUrl[i]]>0)
                    {
                        isMessageCount[webUrl[i]]--;
                        continue;
                    }
                    sendMessage("网站链接失败 \r\n<br/>\t" + webName[i] + "\r\n<br/>\t" + webUrl[i] + "\r\n<br/>\t" + ex.ToString());
                    isMessage[webUrl[i]] = false;
                    ErrorLog.RecordExceptionToFile(ex);
                }
            }
            return;
        }
        #endregion

        #region map的监控
        private void mapTime_elapsed(object sender, ElapsedEventArgs e)
        {
            //监测网站
            for (int i = 0; i < mapSourceLink.Length; i++)
            {
                try
                {
                    HttpWebRequest hwr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(mapSourceLink[i]);
                    System.Net.HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(mapSourceLink[i]);
                    System.Net.HttpWebResponse _HttpWebResponse = (System.Net.HttpWebResponse)_HttpWebRequest.GetResponse();
                    System.IO.Stream _Stream = _HttpWebResponse.GetResponseStream();//得到回写的字节流
                    _HttpWebResponse.Close();
                    isMessage[mapSourceLink[i]] = true;
                    isMessageCount[mapSourceLink[i]] = count;
                }
                catch (Exception ex)
                {
                    if (isMessage[mapSourceLink[i]] == false || isMessageCount[mapSourceLink[i]] > 0)
                    {
                        isMessageCount[mapSourceLink[i]]--;
                        continue;
                    }
                    sendMessage("地图源链接失败 \r\n<br/>\t" + mapSourceLink[i] + "\r\n<br/>\t" + ex.ToString());
                    isMessage[mapSourceLink[i]] = false;
                    ErrorLog.RecordExceptionToFile(ex);
                }
            }
            return;
        }
        #endregion

        #region 数据库的 监控
        private void dataBaseTime_elapsed(object sender, ElapsedEventArgs e)
        {
            //监测网站
            for (int i = 0; i < dataBase.Length; i++)
            {
                try
                {
                    OracleConnection oracleConnection = new OracleConnection(dataBase[i]);
                    oracleConnection.Open();
                    isMessage[dataBase[i]] = true;
                    isMessageCount[dataBase[i]] = count;
                    oracleConnection.Close();
                }
                catch (Exception ex)
                {
                    if (isMessage[dataBase[i]] == false || isMessageCount[dataBase[i]] > 0)
                    {
                        isMessageCount[dataBase[i]]--;
                        continue;
                    }
                    sendMessage("数据库打开失败 \r\n<br/>\t" + dataBase[i] + "\r\n<br/>\t" + ex.ToString());
                    isMessage[dataBase[i]] = false;
                    ErrorLog.RecordExceptionToFile(ex);
                }
            }
            return;
        }
        #endregion

        #region 发送邮件
        //发送邮件
        private void sendMessage(string body)
        {
            try
            {
                MailMessage message = new MailMessage();
                //发件人信息
                message.From = new MailAddress(emailAddress, emailName, new UTF8Encoding());
                //收件人信息//多个电子邮件地址之间必须用逗号字符（“,”）分隔。
                message.To.Add(toAddress);
                //邮件信息
                message.Subject = MessageSubject;
                message.Body = body;
                message.IsBodyHtml = true;    //是否为html格式 
                message.Priority = MailPriority.High; //发送邮件的优先等级 
                SmtpClient sc = new SmtpClient();
                sc.Host = host;    //指定发送邮件的服务器地址或IP 
                sc.Port = 25;       //指定发送邮件端口 
                sc.UseDefaultCredentials = false;
                //sc.EnableSsl = true; 
                sc.Credentials = new System.Net.NetworkCredential(emailAddress, emailPs); //指定登录服务器的用户名和密码 
                sc.Send(message);  //发送邮件 
            }
            catch (Exception ex)
            {
                ErrorLog.RecordExceptionToFile(ex);
            }
        }
        #endregion

    }
}
