namespace IsGunlugu;

/// <summary>
/// This program's identity at Google. The id is public (it shows in the sign-in page anyway). The secret lives in
/// GoogleClient.Secret.cs, kept out of the source repository: Google treats a secret found in a public repository as
/// leaked and may switch it off, which would stop every copy from syncing. A copy built without that file still runs;
/// only connecting Drive says it cannot.
/// </summary>
internal static partial class GoogleClient
{
    public const string Id = "87096890473-5n6r4oao7m4pfgjsc4oqmfk23gbb6ous.apps.googleusercontent.com";

    public static string Secret
    {
        get
        {
            var secret = "";
            LoadSecret(ref secret);
            return secret;
        }
    }

    /// <summary>False in a build made without GoogleClient.Secret.cs.</summary>
    public static bool HasSecret => Secret.Length > 0;

    /// <summary>Filled in by GoogleClient.Secret.cs (see GoogleClient.Secret.example.cs).</summary>
    static partial void LoadSecret(ref string secret);
}
