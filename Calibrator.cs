namespace AutoFlats
{
    public interface Calibrator
    {
        List<string> Calibrate(AutoFlats.FlatsSet set, IReadOnlyList<string> lights, Func<string, string> darkMap, string flat);
    }
}
