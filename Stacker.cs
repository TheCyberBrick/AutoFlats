namespace AutoFlats
{
    public interface Stacker
    {
        void Stack(AutoFlats.FlatsSet set, IEnumerable<string> flats, Func<string, string>? flatDarkMap);
    }
}
