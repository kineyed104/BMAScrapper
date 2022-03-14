using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Timers;

namespace Diagonostics
{
    public class Log
    {
        public static string BaseDirectory;
        public static bool EnableWriteFile = true;
        public static bool RedirectToConsole = false;
        public static TimeSpan LogTruncatePeriod = TimeSpan.FromDays(30);
        public static int TruncateSize = 1000;
        public static Action<string> TraceWriter = message => Trace.WriteLine(message);
        public static event Action<string, string> LogWritten;

        private static Timer CleanTimer = new Timer();

        static Log()
        {
            try
            {

                BaseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "log");

                if (!Directory.Exists(BaseDirectory))
                    Directory.CreateDirectory(BaseDirectory);
                else
                {
                    Clean(BaseDirectory);
                }

                CleanTimer.Interval = 1000 * 86400; // 1 day
                CleanTimer.Elapsed += new ElapsedEventHandler(CleanTimer_Elapsed);
                CleanTimer.Start();
            }
            catch (Exception ex)
            {
                BaseDirectory = Path.Combine(Path.GetTempPath(), "BDLog");
                try
                {
                    if (!Directory.Exists(BaseDirectory))
                        Directory.CreateDirectory(BaseDirectory);
                }
                catch { }
                TraceWriter(ex.ToString());
            }
        }

        private static void CleanTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Clean(BaseDirectory);
        }

        private static void Clean(string baseDirectory)
        {
            foreach (string path in Directory.GetFiles(baseDirectory))
            {
                try
                {
                    DateTime lastTime = File.GetLastWriteTime(path);
                    if (lastTime > DateTime.Now)
                    {
                        WriteError("Time", "CurrentTime is incorrect. " + DateTime.Now);
                        return;
                    }

                    if (lastTime.Add(LogTruncatePeriod) < DateTime.Now)
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    WriteError("Log", "Error : " + ex.ToString());
                }
            }
        }

        public static void Write(string category, string message)
        {
            string filename = GetFileName(category);
            string content = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}({(DateTime.Now - Process.GetCurrentProcess().StartTime).ToString("d\\.hh\\:mm\\:ss")})][{category}] {message}";

            try
            {
                if (RedirectToConsole)
                    Console.WriteLine(content);

                TraceWriter(content);

                if (EnableWriteFile)
                {
                    if (!File.Exists(filename))
                    {
                        using (StreamWriter sw = File.CreateText(filename))
                        {
                            sw.WriteLine(content);
                        }
                    }
                    else
                    {
                        using (StreamWriter sw = File.AppendText(filename))
                        {
                            sw.WriteLine(content);
                        }
                    }
                }

                LogWritten?.Invoke(category, content);
            }
            catch (Exception ex)
            {
                TraceWriter("Log.Write Error : " + ex.ToString());

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var errorFileName = GetFileName("Log.Write.Error");
                        if (!File.Exists(errorFileName))
                        {
                            using (StreamWriter sw = File.CreateText(errorFileName))
                            {
                                sw.WriteLine(ex.ToString() + $"\nCategory={category}, Message={message}");
                            }
                        }
                        else
                        {
                            using (StreamWriter sw = File.AppendText(errorFileName))
                            {
                                sw.WriteLine(content);
                            }
                        }

                        return;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }

        public static void WriteError(string category, string message)
        {
            Write(category + ".Error", message);
        }

        public static void WriteWithCodeInfo(string category, string message)
        {
            Write(category, message + GetCodeInfo(2));
        }

        private static string GetCodeInfo(int stackDepth)
        {
            string strTemp = "";
            StackTrace st = new StackTrace(true);
            if (st.FrameCount > stackDepth)
            {
                StackFrame sf = st.GetFrame(stackDepth);
                strTemp += String.Format(" <-- Method : {0} L:{1} ", sf.GetMethod(), sf.GetFileLineNumber());
            }

            return strTemp;
        }

        private static string GetFileName(string category)
        {
            return Path.Combine(BaseDirectory, category + "@" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }
    }
}
