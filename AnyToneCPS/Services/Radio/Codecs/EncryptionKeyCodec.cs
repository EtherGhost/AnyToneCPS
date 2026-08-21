using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for the 3 encryption key/code lists found 2026-07-18 via a
/// live differential USB capture (see <see cref="D890UvMemoryMap"/>'s
/// AesEncryptionKey/Arc4EncryptionKey/BasicEncryptionCode constants for the
/// full provenance). Unlike every other entity in this codebase, this one
/// was NOT ported from the reference project - no prior codec or address
/// existed for it at all.
///
/// Deliberately narrow: only the field independently confirmed via the
/// capture is decoded (the raw key bytes for AES/ARC4, the 4-digit code for
/// Basic). The vendor CPS's own UI shows a second column per slot for all 3
/// types, confusingly not consistently named: AES's grid is No./Encryption
/// Key (a plain number or "Off")/Encryption ID, and the real key material
/// is under "Encryption ID"; ARC4's grid is No./Encryption ID (number or
/// "Off")/Encryption Key, with the real key material under "Encryption
/// Key" instead; Basic follows AES's layout. See <see cref="RadioReadMapper"/>'s
/// Map* functions for exactly which <c>EncryptionKeyEntry</c> field each
/// decoded value lands in. No address has been found yet for the
/// *other* column of any of the 3 types - left blank rather than guessed.
/// </summary>
public static class EncryptionKeyCodec
{
    public sealed record DecodedEncryptionKey(int Number, string KeyHex);

    public sealed record DecodedEncryptionCode(int Number, string Code);

    /// <summary>AES-256 keys: fixed 32-byte key, no ambiguity about length.</summary>
    public static IReadOnlyList<DecodedEncryptionKey> DecodeAesKeys(ReadOnlySpan<byte> data) =>
        DecodeIndexedKeys(data, D890UvMemoryMap.AesEncryptionKeyStride, D890UvMemoryMap.AesEncryptionKeyMaxSlots, fixedKeyLength: 32);

    /// <summary>ARC4 keys: fixed 5-byte field, left-zero-padded (the real
    /// key digits sit at the END of the field) - confirmed 2026-07-20 via a
    /// live differential write capture (a 2-byte key "AABB" landed on the
    /// wire as 00 00 00 AA BB), matching the reference project's own
    /// encode() (`key.rightJustified(10, '0')`) and the vendor CPS's own UI
    /// (which auto-pads a typed short key with leading zeros as you type).
    /// No trimming - the full 5 bytes are always reported, same as the
    /// vendor CPS's own display.</summary>
    public static IReadOnlyList<DecodedEncryptionKey> DecodeArc4Keys(ReadOnlySpan<byte> data) =>
        DecodeIndexedKeys(data, D890UvMemoryMap.Arc4EncryptionKeyStride, D890UvMemoryMap.Arc4EncryptionKeyMaxSlots, fixedKeyLength: 5);

    private static List<DecodedEncryptionKey> DecodeIndexedKeys(ReadOnlySpan<byte> data, int stride, int maxSlots, int fixedKeyLength)
    {
        var results = new List<DecodedEncryptionKey>();
        for (var slot = 0; slot < maxSlots; slot++)
        {
            var offset = slot * stride;
            if (offset >= data.Length)
            {
                break;
            }

            var entry = data.Slice(offset, Math.Min(stride, data.Length - offset));
            var index = entry[0];
            // 0x00 = never written; 0xFF = erased/uninitialized flash (the
            // same dual blank-sentinel convention TextFieldCodec uses for
            // name fields) - confirmed necessary 2026-07-18: a real read
            // against the ARC4 region showed several 0xFF-index "slot 255"
            // entries past the one real key, which is uninitialized flash,
            // not a genuine 255th key.
            if (index is 0 or 0xFF)
            {
                continue;
            }

            var rawKey = entry.Slice(1, Math.Min(fixedKeyLength, entry.Length - 1));
            results.Add(new DecodedEncryptionKey(index, Convert.ToHexString(rawKey)));
        }

        return results;
    }

    /// <summary>Basic/"Digital" encryption code: 2-byte BCD-as-decimal value
    /// at a fixed offset within each slot, no index byte - slot position is
    /// purely positional (slot 0 = Number 1, etc.). A slot reading exactly
    /// "0000" is treated as unpopulated, same ambiguity every other
    /// "all-zero means blank" convention in this codebase accepts - a real
    /// code of literally 0000 can't be distinguished from an unused slot.</summary>
    public static IReadOnlyList<DecodedEncryptionCode> DecodeBasicEncryptionCodes(ReadOnlySpan<byte> data)
    {
        var results = new List<DecodedEncryptionCode>();
        var stride = D890UvMemoryMap.BasicEncryptionCodeStride;
        var valueOffset = D890UvMemoryMap.BasicEncryptionCodeValueOffset;

        for (var slot = 0; slot < D890UvMemoryMap.BasicEncryptionCodeMaxSlots; slot++)
        {
            var offset = slot * stride + valueOffset;
            if (offset + 2 > data.Length)
            {
                break;
            }

            var valueBytes = data.Slice(offset, 2);
            if (valueBytes[0] == 0 && valueBytes[1] == 0)
            {
                continue;
            }

            var code = BcdDecimalCodec.DecodeAsDecimal(valueBytes).ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
            results.Add(new DecodedEncryptionCode(slot + 1, code));
        }

        return results;
    }

