using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace cTest
{
    static class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // ★ 全域攔截網（第3層防護）：背景/UI 執行緒任何未攔截例外，進程終止前留下 stack trace。
            //   過去完全沒有此機制，閃退無跡可循；自包含檔案寫入不依賴 LogManager 狀態，最適合最後防線。
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.ThreadException += OnThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            WriteCrashLog("Application.ThreadException", e.Exception);
        }

        private static void WriteCrashLog(string source, Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "Crash_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                string msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " || [" + source + "] "
                           + (ex != null ? ex.ToString() : "(null exception)") + Environment.NewLine
                           + "----------------------------------------" + Environment.NewLine;
                File.AppendAllText(file, msg, System.Text.Encoding.UTF8);
            }
            catch (Exception inner)
            {
                // 忽略原因：全域 crash 最後防線，連 log 檔都寫不了時若再拋例外將造成遞迴崩潰，
                //          故以 Trace 警告留痕後吞掉，不可向外傳播。
                CrashTrace.Warn("WriteCrashLog 失敗（已忽略）", inner);
            }
        }
    }

    /// <summary>crash 最後防線專用的極簡警告記錄器（寫 Trace，本身不會拋例外）。</summary>
    internal static class CrashTrace
    {
        internal static void Warn(string message, Exception ex)
        {
            Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [WARN] " + message + " :: " + (ex != null ? ex.Message : ""));
        }
    }
}
