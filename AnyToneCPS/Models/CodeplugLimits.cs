namespace AnyToneCPS.Models;

/// <summary>
/// Validation limits for the new codeplug entity types (RadioId, Talkgroup,
/// ScanList, RoamingChannel, RoamingZone, ReceiveGroupList,
/// AutoRepeaterOffsetFrequency), sourced from
/// Docs/AnyTone_D890UV/Field_Reference.md. Only limits the vendor CPS help
/// text/UI actually states (or explicitly marks "inferred") are included
/// here - if the doc doesn't state a number for something, no constant is
/// added for it rather than guessing.
/// </summary>
public static class CodeplugLimits
{
    /// <summary>
    /// The D890UV's real RF coverage is two disjoint bands, not one
    /// continuous span from the VHF floor to the UHF ceiling - confirmed via
    /// OptionalSettingsEntry's VFO Scan/Auto Repeater VHF and UHF frequency
    /// fields (each live-write-confirmed separately, 2026-07-30/08-01), and
    /// re-confirmed 2026-08-07 when a Roaming Channel frequency picked from
    /// the 174-400 dead zone (250.12345 MHz) was silently rejected by the
    /// vendor CPS and reverted to the row's previous value. Any general-
    /// purpose RX/TX frequency field that could legitimately be either band
    /// (Channel, Roaming Channel, APRS TX frequency) must check membership
    /// in EITHER band via <see cref="IsValidVhfOrUhfFrequencyMhz"/>, not a
    /// single min-max range spanning both - a single continuous range check
    /// silently accepts values in the 174-400 dead zone that the radio
    /// itself will reject. Does NOT apply to AM Air (108-145, a real
    /// continuous aircraft band) or FM Broadcast (76-108, a real continuous
    /// broadcast band) - both receive-only features on a genuinely different,
    /// continuous band, not this radio's own VHF/UHF transmit split.
    /// </summary>
    public const double VhfFrequencyMinMhz = 136.0;
    public const double VhfFrequencyMaxMhz = 174.0;
    public const double UhfFrequencyMinMhz = 400.0;
    public const double UhfFrequencyMaxMhz = 480.0;

    public static bool IsValidVhfOrUhfFrequencyMhz(double mhz) =>
        (mhz >= VhfFrequencyMinMhz && mhz <= VhfFrequencyMaxMhz) ||
        (mhz >= UhfFrequencyMinMhz && mhz <= UhfFrequencyMaxMhz);


    /// <summary>
    /// From the doc's §1 Channel Name row: "max 16 characters (from
    /// installer's legacy `70012=Only can input 16 characters!`, confirmed
    /// generically applies to name fields)". Applies to all name fields
    /// across entity types (Radio ID name, Talkgroup name, Scan List name,
    /// Roaming Channel/Zone name, Receive Group List name).
    /// </summary>
    public const int NameMaxLength = 16;

    /// <summary>
    /// Doc §2/§9: "0-15 inferred" for Color Code (Radio ID CC, Roaming
    /// Channel Color Code; same range noted for Channel RX/TX Color Code).
    /// </summary>
    public const int ColorCodeMin = 0;
    public const int ColorCodeMax = 15;

    /// <summary>
    /// Doc §2 Radio ID: "24-bit DMR ID, 1-16776415 typical registered range
    /// (inferred from DMR standard; CPS help just says 'input different DMR
    /// ID's')". Kept exactly as stated in the doc (note: 2^24-1 = 16777215,
    /// one higher than the doc's number - not "corrected" here since we
    /// don't invent numbers not in the source doc).
    /// </summary>
    public const long DmrIdMin = 1;
    public const long DmrIdMax = 16776415;

    /// <summary>Doc §2: "List capacity: 250 Radio IDs."</summary>
    public const int RadioIdListMax = 250;

    /// <summary>Doc §5: "Capacity: **10,000 Talk Groups**".</summary>
    public const int TalkgroupListMax = 10000;

