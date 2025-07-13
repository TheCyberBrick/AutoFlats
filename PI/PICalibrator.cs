
using System.Text;

namespace AutoFlats.PI
{
    public class PICalibrator : PICli, Calibrator
    {
        public bool CanWriteHeader => true;

        private static readonly string CALIBRATE_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Calibrate.js";
        private static readonly string CONVERT_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Convert.js";

        public bool CosmeticCorrection
        {
            get; set;
        } = true;

        public string OutputExtension
        {
            get; set;
        } = ".fit";

        public string OutputFormat
        {
            get; set;
        } = "f32";

        public new bool StopPixInsight
        {
            get; set;
        } = true;

        public bool UseXISF
        {
            get; set;
        } = true;

        public bool IgnoreMissingXISF
        {
            get; set;
        } = true;

        public PICalibrator(string executablePath, int slot) : base(executablePath, slot)
        {
        }

        private record class LightsSequence(
            string WorkingDir,
            List<string> Lights,
            string Dark,
            string Flat);

        public List<string> Calibrate(AutoFlats.FlatsSet set, IReadOnlyList<string> lights, Func<string, string> darkMap, string flat, Func<string, Dictionary<string, (string, string?)>> additionalTagsMap)
        {
            // Group into sequences
            var sequences = new Dictionary<(string ParentDir, string Dark), LightsSequence>();
            foreach (var light in lights)
            {
                var parentDir = Path.GetDirectoryName(light) ?? throw new Exception($"Couldn't find light {light} parent directory");
                var dark = darkMap(light);

                var seqKey = (parentDir, dark);

                if (!sequences.TryGetValue(seqKey, out var sequence))
                {
                    var workingDir = Path.Combine(parentDir, "tmp");
                    sequences.Add(seqKey, sequence = new LightsSequence(workingDir, new(), dark, flat));
                }

                sequence.Lights.Add(light);
            }

            // Set up working directories
            foreach (var sequence in sequences.Values)
            {
                Directory.CreateDirectory(sequence.WorkingDir);
            }

            try
            {
                StartPixInsight();

                foreach (var sequence in sequences.Values)
                {
                    var seqFlat = sequence.Flat;
                    if (UseXISF)
                    {
                        var newSeqFlat = Path.ChangeExtension(seqFlat, ".xisf");
                        if (File.Exists(newSeqFlat))
                        {
                            seqFlat = newSeqFlat;
                        }
                        else if (!IgnoreMissingXISF)
                        {
                            throw new Exception($"Couldn't find XISF flat {newSeqFlat}");
                        }
                    }

                    var seqDark = sequence.Dark;
                    if (UseXISF)
                    {
                        var newSeqDark = Path.ChangeExtension(seqDark, ".xisf");
                        if (File.Exists(newSeqDark))
                        {
                            seqDark = newSeqDark;
                        }
                        else if (!IgnoreMissingXISF)
                        {
                            throw new Exception($"Couldn't find XISF dark {newSeqDark}");
                        }
                    }

                    CheckAndThrowError(RunPixInsightScript(CALIBRATE_SCRIPT_RESOURCE, new List<object?>()
                    {
                        sequence.WorkingDir, OutputFormat, CosmeticCorrection, seqDark, seqFlat
                    }.Concat(sequence.Lights)));

                    var args = new List<string>()
                    {
                        OutputExtension
                    };
                    foreach (var light in sequence.Lights)
                    {
                        args.Add(Path.Combine(sequence.WorkingDir, Path.GetFileNameWithoutExtension(light) + ".xisf"));

                        var additionalTags = additionalTagsMap(light);

                        var tagsString = new StringBuilder();

                        foreach (var (key, (value, comment)) in additionalTags)
                        {
                            tagsString.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(key))).Append('\n');
                            tagsString.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value))).Append('\n');
                            tagsString.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(comment ?? ""))).Append('\n');
                        }

                        args.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(tagsString.ToString())));
                    }
                    CheckAndThrowError(RunPixInsightScript(CONVERT_SCRIPT_RESOURCE, args));
                }

                var calibratedLights = new List<string>();

                foreach (var sequence in sequences.Values)
                {
                    foreach (var light in sequence.Lights)
                    {
                        var parentDir = Path.GetDirectoryName(light) ?? throw new Exception($"Couldn't find light {light} parent directory");

                        var from = Path.Combine(sequence.WorkingDir, Path.GetFileNameWithoutExtension(light) + ".fit");
                        var to = Path.Combine(parentDir, $"calibrated_{Path.GetFileName(light)}");

                        File.Move(from, to, false);

                        calibratedLights.Add(to);
                    }
                }

                return calibratedLights;
            }
            finally
            {
                if (StopPixInsight)
                {
                    try
                    {
                        StopPixInsight();
                    }
                    catch (Exception)
                    {
                    }
                }

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
