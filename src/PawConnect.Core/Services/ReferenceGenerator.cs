using System.Security.Cryptography;
using PawConnect.Core.Common;

namespace PawConnect.Core.Services;

/// <summary>Creates short, unambiguous application references such as PC-260926-7KQ4.</summary>
public static class ReferenceGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewApplicationReference(DateTime utcNow)
    {
        Span<char> suffix = stackalloc char[4];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"PC-{ShelterTime.ToLocal(utcNow):yyMMdd}-{new string(suffix)}";
    }
}