    /// <summary>Confirmed 2026-08-07 via live differential write capture: an
    /// "All Call" Talkgroup's DMR ID field is disabled in the vendor CPS and
    /// always reads back as this sentinel (2^24-1), regardless of what was
    /// typed before switching Call Type to All Call.</summary>
    public const long TalkgroupAllCallDmrIdSentinel = 16777215;

    /// <summary>Doc §9 Roaming Channel: "Capacity: up to 250 Roaming Channels".</summary>
    public const int RoamingChannelMax = 250;

    /// <summary>Confirmed 2026-08-07 via live differential write capture:
    /// Roaming Channel's own Color Code field has a 17th option beyond the
    /// normal 0-15 range - "No Use" - which encodes as the raw byte value
    /// 16. <see cref="ColorCodeMin"/>/<see cref="ColorCodeMax"/> stay 0-15
    /// (correct for every OTHER entity's own Color Code field, which has no
    /// "No Use" option) - this is a separate, Roaming-Channel-only
    /// constant rather than widening the shared one.</summary>
    public const int RoamingChannelColorCodeNoUseValue = 16;

    /// <summary>Confirmed: Roaming Channel's Slot field is
    /// a raw 0-indexed byte - 0=Slot 1, 1=Slot 2, 2=No Use - NOT the 1-or-2
    /// this code originally assumed (which would have rejected the real
    /// Slot 1 value of 0 as invalid, and never recognized 2 as "No Use").</summary>
    public const int RoamingChannelSlotNoUseValue = 2;

    /// <summary>Confirmed 2026-08-10 directly from the vendor CPS's own
    /// Roaming Zone list, which numbers rows 1-64.</summary>
    public const int RoamingZoneMax = 64;

    /// <summary>RoamingZoneCodec's own ChannelSlotCount - the physical
    /// per-zone member capacity, separate from <see cref="RoamingZoneMax"/>
    /// (the number of zones, not members per zone).</summary>
    public const int RoamingZoneMemberMax = 64;

    /// <summary>Doc §3 Zone: "Capacity: 250 Zones" (confirmed directly in
    /// the vendor CPS help text's ListZone topic: "This shows a brief
    /// message of 250 Zones").</summary>
    public const int ZoneListMax = 250;

    /// <summary>Physical capacity of one zone's channel-membership region
    /// (256 x uint16 slots) - see <see cref="Services.Radio.Codecs.ZoneCodec"/>'s
    /// ChannelMemberSlotCount doc comment. The vendor CPS explicitly states
    /// there's no artificial per-zone member limit, so this is a hardware
    /// ceiling, not a vendor-documented figure like the others above.</summary>
    public const int ZoneMemberMax = 256;

    /// <summary>Vendor CPS help text, HPT topic "ListScan" (id 27): "up to
    /// 250 individual Scan Lists" - not captured in Field_Reference.md §4.</summary>
    public const int ScanListMax = 250;

    /// <summary>Physical capacity of one scan list's channel-membership
    /// region - see <see cref="Services.Radio.Codecs.ScanListCodec"/>'s
    /// ChannelMemberSlotCount (50 slots, 0x30 + i*2 for i in 0..49).</summary>
    public const int ScanListMemberMax = 50;

    /// <summary>Vendor CPS help text, HPT topic "ListReceiveGroupCallList"
    /// (id 40): "250 Receive Group Call Lists" - not captured in
    /// Field_Reference.md §7.</summary>
    public const int ReceiveGroupListMax = 250;

    /// <summary>Vendor CPS help text, same topic as <see cref="ReceiveGroupListMax"/>:
    /// "up to 64 TG's per receive group" - matches
    /// <see cref="Services.Radio.Codecs.ReceiveGroupListCodec"/>'s own
    /// TalkgroupSlotCount constant.</summary>
    public const int ReceiveGroupListMemberMax = 64;

    /// <summary>Matches <see cref="Services.Radio.Codecs.AutoRepeaterOffsetCodec"/>'s
    /// EntryCount - no separate vendor doc capacity number found for this
    /// list, so this is the codec's own confirmed slot count, not a
    /// vendor-stated figure like the others above.</summary>
    public const int AutoRepeaterOffsetMax = 250;

