namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Flash memory address map for the AnyTone AT-D890UV (firmware V100).
///
/// Transcribed from the `D890_MAP` table in `anytone_memory.h` of the
/// MIT-licensed reference project github.com/xbenkozx/anytone-cps, and
/// cross-validated against live USB captures from real
/// hardware. Only the D890UV is supported today, so this is kept as a
/// flat set of constants rather than a per-model abstraction.
/// </summary>
public static class D890UvMemoryMap
{
    public const int LocalInfo = 0x4f80000;
    public const int LocalInfoLength = 0x100;
    public const int BootImage = 0x3f80000;

    // Channels: bitmap of populated indices, then per-channel 128-byte records.
    public const int ChannelSet = 0x3482a00;
    public const int ChannelData = 0x1000000;

    /// <summary>Bytes per channel record. Read as a single contiguous 128-byte
    /// block per channel - the reference project does two adjacent 0x40-byte
    /// reads internally, but they are byte-identical to one 0x80-byte read.</summary>
    public const int ChannelDataOffset = 0x80;

    /// <summary>Channels per bank, used for bank/block index math only -
    /// NOT a byte size (confusingly shares the same numeric value as
    /// <see cref="ChannelDataOffset"/>).</summary>
    public const int ChannelDataBlockSize = 128;

    /// <summary>Address stride between channel banks.</summary>
    public const int ChannelDataBlockOffset = 0x80000;

    /// <summary>Total regular, user-editable channel slots (indices 0-3999) -
    /// confirmed in Protocol_Notes.md ("4000 channels total", cross-checked
    /// against dmrconfig/qdmr). Indices 4000/4001 immediately following are
    /// NOT regular channels - they decode as "Channel VFO A"/"Channel VFO B"
    /// (confirmed live 2026-07-18), the radio's own current-VFO working
    /// state, not part of the user-managed channel list. The channel
    /// presence bitmap has no awareness of this distinction (VFO A/B's bits
    /// are set just like any real channel), so anything walking that bitmap
    /// to build the regular channel list MUST filter to indices below this
    /// bound explicitly, or VFO A/B leak in as two fake "channels" - this
    /// caused a real incident where adding a new channel computed its number
    /// from `Channels.Max(Number) + 1`, which included VFO A/B's indices in
    /// the max and made the app think the radio had ~4002 channels.</summary>
    public const int MaxRegularChannelCount = 4000;

    public const int RadioIdSet = 0x3482c40;
    public const int RadioIdData = 0x3680000;
    public const int RadioIdDataOffset = 0x40;
    public const int RadioIdDataLength = 0x40;

    public const int ScanListSet = 0x3482c60;

    /// <summary>ScanListSet is a 0x20-byte (256-bit) presence bitmap - 256
    /// physical slots, same relationship as ZoneSlotCount vs the vendor-
    /// documented 250 (CodeplugLimits.ScanListMax).</summary>
    public const int ScanListSlotCount = 256;

    public const int ScanListData = 0x2100000;
    public const int ScanListDataOffset = 0x200;
    public const int ScanListDataLength = 0xd0;

    /// <summary>NOTE: inverted bitmap logic! Bit SET means SKIP this index,
    /// bit UNSET (0) means the talkgroup IS populated. This is the opposite
    /// of every other entity's bitmap in this map - confirmed from the
    /// reference source, which carries the same warning.</summary>
    public const int TalkgroupSet = 0x3980000;
    public const int TalkgroupData = 0x3a00000;
    public const int TalkgroupDataOffset = 0xc8;
    public const int TalkgroupDataLength = 0x80;

    public const int RoamingChannelSet = 0x2084000;
    public const int RoamingChannelData = 0x2080000;
    public const int RoamingChannelDataOffset = 0x40;
    public const int RoamingChannelDataLength = 0x40;

    public const int RoamingZoneSet = 0x2084080;
    public const int RoamingZoneData = 0x2085000;
    public const int RoamingZoneDataOffset = 0x80;
    public const int RoamingZoneDataLength = 0x80;

    public const int ReceiveGroupSet = 0x3701510;
    public const int ReceiveGroupData = 0x3780000;
    public const int ReceiveGroupDataOffset = 0x200;
    public const int ReceiveGroupDataLength = 0x120;

    /// <summary>Flat array, 250 entries x 4 bytes each, no bitmap.</summary>
    public const int AutoRepeaterData = 0x3483200;

    // Zones: bitmap of populated indices, then 4 separate parallel arrays.
    public const int ZoneSet = 0x3482c00;

    /// <summary>ZoneSet is a 0x20-byte (256-bit) presence bitmap - 256 is
    /// therefore the radio's real zone slot count, used the same way
    /// <see cref="MaxRegularChannelCount"/> bounds-checks a channel radio
    /// index.</summary>
    public const int ZoneSlotCount = 256;

    public const int ZonesName = 0x3600000;

    /// <summary>Stride for the name array specifically (ZonesName + idx*0x40).</summary>
    public const int ZoneDataOffset = 0x40;

