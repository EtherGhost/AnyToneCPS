using System;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Lightweight service-locator hook so the shared project can obtain a
/// platform-specific <see cref="IRadioConnection"/> (backed by
/// <c>System.IO.Ports</c>, which only exists on the Desktop head) without
/// the shared project ever referencing the Desktop project directly.
///
/// The Desktop head's entry point sets <see cref="Factory"/>/<see cref="PortLister"/>
/// before the Avalonia app starts. On platforms that never set these
/// (Android/iOS/Browser), they stay null and the UI degrades to
/// "radio connection not available on this platform".
/// </summary>
public static class RadioConnectionProvider
{
    public static Func<IRadioConnection>? Factory { get; set; }

    public static Func<IReadOnlyList<string>>? PortLister { get; set; }
}
