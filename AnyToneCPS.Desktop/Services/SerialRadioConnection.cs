using System;
using System.Buffers.Binary;
using System.IO.Ports;
using System.Text;
using AnyToneCPS.Services.Radio;

namespace AnyToneCPS.Desktop.Services;

/// <summary>
/// Serial connection to an AnyTone D890UV over its USB programming cable:
/// the "PROGRAM" handshake, the identity query, memory reads, and memory
/// writes.
///
/// Read protocol confirmed via live USB captures against real hardware and
/// cross-validated against the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (desktop/include/device.h). Write
/// protocol confirmed 2026-07-17 via a live USB capture of the vendor CPS's
/// "Write To Radio" - see <c>Docs/AnyTone_D890UV/Capture_Findings.md</c>'s
/// "WRITE protocol confirmed byte-for-byte" section for the exact captured
/// bytes this implementation is built on.
///
/// Two read paths, deliberately different trust levels: <see cref="ReadMemory"/>
/// is lenient (a bad block just raises <see cref="Warning"/> and returns
/// best-effort bytes) since ordinary reads tolerate an occasional serial
/// glitch. <see cref="ReadMemoryStrict"/> retries a bad block once and then
/// throws rather than returning suspect data - used wherever a wrong byte has
/// real consequences (the write path's read-modify-write base and its
/// mandatory post-write verification), added 2026-07-18 after a real write
/// was reported as "failed" when in fact the write itself was fine and the
/// (lenient) verification read had an ordinary glitch.
/// </summary>
public sealed class SerialRadioConnection : IRadioConnection
{
    private const int BaudRate = 921600;
    private const int ReadTimeoutMs = 1000;
    private const int MemoryBlockLength = 16;

    private SerialPort? _port;

    public event Action<string>? Warning;

    public bool TryOpen(string portName, out string? error)
    {
        SerialPort? port = null;
        try
        {
            port = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = ReadTimeoutMs,
                WriteTimeout = ReadTimeoutMs
            };
            port.Open();
            _port = port;

            var programBytes = Encoding.ASCII.GetBytes("PROGRAM");
            port.Write(programBytes, 0, programBytes.Length);
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

    public RadioIdentity Identify()
    {
        if (_port is null)
        {
            throw new InvalidOperationException("Port is not open. Call TryOpen first.");
        }

        _port.Write([0x02], 0, 1);
        var response = ReadExactly(16);
        if (response.Length < 16)
        {
            return new RadioIdentity("", "", false);
        }

        // Byte 0 = 'I', bytes 1-7 = model name (e.g. "D890UV\0"), byte 8 = band,
        // bytes 9-14 = version (e.g. "V100\0\0"), byte 15 = 0x06 ack.
        var modelGateField = Encoding.ASCII.GetString(response, 0, 8).TrimEnd('\0', ' ');
        var versionGateField = Encoding.ASCII.GetString(response, 9, 4);

        var model = Encoding.ASCII.GetString(response, 1, 7).TrimEnd('\0', ' ');
        var version = Encoding.ASCII.GetString(response, 9, 6).TrimEnd('\0', ' ');

        var isRecognized = modelGateField == "ID890UV" && versionGateField == "V100";
        return new RadioIdentity(model, version, isRecognized);
    }

    public byte[] ReadMemory(int address, int length)
    {
        if (_port is null)
        {
            throw new InvalidOperationException("Port is not open. Call TryOpen first.");
        }

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
        if (_port is null)
        {
            throw new InvalidOperationException("Port is not open. Call TryOpen first.");
        }

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
        if (_port is null)
        {
            throw new InvalidOperationException("Port is not open. Call TryOpen first.");
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
        if (_port is null)
        {
            return;
        }

        try
        {
            if (_port.IsOpen)
            {
                var endBytes = Encoding.ASCII.GetBytes("END");
                _port.Write(endBytes, 0, endBytes.Length);
                ReadExactly(1); // Drain the ack; content is not validated.
            }
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

    /// <summary>Reads one 16-byte block. When <paramref name="strict"/> is
    /// false (the default, general-purpose read path), a malformed response
    /// or checksum mismatch only raises <see cref="Warning"/> and returns
    /// whatever bytes were captured - a stale or corrupt read here is noisy,
    /// not dangerous, since nothing downstream trusts it blindly. When
    /// <paramref name="strict"/> is true (used for the read-modify-write
    /// patch base and post-write verification, where a wrong byte has real
    /// consequences), a bad block is retried once immediately; if the retry
    /// is also bad, throws <see cref="RadioReadVerificationFailedException"/>
    /// instead of returning data nothing can trust.</summary>
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
        _port!.Write(request, 0, request.Length);

        // Response length is deterministic for this protocol: 'W' + 4-byte
        // address + 1-byte size + `length` data bytes + 1-byte checksum +
        // 0x06 ack = 6 + length + 2.
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

    /// <summary>
    /// Writes exactly one 16-byte block. Request bytes are built by
    /// <see cref="RadioWriteProtocol.BuildBlockRequest"/> (kept as a pure,
    /// separately-unit-tested function - see that class for the confirmed
    /// wire format). Response is exactly 1 byte, which must be 0x06 (ACK).
    ///
    /// Unlike <see cref="ReadMemoryBlock"/>, this throws immediately on any
    /// anomaly rather than raising <see cref="Warning"/> and continuing - a
    /// write whose success can't be confirmed must stop the whole operation,
    /// not silently proceed to the next block.
    /// </summary>
    private void WriteMemoryBlock(int address, ReadOnlySpan<byte> data)
    {
        var request = RadioWriteProtocol.BuildBlockRequest(address, data);
        _port!.Write(request, 0, request.Length);

        var response = ReadExactly(1);
        if (response.Length != 1 || response[0] != 0x06)
        {
            throw new RadioWriteFailedException(
                $"Write to 0x{address:X8} was not acknowledged (received {response.Length} byte(s): {Convert.ToHexString(response)}).",
                address);
        }
    }

    /// <summary>
    /// Blocks (via the OS/driver's own wait, through <see cref="SerialPort.Read(byte[],int,int)"/>
    /// and <see cref="SerialPort.ReadTimeout"/>) until exactly
    /// <paramref name="expectedLength"/> bytes have been received or the
    /// per-call timeout elapses, whichever comes first. This replaced an
    /// earlier version that manually polled <see cref="SerialPort.BytesToRead"/>
    /// with a 50ms `Thread.Sleep` between checks - since every response in
    /// this protocol has a known, fixed length, that manual polling was
    /// adding up to 50ms of pure dead time per block read for no reason,
    /// which was the dominant cost across the thousands of block reads a
    /// full codeplug read performs (reported ~10-100x slower than the vendor
    /// CPS before this fix).
    /// </summary>
    private byte[] ReadExactly(int expectedLength)
    {
        if (_port is null)
        {
            throw new InvalidOperationException("Port is not open.");
        }

        var buffer = new byte[expectedLength];
        var offset = 0;

        while (offset < expectedLength)
        {
            int read;
            try
            {
                read = _port.Read(buffer, offset, expectedLength - offset);
            }
            catch (TimeoutException)
            {
                break;
            }

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
            _port?.Dispose();
        }
        catch
        {
            // ignore - we're discarding this connection regardless.
        }
        finally
        {
            _port = null;
        }
    }
}
