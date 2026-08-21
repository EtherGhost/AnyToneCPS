using System.IO.Ports;

namespace AnyToneCPS.Desktop.Services;

/// <summary>Tiny wrapper around the platform serial port enumeration API.</summary>
public static class SerialPortLister
{
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}
