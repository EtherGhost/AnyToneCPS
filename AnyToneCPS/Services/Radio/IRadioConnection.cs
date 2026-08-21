using System;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Connection to an AnyTone radio's programming interface.
///
/// Writing to the radio is a deliberately narrow, high-friction capability:
/// <see cref="WriteMemory"/> writes are not retried on failure (unlike reads,
/// which tolerate a checksum mismatch and keep going) - any malformed/short/
/// non-ACK response throws <see cref="RadioWriteFailedException"/>
/// immediately, since silently continuing after an uncertain flash write
/// would leave the radio in an unknown state. Callers must always follow a
/// strict read-modify-write pattern (read the current bytes fresh, patch
/// only the specific fields being changed, write the full patched buffer
/// back) - never construct a write buffer from scratch. See
/// <c>Docs/AnyTone_D890UV/Protocol_Notes.md</c> §5 and
/// <c>Docs/AnyTone_D890UV/Capture_Findings.md</c>'s "WRITE protocol
/// confirmed byte-for-byte" section for the protocol this is built on.
/// </summary>
public interface IRadioConnection
{
    /// <summary>Raised for non-fatal issues (e.g. a read-block checksum
    /// mismatch) that are logged but do not stop or fail the read.
    /// Never raised for write failures - those throw instead.</summary>
    event Action<string>? Warning;

    bool TryOpen(string portName, out string? error);

    RadioIdentity Identify();

    byte[] ReadMemory(int address, int length);

    /// <summary>Like <see cref="ReadMemory"/>, but for callers where a wrong
    /// byte has real consequences (the read-modify-write patch base, and
    /// post-write verification) rather than just being noisy: any block with
    /// a malformed response or a failing checksum is retried once
    /// immediately, and if the retry also fails, throws
    /// <see cref="RadioReadVerificationFailedException"/> instead of
    /// silently returning suspect bytes. Not a general replacement for
    /// <see cref="ReadMemory"/> - full-codeplug reads intentionally stay
    /// lenient.</summary>
    byte[] ReadMemoryStrict(int address, int length);

    /// <summary>Writes <paramref name="data"/> to the radio starting at
    /// <paramref name="address"/>, in 16-byte-aligned blocks. Both
    /// <paramref name="address"/> and <paramref name="data"/>'s length must
    /// be exact multiples of 16 (throws <see cref="ArgumentException"/>
    /// otherwise). Throws <see cref="RadioWriteFailedException"/> immediately
    /// on the first block that doesn't ACK cleanly - does not attempt the
    /// remaining blocks, and does not retry. Callers are responsible for
    /// verifying the write afterward via a fresh <see cref="ReadMemory"/>
    /// call; this method does not verify on its own.</summary>
    void WriteMemory(int address, byte[] data);

    void Close();
}

/// <summary>
/// Identity reported by the radio during the "PROGRAM" handshake.
/// </summary>
public sealed record RadioIdentity(string Model, string Version, bool IsRecognizedD890UV);

/// <summary>
/// Thrown when a radio write fails at the protocol level (malformed, short,
/// or non-ACK response). Distinct from generic I/O exceptions so callers can
/// specifically detect "the radio rejected/didn't confirm this write" versus
/// a lower-level connection problem.
/// </summary>
public sealed class RadioWriteFailedException(string message, int address) : Exception(message)
{
    public int Address { get; } = address;
}

/// <summary>
/// Thrown by <see cref="IRadioConnection.ReadMemoryStrict"/> when a block's
/// response is malformed or fails its checksum even after one immediate
/// retry. Distinct from <see cref="RadioWriteFailedException"/>: this means
/// "we don't actually know what's in this memory" (read-side uncertainty),
/// not "the radio rejected a write" (write-side certainty).
/// </summary>
public sealed class RadioReadVerificationFailedException(string message, int address) : Exception(message)
{
    public int Address { get; } = address;
}
