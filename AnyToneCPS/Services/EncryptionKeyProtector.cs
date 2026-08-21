using System;
using System.Security.Cryptography;
using System.Text;

namespace AnyToneCPS.Services;

/// <summary>
/// Obfuscates the encryption key material (AES/ARC4/Basic codes) saved in
/// the JSON project file, so it no longer sits there as plain readable
/// hex - decided 2026-08-16 as a deliberate tradeoff: the key below
/// is embedded in this open-source app's own compiled binary, so this is
/// NOT a real secret and does NOT protect against someone with the app's
/// source or a decompiler. It protects against the much more common case
/// (a project file glanced at, synced to a cloud drive, or opened in a
/// text editor) where a plain hex string would otherwise be immediately
/// readable. AES-GCM, not just XOR/base64 - real authenticated encryption
/// against the fixed key, so a tampered value fails to decrypt rather than
/// silently producing garbage.
///
/// Backward compatible: <see cref="Decrypt"/> only touches values that
/// carry <see cref="Prefix"/> - a plain hex value from a project file
/// saved before this feature existed passes through unchanged instead of
/// throwing. The next Save upgrades it to the encrypted form.
/// </summary>
public static class EncryptionKeyProtector
{
    private const string Prefix = "ENC1:";

    // Not a real secret - see class doc comment. Fixed so every install of
    // this app can read a project file any other install encrypted.
    private static readonly byte[] Key =
    [
        0x4a, 0x1f, 0x8c, 0x3e, 0x9b, 0x27, 0x6d, 0x51,
        0xe0, 0x84, 0x3a, 0x17, 0x5c, 0xb2, 0x99, 0x08,
        0x71, 0xfd, 0x2b, 0x64, 0xa8, 0x0e, 0x93, 0x46,
        0xc5, 0x1a, 0xd7, 0x38, 0x60, 0xef, 0x22, 0xb9
    ];

    private const int NonceLength = 12;
    private const int TagLength = 16;

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagLength];

        using var aesGcm = new AesGcm(Key, TagLength);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[NonceLength + cipherBytes.Length + TagLength];
        nonce.CopyTo(combined, 0);
        cipherBytes.CopyTo(combined, NonceLength);
        tag.CopyTo(combined, NonceLength + cipherBytes.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    /// <summary>Returns <paramref name="value"/> unchanged if it doesn't
    /// carry <see cref="Prefix"/> (a legacy plaintext project file, or
    /// simply blank) - see class doc comment. Also falls back to the raw
    /// value (rather than throwing) if it carries the prefix but fails to
    /// decrypt, so a corrupted/hand-edited field doesn't block loading the
    /// rest of the project - the caller sees the still-garbled value and
    /// can re-enter the key.</summary>
    public static string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            var combined = Convert.FromBase64String(value[Prefix.Length..]);
            var nonce = combined.AsSpan(0, NonceLength);
            var cipherBytes = combined.AsSpan(NonceLength, combined.Length - NonceLength - TagLength);
            var tag = combined.AsSpan(combined.Length - TagLength, TagLength);
            var plainBytes = new byte[cipherBytes.Length];

            using var aesGcm = new AesGcm(Key, TagLength);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return value;
        }
    }
}
