using System.Windows.Input;

namespace IsGunlugu;

/// <summary>
/// The tour in parts: each screen invites its own few steps the first time it is shown (top right, "✦ Takvimi tanı ·
/// 3 adım"). The window knows the parts and when each applies; this keeps which were seen and whether to invite.
/// </summary>
public sealed partial class MainViewModel
{
    private string _tourInviteText = "";

    public ICommand ResetToursCommand => new RelayCommand(ResetTours);

    /// <summary>Settings › Yardım › Tanıtım davetleri.</summary>
    public bool TourInvitesOn
    {
        get => _settings.TourInvites ?? true;
        set
        {
            _settings.TourInvites = value;
            if (!_pathsFromCommandLine) _settings.Save();
            Notify();
            ToursChanged?.Invoke();
        }
    }

    /// <summary>"Takvimi tanı · 3 adım", or empty when nothing invites.</summary>
    public string TourInviteText
    {
        get => _tourInviteText;
        set
        {
            if (_tourInviteText == value) return;
            _tourInviteText = value ?? "";
            Notify();
            Notify(nameof(HasTourInvite));
        }
    }

    public bool HasTourInvite => _tourInviteText.Length > 0;

    /// <summary>Seen or passed, or invitations off, or parts were forgotten: the window works out the invitation again.</summary>
    public event Action? ToursChanged;

    public bool IsTourSeen(string part) => _settings.SeenTours?.Contains(part) == true;

    public void MarkTourSeen(string part)
    {
        var seen = _settings.SeenTours ?? new List<string>();
        if (seen.Contains(part)) return;
        seen.Add(part);
        _settings.SeenTours = seen;
        if (!_pathsFromCommandLine) _settings.Save();
        ToursChanged?.Invoke();
    }

    /// <summary>"Tanıtımı sıfırla": every part invites again, from the first steps on, as a game's tutorial played anew.</summary>
    private void ResetTours()
    {
        _settings.SeenTours = new List<string>();
        _settings.TourInvites = true;
        if (!_pathsFromCommandLine) _settings.Save();
        Notify(nameof(TourInvitesOn));
        IsSettingsOpen = false;
        StatusText = "Tanıtım sıfırlandı: davetler en baştan başlıyor.";
        ToursChanged?.Invoke();
    }
}
