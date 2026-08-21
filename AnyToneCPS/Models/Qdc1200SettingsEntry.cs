using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// QDC 1200 Setting - two tabs (Decode, Encode), the global/singleton
/// fields only - the Encode tab's 100-row ID table is a separate list
/// entity, <see cref="Qdc1200IdEntry"/>, matching how Hot Key split its
/// own 3 tabs across a singleton-shaped entity plus separate list
/// entities.
///
/// The full field spec was captured 2026-08-04 from live vendor CPS
/// inspection. No existing byte-layout knowledge anywhere: unlike Hot Key,
/// the xbenkozx/anytone-cps reference project doesn't even have a QDC 1200
/// Setting dialog or decode function for ANY AnyTone model (its own
/// feature-support table lists "QDC 1200" as unimplemented across the
/// board) - a complete blank, not merely unconfirmed for D890UV.
///
/// Real D890UV addresses/byte layout found blind via two live differential
/// WRITE captures the same day - see Qdc1200SettingsCodec's own doc
/// comment for the full per-field confirmation. Full radio-write support
/// added the same day too, once the byte layout was confirmed.
/// </summary>
public partial class Qdc1200SettingsEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - separate from any project-file
    /// "dirty" tracking (this entity has none of its own; the whole
    /// project's dirty flag is tracked at MainViewModel level instead),
    /// same split every other radio-write-capable entity's own
    /// <c>_radioSyncSnapshot</c> uses. Only set by <see cref="MarkRadioSynced"/>.</summary>
    private Qdc1200SettingsSnapshot? _radioSyncSnapshot;

    /// <summary>Single aggregate flag rather than per-field
    /// IsXPendingRadioWrite booleans - same reasoning as
    /// AlarmSettingsEntry's own HasAnyPendingRadioWrite: this view has no
    /// per-field "pending write" UI indicator to drive.</summary>
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private Qdc1200SettingsSnapshot CreateRadioSnapshot() => new(
        AutoResetTime, SelfIdPrivateCall, SelfIdGroupCall, RemoteListeningDuration, RemotelyKillAllow, RemotelyMonitorAllow,
        SideTone, MaxAckWaitTime, Pretime, ResendCode);

    private sealed record Qdc1200SettingsSnapshot(
        byte AutoResetTime, string SelfIdPrivateCall, string SelfIdGroupCall, int RemoteListeningDuration, bool RemotelyKillAllow, bool RemotelyMonitorAllow,
        bool SideTone, double MaxAckWaitTime, int Pretime, byte ResendCode);

    // --- Decode tab ---
    [ObservableProperty] private byte _autoResetTime;
    [ObservableProperty] private string _selfIdPrivateCall = "";
    [ObservableProperty] private string _selfIdGroupCall = "";
    [ObservableProperty] private int _remoteListeningDuration = 5;
    [ObservableProperty] private bool _remotelyKillAllow;
    [ObservableProperty] private bool _remotelyMonitorAllow;

    // --- Encode tab (global fields only - the 100-row ID table is
    // Qdc1200IdEntry, held on MainViewModel like every other list) ---
    [ObservableProperty] private bool _sideTone;
    [ObservableProperty] private double _maxAckWaitTime = 0.5;
    [ObservableProperty] private int _pretime = 10;
    [ObservableProperty] private byte _resendCode = 1;

    public static IReadOnlyList<string> AutoResetTimeOptions { get; } = Enumerable.Range(0, 251).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> RemoteListeningDurationOptions { get; } = Enumerable.Range(5, 236).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> MaxAckWaitTimeOptions { get; } = Enumerable.Range(1, 120).Select(i => (i * 0.5).ToString("0.0", CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> PretimeOptions { get; } = Enumerable.Range(1, 250).Select(i => (i * 10).ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> ResendCodeOptions { get; } = ["1", "2", "3"];

    public string AutoResetTimeText
    {
        get => AutoResetTime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed <= 250)
            {
                AutoResetTime = parsed;
            }
        }
    }

    public string RemoteListeningDurationText
    {
        get => RemoteListeningDuration.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 5 and <= 240)
            {
                RemoteListeningDuration = parsed;
            }
        }
    }

    public string MaxAckWaitTimeText
    {
        get => MaxAckWaitTime.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0.5 and <= 60.0)
            {
                MaxAckWaitTime = parsed;
            }
        }
    }

    public string PretimeText
    {
        get => Pretime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2500)
            {
                Pretime = parsed;
            }
        }
    }

    public string ResendCodeText
    {
        get => ResendCode.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 3)
            {
                ResendCode = parsed;
            }
        }
    }

    partial void OnAutoResetTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoResetTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSelfIdPrivateCallChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnSelfIdGroupCallChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnRemoteListeningDurationChanged(int value)
    {
        OnPropertyChanged(nameof(RemoteListeningDurationText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnRemotelyKillAllowChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnRemotelyMonitorAllowChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnSideToneChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnMaxAckWaitTimeChanged(double value)
    {
        OnPropertyChanged(nameof(MaxAckWaitTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPretimeChanged(int value)
    {
        OnPropertyChanged(nameof(PretimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnResendCodeChanged(byte value)
    {
        OnPropertyChanged(nameof(ResendCodeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