    /// <summary>
    /// Patches a single AES key slot (RMW over an already-read
    /// <see cref="D890UvMemoryMap.AesEncryptionKeyStride"/>-byte slot).
    /// Deliberately does NOT trust <paramref name="currentSlot"/>'s own index
    /// byte (offset 0) - a slot showing "Off" reads back with index 0x00,
    /// and blindly preserving that would silently leave the slot decoding as
    /// still-blank after this write - so the index byte is always set from
    /// <paramref name="slotNumber"/> instead, which the caller already knows
    /// (it's the row being written). The 32-byte key region (offset 1-32) is
    /// overwritten with <paramref name="keyHex"/>.
    ///
    /// Live differential write capture 2026-08-08 (real vendor CPS, slot 11,
    /// a distinctive 32-byte key) confirmed every part of this: index byte =
    /// slotNumber exactly, key bytes land at offset 1-32 in the same order
    /// typed/displayed (no reversal), and the vendor CPS itself left-zero-
    /// pads a short typed key to the full 32 bytes before writing (same
    /// convention as ARC4 below). The offset 33-34 trailer this class's own
    /// doc comment used to call unconfirmed: the SAME capture also wrote 2
    /// pre-existing real keys (slots 9/10) back unchanged, both showing
    /// trailer bytes "00 40", while the newly-written slot 11 (previously a
    /// leftover test/blank slot) came back "00 3e" - proving the vendor CPS
    /// itself never touches these bytes either, it just carries forward
    /// whatever was already there, exactly matching this method's own
    /// "leave <paramref name="currentSlot"/>'s trailer/padding exactly as it
    /// was" behavior. No fabrication needed - this was the right call.
    /// </summary>
    public static byte[] EncodeAesKey(ReadOnlySpan<byte> currentSlot, int slotNumber, string keyHex)
    {
        ValidateSlotLength(currentSlot, D890UvMemoryMap.AesEncryptionKeyStride, nameof(currentSlot));
        ValidateSlotNumber(slotNumber, 1, CodeplugLimits.AesEncryptionKeyCount, nameof(slotNumber));
        var keyBytes = ParseFixedLengthKey(keyHex, 32, nameof(keyHex));

        var result = currentSlot.ToArray();
        result[0] = (byte)slotNumber;
        keyBytes.CopyTo(result, 1);
        return result;
    }

    /// <summary>
    /// Patches a single ARC4 key slot. Same index-byte rationale as
    /// <see cref="EncodeAesKey"/>. Fixed 5-byte field, left-zero-padded - see
    /// <see cref="DecodeArc4Keys"/>'s doc comment for the 2026-07-20 live
    /// confirmation. A key shorter than 10 hex chars is left-padded with '0'
    /// (matching the reference project's own <c>rightJustified(10, '0')</c>
    /// and the vendor CPS's own UI auto-pad-as-you-type behavior) so its real
    /// digits land at the end of the field, not the start.
    /// </summary>
    public static byte[] EncodeArc4Key(ReadOnlySpan<byte> currentSlot, int slotNumber, string keyHex)
    {
        ValidateSlotLength(currentSlot, D890UvMemoryMap.Arc4EncryptionKeyStride, nameof(currentSlot));
        ValidateSlotNumber(slotNumber, 1, CodeplugLimits.Arc4EncryptionKeyCount, nameof(slotNumber));
        var keyBytes = ParseLeftPaddedKey(keyHex, 5, nameof(keyHex));

        var result = currentSlot.ToArray();
        result[0] = (byte)slotNumber;
        keyBytes.CopyTo(result, 1);
        return result;
    }

