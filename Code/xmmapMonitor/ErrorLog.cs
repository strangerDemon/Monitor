using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Configuration;

namespace xmmapMonitor
{
    class ErrorLog
    {
        #region 异常信息写入日志文件
        public static bool RecordExceptionToFile(Exception ex)
        {
            try
            {
                string logPath = ConfigurationManager.AppSettings["logPath"].ToString() == null ? "" : ConfigurationManager.AppSettings["logPath"].ToString();
                string logFileName = ConfigurationManager.AppSettings["logFileName"].ToString() == null ? "" : ConfigurationManager.AppSettings["logFileName"].ToString();
                if (ex.GetType().ToString() == "System.Threading.ThreadAbortException")
                {
                    return false;
                }

                //取得当前需要写入的日志文件名称及路径
                string strFullPath = logPath + @"\" + logFileName+DateTime.Today.ToString("yyyyMMdd")+".log";

                //取得异常信息的内容
                string logErrorInfo = GetLogInfo(ex);

                //执行写入
                //检查 Log 文件所存放的目录是否存在,如果不存在，建立该文件夹
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                //判断当前的日志文件是否创建，如果未创建，执行创建并加入异常内容；
                //如果已经创建则直接追加填写
                if (!File.Exists(strFullPath))
                {
                    using (StreamWriter sw = File.CreateText(strFullPath))
                    {
                        sw.Write(logErrorInfo);
                        sw.Flush();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(strFullPath))
                    {
                        sw.Write(logErrorInfo);
                        sw.Flush();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 组织异常信息字符串
        /// <summary>
        /// 组织异常信息字符串
        /// </summary>
        /// <Coder>董彦雷：2016-6-12</Coder>
        /// <Modifier></Modifier>
        /// <param name="ex">异常变量</param>
        /// <returns>异常信息字符串</returns>
        private static string GetLogInfo(Exception ex)
        {
            try
            {
                string strNow = DateTime.Now.ToString("HH:mm:ss");
                StringBuilder sbLog = new StringBuilder();

                sbLog.Append("\r\n----------------------\r\n");
                sbLog.Append(strNow);
                sbLog.Append("\r\n\tSource:");
                sbLog.Append(ex.Source);
                sbLog.Append("\r\n\tMessage:");
                sbLog.Append(ex.Message);
                sbLog.Append("\r\n\tStackTrace:");
                sbLog.Append(ex.StackTrace);

                if (ex.InnerException != null)
                {
                    sbLog.Append("\r\n\tInnerException:");
                    sbLog.Append(ex.InnerException.StackTrace);
                }

                return sbLog.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
        #endregion
    }
}