    /// <summary>Length of the name field read.</summary>
    public const int ZoneDataLength = 0x20;

    /// <summary>Channel-membership-list array; fixed 0x200-byte stride per
    /// zone (ZoneChannels + idx*0x200), 256 x uint16 channel indices,
    /// 0xFFFF = unused slot (confirmed 2026-07-19 - see ZoneCodec's
    /// ChannelMemberSlotCount doc comment; this used to say 128, a real bug
    /// that silently truncated any zone with more than 128 members on read).</summary>
    public const int ZoneChannels = 0x2000000;

    /// <summary>Flat uint16 array, 2 bytes per zone (idx*2).</summary>
    public const int ZoneAChannel = 0x3500400;

    /// <summary>Same shape as <see cref="ZoneAChannel"/>.</summary>
    public const int ZoneBChannel = 0x3500600;

    /// <summary>Bitmap, 1 bit per zone, hide flag.</summary>
    public const int ZoneHide = 0x3482c20;

    /// <summary>Single fixed-address record (no bitmap, no list - there is
    /// only ever one Master ID).</summary>
    public const int MasterIdData = 0x3684000;
    public const int MasterIdDataLength = 0x40;

    /// <summary>Talk Alias Settings: just 2 bytes at fixed absolute addresses
    /// (see <see cref="Codecs.TalkAliasSettingsCodec"/>) within the same
    /// shared block used for Optional Settings - not a separate region.</summary>
    public const int TalkAliasSettingsBase = 0x3500000;
    public const int TalkAliasSettingsReadLength = 0xf0; // covers both 0xed and 0xee with margin

    /// <summary>Analog Address Book: NOT a bitmap - a flat 256-byte array
    /// where each byte holding a value != 0xFF IS itself the populated
    /// record index (its position in the array is not the index; matches
    /// the reference project's own iteration: `for (uint8_t i : id_list) if
    /// (i != 0xff) idx_list.append(i)`).</summary>
    public const int AnalogBookId = 0x3800000;
    public const int AnalogBookIdLength = 0x100;
    public const int AnalogBookData = 0x3801000;
    public const int AnalogBookDataStride = 0x40;
    public const int AnalogBookDataLength = 0x40;

    /// <summary>GPS Roaming: fixed 32-entry array, no bitmap - read the
    /// whole block in one go and slice per-entry via
    /// <see cref="Codecs.GpsRoamingCodec.OffsetForIndex"/>.</summary>
    public const int GpsRoamingData = 0x3502000;
    public const int GpsRoamingDataLength = 0x400;

    /// <summary>Talkgroup Whitelist: packed stream, not a stride array - see
    /// <see cref="Codecs.TalkgroupWhitelistCodec"/> doc comment for the full
    /// read/stop pattern (up to 10 x 16-byte blocks, stop on blank second half).</summary>
    public const int TalkgroupWhitelistData = 0x4c80000;

    /// <summary>Digital-Contact Whitelist: byte-for-byte identical wire
    /// format to Talkgroup Whitelist above (reuses
    /// <see cref="Codecs.TalkgroupWhitelistCodec"/>) - just a different base
    /// address and a distinct list in the vendor CPS (1000 fixed slots,
    /// same as Talkgroup Whitelist).</summary>
    public const int DigitalContactWhitelistData = 0x4c82000;

    /// <summary>Digital Contact database (the big DMR-ID address book, up to
    /// 500,000 entries - NOT the small Digital-Contact Whitelist above).
    /// This is the ONE entity in this app with an opt-in read gate: reading
    /// is a variable-length sequential stream parse (no per-record fixed
    /// address), so read time scales with however many contacts are
    /// actually populated - see <see cref="Codecs.DigitalContactCodec"/> for
    /// the full block/stride address-translation and parsing scheme, ported
    /// from the reference project's <c>getDigitalContactDataBuffer</c> /
    /// <c>parseDigitalContact_D890UV</c>. <see cref="DigitalContactMeta"/>
    /// holds just a 4-byte populated-count at its start;
    /// <see cref="DigitalContactData"/> is the start of the logical record
    /// stream.</summary>
    public const int DigitalContactMeta = 0x7000000;
    public const int DigitalContactData = 0x7900000;

    /// <summary>Prefabricated SMS: linked-list "used slot" index, not a
    /// bitmap - see <see cref="Codecs.PrefabricatedSmsCodec"/> doc comment
    /// for the full traversal + two-level addressing scheme.</summary>
    public const int PrefabSmsSet = 0x2980000;
    public const int PrefabSmsData = 0x3180000;
    public const int PrefabSmsDataLength = 0x1a0;
    public const int PrefabSmsDataOffset = 0x200;
    public const int PrefabSmsDataBlockSize = 0x1000;
    public const int PrefabSmsDataBlockOffset = 0x80000;

