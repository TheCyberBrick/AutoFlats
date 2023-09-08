namespace AutoFlats
{
    public interface Stacker
    {
        string Stack(AutoFlats.FlatsSet set, IReadOnlyList<string> flats, Func<string, string>? darkMap);
    }
}
