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
        [JsonProperty(PropertyName = "height", Required = Required.Always)] int Height);
}
