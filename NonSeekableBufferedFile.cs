using nom.tam.util;

namespace AutoFlats
{
    internal class NonSeekableBufferedFile : BufferedFile
    {
        public override bool CanSeek => false;

        public NonSeekableBufferedFile(string filename, FileAccess access, FileShare share) : base(filename, access, share)
        {
        }
    }
}
