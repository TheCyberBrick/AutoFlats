
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AutoFlats.PI
{
    public abstract class PICli
    {
        private static readonly string PREAMBLE_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Preamble.js";
        private static readonly string START_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Start.js";

        private static readonly string SCRIPTS_DIR = Path.Combine("AutoFlats", "PI", "Scripts");
        private static readonly string OUTPUTS_DIR = Path.Combine("AutoFlats", "PI", "Outputs");

        private static readonly string STARTED_STATUS = "started";
        private static readonly string FINISHED_STATUS = "finished";

        private static readonly string INPUT_EXT = ".in";
        private static readonly string TIMEOUT_EXT = ".timeout";
        private static readonly string OUTPUT_EXT = ".out";
        private static readonly string STATUS_EXT = ".status";

        private static readonly string ERROR_PREFIX = "ERROR: ";

        public string ExecutablePath
        {
            get; init;
        }

        public int Slot
        {
            get; init;
        }

        public TimeSpan StartTimeout
        {
            get; set;
        } = TimeSpan.FromSeconds(30);

        public TimeSpan StopTimeout
        {
            get; set;
        } = TimeSpan.FromSeconds(30);

        public TimeSpan ScriptTimeout
        {
            get; set;
        } = TimeSpan.FromSeconds(600);

        private string? scriptPreamble = null;

        private readonly Dictionary<string, string> scriptPaths = new();

        protected PICli(string executablePath, int slot)
        {
            if (slot < 1)
            {
                throw new ArgumentException("slot < 1");
            }

            ExecutablePath = executablePath;
            Slot = slot;
        }

        private string LoadPreamble()
        {
            if (scriptPreamble == null)
            {
                using var preambleStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PREAMBLE_SCRIPT_RESOURCE);
                if (preambleStream == null)
                {
                    throw new Exception($"Script preamble resource '{PREAMBLE_SCRIPT_RESOURCE}' not found");
                }

                using var reader = new StreamReader(preambleStream, Encoding.UTF8);

                scriptPreamble = reader.ReadToEnd();
            }

            return scriptPreamble;
        }

        protected void CreatePixInsightScripts(params string[] resources)
        {
            var scriptsDir = Path.Combine(Path.GetTempPath(), SCRIPTS_DIR);

            Directory.CreateDirectory(scriptsDir);

            var assembly = Assembly.GetExecutingAssembly();

            var scriptPaths = new Dictionary<string, string>();

            foreach (var resource in resources)
            {
                string script;
                using (var resourceStream = assembly.GetManifestResourceStream(resource))
                {
                    if (resourceStream == null)
                    {
                        throw new Exception($"Script resource '{resource}' not found");
                    }

                    using var reader = new StreamReader(resourceStream, Encoding.UTF8);

                    script = LoadPreamble() + Environment.NewLine + reader.ReadToEnd();
                }

                var scriptPath = Path.Combine(scriptsDir, resource);

                File.WriteAllText(scriptPath, script, Encoding.UTF8);

                scriptPaths[resource] = scriptPath;
            }

            foreach (var k in scriptPaths.Keys)
            {
                this.scriptPaths[k] = scriptPaths[k];
            }
        }

        protected bool IsPixInsightRunning()
        {
            var title = Slot > 1 ? $"PixInsight ({Slot})" : "PixInsight";
            return Process.GetProcessesByName("PixInsight").Select(x => x.MainWindowTitle == title).Any();
        }

        protected void StartPixInsight()
        {
            if (!IsPixInsightRunning())
            {
                RunPixInsightScriptImpl(true, START_SCRIPT_RESOURCE, Array.Empty<string>());

                if (!IsPixInsightRunning())
                {
                    throw new Exception("Failed starting PixInsight");
                }
            }
        }

        protected void StopPixInsight()
        {
            if (IsPixInsightRunning())
            {
                var endTime = DateTime.Now + StopTimeout;

                var processStartInfo = new ProcessStartInfo(ExecutablePath, $"--automation-mode --terminate={Slot}")
                {
                    UseShellExecute = true
                };

                Process? stopProcess = null;
                void stopProcessThread()
                {
                    stopProcess = Process.Start(processStartInfo);
                }

                var stopThread = new Thread(stopProcessThread);
                stopThread.Start();

                try
                {
                    while (IsPixInsightRunning())
                    {
                        if (DateTime.Now > endTime)
                        {
                            throw new Exception("Failed stopping PixInsight");
                        }

                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    try
                    {
                        stopProcess?.Kill();
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        stopThread.Join();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        protected string RunPixInsightScript(string resource, params object?[] args)
        {
            return RunPixInsightScriptImpl(false, resource, args);
        }

        protected string RunPixInsightScript(string resource, IEnumerable<object?> args)
        {
            return RunPixInsightScriptImpl(false, resource, args);
        }

        private string RunPixInsightScriptImpl(bool isStartupScript, string resource, IEnumerable<object?> args)
        {
            if (!scriptPaths.ContainsKey(resource))
            {
                CreatePixInsightScripts(resource);
            }

            if (!scriptPaths.TryGetValue(resource, out var scriptPath))
            {
                throw new Exception($"Script resource '{resource}' is unknown");
            }

            scriptPath = Path.GetFullPath(scriptPath);

            if (!isStartupScript && !IsPixInsightRunning())
            {
                throw new Exception("PixInsight is not running");
            }

            var execArgs = new List<string>()
            {
                "--automation-mode"
            };

            List<string> scriptArgs;

            if (isStartupScript)
            {
                execArgs.Add($"--new={Slot}");
                execArgs.Add("--no-startup-scripts");

                scriptArgs = new List<string>() { $"--run=\"{scriptPath}\"" };
            }
            else
            {
                scriptArgs = new List<string>() { $"--execute={Slot}:\"{scriptPath}\"" };
            }

            var outputsDir = Path.Combine(Path.GetTempPath(), OUTPUTS_DIR);
            Directory.CreateDirectory(outputsDir);

            var guid = Guid.NewGuid().ToString("D");

            var inputPath = Path.GetFullPath(Path.Combine(outputsDir, guid + INPUT_EXT));
            var timeoutPath = Path.GetFullPath(Path.Combine(outputsDir, guid + TIMEOUT_EXT));
            var resultPath = Path.GetFullPath(Path.Combine(outputsDir, guid + OUTPUT_EXT));
            var statusPath = Path.GetFullPath(Path.Combine(outputsDir, guid + STATUS_EXT));

            Process? startProcess = null;
            Thread? startThread = null;

            try
            {
                var startEndTime = DateTime.Now + StartTimeout;
                var executionEndTime = DateTime.Now + ScriptTimeout;

                if (File.Exists(resultPath))
                {
                    throw new Exception($"Script output file '{resultPath}' already exists");
                }

                if (File.Exists(statusPath))
                {
                    throw new Exception($"Script output file '{statusPath}' already exists");
                }

                File.WriteAllText(inputPath, string.Join(Environment.NewLine, args.Select(arg => Convert.ToBase64String(Encoding.UTF8.GetBytes(arg?.ToString() ?? "")))));

                File.WriteAllText(timeoutPath, ((DateTimeOffset)executionEndTime).ToUnixTimeSeconds().ToString(), Encoding.UTF8);

                scriptArgs.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(inputPath)));
                scriptArgs.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(resultPath)));
                scriptArgs.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(statusPath)));

                execArgs.Add(string.Join(",", scriptArgs));

                var processStartInfo = new ProcessStartInfo(ExecutablePath, string.Join(" ", execArgs))
                {
                    UseShellExecute = true
                };

                void startProcessThread()
                {
                    startProcess = Process.Start(processStartInfo);
                }

                startThread = new Thread(startProcessThread);
                startThread.Start();

                while (!File.Exists(statusPath))
                {
                    if (DateTime.Now > startEndTime)
                    {
                        throw new Exception("Script execution timeout");
                    }

                    Thread.Sleep(500);
                }

                var isPixInsightStopped = false;

                var isFinished = false;

                while (true)
                {
                    if (DateTime.Now > startEndTime)
                    {
                        throw new Exception("Script execution timeout");
                    }

                    try
                    {
                        var state = ReadFileWithoutLock(statusPath).Trim();
                        if (state == STARTED_STATUS)
                        {
                            break;
                        }
                        else if (state == FINISHED_STATUS)
                        {
                            isFinished = true;
                            break;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        throw new Exception("Script execution timeout");
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }

                    if (isPixInsightStopped)
                    {
                        throw new Exception("PixInsight is not running");
                    }

                    if (!IsPixInsightRunning())
                    {
                        isPixInsightStopped = true;
                    }

                    Thread.Sleep(500);
                }

                if (!isFinished)
                {
                    while (true)
                    {
                        if (DateTime.Now > executionEndTime)
                        {
                            throw new Exception("Script execution timeout");
                        }

                        try
                        {
                            var state = ReadFileWithoutLock(statusPath).Trim();
                            if (state == FINISHED_STATUS)
                            {
                                break;
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            throw new Exception("Script execution timeout");
                        }
                        catch (Exception)
                        {
                            // Ignore
                        }

                        if (isPixInsightStopped)
                        {
                            throw new Exception("PixInsight is not running");
                        }

                        if (!IsPixInsightRunning())
                        {
                            isPixInsightStopped = true;
                        }

                        Thread.Sleep(500);
                    }
                }

                try
                {
                    return File.ReadAllText(resultPath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed reading script output file '{resultPath}'", ex);
                }
            }
            finally
            {
                if (!isStartupScript)
                {
                    try
                    {
                        startProcess?.Kill();
                    }
                    catch (Exception)
                    {
                    }
                }

                try
                {
                    startThread?.Join();
                }
                catch (Exception)
                {
                }

                try
                {
                    File.Delete(inputPath);
                }
                catch (Exception)
                {
                }

                try
                {
                    File.Delete(timeoutPath);
                }
                catch (Exception)
                {
                }

                try
                {
                    File.Delete(resultPath);
                }
                catch (Exception)
                {
                }

                try
                {
                    File.Delete(statusPath);
                }
                catch (Exception)
                {
                }

                CleanupOutputFiles();
            }
        }

        protected void CheckAndThrowError(string result)
        {
            if (result.StartsWith(ERROR_PREFIX))
            {
                throw new Exception($"Script failed due to an error: {result.Substring(ERROR_PREFIX.Length)}");
            }
        }

        private void CleanupOutputFiles()
        {
            var outputsDir = Path.Combine(Path.GetTempPath(), OUTPUTS_DIR);

            if (Directory.Exists(outputsDir))
            {
                foreach (var timeoutPath in Directory.EnumerateFiles(outputsDir, "*" + TIMEOUT_EXT))
                {
                    bool isTimedOut = true;
                    try
                    {
                        var time = DateTimeOffset.FromUnixTimeSeconds(long.Parse(ReadFileWithoutLock(timeoutPath).Trim()));

                        if (DateTimeOffset.UtcNow <= time + TimeSpan.FromSeconds(1))
                        {
                            isTimedOut = false;
                        }
                    }
                    catch (Exception)
                    {
                        // OK
                    }

                    if (isTimedOut)
                    {
                        try
                        {
                            File.Delete(timeoutPath);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                foreach (var file in Directory.EnumerateFiles(outputsDir))
                {
                    if (!file.EndsWith(TIMEOUT_EXT))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);

                        if (!File.Exists(Path.Combine(outputsDir, name + TIMEOUT_EXT)))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }
        }

        private static string ReadFileWithoutLock(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
