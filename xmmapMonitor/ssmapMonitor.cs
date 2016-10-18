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

using System.Net;
using System.Timers;
using System.Net.Mail;

using System.Configuration;

using System.IO;

namespace xmmapMonitor
{
    public partial class ssmapMonitor : ServiceBase
    {
        #region attr
        string[] MonitorUrl;//监控的访问地址
        string[] webName;//网站名
        string[] webUrl;//网站链接
        string[] mapSourceLink;//地图源链接

        int webTimeInterval = 300000;
        int mapTimeInterval = 30000;

        string toAddress;//收件人列表
        string MessageSubject = "";//主题
        string MessageBody = "";//正文

        string host = "";//什么邮箱
        string emailAddress = "";//邮箱账号
        string emailName = "";//邮箱名
        string emailPs = "";//邮箱秘钥

        string comment = "";//备注

        HttpListener listener = null;//http监听协议

        string blackPath = "";//黑名单路径
        string blackFileName = "";//黑名单文件名

        string visitLogPath = "";//访问日志路径，ip，时间，url
        string visitFileName = "";//访问日志文件名，

        static List<string> blackList = new List<string>();//文件 内的黑名单

        static Dictionary<string, AccessStatistics> visitList = new Dictionary<string, AccessStatistics>();//间隔时间内的用户访问记录 ip，url，对应url的访问次数
        class AccessStatistics
        {
            public string userIp { get; set; }//用户ip
            public Dictionary<string, int> WebAccess { set; get; }//网站url，访问次数
        }
        #endregion

        public ssmapMonitor()
        {
            // InitializeComponent();
        }

        //thread for monitor
        protected override void OnStart(string[] args) //public void OnStart()//
        {
            ThreadPool.SetMaxThreads(4, 4);
            init();
            ThreadPool.QueueUserWorkItem(visitThread, "visitThread");
            if (webUrl != null)
            {
                ThreadPool.QueueUserWorkItem(webTimeThread, "webTime");
            }
            if (mapSourceLink != null)
            {
                ThreadPool.QueueUserWorkItem(mapTimeThread, "mapTime");
            }

        }

        protected override void OnStop()
        {
            sendMessage("监测程序已经关闭");
            listener.Stop();
            listener = null;
        }

