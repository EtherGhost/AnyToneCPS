using System.Collections.Generic;

namespace AnyToneCPS.Android.Services;

/// <summary>
/// Android equivalent of <c>SerialPortLister.GetAvailablePorts</c> - there's
/// no COM-port concept on Android, just "is the radio's USB device currently
/// attached or not" (checked via VID:PID, see
/// <see cref="AndroidUsbRadioConnection"/>). Returns a single descriptive
/// entry if so, matching the shared ViewModel's expectation of a
/// human-readable string list to populate its port picker.
/// </summary>
public static class AndroidUsbDeviceLister
{
    public static IReadOnlyList<string> GetAvailableDevices() =>
        AndroidUsbRadioConnection.IsRadioAttached() ? ["AnyTone D890UV (USB)"] : [];
}
