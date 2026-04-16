using Mutagen.Bethesda.Plugins;
using NAudio.Wave;
using System.Text.RegularExpressions;
namespace SynPatcher;

public record WordPatch(int Index, string? NewWord)
{
    // Instance Method: Clean the individual word
    public string? GetCleanWord() =>
        NewWord != null ? Regex.Replace(NewWord, @"\p{P}", "").ToLower() : null;

    // Instance Method: Check if a specific string matches this patch
    public bool IsMatch(string wordInString)
    {
        string cleanInput = Regex.Replace(wordInString, @"\p{P}", "");
        return string.Equals(cleanInput, GetCleanWord(), StringComparison.OrdinalIgnoreCase);
    }
}
public static class WPE
{
    static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"\p{P}", "").ToLower();
    }
    public static bool VerifyString(this IEnumerable<WordPatch> patches, string candidate)
    {
        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                if (!string.Equals(Normalize(words[patch.Index]) != Normalize(patch.NewWord), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }
        return true;
    }
    public static bool SatisfiesPatches(this string candidate, IEnumerable<WordPatch> patches)
    {
        return patches.VerifyString(candidate);
    }
    public static IEnumerable<WordPatch> GetWordDifferences(this string source, string target)
    {
        var patches = new List<WordPatch>();
        var words1 = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int maxLen = Math.Max(words1.Length, words2.Length);
        int newLen = words2.Length - 1;
        for (int i = 0; i < maxLen; i++)
        {
            string? w1 = i < words1.Length ? WPE.Normalize(words1[i]) : null;
            string? w2 = i < words2.Length ? WPE.Normalize(words2[i]) : null;
            if (i == newLen)
            {
                patches.Add(new WordPatch(i, w2));
            }
            else if (!string.Equals(w1, w2, StringComparison.OrdinalIgnoreCase))
            {
                patches.Add(new WordPatch(i, w2));
            }
        }
        if (patches.Last().NewWord != words2.Last())
        {
            patches.Add(new WordPatch(maxLen - 1, words2.Last()));
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

public class OldVariantData
{
    public ulong splen = 0;
    public string guid = string.Empty;
    public IEnumerable<string>? reg_frags = null;
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
