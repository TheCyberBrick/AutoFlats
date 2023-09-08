namespace AutoFlats
{
    [Flags]
    public enum FitsProperties
    {
        None = 0,
        Filter = 1,
        Rotation = 2,
        Binning = 4,
        Exposure = 8
    }
}
