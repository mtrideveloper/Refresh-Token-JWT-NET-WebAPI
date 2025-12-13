using System.Security.Cryptography;

namespace JwtAuthApi_Sonnet45.Utils;

public static class IDGenerator
{
    public static string GenerateUniqueId(Type type)
    {
        var typeName = type.Name.ToUpper();
        var number = RandomNumberGenerator.GetInt32(0, 10000);
        var randomPart = number.ToString("D5");
        return $"{typeName}_{randomPart}";
    }
}