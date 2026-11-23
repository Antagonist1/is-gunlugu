namespace IsGunlugu.Core;

public enum FirstRunStep { Welcome, FirstFile, Drive, Source, Fetch }

/// <summary>Where the person is in the first-run screens, and the way back from each one.</summary>
public sealed class FirstRunFlow
{
    public FirstRunStep Step { get; private set; } = FirstRunStep.Welcome;

    public bool CanGoBack => Step != FirstRunStep.Welcome;

    /// <summary>Came with notes from before, so the tour would only be in the way.</summary>
    public bool IsReturning => Step is FirstRunStep.Source or FirstRunStep.Fetch;

    public void QuickStart() => Step = FirstRunStep.FirstFile;

    public void FileChosen() => Step = FirstRunStep.Drive;

    public void Returning() => Step = FirstRunStep.Source;

    public void FetchFromDrive() => Step = FirstRunStep.Fetch;

    /// <summary>Drive had nothing, so this person starts like a new one after all.</summary>
    public void StartFresh() => Step = FirstRunStep.FirstFile;

    public void Back() => Step = Step switch
    {
        FirstRunStep.Drive => FirstRunStep.FirstFile,
        FirstRunStep.Fetch => FirstRunStep.Source,
        _ => FirstRunStep.Welcome,
    };
}
