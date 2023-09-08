namespace AutoFlats
{
    public class SirilCalibrator : SirilCli, Calibrator
    {
        private const string PREPROCESSED_SEQUENCE_PREFIX = "pp_";

        public string CalibrationParameters { get; set; } = "-cc=dark";

        public SirilCalibrator(string sirilPath) : base(sirilPath) { }

        private record class LightsSequence(
            string Name,
            string WorkingDir,
            string LightsWorkingDir,
            List<string> Lights,
            string Dark,
            string Flat,
            string DarkName,
            string FlatName);

        private void WriteScript(TextWriter writer, string calibrationParameters, IEnumerable<LightsSequence> sequences)
        {
            writer.WriteLine($"requires 1.2.0");
            writer.WriteLine($"setext {FILE_EXT}");

            // Calibrate each sequence
            foreach (var sequence in sequences)
            {
                writer.WriteLine();
                writer.WriteLine($"cd \"{sequence.LightsWorkingDir}\"");
                writer.WriteLine($"link {sequence.Name} -out=..");
                writer.WriteLine($"cd \"..\"");
                writer.WriteLine($"calibrate {sequence.Name} -dark={sequence.DarkName} -flat={sequence.FlatName} {calibrationParameters} -prefix={PREPROCESSED_SEQUENCE_PREFIX}");
            }
        }

        public List<string> Calibrate(AutoFlats.FlatsSet set, IReadOnlyList<string> lights, Func<string, string> darkMap, string flat)
        {
            // Group into sequences
            var sequences = new Dictionary<(string ParentDir, string Dark), LightsSequence>();
            var seqNr = 0;
            foreach (var light in lights)
            {
                var parentDir = Path.GetDirectoryName(light) ?? throw new Exception($"Couldn't find light {light} parent directory");
                var dark = darkMap(light);

                var seqKey = (parentDir, dark);

                if (!sequences.TryGetValue(seqKey, out var sequence))
                {
                    var name = "lights" + seqNr;
                    var workingDir = Path.Combine(parentDir, "tmp");
                    sequences.Add(seqKey, sequence = new LightsSequence(name, workingDir, Path.Combine(workingDir, name), new(), dark, flat, $"dark{seqNr}", $"flat{seqNr}"));
                }

                sequence.Lights.Add(light);
            }

            // Set up working directories
            foreach (var sequence in sequences.Values)
            {
                Directory.CreateDirectory(sequence.WorkingDir);
                Directory.CreateDirectory(sequence.LightsWorkingDir);
            }

            try
            {
                // Copy lights and and corresponding flats and darks
                foreach (var sequence in sequences.Values)
                {
                    foreach (var light in sequence.Lights)
                    {
                        File.Copy(light, Path.Combine(sequence.LightsWorkingDir, Path.GetFileName(light) + $".{FILE_EXT}"));
                    }

                    var darkFile = Path.Combine(sequence.WorkingDir, $"{sequence.DarkName}.{FILE_EXT}");
                    if (!File.Exists(darkFile))
                    {
                        File.Copy(sequence.Dark, darkFile);
                    }

                    var flatFile = Path.Combine(sequence.WorkingDir, $"{sequence.FlatName}.{FILE_EXT}");
                    if (!File.Exists(flatFile))
                    {
                        File.Copy(flat, flatFile);
                    }
                }

                var scriptPath = Path.GetTempFileName();

                // Create siril script
                try
                {
                    using (var writer = new StreamWriter(scriptPath))
                    {
                        WriteScript(writer, CalibrationParameters, sequences.Values);
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

                var calibratedLights = new List<string>();

                // Copy calibrated lights to lights dir
                foreach (var sequence in sequences.Values)
                {
                    var map = MapToSequenceFiles(sequence.Lights, sequence.WorkingDir, $"{PREPROCESSED_SEQUENCE_PREFIX}{sequence.Name}");

                    foreach (var light in sequence.Lights)
                    {
                        var parentDir = Path.GetDirectoryName(light) ?? throw new Exception($"Couldn't find light {light} parent directory");

                        var calibratedLightName = $"calibrated_{Path.GetFileName(light)}";
                        var calibratedLightFile = Path.Combine(parentDir, calibratedLightName);
                        File.Copy(map[light], calibratedLightFile);

                        calibratedLights.Add(calibratedLightFile);
                    }
                }

                return calibratedLights;
            }
            finally
            {
                foreach (var sequence in sequences.Values)
                {
                    try
                    {
                        Directory.Delete(sequence.WorkingDir, true);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}
