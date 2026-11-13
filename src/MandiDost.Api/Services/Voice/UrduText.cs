using System.Globalization;
using System.Text;

namespace MandiDost.Api.Services.Voice;

/// <summary>
/// Cleans up spoken or typed Urdu so words can be compared: one spelling per letter, no accents,
/// digits as 0-9, punctuation removed. Speech recognisers and keyboards spell the same word in several ways.
/// </summary>
public static class UrduText
{
    private const string Punctuation = "۔،؛؟!?:;()[]{}\"'«»“”‘’-–—_/\\|…*،";

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = input.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(s.Length + 8);

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch is >= '٠' and <= '٩') ch = (char)('0' + (ch - '٠'));       // Arabic-Indic digits
            else if (ch is >= '۰' and <= '۹') ch = (char)('0' + (ch - '۰'));  // Persian/Urdu digits

            // Accents, tatweel and invisible direction marks
            if (ch is >= 'ً' and <= 'ٟ' or 'ٰ' or 'ـ' or (>= '​' and <= '‏') or (>= '‪' and <= '‮') or '﻿') continue;

            ch = ch switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ه' or 'ۃ' or 'ۂ' or 'ە' => 'ہ',
                'ۓ' => 'ے',
                'أ' or 'إ' or 'ٱ' => 'ا',
                _ => ch,
            };

            if (ch == '.' || ch == ',')
            {
                // keep 1.5 and 80,000 as numbers; elsewhere they are just punctuation
                var prevDigit = sb.Length > 0 && char.IsAsciiDigit(sb[^1]);
                var nextDigit = i + 1 < s.Length && (char.IsAsciiDigit(s[i + 1]) || s[i + 1] is (>= '٠' and <= '٩') or (>= '۰' and <= '۹'));
                if (prevDigit && nextDigit) { if (ch == '.') sb.Append('.'); continue; }
                sb.Append(' ');
                continue;
            }

            if (Punctuation.Contains(ch)) { sb.Append(' '); continue; }
            sb.Append(char.IsWhiteSpace(ch) ? ' ' : char.ToLowerInvariant(ch));
        }

        // "80ہزار" and "ہزار80" → "80 ہزار"
        var spaced = new StringBuilder(sb.Length + 8);
        for (var i = 0; i < sb.Length; i++)
        {
            if (i > 0 && sb[i] != ' ' && sb[i - 1] != ' ' && IsDigitOrDot(sb[i]) != IsDigitOrDot(sb[i - 1]) && !(sb[i] == '.' || sb[i - 1] == '.'))
                spaced.Append(' ');
            spaced.Append(sb[i]);
        }
        return string.Join(' ', spaced.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsDigitOrDot(char c) => char.IsAsciiDigit(c) || c == '.';

    public static string[] Tokenize(string? input)
    {
        var tokens = new List<string>();
        foreach (var raw in Normalize(input).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // "دودانت" → "دو" "دانت"
            if (raw.Length > 4 && raw.EndsWith("دانت", StringComparison.Ordinal)) { tokens.Add(raw[..^4]); tokens.Add("دانت"); }
            else tokens.Add(raw);
        }
        return [.. tokens];
    }

    public static bool IsNumber(string token, out double value) =>
        double.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && token.All(c => char.IsAsciiDigit(c) || c == '.');
}
