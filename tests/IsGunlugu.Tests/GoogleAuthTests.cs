using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class GoogleAuthTests
{
    private sealed class MemoryTokens : ITokenStore
    {
        public string? Json;
        public string? Load() => Json;
        public void Save(string json) => Json = json;
        public void Clear() => Json = null;
    }

    private readonly MemoryTokens _tokens = new();
    private readonly List<string> _bodies = new();
    private DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private Func<HttpResult> _reply = () => Ok("""{"access_token":"a1","refresh_token":"r1","expires_in":3600}""");

    private static HttpResult Ok(string json) => new(200, Encoding.UTF8.GetBytes(json));

    private GoogleAuth Auth() => new("client", null, _tokens, (_, _, _, body, _) =>
    {
        _bodies.Add(Encoding.UTF8.GetString(body ?? new byte[0]));
        return _reply();
    }, () => _now);

    [Fact]
    public void AccessToken_IsReusedWhileValid()
    {
        var auth = Auth();
        auth.ExchangeCode("code", "verifier", "http://127.0.0.1:1/");
        _reply = () => throw new InvalidOperationException("Geçerli belirteç için istek gitmemeli");

        Assert.Equal("a1", auth.AccessToken());
    }

    [Fact]
    public void AccessToken_RefreshesWhenExpired()
    {
        var auth = Auth();
        auth.ExchangeCode("code", "verifier", "http://127.0.0.1:1/");
        _now = _now.AddHours(2);
        _reply = () => Ok("""{"access_token":"a2","expires_in":3600}""");

        Assert.Equal("a2", auth.AccessToken());
        Assert.Contains("grant_type=refresh_token", _bodies.Last());
        Assert.Contains("refresh_token=r1", _bodies.Last());
    }

    [Fact]
    public void Refresh_KeepsRefreshTokenWhenGoogleOmitsIt()
    {
        var auth = Auth();
        auth.ExchangeCode("code", "verifier", "http://127.0.0.1:1/");
        _now = _now.AddHours(2);
        _reply = () => Ok("""{"access_token":"a2","expires_in":3600}""");
        auth.AccessToken();
        _now = _now.AddHours(2);
        _reply = () => Ok("""{"access_token":"a3","expires_in":3600}""");

        Assert.Equal("a3", Auth().AccessToken());
        Assert.Contains("refresh_token=r1", _bodies.Last());
    }

    [Fact]
    public void InvalidGrant_ClearsTokensAndThrowsSyncAuthException()
    {
        var auth = Auth();
        auth.ExchangeCode("code", "verifier", "http://127.0.0.1:1/");
        _now = _now.AddHours(2);
        _reply = () => new HttpResult(400, Encoding.UTF8.GetBytes("""{"error":"invalid_grant"}"""));

        Assert.Throws<SyncAuthException>(() => auth.AccessToken());
        Assert.Null(_tokens.Json);
        Assert.False(auth.IsConnected);
    }

    [Fact]
    public void NotConnected_ThrowsSyncAuthException()
    {
        Assert.Throws<SyncAuthException>(() => Auth().AccessToken());
    }

    [Fact]
    public void AuthUrl_AsksForDriveFileWithPkce()
    {
        var url = Auth().BuildAuthUrl("http://127.0.0.1:1/", "challenge", "state");

        Assert.Contains("drive.file", url);
        Assert.Contains("code_challenge=challenge", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("access_type=offline", url);
    }

    [Fact]
    public void Challenge_IsUrlSafeSha256OfVerifier()
    {
        // The example from RFC 7636, appendix B.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", GoogleAuth.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }
}
