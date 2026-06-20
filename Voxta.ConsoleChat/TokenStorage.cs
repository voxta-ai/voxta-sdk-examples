using System.Security.Cryptography;
using System.Text;

namespace Voxta.ConsoleChat;

public interface ITokenStorage
{
    void SaveToken(string token);
    string? RetrieveToken();
}

public class TokenStorage : ITokenStorage
{
    private static readonly string _appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voxta.ConsoleChat") ;
    private static readonly string _tokenFilePath = Path.Combine(_appDataDirectory, "token.dat") ;

    public void SaveToken(string token)
    {
        #if(WINDOWS)
        Directory.CreateDirectory(_appDataDirectory);

        var encryptedToken = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(token),
            null,
            DataProtectionScope.CurrentUser);

        File.WriteAllBytes(_tokenFilePath, encryptedToken);
        #endif
    }

    public string? RetrieveToken()
    {
        #if(WINDOWS)
        if (!File.Exists(_tokenFilePath))
            return null;

        var encryptedToken = File.ReadAllBytes(_tokenFilePath);

        var decryptedToken = ProtectedData.Unprotect(
            encryptedToken,
            null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(decryptedToken);
        #else
        return null;
        #endif
    }
}