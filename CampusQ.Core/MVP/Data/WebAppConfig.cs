using System;
using System.IO;
using System.Threading;

namespace CampusQ.MVP.Data
{
    public static class WebAppConfig
    {
        // CampusQ.Web runs as a separate child process from the CampusQ desktop launcher, so a plain
        // static field cannot be used to hand the cloudflared-generated tunnel URL from the launcher
        // process over to the web process. Instead the launcher writes the detected URL to this shared
        // file, and both processes read/write through it so they always agree on the current BaseUrl.
        private static readonly string SharedUrlFilePath = Path.Combine(Path.GetTempPath(), "CampusQ_BaseUrl.txt");

        private static string _fallbackBaseUrl = "http://localhost:5131";
        private static DateTime _lastFileCheck = DateTime.MinValue;
        private static string _cachedFileValue = "";
        private static readonly object _fileLock = new object();

        public static string LocalApiBaseUrl =>
            Environment.GetEnvironmentVariable("CAMPUSQ_API_BASE_URL")?.Trim() switch
            {
                { Length: > 0 } configuredUrl when Uri.TryCreate(configuredUrl, UriKind.Absolute, out _) => configuredUrl.TrimEnd('/'),
                _ => "http://localhost:5131"
            };

        /// <summary>
        /// Base URL where CampusQ.Web is publicly reachable (via Cloudflare Tunnel).
        /// This single URL works for students on campus WiFi AND on mobile/cellular data,
        /// since the Cloudflare Tunnel is accessible from anywhere on the internet.
        /// NOTE: Quick Tunnels (trycloudflare.com) are temporary and change each time cloudflared restarts.
        /// Backed by a shared temp file so the value set by the launcher process (once cloudflared prints
        /// the tunnel URL) is visible to the separate CampusQ.Web process that renders the QR code.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                // Always re-check the file every access (no in-memory cache) to catch tunnel URL changes
                const int maxRetries = 3;
                const int retryDelayMs = 50;

                lock (_fileLock)
                {
                    for (int attempt = 0; attempt < maxRetries; attempt++)
                    {
                        try
                        {
                            if (File.Exists(SharedUrlFilePath))
                            {
                                string value = File.ReadAllText(SharedUrlFilePath).Trim();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    // Track the file value so Set() knows what was persisted
                                    _cachedFileValue = value;
                                    return value;
                                }
                            }
                            break; // File doesn't exist or is empty, don't retry
                        }
                        catch (IOException) when (attempt < maxRetries - 1)
                        {
                            // File might be locked by another process, retry
                            Thread.Sleep(retryDelayMs);
                        }
                        catch
                        {
                            // Other errors: log and continue
                            break;
                        }
                    }
                }

                return _fallbackBaseUrl;
            }
            set
            {
                _fallbackBaseUrl = value;
                _cachedFileValue = value;

                lock (_fileLock)
                {
                    try
                    {
                        // Ensure directory exists before writing
                        string? directory = Path.GetDirectoryName(SharedUrlFilePath);
                        if (directory != null && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.WriteAllText(SharedUrlFilePath, value);
                        Console.WriteLine($"✓ BaseUrl persisted to {SharedUrlFilePath}: {value}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.WriteLine($"✗ Permission denied writing BaseUrl to {SharedUrlFilePath}: {ex.Message}");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"✗ I/O error writing BaseUrl to {SharedUrlFilePath}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error writing BaseUrl to {SharedUrlFilePath}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Clear any cached URL values to force a fresh read from the file.
        /// Useful if you suspect stale values are being served.
        /// </summary>
        public static void ResetCache()
        {
            _cachedFileValue = "";
            _lastFileCheck = DateTime.MinValue;
        }
    }
}
