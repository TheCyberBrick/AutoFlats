using CommandLine;
using System.Globalization;
using System.Text;

namespace AutoFlats
{
    public class Program
    {
        private class StatefulOptions
        {
            [Option("db", Required = false, Default = null, HelpText = "Path to database file.")]
            public string? DatabasePath { get; set; }
        }

        [Verb("init", HelpText = "Initializes the database. Finds all files on the specified path(s) and figures out which flats to take.")]
        private class InitOptions : StatefulOptions
        {
            [Option(Required = true, HelpText = "Path to FITS files. Can be a directory or FITS file.")]
            public IEnumerable<string> Files { get; set; }

            [Option("rtol", Default = 360, HelpText = "Rotation tolerance in degrees.")]
            public float RotationTolerance { get; set; }

            [Option(Default = false, HelpText = "Whether binning should be considered.")]
            public bool Binning { get; set; }
        }

        [Verb("terminate", HelpText = "Deletes the database.")]
        private class TerminateOptions : StatefulOptions
        {
        }

        [Verb("proceed", HelpText = "Proceeds to the next set of flats. Returns 'END' if there are no more flats to take.")]
        private class ProceedOptions : StatefulOptions
        {
        }

        [Verb("filter", HelpText = "Returns the filter of the current set of flats.")]
        private class FilterOptions : StatefulOptions
        {
        }

        [Verb("rotation", HelpText = "Returns the rotation (in degrees) of the current set of flats.")]
        private class RotationOptions : StatefulOptions
        {
        }

        private enum Axis
        {
            X, Y
        }

        [Verb("binning", HelpText = "Returns the binning of the current set of flats.")]
        private class BinningOptions : StatefulOptions
        {
            [Option(Default = Axis.X, HelpText = "Coordinate axis (X or Y).")]
            public Axis Axis { get; set; }
        }

        private enum StackingMethod
        {
            Siril
        }

        [Verb("stack", HelpText = "Integrates the current set of flats into a single stacked and calibrated master flat.")]
        private class StackOptions : StatefulOptions
        {
            [Option("method", HelpText = "Stacking method/program to be used for stacking and calibrating the flat frames.")]
            public StackingMethod StackingMethod { get; set; } = StackingMethod.Siril;

            [Option(HelpText = "Path to command line application to be used for stacking. For example, if Siril is used as stacking method then this must point to siril-cli.exe (e.g. \"C:\\Program Files\\Siril\\bin\\siril-cli.exe\").")]
            public string ApplicationPath { get; set; } = "C:\\Program Files\\Siril\\bin\\siril-cli.exe";

            [Option(Required = true, HelpText = "Path to flat frames. Must be a directory containing all flats. The flats may be located in subdirectories.")]
            public IEnumerable<string> Flats { get; set; }

            [Option(HelpText = "Path to dark(s) used for calibrating the flat frames. Can be a directory or FITS file. Calibration is skipped if none specified.")]
            public IEnumerable<string>? Darks { get; set; }

            [Option("exptol", Default = 5.0f, HelpText = "Exposure time tolerance in seconds. Used for matching darks to flats during calibration.")]
            public float ExposureTolerance { get; set; }

            [Option(Default = false, HelpText = "If set, only the master flat is kept and the other flat frames are deleted.")]
            public bool KeepOnlyMasterFlat { get; set; }

            [Option(Default = "", HelpText = "Prefix added to the output file.")]
            public string OutputPrefix { get; set; } = "";

            [Option(Default = "", HelpText = "Suffix added to the output file.")]
            public string OutputSuffix { get; set; } = "";
        }

        [Verb("masterFlat", HelpText = "Returns the path of the stacked master flat of the current set of flats.")]
        private class MasterFlatOptions : StatefulOptions
        {
        }

        private enum CalibrationMethod
        {
            Siril
        }

        [Verb("calibrate", HelpText = "Calibrates all lights matching the current set of flats with a stacked master flat.")]
        private class CalibrateOptions : StatefulOptions
        {
            [Option("method", HelpText = "Stacking method/program to be used for calibrating the light frames.")]
            public CalibrationMethod CalibrationMethod { get; set; } = CalibrationMethod.Siril;

            [Option(HelpText = "Path to command line application to be used for calibration. For example, if Siril is used as calibration method then this must point to siril-cli.exe (e.g. \"C:\\Program Files\\Siril\\bin\\siril-cli.exe\").")]
            public string ApplicationPath { get; set; } = "C:\\Program Files\\Siril\\bin\\siril-cli.exe";

            [Option(Required = true, HelpText = "Path to light frames. Must be a directory containing all lights. The lights may be located in subdirectories.")]
            public IEnumerable<string> Lights { get; set; }

            [Option(Required = true, HelpText = "Path to dark(s) used for calibrating the light frames. Can be a directory or FITS file.")]
            public IEnumerable<string> Darks { get; set; }

            [Option("exptol", Default = 5.0f, HelpText = "Exposure time tolerance in seconds. Used for matching darks to lights during calibration.")]
            public float ExposureTolerance { get; set; }

            [Option(Default = false, HelpText = "If set, only the calibrated lights are kept and the other light frames are deleted.")]
            public bool KeepOnlyCalibratedLights { get; set; }

            [Option(Default = "calibrated/", HelpText = "Prefix added to the output file(s).")]
            public string OutputPrefix { get; set; } = "";

