using System;
using System.IO;
using System.Text;
using System.Threading;

public static class SafeLogger
{
    private static readonly object _lock = new object();

    private static readonly string _logDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly string _logFile =
        Path.Combine(_logDir, $"error_{DateTime.Now:yyyyMMdd}.log");

    public static void LogException(string tag, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(_logDir);

            var sb = new StringBuilder();
            sb.AppendLine("======================================");
            sb.AppendLine($"Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"ThreadId : {Thread.CurrentThread.ManagedThreadId}");
            sb.AppendLine($"Tag : {tag}");
            sb.AppendLine($"Message : {ex.Message}");
            sb.AppendLine($"Type : {ex.GetType().FullName}");
            sb.AppendLine("StackTrace:");
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine();

            lock (_lock)
            {
                File.AppendAllText(_logFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {

        }
    }
}
