using System.Text;

namespace IsGunlugu.Core;

public static class TextSearch
{
    /// <summary>Case and Turkish letter insensitive: "sifre" finds "Şifre", "isik" finds "IŞIK".</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                _ => char.ToLowerInvariant(c),
            });
        }
        return builder.ToString();
    }

    public static bool Matches(Entry entry, string foldedQuery) =>
        Fold(entry.Text).IndexOf(foldedQuery, StringComparison.Ordinal) >= 0
        || entry.Parts.Any(part => Fold(part.Task?.Text ?? part.Line).IndexOf(foldedQuery, StringComparison.Ordinal) >= 0);
}
