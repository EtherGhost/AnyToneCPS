using System;
using System.IO;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Optional plain-text trace of every byte that crosses the radio
/// connection, plus the higher-level write/verify decisions built on top of
/// it. Off by default (no-op, effectively free) - a platform head opts in by
/// calling <see cref="Start"/> once at startup. Added to diagnose a real
/// on-radio "programming error" report that couldn't be explained from a USB
/// capture alone (the capture showed a clean write/verify at the wire level,
/// so the next step is correlating exact addresses/bytes with what the radio
/// does after each one).
/// </summary>
public static class RadioProtocolLog
{
    private static readonly object Lock = new();
    private static StreamWriter? _writer;

    public static bool IsEnabled => _writer is not null;

    /// <summary>Starts logging to <paramref name="filePath"/>, truncating
    /// any previous run's log so there's never ambiguity about which run a
    /// log file belongs to. Best-effort: if the file can't be opened,
    /// logging silently stays disabled rather than blocking startup.</summary>
    public static void Start(string filePath)
    {
        lock (Lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _writer = new StreamWriter(filePath, append: false) { AutoFlush = true };
                _writer.WriteLine($"=== AnyToneCPS radio protocol log started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
            }
            catch
            {
                _writer = null;
            }
        }
    }

    public static void Write(string message)
    {
        var writer = _writer;
        if (writer is null)
        {
            return;
        }

        lock (Lock)
        {
            try
            {
                writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
            }
            catch
            {
                // Best-effort - a logging failure must never break a radio operation.
            }
        }
    }

    public static void WriteHex(string label, ReadOnlySpan<byte> bytes) =>
        Write($"{label} ({bytes.Length}B): {Convert.ToHexString(bytes)}");
}