    // AM Air: standard bitmap-driven pattern (256 possible slots) PLUS one
    // extra always-present "VFO" record at a separate fixed address, read
    // unconditionally regardless of the bitmap.
    public const int AmAirSet = 0x3884200;
    public const int AmAirData = 0x3880000;
    public const int AmAirDataStride = 0x40;
    public const int AmAirDataLength = 0x40;
    public const int AmAirVfo = 0x3884000;

    /// <summary>Bitmap-addressable slot count (AmAirSet is 0x20 bytes = 256
    /// bits) - the VFO slot is separate and not counted here, same
    /// relationship as ScanListSlotCount/ZoneSlotCount vs their own extra
    /// slots.</summary>
    public const int AmAirSlotCount = 256;

    // AM Zone: bitmap of 16 fixed zone slots, per-zone record, plus a flat
    // parallel uint16 array (AChannel) read once for all zones.
    public const int AmZoneSet = 0x3884400;
    public const int AmZoneAChannel = 0x3884600;
    public const int AmZoneData = 0x3888000;
    public const int AmZoneDataStride = 0x80;
    public const int AmZoneDataLength = 0x80;
    public const int AmZoneCount = 16;

    /// <summary>"Zone Scan Channel Member" - a separate list from the
    /// regular Zone Channel Member list (confirmed 2026-08-02 directly from
    /// the vendor CPS "AM Zone Edit" dialog, which shows both as two
    /// distinct transfer-list pairs). One 0x10-byte (128-bit) bitmask per
    /// zone, bit N set meaning AM Air radio index N is a scan-channel
    /// member - confirmed via a live differential write (adding AM CH
    /// 001/002, 0-based radio indexes 0/1, to zone 0's scan list produced
    /// raw byte 0x03 = bits 0+1 set). Address matches the MIT-licensed
    /// reference project's own guess (github.com/xbenkozx/anytone-cps,
    /// am_zone.cpp/device.cpp's AmZoneScan + idx*0x10) - AmZoneCodec's
    /// earlier doc comment worried this address was unreliable because of
    /// an apparent out-of-bounds bug in the reference's own C++ buffer-
    /// slicing code, but that bug was in how the reference sliced ITS OWN
    /// already-fetched buffer, not the radio address itself, which turns
    /// out to be correct.</summary>
    public const int AmZoneScan = 0x3884800;
    public const int AmZoneScanStride = 0x10;
    public const int AmZoneScanLength = 0x10;

    /// <summary>FM broadcast channels: a shared 0x60-byte metadata block
    /// holds the "home"/VFO channel's own record (offsets 0x00/0x04, same
    /// shape as a normal channel record) PLUS the active/scan bitmasks for
    /// the 100 normal channels (which live in a separate strided region).</summary>
    public const int FmMeta = 0x3402000;
    public const int FmMetaLength = 0x60;
    public const int FmActiveMaskOffset = 0x40;
    public const int FmScanMaskOffset = 0x50;
    public const int FmChannelData = 0x3400000;
    public const int FmChannelDataStride = 0x40;
    public const int FmChannelCount = 100;

    /// <summary>Alarm Settings: single instance, reads from 3 separate
    /// addresses (not one contiguous record) - see
    /// <see cref="Codecs.AlarmSettingsCodec"/> doc comment.</summary>
    public const int AlarmSettingsData3483000 = 0x3483000;
    public const int AlarmSettingsData3482e00 = 0x3482e00;
    public const int AlarmSettingsData3500000 = 0x3500000;

    /// <summary>APRS Settings: single instance, main record + a separate
    /// fixed 32-slot receive-filter list. NOTE: the reference project's own
    /// local variable naming at the call site is misleading - it names the
    /// second block "data_3501800" but actually reads from 0x3501300, not
    /// 0x3501800 - verified directly against desktop/src/device.cpp, don't
    /// trust the variable name if re-deriving this.</summary>
    public const int AprsSettingsMainData = 0x3501000;
    public const int AprsReceiveFilterData = 0x3501300;
    public const int AprsReceiveFilterDataLength = 0x100;

    /// <summary>APRS's "Fixed Location Beacon" selector - CONFIRMED
    /// 2026-08-15 by live differential write to live OUTSIDE
    /// <see cref="AprsSettingsMainData"/> entirely, inside the same shared
    /// 0x3500000 block as <see cref="OptionalSettingsData3500000"/>/Alarm
    /// Settings/Talk Alias Settings (same "one captured region, multiple
    /// independent RMW patches" pattern - see Capture_Findings.md).</summary>
    public const int AprsFixedLocationBeaconAddress = 0x350014e;

    /// <summary>Optional Settings: single instance, shares the same
    /// d3500000/d3500900 addresses as Alarm Settings/Talk Alias Settings
    /// (each entity reads its own independent copy - matches this
    /// codebase's existing one-read-per-entity convention, not a shared
    /// cached read). Only a partial field set is currently decoded here -
    /// see OptionalSettingsCodec's doc comment.</summary>
    public const int OptionalSettingsData3500000 = 0x3500000;
    public const int OptionalSettingsData3500900 = 0x3500900;
    public const int OptionalSettingsData3501280 = 0x3501280;