        #region 初始化赋值
        private void init()
        {
            webName = ConfigurationManager.AppSettings["webName"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["webName"].ToString().Split(',');
            webUrl = ConfigurationManager.AppSettings["webUrl"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["webUrl"].ToString().Split(',');
            mapSourceLink = ConfigurationManager.AppSettings["mapSourceLink"].ToString().Split(',') == null ? new string[1] : ConfigurationManager.AppSettings["mapSourceLink"].ToString().Split(',');

            webTimeInterval = (ConfigurationManager.AppSettings["webTimeInterval"].ToString() == null || ConfigurationManager.AppSettings["webTimeInterval"].ToString() == "") ? 300000 : int.Parse(ConfigurationManager.AppSettings["webTimeInterval"].ToString());
            mapTimeInterval = (ConfigurationManager.AppSettings["mapTimeInterval"].ToString() == null || ConfigurationManager.AppSettings["mapTimeInterval"].ToString() == "") ? 30000 : int.Parse(ConfigurationManager.AppSettings["mapTimeInterval"].ToString());
           

            toAddress = ConfigurationManager.AppSettings["toAddress"].ToString() == null ? "" : ConfigurationManager.AppSettings["toAddress"].ToString();

            host = ConfigurationManager.AppSettings["host"].ToString() == null ? "" : ConfigurationManager.AppSettings["host"].ToString();

            MessageSubject = ConfigurationManager.AppSettings["MessageSubject"].ToString() == null ? "" : ConfigurationManager.AppSettings["MessageSubject"].ToString();
            MessageBody = ConfigurationManager.AppSettings["MessageBody"].ToString() == null ? "" : ConfigurationManager.AppSettings["MessageBody"].ToString();

            emailAddress = ConfigurationManager.AppSettings["emailAddress"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailAddress"].ToString();
            emailName = ConfigurationManager.AppSettings["emailName"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailName"].ToString();
            emailPs = ConfigurationManager.AppSettings["emailPs"].ToString() == null ? "" : ConfigurationManager.AppSettings["emailPs"].ToString();

            comment = ConfigurationManager.AppSettings["comment"].ToString() == null ? "" : ConfigurationManager.AppSettings["comment"].ToString();

            blackPath = ConfigurationManager.AppSettings["blackPath"].ToString() == null ? "C:\\ssmapMonitor\\black" : ConfigurationManager.AppSettings["blackPath"].ToString();

            blackFileName = ConfigurationManager.AppSettings["blackFileName"].ToString() == null ? "blackList" : ConfigurationManager.AppSettings["blackFileName"].ToString();

            visitLogPath = ConfigurationManager.AppSettings["visitLogPath"].ToString() == null ? "C:\\ssmapMonitor\\visitLog" : ConfigurationManager.AppSettings["visitLogPath"].ToString();
            visitFileName = ConfigurationManager.AppSettings["visitFileName"].ToString() == null ? "visitLog" : ConfigurationManager.AppSettings["visitFileName"].ToString();

            //listener part
            listenerPrefixes();
        }
        /// <summary>
        /// 添加监听路径,以/结尾
        /// </summary>
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

        #region visit Thread
        /// <summary>
        /// HttpListenerContext 会占会话，要返回结果，这段是黑名单的监控和重定向(以下是要将后台写在window service内的..)，
        /// 目前未实现iis后台和HttpListenerContext共同使用时的情况，响应方面会有异常= =
        /// 黑名单的监控重定向在这里只是鸡肋，不明觉厉，还需要学习
        /// </summary>
        /// <param name="b"></param>
        private void visitThread(object b)
        {
            while (true)
            {
                try
                {
                    listener.Start();
                    //IAsyncResult result = this.listener.BeginGetContext(new AsyncCallback(visitThread), this.listener);
                    HttpListenerContext context = listener.GetContext();
                    //异步监听
                   // IAsyncResult result = listener.BeginGetContext(new AsyncCallback(visitThread), listener);

                    if (context == null)
                        break;

                    #region 读取黑名单文件
                    blackList = File.ReadAllLines(blackPath + "\\" + blackFileName + ".json", new UTF8Encoding()).ToList();
                    #endregion

                    #region ip 获取
                    //获取IP地址
                    string ipv4 = String.Empty;
                    //context.Request.RemoteEndPoint 请求者的IP和端口号
                    //string requestPort = context.Request.RemoteEndPoint.Port.ToString();端口号
                    ipv4 = context.Request.RemoteEndPoint.Address.ToString();

                    if (ipv4 == String.Empty || ipv4 == "::1")
                    {
                        foreach (IPAddress IPA in Dns.GetHostAddresses(Dns.GetHostName()))
                        {
                            if (IPA.AddressFamily.ToString().Equals("InterNetwork"))
                            {
                                ipv4 = IPA.ToString();
                                break;
                            }
                        }
                    }

                    #endregion

                    #region 黑名单重定向
                    if (blackList.Contains(ipv4))
                    {
                        string strScript = "<script language=\"javascript\" type=\"text/javascript\">" +
                                                "window.location.href ='http://127.0.0.1:91/admin/404.html';" +
                                            "</script>";
                        System.IO.Stream outPutStream = context.Response.OutputStream;
                        byte[] con = System.Text.Encoding.UTF8.GetBytes(strScript);
                        outPutStream.Write(con, 0, con.Length);
                        context.Response.Close();
                        outPutStream.Close();
                        listener.Stop();
                        sendMessage("黑名单列表中的：\n\r"+ipv4+"\n在访问程序");
                        writeVisitLog("'黑名单IP'  " + ipv4, context.Request.Url.ToString());
                        continue;
                    }
                    #endregion

                    #region 访问日志
                    string url = context.Request.Url.ToString();
                    writeVisitLog(ipv4, url);
                    #endregion
                   
                    // to do response
                    continue;
                }
                catch (Exception ex)
                {
                    ErrorLog.RecordExceptionToFile(ex);
                }
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

                }
                catch (Exception ex)
                {
                    sendMessage("网站链接失败 \r\n<br/>\t" + webName[i] + "\r\n<br/>\t" + webUrl[i] + "\r\n<br/>\t" + ex.ToString());
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

                }
                catch (Exception ex)
                {
                    sendMessage("地图源链接失败 \r\n<br/>\t" + mapSourceLink[i] + "\r\n<br/>\t" + ex.ToString());
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
                //sc.UseDefaultCredentials = true; 
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

        #region 访问日志
        private void writeVisitLog(string ip, string url)
        {
            //取得当前需要写入的日志文件名称及路径
            string strFullPath = visitLogPath + @"\" + visitFileName + DateTime.Today.ToString("yyyyMMdd") + ".log";

            //执行写入
            //检查 Log 文件所存放的目录是否存在,如果不存在，建立该文件夹
            if (!Directory.Exists(visitLogPath))
            {
                Directory.CreateDirectory(visitLogPath);
            }

            //判断当前的日志文件是否创建，如果未创建，执行创建并加入异常内容；
            //如果已经创建则直接追加填写
            string strNow = DateTime.Now.ToString("HH:mm:ss");
            StringBuilder sbLog = new StringBuilder();

            sbLog.Append("\r\n----------------------\r\n");
            sbLog.Append(strNow);
            sbLog.Append("\r\n\tip:");
            sbLog.Append(ip);
            sbLog.Append("\r\n\turl:");
            sbLog.Append(url);
            if (!File.Exists(strFullPath))
            {
                using (StreamWriter sw = File.CreateText(strFullPath))
                {
                    sw.Write(sbLog.ToString());
                    sw.Flush();
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(strFullPath))
                {
                    sw.Write(sbLog.ToString());
                    sw.Flush();
                }
            }

        }
        #endregion
    }
}
