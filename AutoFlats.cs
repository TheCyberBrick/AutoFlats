﻿using Newtonsoft.Json;
using nom.tam.fits;
using System.Data;
using System.Diagnostics.CodeAnalysis;

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
                FocusPosition = info.FocusPosition;
                Binning = info.Binning;
            }

            [JsonProperty(PropertyName = "filter", Required = Required.Always)]
            public string Filter { get; set; } = "";

            [JsonProperty(PropertyName = "rotation", Required = Required.Always)]
            public float Rotation { get; set; } = 0;

            [JsonProperty(PropertyName = "focusPosition", Required = Required.Always)]
            public float FocusPosition { get; set; } = 0;

            [JsonProperty(PropertyName = "binning", Required = Required.Always)]
            public Binning Binning { get; set; } = new(1, 1);

            [JsonProperty(PropertyName = "count", NullValueHandling = NullValueHandling.Ignore)]
            public int Count { get; set; } = 0;

            [JsonProperty(PropertyName = "masterFlat", NullValueHandling = NullValueHandling.Ignore)]
            public string? MasterFlat { get; set; }

            [JsonProperty(PropertyName = "calibratedLights", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> CalibratedLights { get; set; } = new();

            [JsonProperty(PropertyName = "processedLights", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> ProcessedLights { get; set; } = new();
        }

        private class State
        {
            [JsonProperty(PropertyName = "sets", Required = Required.Always)]
            public List<FlatsSet> FlatsSets { get; set; } = new();

            [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
            public int CurrentIndex = -1;

            [JsonProperty(PropertyName = "rotationTolerance", NullValueHandling = NullValueHandling.Ignore)]
            public float RotationTolerance { get; set; } = -1.0f;

            [JsonProperty(PropertyName = "focusTolerance", NullValueHandling = NullValueHandling.Ignore)]
            public float FocusTolerance { get; set; } = -1.0f;

            [JsonProperty(PropertyName = "binning", NullValueHandling = NullValueHandling.Ignore)]
            public bool Binning { get; set; } = false;

            [JsonIgnore]
            public FitsProperties RequiredFlatsProperties
            {
                get
                {
                    FitsProperties properties = FitsProperties.Filter;

                    if (RotationTolerance >= 0.0f && RotationTolerance < 180.0f)
                    {
                        properties |= FitsProperties.Rotation;
                    }

                    if (FocusTolerance >= 0.0f)
                    {
                        properties |= FitsProperties.FocusPosition;
                    }

                    if (Binning)
                    {
                        properties |= FitsProperties.Binning;
                    }

                    return properties;
                }
            }
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

        public void LoadFlatsSets(IEnumerable<string> paths, float rotationTolerance, float focusTolerance, bool binning)
        {
            state = new()
            {
                RotationTolerance = rotationTolerance,
                FocusTolerance = focusTolerance,
                Binning = binning
            };
            FindAndAddFlatsSets(paths);
        }

        private FitsInfo GetFitsInfoForFile(string file)
        {
            return FitsFileUtils.GetFitsInfoForFile(file, state.RequiredFlatsProperties);
        }

        private bool TryGetFitsInfoForFile(string file, out FitsInfo fitsInfo)
        {
            return FitsFileUtils.TryGetFitsInfoForFile(file, state.RequiredFlatsProperties, out fitsInfo);
        }

        private FlatsSet GetFlatsSetForFile(string file)
        {
            return new FlatsSet(GetFitsInfoForFile(file));
        }

        private bool TryGetFlatsSetForFile(string file, [NotNullWhen(true)] out FlatsSet? flatsSet)
        {
            if (TryGetFitsInfoForFile(file, out var fitsInfo))
            {
                flatsSet = new FlatsSet(fitsInfo);
                return true;
            }
            flatsSet = null;
            return false;
        }

        private IEnumerable<string> FindOriginalFitsFiles(IEnumerable<string> paths, bool excludeCalibratedLights, bool excludeProcessedLights)
        {
            bool isExcluded(FlatsSet set, string file)
            {
                if (set.MasterFlat != null && Path.GetFullPath(set.MasterFlat) == Path.GetFullPath(file))
                {
                    return true;
                }
                if (excludeCalibratedLights && set.CalibratedLights.Any(light => Path.GetFullPath(light) == Path.GetFullPath(file)))
                {
                    return true;
                }
                if (excludeProcessedLights && set.ProcessedLights.Any(light => Path.GetFullPath(light) == Path.GetFullPath(file)))
                {
                    return true;
                }
                return false;
            }
            return FitsFileUtils.FindFitsFiles(paths).Where(file => !state.FlatsSets.Any(set => isExcluded(set, file)));
        }

        private void FindAndAddFlatsSets(IEnumerable<string> paths)
        {
            IEnumerable<(string File, FitsInfo FitsInfo)> fileFitsInfos = FindOriginalFitsFiles(paths, false, false).Select(file => (file, GetFitsInfoForFile(file))).OrderBy(t => t.file).ToList();

            var fileFlatsSets = new Dictionary<string, FlatsSet>();

            var calibratedFitsInfoMap = new Dictionary<string, FitsInfo>();
            var calibratedFileFlatsSetList = new List<(string File, FlatsSet FlatsSet)>();

            var unprocessedFiles = new List<string>();
            var processedFiles = new List<string>();

            foreach (var (file, fitsInfo) in fileFitsInfos)
            {
                var fileFlatsSet = new FlatsSet(fitsInfo);

                if (fitsInfo.IsCalibrated && fitsInfo.UncalibratedFileNameBase64 != null && fitsInfo.UncalibratedFileNameMD5 != null && fitsInfo.UncalibratedFileDataMD5 != null)
                {
                    calibratedFitsInfoMap[fitsInfo.UncalibratedFileNameMD5.ToLowerInvariant()] = fitsInfo;
                    calibratedFileFlatsSetList.Add((file, fileFlatsSet));
                }
                else
                {
                    fileFlatsSets[file] = fileFlatsSet;
                }
            }

            foreach (var (file, _) in fileFitsInfos)
            {
                if (fileFlatsSets.ContainsKey(file))
                {
                    bool isProcessed = false;

                    var fileName = Path.GetFileName(file);
                    var fileNameHash = FitsFileUtils.CalculateTextHash(fileName);

                    if (calibratedFitsInfoMap.TryGetValue(fileNameHash.ToLowerInvariant(), out var calibratedFitsInfo) && calibratedFitsInfo.UncalibratedFileNameBase64 != null && calibratedFitsInfo.UncalibratedFileDataMD5 != null)
                    {
                        var fileNameBase64 = FitsFileUtils.CalculateTextBase64(fileName);

                        var expectedFileNameBase64 = calibratedFitsInfo.UncalibratedFileNameBase64.Trim();
                        if (expectedFileNameBase64.EndsWith("&"))
                        {
                            expectedFileNameBase64 = expectedFileNameBase64.Substring(0, expectedFileNameBase64.Length - 1);
                        }

                        if (fileNameBase64.StartsWith(expectedFileNameBase64))
                        {
                            var fileDataHash = FitsFileUtils.CalculateFileHash(file);

                            if (fileDataHash.ToLowerInvariant().Equals(calibratedFitsInfo.UncalibratedFileDataMD5.ToLowerInvariant()))
                            {
                                isProcessed = true;
                            }
                        }
                    }

                    if (isProcessed)
                    {
                        processedFiles.Add(file);
                    }
                    else
                    {
                        unprocessedFiles.Add(file);
                    }
                }
            }

            FlatsSet? findMatchingFlatsSet(FlatsSet set)
            {
                foreach (var stateFlatsSet in state.FlatsSets)
                {
                    if (CompareFlatsSets(set, stateFlatsSet, state.RotationTolerance, state.FocusTolerance, state.Binning))
                    {
                        return stateFlatsSet;
                    }
                }
                return null;
            }

            foreach (var file in unprocessedFiles)
            {
                var fileFlatsSet = fileFlatsSets[file];

                FlatsSet? matchingFlatsSet = findMatchingFlatsSet(fileFlatsSet);

                if (matchingFlatsSet != null)
                {
                    ++matchingFlatsSet.Count;
                }
                else
                {
                    fileFlatsSet.Count = 1;
                    state.FlatsSets.Add(fileFlatsSet);
                }
            }

            foreach (var file in processedFiles)
            {
                FlatsSet? matchingFlatsSet = findMatchingFlatsSet(fileFlatsSets[file]);

                if (matchingFlatsSet != null)
                {
                    matchingFlatsSet.ProcessedLights.Add(file);
                }
            }

            foreach (var (file, fileFlatsSet) in calibratedFileFlatsSetList)
            {
                FlatsSet? matchingFlatsSet = findMatchingFlatsSet(fileFlatsSet);

                if (matchingFlatsSet != null)
                {
                    matchingFlatsSet.CalibratedLights.Add(file);
                }
            }
        }

        public void Sort()
        {
            state.FlatsSets = new(state.FlatsSets.OrderBy(set => set.Rotation).ThenBy(set => set.FocusPosition).ThenBy(set => set.Filter));
        }

        private bool CompareFlatsSets(FlatsSet flatsSet1, FlatsSet flatsSet2, float rotationTolerance, float focusTolerance, bool binning)
        {
            if (!flatsSet1.Filter.Equals(flatsSet2.Filter))
            {
                return false;
            }

            if (binning && !flatsSet1.Binning.Equals(flatsSet2.Binning))
            {
                return false;
            }

            if (rotationTolerance >= 0.0f && rotationTolerance < 180.0f)
            {
                float diff = flatsSet2.Rotation - flatsSet1.Rotation;

                float delta = Math.Clamp(diff - (float)Math.Floor(diff / 360.0f) * 360.0f, 0.0f, 360.0f);

                if (delta > 180.0F)
                {
                    delta -= 360.0F;
                }

                if (Math.Abs(delta) > rotationTolerance)
                {
                    return false;
                }
            }

            if (focusTolerance >= 0.0f && Math.Abs(flatsSet2.FocusPosition - flatsSet1.FocusPosition) > focusTolerance)
            {
                return false;
            }

            return true;
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

        public float GetCurrentFocusPosition()
        {
            return GetCurrentFlatsSet().FocusPosition;
        }

        public Binning GetCurrentBinning()
        {
            return GetCurrentFlatsSet().Binning;
        }

        public Dictionary<string, string> FindMatchingDarks(IEnumerable<string> lights, IEnumerable<string> darks, float exposureTolerance)
        {
            Dictionary<string, FitsInfo> lightsInfo = new();
            foreach (var light in lights)
            {
                if (FitsFileUtils.TryGetFitsInfoForFile(light, FitsProperties.Exposure, out var info))
                {
                    lightsInfo.Add(light, info);
                }
            }

            Dictionary<string, FitsInfo> darksInfo = new();
            foreach (var dark in darks)
            {
                if (FitsFileUtils.TryGetFitsInfoForFile(dark, FitsProperties.Exposure, out var info))
                {
                    darksInfo.Add(dark, info);
                }
            }

            Dictionary<string, string> matchedDarks = new();
            foreach (var light in lights)
            {
                var lightInfo = lightsInfo[light];

                string? bestDark = null;
                float bestDarkExposureDiff = 0;

                foreach (var dark in darks)
                {
                    var darkInfo = darksInfo[dark];

                    var expDiff = Math.Abs(lightInfo.Exposure - darkInfo.Exposure);
                    if (lightInfo.Width == darkInfo.Width && lightInfo.Height == darkInfo.Height && expDiff <= exposureTolerance && (bestDark == null || expDiff < bestDarkExposureDiff))
                    {
                        bestDark = dark;
                        bestDarkExposureDiff = expDiff;
                    }
                }

                if (bestDark != null)
                {
                    matchedDarks.Add(light, bestDark);
                }
                else
                {
                    throw new CalibrationFrameNotFoundException(CalibrationFrameNotFoundException.FrameType.Dark, light, $"File {light} has no matching dark");
                }
            }

            return matchedDarks;
        }

        public List<string> GetFilesForFlatsSet(IEnumerable<string> files, FlatsSet set, bool throwOnMissingKeyword)
        {
            List<string> matchingFiles = new();

            foreach (var file in files)
            {
                FlatsSet? flatsSet = null;

                if (throwOnMissingKeyword)
                {
                    flatsSet = GetFlatsSetForFile(file);
                }
                else
                {
                    TryGetFlatsSetForFile(file, out flatsSet);
                }

                if (flatsSet != null && CompareFlatsSets(flatsSet, set, state.RotationTolerance, state.FocusTolerance, state.Binning))
                {
                    matchingFiles.Add(file);
                }
            }

            return matchingFiles;
        }

        public void Stack(Stacker stacker, IEnumerable<string> flatsPaths, IEnumerable<string>? darksPaths, float exposureTolerance, bool keepOnlyMasterFlat, string outputPrefix, string outputSuffix)
        {
            var currentFlatsSet = GetCurrentFlatsSet();
            var flats = GetFilesForFlatsSet(FindOriginalFitsFiles(flatsPaths, false, false), currentFlatsSet, false);

            if (!flats.Any())
            {
                // Nothing to stack
                return;
            }

            var darks = darksPaths != null ? FindMatchingDarks(flats, FindOriginalFitsFiles(darksPaths, false, false), exposureTolerance) : null;

            string masterFlat;
            try
            {
                masterFlat = stacker.Stack(currentFlatsSet, flats, darks != null ? (flat => darks[flat]) : null);

                if (!File.Exists(masterFlat))
                {
                    throw new Exception($"Stacked master flat {masterFlat} not found");
                }
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

            try
            {
                var masterFlatDir = Path.GetDirectoryName(masterFlat) ?? throw new Exception($"Couldn't find master flat {masterFlat} parent directory");
                var masterFlatFileName = Path.GetFileNameWithoutExtension(masterFlat);

                var newMasterFlatPath = Path.Combine(masterFlatDir, outputPrefix + masterFlatFileName + outputSuffix + ".fit");
                var newMasterFlatDir = Path.GetDirectoryName(newMasterFlatPath) ?? throw new Exception($"Couldn't find master flat {newMasterFlatPath} parent directory");

                if (newMasterFlatDir != masterFlatDir && !newMasterFlatDir.IsSubPathOf(masterFlatDir))
                {
                    throw new Exception($"Invalid output prefix {outputPrefix} or suffix {outputSuffix}");
                }

                Directory.CreateDirectory(newMasterFlatDir);

                File.Move(masterFlat, newMasterFlatPath, true);

                // Save to state
                currentFlatsSet.MasterFlat = Path.GetFullPath(newMasterFlatPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed moving master flat {masterFlat}: {ex.Message}");
            }
        }

        public string? GetCurrentMasterFlat()
        {
            return GetCurrentFlatsSet().MasterFlat;
        }

        public bool Calibrate(Calibrator calibrator, IEnumerable<string> lightsPaths, IEnumerable<string> darksPaths, float exposureTolerance, bool copyHeaders, bool keepOnlyCalibratedLights, string outputPrefix, string outputSuffix, int batchSize)
        {
            var currentFlatsSet = GetCurrentFlatsSet();
            var lights = GetFilesForFlatsSet(FindOriginalFitsFiles(lightsPaths, true, true), currentFlatsSet, true);

            bool complete = true;

            if (batchSize > 0 && batchSize < lights.Count)
            {
                lights = lights.Take(batchSize).ToList();
                complete = false;
            }

            if (!lights.Any())
            {
                // Nothing to calibrate
                return true;
            }

            var flat = GetCurrentMasterFlat();
            if (flat == null)
            {
                throw new CalibrationFrameNotFoundException(CalibrationFrameNotFoundException.FrameType.Flat, null, "There is no master flat");
            }

            var darks = FindMatchingDarks(lights, FindOriginalFitsFiles(darksPaths, false, false), exposureTolerance);

            var additionalTagsMap = Dictionary<string, (string, string?)> (string light) =>
            {
                try
                {
                    var lightFileName = Path.GetFileName(light);
                    return new()
                        {
                            { FitsFileUtils.UNCALIBRATED_FILE_NAME_BASE64_KEYWORD, (FitsFileUtils.CalculateTextBase64(lightFileName), "Base64 of name of uncalibrated file") },
                            { FitsFileUtils.UNCALIBRATED_FILE_NAME_MD5_KEYWORD, (FitsFileUtils.CalculateTextHash(lightFileName).ToLowerInvariant(), "MD5 of name of uncalibrated file") },
                            { FitsFileUtils.UNCALIBRATED_FILE_DATA_MD5_KEYWORD, (FitsFileUtils.CalculateFileHash(light).ToLowerInvariant(), "MD5 of data of uncalibrated file") }
                        };
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed calculating hash from {light}: {ex.Message}", ex);
                }
            };

            List<string> calibratedLights;
            try
            {
                calibratedLights = calibrator.Calibrate(currentFlatsSet, lights, light => darks[light], flat, additionalTagsMap);

                if (calibratedLights.Count < lights.Count)
                {
                    throw new Exception($"Missing calibrated lights, expected {lights.Count}, got {calibratedLights.Count}");
                }

                foreach (var calibratedLight in calibratedLights)
                {
                    if (!File.Exists(calibratedLight))
                    {
                        throw new Exception($"Calibrated light {calibratedLight} not found");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed calibrating lights: {ex.Message}");
            }

            if (copyHeaders && !calibrator.CanWriteHeader)
            {
                for (int i = 0; i < lights.Count; ++i)
                {
                    var light = lights[i];
                    var calibratedLight = calibratedLights[i];

                    // Some values image properties may be changed during calibration
                    // so those must not be replaced with the previous values
                    var mergeExclusions = new HashSet<string>()
                    {
                        "BITPIX", "BSCALE", "BZERO", "NAXIS1", "NAXIS2", "ROWORDER"
                    };

                    try
                    {
                        FitsFileUtils.MergeFitsHeader(calibratedLight, light, mergeExclusions, additionalTagsMap(light));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed copying header from {light} to {calibratedLight}: {ex.Message}", ex);
                    }
                }
            }

            if (keepOnlyCalibratedLights)
            {
                foreach (var light in lights)
                {
                    try
                    {
                        File.Delete(light);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed deleting light after calibration: {ex.Message}");
                    }
                }
            }

            for (int i = 0; i < lights.Count; ++i)
            {
                var light = lights[i];
                var calibratedLight = calibratedLights[i];

                try
                {
                    var lightDir = Path.GetDirectoryName(light) ?? throw new Exception($"Couldn't find light {light} parent directory");
                    var lightFileName = Path.GetFileNameWithoutExtension(light);

                    var newLightPath = Path.Combine(lightDir, outputPrefix + lightFileName + outputSuffix + ".fit");
                    var newLightDir = Path.GetDirectoryName(newLightPath) ?? throw new Exception($"Couldn't find light {newLightPath} parent directory");

                    if (newLightDir != lightDir && !newLightDir.IsSubPathOf(lightDir))
                    {
                        throw new Exception($"Invalid output prefix {outputPrefix} or suffix {outputSuffix}");
                    }

                    Directory.CreateDirectory(newLightDir);

                    File.Move(calibratedLight, newLightPath, true);

                    // Save to state
                    currentFlatsSet.CalibratedLights.Add(Path.GetFullPath(newLightPath));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed moving light {light}: {ex.Message}");
                }
            }

            // Save to state
            currentFlatsSet.ProcessedLights.AddRange(lights);

            return complete;
        }

        public IReadOnlyList<string> GetCurrentCalibratedLights()
        {
            return GetCurrentFlatsSet().CalibratedLights;
        }

        public IReadOnlyList<string> GetCurrentProcessedLights()
        {
            return GetCurrentFlatsSet().ProcessedLights;
        }

        public IReadOnlyList<string> GetMatchingFiles(IEnumerable<string> paths, bool excludeCalibratedFiles, bool excludeProcessedFiles)
        {
            return GetFilesForFlatsSet(FindOriginalFitsFiles(paths, excludeCalibratedFiles, excludeProcessedFiles), GetCurrentFlatsSet(), false);
        }
    }
}
