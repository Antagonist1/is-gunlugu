using System.IO;
using System.Security.Cryptography;
using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>Keeps the Google sign-in encrypted for this Windows user.</summary>
internal sealed class DpapiTokenStore : ITokenStore
{
    private static readonly string FilePath = Path.Combine(Settings.Directory, "google.bin");

    public string? Load()
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    public void Save(string json)
    {
        Directory.CreateDirectory(Settings.Directory);
        File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));
    }

    public void Clear()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
