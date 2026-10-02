using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>One of up to 32 APRS Receive Filter entries. Radio-write
/// dirty-tracking scaffolding only, added ahead of the actual encode/patch
/// work - see AprsSettingsEntry's own doc comment for the same "ported from
/// a reference project, not yet live-confirmed for D890UV" caveat, which
/// applies equally to AprsReceiveFilterCodec.</summary>
public partial class AprsReceiveFilterEntry : ObservableObject
{
    /// <summary>Radio-write baseline only, same "_radioSyncSnapshot" split
    /// every other radio-write-capable entity uses. Deliberately excludes
    /// Number - it's the slot position, not an encoded field.</summary>
    private AprsReceiveFilterSnapshot? _radioSyncSnapshot;

    /// <summary>Separate baseline from <see cref="_radioSyncSnapshot"/> -
    /// same "unsaved to the project file" vs "not yet written to the
    /// radio" split every other write-capable entity uses. Was missing
    /// entirely until found live 2026-10-02, same gap as every other
    /// entity fixed alongside this one.</summary>
    private AprsReceiveFilterSnapshot? _cleanSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _callsign = "";
    [ObservableProperty] private byte _ssid;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public bool IsDirty => _cleanSnapshot is null || CreateRadioSnapshot() != _cleanSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    public void MarkClean()
    {
        _cleanSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(IsDirty));
    }

    private AprsReceiveFilterSnapshot CreateRadioSnapshot() => new(Enabled, Callsign, Ssid);

    private sealed record AprsReceiveFilterSnapshot(bool Enabled, string Callsign, byte Ssid);

    partial void OnEnabledChanged(bool value) => NotifyDirtyProperties();
    partial void OnCallsignChanged(string value) => NotifyDirtyProperties();
    partial void OnSsidChanged(byte value) => NotifyDirtyProperties();

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsDirty));
    }
}
