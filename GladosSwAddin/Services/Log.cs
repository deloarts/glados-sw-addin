using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GladosSwAddin.Services
{
    /// <summary>Severity of a <see cref="Log"/> entry.</summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Process-wide diagnostic log: one plain-text file per day under
    /// %LOCALAPPDATA%\GladosSwAddin\logs\glados-yyyyMMdd.log, the last <see cref="RetainedDays"/>
    /// files kept. Static rather than an ApplicationServices member because the static
    /// SolidWorks-facing helpers (SwCustomPropertyService) and the COM entry point log too, and
    /// logging must work before - and regardless of - the rest of the composition root.
    ///
    /// Debug entries only reach the debugger output (they include custom-property values,
    /// which do not belong on disk); Info and above are also appended to the file. Every entry
    /// carries the process id because several SOLIDWORKS instances can share one file. Logging
    /// never throws: a locked or unwritable file just loses that line.
    ///
    /// Never pass credentials to it. Callers log exceptions and messages, never request headers
    /// or the personal access token; GladosApiException messages are backend error texts and
    /// contain neither.
    /// </summary>
    public static class Log
    {
        private const string AppFolderName = "GladosSwAddin";
        private const string LogFolderName = "logs";
        private const string FilePrefix = "glados-";
        private const string FileExtension = ".log";
        internal const int RetainedDays = 14;

        private static readonly object FileLock = new object();
        private static readonly int ProcessId = Process.GetCurrentProcess().Id;
        private static string _pruneDoneForDay;
        private static string _pruneDirectory;

        /// <summary>Redirects the log files away from the user's profile; the unit tests point
        /// it at a temp folder so exercising code that logs never writes to real user files.</summary>
        internal static string DirectoryOverride { get; set; }

        /// <summary>The folder holding the log files (created on first write), for the
        /// Settings dialog's "open log folder" link.</summary>
        public static string Directory
        {
            get
            {
                if (!string.IsNullOrEmpty(DirectoryOverride))
                    return DirectoryOverride;

                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, AppFolderName, LogFolderName);
            }
        }

        public static void Debug(string message) => Write(LogLevel.Debug, message, null);

        public static void Info(string message) => Write(LogLevel.Info, message, null);

        public static void Warning(string message, Exception exception = null) =>
            Write(LogLevel.Warning, message, exception);

        public static void Error(string message, Exception exception = null) =>
            Write(LogLevel.Error, message, exception);

        private static void Write(LogLevel level, string message, Exception exception)
        {
            try
            {
                var now = DateTime.Now;
                var line = new StringBuilder()
                    .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                    .Append(" [").Append(ProcessId.ToString(CultureInfo.InvariantCulture)).Append("] ")
                    .Append(level.ToString().ToUpperInvariant().PadRight(7))
                    .Append(' ').Append(message);
                if (exception != null)
                    line.AppendLine().Append(exception);

                var text = line.ToString();
                System.Diagnostics.Debug.WriteLine("Glados: " + text);

                if (level == LogLevel.Debug)
                    return;

                lock (FileLock)
                {
                    var directory = Directory;
                    if (directory != _pruneDirectory)
                    {
                        _pruneDirectory = directory;
                        _pruneDoneForDay = null;
                    }

                    System.IO.Directory.CreateDirectory(directory);

                    var day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    var path = Path.Combine(directory, FilePrefix + day + FileExtension);

                    // FileShare.ReadWrite so a second SOLIDWORKS process (or someone reading the
                    // file) never blocks this one; Append keeps each write at the end.
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(text);
                    }

                    if (_pruneDoneForDay != day)
                    {
                        _pruneDoneForDay = day;
                        PruneOldFiles(directory);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Glados: could not write log file: " + ex.Message);
            }
        }

        /// <summary>File names sort chronologically (yyyyMMdd), so everything but the newest
        /// <see cref="RetainedDays"/> files goes.</summary>
        private static void PruneOldFiles(string directory)
        {
            var stale = new DirectoryInfo(directory)
                .GetFiles(FilePrefix + "*" + FileExtension)
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .Skip(RetainedDays);

            foreach (var file in stale)
            {
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    // Still open in another process; the next day's prune retries.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