    /// <summary>AES-256 encryption keys - found 2026-07-18 via a live
    /// differential USB capture (no codec/address existed before this; not
    /// ported from the reference project). Flat array, 256 reserved slots,
    /// no bitmap - population is indicated by each slot's own first byte
    /// (an index 1-N; 0x00 = unpopulated). Confirmed RAW BINARY on the
    /// radio (32 raw key bytes) - the `.rdt` file's hex-ASCII-text
    /// convention for the same key list is a file-format-only choice, not
    /// what the radio itself stores. See RESUME_HERE.md's 2026-07-18 write
    /// up for the full capture/verification detail.</summary>
    public const int AesEncryptionKeyData = 0x3580000;
    public const int AesEncryptionKeyStride = 0x40;
    public const int AesEncryptionKeyMaxSlots = 256;

    /// <summary>ARC4 keys - same discovery session, immediately following
    /// AES's 256-slot reservation. Stride CONFIRMED 0x10 (not the original
    /// 0x40 guess) via a live differential write capture 2026-07-20: a real
    /// key change to slot 3 landed at 0x3584020, exactly 0x10 past slot 2's
    /// key at 0x3584010 - independently corroborated by the reference
    /// project's own ARC4 encode buffer size (`QByteArray data(0x10, 0x0)`).
    /// Slot count capped to the largest that provably cannot reach
    /// <see cref="BasicEncryptionCodeData"/> (0x3585000, the start of the
    /// neighboring table) - not itself confirmed as the true UI-visible
    /// capacity (see <c>CodeplugLimits.Arc4EncryptionKeyCount</c>=34 for
    /// that), just a safe read/scan upper bound. Combined with
    /// EncryptionKeyCodec's 0x00/0xFF blank-index check, this keeps the
    /// decode honest.</summary>
    public const int Arc4EncryptionKeyData = 0x3584000;
    public const int Arc4EncryptionKeyStride = 0x10;
    public const int Arc4EncryptionKeyMaxSlots = 256; // 256 * 0x10 = 0x1000; 0x3584000 + 0x1000 = 0x3585000

    /// <summary>Basic/"Digital" encryption code - same discovery session.
    /// 2-byte BCD-encoded 4-digit value (same hex-digit-as-decimal-digit
    /// convention this radio uses for frequencies - see BcdDecimalCodec),
    /// 40-byte stride, 32 slots, no index byte (slot position is purely
    /// positional). Confirmed via a 3-point differential test hitting slot
    /// 1, 2, and 32 ("last") exactly on the predicted stride in one
    /// capture.</summary>
    public const int BasicEncryptionCodeData = 0x3585100;
    public const int BasicEncryptionCodeValueOffset = 0x10;
    public const int BasicEncryptionCodeStride = 0x28;
    public const int BasicEncryptionCodeMaxSlots = 32;

    /// <summary>Hot Key - real D890UV addresses confirmed 2026-08-04 via a
    /// live differential READ capture (a plain vendor CPS Read, not a
    /// write - no field changes needed since the goal was just to see what
    /// vendor CPS itself requests). The xbenkozx/anytone-cps reference
    /// project's own guessed addresses (0x25c0000/0x25c0500/0x25c0b00,
    /// D878UVII-only, never run on a D890UV) were flat wrong for this
    /// radio - real addresses are a completely different region, though
    /// the reference's per-record byte SHAPE (Analog Quick Call 2 bytes/
    /// slot, Hot Key 0x30 bytes/slot with the same field offsets) held up
    /// exactly. All three regions sit close together, matching
    /// <see cref="ReceiveGroupSet"/> (0x3701510) immediately following the
    /// Hot Key array's own end (0x3701000 + 18*0x30 = 0x3701360) with a
    /// small gap - confirmed by the SAME capture incidentally covering that
    /// pre-existing address too, not a coincidence worth re-deriving.
    ///
    /// Confirmed with real data: Analog Quick Call's 4 slots (all unset -
    /// confirmed nothing is configured, matches <c>00 FF</c> repeated
    /// 4 times = 4x(OperationType=0 Off, CallId=0xFF Off)). Hot Key's 18
    /// records (confirmed "Hot Key 1" is really Mode=Menu - this app
    /// had never decoded it before, so what looked like a "wrong Mode" was
    /// actually just the local default seed showing, not a bad read).
    /// State Information's real text ("Status Message 1", confirmed as
    /// slot No. 1) sits exactly at StateInformationData with no
    /// offset, confirming a 0x40 stride (32 chars x 2 bytes/char UTF-16LE -
    /// matches the confirmed 32-char MaxLength exactly, one slot =
    /// one full text buffer with no room to spare).
    ///
    /// A same-day follow-up live differential WRITE capture (vendor CPS
    /// Write, not this app's own - see HotKeyCodec's doc comment for the
    /// exact bytes) confirmed the remaining unknowns: real (non-Off) Call
    /// Type/Digi Call Type raw byte values match the reference project's
    /// OWN scheme exactly (0xFF=Off, 0=Analog, 1=Digital for Call Type;
    /// 0xFF=Off, 3=Hot Text, 5=State Information for Digi Call Type - this
    /// codec's own FIRST draft had guessed a plain sequential 0/1/2 mapping
    /// instead, which the capture proved wrong). Call Object and Content
    /// both turned out to be a 0-based INDEX into the referenced list
    /// (Analog Quick Call/Talkgroups for Call Object, Prefabricated SMS/
    /// State Information for Content), not the "Number" this codec first
    /// assumed - HotKeyCodec.Decode now translates that to a 1-based
    /// Number with a +1 so the rest of the app's existing "Number"
    /// convention still applies above the codec layer. Call Object's
    /// endianness remains unconfirmed (every captured value so far is
    /// endian-symmetric: 0 or 0xFFFFFFFF) - kept little-endian. Digi Call
    /// Type's "Call Tip" raw value (4, per the reference) was never
    /// actually set during the write capture, so it's still an assumption,
    /// not a confirmation.</summary>
    public const int AnalogQuickCallData = 0x3700000;
    public const int StateInformationData = 0x3700100;
    public const int StateInformationStride = 0x40;
    public const int StateInformationSlotCount = 32;
    public const int HotKeyData = 0x3701000;

