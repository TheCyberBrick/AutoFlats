using Newtonsoft.Json;
using nom.tam.fits;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoFlats
{
    public class AutoFlats
    {
        public class FlatsSet
        {
            public FlatsSet() { }

            public FlatsSet(FitsInfo info)
            {
                Filter = info.Filter;
                Rotation = info.Rotation;
                Binning = info.Binning;
            }

            [JsonProperty(PropertyName = "filter", Required = Required.Always)]
            public string Filter { get; set; } = "";

            [JsonProperty(PropertyName = "rotation", Required = Required.Always)]
            public float Rotation { get; set; } = 0;

            [JsonProperty(PropertyName = "binning", Required = Required.Always)]
            public Binning Binning { get; set; } = new(1, 1);

            [JsonProperty(PropertyName = "count", NullValueHandling = NullValueHandling.Ignore)]
            public int Count { get; set; } = 0;
        }

        public record struct Binning(
            [JsonProperty(PropertyName = "x", Required = Required.Always)] int X,
            [JsonProperty(PropertyName = "y", Required = Required.Always)] int Y);

        private class State
        {
            [JsonProperty(PropertyName = "sets", Required = Required.Always)]
            public List<FlatsSet> FlatsSets { get; set; } = new();

            [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
            public int CurrentIndex = -1;

            [JsonProperty(PropertyName = "rotationTolerance", NullValueHandling = NullValueHandling.Ignore)]
            public float RotationTolerance { get; set; } = 360.0f;

            [JsonProperty(PropertyName = "binning", NullValueHandling = NullValueHandling.Ignore)]
            public bool Binning { get; set; } = false;
        }

        public record struct FitsInfo(string Filter, float Rotation, Binning Binning, float Exposure, int Width, int Height);

        private State state = new();

        public IEnumerable<FlatsSet> FlatsSets => state.FlatsSets;

        public string? DbFile { get; private set; }

        public void Load(string db)
        {
            string data;
            try
            {
                data = File.ReadAllText(db);
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot read database file {db}: {ex.Message}", ex);
            }

            State? state;
            try
            {
                state = JsonConvert.DeserializeObject<State>(data);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid database file {db}: {ex.Message}", ex);
            }

            if (state == null)
            {
                throw new Exception($"Invalid database file {db}");
            }

            this.state = state;
            DbFile = db;
        }

        public void Save(string? db = null)
        {
            db ??= DbFile;

            if (db != null)
            {
                string data = JsonConvert.SerializeObject(state);

                try
                {
                    File.WriteAllText(db, data);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Cannot write database file {db}", ex);
                }
            }
        }

        public void Delete()
        {
            if (DbFile != null)
            {
                File.Delete(DbFile);
            }
            DbFile = null;
        }

        public void LoatFlatsSets(IEnumerable<string> paths, float rotationTolerance, bool binning)
        {
            state = new()
            {
                RotationTolerance = rotationTolerance,
                Binning = binning
            };
            FindFlatsSets(paths);
        }

        private void FindFlatsSets(IEnumerable<string> paths)
        {
            var files = FindFitsFiles(paths);

            foreach (var file in files)
            {
                GetFitsInfoForFile(file, true, state.RotationTolerance < 180.0f, state.Binning, false, true, out var info);
                var fileFlatsSet = new FlatsSet(info);

                bool hasMatchingFlatsSet = false;

                foreach (var stateFlatsSet in state.FlatsSets)
                {
                    if (CompareFlatsSets(fileFlatsSet, stateFlatsSet, state.RotationTolerance, state.Binning))
                    {
                        hasMatchingFlatsSet = true;
                        ++stateFlatsSet.Count;
                        break;
                    }
                }

                if (!hasMatchingFlatsSet)
                {
                    fileFlatsSet.Count = 1;
                    state.FlatsSets.Add(fileFlatsSet);
                }
            }
        }

        public void Sort()
        {
            state.FlatsSets = new(state.FlatsSets.OrderBy(set => set.Rotation).ThenBy(set => set.Filter));
        }

        private bool CompareFlatsSets(FlatsSet flatsSet1, FlatsSet flatsSet2, float rotationTolerance, bool binning)
        {
            if (!flatsSet1.Filter.Equals(flatsSet2.Filter) || (binning && flatsSet1.Binning.Equals(flatsSet2.Binning)))
            {
                return false;
            }

            if (rotationTolerance < 180.0f)
            {
                float diff = flatsSet2.Rotation - flatsSet1.Rotation;

                float delta = Math.Clamp(diff - (float)Math.Floor(diff / 360.0f) * 360.0f, 0.0f, 360.0f);

                if (delta > 180.0F)
                {
                    delta -= 360.0F;
                }

                return Math.Abs(delta) < rotationTolerance;
            }

            return true;
        }

        private bool GetFitsInfoForFile(string file, bool requireFilter, bool requireRotation, bool requireBinning, bool requireExposure, bool throwOnMissingKeyword, out FitsInfo info)
        {
            info = new();

            try
            {
                var fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite);

                var cout = Console.Out;
                var cerr = Console.Error;

                // Temporarily disabling console output because
                // CSharpFits seems to print out debug stuff
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);

                Fits? fits = null;

                try
                {
                    fits = new Fits(fs);

                    fits.Read();

                    BasicHDU hdu = fits.GetHDU(0);
                    Header header = hdu.Header;

                    int width = header.GetIntValue("NAXIS1", -1);
                    if (width < 0)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing NAXIS1 keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    int height = header.GetIntValue("NAXIS2", -1);
                    if (height < 0)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing NAXIS2 keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    string filter = header.GetStringValue("FILTER");
                    if (filter == null && requireFilter)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing FILTER keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    double rotation = 0;
                    HeaderCard rotCard = header.FindCard("ROTATANG");
                    if (rotCard == null)
                    {
                        rotCard = header.FindCard("ROTATOR");
                    }
                    if (rotCard != null && rotCard.Value != null && double.TryParse(rotCard.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rot))
                    {
                        rotation = rot;
                    }
                    else if (requireRotation)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing ROTATANG / ROTATOR keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    int bx = header.GetIntValue("XBINNING", 0);
                    if (bx == 0 && requireBinning)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing XBINNING keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    int by = header.GetIntValue("YBINNING", 0);
                    if (by == 0 && requireBinning)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing YBINNING keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    double exposure = 0;
                    HeaderCard exposureCard = header.FindCard("EXPOSURE");
                    if (exposureCard == null)
                    {
                        exposureCard = header.FindCard("EXPTIME");
                    }
                    if (exposureCard != null && exposureCard.Value != null && double.TryParse(exposureCard.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var exp))
                    {
                        exposure = exp;
                    }
                    else if (requireExposure)
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new Exception($"Missing EXPOSURE / EXPTIME keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    info = new FitsInfo()
                    {
                        Filter = filter,
                        Rotation = (float)rotation,
                        Binning = new(bx, by),
                        Exposure = (float)exposure,
                        Width = width,
                        Height = height
                    };
                    return true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Cannot parse FITS file {file}: {ex.Message}", ex);
                }
                finally
                {
                    Console.SetOut(cout);
                    Console.SetError(cerr);

                    fits?.Close();
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot read FITS file {file}: {ex.Message}", ex);
            }
        }

        private IEnumerable<string> FindFitsFiles(IEnumerable<string> paths)
        {
            HashSet<string> files = new();

            Regex extensionPattern = new("\\.fit|\\.fits|\\.fts", RegexOptions.IgnoreCase);

            foreach (string path in paths)
            {
                var attr = File.GetAttributes(path);

                if (Directory.Exists(path))
                {
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(file => extensionPattern.IsMatch(Path.GetExtension(file) ?? "")))
                    {
                        files.Add(file);
                    }
                }
                else if (File.Exists(path))
                {
                    if (extensionPattern.IsMatch(Path.GetExtension(path) ?? ""))
                    {
                        files.Add(path);
                    }
                    else
                    {
                        throw new Exception($"Invalid file type {path}");
                    }
                }
                else
                {
                    throw new Exception($"Unknown path {path}");
                }
            }

            return files;
        }

        public bool Proceed()
        {
            if (state.FlatsSets.Count == 0)
            {
                state.CurrentIndex = -1;
                return false;
            }

            if (state.CurrentIndex < 0)
            {
                state.CurrentIndex = 0;
            }
            else
            {
                ++state.CurrentIndex;
            }

            if (state.CurrentIndex > state.FlatsSets.Count - 1)
            {
                state.CurrentIndex = state.FlatsSets.Count;
                return false;
            }

            return true;
        }

        private FlatsSet GetCurrentFlatsSet()
        {
            if (state.CurrentIndex < 0 || state.CurrentIndex > state.FlatsSets.Count - 1)
            {
                throw new Exception("Nothing left to do");
            }
            return state.FlatsSets[state.CurrentIndex];
        }

        public string GetCurrentFilter()
        {
            return GetCurrentFlatsSet().Filter;
        }

        public float GetCurrentRotation()
        {
            return GetCurrentFlatsSet().Rotation;
        }

        public Binning GetCurrentBinning()
        {
            return GetCurrentFlatsSet().Binning;
        }

        private Dictionary<string, string> FindMatchingDarks(IEnumerable<string> flats, IEnumerable<string> darks, float exposureTolerance)
        {
            Dictionary<string, FitsInfo> flatsInfo = new();
            foreach (var flat in flats)
            {
                if (GetFitsInfoForFile(flat, false, false, false, true, false, out var info))
                {
                    flatsInfo.Add(flat, info);
                }
            }

            Dictionary<string, FitsInfo> darksInfo = new();
            foreach (var dark in darks)
            {
                if (GetFitsInfoForFile(dark, false, false, false, true, false, out var info))
                {
                    darksInfo.Add(dark, info);
                }
            }

            Dictionary<string, string> matchedDarks = new();
            foreach (var flat in flats)
            {
                var flatInfo = flatsInfo[flat];

                string? bestDark = null;
                float bestDarkExposureDiff = 0;

                foreach (var dark in darks)
                {
                    var darkInfo = darksInfo[dark];

                    var expDiff = Math.Abs(flatInfo.Exposure - darkInfo.Exposure);
                    if (flatInfo.Width == darkInfo.Width && flatInfo.Height == darkInfo.Height && expDiff <= exposureTolerance && (bestDark == null || expDiff < bestDarkExposureDiff))
                    {
                        bestDark = dark;
                        bestDarkExposureDiff = expDiff;
                    }
                }

                if (bestDark != null)
                {
                    matchedDarks.Add(flat, bestDark);
                }
                else
                {
                    throw new Exception($"Flat file {flat} has no matching dark");
                }
            }

            return matchedDarks;
        }

        private List<string> GetFlatsForFlatsSet(IEnumerable<string> flats, FlatsSet set)
        {
            List<string> matchingFlats = new();

            foreach (var flat in flats)
            {
                if (GetFitsInfoForFile(flat, false, state.RotationTolerance < 180.0f, state.Binning, false, false, out var info))
                {
                    var flatsSet = new FlatsSet(info);

                    if (CompareFlatsSets(flatsSet, set, state.RotationTolerance, state.Binning))
                    {
                        matchingFlats.Add(flat);
                    }
                }
            }

            return matchingFlats;
        }

        public void Stack(Stacker stacker, IEnumerable<string> flatsPaths, IEnumerable<string>? darksPaths, float exposureTolerance, bool keepOnlyMasterFlat)
        {
            var currentFlatsSet = GetCurrentFlatsSet();
            var flats = GetFlatsForFlatsSet(FindFitsFiles(flatsPaths), currentFlatsSet);

            if (!flats.Any())
            {
                // Nothing to stack
                return;
            }

            string? parentDir = null;
            foreach (var flat in flats)
            {
                var flatParentDir = Path.GetDirectoryName(flat);

                if (parentDir != null && parentDir != flatParentDir)
                {
                    throw new Exception($"Flat {flat} is not in the same directory {flatParentDir} as the other flats");
                }

                parentDir = flatParentDir;
            }

            var darks = darksPaths != null ? FindMatchingDarks(flats, FindFitsFiles(darksPaths), exposureTolerance) : null;

            try
            {
                stacker.Stack(currentFlatsSet, flats, darks != null ? (flat => darks[flat]) : null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed stacking flats: {ex.Message}");
            }

            if (keepOnlyMasterFlat)
            {
                foreach (var flat in flats)
                {
                    try
                    {
                        File.Delete(flat);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed deleting flat after stacking: {ex.Message}");
                    }
                }
            }
        }
    }
}
