using System.ComponentModel.DataAnnotations;

namespace MandiDost.Api.Common;

/// <summary>Pakistani national ID card number (CNIC): 13 digits, often written 35202-1234567-1.</summary>
public static class Cnic
{
    /// <summary>Returns the 13 digits without dashes or spaces, or null when it is not a valid CNIC.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (input.Any(c => !char.IsAsciiDigit(c) && c is not '-' and not ' ')) return null;
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        // The first digit is the region code; no real CNIC starts with 0.
        return digits.Length == 13 && digits[0] != '0' ? digits : null;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PakistaniCnicAttribute : ValidationAttribute
{
    public PakistaniCnicAttribute() : base("Enter a valid CNIC: 13 digits, e.g. 35202-1234567-1.") { }

    public override bool IsValid(object? value) =>
        value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || Cnic.Normalize(s) is not null));
}