    /// <summary>QDC 1200 Setting - real D890UV addresses confirmed
    /// 2026-08-04 via two live differential WRITE captures, found blind
    /// (no reference project data exists at all for this entity - see
    /// Qdc1200SettingsEntry's class doc comment). Two unrelated regions,
    /// not adjacent to each other or to Hot Key's own cluster above:
    /// <see cref="Qdc1200IdData"/> is the Encode tab's 100-row ID table
    /// (flat array, 0x40-byte stride, no bitmap/presence list found
    /// anywhere nearby in either capture), <see cref="Qdc1200SettingsData"/>
    /// is the Decode+Encode tabs' shared singleton settings record (just
    /// 32 bytes - only this one region was ever written in either
    /// capture, confirming it's a genuine standalone record, not part of
    /// a larger array). See Qdc1200IdCodec/Qdc1200SettingsCodec's own doc
    /// comments for the full per-field byte confirmation.</summary>
    public const int Qdc1200IdData = 0x3702000;
    public const int Qdc1200SettingsData = 0x3703900;

    /// <summary>QDC Address Book - a flat 128-slot array (No. 1-128, see
    /// CodeplugLimits.QdcAddressMax), 0x30 (48) bytes each, found blind
    /// 2026-08-04 via a live differential WRITE capture (No. 1 set to Call
    /// Type=Private, Private ID="ABCD", Type=ALEART, Ack=On, Name=
    /// "QDCADDRTEST1" - the distinctive Name as the grep anchor, same
    /// technique used for every other blind-search entity) followed
    /// by a live differential READ capture to resolve the exact record
    /// boundary (the write only sent the bytes that actually changed, not
    /// the full record, so the record's true length wasn't knowable from
    /// the write capture alone).
    ///
    /// The byte layout is an EXACT match for QDC 1200 Setting's own ID
    /// table (Qdc1200IdCodec) - Type(0)/CallType(1)/Ack(2)/pad(3)/
    /// GroupID(4-5, reverse-hex)/PrivateID(6-7, reverse-hex)/Name(8+) -
    /// right down to Type's raw byte for ALEART (2) being the exact same
    /// absolute code on this completely different vendor CPS screen. Only
    /// Private Call (raw 0) and ALEART (raw 2) were directly exercised
    /// here; Group/All Call's own raw Call Type byte and the rest of the
    /// absolute Type code table are inherited from Qdc1200IdCodec's own
    /// confirmed values on the assumption this is the same underlying
    /// firmware table/encoding reused across two UI screens - not
    /// independently re-verified for this address.
    ///
    /// Record length is 0x30 (48 bytes), confirmed by the READ capture:
    /// bytes 0x00-0x2F held the written record, byte 0x30 (the very next
    /// record's first byte) read back as 0xFF - i.e. an UNCONFIGURED slot
    /// here is all-0xFF, NOT all-zero like Qdc1200IdCodec's own convention
    /// (confirmed by the same READ capture: No. 2, never touched, read
    /// back as a full 0x30 bytes of 0xFF). Name occupies the remaining 40
    /// bytes (8 through 47) - 20 UTF-16LE characters of wire capacity,
    /// more than the 16-character UI MaxLength the vendor CPS textbox
    /// itself enforces (kept as the UI limit regardless - matches the
    /// "the UI's own limit is what's validated, not the wire's raw
    /// capacity" convention used throughout this codebase).</summary>
    public const int QdcAddressData = 0x4A00000;
    public const int QdcAddressRecordLength = 0x30;

