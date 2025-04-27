using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MouseRecorder
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 设置全局异常处理
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 应用程序初始化
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // 主应用程序循环
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception);
        }

        private static void HandleException(Exception ex)
        {
            string errorMessage = $"程序发生意外错误: {ex.Message}";
            string logPath = GetValidLogPath(out bool logAvailable);

            try
            {
                if (logAvailable)
                {
                    try
                    {
                        File.AppendAllText(logPath,
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex}{Environment.NewLine}{Environment.NewLine}");
                        errorMessage += $"\n\n错误日志位置: {logPath}\n\n请联系技术支持提供此日志文件。";
                    }
                    catch (Exception writeEx)
                    {
                        errorMessage += $"\n\n无法写入错误日志: {writeEx.Message}";
                    }
                }
            }
            finally
            {
                MessageBox.Show(errorMessage, "应用程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private static string GetValidLogPath(out bool isAvailable)
        {
            isAvailable = false;
            string[] candidatePaths =
            {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MouseRecorder"),
        Path.Combine(Path.GetTempPath(), "MouseRecorder"),
        AppDomain.CurrentDomain.BaseDirectory
    };

            foreach (var path in candidatePaths)
            {
                try
                {
                    Directory.CreateDirectory(path);
                    string testFile = Path.Combine(path, "test.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    isAvailable = true;
                    return Path.Combine(path, "error.log");
                }
                catch
                {
                    continue;
                }
            }

            return string.Empty;
        }
    }
}