using Mutagen.Bethesda.Plugins;
using NAudio.Wave;
namespace SynPatcher;
public class LineTracker
{
    public HashSet<FormKey> forms = [];
    public string guid = string.Empty;
    public ulong splen = 0;
}

public class VoiceMeta {
    public ulong splen = 0;
}

public static class Exts
{
    public static ulong GetMp3Duration(string fileName)
    {
        using var mp3Reader = new Mp3FileReader(fileName);
        return (ulong)mp3Reader.TotalTime.TotalMilliseconds;
    }
}