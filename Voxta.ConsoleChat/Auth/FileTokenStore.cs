using System.Security.Cryptography;
using System.Text;

namespace Voxta.ConsoleChat.Auth;

public class FileTokenStore : ITokenStore
{
    private static readonly string Directory_ = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voxta.ConsoleChat");
    private static readonly string FilePath = Path.Combine(Directory_, "token.dat");

    public void Save(string token)
    {
#if WINDOWS
        Directory.CreateDirectory(Directory_);
        var protectedToken = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedToken);
#endif
    }

    public string? Load()
    {
#if WINDOWS
        if (!File.Exists(FilePath))
            return null;
        var protectedToken = File.ReadAllBytes(FilePath);
        var token = ProtectedData.Unprotect(protectedToken, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(token);
#else
        return null;
#endif
    }
}
