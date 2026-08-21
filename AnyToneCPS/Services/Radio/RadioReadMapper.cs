using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AnyToneCPS.Models;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Converts the raw, byte-level results of a <see cref="RadioCodeplugReader"/>
/// read into the app's existing UI-facing Entry models (ChannelEntry,
/// ZoneEntry, RadioIdEntry, etc).
///
/// IMPORTANT - confidence levels: all fields below map cleanly and
/// confidently (frequency, name, color code, time slot, PTT ID, and all
/// simple booleans use the exact byte layouts confirmed via live USB
/// capture and cross-referenced against the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps). Squelch Mode, Optional Signal, the
/// CTCSS/DCS mode bits, and the Busy-Lock/TX-Permit bits were confirmed
/// 2026-07-17 via a live differential test: ~10 spare channels were
/// configured with specific field combinations in the REAL vendor CPS
/// (D890UV_Setup_1.05.exe, not the third-party xbenkozx clone used
/// elsewhere for byte-layout research), written to a real D890UV,
/// then read back read-only and compared. That test found Squelch Mode is
/// actually a 3-bit field (not 2 bits as first assumed - the official CPS
/// has 5 Squelch Mode options, which only fit in 3 bits) and that
/// Busy-Lock/TX-Permit is ONE shared 4-value enum for both digital and
/// analog channels, not two separate lists. See each mapping function's
/// comment below for the specific confirmed values.
/// </summary>
public static class RadioReadMapper
{
    public static IReadOnlyList<ChannelEntry> MapChannels(RadioCodeplugReadResult result)
    {
        var entries = new List<ChannelEntry>();

        foreach (var ch in result.Channels)
        {
            if (ch.IsBlank)
            {
                continue;
            }

            var entry = new ChannelEntry
            {
                Number = ch.Index + 1, // radio uses 0-based slots; CPS displays 1-based numbers
                Name = ch.Name,
                RxFrequencyMHz = ch.RxFrequencyMHz,
                OffsetMHz = ch.OffsetMHz,
                OffsetDirection = ch.OffsetDirection,
                ChannelType = ch.ChannelType,
                TransmitPower = ch.TxPower,
                Bandwidth = ch.BandWidth,
                ColorCode = ch.RxColorCode,
                TxColorCode = ch.TxColorCode,
                RepeaterSlot2 = ch.TimeSlot,
                PttId = ch.PttId,
                TalkAround = ch.Talkaround,
                CallConfirmation = ch.CallConfirmation,
                PttProhibit = ch.PttProhibit,
                Reverse = ch.Reverse,
                WorkAlone = ch.WorkAlone,
                SlotSuit = ch.SlotSuit,
                SmsConfirmation = ch.SmsConfirmation,
                AutoScan = ch.AutoScan,
                AesEncryptionIndex = ch.AesEncryptionIndex,
                Arc4EncryptionKeyIndex = ch.Arc4EncryptionKeyIndex,
                DigitalEncryptionIndex = ch.DigitalEncryption,
                CorrectFrequencyHz = ch.CorrectFrequency,
                CustomCtcss = ch.CustomCtcss,
                CtcssEncodeTone = ch.CtcssEncodeTone,
                CtcssDecodeTone = ch.CtcssDecodeTone,
                DcsEncodeTone = ch.DcsEncodeTone,
                DcsDecodeTone = ch.DcsDecodeTone,
                DmrModeDcdm = ch.DmrModeDcdm,
                DmrMode = ch.DmrMode,
                ScrambleMode = ch.ScramblerSet,
                CustomScrambleFrequencyIndex = ch.CustomScrambler,
                DmrCrcIgnore = ch.DmrCrcIgnore,
                SendTalkerAlias = ch.SendTalkerAlias,
                SmsForbid = ch.SmsForbid,
                DataAckDisable = ch.DataAckDisable,
                ExcludeChannelRoaming = ch.ExcludeChannelRoaming,
                AesRandomKey = ch.AesRandomKey,
                AesMultipleKey = ch.AesMultipleKey,
                AprsRx = ch.AprsRx,
                DtmfIdIndex = ch.DtmfIdIndex,
                Tone2IdIndex = ch.Tone2IdIndex,
                Tone5IdIndex = ch.Tone5IdIndex,
                Tone2Decode = ch.Tone2Decode,
                R5ToneBot = ch.R5ToneBot,
                R5ToneEot = ch.R5ToneEot,
                QdcIdIndex = ch.QdcIdIndex,
                ExtendEncryption = ch.ExtendEncryption,
                TxInterrupt = ch.TxInterrupt,
                IdleTx = ch.IdleTx,
                Ranging = ch.Ranging,

                // Confirmed 2026-07-17 via live differential test - see class doc comment.
                SquelchMode = ch.SquelchMode,
                OptionalSignal = ch.OptionalSignal,
                BusyLock = ch.BusyLock,
                CtcssDcsDecode = ch.CtcssDcsDecode,
                CtcssDcsEncode = ch.CtcssDcsEncode,

                ContactIndex = ch.ContactIndex,
                RadioIdIndex = ch.RadioIdIndex,
                ScanListIndex = ch.ScanListIndex,
                ReceiveGroupListIndex = ch.ReceiveGroupCallListIndex
            };

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>Resolves a channel's Contact/Radio ID/Scan List/Receive
    /// Group List index into its display name - reference fields are no
    /// longer stored as a name string on <see cref="ChannelEntry"/> itself
    /// (see that class's doc comment for why), so the UI layer looks the
    /// name up on demand instead.</summary>
    public static string ResolveContactName(ChannelEntry channel, IReadOnlyDictionary<int, string> talkgroupNamesByIndex) =>
        channel.IsDigital ? talkgroupNamesByIndex.GetValueOrDefault(channel.ContactIndex, $"TG idx {channel.ContactIndex}") : "";

    public static string ResolveRadioIdName(ChannelEntry channel, IReadOnlyDictionary<int, string> radioIdNamesByIndex) =>
        channel.IsDigital ? radioIdNamesByIndex.GetValueOrDefault(channel.RadioIdIndex, $"Radio ID idx {channel.RadioIdIndex}") : "";

    public static string ResolveReceiveGroupListName(ChannelEntry channel, IReadOnlyDictionary<int, string> receiveGroupNamesByIndex) =>
        receiveGroupNamesByIndex.GetValueOrDefault(channel.ReceiveGroupListIndex, "None");

    public static IReadOnlyList<ZoneEntry> MapZones(
        RadioCodeplugReadResult result,
        IReadOnlyDictionary<int, ChannelEntry> channelsByRadioIndex)
    {
        var entries = new List<ZoneEntry>();

        foreach (var zone in result.Zones)
        {
            var entry = new ZoneEntry
            {
                Number = zone.Index + 1,
                Name = zone.Name,
                IsHidden = zone.IsHidden
            };

            foreach (var channelIndex in zone.ChannelMembers)
            {
                if (channelsByRadioIndex.TryGetValue(channelIndex, out var channel))
                {
                    entry.Members.Add(channel);
                }
            }

            // AChannelIndex/BChannelIndex are NOT global radio channel-table
            // indices (unlike ChannelMembers, which is) - confirmed 2026-08-01
            // via a live differential write: setting a zone's A/B Channel to
            // its 3rd/6th/8th member produced raw values 2/5/7, matching a
            // plain 0-based POSITION within this zone's own member list, not
            // any of those channels' real radio index. Previously misread as
            // a global index via channelsByRadioIndex - happened to look
            // right only when a zone's member channel numbers coincidentally
            // matched their position (e.g. a zone containing channels
            // numbered 1,2,3...N in that exact order).
            entry.AChannel = zone.AChannelIndex < entry.Members.Count ? entry.Members[zone.AChannelIndex] : entry.Members.FirstOrDefault();
            entry.BChannel = zone.BChannelIndex < entry.Members.Count ? entry.Members[zone.BChannelIndex] : entry.Members.Skip(1).FirstOrDefault() ?? entry.Members.FirstOrDefault();

            entries.Add(entry);
        }

        return entries;
    }

    public static IReadOnlyList<RadioIdEntry> MapRadioIds(RadioCodeplugReadResult result)
    {
        // DmrId != 0 is the only reliable "this slot is real" signal - the
        // Name field on a blank/erased slot decodes from raw 0xFF bytes as
        // UTF-16LE, which is NOT whitespace (it's the noncharacter U+FFFF
        // repeated), so checking Name here would let garbage entries through.
        return result.RadioIds
            .Where(r => r.DmrId != 0)
            .Select(r => new RadioIdEntry { Number = r.Index + 1, DmrId = r.DmrId, Name = r.Name })
            .ToList();
    }

    public static IReadOnlyList<TalkgroupEntry> MapTalkgroups(RadioCodeplugReadResult result)
    {
        // See MapRadioIds comment - DmrId != 0 only, Name is not a reliable
        // blank-slot signal (a bad Name-based filter previously crashed a
        // read here: the reverse-engineered Talkgroup bitmap isn't perfectly reliable,
        // so this filter plus BcdDecimalCodec's graceful 0-on-garbage
        // fallback are both load-bearing, not just one or the other).
        return result.Talkgroups
            .Where(t => t.DmrId != 0)
            .Select(t => new TalkgroupEntry
            {
                Number = t.Index + 1,
                DmrId = t.DmrId,
                Name = t.Name,
                CallType = t.CallType,
                CallAlert = t.CallAlert
            })
            .ToList();
    }

    public static IReadOnlyList<ScanListEntry> MapScanLists(
        RadioCodeplugReadResult result,
        IReadOnlyDictionary<int, ChannelEntry> channelsByRadioIndex)
    {
        var entries = new List<ScanListEntry>();
        foreach (var s in result.ScanLists)
        {
            if (string.IsNullOrWhiteSpace(s.Name) && s.ChannelMemberIndexes.Count == 0)
            {
                continue;
            }

            var entry = new ScanListEntry
            {
                Number = s.Index + 1,
                Name = s.Name,
                PriorityChannelSelect = s.PriorityChannelSelect,
                LookbackTimeA = s.LookbackTimeA,
                LookbackTimeB = s.LookbackTimeB,
                DropoutDelayTime = s.DropoutDelayTime,
                DwellTime = s.DwellTime,
                RevertChannel = s.RevertChannel
            };

            foreach (var idx in s.ChannelMemberIndexes)
            {
                if (channelsByRadioIndex.TryGetValue(idx, out var channel))
                {
                    entry.Members.Add(channel);
                }
            }

            // PriorityChannel1/2's raw wire value is the 1-based channel
            // number, not a 0-based radio index like ChannelMemberIndexes -
            // confirmed 2026-08-02 via a live capture of a brand-new scan
            // list add in vendor CPS (raw 3 for the 3rd programmed channel).
            // channelsByRadioIndex is keyed 0-based, hence the -1.
            entry.PriorityChannel1 = s.PriorityChannel1 is { } p1 ? channelsByRadioIndex.GetValueOrDefault(p1 - 1) : null;
            entry.PriorityChannel2 = s.PriorityChannel2 is { } p2 ? channelsByRadioIndex.GetValueOrDefault(p2 - 1) : null;

            entries.Add(entry);
        }

        return entries;
    }

    public static IReadOnlyList<RoamingChannelEntry> MapRoamingChannels(RadioCodeplugReadResult result)
    {
        return result.RoamingChannels
            .Where(r => r.RxFrequencyMhz > 0 || !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new RoamingChannelEntry
            {
                Number = r.Index + 1,
                RxFrequencyMhz = r.RxFrequencyMhz,
                TxFrequencyMhz = r.TxFrequencyMhz,
                ColorCode = r.ColorCode,
                Slot = r.Slot,
                Name = r.Name
            })
            .ToList();
    }

    /// <summary>Members are resolved to real <see cref="RoamingChannelEntry"/>
    /// objects via <paramref name="roamingChannelsByRadioIndex"/> (0-based,
    /// same convention as <c>channelsByRadioIndex</c> in <see cref="MapZones"/>) -
    /// see RoamingZoneEntry's own doc comment for why.</summary>
    public static IReadOnlyList<RoamingZoneEntry> MapRoamingZones(
        RadioCodeplugReadResult result,
        IReadOnlyDictionary<int, RoamingChannelEntry> roamingChannelsByRadioIndex)
    {
        var entries = new List<RoamingZoneEntry>();
        foreach (var z in result.RoamingZones)
        {
            if (string.IsNullOrWhiteSpace(z.Name) && z.RoamingChannelIndexes.Count == 0)
            {
                continue;
            }

            var entry = new RoamingZoneEntry { Number = z.Index + 1, Name = z.Name };
            foreach (var idx in z.RoamingChannelIndexes)
            {
                if (roamingChannelsByRadioIndex.TryGetValue(idx, out var roamingChannel))
                {
                    entry.Members.Add(roamingChannel);
                }
            }

            entries.Add(entry);
        }

        return entries;
    }

    public static IReadOnlyList<ReceiveGroupListEntry> MapReceiveGroupLists(RadioCodeplugReadResult result)
    {
        var entries = new List<ReceiveGroupListEntry>();
        foreach (var g in result.ReceiveGroupLists)
        {
            if (string.IsNullOrWhiteSpace(g.Name) && g.TalkgroupIndexes.Count == 0)
            {
                continue;
            }

            var entry = new ReceiveGroupListEntry { Number = g.Index + 1, Name = g.Name };
            foreach (var idx in g.TalkgroupIndexes)
            {
                entry.TalkgroupIndexes.Add(idx);
            }

            entries.Add(entry);
        }

        return entries;
    }

    public static IReadOnlyList<AutoRepeaterOffsetEntry> MapAutoRepeaterOffsets(RadioCodeplugReadResult result)
    {
        return result.AutoRepeaterOffsets
            .Select(a => new AutoRepeaterOffsetEntry { Number = a.Index + 1, OffsetFrequencyMhz = a.OffsetFrequencyMhz })
            .ToList();
    }

    /// <summary>Unconfigured slots (OperationType==0, already filtered out
    /// by RadioCodeplugReader.ReadAnalogQuickCalls) never reach here - same
    /// "sparse UI over a flat always-present wire array" convention as
    /// MapAutoRepeaterOffsets.</summary>
    public static IReadOnlyList<AnalogQuickCallEntry> MapAnalogQuickCalls(RadioCodeplugReadResult result)
    {
        return result.AnalogQuickCalls
            .Select(a => new AnalogQuickCallEntry { Number = a.Index + 1, OperationType = a.OperationType, CallId = a.CallId })
            .ToList();
    }

    /// <summary>Blank slots are skipped, same convention as
    /// MapAutoRepeaterOffsets/MapAnalogQuickCalls above.</summary>
    public static IReadOnlyList<StateInformationEntry> MapStateInformation(RadioCodeplugReadResult result)
    {
        var entries = new List<StateInformationEntry>();
        for (var i = 0; i < result.StateInformation.Count; i++)
        {
            var text = result.StateInformation[i];
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            entries.Add(new StateInformationEntry { Number = i + 1, Content = text });
        }

        return entries;
    }

    /// <summary>Unlike MapAnalogQuickCalls/MapStateInformation above, every
    /// record is kept - Hot Key is a fixed named 18-row list with no
    /// Add/Remove (see HotKeyEntry's class doc comment), so the Key name
    /// comes from HotKeyEntry.KeyNames rather than the wire (which has no
    /// name field at all).</summary>
    public static IReadOnlyList<HotKeyEntry> MapHotKeys(RadioCodeplugReadResult result)
    {
        return result.HotKeys
            .Select(h => new HotKeyEntry
            {
                Key = HotKeyEntry.KeyNames[h.Index],
                Mode = h.Mode,
                Menu = h.Menu,
                CallType = h.CallType,
                CallObject = h.CallObject,
                DigiCallType = h.DigiCallType,
                Content = h.Content
            })
            .ToList();
    }

    /// <summary>A blank Name means "unconfigured" (already filtered out by
    /// RadioCodeplugReader.ReadQdc1200Ids). Property order matters here -
    /// CallType/Type both drive Qdc1200IdEntry's own NeedToAnswer reset-
    /// cascade (see its OnCallTypeChanged/OnTypeChanged), so they're set
    /// BEFORE NeedToAnswer itself, same object-initializer discipline
    /// established after the Hot Key mapper bug.</summary>
    public static IReadOnlyList<Qdc1200IdEntry> MapQdc1200Ids(RadioCodeplugReadResult result)
    {
        return result.Qdc1200Ids
            .Select(q => new Qdc1200IdEntry
            {
                Number = q.Index + 1,
                CallType = q.CallType,
                Type = q.Type,
                NeedToAnswer = q.NeedToAnswer,
                PrivateCallId = q.PrivateCallId,
                GroupCallId = q.GroupCallId,
                Name = q.Name
            })
            .ToList();
    }

    /// <summary>A blank Name means "unconfigured" (already filtered out by
    /// RadioCodeplugReader.ReadQdcAddresses). CallType/Type are set BEFORE
    /// Ack, same object-initializer discipline as MapQdc1200Ids above -
    /// QdcAddressEntry's own OnTypeChanged resets Ack when it becomes
    /// disabled, so Type must be in place first.</summary>
    public static IReadOnlyList<QdcAddressEntry> MapQdcAddresses(RadioCodeplugReadResult result)
    {
        return result.QdcAddresses
            .Select(q => new QdcAddressEntry
            {
                Number = q.Index + 1,
                CallType = q.CallType,
                Type = q.Type,
                Ack = q.Ack,
                PrivateCallId = q.PrivateCallId,
                GroupCallId = q.GroupCallId,
                Name = q.Name
            })
            .ToList();
    }

    /// <summary>Applies the decoded QDC 1200 Setting (single instance, not
    /// a list) onto an existing <see cref="Qdc1200SettingsEntry"/> the
    /// caller owns - same pattern as <see cref="ApplyAlarmSettings"/>.</summary>
    public static void ApplyQdc1200Settings(RadioCodeplugReadResult result, Qdc1200SettingsEntry target)
    {
        if (result.Qdc1200Settings is not { } settings)
        {
            return;
        }

        target.AutoResetTime = settings.AutoResetTime;
        target.SelfIdPrivateCall = settings.SelfIdPrivateCall;
        target.SelfIdGroupCall = settings.SelfIdGroupCall;
        target.RemoteListeningDuration = settings.RemoteListeningDuration;
        target.RemotelyKillAllow = settings.RemotelyKillAllow;
        target.RemotelyMonitorAllow = settings.RemotelyMonitorAllow;
        target.SideTone = settings.SideTone;
        target.MaxAckWaitTime = settings.MaxAckWaitTime;
        target.Pretime = settings.Pretime;
        target.ResendCode = settings.ResendCode;
    }

    /// <summary>Standard/Time Of Encode Tone/Name are set before SpecialCall
    /// so the row's own IsEncodeIdEnabled/IsEncodeIdHexOnly are already
    /// correct once SpecialCall.IsConfigured flips - same object-
    /// initializer discipline as MapQdc1200Ids. EncodeId is set AFTER
    /// SpecialCall regardless of Calling Type: for Send Message, the
    /// entry's own auto-compose subscription deliberately does nothing
    /// (see FiveToneIdEntry's own doc comment - the composition formula
    /// there is row-level-specific and already matches what the codec
    /// decoded), and for "manual" (no marker, includes ANI - see
    /// FiveToneIdCodec's own doc comment for why those share a bucket) the
    /// auto-compose subscription never fires at all since IsConfigured is
    /// false - so an explicit assignment here is the only thing that
    /// reliably sets it in every case.</summary>
    public static IReadOnlyList<FiveToneIdEntry> MapFiveToneIds(RadioCodeplugReadResult result)
    {
        return result.FiveToneIds
            .Select(f =>
            {
                var entry = new FiveToneIdEntry
                {
                    Number = f.Index + 1,
                    Standard = f.Standard,
                    TimeOfEncodeTone = f.TimeOfEncodeTone,
                    Name = f.Name
                };
                ApplyFiveToneSpecialCall(entry.SpecialCall, f.SpecialCall);
                entry.EncodeId = f.EncodeId;

                // Information ID NO. selects a row by its own Number, so
                // only rows whose Number is within the slot array's own
                // range (1-FiveToneInfoIdSlotCount) have Function Option/
                // Function Decoding Response/Information ID/Function Name
                // on the wire at all - see D890UvMemoryMap.FiveToneInfoIdData's
                // own doc comment for why that cap exists.
                if (entry.Number is >= 1 and <= CodeplugLimits.FiveToneIdMax
                    && entry.Number <= result.FiveToneInfoIdSlots.Count)
                {
                    var slot = result.FiveToneInfoIdSlots[entry.Number - 1];
                    entry.FunctionOption = slot.FunctionOption;
                    entry.FunctionDecodingResponse = slot.FunctionDecodingResponse;
                    entry.InformationId = slot.InformationId;
                    entry.FunctionName = slot.FunctionName;
                }

                return entry;
            })
            .ToList();
    }

    /// <summary>Applies the decoded Decode/Information ID/Encode singleton
    /// block plus BOT/EOT onto an existing <see cref="FiveToneSettingsEntry"/>
    /// the caller owns - same pattern as <see cref="ApplyQdc1200Settings"/>.
    /// Function Option/Function Decoding Response/Information ID/Function
    /// Name are handled separately by <see cref="MapFiveToneIds"/> (they're
    /// per-row, not part of this singleton) - Stop Code and Information ID
    /// NO. itself are the only fields still unhandled here (Stop Code never
    /// independently located; Information ID NO. is a transient UI
    /// selector, not a stored value).</summary>
    public static void ApplyFiveToneSettings(RadioCodeplugReadResult result, FiveToneSettingsEntry target)
    {
        if (result.FiveToneSettings is { } settings)
        {
            target.SelfId = settings.SelfId;
            target.DecodeStandard = settings.DecodeStandard;
            target.DecodingResponse = settings.DecodingResponse;
            target.DecodeTimeMs = settings.DecodeTimeMs;
            target.DecUnit1 = settings.DecUnit1;
            target.DecUnit2 = settings.DecUnit2;
            target.DecUnit3 = settings.DecUnit3;
            target.DecUnit4 = settings.DecUnit4;
            target.DecUnit5 = settings.DecUnit5;
            target.DecUnit6 = settings.DecUnit6;
            target.DecUnit7 = settings.DecUnit7;
            target.DispAnyId = settings.DispAnyId;
            target.Pretime = settings.Pretime;
            target.AutoResetTime = settings.AutoResetTime;
            target.TimeLapseAfterEncode = settings.TimeLapseAfterEncode;
            target.PttIdPauseTime = settings.PttIdPauseTime;
            target.FirstToneLength = settings.FirstToneLength;
            target.StopTimeLength = settings.StopTimeLength;
            target.FirstToneLengthAfterStop = settings.FirstToneLengthAfterStop;
            target.SideTone = settings.SideTone;
        }

        if (result.FiveToneBot is { } bot)
        {
            target.BotStandard = bot.Standard;
            target.BotTimeOfEncodeTone = bot.TimeOfEncodeTone;
            ApplyFiveToneSpecialCall(target.BotSpecialCall, bot.SpecialCall);
            target.BotEncodeId = bot.EncodeId;
        }

        if (result.FiveToneEot is { } eot)
        {
            target.EotStandard = eot.Standard;
            target.EotTimeOfEncodeTone = eot.TimeOfEncodeTone;
            ApplyFiveToneSpecialCall(target.EotSpecialCall, eot.SpecialCall);
            target.EotEncodeId = eot.EncodeId;
        }
    }

    private static void ApplyFiveToneSpecialCall(FiveToneSpecialCallEntry target, FiveToneSpecialCallCodecValues source)
    {
        target.CallingType = source.CallingType switch
        {
            FiveToneCallingType.SendMessage => FiveToneSpecialCallEntry.CallingTypeSendMessage,
            FiveToneCallingType.PttId => FiveToneSpecialCallEntry.CallingTypePttId,
            _ => FiveToneSpecialCallEntry.CallingTypeAni
        };
        target.OtherSideId = source.OtherSideId;
        target.Message = source.Message;
        target.IntervalCharacter = 0;
        target.IsConfigured = source.IsConfigured;
    }

    public static IReadOnlyList<TwoToneEncodeEntry> MapTwoToneEncodeEntries(RadioCodeplugReadResult result)
    {
        return result.TwoToneEncodeEntries
            .Select(e => new TwoToneEncodeEntry
            {
                Number = e.Index + 1,
                FirstToneFrequencyHz = e.FirstToneFrequencyHz,
                SecondToneFrequencyHz = e.SecondToneFrequencyHz,
                Name = e.Name
            })
            .ToList();
    }

    public static IReadOnlyList<TwoToneDecodeEntry> MapTwoToneDecodeEntries(RadioCodeplugReadResult result)
    {
        return result.TwoToneDecodeEntries
            .Select(e => new TwoToneDecodeEntry
            {
                Number = e.Index + 1,
                FirstToneFrequencyHz = e.FirstToneFrequencyHz,
                SecondToneFrequencyHz = e.SecondToneFrequencyHz,
                DecodingResponse = e.DecodingResponse,
                Name = e.Name
            })
            .ToList();
    }

    public static void ApplyTwoToneEncodeSettings(RadioCodeplugReadResult result, TwoToneEncodeSettingsEntry target)
    {
        if (result.TwoToneEncodeSettings is { } settings)
        {
            target.FirstToneDurationSeconds = settings.FirstToneDurationSeconds;
            target.SecondToneDurationSeconds = settings.SecondToneDurationSeconds;
            target.LongToneDurationSeconds = settings.LongToneDurationSeconds;
            target.GapTimeMs = settings.GapTimeMs;
            target.AutoResetTimeSeconds = settings.AutoResetTimeSeconds;
            target.SideTone = settings.SideTone;
        }
    }

    public static void ApplyDtmfSettings(RadioCodeplugReadResult result, DtmfSettingsEntry target)
    {
        if (result.DtmfSettings is { } settings)
        {
            target.IntervalCharacter = settings.IntervalCharacter;
            target.GroupCode = settings.GroupCode;
            target.DecodingResponse = settings.DecodingResponse;
            target.PretimeMs = settings.PretimeMs;
            target.FirstDigitTimeMs = settings.FirstDigitTimeMs;
            target.AutoResetTimeSeconds = settings.AutoResetTimeSeconds;
            target.SelfId = settings.SelfId;
            target.TimeLapseAfterEncodeMs = settings.TimeLapseAfterEncodeMs;
            target.PttIdPauseTimeSeconds = settings.PttIdPauseTimeSeconds;
            target.PttId = settings.PttId;
            target.DCodePauseSeconds = settings.DCodePauseSeconds;
            target.SideTone = settings.SideTone;
        }

        target.PttIdStartingBot = result.DtmfBot;
        target.PttIdEndingEot = result.DtmfEot;
        target.RemotelyKill = result.DtmfRemotelyKill;
        target.RemotelyStun = result.DtmfRemotelyStun;
        target.TransmittingTimeMs = result.DtmfTransmittingTimeMs;
    }

    public static IReadOnlyList<DtmfEncodeEntry> MapDtmfEncodeEntries(RadioCodeplugReadResult result)
    {
        return result.DtmfEncodeEntries
            .Select(e => new DtmfEncodeEntry { Number = e.Index + 1, Code = e.Code })
            .ToList();
    }

    public static IReadOnlyList<AnalogAddressEntry> MapAnalogAddresses(RadioCodeplugReadResult result)
    {
        return result.AnalogAddresses
            .Select(a => new AnalogAddressEntry { Number = a.Index + 1, AddressNumber = a.Number, Name = a.Name })
            .ToList();
    }

    public static IReadOnlyList<PrefabricatedSmsEntry> MapPrefabricatedSms(RadioCodeplugReadResult result)
    {
        return result.PrefabricatedSms
            .Select(s => new PrefabricatedSmsEntry { Number = s.SlotIndex + 1, Text = s.Text })
            .ToList();
    }

    /// <summary>AM Air is blank-filtered on frequency==0, same convention
    /// used for Channel/RoamingChannel. The special VFO slot (index 256) is
    /// deliberately excluded entirely - corrected 2026-08-02 to match Channel's own handling of its VFO A/B
    /// slots (indices 4000/4001, excluded at RadioCodeplugReader.ReadChannels
    /// so they never even reach this mapper) - not exposed as an editable
    /// list row at all, rather than a special-cased entry with a made-up
    /// "Number".</summary>
    public static IReadOnlyList<AmAirEntry> MapAmAir(RadioCodeplugReadResult result)
    {
        return result.AmAirChannels
            .Where(a => a.Index != AmAirCodec.VfoIndex && a.FrequencyMHz > 0)
            .Select(a => new AmAirEntry { Number = a.Index + 1, FrequencyMhz = a.FrequencyMHz, Name = a.Name })
            .ToList();
    }

    /// <summary>AM Zone's AChannelIndex is read from a separate flat
    /// per-zone array (D890UvMemoryMap.AmZoneAChannel, one uint16 per zone
    /// slot), NOT embedded inside the zone's own 0x80-byte record like
    /// regular Zone's AChannel/BChannel are - and regular Zone's AChannel/
    /// BChannel turned out to be a 0-based POSITION within that zone's own
    /// member list, not a global radio index (see MapZones' doc comment for
    /// the live differential test that found this). Given the structural
    /// difference (a genuinely separate table, not something embedded in a
    /// variable-length member list), AChannelIndex is treated here as a
    /// global AM Air radio index instead, matching MemberChannelIndexes'
    /// own resolution - confirmed 2026-08-02 via a live read (AChannelIndex
    /// raw 0 for a zone whose vendor-CPS-displayed A Channel was AM CH 001,
    /// AM Air's own radio index 0).</summary>
    public static IReadOnlyList<AmZoneEntry> MapAmZones(RadioCodeplugReadResult result, IReadOnlyDictionary<int, AmAirEntry> amAirChannelsByRadioIndex)
    {
        var entries = new List<AmZoneEntry>();
        foreach (var z in result.AmZones)
        {
            if (string.IsNullOrWhiteSpace(z.Name) && z.MemberChannelIndexes.Count == 0)
            {
                continue;
            }

            var entry = new AmZoneEntry { Number = z.Index + 1, Name = z.Name };
            foreach (var idx in z.MemberChannelIndexes)
            {
                if (amAirChannelsByRadioIndex.TryGetValue(idx, out var channel))
                {
                    entry.Members.Add(channel);
                }
            }

            entry.AChannel = amAirChannelsByRadioIndex.TryGetValue(z.AChannelIndex, out var aChannel) ? aChannel : entry.Members.FirstOrDefault();

            foreach (var idx in z.ScanChannelIndexes)
            {
                if (amAirChannelsByRadioIndex.TryGetValue(idx, out var channel))
                {
                    entry.ScanChannelMembers.Add(channel);
                }
            }

            entries.Add(entry);
        }

        return entries;
    }

    // Excludes the always-present "home"/VFO slot (FmChannelCodec.HomeIndex)
    // before it ever reaches the model - matches MapAmAir's own VFO
    // exclusion (see that method's doc comment).
    public static IReadOnlyList<FmChannelEntry> MapFmChannels(RadioCodeplugReadResult result)
    {
        return result.FmChannels
            .Where(f => f.Index != FmChannelCodec.HomeIndex)
            .Select(f => new FmChannelEntry { Number = f.Index + 1, FrequencyMhz = f.FrequencyMHz, Name = f.Name, ScanAdd = f.ScanAdd })
            .ToList();
    }

    public static IReadOnlyList<TalkgroupWhitelistEntry> MapTalkgroupWhitelist(RadioCodeplugReadResult result)
    {
        return result.TalkgroupWhitelist
            .Select(w => new TalkgroupWhitelistEntry { Number = w.Id + 1, DmrId = w.DmrId, CallType = w.CallType })
            .ToList();
    }

    public static IReadOnlyList<DigitalContactWhitelistEntry> MapDigitalContactWhitelist(RadioCodeplugReadResult result)
    {
        return result.DigitalContactWhitelist
            .Select(w => new DigitalContactWhitelistEntry { Number = w.Id + 1, DmrId = w.DmrId, CallType = w.CallType })
            .ToList();
    }

    /// <summary>Digital Contact database - only populated when the caller
    /// opted in to <see cref="RadioCodeplugReader.Read"/>'s
    /// <c>includeDigitalContacts</c> parameter; empty otherwise.</summary>
    public static IReadOnlyList<DigitalContactEntry> MapDigitalContacts(RadioCodeplugReadResult result)
    {
        return result.DigitalContacts
            .Select(c => new DigitalContactEntry
            {
                Index = c.Index,
                CallType = c.CallType,
                CallAlert = c.CallAlert,
                IsFriend = c.IsFriend,
                RadioId = c.RadioId,
                Name = c.Name,
                City = c.City,
                Callsign = c.Callsign,
                State = c.State,
                Country = c.Country,
                Remarks = c.Remarks
            })
            .ToList();
    }

    /// <summary>Fixed 32-slot array (see ReadGpsRoaming) - includes all
    /// slots unconditionally, matching the reference project's own
    /// behavior, which never filters/skips any of the 32.</summary>
    public static IReadOnlyList<GpsRoamingEntry> MapGpsRoaming(RadioCodeplugReadResult result)
    {
        return result.GpsRoamingEntries
            .Select(g => new GpsRoamingEntry
            {
                Number = g.Index + 1,
                Enabled = g.Enabled,
                ZoneIndex = g.ZoneIndex,
                LatDegree = g.LatDegree,
                LatMinute = g.LatMinute,
                LatMinuteDecimal = g.LatMinuteDecimal,
                NorthSouth = g.NorthSouth,
                LongDegree = g.LongDegree,
                LongMinute = g.LongMinute,
                LongMinuteDecimal = g.LongMinuteDecimal,
                EastWest = g.EastWest,
                Radius = g.Radius
            })
            .ToList();
    }

    /// <summary>Applies the decoded Master ID (single instance, not a list)
    /// onto an existing <see cref="MasterIdEntry"/> the caller owns.</summary>
    public static void ApplyMasterId(RadioCodeplugReadResult result, MasterIdEntry target)
    {
        if (result.MasterId is not { } masterId)
        {
            return;
        }

        target.DmrId = masterId.DmrId;
        target.Used = masterId.Used;
        target.Name = masterId.Name;
    }

    /// <summary>Applies the decoded Talk Alias Settings (single instance, not
    /// a list) onto an existing <see cref="TalkAliasSettingsEntry"/> the
    /// caller owns.</summary>
    public static void ApplyTalkAliasSettings(RadioCodeplugReadResult result, TalkAliasSettingsEntry target)
    {
        if (result.TalkAliasSettings is not { } talkAlias)
        {
            return;
        }

        target.DisplayPriority = talkAlias.DisplayPriority;
        target.DataFormat = talkAlias.DataFormat;
    }

    /// <summary>Applies the decoded Alarm Settings (single instance, not a
    /// list) onto an existing <see cref="AlarmSettingsEntry"/> the caller
    /// owns.</summary>
    public static void ApplyAlarmSettings(RadioCodeplugReadResult result, AlarmSettingsEntry target)
    {
        if (result.AlarmSettings is not { } alarm)
        {
            return;
        }

        target.AnalogEmergencyAlarm = alarm.AnalogEmergencyAlarm;
        target.AnalogEniType = alarm.AnalogEniType;
        target.AnalogEmergencyId = alarm.AnalogEmergencyId;
        target.AnalogAlarmTime = alarm.AnalogAlarmTime;
        target.AnalogTxDuration = alarm.AnalogTxDuration;
        target.AnalogRxDuration = alarm.AnalogRxDuration;
        target.AnalogEmergencyChannel = alarm.AnalogEmergencyChannel;
        target.AnalogEniSend = alarm.AnalogEniSend;
        target.AnalogEmergencyCycle = alarm.AnalogEmergencyCycle;

        target.DigitalEmergencyAlarm = alarm.DigitalEmergencyAlarm;
        target.DigitalAlarmTime = alarm.DigitalAlarmTime;
        target.DigitalTxDuration = alarm.DigitalTxDuration;
        target.DigitalRxDuration = alarm.DigitalRxDuration;
        target.DigitalEmergencyChannel = alarm.DigitalEmergencyChannel;
        target.DigitalEmergencyCycle = alarm.DigitalEmergencyCycle;
        target.DigitalEniSend = alarm.DigitalEniSend;
        target.DigitalCallType = alarm.DigitalCallType;
        target.DigitalTgDmrId = alarm.DigitalTgDmrId;

        target.ReceiveAlarm = alarm.ReceiveAlarm;
        target.ManDown = alarm.ManDown;
        target.ManDownDelay = alarm.ManDownDelay;

        target.WorkAloneResponseTime = alarm.WorkAloneResponseTime;
        target.WorkAloneWarningTime = alarm.WorkAloneWarningTime;
        target.WorkAloneResponse = alarm.WorkAloneResponse;

        target.QdcGroupId = alarm.QdcGroupId;
        target.QdcPrivateId = alarm.QdcPrivateId;
    }

    /// <summary>Applies the decoded APRS Settings (single instance, not a
    /// list) onto an existing <see cref="AprsSettingsEntry"/> the caller
    /// owns. The two fixed-count sub-lists (AdditionalFixLocations,
    /// DigitalReports) are updated in place by Number rather than
    /// cleared/rebuilt, since they always have the same fixed size.</summary>
    /// <summary>Applies the decoded Optional Settings (single instance, not
    /// a list) onto an existing <see cref="OptionalSettingsEntry"/> the
    /// caller owns. This is a partial port (Power-on/Display/Key Function
    /// only) - see OptionalSettingsCodec's doc comment.</summary>
    public static void ApplyOptionalSettings(RadioCodeplugReadResult result, OptionalSettingsEntry target)
    {
        if (result.OptionalSettings is not { } opt)
        {
            return;
        }

        target.PowerOnInterface = opt.PowerOnInterface;
        target.PowerOnDisplayLine1 = opt.PowerOnDisplayLine1;
        target.PowerOnDisplayLine2 = opt.PowerOnDisplayLine2;
        target.PowerOnPassword = opt.PowerOnPassword;
        target.PowerOnPasswordChar = opt.PowerOnPasswordChar;
        target.DefaultStartupChannel = opt.DefaultStartupChannel;
        target.StartupZoneA = opt.StartupZoneA;
        target.StartupChannelA = opt.StartupChannelA;
        target.StartupZoneB = opt.StartupZoneB;
        target.StartupChannelB = opt.StartupChannelB;
        target.StartupGpsTest = opt.StartupGpsTest;
        target.StartupReset = opt.StartupReset;

        target.Brightness = opt.Brightness;
        target.AutoBacklightDuration = opt.AutoBacklightDuration;
        target.BacklightTxDelay = opt.BacklightTxDelay;
        target.MenuExitTime = opt.MenuExitTime;
        target.TimeDisplay = opt.TimeDisplay;
        target.LastCaller = opt.LastCaller;
        target.CallDisplayMode = opt.CallDisplayMode;
        target.CallsignDisplayColor = opt.CallsignDisplayColor;
        target.CallEndPromptBox = opt.CallEndPromptBox;
        target.DisplayChannelNumber = opt.DisplayChannelNumber;
        target.DisplayCurrentContact = opt.DisplayCurrentContact;
        target.StandbyCharColor = opt.StandbyCharColor;
        target.StandbyBkPicture = opt.StandbyBkPicture;
        target.ShowLastCallOnLaunch = opt.ShowLastCallOnLaunch;
        target.SeparateDisplay = opt.SeparateDisplay;
        target.ChSwitchingKeepsCaller = opt.ChSwitchingKeepsCaller;
        target.BacklightRxDelay = opt.BacklightRxDelay;
        target.ChannelNameColorA = opt.ChannelNameColorA;
        target.ChannelNameColorB = opt.ChannelNameColorB;
        target.ZoneNameColorA = opt.ZoneNameColorA;
        target.ZoneNameColorB = opt.ZoneNameColorB;
        target.DisplayChannelType = opt.DisplayChannelType;
        target.DisplayTimeSlot = opt.DisplayTimeSlot;
        target.DisplayColorCode = opt.DisplayColorCode;
        target.DateDisplayFormat = opt.DateDisplayFormat;
        target.VolumeBar = opt.VolumeBar;

        target.KeyLock = opt.KeyLock;
        target.Pf1ShortKey = opt.Pf1ShortKey;
        target.Pf2ShortKey = opt.Pf2ShortKey;
        target.Pf3ShortKey = opt.Pf3ShortKey;
        target.P1ShortKey = opt.P1ShortKey;
        target.P2ShortKey = opt.P2ShortKey;
        target.Pf1LongKey = opt.Pf1LongKey;
        target.Pf2LongKey = opt.Pf2LongKey;
        target.Pf3LongKey = opt.Pf3LongKey;
        target.P1LongKey = opt.P1LongKey;
        target.P2LongKey = opt.P2LongKey;
        target.LongKeyTime = opt.LongKeyTime;
        target.KnobLock = opt.KnobLock;
        target.KeyboardLock = opt.KeyboardLock;
        target.SideKeyLock = opt.SideKeyLock;
        target.ForcedKeyLock = opt.ForcedKeyLock;

        target.SmsAlert = opt.SmsAlert;
        target.CallAlert = opt.CallAlert;
        target.DigiCallResetTone = opt.DigiCallResetTone;
        target.TalkPermit = opt.TalkPermit;
        target.KeyTone = opt.KeyTone;
        target.DigiIdleChannelTone = opt.DigiIdleChannelTone;
        target.StartupSound = opt.StartupSound;
        target.ToneKeySoundAdjustable = opt.ToneKeySoundAdjustable;
        target.AnalogIdleChannelTone = opt.AnalogIdleChannelTone;
        target.PluginRecordingTone = opt.PluginRecordingTone;

        target.GpsPower = opt.GpsPower;
        target.GpsPositioning = opt.GpsPositioning;
        target.TimeZone = opt.TimeZone;
        target.RangingInterval = opt.RangingInterval;
        target.DistanceUnit = opt.DistanceUnit;
        target.GpsTemplateInformation = opt.GpsTemplateInformation;
        target.GpsInformationChar = opt.GpsInformationChar;
        target.GpsMode = opt.GpsMode;
        target.GpsRoaming = opt.GpsRoaming;

        target.VfoScanType = opt.VfoScanType;
        target.VfoScanStartFreqUhf = opt.VfoScanStartFreqUhf;
        target.VfoScanEndFreqUhf = opt.VfoScanEndFreqUhf;
        target.VfoScanStartFreqVhf = opt.VfoScanStartFreqVhf;
        target.VfoScanEndFreqVhf = opt.VfoScanEndFreqVhf;

        target.AutoRepeaterA = opt.AutoRepeaterA;
        target.AutoRepeaterB = opt.AutoRepeaterB;
        target.AutoRepeater1Uhf = opt.AutoRepeater1Uhf;
        target.AutoRepeater1Vhf = opt.AutoRepeater1Vhf;
        target.AutoRepeater2Uhf = opt.AutoRepeater2Uhf;
        target.AutoRepeater2Vhf = opt.AutoRepeater2Vhf;
        target.RepeaterCheck = opt.RepeaterCheck;
        target.RepeaterCheckInterval = opt.RepeaterCheckInterval;
        target.RepeaterCheckReconnections = opt.RepeaterCheckReconnections;
        target.RepeaterOutOfRangeNotify = opt.RepeaterOutOfRangeNotify;
        target.OutOfRangeNotify = opt.OutOfRangeNotify;
        target.AutoRoaming = opt.AutoRoaming;
        target.AutoRoamingStartCondition = opt.AutoRoamingStartCondition;
        target.AutoRoamingFixedTime = opt.AutoRoamingFixedTime;
        target.RoamingEffectWaitTime = opt.RoamingEffectWaitTime;
        target.RoamingZone = opt.RoamingZone;
        target.AutoRepeater1MinFreqVhf = opt.AutoRepeater1MinFreqVhf;
        target.AutoRepeater1MaxFreqVhf = opt.AutoRepeater1MaxFreqVhf;
        target.AutoRepeater1MinFreqUhf = opt.AutoRepeater1MinFreqUhf;
        target.AutoRepeater1MaxFreqUhf = opt.AutoRepeater1MaxFreqUhf;
        target.AutoRepeater2MinFreqVhf = opt.AutoRepeater2MinFreqVhf;
        target.AutoRepeater2MaxFreqVhf = opt.AutoRepeater2MaxFreqVhf;
        target.AutoRepeater2MinFreqUhf = opt.AutoRepeater2MinFreqUhf;
        target.AutoRepeater2MaxFreqUhf = opt.AutoRepeater2MaxFreqUhf;
        target.RepeaterMode = opt.RepeaterMode;
        target.RepCcLimit = opt.RepCcLimit;
        target.RepSlotA = opt.RepSlotA;
        target.RepSlotB = opt.RepSlotB;

        target.RecordFunction = opt.RecordFunction;
        target.RecordDelay = opt.RecordDelay;

        target.MaxVolume = opt.MaxVolume;
        target.PowerOnVolumeType = opt.PowerOnVolumeType;
        target.PowerOnVolume = opt.PowerOnVolume;
        target.MaxHeadphoneVolume = opt.MaxHeadphoneVolume;
        target.DigiMicGain = opt.DigiMicGain;
        target.EnhancedSoundQuality = opt.EnhancedSoundQuality;
        target.AnalogMicGain = opt.AnalogMicGain;
        target.RxAgc = opt.RxAgc;
        target.NxMicGain = opt.NxMicGain;

        target.DisplayMode = opt.DisplayMode;
        target.VfMrA = opt.VfMrA;
        target.VfMrB = opt.VfMrB;
        target.MemZoneA = opt.MemZoneA;
        target.MemZoneB = opt.MemZoneB;
        target.MainChannelSet = opt.MainChannelSet;
        target.SubChannelMode = opt.SubChannelMode;
        target.WorkingMode = opt.WorkingMode;

        target.VoxLevel = opt.VoxLevel;
        target.VoxDelay = opt.VoxDelay;
        target.VoxDetection = opt.VoxDetection;
        target.BtOnOff = opt.BtOnOff;
        target.BtIntMic = opt.BtIntMic;
        target.BtIntSpk = opt.BtIntSpk;
        target.BtMicGain = opt.BtMicGain;
        target.BtSpkGain = opt.BtSpkGain;
        target.BtHoldTime = opt.BtHoldTime;
        target.BtRxDelay = opt.BtRxDelay;
        target.BtPttHold = opt.BtPttHold;
        target.BtPttSleepTime = opt.BtPttSleepTime;
        target.BtNrBefore = opt.BtNrBefore;
        target.BtNrAfter = opt.BtNrAfter;

        target.SteTypeOfCtcss = opt.SteTypeOfCtcss;
        target.SteWhenNoSignal = opt.SteWhenNoSignal;
        target.SteTime = opt.SteTime;

        target.AmFmFunction = opt.AmFmFunction;
        target.FmVfoMem = opt.FmVfoMem;
        target.FmWorkChannel = opt.FmWorkChannel;
        target.FmMonitor = opt.FmMonitor;
        target.AmVfoMem = opt.AmVfoMem;
        target.AmWorkZone = opt.AmWorkZone;
        target.AmOffset = opt.AmOffset;
        target.AmSqlLevel = opt.AmSqlLevel;

        target.AutoShutdown = opt.AutoShutdown;
        target.PowerSave = opt.PowerSave;
        target.AutoShutdownType = opt.AutoShutdownType;

        target.AddressBookSentWithCode = opt.AddressBookSentWithCode;
        target.Tot = opt.Tot;
        target.Language = opt.Language;
        target.FrequencyStep = opt.FrequencyStep;
        target.SqlLevelA = opt.SqlLevelA;
        target.SqlLevelB = opt.SqlLevelB;
        target.Tbst = opt.Tbst;
        target.AnalogCallHoldTime = opt.AnalogCallHoldTime;
        target.CallChannelMaintained = opt.CallChannelMaintained;
        target.PriorityZoneA = opt.PriorityZoneA;
        target.PriorityZoneB = opt.PriorityZoneB;
        target.MuteTiming = opt.MuteTiming;
        target.EncryptionType = opt.EncryptionType;
        target.TotPredict = opt.TotPredict;
        target.TxPowerAgc = opt.TxPowerAgc;
        target.NoaaMoni = opt.NoaaMoni;
        target.NoaaScan = opt.NoaaScan;
        target.Noaa = opt.Noaa;
        target.NoaaChannel = opt.NoaaChannel;

        target.GroupCallHoldTime = opt.GroupCallHoldTime;
        target.PrivateCallHoldTime = opt.PrivateCallHoldTime;
        target.ManualDialGroupCallHoldTime = opt.ManualDialGroupCallHoldTime;
        target.ManualDialPrivateCallHoldTime = opt.ManualDialPrivateCallHoldTime;
        target.VoiceHeaderRepetitions = opt.VoiceHeaderRepetitions;
        target.TxPreambleDuration = opt.TxPreambleDuration;
        target.FilterOwnId = opt.FilterOwnId;
        target.DigitalRemoteKill = opt.DigitalRemoteKill;
        target.DigitalMonitor = opt.DigitalMonitor;
        target.DigitalMonitorCc = opt.DigitalMonitorCc;
        target.DigitalMonitorId = opt.DigitalMonitorId;
        target.MonitorSlotHold = opt.MonitorSlotHold;
        target.RemoteMonitor = opt.RemoteMonitor;
        target.SmsFormat = opt.SmsFormat;
        target.ResetDigitalProtocol = opt.ResetDigitalProtocol;

        target.SatLocation = opt.SatLocation;
        target.SatTxPower = opt.SatTxPower;
        target.SatAnaSql = opt.SatAnaSql;
        target.SatAosLimit = opt.SatAosLimit;

        foreach (var tone in opt.AlertTones)
        {
            var existing = target.AlertTones.FirstOrDefault(t => t.Category == tone.Category && t.ToneNumber == tone.ToneNumber);
            if (existing is null)
            {
                continue;
            }

            existing.Frequency = tone.Frequency;
            existing.Period = tone.Period;
        }
    }

    public static void ApplyAprsSettings(RadioCodeplugReadResult result, AprsSettingsEntry target)
    {
        if (result.AprsSettings is not { } aprs)
        {
            return;
        }

        target.TxFreq1Mhz = aprs.TxFreq1MHz;
        target.TxDelay = aprs.TxDelay;
        target.SendSubtone = aprs.SendSubtone;
        target.Ctcss = aprs.Ctcss;
        target.Dcs = aprs.Dcs;
        target.ManualTxInterval = aprs.ManualTxInterval;
        target.AutoTxInterval = aprs.AutoTxInterval;
        target.TxTone = aprs.TxTone;
        target.FixedLocationBeacon = aprs.FixedLocationBeacon;

        target.Fix1Lat = aprs.Fix1Lat;
        target.Fix1Ns = aprs.Fix1Ns;
        target.Fix1Lng = aprs.Fix1Lng;
        target.Fix1Ew = aprs.Fix1Ew;

        target.ToCall = aprs.ToCall;
        target.ToCallSsid = aprs.ToCallSsid;
        target.YourCall = aprs.YourCall;
        target.YourCallSsid = aprs.YourCallSsid;
        target.DigipeaterPath = aprs.DigipeaterPath;

        target.AprsSymbol = aprs.AprsSymbol;
        target.MapIcon = aprs.MapIcon;
        target.TxPower = aprs.TxPower;
        target.PrewaveTime = aprs.PrewaveTime;

        target.RoamingSupport = aprs.RoamingSupport;
        target.RepeaterActivationDelay = aprs.RepeaterActivationDelay;
        target.DisTime = aprs.DisTime;
        target.Altitude = aprs.Altitude;
        target.AnalogTxMode = aprs.AnalogTxMode;
        target.PassAll = aprs.PassAll;

        target.TxFreq2Mhz = aprs.TxFreq2MHz;
        target.TxFreq3Mhz = aprs.TxFreq3MHz;
        target.TxFreq4Mhz = aprs.TxFreq4MHz;
        target.TxFreq5Mhz = aprs.TxFreq5MHz;
        target.TxFreq6Mhz = aprs.TxFreq6MHz;
        target.TxFreq7Mhz = aprs.TxFreq7MHz;
        target.TxFreq8Mhz = aprs.TxFreq8MHz;

        target.SendingText = aprs.SendingText;

        target.FilterPosition = aprs.FilterPosition;
        target.FilterMicE = aprs.FilterMicE;
        target.FilterObject = aprs.FilterObject;
        target.FilterItem = aprs.FilterItem;
        target.FilterMessage = aprs.FilterMessage;
        target.FilterWxReport = aprs.FilterWxReport;
        target.FilterNmeaReport = aprs.FilterNmeaReport;
        target.FilterStatusReport = aprs.FilterStatusReport;
        target.FilterOther = aprs.FilterOther;

        foreach (var fix in aprs.FixLocations)
        {
            var existing = target.AdditionalFixLocations.FirstOrDefault(f => f.Number == fix.Number);
            if (existing is null)
            {
                continue;
            }

            existing.Lat = fix.Lat;
            existing.Ns = fix.Ns;
            existing.Lng = fix.Lng;
            existing.Ew = fix.Ew;
        }

        foreach (var report in aprs.DigitalReports)
        {
            var existing = target.DigitalReports.FirstOrDefault(r => r.Number == report.Number);
            if (existing is null)
            {
                continue;
            }

            existing.Channel = report.Channel;
            existing.TalkgroupId = report.TalkgroupId;
            existing.CallType = report.CallType;
            existing.Slot = report.Slot;
        }
    }

    public static IReadOnlyList<AprsReceiveFilterEntry> MapAprsReceiveFilters(RadioCodeplugReadResult result)
    {
        return result.AprsReceiveFilters
            .Select(f => new AprsReceiveFilterEntry { Number = f.Index + 1, Enabled = f.Enabled, Callsign = f.Callsign, Ssid = f.Ssid })
            .ToList();
    }

    // The 3 encryption key/code mappings below. Confirmed 2026-07-18 against
    // the real vendor CPS's own column headers: AES's grid is No./
    // Encryption Key/Encryption ID - the actual key material lives under
    // "Encryption ID", not "Encryption Key" (counter-intuitively). ARC4's
    // grid is the other way round - No./Encryption ID/Encryption Key - so
    // its real key material genuinely is under "Encryption Key". Basic
    // follows AES's layout ("like AES").
    //
    // The OTHER column for each type (the one no radio address was found
    // for) turned out, from direct observation of the real CPS, to
    // always equal the slot's own number for every populated slot
    // checked (AES: 1-10 for its 10 keys; ARC4: 1 for its 1 key) - or a
    // simple transform of it for Basic (01/02/... doubled: "0101","0202").
    // This exactly matches this app's OWN pre-existing default-value
    // generators for a manually-"Add"ed key (see AddAesEncryptionKey/
    // AddArc4EncryptionKey/AddEncryptionKey in MainViewModel.cs), which is
    // why this is computed here rather than decoded from a byte - it's
    // very likely a vendor-CPS-side auto-generated companion value tied to
    // the slot index, not independent stored data. Not independently
    // confirmed by editing that column to something OTHER than the slot
    // number and re-reading - if it turns out to be independently
    // settable, this would need a real address and a differential test
    // like everything else.
    // 2026-07-18: always generate the FULL slot range (1..count), not just
    // the populated ones - matches the real vendor CPS, which always shows
    // every slot with "Off"/"0000" for unset ones rather than a variable-
    // length list. See CodeplugLimits' *EncryptionKeyCount/
    // BasicEncryptionCodeCount doc comment and MainViewModel.SeedEncryptionKeySlots
    // (the same convention applies to a brand new, non-radio-read project).
    public static IReadOnlyList<EncryptionKeyEntry> MapAesEncryptionKeys(RadioCodeplugReadResult result)
    {
        var byNumber = result.AesEncryptionKeys.ToDictionary(k => k.Number, k => k.KeyHex);
        return Enumerable.Range(1, CodeplugLimits.AesEncryptionKeyCount)
            .Select(number => new EncryptionKeyEntry
            {
                Kind = EncryptionKeyKind.Aes,
                Number = number,
                EncryptionIdText = byNumber.GetValueOrDefault(number, "Off"),
                EncryptionKeyText = number.ToString(CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    public static IReadOnlyList<EncryptionKeyEntry> MapArc4EncryptionKeys(RadioCodeplugReadResult result)
    {
        var byNumber = result.Arc4EncryptionKeys.ToDictionary(k => k.Number, k => k.KeyHex);
        return Enumerable.Range(1, CodeplugLimits.Arc4EncryptionKeyCount)
            .Select(number => new EncryptionKeyEntry
            {
                Kind = EncryptionKeyKind.Arc4,
                Number = number,
                EncryptionKeyText = byNumber.GetValueOrDefault(number, "Off"),
                EncryptionIdText = number.ToString(CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    public static IReadOnlyList<EncryptionKeyEntry> MapBasicEncryptionCodes(RadioCodeplugReadResult result)
    {
        var byNumber = result.BasicEncryptionCodes.ToDictionary(c => c.Number, c => c.Code);
        return Enumerable.Range(1, CodeplugLimits.BasicEncryptionCodeCount)
            .Select(number => new EncryptionKeyEntry
            {
                Kind = EncryptionKeyKind.Basic,
                Number = number,
                EncryptionIdText = byNumber.GetValueOrDefault(number, "0000"),
                EncryptionKeyText = $"{number:00}{number:00}"
            })
            .ToList();
    }

    /// <summary>Builds "radio channel slot index -> display name" lookups for
    /// the referenced entities, used to resolve a channel's Contact/RadioId/
    /// ScanList/ReceiveGroupList fields to a human-readable name instead of a
    /// bare index.</summary>
    public static Dictionary<int, string> BuildTalkgroupNameLookup(RadioCodeplugReadResult result) =>
        result.Talkgroups.ToDictionary(t => t.Index, t => string.IsNullOrWhiteSpace(t.Name) ? $"TG {t.DmrId}" : t.Name);

    public static Dictionary<int, string> BuildRadioIdNameLookup(RadioCodeplugReadResult result) =>
        result.RadioIds.ToDictionary(r => r.Index, r => string.IsNullOrWhiteSpace(r.Name) ? r.DmrId.ToString(CultureInfo.InvariantCulture) : r.Name);

    public static Dictionary<int, string> BuildReceiveGroupNameLookup(RadioCodeplugReadResult result) =>
        result.ReceiveGroupLists.ToDictionary(g => g.Index, g => g.Name);

    public static Dictionary<int, string> BuildZoneNameLookup(RadioCodeplugReadResult result) =>
        result.Zones.ToDictionary(z => z.Index, z => z.Name);

    /// <summary>255 is the "no zone" sentinel, confirmed live 2026-08-09 -
    /// see GpsRoamingCodec's own doc comment. Matches the vendor CPS's own
    /// "Off" text for an unconfigured slot's Zone column.</summary>
    public static string ResolveZoneName(int zoneIndex, IReadOnlyDictionary<int, string> zoneNamesByIndex) =>
        zoneIndex == 255 ? "Off" : zoneNamesByIndex.GetValueOrDefault(zoneIndex, $"Zone idx {zoneIndex}");
}
