using System.Globalization;

namespace WindowManager.Windowing;

public static class WindowSizePercentage
{
    public const string InvalidInputMessage = "Enter a whole number from 0 to 100.";

    public static bool TryParse(string? input, out int percentage)
    {
        // Accept surrounding whitespace, but not signs, decimal points, percent signs, or exponents.
        if (input?.Trim() is { Length: > 0 } text && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0 and <= 100)
        {
            percentage = parsed;
            return true;
        }

        percentage = default;
        return false;
    }
}
