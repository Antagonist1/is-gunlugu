namespace IsGunlugu.Core;

/// <summary>Google no longer accepts this sign-in: the account has to be connected again.</summary>
public sealed class SyncAuthException(string message) : Exception(message);

/// <summary>Drive could not be reached or refused for a reason that may pass.</summary>
public sealed class SyncNetworkException(string message, Exception? inner = null) : Exception(message, inner);

public interface ITokenStore
{
    string? Load();
    void Save(string json);
    void Clear();
}

public sealed class HttpResult(int status, byte[] body)
{
    public int Status { get; } = status;
    public byte[] Body { get; } = body;
}

public delegate HttpResult HttpSend(string method, string url, string? bearer, byte[]? body, string? contentType);
