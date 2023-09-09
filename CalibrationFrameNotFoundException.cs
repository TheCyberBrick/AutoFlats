namespace AutoFlats
{
    public class CalibrationFrameNotFoundException : Exception
    {
        public enum FrameType
        {
            Dark, Flat
        }

        public FrameType Type { init; get; }

        public string? Light { init; get; }

        public CalibrationFrameNotFoundException(FrameType type, string? light, string message) : base(message)
        {
            Type = type;
            Light = light;
        }
    }
}
