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

    [ObservableProperty] private int _number;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _callsign = "";
    [ObservableProperty] private byte _ssid;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AprsReceiveFilterSnapshot CreateRadioSnapshot() => new(Enabled, Callsign, Ssid);

    private sealed record AprsReceiveFilterSnapshot(bool Enabled, string Callsign, byte Ssid);

    partial void OnEnabledChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnCallsignChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnSsidChanged(byte value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
