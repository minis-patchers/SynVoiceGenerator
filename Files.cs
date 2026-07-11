using Noggog;

namespace SynPatcher;

public static class WPE
{
    static bool IsValidChar(char x) => Char.IsAsciiLetterOrDigit(x) || x == '-' || x == '=' || x == '$' || x == '<' || x == '>' || x == ' ' || x == '.' || x == ',' || x == '?' || x == '!' || x == '\"' || x == '\'' || x == '*' || x == '[' || x == ']' || x == '(' || x == ')';
    public static string CleanString(this string? str) => new string([.. REG.HiddenFN2.Replace(REG.HiddenFN.Replace(str ?? string.Empty, ""), "").Where(IsValidChar)]).Trim().TrimEnd(',').Replace("\n", " ").Replace("\r", " ").Replace("\t", " ").Replace("  ", " ").TrimEnd(' ');
    public static string RemovePunct(this string? str) => str?.Replace(".", "")?.Replace("!", "")?.Replace("?", "")?.Replace(",", "")?.Replace("\"", "") ?? string.Empty;
    public static bool DoSkip(this string? str) => str.CleanString().RemovePunct().Replace("-", "").Replace(".", "").Trim().IsNullOrEmpty();
    public static ulong GetWavLengthInMilliseconds(this string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        // Skip RIFF header marker (4 bytes), File Size (4 bytes), and WAVE marker (4 bytes)
        br.ReadBytes(12);

        int byteRate = 0;
        int dataSize = 0;

        // Scan the chunks sequentially to find byte rate and data size
        while (fs.Position < fs.Length)
        {
            string chunkId = new string(br.ReadChars(4));
            int chunkSize = br.ReadInt32();

            if (chunkId == "fmt ")
            {
                br.ReadInt16(); // Skip AudioFormat
                br.ReadInt16(); // Skip NumChannels
                br.ReadInt32(); // Skip SampleRate
                byteRate = br.ReadInt32(); // Read ByteRate (Bytes per second)
                br.ReadInt16(); // Skip BlockAlign
                br.ReadInt16(); // Skip BitsPerSample

                // Skip any extra format configuration bytes if present
                if (chunkSize > 16) br.ReadBytes(chunkSize - 16);
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize; // Read total size of raw audio data
                break; // Stop parsing as we now have both required values
            }
            else
            {
                // Safely skip metadata chunks like LIST, ID3, or metadata tags
                fs.Position += chunkSize;
            }
        }

        if (byteRate == 0 || dataSize == 0)
            throw new InvalidDataException("Invalid or unsupported WAV file structure.");

        // Calculate time: (Data Size / Bytes Per Second) * 1000 milliseconds
        return (ulong)Math.Ceiling(((double)dataSize / byteRate) * 1000);
    }

}

public struct LineData
{
    public string guid;
    public ulong length_ms;
}

public struct VariantData
{
    public string guid;
    public ulong frag_hash;
    public ulong? length_ms;
}
