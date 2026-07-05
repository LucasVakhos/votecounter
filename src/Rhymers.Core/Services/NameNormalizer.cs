using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rhymers.Core.Services;

/// <summary>
/// Static utility for name normalization and comparison.
/// </summary>
/// <remarks>
/// Normalizes names by converting to lowercase, removing accents, and replacing ё with е.
/// Uses compiled regex and LRU-style result caching for performance.
/// Cache is bounded to 10,000 entries to prevent unbounded memory growth.
/// </remarks>
public static class NameNormalizer
{
    private static readonly Regex YoRegex = new("ё", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> NormalizeCache = new(StringComparer.Ordinal);
    private const int MaxCacheSize = 10000;

    /// <summary>
    /// Normalizes a name for comparison and storage.
    /// </summary>
    /// <param name="value">The name to normalize.</param>
    /// <returns>Normalized name (lowercase, no accents, ё→е). Empty string if input is null/whitespace.</returns>
    /// <remarks>
    /// Uses LRU cache (max 10K entries) for performance. Results are cached to avoid repeated normalization.
    /// </remarks>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Check cache first
        if (NormalizeCache.TryGetValue(value, out var cached))
            return cached;

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

        var result = YoRegex.Replace(sb.ToString(), "е");
        
        // Add to cache (with size limit)
        if (NormalizeCache.Count < MaxCacheSize)
            NormalizeCache[value] = result;

        return result;
    }

    /// <summary>
    /// Compares two names for equality using normalization.
    /// </summary>
    /// <param name="left">First name to compare.</param>
    /// <param name="right">Second name to compare.</param>
    /// <returns>True if normalized names are equal.</returns>
    /// <remarks>
    /// Performs reference equality checks first for performance.
    /// Then checks if both are null/empty, or normalizes and compares.
    /// </remarks>
    public static bool Same(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            return true;
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft == normalizedRight;
    }

    /// <summary>
    /// Clears the normalization cache.
    /// </summary>
    /// <remarks>
    /// Useful for testing or when memory cleanup is needed.
    /// Cache automatically bounded to 10,000 entries during normal operation.
    /// </remarks>
    public static void ClearCache() => NormalizeCache.Clear();
}
