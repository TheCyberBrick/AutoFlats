namespace AutoFlats
{
    public interface Calibrator
    {
        bool CanWriteHeader { get; }

        List<string> Calibrate(AutoFlats.FlatsSet set, IReadOnlyList<string> lights, Func<string, string> darkMap, string flat, Func<string, Dictionary<string, (string, string?)>> additionalTagsMap);
    }
}
