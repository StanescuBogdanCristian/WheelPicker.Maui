namespace SBC.WheelPicker.Helpers;

internal static class StringHelper
{
    /// <summary>
	/// Normalizes a string format so it can be safely used with <see cref="string.Format(IFormatProvider,string,object?)"/>.
	/// </summary>
	/// <remarks>
	/// Accepts either a full composite format string (e.g. <c>"Value: {0:D2}"</c>)
	/// or a format specifier (e.g. <c>"D2"</c>, treated as <c>"{0:D2}"</c>).
	/// </remarks>
	public static string? NormalizeStringFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return null;

        format = format.Trim();

        // XAML "escape" prefix. In C# it usually won't appear, but it can if copied from XAML.
        if (format.StartsWith("{}", StringComparison.Ordinal))
            format = format.Substring(2);

        // If it already contains a placeholder, assume it's a composite format string.
        if (format.Contains("{0", StringComparison.Ordinal))
            return format;

        // Treat as a raw specifier.
        return string.Concat("{0:", format, "}");
    }
}
