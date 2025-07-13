
using System.Globalization;

namespace AutoFlats.PI
{
    public class PIStacker : PICli, Stacker
    {
        private static readonly string CALIBRATE_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Calibrate.js";
        private static readonly string STACK_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Stack.js";
        private static readonly string CONVERT_SCRIPT_RESOURCE = "AutoFlats.PI.Scripts.Convert.js";

        public bool CosmeticCorrection
        {
            get; set;
        } = true;

        public new bool StopPixInsight
        {
            get; set;
        } = true;

        public bool SaveXISF
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

        public PIStacker(string executablePath, int slot) : base(executablePath, slot)
        {
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
                var groups = new Dictionary<string, List<string>>();

                foreach (var flat in flats)
                {
                    var dark = darkMap?.Invoke(flat) ?? string.Empty;

                    if (!groups.TryGetValue(dark, out var group))
                    {
                        groups[dark] = group = new List<string>();
                    }

                    group.Add(flat);
                }

                StartPixInsight();

                foreach (var (dark, files) in groups)
                {
                    var groupDark = dark;
                    if (UseXISF)
                    {
                        var newGroupDark = Path.ChangeExtension(groupDark, ".xisf");
                        if (File.Exists(newGroupDark))
                        {
                            groupDark = newGroupDark;
                        }
                        else if (!IgnoreMissingXISF)
                        {
                            throw new Exception($"Couldn't find XISF dark {newGroupDark}");
                        }
                    }

                    CheckAndThrowError(RunPixInsightScript(CALIBRATE_SCRIPT_RESOURCE, new List<object?>()
                    {
                        workingDir, "f32", CosmeticCorrection, groupDark, null
                    }.Concat(files)));
                }

                var masterFlatName = $"MasterFlat [{set.Filter}][{set.Binning.X}x{set.Binning.Y}][{string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.Rotation)}°][F{string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.FocusPosition)}]";
                var masterFlatFile = Path.Combine(flatsDir, masterFlatName);
                var masterFlatExt = SaveXISF ? ".xisf" : ".fit";

                CheckAndThrowError(RunPixInsightScript(STACK_SCRIPT_RESOURCE, new List<object?>()
                {
                    masterFlatFile + masterFlatExt
                }.Concat(flats.Select(light => Path.Combine(workingDir, Path.GetFileNameWithoutExtension(light) + ".xisf")))));

                if (SaveXISF)
                {
                    CheckAndThrowError(RunPixInsightScript(CONVERT_SCRIPT_RESOURCE, ".fit", masterFlatFile + masterFlatExt));
                }

                return masterFlatFile + ".fit";
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