    /// <summary>Confirmed 2026-08-03 directly from the vendor CPS Auto
    /// Repeater Offset Frequency dialog: 1.00 kHz - 90.00000 MHz. Vendor CPS
    /// itself always takes MHz as input and only switches its own on-screen
    /// LABEL to kHz for small values - this app keeps MHz consistently
    /// everywhere instead, matching every other frequency field app-wide.
    /// 1 kHz = 0.001 MHz.</summary>
    public const double AutoRepeaterOffsetFrequencyMinMhz = 0.001;
    public const double AutoRepeaterOffsetFrequencyMaxMhz = 90.0;

    /// <summary>Doc §11 Prefabricated SMS: "SMS function allows maximum 100 letters".</summary>
    public const int PrefabricatedSmsTextMaxLength = 100;

    /// <summary>Vendor CPS help text, HPT topic id 502 "AMList": "251 AMs
    /// (250 Normal AMs + VFO AM)" - not captured in Field_Reference.md §19.</summary>
    public const int AmAirMax = 251;

    /// <summary>Vendor CPS help text, HPT topic id 504 "FrmAM"/"Frequency":
    /// "Allows AM air band frequency 108-145MHz. 108-137MHz has best spec,
    /// 138-145MHz spec is not promised." Both bounds accepted (the
    /// 137/145 split is a signal-quality note, not a hard cutoff).</summary>
    public const double AmAirFrequencyMinMhz = 108;
    public const double AmAirFrequencyMaxMhz = 145;

    /// <summary>Hardware slot count for AM Zone - 16 fixed zone slots, see
    /// <see cref="Services.Radio.D890UvMemoryMap.AmZoneCount"/>.</summary>
    public const int AmZoneMax = 16;

    /// <summary>100 normal FM broadcast channel slots (Number 1-100) - the
    /// always-present "home"/VFO channel (vendor CPS help text, HPT topic id
    /// 28 "ListFM": "101 FMs (100 Normal FMs + VFO FM)") is excluded from
    /// this count entirely, same convention as AM Air's own VFO exclusion -
    /// see FmChannelEntry/RadioReadMapper.MapFmChannels. Confirmed
    /// 2026-08-03 as a hard cap, not just a warning.</summary>
    public const int FmChannelMax = 100;

    /// <summary>Confirmed 2026-08-03 directly from the vendor CPS FM
    /// broadcast channel dialog: 76.00-108.00 MHz.</summary>
    public const double FmChannelFrequencyMinMhz = 76.0;
    public const double FmChannelFrequencyMaxMhz = 108.0;

    /// <summary>Analog Address Book's name field is 0x1e=30 bytes (15 UTF-16LE
    /// chars), one shorter than every other entity's 0x20=32-byte/16-char
    /// name field - see <see cref="Services.Radio.Codecs.AnalogAddressCodec"/>.
    /// Deliberately a separate constant from <see cref="NameMaxLength"/>
    /// rather than reusing it, since applying the generic 16 here would
    /// accept a 16th character the wire format has no room for.</summary>
    public const int AnalogAddressNameMaxLength = 15;

    /// <summary>Confirmed 2026-08-04 directly from the vendor CPS
    /// Analog Address Book dialog: No. 1-128.</summary>
    public const int AnalogAddressMax = 128;

    /// <summary>10 digits - matches AnalogAddressCodec's own decode
    /// capacity (5 bytes = 10 BCD hex digits) and the xbenkozx/anytone-cps
    /// reference project's own edit dialog maxLength=10 exactly. The real
    /// vendor CPS UI initially appeared to allow 14, but confirmed
    /// 2026-08-04 that typing more than 10 digits into the real Address
    /// Number field actually CRASHES vendor CPS - i.e. the field accepts
    /// keystrokes past what the underlying data can hold (the same class
    /// of "vendor CPS UI-validation gap" bug found elsewhere,
    /// e.g. the Digital Alarm TG/DMR ID field accepting letters). No live
    /// capture needed - the crash itself settles which number is real.</summary>
    public const int AnalogAddressNumberMaxDigits = 10;