    /// <summary>5Tone Settings - found blind across 5 live differential
    /// WRITE captures 2026-08-05/06 (no reference project data for this
    /// entity either), CORRECTED 2026-08-16 after live captures proved the
    /// original BOT address wrong (see below). Regions:
    ///
    /// <see cref="FiveToneIdData"/> - a 99-row usable ID table (No. 1-99,
    /// see CodeplugLimits.FiveToneIdMax), 0x40 (64) bytes each, found via
    /// the row's own distinctive Name ("TESTID1", UTF-16LE) as a grep
    /// anchor. Row stride confirmed 0x40 (NOT the 0x30 first guessed from
    /// a single row's own zero-padded tail) via TWO independent row-to-row
    /// Name address deltas, both exactly 0x40 apart. No presence bitmap
    /// found adjacent to the table itself - unlike the singleton block
    /// below, Number is inferred from array position (row 0 = No. 1,
    /// etc.), same convention as Qdc1200IdCodec. The vendor CPS UI shows a
    /// 100th row too, but live capture 2026-08-16 (writing row 100 with a
    /// distinctive Name/Message through the real vendor CPS) proved row
    /// 100's write lands at 0x34818C0-0x34818FF - see
    /// <see cref="FiveToneIdRow100ReservedData"/> below. This app
    /// deliberately stops at row 99.
    ///
    /// <see cref="FiveToneDecodeEncodeData"/> - the Decode/Information ID/
    /// Encode tabs' shared singleton fields, starting with a presence
    /// bitmap (byte 0: one bit per populated ID table row, confirmed via
    /// 0x01→0x03→0x07 as rows 1/2/3 were added - NOT a row count, a real
    /// bitmap). The Information ID/Function1 sub-area (Function Option/
    /// Function Decoding Response/Stop Code/Information ID hex/Function
    /// Name/Information ID NO.) is NOT covered by FiveToneSettingsCodec -
    /// repeated captures showed it relocating and losing data even when
    /// untouched, genuinely unstable, not safe to write against yet (see
    /// FiveToneSettingsCodec's own doc comment for the full story). PTT ID
    /// Starting (BOT)'s own Standard/TimeOfEncodeTone/EncodeId/SpecialCall
    /// fields ALSO live inside this same singleton region, at internal
    /// offset 0x30 (see <see cref="FiveToneBotSettingsData"/> below) - NOT
    /// in a separate region as originally believed.
    ///
    /// <see cref="FiveToneIdRow100ReservedData"/> (0x34818C0) - originally
    /// documented as "FiveToneBotData", a separate region for PTT ID
    /// Starting. Live capture 2026-08-16 disproved this: writing BOT's
    /// Standard/Encode ID through the real vendor CPS's own BOT tab never
    /// touched this address at all (byte-for-byte identical before/after);
    /// what DOES live here is 5Tone ID row 100 (see FiveToneIdData above),
    /// using the exact same layout as every other ID table row
    /// (FiveToneIdCodec, Standard at +0x01, PackedRegion at +0x04, Name at
    /// +0x18). This app never writes here directly (row 100 isn't
    /// offered), but the region is still captured so a whole-codeplug
    /// write doesn't corrupt whatever's already there.
    ///
    /// <see cref="FiveToneBotSettingsData"/> (0x3481930, inside the
    /// singleton above) - PTT ID Starting's real Standard/TimeOfEncodeTone/
    /// EncodeId/SpecialCall fields, live-capture-confirmed 2026-08-16 by
    /// isolating each field independently (Standard: ZVEI2→CCIR1 changed
    /// only byte +0x01; TimeOfEncodeTone: an auto-adjusted 70→100 changed
    /// only byte +0x03; Encode ID/Special Call: changed bytes +0x04
    /// onward, matching FiveToneIdCodec's own PackedRegionOffset/Length
    /// exactly). Byte +0x00 and +0x02 are unattributed, same convention as
    /// FiveToneIdCodec's own row layout - left untouched. No Name field
    /// exists for BOT (confirmed directly: no Name control
    /// exists anywhere in the vendor CPS's BOT/EOT tabs) - the original
    /// "BOT has its own Name field" claim was a misreading of ID table row
    /// 100's OWN Name field, which happens to alias the address this
    /// region used to be attributed to.
    ///
    /// <see cref="FiveToneEotData"/> (0x3481950) - PTT ID Ending, address
    /// was already correct; SAME internal layout as
    /// <see cref="FiveToneBotSettingsData"/> (Standard at +0x01,
    /// TimeOfEncodeTone at +0x03, PackedRegion at +0x04), independently
    /// confirmed live 2026-08-16 (Standard/EncodeId isolated the same way
    /// as BOT, plus a Special Call/Send Message round trip). Record length
    /// below is a real address gap, not tight-fitted to confirmed fields -
    /// only bytes actually decoded get touched by the codecs either way
    /// (everything else round-trips untouched via the same "preserve the
    /// unknown bytes" RMW discipline every other codec in this app already
    /// uses).</summary>
    public const int FiveToneIdData = 0x3480000;
    public const int FiveToneIdRecordLength = 0x40;

