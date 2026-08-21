using System;
using System.Collections.Generic;
using System.Linq;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Applies a single-field patch to an already-captured
/// <see cref="RadioCodeplugRawSnapshot"/>, reusing the existing, already-
/// tested <see cref="ChannelCodec.Encode"/>/<see cref="EncryptionKeyCodec"/>
/// RMW functions - the only two entities with any patch support. Every other
/// captured region is left byte-for-byte untouched, exactly as the snapshot
/// read it.
///
/// Deliberately narrow: only patches a field WITHIN an already-populated
/// record that the snapshot already captured (an existing channel, an
/// existing encryption key slot) - unless <see cref="ApplyChannelPatch"/> is
/// asked to create a brand-new channel (see that method's own doc comment).
/// </summary>
public static class RadioCodeplugPatcher
{
    /// <summary>
    /// Patches an existing channel's fields, OR creates a genuinely new one
    /// if <paramref name="radioIndex"/> isn't populated yet. The latter
    /// requires the caller to have captured the snapshot with
    /// <paramref name="radioIndex"/> passed as one of
    /// <see cref="RadioCodeplugRawSnapshotReader.Capture"/>'s
    /// <c>additionalChannelIndices</c> - otherwise there's no captured
    /// region to safely read-modify-write against (this app never
    /// fabricates bytes it hasn't actually read from the radio), and this
    /// throws the same as before. When creating a new channel, also flips
    /// the corresponding bit in the <see cref="D890UvMemoryMap.ChannelSet"/>
    /// presence bitmap - without that, the radio would silently never
    /// recognize the channel as populated, no matter how correct its
    /// record's bytes are (the failure mode the original, retired
    /// <c>RadioChannelWriter</c> had, since it never touched the bitmap
    /// either).
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyChannelPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, ChannelCodec.ChannelFieldPatch patch)
    {
        var address = ChannelAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.ChannelSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, ChannelCodec.RecordLength, current => ChannelCodec.Encode(current, patch));
    }

    /// <summary>
    /// Patches an existing zone's fields, OR creates a genuinely new one if
    /// <paramref name="radioIndex"/> isn't populated yet - same requirement
    /// as <see cref="ApplyChannelPatch"/>: the snapshot must already have
    /// captured this zone's Name/ChannelMembers regions (A-Channel/B-Channel/
    /// Hide are always captured in full for every possible zone slot
    /// regardless - see <see cref="RadioCodeplugRawSnapshotReader"/>). Unlike
    /// Channel, a zone's 4 fields are 4 completely independent arrays with no
    /// shared bytes/bits, so each patch is applied separately rather than
    /// needing one combined record encode.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyZonePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, ZoneCodec.ZoneFieldPatch patch)
    {
        var result = SetBit(snapshot, D890UvMemoryMap.ZoneSet, radioIndex, true);

        if (patch.Name is { } name)
        {
            var address = D890UvMemoryMap.ZonesName + radioIndex * D890UvMemoryMap.ZoneDataOffset;
            result = ApplyPatch(result, address, D890UvMemoryMap.ZoneDataLength, _ => ZoneCodec.EncodeName(name));
        }

        if (patch.ChannelMembers is { } members)
        {
            var address = D890UvMemoryMap.ZoneChannels + radioIndex * ZoneChannelsRecordLength;
            result = ApplyPatch(result, address, ZoneChannelsRecordLength, _ => ZoneCodec.EncodeChannelMembers(members));
        }

        if (patch.AChannelIndex is { } aChannelIndex)
        {
            var address = D890UvMemoryMap.ZoneAChannel + radioIndex * 2;
            result = ApplyPatch(result, address, 2, _ => ZoneCodec.EncodeChannelIndex(aChannelIndex));
        }

        if (patch.BChannelIndex is { } bChannelIndex)
        {
            var address = D890UvMemoryMap.ZoneBChannel + radioIndex * 2;
            result = ApplyPatch(result, address, 2, _ => ZoneCodec.EncodeChannelIndex(bChannelIndex));
        }

        if (patch.IsHidden is { } isHidden)
        {
            result = SetBit(result, D890UvMemoryMap.ZoneHide, radioIndex, isHidden);
        }

        return result;
    }

    /// <summary>
    /// Deletes a zone: blanks its name and channel-membership records (all
    /// 0xFF, matching the erased-flash convention <c>ZoneCodec.DecodeName</c>/
    /// <c>DecodeChannelMembers</c> both already treat as "nothing here"),
    /// resets A/B-channel to the "no channel" sentinel (0xFFFF), and clears
    /// the presence bit - the same reasoning as <see cref="ApplyChannelDelete"/>:
    /// a deleted zone shouldn't leave any of its old field values behind.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyZoneDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var nameAddress = D890UvMemoryMap.ZonesName + radioIndex * D890UvMemoryMap.ZoneDataOffset;
        var result = ApplyPatch(snapshot, nameAddress, D890UvMemoryMap.ZoneDataLength, _ => Enumerable.Repeat((byte)0xFF, D890UvMemoryMap.ZoneDataLength).ToArray());

        var membersAddress = D890UvMemoryMap.ZoneChannels + radioIndex * ZoneChannelsRecordLength;
        result = ApplyPatch(result, membersAddress, ZoneChannelsRecordLength, _ => Enumerable.Repeat((byte)0xFF, ZoneChannelsRecordLength).ToArray());

        var aChannelAddress = D890UvMemoryMap.ZoneAChannel + radioIndex * 2;
        result = ApplyPatch(result, aChannelAddress, 2, _ => ZoneCodec.EncodeChannelIndex(0xFFFF));

        var bChannelAddress = D890UvMemoryMap.ZoneBChannel + radioIndex * 2;
        result = ApplyPatch(result, bChannelAddress, 2, _ => ZoneCodec.EncodeChannelIndex(0xFFFF));

        return SetBit(result, D890UvMemoryMap.ZoneSet, radioIndex, false);
    }

    /// <summary>Full 0x200-byte channel-membership record length - matches
    /// <see cref="RadioCodeplugRawSnapshotReader"/>'s ZoneChannelsRecordBytes
    /// and <see cref="ZoneCodec"/>'s own 256-slot capacity.</summary>
    private const int ZoneChannelsRecordLength = 0x200;

    /// <summary>
    /// Patches an existing scan list's fields, OR creates a genuinely new
    /// one if <paramref name="radioIndex"/> isn't populated yet - same
    /// requirement as <see cref="ApplyChannelPatch"/>: the snapshot must
    /// already have captured this scan list's record (see
    /// <see cref="RadioCodeplugRawSnapshotReader.AddMissingScanLists"/>).
    /// Unlike Channel, every field here is unconditionally re-encoded from
    /// <paramref name="values"/> rather than patched per-dirty-field - see
    /// <see cref="ScanListCodec.Encode"/>'s doc comment for why that's safe.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyScanListPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, ScanListCodec.DecodedScanList values)
    {
        var address = ScanListAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.ScanListSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, ScanListCodec.RecordLength, current => ScanListCodec.Encode(current, values));
    }

    /// <summary>
    /// Deletes a scan list: blanks its whole record to 0xFF (matching the
    /// same erased-flash convention <see cref="ApplyChannelDelete"/> uses)
    /// and clears its presence bit.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyScanListDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = ScanListAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, ScanListCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, ScanListCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.ScanListSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Scan Lists (ScanListData + idx*ScanListDataOffset).</summary>
    public static int ScanListAddress(int radioIndex) => D890UvMemoryMap.ScanListData + radioIndex * D890UvMemoryMap.ScanListDataOffset;

    /// <summary>Patches a Radio ID's DMR ID/Name - same shared bitmap+data
    /// pattern as <see cref="ApplyScanListPatch"/>, including needing the
    /// snapshot to already have this record captured for a brand-new row
    /// (see <see cref="RadioCodeplugRawSnapshotReader.AddMissingRadioIds"/>).</summary>
    public static RadioCodeplugRawSnapshot ApplyRadioIdPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, RadioIdCodec.DecodedRadioId values)
    {
        var address = RadioIdAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.RadioIdSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, RadioIdCodec.RecordLength, current => RadioIdCodec.Encode(current, values));
    }

    /// <summary>Deletes a Radio ID: blanks its whole record to 0xFF (same
    /// erased-flash convention as <see cref="ApplyScanListDelete"/>) and
    /// clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyRadioIdDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = RadioIdAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, RadioIdCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, RadioIdCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.RadioIdSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Radio IDs (RadioIdData + idx*RadioIdDataOffset).</summary>
    public static int RadioIdAddress(int radioIndex) => D890UvMemoryMap.RadioIdData + radioIndex * D890UvMemoryMap.RadioIdDataOffset;

    /// <summary>Patches a Talkgroup's DMR ID/Name/CallType/CallAlert - same
    /// shared bitmap+data pattern as <see cref="ApplyRadioIdPatch"/>, except
    /// the Talkgroup presence bitmap is INVERTED (bit UNSET = present, see
    /// <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// invertedBitmap:true for TalkgroupSet) - so "present" here means
    /// clearing the bit, not setting it.</summary>
    public static RadioCodeplugRawSnapshot ApplyTalkgroupPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, TalkgroupCodec.DecodedTalkgroup values)
    {
        var address = TalkgroupAddress(radioIndex);
        var withBitCleared = SetBit(snapshot, D890UvMemoryMap.TalkgroupSet, radioIndex, false);
        return ApplyPatch(withBitCleared, address, TalkgroupCodec.RecordLength, current => TalkgroupCodec.Encode(current, values));
    }

    /// <summary>Deletes a Talkgroup: blanks its whole record to 0xFF (same
    /// erased-flash convention as <see cref="ApplyRadioIdDelete"/>) and SETS
    /// its presence bit (inverted bitmap - set means absent here).</summary>
    public static RadioCodeplugRawSnapshot ApplyTalkgroupDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = TalkgroupAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, TalkgroupCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, TalkgroupCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.TalkgroupSet, radioIndex, true);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Talkgroups (TalkgroupData + idx*TalkgroupDataOffset).</summary>
    public static int TalkgroupAddress(int radioIndex) => D890UvMemoryMap.TalkgroupData + radioIndex * D890UvMemoryMap.TalkgroupDataOffset;

    /// <summary>Patches a Receive Group List's Name/member talkgroups - same
    /// shared bitmap+data pattern as <see cref="ApplyRadioIdPatch"/>
    /// (ReceiveGroupSet is a normal, non-inverted bitmap, unlike Talkgroup's) -
    /// confirmed via a live differential write capture 2026-08-08, see
    /// ReceiveGroupListCodec's own doc comment for the byte-level findings.</summary>
    public static RadioCodeplugRawSnapshot ApplyReceiveGroupListPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, ReceiveGroupListCodec.DecodedReceiveGroupList values)
    {
        var address = ReceiveGroupListAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.ReceiveGroupSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, ReceiveGroupListCodec.RecordLength, current => ReceiveGroupListCodec.Encode(current, values));
    }

    /// <summary>Deletes a Receive Group List: blanks its whole record to
    /// 0xFF (same erased-flash convention as <see cref="ApplyRadioIdDelete"/>)
    /// and clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyReceiveGroupListDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = ReceiveGroupListAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, ReceiveGroupListCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, ReceiveGroupListCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.ReceiveGroupSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Receive Group Lists (ReceiveGroupData + idx*ReceiveGroupDataOffset).</summary>
    public static int ReceiveGroupListAddress(int radioIndex) => D890UvMemoryMap.ReceiveGroupData + radioIndex * D890UvMemoryMap.ReceiveGroupDataOffset;

    /// <summary>Patches a Roaming Channel's RX/TX frequency/Color Code/Slot/
    /// Name - same shared bitmap+data pattern as <see cref="ApplyRadioIdPatch"/>
    /// (RoamingChannelSet is a normal, non-inverted bitmap, unlike Talkgroup's).</summary>
    public static RadioCodeplugRawSnapshot ApplyRoamingChannelPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, RoamingChannelCodec.DecodedRoamingChannel values)
    {
        var address = RoamingChannelAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.RoamingChannelSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, RoamingChannelCodec.RecordLength, current => RoamingChannelCodec.Encode(current, values));
    }

    /// <summary>Deletes a Roaming Channel: blanks its whole record to 0xFF
    /// (same erased-flash convention as <see cref="ApplyRadioIdDelete"/>)
    /// and clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyRoamingChannelDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = RoamingChannelAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, RoamingChannelCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, RoamingChannelCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.RoamingChannelSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Roaming Channels (RoamingChannelData + idx*RoamingChannelDataOffset).</summary>
    public static int RoamingChannelAddress(int radioIndex) => D890UvMemoryMap.RoamingChannelData + radioIndex * D890UvMemoryMap.RoamingChannelDataOffset;

    /// <summary>Patches a Roaming Zone's member list/Name - same shared
    /// bitmap+data pattern as <see cref="ApplyRoamingChannelPatch"/>
    /// (RoamingZoneSet is a normal, non-inverted bitmap). Confirmed
    /// 2026-08-10 via live differential write - see RoamingZoneCodec's own
    /// doc comment.</summary>
    public static RadioCodeplugRawSnapshot ApplyRoamingZonePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, RoamingZoneCodec.DecodedRoamingZone values)
    {
        var address = RoamingZoneAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.RoamingZoneSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, RoamingZoneCodec.RecordLength, current => RoamingZoneCodec.Encode(current, values));
    }

    /// <summary>Deletes a Roaming Zone: blanks its whole record to 0xFF
    /// (same erased-flash convention as <see cref="ApplyRoamingChannelDelete"/>)
    /// and clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyRoamingZoneDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = RoamingZoneAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, RoamingZoneCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, RoamingZoneCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.RoamingZoneSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Roaming Zones (RoamingZoneData + idx*RoamingZoneDataOffset).</summary>
    public static int RoamingZoneAddress(int radioIndex) => D890UvMemoryMap.RoamingZoneData + radioIndex * D890UvMemoryMap.RoamingZoneDataOffset;

    /// <summary>Rewrites the ENTIRE Talkgroup Whitelist region from the
    /// current in-memory list - see <see cref="TalkgroupWhitelistCodec.EncodeAll"/>'s
    /// own doc comment for why a whole-region rewrite (never a per-entry
    /// patch) is correct here: entries are packed with no gaps regardless of
    /// row number, so any add/edit/delete shifts everything after it.</summary>
    public static RadioCodeplugRawSnapshot ApplyTalkgroupWhitelistPatch(RadioCodeplugRawSnapshot snapshot, IReadOnlyList<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries) =>
        ApplyPatch(snapshot, D890UvMemoryMap.TalkgroupWhitelistData, TalkgroupWhitelistCodec.MaxBlocks * TalkgroupWhitelistCodec.BlockLength, _ => TalkgroupWhitelistCodec.EncodeAll(entries));

    /// <summary>Same shape as <see cref="ApplyTalkgroupWhitelistPatch"/>, a
    /// different base address and distinct list in the vendor CPS - byte-
    /// for-byte identical wire format (see <see cref="TalkgroupWhitelistCodec"/>'s
    /// own doc comment).</summary>
    public static RadioCodeplugRawSnapshot ApplyDigitalContactWhitelistPatch(RadioCodeplugRawSnapshot snapshot, IReadOnlyList<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DigitalContactWhitelistData, TalkgroupWhitelistCodec.MaxBlocks * TalkgroupWhitelistCodec.BlockLength, _ => TalkgroupWhitelistCodec.EncodeAll(entries));

    /// <summary>Patches the single Master ID record - one standalone
    /// record, same reasoning as Qdc1200SettingsCodec (only one Master ID
    /// exists on the radio).</summary>
    public static RadioCodeplugRawSnapshot ApplyMasterIdPatch(RadioCodeplugRawSnapshot snapshot, MasterIdCodec.DecodedMasterId values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.MasterIdData, MasterIdCodec.RecordLength, current => MasterIdCodec.Encode(current, values));
    }

    /// <summary>
    /// Patches an AM Air channel's Frequency/Name (see <see cref="ApplyScanListPatch"/>
    /// for the shared bitmap+data pattern). The special always-present VFO
    /// row (<see cref="AmAirCodec.VfoIndex"/>) never reaches here - it's
    /// excluded before it ever becomes an editable row, see
    /// RadioReadMapper.MapAmAir's doc comment. Every field is unconditionally
    /// re-encoded, same safety reasoning as ScanListCodec.Encode's doc
    /// comment (no bit-sharing between Frequency/Name).</summary>
    public static RadioCodeplugRawSnapshot ApplyAmAirPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, AmAirCodec.DecodedAmAir values)
    {
        var address = AmAirAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.AmAirSet, radioIndex, true);
        return ApplyPatch(withBitSet, address, AmAirCodec.RecordLength, current => AmAirCodec.Encode(current, values));
    }

    /// <summary>
    /// Deletes a regular AM Air channel (blanks to 0xFF, clears its presence
    /// bit - same convention as <see cref="ApplyScanListDelete"/>).</summary>
    public static RadioCodeplugRawSnapshot ApplyAmAirDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = AmAirAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, AmAirCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, AmAirCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.AmAirSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for AM Air's regular (non-VFO) slots.</summary>
    public static int AmAirAddress(int radioIndex) => D890UvMemoryMap.AmAirData + radioIndex * D890UvMemoryMap.AmAirDataStride;

    /// <summary>
    /// Patches an Analog Address Book entry - the record itself plus its
    /// id-list byte (D890UvMemoryMap.AnalogBookId + radioIndex), confirmed
    /// 2026-08-04 via a live differential write to be a plain "byte value
    /// equals its own position, else 0xFF" presence marker, NOT a bitmap
    /// (see AnalogAddressCodec's doc comment) - so this uses a direct
    /// single-byte ApplyPatch rather than SetBit.</summary>
    public static RadioCodeplugRawSnapshot ApplyAnalogAddressPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, AnalogAddressCodec.DecodedAnalogAddress values)
    {
        var address = AnalogAddressAddress(radioIndex);
        var withIdSet = SetAnalogAddressIdByte(snapshot, radioIndex, populated: true);
        return ApplyPatch(withIdSet, address, AnalogAddressCodec.RecordLength, current => AnalogAddressCodec.Encode(current, values));
    }

    /// <summary>
    /// Deletes an Analog Address Book entry (blanks to 0xFF, clears its
    /// id-list byte back to 0xFF - same convention as
    /// <see cref="ApplyAmAirDelete"/>).</summary>
    public static RadioCodeplugRawSnapshot ApplyAnalogAddressDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = AnalogAddressAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, AnalogAddressCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, AnalogAddressCodec.RecordLength).ToArray());
        return SetAnalogAddressIdByte(blanked, radioIndex, populated: false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for Analog Address Book slots.</summary>
    public static int AnalogAddressAddress(int radioIndex) => D890UvMemoryMap.AnalogBookData + radioIndex * D890UvMemoryMap.AnalogBookDataStride;

    private static RadioCodeplugRawSnapshot SetAnalogAddressIdByte(RadioCodeplugRawSnapshot snapshot, int radioIndex, bool populated)
    {
        var idAddress = D890UvMemoryMap.AnalogBookId + radioIndex;
        return ApplyPatch(snapshot, idAddress, 1, _ => [populated ? (byte)radioIndex : (byte)0xFF]);
    }

    /// <summary>
    /// Patches an AM Zone's Name/Members (the shared 0x80-byte record) plus
    /// its separately-addressed AChannel/ScanChannel fields - same multi-
    /// region pattern as <see cref="ApplyZonePatch"/>, confirmed write-safe
    /// 2026-08-02 (see AmZoneCodec's doc comment for the live differential
    /// test covering all four fields at once).</summary>
    public static RadioCodeplugRawSnapshot ApplyAmZonePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, AmZoneCodec.DecodedAmZone values)
    {
        var address = AmZoneAddress(radioIndex);
        var result = SetBit(snapshot, D890UvMemoryMap.AmZoneSet, radioIndex, true);
        result = ApplyPatch(result, address, AmZoneCodec.RecordLength, current => AmZoneCodec.Encode(current, values));

        var aChannelAddress = D890UvMemoryMap.AmZoneAChannel + radioIndex * 2;
        result = ApplyPatch(result, aChannelAddress, 2, _ => AmZoneCodec.EncodeAChannelIndex(values.AChannelIndex));

        var scanChannelAddress = D890UvMemoryMap.AmZoneScan + radioIndex * D890UvMemoryMap.AmZoneScanStride;
        result = ApplyPatch(result, scanChannelAddress, D890UvMemoryMap.AmZoneScanLength, _ => AmZoneCodec.EncodeScanChannelBitmask(values.ScanChannelIndexes));

        return result;
    }

    /// <summary>
    /// Deletes an AM Zone: blanks its record and scan-channel bitmask to
    /// 0xFF, resets AChannel to the "no channel" sentinel (0xFFFF), and
    /// clears its presence bit - same conventions as
    /// <see cref="ApplyZoneDelete"/>.</summary>
    public static RadioCodeplugRawSnapshot ApplyAmZoneDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = AmZoneAddress(radioIndex);
        var result = ApplyPatch(snapshot, address, AmZoneCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, AmZoneCodec.RecordLength).ToArray());

        var aChannelAddress = D890UvMemoryMap.AmZoneAChannel + radioIndex * 2;
        result = ApplyPatch(result, aChannelAddress, 2, _ => AmZoneCodec.EncodeAChannelIndex(0xFFFF));

        // 0x00, not 0xFF - a set bit means "included" in this bitmask
        // (unlike the member list's 0xFFFF="empty" sentinel), so an all-zero
        // bitmask is the correct "no scan channels" state, not all-0xFF
        // (which would mean every possible index is a member).
        var scanChannelAddress = D890UvMemoryMap.AmZoneScan + radioIndex * D890UvMemoryMap.AmZoneScanStride;
        result = ApplyPatch(result, scanChannelAddress, D890UvMemoryMap.AmZoneScanLength, _ => new byte[D890UvMemoryMap.AmZoneScanLength]);

        return SetBit(result, D890UvMemoryMap.AmZoneSet, radioIndex, false);
    }

    /// <summary>Matches <see cref="RadioCodeplugRawSnapshotReader"/>'s own
    /// address computation for AM Zone's main record.</summary>
    public static int AmZoneAddress(int radioIndex) => D890UvMemoryMap.AmZoneData + radioIndex * D890UvMemoryMap.AmZoneDataStride;

    /// <summary>Patches a single Prefabricated SMS slot's text record.
    /// Confirmed write-safe 2026-08-03 - see PrefabricatedSmsCodec's doc
    /// comment. The used-slot chain itself is a completely separate write,
    /// see <see cref="ApplyPrefabricatedSmsSetChain"/> - this only ever
    /// touches the text, never the chain.</summary>
    public static RadioCodeplugRawSnapshot ApplyPrefabricatedSmsTextPatch(RadioCodeplugRawSnapshot snapshot, int slotId, string text)
    {
        var address = PrefabricatedSmsCodec.ComputeAddress(slotId);
        return ApplyPatch(snapshot, address, D890UvMemoryMap.PrefabSmsDataLength, _ => PrefabricatedSmsCodec.Encode(text));
    }

    /// <summary>Blanks a deleted Prefabricated SMS slot's text record to
    /// 0xFF (matching the erased-flash convention used everywhere else) -
    /// purely for tidiness, since a slot excluded from the chain (see
    /// <see cref="ApplyPrefabricatedSmsSetChain"/>) is never read again
    /// regardless of what's left in its old text record.</summary>
    public static RadioCodeplugRawSnapshot ApplyPrefabricatedSmsDelete(RadioCodeplugRawSnapshot snapshot, int slotId)
    {
        var address = PrefabricatedSmsCodec.ComputeAddress(slotId);
        return ApplyPatch(snapshot, address, D890UvMemoryMap.PrefabSmsDataLength, _ => Enumerable.Repeat((byte)0xFF, D890UvMemoryMap.PrefabSmsDataLength).ToArray());
    }

    /// <summary>Rewrites the ENTIRE used-slot linked list as nodes
    /// 0..sortedSlotIds.Count-1, one <see cref="ApplyPatch"/> call per node
    /// (each node is independently addressed and independently captured by
    /// <see cref="RadioCodeplugRawSnapshotReader.CapturePrefabricatedSms"/> -
    /// NOT one contiguous region, so this can't be a single combined-block
    /// patch like most other entities' "always re-encode everything"
    /// writes). Confirmed write-safe 2026-08-03 via a live differential
    /// write - see PrefabricatedSmsCodec's doc comment. Callers must ensure
    /// every node address 0..count-1 is already captured (see
    /// <see cref="RadioCodeplugRawSnapshotReader.AddMissingPrefabricatedSms"/>)
    /// before calling this - a growing chain needs node addresses beyond
    /// whatever the last read's own (shorter) walk happened to capture.</summary>
    public static RadioCodeplugRawSnapshot ApplyPrefabricatedSmsSetChain(RadioCodeplugRawSnapshot snapshot, IReadOnlyList<int> sortedSlotIds)
    {
        var result = snapshot;
        for (var i = 0; i < sortedSlotIds.Count; i++)
        {
            var address = D890UvMemoryMap.PrefabSmsSet + i * PrefabricatedSmsCodec.SetEntryLength;
            var next = i == sortedSlotIds.Count - 1 ? PrefabricatedSmsCodec.EndMarker : (byte)(i + 1);
            var id = (byte)sortedSlotIds[i];
            result = ApplyPatch(result, address, PrefabricatedSmsCodec.SetEntryLength, _ => PrefabricatedSmsCodec.EncodeSetNode(next, id));
        }

        return result;
    }

    /// <summary>
    /// Patches an FM broadcast channel's record plus its active/scan bits.
    /// Unlike AM Air/AM Zone's own dedicated bitmap regions, FM's active and
    /// scan bits live inside the shared <see cref="D890UvMemoryMap.FmMeta"/>
    /// block (which also holds the always-present "home" channel's own
    /// record) - <see cref="SetBit"/> still works unmodified since it finds
    /// whichever captured region contains the target address, and FmMeta is
    /// always captured whole regardless of which channels are active. See
    /// FmChannelCodec.Encode's doc comment for the confirming live write.</summary>
    public static RadioCodeplugRawSnapshot ApplyFmChannelPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, FmChannelCodec.DecodedFmChannel values)
    {
        var address = FmChannelAddress(radioIndex);
        var result = SetBit(snapshot, D890UvMemoryMap.FmMeta + D890UvMemoryMap.FmActiveMaskOffset, radioIndex, true);
        result = SetBit(result, D890UvMemoryMap.FmMeta + D890UvMemoryMap.FmScanMaskOffset, radioIndex, values.ScanAdd);
        return ApplyPatch(result, address, FmChannelCodec.RecordLength, current => FmChannelCodec.Encode(current, values));
    }

    /// <summary>
    /// Deletes an FM broadcast channel (blanks to 0xFF, clears both its
    /// active and scan bits within the shared FmMeta block).</summary>
    public static RadioCodeplugRawSnapshot ApplyFmChannelDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = FmChannelAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, FmChannelCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, FmChannelCodec.RecordLength).ToArray());
        var result = SetBit(blanked, D890UvMemoryMap.FmMeta + D890UvMemoryMap.FmActiveMaskOffset, radioIndex, false);
        return SetBit(result, D890UvMemoryMap.FmMeta + D890UvMemoryMap.FmScanMaskOffset, radioIndex, false);
    }

    public static int FmChannelAddress(int radioIndex) => D890UvMemoryMap.FmChannelData + radioIndex * D890UvMemoryMap.FmChannelDataStride;

    /// <summary>
    /// Patches a single Auto Repeater Offset slot. Unlike every other
    /// entity, there's no presence bitmap to set - see
    /// AutoRepeaterOffsetCodec's doc comment for the confirming live write -
    /// so this is just a plain 4-byte record patch.</summary>
    public static RadioCodeplugRawSnapshot ApplyAutoRepeaterOffsetPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, double offsetFrequencyMhz)
    {
        var address = AutoRepeaterOffsetAddress(radioIndex);
        return ApplyPatch(snapshot, address, AutoRepeaterOffsetCodec.RecordLength, _ => AutoRepeaterOffsetCodec.Encode(offsetFrequencyMhz));
    }

    /// <summary>Deletes an Auto Repeater Offset slot - writes the same
    /// all-zero bytes an unused slot already reads back as (confirmed live,
    /// see AutoRepeaterOffsetCodec's doc comment), NOT the usual 0xFF-erased
    /// convention every other entity's delete uses.</summary>
    public static RadioCodeplugRawSnapshot ApplyAutoRepeaterOffsetDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = AutoRepeaterOffsetAddress(radioIndex);
        return ApplyPatch(snapshot, address, AutoRepeaterOffsetCodec.RecordLength, _ => AutoRepeaterOffsetCodec.Encode(0.0));
    }

    public static int AutoRepeaterOffsetAddress(int radioIndex) => D890UvMemoryMap.AutoRepeaterData + radioIndex * AutoRepeaterOffsetCodec.RecordLength;

    /// <summary>
    /// Patches a QDC 1200 ID table entry - a flat array, no bitmap or
    /// presence list found anywhere nearby in either live capture (see
    /// Qdc1200IdCodec's own doc comment), so no SetBit call is needed
    /// here, unlike AM Air/Analog Address Book.</summary>
    public static RadioCodeplugRawSnapshot ApplyQdc1200IdPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, Qdc1200IdCodec.DecodedQdc1200Id values)
    {
        var address = Qdc1200IdAddress(radioIndex);
        return ApplyPatch(snapshot, address, Qdc1200IdCodec.RecordLength, current => Qdc1200IdCodec.Encode(current, values));
    }

    /// <summary>Deletes a QDC 1200 ID table entry - writes all-zero bytes,
    /// same "flat array, zero not 0xFF" convention as
    /// <see cref="ApplyAutoRepeaterOffsetDelete"/> (this codec's own
    /// RadioCodeplugReader.ReadQdc1200Ids treats a blank Name, which an
    /// all-zero record decodes to, as "unconfigured").</summary>
    public static RadioCodeplugRawSnapshot ApplyQdc1200IdDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = Qdc1200IdAddress(radioIndex);
        return ApplyPatch(snapshot, address, Qdc1200IdCodec.RecordLength, _ => new byte[Qdc1200IdCodec.RecordLength]);
    }

    public static int Qdc1200IdAddress(int radioIndex) => D890UvMemoryMap.Qdc1200IdData + radioIndex * Qdc1200IdCodec.RecordLength;

    /// <summary>
    /// Patches the QDC 1200 Setting singleton record - no bitmap, no
    /// per-index addressing (there's only ever one), same shape as
    /// <see cref="ApplyAutoRepeaterOffsetPatch"/> minus the index math.</summary>
    /// <summary>Patches one of the 32 fixed GPS Roaming slots - no bitmap,
    /// no delete (see GpsRoamingEntry's own doc comment: every slot always
    /// exists, "removing" one from this app's own list just means it's
    /// skipped this write, not reset). Confirmed write-safe 2026-08-09 -
    /// see GpsRoamingCodec's own doc comment for the live-found second-half
    /// addressing bug this fix depends on.</summary>
    public static RadioCodeplugRawSnapshot ApplyGpsRoamingPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, GpsRoamingCodec.DecodedGpsRoaming values)
    {
        var address = D890UvMemoryMap.GpsRoamingData + GpsRoamingCodec.OffsetForIndex(radioIndex);
        return ApplyPatch(snapshot, address, GpsRoamingCodec.RecordLength, _ => GpsRoamingCodec.Encode(values));
    }

    public static RadioCodeplugRawSnapshot ApplyQdc1200SettingsPatch(RadioCodeplugRawSnapshot snapshot, Qdc1200SettingsCodec.DecodedQdc1200Settings values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.Qdc1200SettingsData, Qdc1200SettingsCodec.RecordLength, current => Qdc1200SettingsCodec.Encode(current, values));
    }

    /// <summary>Patches an Analog Quick Call slot - a flat array, no
    /// bitmap (see AnalogQuickCallCodec's own doc comment), same shape as
    /// <see cref="ApplyQdc1200IdPatch"/>.</summary>
    public static RadioCodeplugRawSnapshot ApplyAnalogQuickCallPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, AnalogQuickCallCodec.DecodedAnalogQuickCall values)
    {
        var address = AnalogQuickCallAddress(radioIndex);
        return ApplyPatch(snapshot, address, AnalogQuickCallCodec.RecordLength, _ => AnalogQuickCallCodec.Encode(values));
    }

    /// <summary>Deletes an Analog Quick Call slot - writes Operation
    /// Type=Off/Call Id=0xFF, the same "unconfigured" byte pair
    /// RadioCodeplugReader.ReadAnalogQuickCalls already treats as absent
    /// (OperationType != 0 is its own presence check).</summary>
    public static RadioCodeplugRawSnapshot ApplyAnalogQuickCallDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = AnalogQuickCallAddress(radioIndex);
        return ApplyPatch(snapshot, address, AnalogQuickCallCodec.RecordLength, _ => AnalogQuickCallCodec.Encode(new AnalogQuickCallCodec.DecodedAnalogQuickCall(radioIndex)));
    }

    public static int AnalogQuickCallAddress(int radioIndex) => D890UvMemoryMap.AnalogQuickCallData + radioIndex * AnalogQuickCallCodec.RecordLength;

    /// <summary>Patches a State Information slot - a flat array, no
    /// bitmap (see StateInformationCodec's own doc comment).</summary>
    public static RadioCodeplugRawSnapshot ApplyStateInformationPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, string content)
    {
        var address = StateInformationAddress(radioIndex);
        return ApplyPatch(snapshot, address, StateInformationCodec.RecordLength, _ => StateInformationCodec.Encode(content));
    }

    /// <summary>Deletes a State Information slot - writes a blank (all-zero)
    /// name buffer, the same "unconfigured" state TextFieldCodec.DecodeName
    /// already treats as an empty string.</summary>
    public static RadioCodeplugRawSnapshot ApplyStateInformationDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = StateInformationAddress(radioIndex);
        return ApplyPatch(snapshot, address, StateInformationCodec.RecordLength, _ => StateInformationCodec.Encode(""));
    }

    public static int StateInformationAddress(int radioIndex) => D890UvMemoryMap.StateInformationData + radioIndex * StateInformationCodec.RecordLength;

    /// <summary>Patches a Hot Key record - a flat, fixed 18-record array,
    /// no bitmap and no delete (the 18 rows are never added/removed, only
    /// edited - see HotKeyEntry's own doc comment).</summary>
    public static RadioCodeplugRawSnapshot ApplyHotKeyPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, HotKeyCodec.DecodedHotKey values)
    {
        var address = HotKeyAddress(radioIndex);
        return ApplyPatch(snapshot, address, HotKeyCodec.RecordLength, current => HotKeyCodec.Encode(current, values));
    }

    public static int HotKeyAddress(int radioIndex) => D890UvMemoryMap.HotKeyData + radioIndex * HotKeyCodec.RecordLength;

    /// <summary>Patches a QDC Address Book entry - a flat array, no
    /// presence bitmap (see QdcAddressCodec's own doc comment).</summary>
    public static RadioCodeplugRawSnapshot ApplyQdcAddressPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, QdcAddressCodec.DecodedQdcAddress values)
    {
        var address = QdcAddressAddress(radioIndex);
        return ApplyPatch(snapshot, address, QdcAddressCodec.RecordLength, current => QdcAddressCodec.Encode(current, values));
    }

    /// <summary>Deletes a QDC Address Book entry - blanks the record to
    /// all-0xFF, NOT all-zero like Qdc1200IdCodec's own delete convention.
    /// Confirmed live 2026-08-04: an untouched slot on this entity's own
    /// address reads back as 0xFF, not 0x00 (see D890UvMemoryMap.QdcAddressData's
    /// own doc comment) - same erased-flash convention as Analog Address
    /// Book/AM Air, not Auto Repeater Offset/QDC 1200 ID's zero
    /// convention.</summary>
    public static RadioCodeplugRawSnapshot ApplyQdcAddressDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = QdcAddressAddress(radioIndex);
        var blank = new byte[QdcAddressCodec.RecordLength];
        Array.Fill(blank, (byte)0xFF);
        return ApplyPatch(snapshot, address, QdcAddressCodec.RecordLength, _ => blank);
    }

    public static int QdcAddressAddress(int radioIndex) => D890UvMemoryMap.QdcAddressData + radioIndex * QdcAddressCodec.RecordLength;

    /// <summary>Patches a 5Tone ID table row - has a REAL presence bitmap
    /// (unlike QDC Address Book/QDC 1200 ID above), confirmed 2026-08-06 as
    /// the singleton block's own byte 0 (one bit per row, NOT a row count -
    /// see D890UvMemoryMap's own doc comment). The whole 100-row table is
    /// always captured in full by RadioCodeplugRawSnapshot (see its own
    /// comment there for why), so a brand-new row's region is always
    /// already present - no "additionalIndices" plumbing needed the way
    /// ApplyChannelPatch's own new-channel support requires.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneIdPatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, FiveToneIdCodec.DecodedFiveToneId values)
    {
        var address = FiveToneIdAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.FiveToneDecodeEncodeData, radioIndex, true);
        return ApplyPatch(withBitSet, address, FiveToneIdCodec.RecordLength, current => FiveToneIdCodec.Encode(current, values));
    }

    /// <summary>Deletes a 5Tone ID table row - zeroes the record (matching
    /// FiveToneIdCodec.Decode's own "all-zero packed region = never
    /// configured" convention, NOT the 0xFF convention QDC Address Book/AM
    /// Air use) and clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneIdDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = FiveToneIdAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, FiveToneIdCodec.RecordLength, _ => new byte[FiveToneIdCodec.RecordLength]);
        return SetBit(blanked, D890UvMemoryMap.FiveToneDecodeEncodeData, radioIndex, false);
    }

    public static int FiveToneIdAddress(int radioIndex) => D890UvMemoryMap.FiveToneIdData + radioIndex * FiveToneIdCodec.RecordLength;

    /// <summary>Patches the Decode/Information ID/Encode singleton block -
    /// one standalone record, same reasoning as Qdc1200SettingsCodec (only
    /// one 5Tone Settings exists on the radio). Leaves the presence bitmap
    /// (byte 0) and the whole Information ID/Function1 sub-area past these
    /// offsets untouched - see FiveToneSettingsCodec's own doc comment for
    /// why that sub-area isn't covered at all.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneSettingsPatch(RadioCodeplugRawSnapshot snapshot, FiveToneSettingsCodec.DecodedFiveToneSettings values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.FiveToneDecodeEncodeData, D890UvMemoryMap.FiveToneDecodeEncodeRecordLength, current => FiveToneSettingsCodec.EncodeSingleton(current, values));
    }

    /// <summary>Patches PTT ID Starting (BOT) - lives INSIDE the
    /// FiveToneDecodeEncodeData singleton region (see
    /// D890UvMemoryMap.FiveToneBotSettingsData's own doc comment,
    /// corrected 2026-08-16), not a separate captured region of its own.
    /// ApplyPatch finds the containing region and splices just this
    /// sub-range, same as every other sub-record patch in this
    /// file.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneBotPatch(RadioCodeplugRawSnapshot snapshot, FiveToneSettingsCodec.DecodedFiveToneBotEot values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.FiveToneBotSettingsData, D890UvMemoryMap.FiveToneBotSettingsLength, current => FiveToneSettingsCodec.EncodeBot(current, values));
    }

    /// <summary>Patches PTT ID Ending (EOT) - see <see cref="ApplyFiveToneBotPatch"/>.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneEotPatch(RadioCodeplugRawSnapshot snapshot, FiveToneSettingsCodec.DecodedFiveToneBotEot values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.FiveToneEotData, D890UvMemoryMap.FiveToneEotRecordLength, current => FiveToneSettingsCodec.EncodeEot(current, values));
    }

    /// <summary>Patches one Information ID / Information Code Function1
    /// slot - <paramref name="slotIndex"/> is 0-based (Information ID
    /// NO. 1 = slot 0), caller's own responsibility to only call this for
    /// a row Number within D890UvMemoryMap.FiveToneInfoIdSlotCount (see
    /// that constant's own doc comment). No presence bitmap - the whole
    /// slot array is always captured in full (small, flat), same as Auto
    /// Repeater Offset/QDC 1200 ID.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneInfoIdSlotPatch(RadioCodeplugRawSnapshot snapshot, int slotIndex, FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot values)
    {
        var address = FiveToneInfoIdSlotAddress(slotIndex);
        return ApplyPatch(snapshot, address, FiveToneInfoIdSlotCodec.RecordLength, current => FiveToneInfoIdSlotCodec.Encode(current, values));
    }

    /// <summary>Clears a slot back to blank (Function Option/Function
    /// Decoding Response = 0, Information ID/Function Name empty) - used
    /// when a row that used to have a Number within the slot array's own
    /// range no longer does (deleted, or renumbered past
    /// FiveToneInfoIdSlotCount), so old data doesn't linger under a slot
    /// nothing points to anymore.</summary>
    public static RadioCodeplugRawSnapshot ApplyFiveToneInfoIdSlotClear(RadioCodeplugRawSnapshot snapshot, int slotIndex)
    {
        var address = FiveToneInfoIdSlotAddress(slotIndex);
        return ApplyPatch(snapshot, address, FiveToneInfoIdSlotCodec.RecordLength, _ => new byte[FiveToneInfoIdSlotCodec.RecordLength]);
    }

    public static int FiveToneInfoIdSlotAddress(int slotIndex) => D890UvMemoryMap.FiveToneInfoIdData + slotIndex * D890UvMemoryMap.FiveToneInfoIdSlotStride;

    /// <summary>Patches a 2Tone Encode table row - real presence bitmap
    /// confirmed live 2026-08-06 (unlike 5Tone's ID table/QDC 1200 ID,
    /// which have none), same SetBit pattern as AM Air/Channel/Zone.</summary>
    public static RadioCodeplugRawSnapshot ApplyTwoToneEncodePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, TwoToneEncodeCodec.DecodedTwoToneEncode values)
    {
        var address = TwoToneEncodeAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.TwoToneEncodeBitmap, radioIndex, true);
        return ApplyPatch(withBitSet, address, TwoToneEncodeCodec.RecordLength, current => TwoToneEncodeCodec.Encode(current, values));
    }

    /// <summary>Deletes a 2Tone Encode table row - zeroes the record
    /// (matching FiveToneIdCodec's own convention, not AM Air/QDC Address
    /// Book's 0xFF blanking) and clears its presence bit.</summary>
    public static RadioCodeplugRawSnapshot ApplyTwoToneEncodeDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = TwoToneEncodeAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, TwoToneEncodeCodec.RecordLength, _ => new byte[TwoToneEncodeCodec.RecordLength]);
        return SetBit(blanked, D890UvMemoryMap.TwoToneEncodeBitmap, radioIndex, false);
    }

    public static int TwoToneEncodeAddress(int radioIndex) => D890UvMemoryMap.TwoToneEncodeData + radioIndex * D890UvMemoryMap.TwoToneEncodeRecordLength;

    /// <summary>Patches a 2Tone Decode table row - see <see cref="ApplyTwoToneEncodePatch"/>.</summary>
    public static RadioCodeplugRawSnapshot ApplyTwoToneDecodePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, TwoToneDecodeCodec.DecodedTwoToneDecode values)
    {
        var address = TwoToneDecodeAddress(radioIndex);
        var withBitSet = SetBit(snapshot, D890UvMemoryMap.TwoToneDecodeBitmap, radioIndex, true);
        return ApplyPatch(withBitSet, address, TwoToneDecodeCodec.RecordLength, current => TwoToneDecodeCodec.Encode(current, values));
    }

    /// <summary>Deletes a 2Tone Decode table row - see <see cref="ApplyTwoToneEncodeDelete"/>.</summary>
    public static RadioCodeplugRawSnapshot ApplyTwoToneDecodeDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = TwoToneDecodeAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, TwoToneDecodeCodec.RecordLength, _ => new byte[TwoToneDecodeCodec.RecordLength]);
        return SetBit(blanked, D890UvMemoryMap.TwoToneDecodeBitmap, radioIndex, false);
    }

    public static int TwoToneDecodeAddress(int radioIndex) => D890UvMemoryMap.TwoToneDecodeData + radioIndex * D890UvMemoryMap.TwoToneDecodeRecordLength;

    /// <summary>Patches the Encode tab's scalar settings block - one
    /// standalone record, same reasoning as Qdc1200SettingsCodec/
    /// FiveToneSettingsCodec (only one 2Tone Encode Settings exists on the
    /// radio). Leaves the whole block's own bytes 0x00-0x08 and 0x0F
    /// untouched (unconfirmed/unused in every capture so far).</summary>
    public static RadioCodeplugRawSnapshot ApplyTwoToneEncodeSettingsPatch(RadioCodeplugRawSnapshot snapshot, TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.TwoToneEncodeSettingsData, TwoToneEncodeSettingsCodec.RecordLength, current => TwoToneEncodeSettingsCodec.Encode(current, values));
    }

    /// <summary>Patches DTMF Settings' own scalar fields - one standalone
    /// record, same reasoning as TwoToneEncodeSettingsCodec above.</summary>
    public static RadioCodeplugRawSnapshot ApplyDtmfSettingsPatch(RadioCodeplugRawSnapshot snapshot, DtmfSettingsCodec.DecodedDtmfSettings values)
    {
        return ApplyPatch(snapshot, D890UvMemoryMap.DtmfSettingsData, D890UvMemoryMap.DtmfSettingsRecordLength, current => DtmfSettingsCodec.EncodeSingleton(current, values));
    }

    /// <summary>Patches DTMF's PTT ID Starting (BOT) - a standalone code
    /// field, same raw-nibble-per-char encoding as M1-M16 (see
    /// DtmfCodeCodec). EOT/Remotely Kill/Remotely Stun below are the exact
    /// same shape, just different addresses.</summary>
    public static RadioCodeplugRawSnapshot ApplyDtmfBotPatch(RadioCodeplugRawSnapshot snapshot, string code) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DtmfBotData, D890UvMemoryMap.DtmfSettingsRecordLength, _ => DtmfSettingsCodec.EncodeCode(code, D890UvMemoryMap.DtmfSettingsRecordLength));

    public static RadioCodeplugRawSnapshot ApplyDtmfEotPatch(RadioCodeplugRawSnapshot snapshot, string code) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DtmfEotData, D890UvMemoryMap.DtmfSettingsRecordLength, _ => DtmfSettingsCodec.EncodeCode(code, D890UvMemoryMap.DtmfSettingsRecordLength));

    public static RadioCodeplugRawSnapshot ApplyDtmfRemotelyKillPatch(RadioCodeplugRawSnapshot snapshot, string code) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DtmfRemotelyKillData, D890UvMemoryMap.DtmfSettingsRecordLength, _ => DtmfSettingsCodec.EncodeCode(code, D890UvMemoryMap.DtmfSettingsRecordLength));

    public static RadioCodeplugRawSnapshot ApplyDtmfRemotelyStunPatch(RadioCodeplugRawSnapshot snapshot, string code) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DtmfRemotelyStunData, D890UvMemoryMap.DtmfSettingsRecordLength, _ => DtmfSettingsCodec.EncodeCode(code, D890UvMemoryMap.DtmfSettingsRecordLength));

    /// <summary>Patches one DTMF M1-M16 slot - no presence bitmap (fixed
    /// set, blank = all-0xFF), so no SetBit call needed, unlike 2Tone/5Tone.</summary>
    public static RadioCodeplugRawSnapshot ApplyDtmfEncodePatch(RadioCodeplugRawSnapshot snapshot, int radioIndex, string code)
    {
        var address = DtmfEncodeAddress(radioIndex);
        return ApplyPatch(snapshot, address, DtmfEncodeCodec.RecordLength, _ => DtmfEncodeCodec.Encode(code));
    }

    public static int DtmfEncodeAddress(int radioIndex) => D890UvMemoryMap.DtmfEncodeData + radioIndex * D890UvMemoryMap.DtmfEncodeRecordLength;

    /// <summary>Patches DTMF Transmitting Time's own standalone byte -
    /// completely separate address from the rest of DTMF Settings, see
    /// D890UvMemoryMap.DtmfTransmittingTimeIndexData's own doc comment.</summary>
    public static RadioCodeplugRawSnapshot ApplyDtmfTransmittingTimePatch(RadioCodeplugRawSnapshot snapshot, int index) =>
        ApplyPatch(snapshot, D890UvMemoryMap.DtmfTransmittingTimeIndexData, 1, _ => [DtmfSettingsCodec.EncodeTransmittingTimeIndex(index)]);

    /// <summary>Sets or clears a single bit within a bitmap region (channel/
    /// zone presence, zone hide-flags) - shared by every entity whose
    /// existence/state is tracked by one bit per index rather than a value
    /// field.</summary>
    private static RadioCodeplugRawSnapshot SetBit(RadioCodeplugRawSnapshot snapshot, int bitmapAddress, int index, bool value)
    {
        var byteIndex = index / 8;
        var bitIndex = index % 8;
        var mask = (byte)(1 << bitIndex);

        var region = snapshot.FindRegionContaining(bitmapAddress)
            ?? throw new InvalidOperationException($"Bitmap at 0x{bitmapAddress:X8} was not captured in this snapshot.");

        var offsetInRegion = bitmapAddress - region.Address + byteIndex;
        var currentlySet = (region.Data[offsetInRegion] & mask) != 0;
        if (currentlySet == value)
        {
            return snapshot;
        }

        var patchedBitmap = (byte[])region.Data.Clone();
        if (value)
        {
            patchedBitmap[offsetInRegion] |= mask;
        }
        else
        {
            patchedBitmap[offsetInRegion] &= (byte)~mask;
        }

        var patchedRegions = new List<CodeplugRawRegion>(snapshot.Regions.Count);
        foreach (var r in snapshot.Regions)
        {
            patchedRegions.Add(r.Address == region.Address ? new CodeplugRawRegion(r.Address, patchedBitmap) : r);
        }

        return new RadioCodeplugRawSnapshot { Regions = patchedRegions };
    }

    /// <summary>
    /// Deletes a channel: blanks its whole 128-byte record to 0xFF (matching
    /// the radio's own erased-flash convention - <c>ChannelCodec.Decode</c>
    /// treats an all-zero RX frequency, which this produces, as
    /// <c>IsBlank</c>) and clears its presence bit. Deliberately a full-record
    /// blank rather than a field patch - <see cref="ChannelCodec.Encode"/>'s
    /// RMW only ever touches confirmed fields, which can't express "erase
    /// everything", and a channel the user deleted should not leave any of
    /// its old field values behind for a later differential test to
    /// misread as live data.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyChannelDelete(RadioCodeplugRawSnapshot snapshot, int radioIndex)
    {
        var address = ChannelAddress(radioIndex);
        var blanked = ApplyPatch(snapshot, address, ChannelCodec.RecordLength, _ => Enumerable.Repeat((byte)0xFF, ChannelCodec.RecordLength).ToArray());
        return SetBit(blanked, D890UvMemoryMap.ChannelSet, radioIndex, false);
    }

    /// <summary>
    /// Patches the Power-on tab's fields - the only OptionalSettings fields
    /// with write support so far (see <see cref="OptionalSettingsCodec.PowerOnFieldPatch"/>'s
    /// doc comment). Unlike Channel/Zone, this is a single global record (no
    /// radioIndex) split across two independently-captured regions
    /// (data_3500000/data_3500900) - each is only touched if the patch
    /// actually has a field living there, same "only patch what's dirty"
    /// discipline as <see cref="ApplyZonePatch"/>.
    /// </summary>
    public static RadioCodeplugRawSnapshot ApplyOptionalSettingsPatch(RadioCodeplugRawSnapshot snapshot, OptionalSettingsCodec.PowerOnFieldPatch patch)
    {
        var result = snapshot;

        if (patch.PowerOnInterface is not null || patch.PowerOnPassword is not null || patch.DefaultStartupChannel is not null
            || patch.StartupZoneA is not null || patch.StartupZoneB is not null || patch.StartupChannelA is not null
            || patch.StartupChannelB is not null || patch.StartupReset is not null
            || patch.SmsAlert is not null || patch.CallAlert is not null || patch.DigiCallResetTone is not null
            || patch.TalkPermit is not null || patch.KeyTone is not null || patch.DigiIdleChannelTone is not null
            || patch.StartupSound is not null || patch.AnalogIdleChannelTone is not null
            || patch.CallPermitTones is not null || patch.MatchEndTones is not null || patch.CallResetTones is not null
            || patch.UnMatchEndTones is not null || patch.CallAllTones is not null
            || patch.AutoShutdown is not null || patch.PowerSave is not null || patch.AutoShutdownType is not null
            || patch.Brightness is not null || patch.AutoBacklightDuration is not null || patch.BacklightTxDelay is not null || patch.MenuExitTime is not null || patch.TimeDisplay is not null || patch.LastCaller is not null || patch.CallDisplayMode is not null || patch.CallsignDisplayColor is not null || patch.CallEndPromptBox is not null || patch.DisplayChannelNumber is not null || patch.DisplayCurrentContact is not null || patch.StandbyCharColor is not null || patch.StandbyBkPicture is not null || patch.ShowLastCallOnLaunch is not null || patch.SeparateDisplay is not null || patch.ChSwitchingKeepsCaller is not null || patch.BacklightRxDelay is not null || patch.ChannelNameColorA is not null || patch.ChannelNameColorB is not null || patch.ZoneNameColorA is not null || patch.ZoneNameColorB is not null || patch.DisplayChannelType is not null || patch.DisplayTimeSlot is not null || patch.DisplayColorCode is not null || patch.DateDisplayFormat is not null || patch.VolumeBar is not null || patch.NightMode is not null
            || patch.DisplayMode is not null || patch.VfMrA is not null || patch.MemZoneA is not null || patch.VfMrB is not null || patch.MemZoneB is not null || patch.MainChannelSet is not null || patch.SubChannelMode is not null || patch.WorkingMode is not null
            || patch.VoxLevel is not null || patch.VoxDelay is not null || patch.VoxDetection is not null
            || patch.SteTypeOfCtcss is not null || patch.SteWhenNoSignal is not null || patch.SteTime is not null
            || patch.AmFmFunction is not null || patch.FmVfoMem is not null || patch.FmMonitor is not null
            || patch.AmVfoMem is not null || patch.AmOffset is not null || patch.AmSqlLevel is not null || patch.FrequencyStep is not null
            || patch.KeyLock is not null || patch.Pf1ShortKey is not null || patch.Pf2ShortKey is not null || patch.Pf3ShortKey is not null
            || patch.P1ShortKey is not null || patch.P2ShortKey is not null || patch.Pf1LongKey is not null || patch.Pf2LongKey is not null
            || patch.Pf3LongKey is not null || patch.P1LongKey is not null || patch.P2LongKey is not null || patch.LongKeyTime is not null
            || patch.KnobLock is not null || patch.KeyboardLock is not null || patch.SideKeyLock is not null || patch.ForcedKeyLock is not null
            || patch.AddressBookSentWithCode is not null || patch.Tot is not null || patch.Language is not null || patch.GeneralFrequencyStep is not null
            || patch.SqlLevelA is not null || patch.SqlLevelB is not null || patch.Tbst is not null || patch.AnalogCallHoldTime is not null
            || patch.CallChannelMaintained is not null || patch.PriorityZoneA is not null || patch.PriorityZoneB is not null || patch.MuteTiming is not null
            || patch.EncryptionType is not null || patch.TotPredict is not null || patch.TxPowerAgc is not null || patch.NoaaMoni is not null
            || patch.NoaaScan is not null || patch.Noaa is not null || patch.NoaaChannel is not null
            || patch.GroupCallHoldTime is not null || patch.PrivateCallHoldTime is not null || patch.ManualDialGroupCallHoldTime is not null
            || patch.ManualDialPrivateCallHoldTime is not null || patch.VoiceHeaderRepetitions is not null || patch.TxPreambleDuration is not null
            || patch.FilterOwnId is not null || patch.DigitalRemoteKill is not null || patch.DigitalMonitor is not null
            || patch.DigitalMonitorCc is not null || patch.DigitalMonitorId is not null || patch.MonitorSlotHold is not null
            || patch.RemoteMonitor is not null || patch.SmsFormat is not null || patch.ResetDigitalProtocol is not null
            || patch.GpsPositioning is not null || patch.TimeZone is not null || patch.GpsMode is not null
            || patch.VfoScanType is not null || patch.VfoScanStartFreqUhf is not null || patch.VfoScanEndFreqUhf is not null
            || patch.VfoScanStartFreqVhf is not null || patch.VfoScanEndFreqVhf is not null
            || patch.AutoRepeaterA is not null || patch.AutoRepeaterB is not null
            || patch.AutoRepeater1Uhf is not null || patch.AutoRepeater1Vhf is not null
            || patch.AutoRepeater2Uhf is not null || patch.AutoRepeater2Vhf is not null
            || patch.RepeaterCheck is not null || patch.RepeaterCheckInterval is not null || patch.RepeaterCheckReconnections is not null
            || patch.RepeaterOutOfRangeNotify is not null || patch.OutOfRangeNotify is not null
            || patch.AutoRoaming is not null || patch.AutoRoamingStartCondition is not null
            || patch.AutoRoamingFixedTime is not null || patch.RoamingEffectWaitTime is not null
            || patch.AutoRepeater1MinFreqVhf is not null || patch.AutoRepeater1MaxFreqVhf is not null
            || patch.AutoRepeater1MinFreqUhf is not null || patch.AutoRepeater1MaxFreqUhf is not null
            || patch.AutoRepeater2MinFreqVhf is not null || patch.AutoRepeater2MaxFreqVhf is not null
            || patch.AutoRepeater2MinFreqUhf is not null || patch.AutoRepeater2MaxFreqUhf is not null
            || patch.RepeaterMode is not null || patch.RepCcLimit is not null
            || patch.RepSlotA is not null || patch.RepSlotB is not null || patch.RepeaterWhitelist is not null
            || patch.RecordFunction is not null || patch.RecordDelay is not null
            || patch.MaxVolume is not null || patch.PowerOnVolumeType is not null || patch.PowerOnVolume is not null
            || patch.MaxHeadphoneVolume is not null || patch.DigiMicGain is not null || patch.EnhancedSoundQuality is not null
            || patch.AnalogMicGain is not null || patch.RxAgc is not null || patch.NxMicGain is not null
            || patch.SubSpkInTx is not null || patch.RxNoiseReduction is not null || patch.TxNoiseReduction is not null
            || patch.SatLocation is not null || patch.SatTxPower is not null || patch.SatAnaSql is not null || patch.SatAosLimit is not null
            || patch.RoamingZone is not null)
        {
            result = ApplyPatch(result, D890UvMemoryMap.OptionalSettingsData3500000, OptionalSettingsCodec.MainDataLength, current => OptionalSettingsCodec.EncodeMain(current, patch));
        }

        if (patch.PowerOnDisplayLine1 is not null || patch.PowerOnDisplayLine2 is not null || patch.PowerOnPasswordChar is not null)
        {
            result = ApplyPatch(result, D890UvMemoryMap.OptionalSettingsData3500900, OptionalSettingsCodec.SecondaryDataLength, current => OptionalSettingsCodec.EncodeDisplay(current, patch));
        }

        return result;
    }

    /// <summary>
    /// Patches the whole Alarm Settings record across its 3 separate
    /// addresses (same call shape as <see cref="AlarmSettingsCodec.Decode"/>).
    /// The 0x3500000 sub-patch deliberately reuses the SAME base address as
    /// <see cref="ApplyOptionalSettingsPatch"/>'s own 0x160-byte record - the
    /// snapshot only ever captures ONE region there (the largest length
    /// requested wins, see <see cref="RadioCodeplugRawSnapshot"/>'s capture
    /// dedupe), so this is a genuine read-modify-write against the SAME
    /// bytes Optional Settings owns, touching only offsets 0x24/0x4f -
    /// confirmed safe by the composing-patches-in-sequence architecture
    /// every other shared-region entity already relies on. Confirmed
    /// write-safe 2026-08-04 - see AlarmSettingsCodec.EncodeD3483000's doc
    /// comment for the 4 live USB captures.</summary>
    public static RadioCodeplugRawSnapshot ApplyAlarmSettingsPatch(RadioCodeplugRawSnapshot snapshot, AlarmSettingsCodec.DecodedAlarmSettings values)
    {
        var result = ApplyPatch(snapshot, D890UvMemoryMap.AlarmSettingsData3483000, AlarmSettingsCodec.Data3483000Length, current => AlarmSettingsCodec.EncodeD3483000(current, values));
        result = ApplyPatch(result, D890UvMemoryMap.AlarmSettingsData3482e00, AlarmSettingsCodec.Data3482e00Length, current => AlarmSettingsCodec.EncodeD3482e00(current, values));
        return ApplyPatch(result, D890UvMemoryMap.AlarmSettingsData3500000, AlarmSettingsCodec.Data3500000Length, current => AlarmSettingsCodec.EncodeD3500000(current, values));
    }

    /// <summary>Patches Talk Alias Settings' 2 adjacent bytes - same shared-
    /// region reasoning as <see cref="ApplyAlarmSettingsPatch"/>'s own
    /// 0x3500000 sub-patch (offsets 0xed/0xee both comfortably inside
    /// <see cref="ApplyOptionalSettingsPatch"/>'s own 0x160-byte captured
    /// region there). Confirmed write-safe 2026-08-09 - see
    /// TalkAliasSettingsCodec's own doc comment for the live capture.</summary>
    public static RadioCodeplugRawSnapshot ApplyTalkAliasSettingsPatch(RadioCodeplugRawSnapshot snapshot, TalkAliasSettingsCodec.DecodedTalkAliasSettings values) =>
        ApplyPatch(snapshot, TalkAliasSettingsCodec.DisplayPriorityAddress, 2, _ => TalkAliasSettingsCodec.Encode(values));

    /// <summary>Patches the whole APRS Settings record (main 0x260-byte
    /// region) plus the FixedLocationBeacon byte, which lives in the SAME
    /// shared 0x3500000 block as Alarm/Talk Alias/Optional Settings - same
    /// reasoning as <see cref="ApplyAlarmSettingsPatch"/>'s own 0x3500000
    /// sub-patch. See Capture_Findings.md for the live-test coverage behind
    /// every offset <see cref="AprsSettingsCodec.Encode"/> actually
    /// writes.</summary>
    public static RadioCodeplugRawSnapshot ApplyAprsSettingsPatch(RadioCodeplugRawSnapshot snapshot, AprsSettingsCodec.DecodedAprsSettings values)
    {
        var result = ApplyPatch(snapshot, D890UvMemoryMap.AprsSettingsMainData, AprsSettingsCodec.MainDataLength, current => AprsSettingsCodec.Encode(current, values));
        return ApplyPatch(result, D890UvMemoryMap.AprsFixedLocationBeaconAddress, 1, _ => AprsSettingsCodec.EncodeFixedLocationBeacon(values.FixedLocationBeacon));
    }

    public static RadioCodeplugRawSnapshot ApplyAesKeyPatch(RadioCodeplugRawSnapshot snapshot, int slotNumber, string keyHex)
    {
        var address = D890UvMemoryMap.AesEncryptionKeyData + (slotNumber - 1) * D890UvMemoryMap.AesEncryptionKeyStride;
        return ApplyPatch(snapshot, address, D890UvMemoryMap.AesEncryptionKeyStride, current => EncryptionKeyCodec.EncodeAesKey(current, slotNumber, keyHex));
    }

    /// <summary>Sets an AES key slot back to "Off" - see
    /// <see cref="EncryptionKeyCodec.ClearIndexedKeySlot"/>'s doc comment.</summary>
    public static RadioCodeplugRawSnapshot ApplyAesKeyClearPatch(RadioCodeplugRawSnapshot snapshot, int slotNumber)
    {
        var address = D890UvMemoryMap.AesEncryptionKeyData + (slotNumber - 1) * D890UvMemoryMap.AesEncryptionKeyStride;
        return ApplyPatch(snapshot, address, D890UvMemoryMap.AesEncryptionKeyStride, current => EncryptionKeyCodec.ClearIndexedKeySlot(current));
    }

    public static RadioCodeplugRawSnapshot ApplyArc4KeyPatch(RadioCodeplugRawSnapshot snapshot, int slotNumber, string keyHex)
    {
        var address = D890UvMemoryMap.Arc4EncryptionKeyData + (slotNumber - 1) * D890UvMemoryMap.Arc4EncryptionKeyStride;
        return ApplyPatch(snapshot, address, D890UvMemoryMap.Arc4EncryptionKeyStride, current => EncryptionKeyCodec.EncodeArc4Key(current, slotNumber, keyHex));
    }

    /// <summary>Sets an ARC4 key slot back to "Off" - see
    /// <see cref="EncryptionKeyCodec.ClearIndexedKeySlot"/>'s doc comment.</summary>
    public static RadioCodeplugRawSnapshot ApplyArc4KeyClearPatch(RadioCodeplugRawSnapshot snapshot, int slotNumber)
    {
        var address = D890UvMemoryMap.Arc4EncryptionKeyData + (slotNumber - 1) * D890UvMemoryMap.Arc4EncryptionKeyStride;
        return ApplyPatch(snapshot, address, D890UvMemoryMap.Arc4EncryptionKeyStride, current => EncryptionKeyCodec.ClearIndexedKeySlot(current));
    }

    public static RadioCodeplugRawSnapshot ApplyBasicCodePatch(RadioCodeplugRawSnapshot snapshot, int slotNumber, string code)
    {
        const int groupSize = 4;
        var stride = D890UvMemoryMap.BasicEncryptionCodeStride;
        var groupIndex = (slotNumber - 1) / groupSize;
        var slotIndexWithinGroup = (slotNumber - 1) % groupSize;
        var groupAddress = D890UvMemoryMap.BasicEncryptionCodeData + groupIndex * groupSize * stride;
        return ApplyPatch(snapshot, groupAddress, groupSize * stride, current => EncryptionKeyCodec.EncodeBasicCodeGroup(current, slotIndexWithinGroup, code));
    }

    /// <summary>Same address computation <see cref="RadioCodeplugRawSnapshotReader"/>
    /// and the retired <c>RadioChannelWriter</c> both used.</summary>
    public static int ChannelAddress(int radioIndex)
    {
        var blockIndex = radioIndex / D890UvMemoryMap.ChannelDataBlockSize;
        var indexInBlock = radioIndex % D890UvMemoryMap.ChannelDataBlockSize;
        return D890UvMemoryMap.ChannelData
            + blockIndex * D890UvMemoryMap.ChannelDataBlockOffset
            + indexInBlock * D890UvMemoryMap.ChannelDataOffset;
    }

    /// <summary>
    /// Finds the captured region containing <paramref name="recordAddress"/>
    /// (which may be a small per-record region, e.g. one channel, or a much
    /// larger combined region, e.g. the whole AES key table), slices out
    /// exactly the <paramref name="recordLength"/>-byte record at that
    /// address, runs <paramref name="encode"/> against it, and splices the
    /// (same-length) result back into a COPY of that region's bytes -
    /// returning a new snapshot with every other region shared/untouched.
    /// </summary>
    private static RadioCodeplugRawSnapshot ApplyPatch(RadioCodeplugRawSnapshot snapshot, int recordAddress, int recordLength, Func<byte[], byte[]> encode)
    {
        var region = snapshot.FindRegionContaining(recordAddress)
            ?? throw new InvalidOperationException($"No captured region contains address 0x{recordAddress:X8} - the record may not exist yet (never populated).");

        if (recordAddress + recordLength > region.Address + region.Length)
        {
            throw new InvalidOperationException($"Record at 0x{recordAddress:X8} (length {recordLength}) extends past its captured region (0x{region.Address:X8}, length {region.Length}).");
        }

        var offsetInRegion = recordAddress - region.Address;
        var currentRecord = region.Data.AsSpan(offsetInRegion, recordLength).ToArray();
        var patchedRecord = encode(currentRecord);
        if (patchedRecord.Length != recordLength)
        {
            throw new InvalidOperationException($"Encoded record is {patchedRecord.Length} bytes, expected {recordLength} - refusing to splice a mismatched-length patch.");
        }

        var patchedRegionData = (byte[])region.Data.Clone();
        patchedRecord.CopyTo(patchedRegionData, offsetInRegion);

        var patchedRegions = new List<CodeplugRawRegion>(snapshot.Regions.Count);
        foreach (var r in snapshot.Regions)
        {
            patchedRegions.Add(r.Address == region.Address ? new CodeplugRawRegion(r.Address, patchedRegionData) : r);
        }

        return new RadioCodeplugRawSnapshot { Regions = patchedRegions };
    }
}