    /// <summary>Confirmed 2026-08-04 directly from the vendor CPS
    /// QDC 1200 Setting &gt; Encode dialog: ID 1-100.</summary>
    public const int Qdc1200IdMax = 100;

    /// <summary>Confirmed 2026-08-04: the QDC 1200 ID table's Name
    /// field max length.</summary>
    public const int Qdc1200IdNameMaxLength = 12;

    /// <summary>Slot count backing both Talkgroup Whitelist and Digital
    /// Contact Whitelist's "Number" field range - confirmed via the
    /// xbenkozx/anytone-cps reference project's own array init
    /// (anytone_memory.cpp: 1000-entry loop), not a vendor CPS doc figure
    /// like the others above, so treated as a soft warning rather than a
    /// hard error.</summary>
    public const int WhitelistSlotMax = 1000;

    /// <summary>Digital Contact List's "Friends List" cap - the vendor CPS's
    /// own error string (Field_Reference.md id 99129): "The number of
    /// friends cannot exceed 1000!". Counted across DigitalContactEntry.IsFriend,
    /// not a separate list - see DigitalContactCodec's own doc comment for
    /// why (the flag is packed into an existing byte, not a separate
    /// region).</summary>
    public const int DigitalContactFriendsMax = 1000;

    /// <summary>Real vendor CPS slot counts for the 3 encryption key/code
    /// lists, confirmed 2026-07-18 directly by reading the vendor
    /// CPS's own grids (not from Field_Reference.md - no doc entry exists
    /// for these). Every slot always exists in that UI (1..max, "Off" for
    /// unset) rather than being a variable-length add-as-you-go list - see
    /// MainViewModel.SeedEncryptionKeySlots and RadioReadMapper's
    /// Map*EncryptionKeys/MapBasicEncryptionCodes.</summary>
    public const int BasicEncryptionCodeCount = 32;

    public const int Arc4EncryptionKeyCount = 34;

    /// <summary>255, not 256 - a single index byte, 1-255; 0 is the
    /// "unpopulated" sentinel (see D890UvMemoryMap.AesEncryptionKeyMaxSlots,
    /// which reserves 256 slots of flash for this reason).</summary>
    public const int AesEncryptionKeyCount = 255;

    /// <summary>Confirmed 2026-08-04 directly from the vendor CPS Hot
    /// Key &gt; Analog Quick Call tab: fixed at 4 rows (No. 1-4). No known
    /// radio address yet - see HotKeyEntry's class doc comment.</summary>
    public const int AnalogQuickCallMax = 4;

    /// <summary>Confirmed 2026-08-04 directly from the vendor CPS Hot
    /// Key &gt; State Information tab: fixed at 32 rows (No. 1-32).</summary>
    public const int StateInformationMax = 32;

    /// <summary>Confirmed 2026-08-04: the State Information text box's
    /// own MaxLength (matches the xbenkozx/anytone-cps reference project's
    /// hotkey_state_info_table_model.cpp QLineEdit::setMaxLength(32) too,
    /// for the D878UVII variant that project actually implemented).</summary>
    public const int StateInformationTextMaxLength = 32;

    /// <summary>Confirmed 2026-08-04: the vendor CPS Hot Key &gt; Hot
    /// Key tab always shows exactly these 18 physical/programmable keys
    /// (Hot Key 1-6, Fun Key+0-9, Fun Key+*, Fun Key+#) - not addable or
    /// removable, unlike Analog Quick Call/State Information above.</summary>
    public const int HotKeyKeyCount = 18;

    /// <summary>Confirmed 2026-08-04 directly from the vendor CPS QDC
    /// Address Book: No. 1-128. UI/model only - no radio address confirmed
    /// yet, see QdcAddressEntry's class doc comment.</summary>
    public const int QdcAddressMax = 128;