    /// <summary>Information ID / Information Code Function1 - a SEPARATE
    /// small slot array, found blind 2026-08-06 (base + digit-count +
    /// "111111" as raw nibble-value bytes as the anchor). Base address
    /// confirmed for slot 1 (Information ID NO. = 1) and slot 2 - the
    /// stride between them (0x40) is confirmed via 2 independent slot-to-
    /// slot deltas (Function Name's own address, same technique as the ID
    /// table's own row stride confirmation). Slot count is NOT confirmed -
    /// the vendor CPS UI originally showed "Information ID NO." as a
    /// 1-16 dropdown before a later differential test showed it actually tracks
    /// existing row numbers (which can go up to the vendor CPS's own
    /// 100-row range, though this app's own CodeplugLimits.FiveToneIdMax
    /// stops at 99 - see that constant's own doc comment) - rather than
    /// guess the real cap and risk reading/writing into whatever comes
    /// after this region
    /// (unmapped), this stays capped at the ORIGINALLY-OBSERVED 16 slots.
    /// A row whose own Number exceeds 16 simply won't have its Function
    /// Option/Function Decoding Response/Information ID/Function Name
    /// read from or written to the radio - a known, deliberate limitation,
    /// not an oversight.</summary>
    public const int FiveToneInfoIdData = 0x3481A00;
    public const int FiveToneInfoIdSlotStride = 0x40;
    public const int FiveToneInfoIdSlotCount = 16;
    /// <summary>Row 100's own storage (see FiveToneIdData's own doc
    /// comment) - captured only for RMW safety of whatever's already
    /// there, never patched by this app (row 100 isn't offered). NOT PTT
    /// ID Starting data despite the old name - see FiveToneIdData's doc
    /// comment for the 2026-08-16 correction.</summary>
    public const int FiveToneIdRow100ReservedData = 0x34818C0;
    public const int FiveToneIdRow100ReservedLength = 0x40; // gap to FiveToneDecodeEncodeData
    public const int FiveToneDecodeEncodeData = 0x3481900;
    public const int FiveToneDecodeEncodeRecordLength = 0x50; // gap to FiveToneEotData

    /// <summary>PTT ID Starting (BOT)'s real Standard/TimeOfEncodeTone/
    /// EncodeId/SpecialCall fields - inside FiveToneDecodeEncodeData, NOT
    /// a separate region. See FiveToneIdData's own doc comment for the
    /// live-capture confirmation (2026-08-16).</summary>
    public const int FiveToneBotSettingsData = FiveToneDecodeEncodeData + 0x30;
    public const int FiveToneBotSettingsLength = 0x20; // 0x3481930-0x3481950, right up to FiveToneEotData

    public const int FiveToneEotData = 0x3481950;
    public const int FiveToneEotRecordLength = 0x30; // safe margin, matches BOT's own confirmed-field extent

    /// <summary>2Tone Settings - found blind across 2 live differential WRITE
    /// captures 2026-08-06 (no reference project data for this entity
    /// either). Two independent row tables plus one settings/bitmap block:
    ///
    /// <see cref="TwoToneEncodeData"/> - the Encode tab's 24-row frequency
    /// table (No. 1-24, see CodeplugLimits.TwoToneEncodeMax), 0x20 (32)
    /// bytes each, found via the row's own distinctive Name (UTF-16LE) as a
    /// grep anchor. Stride confirmed via 2 independent row-to-row deltas
    /// (row 1→2, and row 2→4 landing exactly where 2×stride predicted).
    ///
    /// <see cref="TwoToneDecodeData"/> - the Decode tab's 16-row table (No.
    /// 1-16, see CodeplugLimits.TwoToneDecodeMax), 0x40 (64) bytes each -
    /// genuinely different stride from Encode, confirmed the same way.
    ///
    /// <see cref="TwoToneEncodeBitmap"/>/<see cref="TwoToneDecodeBitmap"/> -
    /// one 32-bit little-endian bitmask each (bit N = row N+1 present),
    /// confirmed definitively via a non-contiguous population test: rows
    /// 1+2 gave bitmap value 3 (0b011), then adding row 4 while leaving
    /// rows 1+2 untouched gave 11 (0b1011) - rules out a simple row COUNT
    /// (would have gone 2→3, not 3→11), confirms a real per-row bitmask.
    /// Sits in the same 16-byte block as <see cref="TwoToneEncodeSettingsData"/>
    /// (Encode tab's scalar fields) - bitmap is bytes 0-3, settings fields
    /// start at offset 0x09 within a LATER block at this address + 0x20,
    /// see TwoToneEncodeSettingsCodec's own doc comment. Bytes 4-8 and the
    /// whole 0x2810 block beyond byte 3 are unconfirmed/unused in every
    /// capture so far - preserved untouched by the codec either way, same
    /// "unknown bytes round-trip via RMW" discipline as 5Tone.</summary>
    public const int TwoToneEncodeData = 0x3482000;
    public const int TwoToneEncodeRecordLength = 0x20;
    public const int TwoToneDecodeData = 0x3482400;
    public const int TwoToneDecodeRecordLength = 0x40;
    public const int TwoToneEncodeBitmap = 0x3482800;
    public const int TwoToneDecodeBitmap = 0x3482810;
    public const int TwoToneEncodeSettingsData = 0x3482820;

