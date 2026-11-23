using System.IO;
using System.Net;

namespace IsGunlugu.Core;

/// <summary>
/// Plain HTTPS, so the same code runs on .NET Framework 4.8 and on the phone. Built for 4.8, the connection uses
/// whatever TLS versions Windows allows, so nothing is pinned here.
/// </summary>
public static class GoogleHttp
{
#pragma warning disable SYSLIB0014 // The same WebRequest code serves both platforms; HttpClient would need two.
    public static HttpResult Send(string method, string url, string? bearer, byte[]? body, string? contentType)
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = 60000;
            if (bearer != null) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearer;
            if (contentType != null) request.ContentType = contentType;
            if (body != null)
            {
                request.ContentLength = body.Length;
                using var output = request.GetRequestStream();
                output.Write(body, 0, body.Length);
            }

            using var response = (HttpWebResponse)request.GetResponse();
            return new HttpResult((int)response.StatusCode, ReadAll(response));
        }
        catch (WebException ex) when (ex.Response is HttpWebResponse response)
        {
            using (response) return new HttpResult((int)response.StatusCode, ReadAll(response));
        }
        catch (Exception ex) when (ex is WebException or IOException or NotSupportedException)
        {
            throw new SyncNetworkException("Google Drive'a ulaşılamadı: " + ex.Message, ex);
        }
    }
#pragma warning restore SYSLIB0014

    private static byte[] ReadAll(WebResponse response)
    {
        using var input = response.GetResponseStream();
        using var copy = new MemoryStream();
        if (input != null) input.CopyTo(copy);
        return copy.ToArray();
    }
}
