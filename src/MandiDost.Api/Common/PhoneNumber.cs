using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MandiDost.Api.Common;

/// <summary>Pakistani mobile numbers. Accepts 03001234567, 3001234567, 923001234567, +92 300-1234567.</summary>
public static partial class PhoneNumber
{
    [GeneratedRegex(@"^\+923\d{9}$")]
    private static partial Regex E164Pk();

    /// <summary>Returns the number as +923XXXXXXXXX, or null when it is not a valid PK mobile.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        var normalized = digits switch
        {
            { Length: 11 } when digits.StartsWith("03") => "+92" + digits[1..],
            { Length: 10 } when digits.StartsWith('3') => "+92" + digits,
            { Length: 12 } when digits.StartsWith("923") => "+" + digits,
            { Length: 14 } when digits.StartsWith("00923") => "+" + digits[2..],
            _ => null,
        };
        return normalized is not null && E164Pk().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>Display form used across Pakistan: 0300-1234567.</summary>
    public static string ToLocal(string e164) => e164.StartsWith("+92") && e164.Length == 13 ? $"0{e164[3..6]}-{e164[6..]}" : e164;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PakistaniMobileAttribute : ValidationAttribute
{
    public PakistaniMobileAttribute() : base("Enter a valid Pakistani mobile number, e.g. 03001234567.") { }

    public override bool IsValid(object? value) => value is null || (value is string s && PhoneNumber.Normalize(s) is not null);
}
