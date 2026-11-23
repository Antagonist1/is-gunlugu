namespace IsGunlugu.Core;

/// <summary>A time typed with a task, "toplantı 14:00" or "9:30 ara": taken out of the title, read from the first line only.</summary>
public static class TimeInText
{
    public sealed class Found(TimeSpan at, string text)
    {
        public TimeSpan At { get; } = at;

        /// <summary>The task without the time.</summary>
        public string Text { get; } = text;
    }

    public static Found? Find(string text)
    {
        var lineEnd = text.IndexOf('\n');
        var first = lineEnd < 0 ? text : text.Substring(0, lineEnd);
        var rest = lineEnd < 0 ? "" : text.Substring(lineEnd);

        var words = first.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        for (var i = 0; i < words.Count; i++)
        {
            if (!TaskTime.TryTime(words[i], out var at)) continue;
            words.RemoveAt(i);
            // A time alone is not a task.
            return words.Count == 0 ? null : new Found(at, string.Join(" ", words.ToArray()) + rest);
        }
        return null;
    }
}
