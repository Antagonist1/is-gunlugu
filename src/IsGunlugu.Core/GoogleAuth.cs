using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace IsGunlugu.Core;

/// <summary>Signs in with PKCE and keeps the access token fresh. Showing the browser is the app's job.</summary>
public sealed class GoogleAuth(string clientId, string? clientSecret, ITokenStore tokens, HttpSend? send = null, Func<DateTime>? utcNow = null)
{
    private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string Scope = "https://www.googleapis.com/auth/drive.file";

    private readonly HttpSend _send = send ?? GoogleHttp.Send;
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);

    [DataContract]
    private sealed class TokenResponse
    {
        [DataMember(Name = "access_token")] public string? AccessToken { get; set; }
        [DataMember(Name = "refresh_token")] public string? RefreshToken { get; set; }
        [DataMember(Name = "expires_in")] public int ExpiresIn { get; set; }
    }

    [DataContract]
    private sealed class Stored
    {
        [DataMember] public string? Access { get; set; }
        [DataMember] public string? Refresh { get; set; }
        [DataMember] public long ExpiresUtcTicks { get; set; }
    }

    public bool IsConnected => Load()?.Refresh != null;

    public string BuildAuthUrl(string redirectUri, string codeChallenge, string state) =>
        AuthUrl + "?" + Form(
            "client_id", clientId,
            "redirect_uri", redirectUri,
            "response_type", "code",
            "scope", Scope,
            "code_challenge", codeChallenge,
            "code_challenge_method", "S256",
            "access_type", "offline",
            "prompt", "consent",
            "state", state);

    public void ExchangeCode(string code, string verifier, string redirectUri)
    {
        var fields = new List<string>
        {
            "grant_type", "authorization_code",
            "code", code,
            "code_verifier", verifier,
            "redirect_uri", redirectUri,
            "client_id", clientId,
        };
        if (clientSecret != null) fields.AddRange(new[] { "client_secret", clientSecret });
        Remember(Post(fields.ToArray()), null);
    }

    public string AccessToken()
    {
        var stored = Load();
        if (stored?.Refresh == null) throw new SyncAuthException("Google hesabı bağlı değil.");
        if (stored.Access != null && _utcNow().AddMinutes(1).Ticks < stored.ExpiresUtcTicks) return stored.Access;

        var fields = new List<string>
        {
            "grant_type", "refresh_token",
            "refresh_token", stored.Refresh,
            "client_id", clientId,
        };
        if (clientSecret != null) fields.AddRange(new[] { "client_secret", clientSecret });
        return Remember(Post(fields.ToArray()), stored.Refresh);
    }

    public void Disconnect() => tokens.Clear();

    public static string NewVerifier()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        return Base64Url(bytes);
    }

    public static string Challenge(string verifier)
    {
        using var sha = SHA256.Create();
        return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Alternating names and values.</summary>
    private static string Form(params string[] fields) =>
        string.Join("&", Enumerable.Range(0, fields.Length / 2).Select(i => fields[2 * i] + "=" + Uri.EscapeDataString(fields[2 * i + 1])));

    private TokenResponse Post(string[] fields)
    {
        var result = _send("POST", TokenUrl, null, Encoding.UTF8.GetBytes(Form(fields)), "application/x-www-form-urlencoded");
        if (result.Status == 200) return Json<TokenResponse>(result.Body);

        if (Encoding.UTF8.GetString(result.Body).Contains("invalid_grant"))
        {
            Disconnect();
            throw new SyncAuthException("Google erişimi sona erdi. Hesabı yeniden bağla.");
        }
        throw new SyncNetworkException($"Google girişi başarısız oldu ({result.Status}).");
    }

    private string Remember(TokenResponse response, string? previousRefresh)
    {
        var stored = new Stored
        {
            Access = response.AccessToken,
            Refresh = response.RefreshToken ?? previousRefresh,
            ExpiresUtcTicks = _utcNow().AddSeconds(response.ExpiresIn).Ticks,
        };

        var serializer = new DataContractJsonSerializer(typeof(Stored));
        using var buffer = new MemoryStream();
        serializer.WriteObject(buffer, stored);
        tokens.Save(Encoding.UTF8.GetString(buffer.ToArray()));
        return stored.Access ?? throw new SyncNetworkException("Google yanıtında erişim anahtarı yok.");
    }

    private Stored? Load()
    {
        var json = tokens.Load();
        return json == null ? null : Json<Stored>(Encoding.UTF8.GetBytes(json));
    }

    private static T Json<T>(byte[] body) where T : class
    {
        using var stream = new MemoryStream(body);
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream)!;
    }
}
