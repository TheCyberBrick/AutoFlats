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

        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<InitOptions, TerminateOptions, ProceedOptions, FilterOptions, RotationOptions, BinningOptions>(args)
                .MapResult(
                (InitOptions opts) => Init(opts),
                (TerminateOptions opts) => Terminate(opts),
                (ProceedOptions opts) => Proceed(opts),
                (FilterOptions opts) => Filter(opts),
                (RotationOptions opts) => Rotation(opts),
                (BinningOptions opts) => Binning(opts),
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
    }
}