            [Option(Default = "", HelpText = "Suffix added to the output file(s).")]
            public string OutputSuffix { get; set; } = "";
        }

        [Verb("calibratedLights", HelpText = "Returns the paths of the calibrated lights of the current set of flats.")]
        private class CalibratedLightsOptions : StatefulOptions
        {
        }

        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<InitOptions, TerminateOptions, ProceedOptions, FilterOptions, RotationOptions, BinningOptions, StackOptions, MasterFlatOptions, CalibrateOptions, CalibratedLightsOptions>(args)
                .MapResult(
                (InitOptions opts) => Init(opts),
                (TerminateOptions opts) => Terminate(opts),
                (ProceedOptions opts) => Proceed(opts),
                (FilterOptions opts) => Filter(opts),
                (RotationOptions opts) => Rotation(opts),
                (BinningOptions opts) => Binning(opts),
                (StackOptions opts) => Stack(opts),
                (MasterFlatOptions opts) => MasterFlat(opts),
                (CalibrateOptions opts) => Calibrate(opts),
                (CalibratedLightsOptions opts) => CalibratedLights(opts),
                errs => 1);
        }

        private static int Run(StatefulOptions opts, bool init, Action<AutoFlats> action)
        {
            try
            {
                var path = opts.DatabasePath ?? "autoflatsdb.json";
                if (!path.EndsWith(".json"))
                {
                    path += ".json";
                }

                var autoflats = new AutoFlats();

                if (File.Exists(path))
                {
                    autoflats.Load(path);
                }
                else if (!init)
                {
                    throw new Exception($"Database file {path} does not exist");
                }

                action(autoflats);

                if (init || autoflats.DbFile != null)
                {
                    autoflats.Save(path);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }

        private static int Init(InitOptions opts)
        {
            return Run(opts, true, autoflats =>
            {
                autoflats.LoatFlatsSets(opts.Files, opts.RotationTolerance, opts.Binning);
                autoflats.Sort();

                if (!autoflats.FlatsSets.Any())
                {
                    Console.WriteLine("OK: No flats required");
                }
                else
                {
                    StringBuilder sb = new();

                    sb.Append("OK: Flats required for ");

                    foreach (var set in autoflats.FlatsSets)
                    {
                        sb
                        .Append("[")
                        .Append(set.Binning.X).Append("x").Append(set.Binning.Y)
                        .Append(" ").Append(set.Filter).Append(" ")
                        .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.00}", set.Rotation))
                        .Append("°], ");
                    }

                    sb.Remove(sb.Length - 2, 2);

                    Console.WriteLine(sb);
                }
            });
        }
        private static int Terminate(TerminateOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                autoflats.Delete();
                Console.WriteLine("OK");
            });
        }

        private static int Proceed(ProceedOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                if (autoflats.Proceed())
                {
                    Console.WriteLine("OK");
                }
                else
                {
                    autoflats.Delete();
                    Console.WriteLine("END");
                }
            });
        }

        private static int Filter(FilterOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                Console.WriteLine(autoflats.GetCurrentFilter());
            });
        }

        private static int Rotation(RotationOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.00}", autoflats.GetCurrentRotation()));
            });
        }

        private static int Binning(BinningOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                var (bx, by) = autoflats.GetCurrentBinning();
                Console.WriteLine(opts.Axis == Axis.X ? bx : by);
            });
        }

        private static int Stack(StackOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                Stacker stacker;
                switch (opts.StackingMethod)
                {
                    case StackingMethod.Siril:
                        stacker = new SirilStacker(opts.ApplicationPath);
                        break;
                    default:
                        throw new Exception($"Unknown stacking method {opts.StackingMethod}");
                }

                autoflats.Stack(stacker, opts.Flats, opts.Darks, opts.ExposureTolerance, opts.KeepOnlyMasterFlat, opts.OutputPrefix, opts.OutputSuffix);

                Console.WriteLine("OK");
            });
        }

        private static int MasterFlat(MasterFlatOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                var masterFlat = autoflats.GetCurrentMasterFlat();
                if (masterFlat == null)
                {
                    throw new Exception("There is no stacked master flat");
                }
                Console.WriteLine(masterFlat);
            });
        }

        private static int Calibrate(CalibrateOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                Calibrator calibrator;
                switch (opts.CalibrationMethod)
                {
                    case CalibrationMethod.Siril:
                        calibrator = new SirilCalibrator(opts.ApplicationPath);
                        break;
                    default:
                        throw new Exception($"Unknown calibration method {opts.CalibrationMethod}");
                }

                autoflats.Calibrate(calibrator, opts.Lights, opts.Darks, opts.ExposureTolerance, opts.KeepOnlyCalibratedLights, opts.OutputPrefix, opts.OutputSuffix);

                Console.WriteLine("OK");
            });
        }

        private static int CalibratedLights(CalibratedLightsOptions opts)
        {
            return Run(opts, false, autoflats =>
            {
                var calibratedLights = autoflats.GetCurrentCalibratedLights();
                if (calibratedLights.Count == 0)
                {
                    throw new Exception("There are no calibrated lights");
                }
                foreach (var calibratedLight in calibratedLights)
                {
                    Console.WriteLine(calibratedLight);
                }
            });
        }
    }
}