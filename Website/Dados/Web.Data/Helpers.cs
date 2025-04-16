namespace Web.Data;

using System;
using System.Security.Cryptography;
using System.Text;

public static class Helpers
{
    public static byte[] HashString(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Hash(bytes);
    }
    public static byte[] Hash(byte[] input)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(input);
    }
    public static string ToHex(byte[] input)
    {
        return Convert.ToHexString(input).Replace("-", "");
    }

    public static string ApiToEstacao(string apiKey)
    {
        var hash = HashString(apiKey);
        var hex = ToHex(hash);
        return hex.Substring(0, 16);
    }

    public static decimal? Round(this decimal? value, int decimals)
    {
        if (value == null) return null;
        return Math.Round(value.Value, decimals);
    }

    public static decimal ToDecimal(this string value) => decimal.Parse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
}
