using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>Opens Google's sign-in in the browser and catches its answer on a local port.</summary>
internal static class GoogleLoopback
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(5);

    public static void SignIn(GoogleAuth auth)
    {
        // TcpListener rather than HttpListener, which needs administrator rights.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            var verifier = GoogleAuth.NewVerifier();
            var state = GoogleAuth.NewVerifier();
            Process.Start(auth.BuildAuthUrl(redirect, GoogleAuth.Challenge(verifier), state));

            var started = DateTime.UtcNow;
            while (true)
            {
                while (!listener.Pending())
                {
                    if (DateTime.UtcNow - started > Patience) throw new SyncNetworkException("Google girişi zaman aşımına uğradı.");
                    Thread.Sleep(200);
                }

                var query = ReadQuery(listener);
                if (!query.TryGetValue("state", out var returned) || returned != state) continue; // Not the browser's answer.
                if (!query.TryGetValue("code", out var code)) throw new SyncNetworkException("Google girişi iptal edildi.");

                auth.ExchangeCode(code, verifier, redirect);
                return;
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Dictionary<string, string> ReadQuery(TcpListener listener)
    {
        var result = new Dictionary<string, string>();
        using var client = listener.AcceptTcpClient();
        client.ReceiveTimeout = 2000; // Browsers open spare connections and send nothing on them.
        client.SendTimeout = 2000;
        using var stream = client.GetStream();

        string[] parts;
        try
        {
            parts = (new StreamReader(stream, Encoding.ASCII).ReadLine() ?? "").Split(' ');
            var page = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><body style='font-family:sans-serif;padding:3em'>İş Günlüğü'ne dönebilirsin, bu sekmeyi kapatabilirsin.");
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {page.Length}\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(page, 0, page.Length);
        }
        catch (IOException)
        {
            return result;
        }

        var cut = parts.Length > 1 ? parts[1].IndexOf('?') : -1;
        if (cut < 0) return result;

        foreach (var pair in parts[1].Substring(cut + 1).Split('&'))
        {
            var split = pair.IndexOf('=');
            if (split > 0) result[pair.Substring(0, split)] = Uri.UnescapeDataString(pair.Substring(split + 1));
        }
        return result;
    }
}
