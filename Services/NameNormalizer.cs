using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoteCounter.Services;

public static class NameNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var form = value.Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var sb = new StringBuilder(form.Length);

        foreach (var ch in form)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.OtherSymbol or UnicodeCategory.Surrogate)
                continue;

            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return Regex.Replace(sb.ToString(), "ё", "е");
    }

    public static bool Same(string? left, string? right)
        => Normalize(left) == Normalize(right);
}
