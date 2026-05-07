using System;
using System.IO;
using System.Text;
using System.Threading;

namespace POS_UI.Services
{
	public static class LogService
	{
		private static readonly object _sync = new object();
		private static string GetLogFilePath()
		{
			// Ensure base folder exists and write to daily file
			PathService.EnsureInitialized();
			var baseFolder = PathService.GetBaseFolderPath();
			var date = DateTime.Now.ToString("yyyy-MM-dd");
			var file = Path.Combine(baseFolder, $"pos-log-{date}.txt");
			return file;
		}

		public static void Info(string message)
		{
#if DEBUG
			Write("INFO", message);
#else
			// no-op in Release
#endif
		}

		public static void Warn(string message)
		{
#if DEBUG
			Write("WARN", message);
#else
			// no-op in Release
#endif
		}

		public static void Error(string message, Exception ex = null)
		{
			var details = ex == null ? message : message + " | " + FlattenException(ex);
			Write("ERROR", details);
		}

		private static void Write(string level, string message)
		{
			try
			{
				var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
				var path = GetLogFilePath();
				lock (_sync)
				{
					File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
				}
			}
			catch
			{
				// Swallow logging errors to avoid cascading failures in production
			}
		}

		private static string FlattenException(Exception ex)
		{
			var sb = new StringBuilder();
			int depth = 0;
			while (ex != null && depth < 5)
			{
				sb.Append($"{ex.GetType().Name}: {ex.Message}");
				if (!string.IsNullOrWhiteSpace(ex.StackTrace))
				{
					sb.Append("\nStack: ").Append(ex.StackTrace);
				}
				ex = ex.InnerException;
				if (ex != null) sb.Append("\nInner-> ");
				depth++;
			}
			return sb.ToString();
		}
	}
}
