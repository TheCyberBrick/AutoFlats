namespace AutoFlats
{
    public class KeywordNotFoundException : Exception
    {
        public string File { init; get; }

        public string Keyword { init; get; }

        public KeywordNotFoundException(string file, string keyword, string message) : base(message)
        {
            File = file;
            Keyword = keyword;
        }
    }
}
