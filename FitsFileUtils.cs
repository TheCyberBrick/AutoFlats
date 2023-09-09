using nom.tam.fits;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoFlats
{
    public static class FitsFileUtils
    {
        public static IEnumerable<string> FindFitsFiles(IEnumerable<string> paths)
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

        public static FitsInfo GetFitsInfoForFile(string file, FitsProperties requiredProperties)
        {
            DoTryGetFitsInfoForFile(file, requiredProperties, true, out var info);
            return info;
        }

        public static bool TryGetFitsInfoForFile(string file, FitsProperties requiredProperties, out FitsInfo info)
        {
            return DoTryGetFitsInfoForFile(file, requiredProperties, false, out info);
        }

        private static bool DoTryGetFitsInfoForFile(string file, FitsProperties requiredProperties, bool throwOnMissingKeyword, out FitsInfo info)
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
                            throw new KeywordNotFoundException(file, "NAXIS1", $"Missing NAXIS1 keyword in FITS file {file}");
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
                            throw new KeywordNotFoundException(file, "NAXIS2", $"Missing NAXIS2 keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    string filter = header.GetStringValue("FILTER");
                    if (filter == null && requiredProperties.HasFlag(FitsProperties.Filter))
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new KeywordNotFoundException(file, "FILTER", $"Missing FILTER keyword in FITS file {file}");
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
                    else if (requiredProperties.HasFlag(FitsProperties.Rotation))
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new KeywordNotFoundException(file, "ROTATANG", $"Missing ROTATANG / ROTATOR keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    int bx = header.GetIntValue("XBINNING", 0);
                    if (bx == 0 && requiredProperties.HasFlag(FitsProperties.Binning))
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new KeywordNotFoundException(file, "XBINNING", $"Missing XBINNING keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    int by = header.GetIntValue("YBINNING", 0);
                    if (by == 0 && requiredProperties.HasFlag(FitsProperties.Binning))
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new KeywordNotFoundException(file, "YBINNING", $"Missing YBINNING keyword in FITS file {file}");
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
                    else if (requiredProperties.HasFlag(FitsProperties.Exposure))
                    {
                        if (throwOnMissingKeyword)
                        {
                            throw new KeywordNotFoundException(file, "EXPOSURE", $"Missing EXPOSURE / EXPTIME keyword in FITS file {file}");
                        }
                        else
                        {
                            return false;
                        }
                    }

                    info = new FitsInfo()
                    {
                        Filter = filter ?? "",
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
    }
}