    /// <summary>Confirmed 2026-08-04: the QDC Address Book Name
    /// field's max length.</summary>
    public const int QdcAddressNameMaxLength = 16;

    /// <summary>Vendor CPS's own 5Tone Settings screen shows the ID table
    /// as No. 1-100, but row 100 is deliberately NOT offered here - live
    /// capture 2026-08-16 (writing row 100 with a distinctive Name/Message
    /// through the real vendor CPS) proved row 100's write lands at
    /// address 0x34818C0-0x34818FF, byte-for-byte the same range this app
    /// already confirmed (2026-08-05/06) as <c>FiveToneBotData</c> (PTT ID
    /// Starting). Row 100 and BOT alias the same physical storage on this
    /// radio - not an app bug, a real vendor CPS/firmware quirk. Capping
    /// here at 99 keeps this app's own capture region
    /// (<see cref="Services.Radio.RadioCodeplugRawSnapshot"/>) from ever
    /// overlapping BOT's region, same "cap below the vendor's stated max
    /// rather than risk the unsafe boundary" precedent as
    /// <c>D890UvMemoryMap.FiveToneInfoIdSlotCount</c>. Row 1-99 addresses
    /// still confirmed 2026-08-05.</summary>
    public const int FiveToneIdMax = 99;

    public const int FiveToneSelfIdMaxLength = 7;
    public const int FiveToneInformationIdMaxLength = 12;
    public const int FiveToneFunctionNameMaxLength = 7;
    public const int FiveToneEncodeIdMaxLength = 40;
    public const int FiveToneNameMaxLength = 7;
    public const int FiveTonePttIdEncodeIdMaxLength = 24;

    /// <summary>Confirmed 2026-08-06 directly from the vendor CPS
    /// 2Tone Settings screen: Encode tab's No. 1-24, Decode tab's No.
    /// 1-16 - genuinely different caps, unlike 5Tone's single shared ID
    /// table. UI/model only - no radio address confirmed yet.</summary>
    public const int TwoToneEncodeMax = 24;
    public const int TwoToneDecodeMax = 16;

    public const int TwoToneNameMaxLength = 7;
    public const double TwoToneFrequencyMinHz = 288.0;
    public const double TwoToneFrequencyMaxHz = 3106.0;

    /// <summary>Confirmed 2026-08-06 directly from the vendor CPS DTMF
    /// screen: a fixed 16-slot M1-M16 list, not addable/removable, same
    /// "fixed named set" convention as Hot Key's own 18 keys.</summary>
    public const int DtmfEncodeSlotCount = 16;

    public const int DtmfEncodeCodeMaxLength = 16;
    public const int DtmfSelfIdMaxLength = 3;
    public const int DtmfOtherSideIdMaxLength = 3;
    public const int DtmfBotEotMaxLength = 16;
    public const int DtmfRemotelyKillStunMaxLength = 14;

    // No. 1-250 cap already exists as RadioIdListMax (see that constant's
    // own usage) - confirmed 2026-08-06 directly from the vendor CPS
    // Radio ID list screenshot, matches exactly.
    public const int RadioIdDmrIdMaxLength = 8;
    public const int RadioIdNameMaxLength = 17;

    /// <summary>DMR ID maxlength confirmed 2026-08-06 from the vendor
    /// CPS Master ID popup screenshot. Name maxlength was ORIGINALLY given
    /// as 26 (matching Radio ID list's own, probably by mistake, since the
    /// 2 screens are structurally similar) - corrected to 16 the same day
    /// after testing the real vendor CPS directly: typing all 26
    /// characters into the popup, clicking OK, then reopening the popup
    /// showed it silently truncated to 16 - confirms MasterIdCodec's own
    /// reference-derived Name field capacity (32 bytes = 16 UTF-16LE
    /// chars) was right all along. Write support still pending a live
    /// capture to confirm the DMR ID/Used byte offsets, see MasterIdEntry's
    /// own class doc comment - this constant is now presumed correct.</summary>
    public const int MasterIdDmrIdMaxLength = 8;
    public const int MasterIdNameMaxLength = 16;
}
