using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GladosSwAddin.Api;
using GladosSwAddin.Models;
using Newtonsoft.Json;

namespace GladosSwAddin.Services
{
    /// <summary>Persists the offline lookup cache (Manufacturer/Article Group/Fabrication
    /// Method/Inspection Method/Source Item Type/Source Item lists — see LookupCacheEnvelope
    /// and LookupSnapshot) to
    /// %LOCALAPPDATA%\GladosSwAddin\cache\&lt;serverKey&gt;\lookups.json, one file per
    /// configured server so switching servers can't mix up their data. serverKey is the first
    /// 16 hex characters of the SHA-256 hash of the normalised (trimmed, trailing-slash-
    /// stripped, lower-cased) server base URL. LookupService is the only caller.</summary>
    public class LookupCacheService
    {
        /// <summary>Bumped if the persisted JSON shape ever changes incompatibly — a stored
        /// blob from an older schema is treated the same as "nothing stored" rather than
        /// risking a mis-parsed cache. 2: FabricationMethodResponse gained DefaultForAssemblies —
        /// a version-1 file can't carry it, and its stored ETag would keep revalidating as
        /// current (the server's ETag fingerprints row counts and change times, not columns), so
        /// it has to be refetched once.</summary>
        private const int CurrentSchemaVersion = 2;

        private const string AppFolderName = "GladosSwAddin";
        private const string CacheFolderName = "cache";
        private const string FileName = "lookups.json";
        private const int MaxReadRetries = 3;
        private const int RetryDelayMilliseconds = 50;

        /// <summary>Writes the envelope atomically: a temp file in the same directory, flushed
        /// to disk, then File.Replace over the existing file (File.Move when there isn't one
        /// yet — File.Replace requires the destination to already exist). Serialized against
        /// other processes/instances by a named mutex (see AcquireLock) that is taken and
        /// released here, on this one thread, synchronously — a Mutex is thread-affine, so it
        /// must never be held across an await. Only the write is locked; concurrent downloads
        /// by two instances are harmless (each write is a complete server snapshot, last
        /// writer wins). Best-effort: a failed write just means the next TryLoad falls back to
        /// whatever (if anything) is already on disk.</summary>
        public void Save(string serverUrl, LookupCacheEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var normalizedUrl = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrEmpty(normalizedUrl))
                return;

            envelope.SchemaVersion = CurrentSchemaVersion;
            envelope.ServerUrl = normalizedUrl;

            var serverKey = ComputeServerKey(normalizedUrl);
            var json = JsonConvert.SerializeObject(envelope, GladosApiClient.JsonSettings);

            using (AcquireLock(serverKey))
            {
                try
                {
                    var directory = GetCacheDirectory(serverKey);
                    Directory.CreateDirectory(directory);

                    var targetPath = Path.Combine(directory, FileName);
                    var tempPath = Path.Combine(directory, FileName + "." + Guid.NewGuid().ToString("N") + ".tmp");

                    using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(json);
                        writer.Flush();
                        stream.Flush(true);
                    }

                    if (File.Exists(targetPath))
                        File.Replace(tempPath, targetPath, null);
                    else
                        File.Move(tempPath, targetPath);
                }
                catch (IOException ex)
                {
                    Log.Warning("Could not write the lookup cache", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Warning("Could not write the lookup cache", ex);
                }
            }
        }

        /// <summary>Returns null if there's no cache yet for this server, it can't be read, or
        /// it doesn't match (schema version or server URL) — any of those means "no cache", not
        /// an error. Opens with FileShare.ReadWrite | FileShare.Delete and retries a few times
        /// on IOException, since a concurrent writer (this process or another SolidWorks
        /// instance) may hold the file briefly during its own atomic replace.</summary>
        public LookupCacheEnvelope TryLoad(string serverUrl)
        {
            var normalizedUrl = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrEmpty(normalizedUrl))
                return null;

            var path = Path.Combine(GetCacheDirectory(ComputeServerKey(normalizedUrl)), FileName);

            for (int attempt = 1; attempt <= MaxReadRetries; attempt++)
            {
                try
                {
                    string json;
                    using (var stream = new FileStream(
                        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        json = reader.ReadToEnd();
                    }

                    var envelope = JsonConvert.DeserializeObject<LookupCacheEnvelope>(json, GladosApiClient.JsonSettings);
                    if (envelope == null || envelope.SchemaVersion != CurrentSchemaVersion)
                        return null;

                    if (!string.Equals(NormalizeServerUrl(envelope.ServerUrl), normalizedUrl, StringComparison.Ordinal))
                        return null;

                    return envelope;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (DirectoryNotFoundException)
                {
                    return null;
                }
                catch (IOException)
                {
                    if (attempt == MaxReadRetries)
                        return null;
                    Thread.Sleep(RetryDelayMilliseconds);
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (JsonException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>Named mutex Local\GladosSwAddin.lookups.&lt;serverKey&gt; guarding a write
        /// against another process (another running instance of the add-in, in another
        /// SolidWorks session) writing at the same time. Acquired and released on the calling
        /// thread only — never hold it across an await. An abandoned mutex (a prior holder
        /// crashed/exited without releasing) is treated as acquired — the file itself is always
        /// left consistent by Save's temp-file-then-replace, so there's nothing to recover.
        /// Never throws: if the mutex can't even be created, the caller proceeds unlocked
        /// rather than skipping the write entirely.</summary>
        private static IDisposable AcquireLock(string serverKey)
        {
            Mutex mutex;
            try
            {
                mutex = new Mutex(false, MutexName(serverKey));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Could not create the lookup cache mutex, proceeding unlocked", ex);
                return NoOpLock.Instance;
            }

            bool owned;
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            return new MutexLock(mutex, owned);
        }

        private static string GetCacheDirectory(string serverKey)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppFolderName, CacheFolderName, serverKey);
        }

        private static string MutexName(string serverKey)
        {
            return "Local\\GladosSwAddin.lookups." + serverKey;
        }

        /// <summary>Trimmed and trailing-slash-stripped, lower-cased — used both as the stored
        /// ServerUrl (so a same-server comparison is a plain ordinal Equals) and as the input to
        /// ComputeServerKey.</summary>
        private static string NormalizeServerUrl(string url)
        {
            return (url ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        }

        /// <summary>First 16 hex characters of the SHA-256 hash of the normalised server base
        /// URL — short enough for a directory/mutex name, long enough that two different
        /// servers colliding is not a practical concern.</summary>
        private static string ComputeServerKey(string normalizedUrl)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedUrl));
                var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                return hex.Substring(0, 16);
            }
        }

        private sealed class MutexLock : IDisposable
        {
            private readonly Mutex _mutex;
            private readonly bool _owned;

            public MutexLock(Mutex mutex, bool owned)
            {
                _mutex = mutex;
                _owned = owned;
            }

            public void Dispose()
            {
                if (_owned)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Already released/abandoned — nothing left to do.
                    }
                }

                _mutex.Dispose();
            }
        }

        private sealed class NoOpLock : IDisposable
        {
            public static readonly NoOpLock Instance = new NoOpLock();

            public void Dispose()
            {
            }
        }
    }
}
