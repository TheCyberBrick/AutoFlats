using System.Globalization;

namespace AutoFlats.Siril
{
    public class SirilStacker : SirilCli, Stacker
    {
        private const string MASTER_FLAT_NAME = "stack";
        private const string PREPROCESSED_SEQUENCE_PREFIX = "pp_";

        public string CalibrationParameters { get; set; } = "-cc=dark";

        public string StackingParameters { get; set; } = "mean winsorized 3 3 -norm=mul";

        public SirilStacker(string sirilPath) : base(sirilPath) { }

        private record class FlatsSequence(
            string Name,
            string FlatsWorkingDir,
            List<string> Flats,
            string? Dark,
            string? DarkName);

        private void WriteScript(TextWriter writer, string workingDir, string calibrationParameters, string stackingParameters, IEnumerable<FlatsSequence> sequences)
        {
            writer.WriteLine($"requires 1.2.0");
            writer.WriteLine($"setext {FILE_EXT}");

            // Calibrate each sequence
            foreach (var sequence in sequences)
            {
                writer.WriteLine();
                writer.WriteLine($"cd \"{sequence.FlatsWorkingDir}\"");
                writer.WriteLine($"link {sequence.Name} -out=..");
                writer.WriteLine($"cd \"{workingDir}\"");
                writer.WriteLine($"calibrate {sequence.Name} -dark={sequence.DarkName} {calibrationParameters} -prefix={PREPROCESSED_SEQUENCE_PREFIX}");
            }

            string mergedSequenceName;

            if (sequences.Count() > 1)
            {
                mergedSequenceName = "merged";

                // Merge calibrated sequences together
                writer.WriteLine();
                writer.WriteLine($"cd \"{workingDir}\"");
                writer.Write("merge ");
                foreach (var sequence in sequences)
                {
                    writer.Write($"{PREPROCESSED_SEQUENCE_PREFIX}{sequence.Name} ");
                }
                writer.WriteLine($"{mergedSequenceName}");
            }
            else
            {
                mergedSequenceName = $"{PREPROCESSED_SEQUENCE_PREFIX}{sequences.First().Name}";
            }

            // Stack merged sequence
            writer.WriteLine();
            writer.WriteLine($"cd \"{workingDir}\"");
            writer.WriteLine($"stack {mergedSequenceName} {stackingParameters} -out={MASTER_FLAT_NAME}");
        }

        public string Stack(AutoFlats.FlatsSet set, IReadOnlyList<string> flats, Func<string, string>? darkMap)
        {
            var flatsDir = Path.GetDirectoryName(flats.First()) ?? throw new Exception("Couldn't find flats parent directory");

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
                var sequences = new Dictionary<string, FlatsSequence>();
                var seqNr = 0;
                foreach (var flat in flats)
                {
                    var dark = darkMap != null ? darkMap(flat) : null;
                    var seqKey = dark ?? "default";

                    if (!sequences.TryGetValue(seqKey, out var sequence))
                    {
                        var name = "flats" + seqNr;
                        sequences.Add(seqKey, sequence = new FlatsSequence(name, Path.Combine(workingDir, name), new(), dark, $"dark{seqNr}"));
                    }

                    sequence.Flats.Add(flat);
                }

                // Copy flats and corresponding darks
                foreach (var sequence in sequences.Values)
                {
                    Directory.CreateDirectory(sequence.FlatsWorkingDir);

                    foreach (var flat in sequence.Flats)
                    {
                        File.Copy(flat, Path.Combine(sequence.FlatsWorkingDir, Path.GetFileName(flat) + $".{FILE_EXT}"));
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

                // Create siril script
                try
                {
                    using (var writer = new StreamWriter(scriptPath))
                    {
                        WriteScript(writer, workingDir, CalibrationParameters, StackingParameters, sequences.Values);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed writing siril script to {scriptPath}: {ex.Message}");
                }

                try
                {
                    RunScript(scriptPath);
                }
                finally
                {
                    File.Delete(scriptPath);
                }

                // Copy master flat to flat dir and
                // give it a useful name
                var masterFlatName = $"MasterFlat [{set.Filter}][{set.Binning.X}x{set.Binning.Y}][{string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.Rotation)}°][F{string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.FocusPosition)}].{FILE_EXT}";
                var masterFlatFile = Path.Combine(flatsDir, masterFlatName);
                File.Copy(Path.Combine(workingDir, $"{MASTER_FLAT_NAME}.{FILE_EXT}"), masterFlatFile, true);

                return masterFlatFile;
            }
            finally
            {
                try
                {
                    Directory.Delete(workingDir, true);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
