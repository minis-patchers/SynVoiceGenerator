using Mutagen.Bethesda.Plugins;
using NAudio.Wave;
using Noggog;

namespace SynPatcher;

public struct WordPatch
{
    public int Index;
    public string? NewWord;
}
public static class WPE
{
    static bool IsValidChar(char x) => Char.IsAsciiLetterOrDigit(x) || x == '-' || x == '=' || x == '$' || x == '<' || x == '>' || x == ' ' || x == '.' || x == ',' || x == '?' || x == '!' || x == '\"' || x == '\'' || x == '*' || x == '[' || x == ']' || x == '(' || x == ')';
    public static string CleanString(this string? str) => new string([.. REG.HiddenFN2.Replace(REG.HiddenFN.Replace(str ?? string.Empty, ""), "").Where(IsValidChar)]).Trim().TrimEnd(',').Replace("\n", " ").Replace("\r", " ").Replace("\t", " ").Replace("  ", " ").TrimEnd(' ');
    public static string RemovePunct(this string? str) => str?.Replace(".", "")?.Replace("!", "")?.Replace("?", "")?.Replace(",", "")?.Replace("\"", "") ?? string.Empty;
    public static bool Skip(this string? str) => str.CleanString().RemovePunct().Replace("-", "").Replace(".", "").Trim().IsNullOrEmpty();
    public static bool VerifyString(this IEnumerable<WordPatch> patches, string candidate)
    {
        var words = candidate.CleanString().RemovePunct().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int expectedLength = patches
                    .Where(p => p.NewWord != null)
                    .Select(p => p.Index + 1)
                    .DefaultIfEmpty(0)
                    .Max();
        if (words.Length != expectedLength) return false;
        foreach (var patch in patches)
        {
            bool wordExistsAtPos = patch.Index < words.Length;
            if (patch.NewWord == null)
            {
                if (wordExistsAtPos) return false;
            }
            else
            {
                if (!wordExistsAtPos) return false;
                if (!string.Equals(words[patch.Index], patch.NewWord))
                {
                    return false;
                }
            }
        }
        return true;
    }
    public static IEnumerable<WordPatch> GetWordDifferences(this string source, string target)
    {
        var patches = new List<WordPatch>();
        var words1 = source.CleanString().RemovePunct().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
        var words2 = target.CleanString().RemovePunct().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
        int maxLen = Math.Max(words1.Length, words2.Length);
        int newLen = words2.Length - 1;
        for (int i = 0; i < maxLen; i++)
        {
            string? w1 = i < words1.Length ? words1[i] : null;
            string? w2 = i < words2.Length ? words2[i] : null;
            if (!string.Equals(w1, w2, StringComparison.OrdinalIgnoreCase))
            {
                patches.Add(new WordPatch()
                {
                    Index = i,
                    NewWord = w2
                });
            }
        }
        if (patches.Any() && !patches.Any(x => x.Index == newLen))
        {
            patches.Add(new WordPatch()
            {
                Index = newLen,
                NewWord = words2[newLen]
            });
        }
        return patches;
    }
}

public class LineTracker
{
    public HashSet<FormKey> forms = [];
    public HashSet<VariantData> variants = [];
}

public struct LineData
{
    public string guid;
    public ulong splen;
}

public class VariantData
{
    public ulong splen = 0;
    public string guid = string.Empty;
    public IEnumerable<WordPatch>? reg_frags = null;
}

public static class Exts
{
    public static ulong GetMp3Duration(string fileName)
    {
        using var mp3Reader = new Mp3FileReader(fileName);
        return (ulong)mp3Reader.TotalTime.TotalMilliseconds;
    }
}
