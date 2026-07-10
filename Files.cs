using Noggog;

namespace SynPatcher;

public static class WPE
{
    static bool IsValidChar(char x) => Char.IsAsciiLetterOrDigit(x) || x == '-' || x == '=' || x == '$' || x == '<' || x == '>' || x == ' ' || x == '.' || x == ',' || x == '?' || x == '!' || x == '\"' || x == '\'' || x == '*' || x == '[' || x == ']' || x == '(' || x == ')';
    public static string CleanString(this string? str) => new string([.. REG.HiddenFN2.Replace(REG.HiddenFN.Replace(str ?? string.Empty, ""), "").Where(IsValidChar)]).Trim().TrimEnd(',').Replace("\n", " ").Replace("\r", " ").Replace("\t", " ").Replace("  ", " ").TrimEnd(' ');
    public static string RemovePunct(this string? str) => str?.Replace(".", "")?.Replace("!", "")?.Replace("?", "")?.Replace(",", "")?.Replace("\"", "") ?? string.Empty;
    public static bool DoSkip(this string? str) => str.CleanString().RemovePunct().Replace("-", "").Replace(".", "").Trim().IsNullOrEmpty();
}

public struct LineData
{
    public string guid;
}

public struct VariantData
{
    public string guid;
    public ulong frag_hash;
}