    /// <summary>DTMF Settings - found blind across 2 live differential WRITE
    /// captures 2026-08-06 (no reference project data for this entity
    /// either). Three unrelated regions, unlike 2Tone/5Tone's own tighter
    /// clusters:
    ///
    /// <see cref="DtmfSettingsData"/> - the single dialog's own scalar
    /// fields, sandwiched directly between BOT/EOT/Remotely Kill/Remotely
    /// Stun in one contiguous cluster (this order: settings - BOT - EOT -
    /// Remotely Kill - Remotely Stun, each exactly 0x10 (16) bytes).
    /// Confirmed field-by-field via 2 rounds - round 1 set every field to a
    /// distinctive nonzero value, round 2 changed exactly 5 of them
    /// (Interval Character, Group Code, PTT ID Pause Time, D Code Pause,
    /// and DTMF Transmitting Time elsewhere) and diffed against round 1,
    /// which is what nailed the 4 "Off"/symbol-value sentinels that round 1
    /// alone couldn't reach. One byte (offset 0x09) stayed 0x00 across both
    /// rounds and was never attributed to any field - preserved untouched
    /// via the usual RMW discipline, not Transmitting Time (independently
    /// confirmed elsewhere, see <see cref="DtmfTransmittingTimeIndexData"/>).
    ///
    /// Interval Character/Group Code use a RAW SYMBOL VALUE encoding, not a
    /// list-position index - confirmed both directly (Interval Character
    /// 'B' -&gt; 0x0B, changed to '*' -&gt; 0x0E) and indirectly (the same
    /// value reappears as the composed-code marker in <see
    /// cref="DtmfEncodeData"/> - see that constant's own doc comment). This
    /// matches DTMF's own natural 16-symbol alphabet (0-9, A-D, *, #) fitting
    /// exactly into a 4-bit nibble - 0-9 -&gt; 0x0-0x9, A-D -&gt; 0xA-0xD, *
    /// -&gt; 0xE confirmed, # -&gt; 0xF inferred by the same pattern but not
    /// independently tested. Group Code's own "Off" sentinel is 0xFF
    /// (confirmed) - genuinely different from PTT ID Pause Time/D Code
    /// Pause's own "Off" sentinel (0x00, matching 5Tone's PttIdPauseTime
    /// convention) - both confirmed via the same differential capture.
    ///
    /// <see cref="DtmfEncodeData"/> - the fixed 16-slot M1-M16 list (see
    /// CodeplugLimits.DtmfEncodeSlotCount), 0x10 (16) bytes each,
    /// completely separate region from the settings cluster above. Same
    /// raw-nibble-per-char/0xFF-padded encoding as BOT/EOT - confirmed via
    /// M1 (typed directly, "1234AB") and M2 (composed via &amp;Special
    /// Call: Other Side ID "456" + Interval Character 'B' + Self ID "123"
    /// -&gt; bytes [04 05 06 0B 01 02 03], byte-for-byte matching the
    /// confirmed composition formula). M2's own composed bytes were also
    /// seen to auto-update between round 1 and round 2 when DTMF Settings'
    /// own Interval Character changed from 'B' to '*' (0x0B -&gt; 0x0E in
    /// M2's own stored bytes too) - the vendor CPS itself keeps composed
    /// slots in sync with the shared settings, matching (not just
    /// resembling) the recompose-on-change behavior already built into
    /// MainViewModel.Dtmf.cs before this was confirmed. No presence bitmap
    /// - blank slots are plain all-0xFF, matching every other "fixed set,
    /// not addable/removable" list in this app.
    ///
    /// <see cref="DtmfTransmittingTimeIndexData"/> - a single standalone
    /// byte, found via an isolated round-2 diff (300ms -&gt; 100ms changed
    /// exactly one byte elsewhere in the codeplug, 0x03 -&gt; 0x01 - a clean
    /// 0-based index into the 5-item [50,100,200,300,500] list). Not
    /// adjacent to the settings cluster or the M1-M16 table.</summary>
    public const int DtmfSettingsData = 0x3481E00;
    public const int DtmfSettingsRecordLength = 0x10;
    public const int DtmfBotData = 0x3481E10;
    public const int DtmfEotData = 0x3481E20;
    public const int DtmfRemotelyKillData = 0x3481E30;
    public const int DtmfRemotelyStunData = 0x3481E40;
    public const int DtmfEncodeData = 0x3500800;
    public const int DtmfEncodeRecordLength = 0x10;
    public const int DtmfTransmittingTimeIndexData = 0x3500023;
}
