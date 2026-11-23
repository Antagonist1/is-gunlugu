namespace IsGunlugu.Core;

/// <summary>What the desktop composer collects for a new task, before it is written to the notes.</summary>
public sealed class NewTaskDraft
{
    public sealed class Resolution(DateTime date, string title, string? phrase)
    {
        public DateTime Date { get; } = date;
        public string Title { get; } = title;

        /// <summary>The day words read from the title; null when the day was chosen by hand or none was found.</summary>
        public string? Phrase { get; } = phrase;
    }

    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<string> SubTasks { get; } = new();
    public DateTime? ChosenDay { get; set; }

    public Resolution Resolve(DateTime today, DateTime defaultDay)
    {
        var title = (Title ?? "").Trim();
        if (ChosenDay is { } chosen) return new Resolution(chosen, title, null);

        // A hand-picked day wins, so words like "yarın" stay in the title in that case.
        var found = DateInText.Find(title, today);
        return found is null
            ? new Resolution(defaultDay, title, null)
            : new Resolution(found.Date, found.Text, found.Phrase);
    }
}