    /// <summary>
    /// Patches one slot's 4-digit code within an already-read 4-slot
    /// (<c>0xA0</c>-byte) Basic Encryption Code group. Only the 2 BCD bytes
    /// at <see cref="D890UvMemoryMap.BasicEncryptionCodeValueOffset"/> within
    /// the target slot are touched - the other 3 slots in the group, and
    /// every other byte of the target slot, are left exactly as
    /// <paramref name="currentGroup"/> had them. A whole group must be
    /// read/written together because the 40-byte (<c>0x28</c>) per-slot
    /// stride isn't itself 16-byte aligned, so an individual slot can't be
    /// written in isolation without also touching neighboring slots' bytes
    /// that share the same aligned write block.
    ///
    /// Live differential write capture 2026-08-08 (real vendor CPS, code
    /// "1234" into slot 1, a spare/blank slot) confirmed the address
    /// arithmetic and encoding exactly: the 2 bytes landed at
    /// BasicEncryptionCodeData (0x3585100) + valueOffset (0x10) as
    /// <c>0x12 0x34</c> - the same packed-BCD-per-digit convention
    /// <see cref="BcdDecimalCodec"/> already uses elsewhere in this
    /// codebase, exactly as this method assumed. No other byte in the
    /// 0xA0-byte group changed.
    /// </summary>
    public static byte[] EncodeBasicCodeGroup(ReadOnlySpan<byte> currentGroup, int slotIndexWithinGroup, string code)
    {
        const int groupSize = 4;
        var stride = D890UvMemoryMap.BasicEncryptionCodeStride;
        var valueOffset = D890UvMemoryMap.BasicEncryptionCodeValueOffset;
        var expectedLength = stride * groupSize;

        if (currentGroup.Length != expectedLength)
        {
            throw new ArgumentException($"Basic encryption code group must be exactly {expectedLength} bytes.", nameof(currentGroup));
        }

        if (slotIndexWithinGroup is < 0 or >= groupSize)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndexWithinGroup), slotIndexWithinGroup, $"Must be 0-{groupSize - 1}.");
        }

        if (code is not { Length: 4 } || !code.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Basic encryption code must be exactly 4 decimal digits.", nameof(code));
        }

        var value = long.Parse(code, CultureInfo.InvariantCulture);
        var encoded = BcdDecimalCodec.EncodeAsDecimal(value, 2);

        var result = currentGroup.ToArray();
        var offset = slotIndexWithinGroup * stride + valueOffset;
        encoded.CopyTo(result, offset);
        return result;
    }

    /// <summary>Clears an AES or ARC4 key slot back to "unpopulated" - zeroes
    /// the whole slot, including the index byte, matching Decode*Keys' own
    /// 0x00-index-means-blank convention. A separate method from
    /// EncodeAesKey/EncodeArc4Key because those always stamp a real index
    /// byte and require valid key hex - neither can represent the vendor
    /// CPS's "Off" state.</summary>
    public static byte[] ClearIndexedKeySlot(ReadOnlySpan<byte> currentSlot) => new byte[currentSlot.Length];

    private static byte[] ParseFixedLengthKey(string keyHex, int expectedLength, string paramName)
    {
        if (string.IsNullOrEmpty(keyHex))
        {
            throw new ArgumentException("Key hex must not be empty.", paramName);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(keyHex);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Key hex is not valid hex.", paramName, ex);
        }

        if (bytes.Length != expectedLength)
        {
            throw new ArgumentException($"Key must be exactly {expectedLength} bytes ({expectedLength * 2} hex chars), got {bytes.Length}.", paramName);
        }

        return bytes;
    }

    /// <summary>Left-pads <paramref name="keyHex"/> with '0' up to
    /// <paramref name="exactByteLength"/> * 2 hex chars before parsing - see
    /// <see cref="EncodeArc4Key"/>'s doc comment for why (the field is a
    /// fixed byte length on the wire, and short keys are stored with their
    /// real digits at the end, not the start).</summary>
    private static byte[] ParseLeftPaddedKey(string keyHex, int exactByteLength, string paramName)
    {
        if (string.IsNullOrEmpty(keyHex))
        {
            throw new ArgumentException("Key hex must not be empty.", paramName);
        }

        var expectedHexChars = exactByteLength * 2;
        if (keyHex.Length > expectedHexChars)
        {
            throw new ArgumentException($"Key must be at most {exactByteLength} bytes ({expectedHexChars} hex chars), got {keyHex.Length} hex chars.", paramName);
        }

        try
        {
            return Convert.FromHexString(keyHex.PadLeft(expectedHexChars, '0'));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Key hex is not valid hex.", paramName, ex);
        }
    }

    private static void ValidateSlotLength(ReadOnlySpan<byte> slot, int expectedLength, string paramName)
    {
        if (slot.Length != expectedLength)
        {
            throw new ArgumentException($"Slot must be exactly {expectedLength} bytes, got {slot.Length}.", paramName);
        }
    }

    private static void ValidateSlotNumber(int slotNumber, int min, int max, string paramName)
    {
        if (slotNumber < min || slotNumber > max)
        {
            throw new ArgumentOutOfRangeException(paramName, slotNumber, $"Must be {min}-{max}.");
        }
    }
}
