using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using AnyToneCPS.Services.Radio;

namespace AnyToneCPS.Android.Services;

/// <summary>
/// USB Host connection to an AnyTone D890UV's programming cable, for the
/// Android head (no <c>System.IO.Ports</c> equivalent exists on Android -
/// this talks to the same STM32 CDC-ACM device
/// <c>AnyToneCPS.Desktop.Services.SerialRadioConnection</c> talks to on
/// Desktop, but over Android's raw USB Host API instead of a serial port).
/// Protocol logic (PROGRAM handshake, read/write block framing, checksums)
/// is deliberately identical to <c>SerialRadioConnection</c> - only the
/// transport differs, so any protocol fix made there should be mirrored
/// here too.
///
/// VID:PID <c>0x0483:0x5740</c> (ST's generic demo IDs, not AnyTone's own)
/// confirmed via <c>Docs/AnyTone_D890UV/Protocol_Notes.md</c> - do not reuse
/// the D868UV/D878UV family's <c>0x28e9:0x018a</c> or the D578UV/D168UV
/// family's <c>0x2e3c:0x5740</c>, both different hardware.
/// </summary>
public sealed class AndroidUsbRadioConnection : IRadioConnection
{
    private const int VendorId = 0x0483;
    private const int ProductId = 0x5740;
    private const int MemoryBlockLength = 16;
    private const int UsbTimeoutMs = 2000;
    private const string UsbPermissionAction = "se.tobbe.anytonecps.USB_PERMISSION";

    private UsbDeviceConnection? _connection;
    private UsbEndpoint? _bulkIn;
    private UsbEndpoint? _bulkOut;
    private UsbInterface? _dataInterface;

    public event Action<string>? Warning;

    public static bool IsRadioAttached() => FindDevice() is not null;

    private static UsbDevice? FindDevice()
    {
        var usbManager = GetUsbManager();
        return usbManager.DeviceList?.Values
            .FirstOrDefault(d => d.VendorId == VendorId && d.ProductId == ProductId);
    }

    private static UsbManager GetUsbManager() =>
        (UsbManager)Application.Context.GetSystemService(Context.UsbService)!;

    public bool TryOpen(string portName, out string? error)
    {
        try
        {
            var device = FindDevice();
            if (device is null)
            {
                error = "No AnyTone radio found on USB - check the OTG cable and that the radio is powered on.";
                return false;
            }

            var usbManager = GetUsbManager();
            if (!usbManager.HasPermission(device) && !RequestPermissionAndWait(usbManager, device))
            {
                error = "USB permission for the radio was not granted.";
                return false;
            }

            var (dataInterface, bulkIn, bulkOut) = FindDataInterface(device);
            if (dataInterface is null || bulkIn is null || bulkOut is null)
            {
                error = "Could not find the radio's USB data endpoints (unexpected device descriptor).";
                return false;
            }

            var connection = usbManager.OpenDevice(device);
            if (connection is null)
            {
                error = "Failed to open the USB connection to the radio.";
                return false;
            }

            if (!connection.ClaimInterface(dataInterface, true))
            {
                error = "Failed to claim the radio's USB data interface.";
                connection.Close();
                return false;
            }

            _connection = connection;
            _dataInterface = dataInterface;
            _bulkIn = bulkIn;
            _bulkOut = bulkOut;

            ConfigureCdcLine(device, connection);

            var programBytes = Encoding.ASCII.GetBytes("PROGRAM");
            WriteRaw(programBytes);
            var response = ReadExactly(3);

            if (response.Length != 3 || response[0] != 0x51 || response[1] != 0x58 || response[2] != 0x06)
            {
                error = "Radio did not respond to the PROGRAM handshake as expected.";
                ClosePortOnly();
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            ClosePortOnly();
            return false;
        }
    }

    /// <summary>Blocks (this method is always called from a background
    /// thread via <c>Task.Run</c> in <c>MainViewModel.Radio.cs</c>, never
    /// the UI thread) until the user responds to Android's system USB
    /// permission dialog, or 30s pass with no response.</summary>
    private static bool RequestPermissionAndWait(UsbManager usbManager, UsbDevice device)
    {
        using var signal = new SemaphoreSlim(0, 1);
        var granted = false;

        var receiver = new UsbPermissionReceiver((intent) =>
        {
            granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            signal.Release();
        });

        var filter = new IntentFilter(UsbPermissionAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Application.Context.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            Application.Context.RegisterReceiver(receiver, filter);
        }

        try
        {
            var intent = new Intent(UsbPermissionAction);
            intent.SetPackage(Application.Context.PackageName);
            var flags = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? PendingIntentFlags.Mutable
                : PendingIntentFlags.UpdateCurrent;
            var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, flags);
            usbManager.RequestPermission(device, pendingIntent);

            signal.Wait(TimeSpan.FromSeconds(30));
            return granted;
        }
        finally
        {
            Application.Context.UnregisterReceiver(receiver);
        }
    }

