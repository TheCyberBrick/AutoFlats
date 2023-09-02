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
            [JsonProperty(PropertyName = "X", Required = Required.Always)] int X,
            [JsonProperty(PropertyName = "Y", Required = Required.Always)] int Y);

        private class State
        {
            [JsonProperty(PropertyName = "sets", Required = Required.Always)]
            public List<FlatsSet> FlatsSets { get; set; } = new();

            [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
            public int CurrentIndex = -1;
        }

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
            state = new();
            FindFlatsSets(paths, rotationTolerance, binning);
        }

        public void FindFlatsSets(IEnumerable<string> paths, float rotationTolerance, bool binning)
        {
            var files = FindFitsFiles(paths);

            foreach (var file in files)
            {
                var fileFlatsSet = GetFlatsSetForFile(file, rotationTolerance < 180.0f, binning);

                bool hasMatchingFlatsSet = false;

                foreach (var stateFlatsSet in state.FlatsSets)
                {
                    if (CompareFlatsSets(fileFlatsSet, stateFlatsSet, rotationTolerance, binning))
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

        private FlatsSet GetFlatsSetForFile(string file, bool requireRotation, bool requireBinning)
        {
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

                    string filter = header.GetStringValue("FILTER");
                    if (filter == null)
                    {
                        throw new Exception($"Missing FILTER keyword in FITS file {file}");
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
                        throw new Exception($"Missing ROTATANG / ROTATOR keyword in FITS file {file}");
                    }

                    int bx = header.GetIntValue("XBINNING", 0);
                    if (bx == 0 && requireBinning)
                    {
                        throw new Exception($"Missing XBINNING keyword in FITS file {file}");
                    }

                    int by = header.GetIntValue("YBINNING", 0);
                    if (by == 0 && requireBinning)
                    {
                        throw new Exception($"Missing YBINNING keyword in FITS file {file}");
                    }

                    return new FlatsSet()
                    {
                        Filter = filter,
                        Rotation = (float)rotation,
                        Binning = new(bx, by)
                    };
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
    }
}
