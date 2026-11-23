using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace IsGunlugu;

public enum ThemeChoice { System, Light, Dark }

/// <summary>A named set of note files in the sidebar. The files themselves stay where they are.</summary>
[DataContract]
public sealed class FileGroup
{
    [DataMember]
    public string Name { get; set; } = "";

    [DataMember]
    public List<string> Paths { get; set; } = new();

    [DataMember]
    public bool IsCollapsed { get; set; }
}

[DataContract]
public sealed class Settings
{
    public static readonly string Directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsGunlugu");

    public const string CarriedSection = "kalan";

    private static readonly string FilePath = Path.Combine(Directory, "settings.json");

    /// <summary>The file that was open last.</summary>
    [DataMember]
    public string? NotesPath { get; set; }

    [DataMember]
    public List<string> NotesPaths { get; set; } = new();

    /// <summary>The width the right panel was dragged to; empty is the usual 340.</summary>
    [DataMember]
    public double? DetailWidth { get; set; }

    /// <summary>The file the Win+Shift+N box added to last; it goes there again until another is picked.</summary>
    [DataMember]
    public string? QuickAddPath { get; set; }

    [DataMember]
    public ThemeChoice Theme { get; set; }

    /// <summary>Null in settings written before this option existed, which means on.</summary>
    [DataMember]
    public bool? CloseToTray { get; set; }

    /// <summary>Written by older versions; read once into <see cref="CollapsedSections"/>.</summary>
    [DataMember]
    public bool CarryOverCollapsed { get; set; }

    /// <summary>The closed headings of the Tümü screen.</summary>
    [DataMember]
    public List<string> CollapsedSections { get; set; } = new();

    [DataMember]
    public bool TaskGroupCollapsed { get; set; }

    [DataMember]
    public List<FileGroup> FileGroups { get; set; } = new();

    [DataMember]
    public bool GuideShown { get; set; }

    /// <summary>The tour's parts seen (finished or passed), by name; each invites only until then.</summary>
    [DataMember]
    public List<string>? SeenTours { get; set; }

    /// <summary>The small tour invitations. Null in settings written before they existed, which means on.</summary>
    [DataMember]
    public bool? TourInvites { get; set; }

    /// <summary>The sidebar as it was before the two-tier one: chosen in Settings, false means the new one.</summary>
    [DataMember]
    public bool ClassicSidebar { get; set; }

    /// <summary>Tasks written as plain text ("- " sub-tasks) instead of the visual box: chosen in Settings, false means visual.</summary>
    [DataMember]
    public bool TextWriting { get; set; }

    /// <summary>Where the window was left, in window units: left, top, width, height. Null until it is first closed.</summary>
    [DataMember]
    public double[]? WindowPlace { get; set; }

    [DataMember]
    public bool WindowMaximized { get; set; }

    /// <summary>The evening reminder from the tray. Null in settings written before it existed, which means on.</summary>
    [DataMember]
    public bool? DayEndReminder { get; set; }

    /// <summary>Tasks whose sub-tasks are folded in the list, by title, so the fold follows a task to another day.</summary>
    [DataMember]
    public List<string>? CollapsedTasks { get; set; }

    /// <summary>Tasks whose sub-tasks were opened by hand, by title; every other task starts with them folded.</summary>
    [DataMember]
    public List<string>? ExpandedTasks { get; set; }

    /// <summary>"17:30"; null means the default time.</summary>
    [DataMember]
    public string? DayEndTime { get; set; }

    /// <summary>Reminders before a task's time. Null in settings written before they existed, which means on.</summary>
    [DataMember]
    public bool? TimedReminders { get; set; }

    /// <summary>What a task newly given a time gets, as in the file: "30dk 15dk". Null means that default.</summary>
    [DataMember]
    public string? ReminderDefaults { get; set; }

    /// <summary>Reminders already shown on this computer, so each comes once; a week is kept.</summary>
    [DataMember]
    public List<string>? RungReminders { get; set; }

    /// <summary>The last day the reminder was shown, "2026-11-12", so it comes once a day across restarts.</summary>
    [DataMember]
    public string? DayEndShownOn { get; set; }

    /// <summary>Week, Month or Year: the size the calendar page was last left at.</summary>
    [DataMember]
    public string? CalendarScale { get; set; }

    [DataMember]
    public bool TrayHintShown { get; set; }

    /// <summary>Older versions only sorted a day on screen; this is set once the files themselves are in that order.</summary>
    [DataMember]
    public bool DaysSortedInFiles { get; set; }

    /// <summary>Files that could not be sorted yet. Kept by name, so an order set by hand in the others is never sorted again.</summary>
    [DataMember]
    public List<string>? FilesToSort { get; set; }

    public static Settings Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            var settings = (Settings?)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream) ?? new Settings();

            // The serializer skips the initializer above, and older settings held a single file.
            settings.NotesPaths ??= new List<string>();
            settings.CollapsedSections ??= new List<string>();
            settings.FileGroups ??= new List<FileGroup>();
            if (settings.CarryOverCollapsed)
            {
                settings.CarryOverCollapsed = false;
                settings.CollapsedSections.Add(CarriedSection);
            }
            if (settings.NotesPaths.Count == 0 && settings.NotesPath is not null) settings.NotesPaths.Add(settings.NotesPath);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SerializationException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        using var stream = File.Create(FilePath);
        new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, this);
    }
}
