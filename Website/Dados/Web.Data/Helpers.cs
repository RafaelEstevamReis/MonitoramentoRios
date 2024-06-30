using System;
using System.Security.Cryptography;
using System.Text;

namespace Web.Data;

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
}
