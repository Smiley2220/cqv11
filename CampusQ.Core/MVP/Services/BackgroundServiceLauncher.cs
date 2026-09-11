using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using CampusQ.MVP.Data;

namespace CampusQ.MVP.Services
{
    /// <summary>
    /// Automatically starts CampusQ.Web (the Razor Pages site) and a Cloudflare Quick Tunnel
    /// when the desktop app launches, so the operator never has to start the web app manually.
    /// Once cloudflared prints the generated *.trycloudflare.com URL, <see cref="WebAppConfig.BaseUrl"/>
    /// is updated automatically.
    /// </summary>
    public static class BackgroundServiceLauncher
    {
        private const string LocalUrl = "http://localhost:5131";
        private static readonly Regex TunnelUrlRegex = new(@"https://[a-zA-Z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled);

        private static Process? _webProcess;
        private static Process? _tunnelProcess;
        private static bool _started;
        private static readonly object _lock = new();

        /// <summary>
        /// Starts CampusQ.Web and the Cloudflare tunnel in the background. Safe to call multiple times;
        /// only the first call has any effect. Any failure (missing dotnet/cloudflared, etc.) is swallowed
        /// so the desktop app keeps working with whatever BaseUrl was previously configured.
        /// </summary>
        public static void Start()
        {
            lock (_lock)
            {
                if (_started)
                {
                    return;
                }
                _started = true;
            }

            try
            {
                if (StartWebApp())
                {
                    StartTunnel();
                }
                else
                {
                    Log("CampusQ.Web never became reachable on " + LocalUrl + "; skipping tunnel start to avoid a broken public URL. Check " + WebLogPath + " for details.");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to auto-start web app/tunnel: {ex}");
            }
        }

        /// <summary>
        /// Stops the child processes started by <see cref="Start"/>. Should be called when the desktop app exits.
        /// </summary>
        public static void StopAll()
        {
            TryKill(ref _tunnelProcess);
            TryKill(ref _webProcess);
        }

        private static readonly string WebLogPath = Path.Combine(Path.GetTempPath(), "CampusQ_WebApp.log");
        private static readonly string TunnelLogPath = Path.Combine(Path.GetTempPath(), "CampusQ_Tunnel.log");

        private static bool StartWebApp()
        {
            string? webProjectDir = FindWebProjectRoot();
            if (webProjectDir == null)
            {
                Log("Could not locate the CampusQ.Web project directory; skipping auto-start of the web app.");
                return false;
            }

            if (!EnsureWebAppBuilt(webProjectDir))
            {
                Log($"Failed to build CampusQ.Web; skipping auto-start. See {BuildLogPath} for details.");
                return false;
            }

            string? webDll = FindCampusQWebDll(webProjectDir);
            if (webDll == null)
            {
                Log("Could not locate CampusQ.Web.dll after build; skipping auto-start of the web app.");
                return false;
            }

            // Kill any leftover CampusQ.Web process still bound to our port from a previous run
            // (e.g. a stale instance started without --contentRoot) so the new one always wins.
            KillProcessesOnPort(5131);

            // The bin output folder does not contain a physical copy of wwwroot (static web assets are
            // normally resolved from the project source via a manifest). Point the content root at the
            // actual CampusQ.Web project directory so UseStaticFiles() can find the real wwwroot (CSS/JS/images).
            var argsBuilder = $"\"{webDll}\" --contentRoot \"{webProjectDir}\"";

            var psi = new ProcessStartInfo("dotnet", argsBuilder)
            {
                WorkingDirectory = webProjectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = LocalUrl;

            try
            {
                File.WriteAllText(WebLogPath, string.Empty);
            }
            catch
            {
                // Best-effort logging; ignore if we can't create the file.
            }

            _webProcess = Process.Start(psi);
            if (_webProcess == null)
            {
                Log("Failed to start the CampusQ.Web process (Process.Start returned null).");
                return false;
            }

            _webProcess.OutputDataReceived += (_, e) => AppendLog(WebLogPath, e.Data);
            _webProcess.ErrorDataReceived += (_, e) => AppendLog(WebLogPath, e.Data);
            _webProcess.BeginOutputReadLine();
            _webProcess.BeginErrorReadLine();

            // Poll until Kestrel is actually accepting connections on the port, instead of a fixed delay.
            // This avoids handing out a public tunnel URL before the origin is ready (which Cloudflare
            // reports to visitors as a "host error").
            const int timeoutMs = 30000;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (_webProcess.HasExited)
                {
                    Log($"CampusQ.Web exited prematurely (exit code {_webProcess.ExitCode}). See {WebLogPath} for details.");
                    return false;
                }

                if (IsPortOpen("localhost", 5131))
                {
                    Log("CampusQ.Web is up and listening on " + LocalUrl);
                    return true;
                }

                Thread.Sleep(500);
            }

            Log($"Timed out after {timeoutMs / 1000}s waiting for CampusQ.Web to listen on {LocalUrl}. See {WebLogPath} for details.");
            return false;
        }

        private static bool IsPortOpen(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                bool connected = result.AsyncWaitHandle.WaitOne(500);
                if (connected && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void StartTunnel()
        {
            ProcessStartInfo psi;
            try
            {
                File.WriteAllText(TunnelLogPath, string.Empty);
            }
            catch
            {
                // Best-effort logging; ignore if we can't create the file.
            }

            try
            {
                psi = new ProcessStartInfo("cloudflared", $"tunnel --url {LocalUrl}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                _tunnelProcess = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"❌ cloudflared not available: {ex.Message}. Install from: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/");
                return;
            }

            if (_tunnelProcess == null)
            {
                Log("❌ Failed to start tunnel process (Process.Start returned null).");
                return;
            }

            _tunnelProcess.OutputDataReceived += (_, e) => { AppendLog(TunnelLogPath, e.Data); OnTunnelOutput(e.Data); };
            _tunnelProcess.ErrorDataReceived += (_, e) => { AppendLog(TunnelLogPath, e.Data); OnTunnelOutput(e.Data); };
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            // ✅ NEW: Wait for tunnel URL to be detected (up to 15 seconds)
            // This ensures the tunnel is actually running before we return to the caller.
            const int tunnelTimeoutMs = 15000;  // 15 seconds
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < tunnelTimeoutMs)
            {
                if (_tunnelProcess.HasExited)
                {
                    Log($"❌ Cloudflared exited prematurely (exit code {_tunnelProcess.ExitCode}). Check {TunnelLogPath} for details.");
                    return;
                }

                // Check if URL was detected by OnTunnelOutput (reading from file to avoid cross-process static issues)
                try
                {
                    if (File.Exists(Path.Combine(Path.GetTempPath(), "CampusQ_BaseUrl.txt")))
                    {
                        string content = File.ReadAllText(Path.Combine(Path.GetTempPath(), "CampusQ_BaseUrl.txt")).Trim();
                        if (!string.IsNullOrEmpty(content) && content.Contains("trycloudflare"))
                        {
                            Log($"✓ Tunnel online at {content}");
                            return;  // Success!
                        }
                    }
                }
                catch
                {
                    // Best-effort check; ignore file read errors
                }

                Thread.Sleep(200);  // Check every 200ms
            }

            Log($"⚠ Tunnel startup did not complete within {tunnelTimeoutMs / 1000}s. Check {TunnelLogPath} for errors. Continuing with localhost fallback.");
        }

        private static void OnTunnelOutput(string? line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            var match = TunnelUrlRegex.Match(line);
            if (match.Success)
            {
                string detectedUrl = match.Value;
                WebAppConfig.BaseUrl = detectedUrl;
                Log($"✓ Detected Cloudflare tunnel URL: {detectedUrl}");

                // Verify the URL was actually persisted to the file
                try
                {
                    string filePath = Path.Combine(Path.GetTempPath(), "CampusQ_BaseUrl.txt");
                    if (File.Exists(filePath))
                    {
                        string persistedUrl = File.ReadAllText(filePath).Trim();
                        if (persistedUrl == detectedUrl)
                        {
                            Log($"✓ Tunnel URL successfully persisted to shared file");
                        }
                        else
                        {
                            Log($"⚠ WARNING: Persisted URL ({persistedUrl}) differs from detected URL ({detectedUrl})");
                        }
                    }
                    else
                    {
                        Log($"⚠ WARNING: Shared URL file not found at {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠ Error verifying URL persistence: {ex.Message}");
                }
            }
        }

        private static string? FindWebProjectDir(string webDllPath)
        {
            string? dir = Path.GetDirectoryName(webDllPath);
            while (dir != null && !string.Equals(Path.GetFileName(dir), "CampusQ.Web", StringComparison.OrdinalIgnoreCase))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir;
        }

        private static string? FindWebProjectRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && Directory.GetFiles(dir, "*.sln").Length == 0)
            {
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }

            if (dir == null)
            {
                return null;
            }

            string webProjectDir = Path.Combine(dir, "CampusQ.Web");
            return Directory.Exists(webProjectDir) ? webProjectDir : null;
        }

        private static readonly string BuildLogPath = Path.Combine(Path.GetTempPath(), "CampusQ_WebApp_Build.log");

        /// <summary>
        /// Runs "dotnet build" against CampusQ.Web.csproj so the output directory always has a fresh,
        /// consistent set of files (dll + runtimeconfig.json + deps.json). Without this, a stale or
        /// partially-cleaned bin folder (e.g. missing runtimeconfig.json) causes "dotnet" to fail with
        /// a hostpolicy.dll / apphost error when we try to launch the web app.
        /// </summary>
        private static bool EnsureWebAppBuilt(string webProjectDir)
        {
            string csproj = Path.Combine(webProjectDir, "CampusQ.Web.csproj");
            if (!File.Exists(csproj))
            {
                Log($"Could not find {csproj}.");
                return false;
            }

            try
            {
                File.WriteAllText(BuildLogPath, string.Empty);
            }
            catch
            {
                // Best-effort logging; ignore if we can't create the file.
            }

            var psi = new ProcessStartInfo("dotnet", $"build \"{csproj}\" -c Debug")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                Process? build = Process.Start(psi);
                if (build == null)
                {
                    Log("Failed to start \"dotnet build\" for CampusQ.Web.");
                    return false;
                }

                using (build)
                {
                    build.OutputDataReceived += (_, e) => AppendLog(BuildLogPath, e.Data);
                    build.ErrorDataReceived += (_, e) => AppendLog(BuildLogPath, e.Data);
                    build.BeginOutputReadLine();
                    build.BeginErrorReadLine();

                    if (!build.WaitForExit(120000))
                    {
                        Log("Timed out waiting for \"dotnet build\" of CampusQ.Web to finish.");
                        TryKill(ref build);
                        return false;
                    }

                    if (build.ExitCode != 0)
                    {
                        Log($"\"dotnet build\" of CampusQ.Web exited with code {build.ExitCode}. See {BuildLogPath} for details.");
                        return false;
                    }
                }

                Log("CampusQ.Web build succeeded.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to build CampusQ.Web: {ex.Message}");
                return false;
            }
        }

        private static string? FindCampusQWebDll(string webProjectDir)
        {
            string binDir = Path.Combine(webProjectDir, "bin");
            if (!Directory.Exists(binDir))
            {
                return null;
            }

            // Only consider a dll if its companion runtimeconfig.json is also present, otherwise
            // "dotnet <dll>" fails with a hostpolicy.dll / apphost error.
            return Directory.GetFiles(binDir, "CampusQ.Web.dll", SearchOption.AllDirectories)
                .Where(dll => File.Exists(Path.ChangeExtension(dll, null) + ".runtimeconfig.json"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void KillProcessesOnPort(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c netstat -ano -p tcp | findstr :{port}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };

                using var netstat = Process.Start(psi);
                if (netstat == null)
                {
                    return;
                }

                string output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit(2000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5 || !parts[1].EndsWith($":{port}", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (int.TryParse(parts[^1], out int pid) && pid != Environment.ProcessId)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById(pid);
                            proc.Kill(entireProcessTree: true);
                            Log($"Killed stale process {pid} listening on port {port}.");
                        }
                        catch
                        {
                            // Process may already be gone; ignore.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Could not check/clean up port {port}: {ex.Message}");
            }
        }

        private static void Log(string message)
        {
            Debug.WriteLine($"[BackgroundServiceLauncher] {message}");
        }

        private static void AppendLog(string path, string? line)
        {
            if (line == null)
            {
                return;
            }

            try
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch
            {
                // Best-effort logging; ignore write failures.
            }
        }

        private static void TryKill(ref Process? process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackgroundServiceLauncher] Failed to stop process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
                process = null;
            }
        }
    }
}
