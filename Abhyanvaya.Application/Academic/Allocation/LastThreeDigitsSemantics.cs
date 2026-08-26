using System.Globalization;
using System.Text;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1D.24B.4 — Shared last-three-digit semantics for population filters and roll-number band placement.
/// Numeric 000–999 only; never full StudentNumber ordinal compare.
/// </summary>
public static class LastThreeDigitsSemantics
{
    public const int MinValue = 0;
    public const int MaxValue = 999;

    /// <summary>
    /// Parse a population bound ("046", "46", "0") into 0–999 and normalize to D3 ("046").
    /// Rejects non-digit, empty, and out-of-range values.
    /// </summary>
    public static bool TryParseBound(string? raw, out int value, out string normalized, out string? error)
    {
        value = 0;
        normalized = "";
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Last 3 Digits range requires both From and To (000–999).";
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0 || !trimmed.All(char.IsDigit))
        {
            error = $"Invalid Last 3 Digits value '{raw}'. Use digits only (000–999).";
            return false;
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid Last 3 Digits value '{raw}'.";
            return false;
        }

        if (value < MinValue || value > MaxValue)
        {
            error = $"Last 3 Digits value '{raw}' must be between 000 and 999.";
            return false;
        }

        normalized = value.ToString("D3", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Extract trailing numeric last-three digits from a student number (digit characters only).</summary>
    public static bool TryExtractLastThree(string? studentNumber, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(studentNumber))
            return false;

        var digits = new StringBuilder(studentNumber.Length);
        foreach (var ch in studentNumber)
        {
            if (char.IsDigit(ch))
                digits.Append(ch);
        }

        if (digits.Length == 0)
            return false;

        var all = digits.ToString();
        var last = all.Length <= 3 ? all : all[^3..];
        if (!int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;

        return value is >= MinValue and <= MaxValue;
    }

    /// <summary>
    /// Band index for college-style roll banding: 001–060 → 0 when bandSize=60; 061–120 → 1; 000 → 0.
    /// </summary>
    public static int BandIndex(int lastThree, int bandSize)
    {
        if (bandSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bandSize), "Band size must be positive.");
        if (lastThree <= 0)
            return 0;
        return (lastThree - 1) / bandSize;
    }

    public static string FormatD3(int value)
        => Math.Clamp(value, MinValue, MaxValue).ToString("D3", CultureInfo.InvariantCulture);
}
