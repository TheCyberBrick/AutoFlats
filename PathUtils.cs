namespace AutoFlats
{
    public static class PathUtils
    {
        // https://stackoverflow.com/a/66877016
        public static bool IsSubPathOf(this string subPath, string basePath)
        {
            var rel = Path.GetRelativePath(
                basePath.Replace('\\', '/'),
                subPath.Replace('\\', '/'));
            return rel != "."
                && rel != ".."
                && !rel.StartsWith("../")
                && !Path.IsPathRooted(rel);
        }
    }
}
