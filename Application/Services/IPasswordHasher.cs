namespace JwtAuthApi_Sonnet45.Application.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}