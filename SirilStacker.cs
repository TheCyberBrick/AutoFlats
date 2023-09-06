using System.Diagnostics;
using System.Globalization;

namespace AutoFlats
{
    public class SirilStacker : Stacker
    {
        private const string FILE_EXT = "fit";

        private readonly string sirilPath;

        public SirilStacker(string sirilPath)
        {
            if (!File.Exists(sirilPath))
            {
                throw new Exception($"Siril executable {sirilPath} does not exist");
            }

            try
            {
                var info = FileVersionInfo.GetVersionInfo(sirilPath);

                if (info.ProductName?.ToLower() != "siril")
                {
                    throw new Exception("Failed verifying siril executable: product name is not 'siril'");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed retrieving file info of {sirilPath}: {ex.Message}");
            }

            this.sirilPath = sirilPath;
        }

        private void WriteScript(string scriptPath, string workingDir, IEnumerable<Sequence> sequences)
        {
            try
            {
                using (var scriptWriter = new StreamWriter(scriptPath))
                {
                    scriptWriter.WriteLine($"requires 1.2.0");

                    foreach (var sequence in sequences)
                    {
                        scriptWriter.WriteLine($"setext {FILE_EXT}");
                        scriptWriter.WriteLine($"cd \"{sequence.Dir}\"");
                        scriptWriter.WriteLine($"link {sequence.Name} -out=..");
                        scriptWriter.WriteLine($"cd \"{workingDir}\"");
                        scriptWriter.WriteLine($"calibrate {sequence.Name} -dark={sequence.DarkName} -cc=dark -prefix=pp_");
                        scriptWriter.WriteLine($"stack pp_{sequence.Name} mean winsorized 3 3 -norm=mul -out={sequence.Name}_master");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed writing siril script to {scriptPath}: {ex.Message}");
            }
        }

        private void RunScript(string scriptPath, string workingDir)
        {
            var process = new Process();
            process.StartInfo.FileName = sirilPath;
            process.StartInfo.Arguments = $"--script \"{scriptPath}\"";
            process.StartInfo.WorkingDirectory = workingDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Siril script execution failed:\n{stdout}\n{stderr}");
            }
        }

        private record class Sequence(string Name, string Dir, List<string> Flats, string? Dark, string? DarkName);

        public void Stack(AutoFlats.FlatsSet set, IEnumerable<string> flats, Func<string, string>? flatDarkMap)
        {
            var flatsDir = Path.GetDirectoryName(flats.First())!;

            // Create a working directory for temporary files
            var workingDir = Path.Combine(flatsDir, "tmp");
            if (Directory.Exists(workingDir) && Directory.EnumerateFiles(workingDir).Any())
            {
                throw new Exception($"Working directory {workingDir} already exists and is not empty");
            }
            else
            {
                Directory.CreateDirectory(workingDir);
            }

            try
            {
                // Group flats and darks into sequences
                var sequences = new Dictionary<string, Sequence>();
                var seqNr = 0;
                foreach (var flat in flats)
                {
                    var dark = flatDarkMap != null ? flatDarkMap(flat) : null;
                    var seqKey = dark ?? "default";

                    if (!sequences.TryGetValue(seqKey, out var sequence))
                    {
                        var name = "flats" + seqNr;
                        sequences.Add(seqKey, sequence = new Sequence(name, Path.Combine(workingDir, name), new(), dark, $"dark{seqNr}"));
                    }

                    sequence.Flats.Add(flat);
                }

                // Copy flats and corresponding darks
                foreach (var sequence in sequences.Values)
                {
                    Directory.CreateDirectory(sequence.Dir);

                    foreach (var flat in flats)
                    {
                        File.Copy(flat, Path.Combine(sequence.Dir, Path.GetFileName(flat) + $".{FILE_EXT}"));
                    }

                    var dark = sequence.Dark;
                    if (dark != null && sequence.DarkName != null)
                    {
                        var darkFile = Path.Combine(workingDir, $"{sequence.DarkName}.{FILE_EXT}");
                        if (!File.Exists(darkFile))
                        {
                            File.Copy(dark, darkFile);
                        }
                    }
                }

                var scriptPath = Path.GetTempFileName();

                WriteScript(scriptPath, workingDir, sequences.Values);

                try
                {
                    RunScript(scriptPath, workingDir);
                }
                finally
                {
                    File.Delete(scriptPath);
                }

                // Copy master flats to flat dir with
                // a useful name
                foreach (var sequence in sequences.Values)
                {
                    var seqMasterFlatName = $"{sequence.Name}_master.{FILE_EXT}";
                    var outMasterFlatName = $"MasterFlat [{set.Filter}][{set.Binning.X}x{set.Binning.Y}][{string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.Rotation)}°].{FILE_EXT}";

                    if (File.Exists(outMasterFlatName))
                    {
                        File.Delete(outMasterFlatName);
                    }

                    File.Copy(Path.Combine(workingDir, seqMasterFlatName), Path.Combine(flatsDir, outMasterFlatName));
                }
            }
            finally
            {
                Directory.Delete(workingDir, true);
            }
        }
    }
}
