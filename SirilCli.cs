using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoFlats
{
    public abstract class SirilCli
    {
        protected const string FILE_EXT = "fit";

        protected readonly string sirilPath;

        public SirilCli(string sirilPath)
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

        protected void RunScript(string scriptPath)
        {
            var process = new Process();
            process.StartInfo.FileName = sirilPath;
            process.StartInfo.Arguments = $"--script \"{scriptPath}\"";
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

        protected List<string> MapToSequenceFiles(string workingDir, string sequenceName)
        {
            var sequenceFileRegex = new Regex($"^{sequenceName}_?[0-9]+.{FILE_EXT}$", RegexOptions.None);

            var sequenceFilesMap = new Dictionary<int, string>();

            foreach (string file in Directory.EnumerateFiles(workingDir, $"*.{FILE_EXT}", SearchOption.TopDirectoryOnly).Where(file => sequenceFileRegex.IsMatch(Path.GetFileName(file))))
            {
                var fileName = Path.GetFileName(file);

                var digits = new List<char>();
                for (int i = fileName.Length - FILE_EXT.Length - 2; i >= 0; --i)
                {
                    if (char.IsDigit(fileName[i]))
                    {
                        digits.Add(fileName[i]);
                    }
                    else
                    {
                        break;
                    }
                }
                digits.Reverse();

                if (int.TryParse(new string(digits.ToArray()), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nr))
                {
                    sequenceFilesMap.Add(nr, file);
                }
            }

            var sequenceFiles = new List<string>();

            for (int index = 0; index < sequenceFilesMap.Count; ++index)
            {
                if (sequenceFilesMap.TryGetValue(index + 1, out var file))
                {
                    sequenceFiles.Add(file);
                }
                else
                {
                    throw new Exception($"Incomplete sequence {sequenceName} in {workingDir}. Missing file #{index + 1}");
                }
            }

            return sequenceFiles;
        }

        protected Dictionary<string, string> MapToSequenceFiles(IReadOnlyList<string> files, string workingDir, string sequenceName)
        {
            var sequenceFiles = MapToSequenceFiles(workingDir, sequenceName);

            var sequenceFileMap = new Dictionary<string, string>();

            int index = 0;
            foreach (var file in files)
            {
                if (index < sequenceFiles.Count)
                {
                    sequenceFileMap.Add(file, sequenceFiles[index]);
                }
                else
                {
                    throw new Exception($"Failed mapping file {file} (#{index + 1}) to file in sequence {sequenceName} in {workingDir}");
                }

                ++index;
            }

            return sequenceFileMap;
        }
    }
}
