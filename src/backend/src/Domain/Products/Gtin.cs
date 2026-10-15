using System.Globalization;
using Domain.Shared;

namespace Domain.Products;

/// <summary>
/// A barcode in the one form every scanner's spelling of it shares: digits only, with a correct check digit.
/// </summary>
/// <remarks>
/// EAN-13 and GTIN-14 stay as they are, a UPC-A is padded with a zero to 13 (it is the same product as that EAN-13),
/// and a UPC-E is expanded to its UPC-A and padded the same, so the symbology a phone read does not decide which
/// household row it meets. An EAN-8 stays 8 digits. Eight digits are an EAN-8 whenever their check digit fits one;
/// a UPC-E with a sixth digit of 5 to 9 fits both, and the digits alone cannot say which it was, so it stays as read.
/// </remarks>
public sealed record Gtin
{
    private Gtin(string value) => Value = value;

    /// <summary>The normalised digits: 8, 13 or 14.</summary>
    public string Value { get; }

    /// <summary>Reads a barcode, or says why it is not one.</summary>
    /// <param name="code">What a scanner read or a person typed.</param>
    public static Result<Gtin> Create(string? code)
    {
        var digits = code?.Trim();

        if (string.IsNullOrEmpty(digits) || !digits.All(char.IsAsciiDigit))
        {
            return ProductErrors.InvalidGtin;
        }

        return digits.Length switch
        {
            8 => Eight(digits),
            12 when HasCheckDigit(digits) => new Gtin("0" + digits),
            13 or 14 when HasCheckDigit(digits) => new Gtin(digits),
            _ => ProductErrors.InvalidGtin
        };
    }

    /// <summary>The digits.</summary>
    public override string ToString() => Value;

    private static Result<Gtin> Eight(string digits)
    {
        if (HasCheckDigit(digits))
        {
            return new Gtin(digits);
        }

        // A UPC-E's check digit is the one of the UPC-A it stands for, so it fails the EAN-8 sum above.
        var expanded = digits[0] is '0' or '1' ? ExpandUpcE(digits) : null;

        return expanded is not null && HasCheckDigit(expanded)
            ? new Gtin("0" + expanded)
            : ProductErrors.InvalidGtin;
    }

    // The digits weigh 3, 1, 3, 1 from the right, the check digit last and weighing 1; a valid code sums to a multiple of ten.
    private static bool HasCheckDigit(string digits)
    {
        var sum = 0;

        for (var index = 0; index < digits.Length; index++)
        {
            var weight = (digits.Length - 1 - index) % 2 == 0 ? 1 : 3;

            sum += (digits[index] - '0') * weight;
        }

        return sum % 10 == 0;
    }

    // number system, six digits, check digit: where the six go in the twelve depends on the last of them.
    private static string ExpandUpcE(string upcE)
    {
        var (system, d, check) = (upcE[0], upcE[1..7], upcE[7]);

        var body = d[5] switch
        {
            '0' or '1' or '2' => $"{d[0]}{d[1]}{d[5]}0000{d[2]}{d[3]}{d[4]}",
            '3' => $"{d[0]}{d[1]}{d[2]}00000{d[3]}{d[4]}",
            '4' => $"{d[0]}{d[1]}{d[2]}{d[3]}00000{d[4]}",
            _ => $"{d[0]}{d[1]}{d[2]}{d[3]}{d[4]}0000{d[5]}"
        };

        return string.Create(CultureInfo.InvariantCulture, $"{system}{body}{check}");
    }
}
