using System;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Wraps a real <see cref="IRadioConnection"/> so a SEQUENCE of otherwise-
/// independent read operations (one <see cref="RadioCodeplugRawSnapshotReader.Capture"/>
/// followed by up to a dozen <c>AddMissingXxx</c> calls, each of which
/// normally opens its own session and closes it again) collapses into ONE
/// real session instead of a dozen.
///
/// Found live 2026-08-24: every real <see cref="IRadioConnection.Close"/>
/// makes the radio physically reboot (already documented elsewhere in this
/// codebase - see <see cref="RadioWriteVerification"/>'s own doc comment).
/// Preparing a write that needs several different entity types topped up
/// (a few new/edited zones, a new scan list, a new radio ID, say) ran that
/// whole 13-call sequence unwrapped, rebooting the radio once per entity
/// type that had anything missing - reported live as the radio rebooting
/// 3-4+ times back to back before a write even started, badly slow and
/// occasionally never recovering.
///
/// The first <see cref="TryOpen"/> really opens. Every <see cref="TryOpen"/>
/// after that, while still open on the same port, is a free no-op success.
/// <see cref="Close"/> is deferred - it does nothing - until the
/// orchestrating code calls <see cref="FinishAndClose"/> explicitly, once
/// the whole read-preparation sequence is done. The real
/// <see cref="IRadioConnection"/> this wraps should be used directly again
/// (not through this wrapper) for the write itself afterward.
/// </summary>
public sealed class BatchedRadioConnection(IRadioConnection inner) : IRadioConnection
{
    private bool _isOpen;
    private string? _openPortName;

    public event Action<string>? Warning
    {
        add => inner.Warning += value;
        remove => inner.Warning -= value;
    }

    public bool TryOpen(string portName, out string? error)
    {
        if (_isOpen && _openPortName == portName)
        {
            error = null;
            return true;
        }

        if (!inner.TryOpen(portName, out error))
        {
            return false;
        }

        _isOpen = true;
        _openPortName = portName;
        return true;
    }

    public RadioIdentity Identify() => inner.Identify();

    public byte[] ReadMemory(int address, int length) => inner.ReadMemory(address, length);

    public byte[] ReadMemoryStrict(int address, int length) => inner.ReadMemoryStrict(address, length);

    public void WriteMemory(int address, byte[] data) => inner.WriteMemory(address, data);

    /// <summary>Deferred - see this class's own doc comment. Only
    /// <see cref="FinishAndClose"/> can actually end the batched session,
    /// so a caller holding only this wrapper can't prematurely trigger a
    /// reboot partway through the sequence.</summary>
    public void Close()
    {
    }

    /// <summary>Actually closes the real connection (rebooting the radio,
    /// same as any other close) if it was ever opened through this wrapper.
    /// Call once, after the whole read-preparation sequence finishes -
    /// including on the exception path, so a failure partway through still
    /// lets the radio reboot and recover rather than leaving the port open.</summary>
    public void FinishAndClose()
    {
        if (_isOpen)
        {
            inner.Close();
            _isOpen = false;
        }
    }
}
