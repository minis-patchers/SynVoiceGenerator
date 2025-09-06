using Mutagen.Bethesda.Plugins;
using NAudio.Wave;
namespace SynPatcher;
public class LineTracker
{
    public HashSet<FormKey> forms = [];
    public HashSet<VariantData> variants = [];
}

public struct LineData {
    public string guid;
    public ulong splen;
}

public class VariantData {
    public ulong splen = 0;
    public string guid = string.Empty;
    public IEnumerable<string>? reg_frags = null;
}

public static class Exts
{
    public static ulong GetMp3Duration(string fileName)
    {
        using var mp3Reader = new Mp3FileReader(fileName);
        return (ulong)mp3Reader.TotalTime.TotalMilliseconds;
    }
}