    private sealed class UsbPermissionReceiver(Action<Intent> onReceive) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null)
            {
                onReceive(intent);
            }
        }
    }

    /// <summary>The Data Class interface (bulk IN/OUT, the actual byte
    /// stream) is a separate USB interface from the Communications Class
    /// interface (control/interrupt, used only for <see cref="ConfigureCdcLine"/>) -
    /// standard CDC-ACM shape, not specific to this radio.</summary>
    private static (UsbInterface? DataInterface, UsbEndpoint? BulkIn, UsbEndpoint? BulkOut) FindDataInterface(UsbDevice device)
    {
        for (var i = 0; i < device.InterfaceCount; i++)
        {
            var iface = device.GetInterface(i);
            if (iface is null)
            {
                continue;
            }

            UsbEndpoint? bulkIn = null;
            UsbEndpoint? bulkOut = null;
            for (var e = 0; e < iface.EndpointCount; e++)
            {
                var endpoint = iface.GetEndpoint(e);
                if (endpoint?.Type != UsbAddressing.XferBulk)
                {
                    continue;
                }

                if (endpoint.Direction == UsbAddressing.In)
                {
                    bulkIn = endpoint;
                }
                else if (endpoint.Direction == UsbAddressing.Out)
                {
                    bulkOut = endpoint;
                }
            }

            if (bulkIn is not null && bulkOut is not null)
            {
                return (iface, bulkIn, bulkOut);
            }
        }

        return (null, null, null);
    }

    /// <summary>Standard CDC-ACM class control requests, sent to the
    /// Communications interface: SET_LINE_CODING (921600 8N1, matching
    /// <c>SerialRadioConnection.BaudRate</c> exactly - the vendor CPS uses
    /// the same rate) and SET_CONTROL_LINE_STATE (assert DTR+RTS). Neither
    /// is strictly meaningful for a USB CDC virtual COM port's data content,
    /// but some CDC-ACM device firmware won't start passing bulk data
    /// through until DTR is asserted - best-effort, failures here are not
    /// fatal (caught and ignored) since the actual byte stream is what
    /// matters, not whether the device's UART-emulation registers agree.</summary>
    private static void ConfigureCdcLine(UsbDevice device, UsbDeviceConnection connection)
    {
        try
        {
            var commInterfaceNumber = 0;
            for (var i = 0; i < device.InterfaceCount; i++)
            {
                var iface = device.GetInterface(i);
                if (iface?.InterfaceClass == UsbClass.Comm)
                {
                    commInterfaceNumber = iface.Id;
                    break;
                }
            }

            const int setLineCoding = 0x20;
            const int setControlLineState = 0x22;
            const UsbAddressing classInterfaceOut = (UsbAddressing)0x21;

            var lineCoding = new byte[7];
            BinaryPrimitives.WriteInt32LittleEndian(lineCoding.AsSpan(0, 4), 921600);
            lineCoding[4] = 0; // 1 stop bit
            lineCoding[5] = 0; // no parity
            lineCoding[6] = 8; // 8 data bits
            connection.ControlTransfer(classInterfaceOut, setLineCoding, 0, commInterfaceNumber, lineCoding, lineCoding.Length, UsbTimeoutMs);

            connection.ControlTransfer(classInterfaceOut, setControlLineState, 0x0003, commInterfaceNumber, null, 0, UsbTimeoutMs);
        }
        catch
        {
            // Best-effort - see doc comment above.
        }
    }

    public RadioIdentity Identify()
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Connection is not open. Call TryOpen first.");
        }

        WriteRaw([0x02]);
        var response = ReadExactly(16);
        if (response.Length < 16)
        {
            return new RadioIdentity("", "", false);
        }

        var modelGateField = Encoding.ASCII.GetString(response, 0, 8).TrimEnd('\0', ' ');
        var versionGateField = Encoding.ASCII.GetString(response, 9, 4);

        var model = Encoding.ASCII.GetString(response, 1, 7).TrimEnd('\0', ' ');
        var version = Encoding.ASCII.GetString(response, 9, 6).TrimEnd('\0', ' ');

        var isRecognized = modelGateField == "ID890UV" && versionGateField == "V100";
        return new RadioIdentity(model, version, isRecognized);
    }

    public byte[] ReadMemory(int address, int length)
    {
        var result = new byte[length];
        var written = 0;
        for (var addr = address; addr < address + length; addr += MemoryBlockLength)
        {
            var block = ReadMemoryBlock(addr, strict: false);
            var toCopy = Math.Min(block.Length, length - written);
            Array.Copy(block, 0, result, written, toCopy);
            written += toCopy;
        }

        return result;
    }

    public byte[] ReadMemoryStrict(int address, int length)
    {
        var result = new byte[length];
        var written = 0;
        for (var addr = address; addr < address + length; addr += MemoryBlockLength)
        {
            var block = ReadMemoryBlock(addr, strict: true);
            var toCopy = Math.Min(block.Length, length - written);
            Array.Copy(block, 0, result, written, toCopy);
            written += toCopy;
        }

        return result;
    }

    public void WriteMemory(int address, byte[] data)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Connection is not open. Call TryOpen first.");
        }

        if (address % MemoryBlockLength != 0)
        {
            throw new ArgumentException($"Address 0x{address:X8} is not 16-byte aligned.", nameof(address));
        }

        if (data.Length % MemoryBlockLength != 0)
        {
            throw new ArgumentException($"Data length {data.Length} is not a multiple of 16 bytes.", nameof(data));
        }

        for (var offset = 0; offset < data.Length; offset += MemoryBlockLength)
        {
            WriteMemoryBlock(address + offset, data.AsSpan(offset, MemoryBlockLength));
        }
    }

    public void Close()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            var endBytes = Encoding.ASCII.GetBytes("END");
            WriteRaw(endBytes);
            ReadExactly(1); // Drain the ack; content is not validated.
        }
        catch
        {
            // Best-effort: we're tearing the connection down regardless.
        }
        finally
        {
            ClosePortOnly();
        }
    }

    private byte[] ReadMemoryBlock(int address, bool strict)
    {
        var (data, ok, detail) = TryReadMemoryBlockOnce(address);
        if (ok)
        {
            return data;
        }

        if (!strict)
        {
            Warning?.Invoke($"{detail} at 0x{address:X8}.");
            return data;
        }

        (data, ok, detail) = TryReadMemoryBlockOnce(address);
        if (ok)
        {
            return data;
        }

        throw new RadioReadVerificationFailedException($"{detail} at 0x{address:X8} (persisted after one immediate retry).", address);
    }

    private (byte[] Data, bool Ok, string Detail) TryReadMemoryBlockOnce(int address)
    {
        const int length = MemoryBlockLength;

        var request = new byte[6];
        request[0] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(1, 4), address);
        request[5] = length;
        WriteRaw(request);

        var expectedTotalLength = 6 + length + 2;
        var response = ReadExactly(expectedTotalLength);

        var data = new byte[length];
        if (response.Length >= 6 + length)
        {
            Array.Copy(response, 6, data, 0, length);
        }
        else if (response.Length > 6)
        {
            Array.Copy(response, 6, data, 0, response.Length - 6);
        }

        var malformed = response.Length < expectedTotalLength
            || response[0] != (byte)'W'
            || response[^1] != 0x06;

        if (malformed)
        {
            return (data, false, $"Malformed read-memory response ({response.Length} bytes received)");
        }

        var sum = 0;
        for (var i = 1; i <= 5 + length; i++)
        {
            sum += response[i];
        }

        var checksum = (byte)(sum & 0xFF);
        if (checksum != response[6 + length])
        {
            return (data, false, "Checksum mismatch reading memory");
        }

        return (data, true, "");
    }

    private void WriteMemoryBlock(int address, ReadOnlySpan<byte> data)
    {
        var request = RadioWriteProtocol.BuildBlockRequest(address, data);
        WriteRaw(request);

        var response = ReadExactly(1);
        if (response.Length != 1 || response[0] != 0x06)
        {
            throw new RadioWriteFailedException(
                $"Write to 0x{address:X8} was not acknowledged (received {response.Length} byte(s): {Convert.ToHexString(response)}).",
                address);
        }
    }

    private void WriteRaw(byte[] data)
    {
        if (_connection is null || _bulkOut is null)
        {
            throw new InvalidOperationException("Connection is not open.");
        }

        var sent = _connection.BulkTransfer(_bulkOut, data, 0, data.Length, UsbTimeoutMs);
        if (sent != data.Length)
        {
            throw new IOException($"USB write sent {sent} of {data.Length} bytes.");
        }
    }

    /// <summary>Mirrors <c>SerialRadioConnection.ReadExactly</c> - blocks
    /// (via <see cref="UsbDeviceConnection.BulkTransfer(UsbEndpoint, byte[], int, int, int)"/>'s
    /// own timeout) until exactly <paramref name="expectedLength"/> bytes
    /// have been received or the per-call timeout elapses.</summary>
    private byte[] ReadExactly(int expectedLength)
    {
        if (_connection is null || _bulkIn is null)
        {
            throw new InvalidOperationException("Connection is not open.");
        }

        var buffer = new byte[expectedLength];
        var offset = 0;

        while (offset < expectedLength)
        {
            var read = _connection.BulkTransfer(_bulkIn, buffer, offset, expectedLength - offset, UsbTimeoutMs);
            if (read <= 0)
            {
                break;
            }

            offset += read;
        }

        if (offset == expectedLength)
        {
            return buffer;
        }

        var partial = new byte[offset];
        Array.Copy(buffer, partial, offset);
        return partial;
    }

    private void ClosePortOnly()
    {
        try
        {
            if (_connection is not null && _dataInterface is not null)
            {
                _connection.ReleaseInterface(_dataInterface);
            }

            _connection?.Close();
        }
        catch
        {
            // ignore - we're discarding this connection regardless.
        }
        finally
        {
            _connection = null;
            _dataInterface = null;
            _bulkIn = null;
            _bulkOut = null;
        }
    }
}
