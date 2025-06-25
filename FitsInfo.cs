using Newtonsoft.Json;

namespace AutoFlats
{
    public record struct Binning(
        [JsonProperty(PropertyName = "x", Required = Required.Always)] int X,
        [JsonProperty(PropertyName = "y", Required = Required.Always)] int Y);

    public record struct FitsInfo(
        [JsonProperty(PropertyName = "filter", Required = Required.Always)] string Filter,
        [JsonProperty(PropertyName = "rotation", Required = Required.Always)] float Rotation,
        [JsonProperty(PropertyName = "focusPosition", Required = Required.Always)] float FocusPosition,
        [JsonProperty(PropertyName = "binning", Required = Required.Always)] Binning Binning,
        [JsonProperty(PropertyName = "exposure", Required = Required.Always)] float Exposure,
        [JsonProperty(PropertyName = "width", Required = Required.Always)] int Width,
        [JsonProperty(PropertyName = "height", Required = Required.Always)] int Height,
        [JsonProperty(PropertyName = "uncalibratedFileNameBase64")] string? UncalibratedFileNameBase64,
        [JsonProperty(PropertyName = "uncalibratedFileNameMD5")] string? UncalibratedFileNameMD5,
        [JsonProperty(PropertyName = "uncalibratedFileDataMD5")] string? UncalibratedFileDataMD5)
    {
        [JsonIgnore]
        public readonly bool IsCalibrated => UncalibratedFileNameBase64 != null && UncalibratedFileNameMD5 != null && UncalibratedFileDataMD5 != null;
    }
}
