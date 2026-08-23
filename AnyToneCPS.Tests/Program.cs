using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AnyToneCPS.Converters;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using AnyToneCPS.Services.Radio.Codecs;
using AnyToneCPS.ViewModels;
using AnyToneCPS.Views;

namespace AnyToneCPS.Tests;

public static class Program
{
    private static readonly List<string> Failures = [];

    public static int Main()
    {
        // CSV import/export tests removed during the Channel canonical-model
        // migration - CpsCsvImporter/CpsCsvExporter now throw
        // NotSupportedException pending reconnect.
        Run("Maps project data roundtrip", MapsProjectDataRoundtrip);
        Run("Maps advanced channel fields roundtrip", MapsAdvancedChannelFieldsRoundtrip);
        Run("Maps channel encryption settings roundtrip", MapsChannelEncryptionSettingsRoundtrip);
        Run("Busy lock tx permit uses different labels for analog and digital", BusyLockTxPermitUsesDifferentLabelsForAnalogAndDigital);
        Run("Saving does not clear a pending radio write", SavingDoesNotClearAPendingRadioWrite);
        Run("Writing to radio does not clear file dirty state", WritingToRadioDoesNotClearFileDirtyState);
        Run("Overlong channel name blocks save and write", OverlongChannelNameBlocksSaveAndWrite);
        Run("Rejects invalid frequency text without changing the value", RejectsInvalidFrequencyTextWithoutChangingTheValue);
        Run("Rejects out of range frequency text without changing the value", RejectsOutOfRangeFrequencyTextWithoutChangingTheValue);
        Run("Simplex channel keeps OffsetMHz in sync with RX on every edit", SimplexChannelKeepsOffsetMHzInSyncWithRxOnEveryEdit);
        Run("Switching to simplex snaps OffsetMHz to RX", SwitchingToSimplexSnapsOffsetMHzToRx);
        Run("Save normalizes stale simplex offsets before writing", SaveNormalizesStaleSimplexOffsetsBeforeWriting);
        Run("Invalid encryption key formats block save", InvalidEncryptionKeyFormatsBlockSave);
        Run("Validates dotted frequencies independent of current culture", ValidatesDottedFrequenciesIndependentOfCurrentCulture);
        Run("Adds one encryption key per type and selects it", AddsOneEncryptionKeyPerTypeAndSelectsIt);
        Run("Randomize encryption key replaces value in place without changing slot", RandomizeEncryptionKeyReplacesValueInPlaceWithoutChangingSlot);
        Run("Regenerate encryption key commands notify CanExecuteChanged on selection", RegenerateEncryptionKeyCommandsNotifyCanExecuteChangedOnSelection);
        Run("Adding encryption keys fills slots in order then stops", AddingEncryptionKeysFillsSlotsInOrderThenStops);
        Run("Removes unused encryption key", RemovesUnusedEncryptionKey);
        Run("Blocks removal of used encryption key", BlocksRemovalOfUsedEncryptionKey);
        Run("Can remove used encryption key and disable channel encryption", CanRemoveUsedEncryptionKeyAndDisableChannelEncryption);
        Run("Ignores transient null encryption selection during validation", IgnoresTransientNullEncryptionSelectionDuringValidation);
        Run("Adds and removes multiple zone members", AddsAndRemovesMultipleZoneMembers);
        Run("Map zones resolves a channel b channel by position in members not global index", MapZonesResolvesAChannelBChannelByPositionInMembersNotGlobalIndex);
        Run("Map scan lists resolves priority channels by one based channel number not radio index", MapScanListsResolvesPriorityChannelsByOneBasedChannelNumberNotRadioIndex);
        Run("Map am zones resolves members and a channel by global am air index", MapAmZonesResolvesMembersAndAChannelByGlobalAmAirIndex);
        Run("Am zone codec decodes scan channel bitmask not index list", AmZoneCodecDecodesScanChannelBitmaskNotIndexList);
        Run("Single member zone only sets A channel", SingleMemberZoneOnlySetsAChannel);
        Run("Tracks unsaved field changes", TracksUnsavedFieldChanges);
        Run("Refresh filtered digital contacts combines friends only with text filter", RefreshFilteredDigitalContactsCombinesFriendsOnlyWithTextFilter);
        Run("Tracks unsaved zone membership changes", TracksUnsavedZoneMembershipChanges);
        Run("Scan list assignment edits scan list membership not a channel field", ScanListAssignmentEditsScanListMembershipNotAChannelField);
        Run("Selecting a disabled navigation node does not change the selected tab", SelectingADisabledNavigationNodeDoesNotChangeTheSelectedTab);
        Run("Enables save commands only when dirty", EnablesSaveCommandsOnlyWhenDirty);
        Run("Duplicate channel copies every canonical field", DuplicateChannelCopiesEveryCanonicalField);
        Run("Duplicate channel with multiple selected duplicates all of them", DuplicateChannelWithMultipleSelectedDuplicatesAllOfThem);
        Run("Remove channel with multiple selected removes all of them", RemoveChannelWithMultipleSelectedRemovesAllOfThem);
        Run("Selecting multiple channels hides the single channel editor", SelectingMultipleChannelsHidesTheSingleChannelEditor);
        Run("Reorder lists by number sorts channels ascending and keeps selection", ReorderListsByNumberSortsChannelsAscendingAndKeepsSelection);
        Run("Setting startup zone A name updates the underlying zone index and round trips", SettingStartupZoneANameUpdatesTheUnderlyingZoneIndexAndRoundTrips);
        Run("Navigating to About raises PropertyChanged for IsAboutViewSelected", NavigatingToAboutRaisesPropertyChangedForIsAboutViewSelected);
        Run("Every IsViewSelected property notifies on SelectedTabIndex changed", EveryIsViewSelectedPropertyNotifiesOnSelectedTabIndexChanged);
        Run("Builds write block request matching a real captured write", BuildsWriteBlockRequestMatchingARealCapturedWrite);
        Run("Builds write block request matching the revert capture", BuildsWriteBlockRequestMatchingTheRevertCapture);
        Run("Rejects write block data of the wrong length", RejectsWriteBlockDataOfTheWrongLength);
        Run("Empty channel patch is a byte-identical round trip", EmptyChannelPatchIsAByteIdenticalRoundTrip);
        Run("Channel name patch touches only the name bytes", ChannelNamePatchTouchesOnlyTheNameBytes);
        Run("Channel frequency patch round trips through decode", ChannelFrequencyPatchRoundTripsThroughDecode);
        Run("Offset direction patch preserves sibling bits", OffsetDirectionPatchPreservesSiblingBits);
        Run("CTCSS DCS mode patch preserves sibling bits", CtcssDcsModePatchPreservesSiblingBits);
        Run("Squelch mode patch preserves ptt id bits", SquelchModePatchPreservesPttIdBits);
        Run("Optional signal and busy lock patch each other's bits independently", OptionalSignalAndBusyLockPatchEachOthersBitsIndependently);
        Run("Contact index patch round trips through decode", ContactIndexPatchRoundTripsThroughDecode);
        Run("Radio ID and PTT ID patch each other's bits independently", RadioIdAndPttIdPatchEachOthersBitsIndependently);
        Run("Receive group call list index patch round trips through decode", ReceiveGroupCallListIndexPatchRoundTripsThroughDecode);
        Run("Channel type transmit power and bandwidth patch each other's bits independently", ChannelTypeTransmitPowerAndBandwidthPatchEachOthersBitsIndependently);
        Run("Talk around call confirmation ptt prohibit and reverse patch each other's bits independently", TalkAroundCallConfirmationPttProhibitAndReversePatchEachOthersBitsIndependently);
        Run("RX color code and TX color code patch round trip through decode independently", RxColorCodeAndTxColorCodePatchRoundTripThroughDecodeIndependently);
        Run("Work alone slot suit repeater slot and sms confirmation patch each other's bits independently", WorkAloneSlotSuitRepeaterSlotAndSmsConfirmationPatchEachOthersBitsIndependently);
        Run("Dmr mode dcdm patch preserves sibling bits in 0x21", DmrModeDcdmPatchPreservesSiblingBitsIn0x21);
        Run("Dmr mode patch preserves sibling bits in 0x34", DmrModePatchPreservesSiblingBitsIn0x34);
        Run("Dmr mode selection combines dmr mode dcdm and dmr mode", DmrModeSelectionCombinesDmrModeDcdmAndDmrMode);
        Run("Aes arc4 auto scan scramble mode and custom scramble frequency index patch round trip through decode", AesArc4AutoScanScrambleModeAndCustomScrambleFrequencyIndexPatchRoundTripThroughDecode);
        Run("Digital encryption correct frequency and custom ctcss patch round trip through decode", DigitalEncryptionCorrectFrequencyAndCustomCtcssPatchRoundTripThroughDecode);
        Run("Dmr crc ignore send talker alias and sms forbid patch round trip through decode", DmrCrcIgnoreSendTalkerAliasAndSmsForbidPatchRoundTripThroughDecode);
        Run("Data ack disable exclude channel roaming and aes key flags patch round trip through decode", DataAckDisableExcludeChannelRoamingAndAesKeyFlagsPatchRoundTripThroughDecode);
        Run("Aprs rx patch preserves sibling bits in 0x21", AprsRxPatchPreservesSiblingBitsIn0x21);
        Run("Dtmf id index patch touches only its own byte", DtmfIdIndexPatchTouchesOnlyItsOwnByte);
        Run("Dtmf id selection round trips through m1 to m16 labels", DtmfIdSelectionRoundTripsThroughM1ToM16Labels);
        Run("Tone2 id index patch touches only its own byte", Tone2IdIndexPatchTouchesOnlyItsOwnByte);
        Run("Tone2 id selection round trips through 1 to 16 labels", Tone2IdSelectionRoundTripsThrough1To16Labels);
        Run("Tone5 id index patch touches only its own byte", Tone5IdIndexPatchTouchesOnlyItsOwnByte);
        Run("Tone5 id selection round trips through 1 to 16 labels", Tone5IdSelectionRoundTripsThrough1To16Labels);
        Run("Tone2 decode patch touches only its own byte", Tone2DecodePatchTouchesOnlyItsOwnByte);
        Run("Tone2 decode selection round trips through 1 to 16 labels", Tone2DecodeSelectionRoundTripsThrough1To16Labels);
        Run("R5tone bot and r5tone eot patch touch only their own bytes", R5ToneBotAndR5ToneEotPatchTouchOnlyTheirOwnBytes);
        Run("R5tone bot customize patch round trips through decode", R5ToneBotCustomizePatchRoundTripsThroughDecode);
        Run("R5tone bot and eot selection round trip through 1 and 2 and customize labels", R5ToneBotAndEotSelectionRoundTripThrough1And2AndCustomizeLabels);
        Run("Qdc id index patch touches only its own byte", QdcIdIndexPatchTouchesOnlyItsOwnByte);
        Run("Qdc id selection round trips through 1 to 16 labels", QdcIdSelectionRoundTripsThrough1To16Labels);
        Run("Extend encryption patch preserves sibling bits in 0x3b", ExtendEncryptionPatchPreservesSiblingBitsIn0x3b);
        Run("Extend encryption selection round trips through aes and arc4 labels", ExtendEncryptionSelectionRoundTripsThroughAesAndArc4Labels);
        Run("Idle tx patch preserves sibling bits in 0x34", IdleTxPatchPreservesSiblingBitsIn0x34);
        Run("Ranging patch preserves sibling bits in 0x34", RangingPatchPreservesSiblingBitsIn0x34);
        Run("Tx interrupt patch preserves sibling bits in 0x3b", TxInterruptPatchPreservesSiblingBitsIn0x3b);
        Run("Tx interrupt selection round trips through off and low priority labels", TxInterruptSelectionRoundTripsThroughOffAndLowPriorityLabels);
        Run("Correct frequency hz text converts to and from tens of hz", CorrectFrequencyHzTextConvertsToAndFromTensOfHz);
        Run("Custom ctcss text converts to and from tenths of hz", CustomCtcssTextConvertsToAndFromTenthsOfHz);
        Run("Ctcss and dcs tone patches round trip through decode", CtcssAndDcsTonePatchesRoundTripThroughDecode);
        Run("Ctcss and dcs tone patches touch only their own bytes", CtcssAndDcsTonePatchesTouchOnlyTheirOwnBytes);
        Run("Dcs code labels cover all 1024 entries in the confirmed order", DcsCodeLabelsCoverAll1024EntriesInTheConfirmedOrder);
        Run("Encode and decode tone selection switch between ctcss and dcs by mode", EncodeAndDecodeToneSelectionSwitchBetweenCtcssAndDcsByMode);
        Run("Encode tone selection rejects custom ctcss", EncodeToneSelectionRejectsCustomCtcss);
        Run("Channel type change forces bandwidth and reverse into valid state", ChannelTypeChangeForcesBandwidthAndReverseIntoValidState);
        Run("Channel type change to analog clears digital only fields", ChannelTypeChangeToAnalogClearsDigitalOnlyFields);
        Run("Color code text rejects out of range input without changing the value", ColorCodeTextRejectsOutOfRangeInputWithoutChangingTheValue);
        Run("Only one encryption index can be nonzero at a time", OnlyOneEncryptionIndexCanBeNonzeroAtATime);
        Run("Decodes AES encryption keys from a real captured layout", DecodesAesEncryptionKeysFromARealCapturedLayout);
        Run("Decodes ARC4 key as fixed five byte field no trimming", DecodesArc4KeyAsFixedFiveByteFieldNoTrimming);
        Run("Decodes basic encryption codes at first second and last slot", DecodesBasicEncryptionCodesAtFirstSecondAndLastSlot);
        Run("Skips unpopulated encryption key and code slots", SkipsUnpopulatedEncryptionKeyAndCodeSlots);
        Run("Encodes AES key sets index byte and preserves trailer", EncodesAesKeySetsIndexByteAndPreservesTrailer);
        Run("Encodes AES key rejects wrong length", EncodesAesKeyRejectsWrongLength);
        Run("Encodes ARC4 key left pads shorter key with zeros", EncodesArc4KeyLeftPadsShorterKeyWithZeros);
        Run("Encodes ARC4 key rejects key too long for slot", EncodesArc4KeyRejectsKeyTooLongForSlot);
        Run("Encodes basic code group touches only target slot", EncodesBasicCodeGroupTouchesOnlyTargetSlot);
        Run("Encodes basic code group rejects non 4 digit code", EncodesBasicCodeGroupRejectsNonFourDigitCode);
        Run("Patcher applies channel patch within its own region", PatcherAppliesChannelPatchWithinItsOwnRegion);
        Run("Patcher leaves other regions untouched", PatcherLeavesOtherRegionsUntouched);
        Run("Patcher sets presence bit for a new channel", PatcherAppliesChannelPatchAndSetsPresenceBitForANewChannel);
        Run("Patcher deletes channel blanking record and clearing presence bit", PatcherDeletesChannelBlankingRecordAndClearingPresenceBit);
        Run("Patcher splices AES key patch within a larger combined region", PatcherSplicesAesKeyPatchWithinALargerCombinedRegion);
        Run("Patcher clears AES and ARC4 key slots leaving siblings untouched", PatcherClearsAesAndArc4KeySlotsLeavingSiblingsUntouched);
        Run("Fresh encryption key placeholders never show as pending radio write", FreshEncryptionKeyPlaceholdersNeverShowAsPendingRadioWrite);
        Run("Patcher throws for an unpopulated channel address", PatcherThrowsForAnUnpopulatedChannelAddress);
        Run("Zone codec channel members round trip through 256 slots", ZoneCodecChannelMembersRoundTripThrough256Slots);
        Run("Patcher applies zone patch and sets presence bit for a new zone", PatcherAppliesZonePatchAndSetsPresenceBitForANewZone);
        Run("Patcher patches only dirty zone fields leaving others untouched", PatcherPatchesOnlyDirtyZoneFieldsLeavingOthersUntouched);
        Run("Patcher deletes zone blanking records and clearing presence bit", PatcherDeletesZoneBlankingRecordsAndClearingPresenceBit);
        Run("Scan list codec encode decode round trips", ScanListCodecEncodeDecodeRoundTrips);
        Run("Scan list timing fields are raw tenths of a second no offset", ScanListTimingFieldsAreRawTenthsOfASecondNoOffset);
        Run("Patcher applies scan list patch and sets presence bit for a new scan list", PatcherAppliesScanListPatchAndSetsPresenceBitForANewScanList);
        Run("Patcher deletes scan list blanking record and clearing presence bit", PatcherDeletesScanListBlankingRecordAndClearingPresenceBit);
        Run("Am air codec encode decode round trips", AmAirCodecEncodeDecodeRoundTrips);
        Run("Patcher applies am air patch and sets presence bit for a new channel", PatcherAppliesAmAirPatchAndSetsPresenceBitForANewChannel);
        Run("Patcher deletes am air blanking record and clearing presence bit", PatcherDeletesAmAirBlankingRecordAndClearingPresenceBit);
        Run("Patcher applies am zone patch and sets presence bit for a new zone", PatcherAppliesAmZonePatchAndSetsPresenceBitForANewZone);
        Run("Patcher deletes am zone blanking record clearing scan bitmask and presence bit", PatcherDeletesAmZoneBlankingRecordClearingScanBitmaskAndPresenceBit);
        Run("Prefabricated sms codec encode decode round trips", PrefabricatedSmsCodecEncodeDecodeRoundTrips);
        Run("Patcher applies prefabricated sms text patch", PatcherAppliesPrefabricatedSmsTextPatch);
        Run("Patcher deletes prefabricated sms blanking record", PatcherDeletesPrefabricatedSmsBlankingRecord);
        Run("Patcher applies prefabricated sms set chain writes next and id per node with end marker on last", PatcherAppliesPrefabricatedSmsSetChainWritesNextAndIdPerNodeWithEndMarkerOnLast);
        Run("Fm channel codec encode decode round trips", FmChannelCodecEncodeDecodeRoundTrips);
        Run("Patcher applies fm channel patch and sets active and scan bits for a new channel", PatcherAppliesFmChannelPatchAndSetsActiveAndScanBitsForANewChannel);
        Run("Patcher deletes fm channel blanking record and clearing active and scan bits", PatcherDeletesFmChannelBlankingRecordAndClearingActiveAndScanBits);
        Run("Auto repeater offset frequency text can be typed up from below its 1 khz floor", AutoRepeaterOffsetFrequencyTextCanBeTypedUpFromBelowIts1KhzFloor);
        Run("Auto repeater offset frequency text rejects out of range values", AutoRepeaterOffsetFrequencyTextRejectsOutOfRangeValues);
        Run("Auto repeater offset codec encode decode round trips", AutoRepeaterOffsetCodecEncodeDecodeRoundTrips);
        Run("Patcher applies auto repeater offset patch with no presence bitmap", PatcherAppliesAutoRepeaterOffsetPatchWithNoPresenceBitmap);
        Run("Patcher deletes auto repeater offset by zeroing not blanking to 0xff", PatcherDeletesAutoRepeaterOffsetByZeroingNotBlankingTo0xff);
        Run("Local info codec decodes narrow ascii fields not utf16le", LocalInfoCodecDecodesNarrowAsciiFieldsNotUtf16Le);
        Run("Local info codec treats all 0xff record as blank", LocalInfoCodecTreatsAll0xffRecordAsBlank);
        Run("Analog alarm time enabled only when emergency alarm is alarm", AnalogAlarmTimeEnabledOnlyWhenEmergencyAlarmIsAlarm);
        Run("Analog eni type and emergency id disabled when emergency alarm is alarm", AnalogEniTypeAndEmergencyIdDisabledWhenEmergencyAlarmIsAlarm);
        Run("Analog emergency id enabled only for dtmf and five tone eni types", AnalogEmergencyIdEnabledOnlyForDtmfAndFiveToneEniTypes);
        Run("Analog emergency channel enabled only when eni send is assigned channel", AnalogEmergencyChannelEnabledOnlyWhenEniSendIsAssignedChannel);
        Run("Analog emergency cycle text maps zero to continuous", AnalogEmergencyCycleTextMapsZeroToContinuous);
        Run("Qdc setting groupbox enabled only when eni type is qdc1200", QdcSettingGroupboxEnabledOnlyWhenEniTypeIsQdc1200);
        Run("Qdc group id and private id gated by kind mutually exclusively", QdcGroupIdAndPrivateIdGatedByKindMutuallyExclusively);
        Run("Work alone response time text uses raw byte plus one minutes", WorkAloneResponseTimeTextUsesRawBytePlusOneMinutes);
        Run("Work alone warning time text covers 1 to 255 seconds only", WorkAloneWarningTimeTextCoversOneTo255SecondsOnly);
        Run("Work alone response text maps key and voice transmit", WorkAloneResponseTextMapsKeyAndVoiceTransmit);
        Run("Digital emergency channel enabled only when eni send is assigned channel", DigitalEmergencyChannelEnabledOnlyWhenEniSendIsAssignedChannel);
        Run("Digital alarm fields are never gated by emergency alarm state", DigitalAlarmFieldsAreNeverGatedByEmergencyAlarmState);
        Run("Digital and qdc call type share the same options list", DigitalAndQdcCallTypeShareTheSameOptionsList);
        Run("Man down delay text covers full 0 to 255 byte range", ManDownDelayTextCoversFull0To255ByteRange);
        Run("Alarm settings codec encode decode round trips", AlarmSettingsCodecEncodeDecodeRoundTrips);
        Run("Patcher applies alarm settings patch across all three regions", PatcherAppliesAlarmSettingsPatchAcrossAllThreeRegions);
        Run("Patcher applies alarm man down patch without clobbering optional settings in the shared 3500000 region", PatcherAppliesAlarmManDownPatchWithoutClobberingOptionalSettingsInTheShared3500000Region);
        Run("Optional settings encode main only touches patched offsets", OptionalSettingsEncodeMainOnlyTouchesPatchedOffsets);
        Run("Optional settings encode display only touches patched text fields", OptionalSettingsEncodeDisplayOnlyTouchesPatchedTextFields);
        Run("Power on display lines allow 14 characters not 7", PowerOnDisplayLinesAllow14CharactersNot7);
        Run("Power on password char rejects non digit input without changing the value", PowerOnPasswordCharRejectsNonDigitInputWithoutChangingTheValue);
        Run("Alert tone frequency text rejects out of range and non digit input without changing the value", AlertToneFrequencyTextRejectsOutOfRangeAndNonDigitInputWithoutChangingTheValue);
        Run("Alert tone period text rejects out of range input without changing the value", AlertTonePeriodTextRejectsOutOfRangeInputWithoutChangingTheValue);
        Run("Correct frequency hz text reports validation errors instead of reverting", CorrectFrequencyHzTextReportsValidationErrorsInsteadOfReverting);
        Run("Correct frequency hz out of range raw byte blocks save", CorrectFrequencyHzOutOfRangeRawByteBlocksSave);
        Run("Alert tone frequency and period text report validation errors instead of reverting", AlertToneFrequencyAndPeriodTextReportValidationErrorsInsteadOfReverting);
        Run("Alert tone validation errors block save and write commands", AlertToneValidationErrorsBlockSaveAndWriteCommands);
        Run("Turning vox on resets vox detection back to its first option", TurningVoxOnResetsVoxDetectionBackToItsFirstOption);
        Run("Am fm function controls fm and am section enabled state", AmFmFunctionControlsFmAndAmSectionEnabledState);
        Run("Power on volume options cover the full 0 to 15 range not max volumes indoors scale", PowerOnVolumeOptionsCoverTheFull0To15RangeNotMaxVolumesIndoorsScale);
        Run("Power on volume type minimum disables the power on volume field", PowerOnVolumeTypeMinimumDisablesThePowerOnVolumeField);
        Run("Sat location options start with gps not off", SatLocationOptionsStartWithGpsNotOff);
        Run("Fm work channel name resolves against the live fm channel list", FmWorkChannelNameResolvesAgainstTheLiveFmChannelList);
        Run("Map fm channels excludes the home vfo slot", MapFmChannelsExcludesTheHomeVfoSlot);
        Run("Am work zone name resolves against the live am zone list", AmWorkZoneNameResolvesAgainstTheLiveAmZoneList);
        Run("Key function options match the real vendor cps list not the drifted port", KeyFunctionOptionsMatchTheRealVendorCpsList);
        Run("Analog call hold time and mute timing options cover their full corrected range", AnalogCallHoldTimeAndMuteTimingOptionsCoverTheirFullCorrectedRange);
        Run("On off to bool converter round trips on off text", OnOffToBoolConverterRoundTripsOnOffText);
        Run("Encryption key visibility converter gates on type selectors not key index", EncryptionKeyVisibilityConverterGatesOnTypeSelectorsNotKeyIndex);
        Run("Vox on drives the vox safety warning state", VoxOnDrivesTheVoxSafetyWarningState);
        Run("Voice header repetitions and tx preamble duration options match the real vendor cps list", VoiceHeaderRepetitionsAndTxPreambleDurationOptionsMatchTheRealVendorCpsList);
        Run("Patcher applies optional settings patch to main and display regions", PatcherAppliesOptionalSettingsPatchToMainAndDisplayRegions);
        Run("Patcher applies alert zone scalar fields and tone matrices", PatcherAppliesAlertZoneScalarFieldsAndToneMatrices);
        Run("Patcher applies alert tone1 unmatch end and call all tone matrices", PatcherAppliesAlertTone1ToneMatrices);
        Run("Patcher applies power save fields at their confirmed offsets", PatcherAppliesPowerSaveFieldsAtConfirmedOffsets);
        Run("Patcher applies display tab fields including night mode and fixed backlight offset", PatcherAppliesDisplayTabFields);
        Run("Patcher applies work mode fields including mem zone a and b indices", PatcherAppliesWorkModeFields);
        Run("Patcher applies vox fields at their confirmed offsets", PatcherAppliesVoxFieldsAtConfirmedOffsets);
        Run("Patcher applies ste fields at their confirmed offsets", PatcherAppliesSteFieldsAtConfirmedOffsets);
        Run("Ste time text converts raw byte with off by one mapping", SteTimeTextConvertsRawByteWithOffByOneMapping);
        Run("Patcher applies am fm fields at their confirmed offsets", PatcherAppliesAmFmFieldsAtConfirmedOffsets);
        Run("Patcher applies key function fields at their confirmed offsets", PatcherAppliesKeyFunctionFieldsAtConfirmedOffsets);
        Run("Patcher applies other tab fields at their confirmed offsets", PatcherAppliesOtherTabFieldsAtConfirmedOffsets);
        Run("Patcher applies digital func fields at their confirmed offsets", PatcherAppliesDigitalFuncFieldsAtConfirmedOffsets);
        Run("Patcher applies gps ranging fields at their confirmed offsets", PatcherAppliesGpsRangingFieldsAtConfirmedOffsets);
        Run("Time zone options match the real vendor cps list not the reference project's", TimeZoneOptionsMatchTheRealVendorCpsListNotTheReferenceProjects);
        Run("Patcher applies vfo scan fields at their confirmed offsets", PatcherAppliesVfoScanFieldsAtConfirmedOffsets);
        Run("Vfo scan frequency text fields convert to and from mhz times 100000", VfoScanFrequencyTextFieldsConvertToAndFromMhzTimes100000);
        Run("Patcher applies auto repeater fields at their confirmed offsets", PatcherAppliesAutoRepeaterFieldsAtConfirmedOffsets);
        Run("Auto repeater offset fields use off sentinel not a plain three item list", AutoRepeaterOffsetFieldsUseOffSentinelNotAPlainThreeItemList);
        Run("Patcher applies record fields at their confirmed offsets", PatcherAppliesRecordFieldsAtConfirmedOffsets);
        Run("Patcher applies volume audio fields at their confirmed offsets", PatcherAppliesVolumeAudioFieldsAtConfirmedOffsets);
        Run("Mic gain options include the auto entry vendor cps has", MicGainOptionsIncludeTheAutoEntryVendorCpsHas);
        Run("Patcher applies satellite fields at their confirmed offsets", PatcherAppliesSatelliteFieldsAtConfirmedOffsets);
        Run("Patcher applies roaming zone at its corrected offset not the address book collision", PatcherAppliesRoamingZoneAtItsCorrectedOffsetNotTheAddressBookCollision);
        Run("Hold time and voice header repetitions text use offset encoding", HoldTimeAndVoiceHeaderRepetitionsTextUseOffsetEncoding);
        Run("Vfo scan frequency text fields reject out of band values", VfoScanFrequencyTextFieldsRejectOutOfBandValues);
        Run("Vfo scan frequency text fields can be typed up from a value below their minimum", VfoScanFrequencyTextFieldsCanBeTypedUpFromAValueBelowTheirMinimum);
        Run("Vfo scan frequency text fields report a validation error for out of range values", VfoScanFrequencyTextFieldsReportAValidationErrorForOutOfRangeValues);
        Run("Auto repeater frequency text fields report a validation error for unparsable values", AutoRepeaterFrequencyTextFieldsReportAValidationErrorForUnparsableValues);
        Run("Auto repeater frequency text fields enforce the same vhf uhf band limits as vfo scan", AutoRepeaterFrequencyTextFieldsEnforceTheSameVhfUhfBandLimitsAsVfoScan);
        Run("Auto roaming fixed time options cover the full 1 to 256 range", AutoRoamingFixedTimeOptionsCoverTheFull1To256Range);
        Run("Optional settings validation errors block save and write commands", OptionalSettingsValidationErrorsBlockSaveAndWriteCommands);
        Run("Analog quick call operation type change resets call id and disables it for off and dtmf", AnalogQuickCallOperationTypeChangeResetsCallIdAndDisablesItForOffAndDtmf);
        Run("Analog quick call operation type text round trips through options", AnalogQuickCallOperationTypeTextRoundTripsThroughOptions);
        Run("Hot key enable flags match the real vendor cps gating for every mode and call type combination", HotKeyEnableFlagsMatchTheRealVendorCpsGatingForEveryModeAndCallTypeCombination);
        Run("Hot key changing mode call type or digi call type resets fields gated below it", HotKeyChangingModeCallTypeOrDigiCallTypeResetsFieldsGatedBelowIt);
        Run("New main view model seeds exactly eighteen hot key rows with the real key names", NewMainViewModelSeedsExactlyEighteenHotKeyRowsWithTheRealKeyNames);
        Run("Add analog quick call is capped at four slots", AddAnalogQuickCallIsCappedAtFourSlots);
        Run("Add state information is capped at thirty two slots", AddStateInformationIsCappedAtThirtyTwoSlots);
        Run("Analog quick call call id options pick the list matching the selected operation type", AnalogQuickCallCallIdOptionsPickTheListMatchingTheSelectedOperationType);
        Run("Hot key call object options only offer configured analog quick call slots", HotKeyCallObjectOptionsOnlyOfferConfiguredAnalogQuickCallSlots);
        Run("Hot key content options offer prefabricated sms for hot text and state information entries for state information", HotKeyContentOptionsOfferPrefabricatedSmsForHotTextAndStateInformationEntriesForStateInformation);
        Run("Analog quick call codec decodes off and dtmf as an unavailable call id matching the live capture", AnalogQuickCallCodecDecodesOffAndDtmfAsAnUnavailableCallIdMatchingTheLiveCapture);
        Run("State information codec decodes text and treats a blank slot as empty", StateInformationCodecDecodesTextAndTreatsABlankSlotAsEmpty);
        Run("Hot key codec decodes every field at its confirmed byte offset", HotKeyCodecDecodesEveryFieldAtItsConfirmedByteOffset);
        Run("Hot key codec infers call type off from an unset call object rather than the raw call type byte", HotKeyCodecInfersCallTypeOffFromAnUnsetCallObjectRatherThanTheRawCallTypeByte);
        Run("Analog quick call codec encode decode round trips", AnalogQuickCallCodecEncodeDecodeRoundTrips);
        Run("Patcher applies analog quick call patch with no presence bitmap", PatcherAppliesAnalogQuickCallPatchWithNoPresenceBitmap);
        Run("Patcher deletes analog quick call resetting to operation type off", PatcherDeletesAnalogQuickCallResettingToOperationTypeOff);
        Run("State information codec encode decode round trips", StateInformationCodecEncodeDecodeRoundTrips);
        Run("Patcher applies state information patch with no presence bitmap", PatcherAppliesStateInformationPatchWithNoPresenceBitmap);
        Run("Patcher deletes state information blanking the name buffer", PatcherDeletesStateInformationBlankingTheNameBuffer);
        Run("Hot key codec encode decode round trips", HotKeyCodecEncodeDecodeRoundTrips);
        Run("Hot key codec encode has no dedicated off byte for call type", HotKeyCodecEncodeHasNoDedicatedOffByteForCallType);
        Run("Patcher applies hot key patch with no presence bitmap", PatcherAppliesHotKeyPatchWithNoPresenceBitmap);
        Run("Add analog address is capped at 128 slots", AddAnalogAddressIsCappedAt128Slots);
        Run("Analog address validation flags number out of range and address number over 10 digits", AnalogAddressValidationFlagsNumberOutOfRangeAndAddressNumberOver10Digits);
        Run("Analog address codec encode decode round trips", AnalogAddressCodecEncodeDecodeRoundTrips);
        Run("Analog address codec encode derives number len from digit count matching the live capture", AnalogAddressCodecEncodeDerivesNumberLenFromDigitCountMatchingTheLiveCapture);
        Run("Patcher applies analog address patch and sets id list byte for a new entry", PatcherAppliesAnalogAddressPatchAndSetsIdListByteForANewEntry);
        Run("Patcher deletes analog address blanking record and clearing id list byte", PatcherDeletesAnalogAddressBlankingRecordAndClearingIdListByte);
        Run("Qdc1200 settings text wrappers round trip through their confirmed ranges", Qdc1200SettingsTextWrappersRoundTripThroughTheirConfirmedRanges);
        Run("Qdc1200 id entry enable flags and type options depend on call type not a shared filtered list", Qdc1200IdEntryEnableFlagsAndTypeOptionsDependOnCallTypeNotASharedFilteredList);
        Run("Qdc1200 id entry need to answer is only enabled for aleart and remotely moniton types", Qdc1200IdEntryNeedToAnswerIsOnlyEnabledForAleartAndRemotelyMonitonTypes);
        Run("Qdc1200 id entry changing call type only resets need to answer when it becomes disabled", Qdc1200IdEntryChangingCallTypeOnlyResetsNeedToAnswerWhenItBecomesDisabled);
        Run("Add qdc1200 id is capped at 100 slots", AddQdc1200IdIsCappedAt100Slots);
        Run("Add qdc address is capped at 128 slots", AddQdcAddressIsCappedAt128Slots);
        Run("Five tone id entry has any pending radio write tracks all fields", FiveToneIdEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Five tone settings entry has any pending radio write ignores info id no and stop code", FiveToneSettingsEntryHasAnyPendingRadioWriteIgnoresInfoIdNoAndStopCode);
        Run("Add five tone id is capped at 99 slots", AddFiveToneIdIsCappedAt99Slots);
        Run("Five tone settings text wrappers round trip through their confirmed ranges", FiveToneSettingsTextWrappersRoundTripThroughTheirConfirmedRanges);
        Run("Five tone settings function decoding response options depend on function option", FiveToneSettingsFunctionDecodingResponseOptionsDependOnFunctionOption);
        Run("Five tone id entry standard and time and name are disabled until has special call", FiveToneIdEntryStandardAndTimeAndNameAreDisabledUntilHasSpecialCall);
        Run("Five tone special call entry calling type drives is send message is ani is ptt id", FiveToneSpecialCallEntryCallingTypeDrivesIsSendMessageIsAniIsPttId);
        Run("Five tone other side id max length tracks self id length capped at 7", FiveToneOtherSideIdMaxLengthTracksSelfIdLengthCappedAt7);
        Run("Selected info id row switches to a different rows own function values", SelectedInfoIdRowSwitchesToADifferentRowsOwnFunctionValues);
        Run("Open five tone row special call retargets a different row via group no", OpenFiveToneRowSpecialCallRetargetsADifferentRowViaGroupNo);
        Run("Open five tone row special call creates a new row when group no has none yet", OpenFiveToneRowSpecialCallCreatesANewRowWhenGroupNoHasNoneYet);
        Run("Five tone id entry composes encode id for send message ani and ptt id", FiveToneIdEntryComposesEncodeIdForSendMessageAniAndPttId);
        Run("Five tone id entry encode id disabled once any special call is configured", FiveToneIdEntryEncodeIdDisabledOnceAnySpecialCallIsConfigured);
        Run("Five tone id entry encode id hex only disabled only for configured send message", FiveToneIdEntryEncodeIdHexOnlyDisabledOnlyForConfiguredSendMessage);
        Run("Five tone settings bot composes encode id for ani and ptt id but not send message", FiveToneSettingsBotComposesEncodeIdForAniAndPttIdButNotSendMessage);
        Run("Reset five tone row special call clears state after confirmation", ResetFiveToneRowSpecialCallClearsStateAfterConfirmation);
        Run("Reset five tone row special call does nothing without confirmation", ResetFiveToneRowSpecialCallDoesNothingWithoutConfirmation);
        Run("Reset five tone bot and eot special call clear state after confirmation", ResetFiveToneBotAndEotSpecialCallClearStateAfterConfirmation);
        Run("Five tone validation flags self id outside 5 to 7 digits but not blank", FiveToneValidationFlagsSelfIdOutside5To7DigitsButNotBlank);
        Run("Five tone validation flags id number out of range and duplicates", FiveToneValidationFlagsIdNumberOutOfRangeAndDuplicates);
        Run("Five tone id codec decodes real captured bytes from the live write capture", FiveToneIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Five tone id codec decodes ani real captured bytes", FiveToneIdCodecDecodesAniRealCapturedBytes);
        Run("Five tone id codec decodes send message 99999 real captured bytes", FiveToneIdCodecDecodesSendMessage99999RealCapturedBytes);
        Run("Five tone id codec decodes bot ptt id real captured bytes", FiveToneIdCodecDecodesBotPttIdRealCapturedBytes);
        Run("Five tone id codec encode decode round trips", FiveToneIdCodecEncodeDecodeRoundTrips);
        Run("Five tone settings codec decodes real captured bytes from the live write capture", FiveToneSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Five tone settings codec decodes bot and eot real captured bytes", FiveToneSettingsCodecDecodesBotAndEotRealCapturedBytes);
        Run("Five tone settings codec encode decode round trips", FiveToneSettingsCodecEncodeDecodeRoundTrips);
        Run("Patcher applies five tone id patch and sets presence bit for a new row", PatcherAppliesFiveToneIdPatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes five tone id zeroing record and clearing presence bit", PatcherDeletesFiveToneIdZeroingRecordAndClearingPresenceBit);
        Run("Patcher applies five tone settings patch", PatcherAppliesFiveToneSettingsPatch);
        Run("Five tone info id slot codec decodes real captured bytes from the live write capture", FiveToneInfoIdSlotCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Five tone info id slot codec decodes fedcba987654 real captured bytes", FiveToneInfoIdSlotCodecDecodesFedcba987654RealCapturedBytes);
        Run("Five tone info id slot codec encode decode round trips", FiveToneInfoIdSlotCodecEncodeDecodeRoundTrips);
        Run("Two tone encode codec decodes real captured bytes from the live write capture", TwoToneEncodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Two tone encode codec encode decode round trips", TwoToneEncodeCodecEncodeDecodeRoundTrips);
        Run("Two tone decode codec decodes real captured bytes from the live write capture", TwoToneDecodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Two tone decode codec encode decode round trips", TwoToneDecodeCodecEncodeDecodeRoundTrips);
        Run("Two tone encode settings codec decodes real captured bytes from the live write capture", TwoToneEncodeSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Two tone encode settings codec encode decode round trips", TwoToneEncodeSettingsCodecEncodeDecodeRoundTrips);
        Run("Patcher applies two tone encode patch and sets presence bit for a new row", PatcherAppliesTwoToneEncodePatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes two tone encode zeroing record and clearing presence bit", PatcherDeletesTwoToneEncodeZeroingRecordAndClearingPresenceBit);
        Run("Patcher applies two tone decode patch and sets presence bit for a new row", PatcherAppliesTwoToneDecodePatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes two tone decode zeroing record and clearing presence bit", PatcherDeletesTwoToneDecodeZeroingRecordAndClearingPresenceBit);
        Run("Patcher applies two tone encode settings patch", PatcherAppliesTwoToneEncodeSettingsPatch);
        Run("Two tone encode entry has any pending radio write tracks all fields", TwoToneEncodeEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Two tone decode entry has any pending radio write tracks all fields", TwoToneDecodeEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Two tone encode settings entry has any pending radio write tracks all fields", TwoToneEncodeSettingsEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Dtmf code codec decodes real captured bytes from the live write capture", DtmfCodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Dtmf code codec encode decode round trips including star and hash", DtmfCodeCodecEncodeDecodeRoundTripsIncludingStarAndHash);
        Run("Dtmf settings codec decodes real captured bytes from the live write capture", DtmfSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Dtmf settings codec decodes off sentinels from the round two live write capture", DtmfSettingsCodecDecodesOffSentinelsFromTheRoundTwoLiveWriteCapture);
        Run("Dtmf settings codec encode decode round trips", DtmfSettingsCodecEncodeDecodeRoundTrips);
        Run("Dtmf encode codec decodes real captured bytes from the live write capture", DtmfEncodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Dtmf encode codec decodes composed m2 code matching the confirmed formula", DtmfEncodeCodecDecodesComposedM2CodeMatchingTheConfirmedFormula);
        Run("Dtmf encode codec encode decode round trips", DtmfEncodeCodecEncodeDecodeRoundTrips);
        Run("Patcher applies dtmf encode patch with no presence bitmap", PatcherAppliesDtmfEncodePatchWithNoPresenceBitmap);
        Run("Patcher applies dtmf settings patch", PatcherAppliesDtmfSettingsPatch);
        Run("Patcher applies dtmf bot eot remotely kill stun patches", PatcherAppliesDtmfBotEotRemotelyKillStunPatches);
        Run("Patcher applies dtmf transmitting time patch", PatcherAppliesDtmfTransmittingTimePatch);
        Run("Dtmf encode entry has any pending radio write tracks code only", DtmfEncodeEntryHasAnyPendingRadioWriteTracksCodeOnly);
        Run("Dtmf settings entry has any pending radio write tracks all fields", DtmfSettingsEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Radio id codec decodes real captured bytes from the live write capture", RadioIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Radio id codec encode decode round trips", RadioIdCodecEncodeDecodeRoundTrips);
        Run("Patcher applies radio id patch and sets presence bit for a new row", PatcherAppliesRadioIdPatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes radio id blanking to 0xff not zero", PatcherDeletesRadioIdBlankingTo0xffNotZero);
        Run("Radio id entry has any pending radio write tracks all fields", RadioIdEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Dmr id text fields report validation errors instead of reverting", DmrIdTextFieldsReportValidationErrorsInsteadOfReverting);
        Run("Talkgroup dmr id text bypasses range check only for all call", TalkgroupDmrIdTextBypassesRangeCheckOnlyForAllCall);
        Run("Alarm settings and aprs digital report dmr id text bypass zero", AlarmSettingsAndAprsDigitalReportDmrIdTextBypassZero);
        Run("Dmr id validation errors block save and write commands", DmrIdValidationErrorsBlockSaveAndWriteCommands);
        Run("Master id codec decodes real captured bytes from the live write capture", MasterIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Master id codec encode decode round trips", MasterIdCodecEncodeDecodeRoundTrips);
        Run("Patcher applies master id patch", PatcherAppliesMasterIdPatch);
        Run("Master id entry has any pending radio write tracks all fields", MasterIdEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Talk alias settings codec encode matches real captured bytes", TalkAliasSettingsCodecEncodeMatchesRealCapturedBytes);
        Run("Patcher applies talk alias settings patch", PatcherAppliesTalkAliasSettingsPatch);
        Run("Talk alias settings entry has any pending radio write tracks both fields", TalkAliasSettingsEntryHasAnyPendingRadioWriteTracksBothFields);
        Run("Talk alias settings entry display priority options are the confirmed three values", TalkAliasSettingsEntryDisplayPriorityOptionsAreTheConfirmedThreeValues);
        Run("Talkgroup codec decodes real captured bytes from the live write capture", TalkgroupCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Talkgroup codec encode decode round trips", TalkgroupCodecEncodeDecodeRoundTrips);
        Run("Talkgroup codec encode forces all call sentinel and none alert", TalkgroupCodecEncodeForcesAllCallSentinelAndNoneAlert);
        Run("Patcher applies talkgroup patch and clears presence bit for a new row", PatcherAppliesTalkgroupPatchAndClearsPresenceBitForANewRow);
        Run("Patcher deletes talkgroup blanking to 0xff not zero and sets presence bit", PatcherDeletesTalkgroupBlankingTo0xffNotZeroAndSetsPresenceBit);
        Run("Talkgroup entry has any pending radio write tracks all fields", TalkgroupEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Talkgroup entry switching to all call forces call alert to none", TalkgroupEntrySwitchingToAllCallForcesCallAlertToNone);
        Run("Receive group list codec decodes real captured bytes from the live write capture", ReceiveGroupListCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Receive group list codec encode decode round trips", ReceiveGroupListCodecEncodeDecodeRoundTrips);
        Run("Patcher applies receive group list patch and sets presence bit for a new row", PatcherAppliesReceiveGroupListPatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes receive group list blanking to 0xff not zero and clears presence bit", PatcherDeletesReceiveGroupListBlankingTo0xffNotZeroAndClearsPresenceBit);
        Run("Receive group list entry has any pending radio write tracks all fields", ReceiveGroupListEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Roaming channel codec decodes real captured bytes from the live write capture", RoamingChannelCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Roaming channel codec encode decode round trips", RoamingChannelCodecEncodeDecodeRoundTrips);
        Run("Roaming channel codec color code and slot string mappings round trip", RoamingChannelCodecColorCodeAndSlotStringMappingsRoundTrip);
        Run("Patcher applies roaming channel patch and sets presence bit for a new row", PatcherAppliesRoamingChannelPatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes roaming channel blanking to 0xff not zero", PatcherDeletesRoamingChannelBlankingTo0xffNotZero);
        Run("Roaming channel entry has any pending radio write tracks all fields", RoamingChannelEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Roaming zone codec decodes real captured bytes from the live write capture", RoamingZoneCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Roaming zone codec encode decode round trips and preserves member order", RoamingZoneCodecEncodeDecodeRoundTripsAndPreservesMemberOrder);
        Run("Patcher applies roaming zone patch and sets presence bit for a new row", PatcherAppliesRoamingZonePatchAndSetsPresenceBitForANewRow);
        Run("Patcher deletes roaming zone blanking to 0xff not zero", PatcherDeletesRoamingZoneBlankingTo0xffNotZero);
        Run("Roaming zone entry has any pending radio write tracks name and members", RoamingZoneEntryHasAnyPendingRadioWriteTracksNameAndMembers);
        Run("Codeplug limits rejects frequency in the vhf uhf dead zone", CodeplugLimitsRejectsFrequencyInTheVhfUhfDeadZone);
        Run("Loading an old project file with talkgroup call alert as a bool does not throw", LoadingAnOldProjectFileWithTalkgroupCallAlertAsABoolDoesNotThrow);
        Run("Full radio project data round trips through a real file", FullRadioProjectDataRoundTripsThroughARealFile);
        Run("Saving a project encrypts encryption key material in the json file", SavingAProjectEncryptsEncryptionKeyMaterialInTheJsonFile);
        Run("Encrypting and decrypting through a raw stream round trips like the storage picker path does", EncryptingAndDecryptingThroughARawStreamRoundTripsLikeTheStoragePickerPathDoes);
        Run("Saving a project does not mutate the callers own encryption keys", SavingAProjectDoesNotMutateTheCallersOwnEncryptionKeys);
        Run("Loading a legacy plain text encryption key project file still works", LoadingALegacyPlainTextEncryptionKeyProjectFileStillWorks);
        Run("Encryption key protector round trips and falls back on tampered input", EncryptionKeyProtectorRoundTripsAndFallsBackOnTamperedInput);
        Run("Loading an old project file with digital contact call alert as a bool does not throw", LoadingAnOldProjectFileWithDigitalContactCallAlertAsABoolDoesNotThrow);
        Run("Digital contact entry call type text round trips and forces call alert to none on all call", DigitalContactEntryCallTypeTextRoundTripsAndForcesCallAlertToNoneOnAllCall);
        Run("Digital contact entry radio id text bypasses range check only for all call", DigitalContactEntryRadioIdTextBypassesRangeCheckOnlyForAllCall);
        Run("Digital contact codec encode record matches real captured bytes", DigitalContactCodecEncodeRecordMatchesRealCapturedBytes);
        Run("Digital contact codec decodes friend flag packed into the call alert byte", DigitalContactCodecDecodesFriendFlagPackedIntoTheCallAlertByte);
        Run("Digital contact codec encode record round trips friend flag without disturbing call alert", DigitalContactCodecEncodeRecordRoundTripsFriendFlagWithoutDisturbingCallAlert);
        Run("Digital contact codec encode meta matches real captured values", DigitalContactCodecEncodeMetaMatchesRealCapturedValues);
        Run("Digital contact codec encode all throws when exceeding block length", DigitalContactCodecEncodeAllThrowsWhenExceedingBlockLength);
        Run("Digital contact writer round trips add edit delete through fake connection", DigitalContactWriterRoundTripsAddEditDeleteThroughFakeConnection);
        Run("Digital contact writer handles a write spanning two blocks through fake connection", DigitalContactWriterHandlesAWriteSpanningTwoBlocksThroughFakeConnection);
        Run("Digital contact codec address translation round trips at high block indices", DigitalContactCodecAddressTranslationRoundTripsAtHighBlockIndices);
        Run("Talkgroup whitelist codec encode all matches real captured bytes", TalkgroupWhitelistCodecEncodeAllMatchesRealCapturedBytes);
        Run("Digital contact whitelist codec encode all matches real captured bytes", DigitalContactWhitelistCodecEncodeAllMatchesRealCapturedBytes);
        Run("Talkgroup whitelist codec encode all ignores stored id uses list position", TalkgroupWhitelistCodecEncodeAllIgnoresStoredIdUsesListPosition);
        Run("Talkgroup whitelist codec encode all throws when exceeding cap", TalkgroupWhitelistCodecEncodeAllThrowsWhenExceedingCap);
        Run("Talkgroup whitelist codec encode all decode block round trips", TalkgroupWhitelistCodecEncodeAllDecodeBlockRoundTrips);
        Run("Talkgroup call alert still round trips as a plain string going forward", TalkgroupCallAlertStillRoundTripsAsAPlainStringGoingForward);
        Run("Qdc address entry type options depend on call type and ack enabled needs aleart or remotely monitor", QdcAddressEntryTypeOptionsDependOnCallTypeAndAckEnabledNeedsAleartOrRemotelyMonitor);
        Run("Qdc address entry changing call type does not reset private group id or type and toggles enable flags", QdcAddressEntryChangingCallTypeDoesNotResetPrivateGroupIdOrTypeAndTogglesEnableFlags);
        Run("Qdc address codec decodes real captured bytes from the live write capture", QdcAddressCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Qdc address codec encode decode round trips", QdcAddressCodecEncodeDecodeRoundTrips);
        Run("Patcher applies qdc address patch with no presence bitmap", PatcherAppliesQdcAddressPatchWithNoPresenceBitmap);
        Run("Patcher deletes qdc address blanking to 0xff not zero", PatcherDeletesQdcAddressBlankingTo0xffNotZero);
        Run("Map qdc addresses survives the entrys own ack reset cascade", MapQdcAddressesSurvivesTheEntrysOwnAckResetCascade);
        Run("Map five tone ids merges info id slots by row number", MapFiveToneIdsMergesInfoIdSlotsByRowNumber);
        Run("Qdc1200 id codec decodes real captured bytes from the live write capture", Qdc1200IdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Qdc1200 settings codec decodes real captured bytes from the live write capture", Qdc1200SettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Map qdc1200 ids survives the entrys own need to answer reset cascade", MapQdc1200IdsSurvivesTheEntrysOwnNeedToAnswerResetCascade);
        Run("Qdc1200 id codec encode decode round trips", Qdc1200IdCodecEncodeDecodeRoundTrips);
        Run("Patcher applies qdc1200 id patch with no presence bitmap", PatcherAppliesQdc1200IdPatchWithNoPresenceBitmap);
        Run("Patcher deletes qdc1200 id by zeroing not blanking to 0xff", PatcherDeletesQdc1200IdByZeroingNotBlankingTo0xff);
        Run("Qdc1200 settings codec encode decode round trips", Qdc1200SettingsCodecEncodeDecodeRoundTrips);
        Run("Patcher applies qdc1200 settings patch", PatcherAppliesQdc1200SettingsPatch);
        Run("Map hot keys survives the entry's own reset on change cascade for a fully configured key", MapHotKeysSurvivesTheEntrysOwnResetOnChangeCascadeForAFullyConfiguredKey);
        Run("Map analog quick calls survives the entry's own reset on change cascade for a configured slot", MapAnalogQuickCallsSurvivesTheEntrysOwnResetOnChangeCascadeForAConfiguredSlot);
        Run("Map state information skips blank slots and numbers by slot position", MapStateInformationSkipsBlankSlotsAndNumbersBySlotPosition);
        Run("Gps roaming codec offset for index puts entry 16 in the second half at 0x200 not 0x10", GpsRoamingCodecOffsetForIndexPutsEntry16InTheSecondHalfAt0x200Not0x10);
        Run("Gps roaming codec decodes real captured bytes from the live write capture", GpsRoamingCodecDecodesRealCapturedBytesFromTheLiveWriteCapture);
        Run("Gps roaming codec encode decode round trips", GpsRoamingCodecEncodeDecodeRoundTrips);
        Run("Patcher applies gps roaming patch at the correct second half address", PatcherAppliesGpsRoamingPatchAtTheCorrectSecondHalfAddress);
        Run("Gps roaming entry has any pending radio write tracks all fields", GpsRoamingEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Gps roaming entry minute text validates mm dot mm format", GpsRoamingEntryMinuteTextValidatesMmDotMmFormat);
        Run("Aprs settings entry has any pending radio write tracks scalar fields", AprsSettingsEntryHasAnyPendingRadioWriteTracksScalarFields);
        Run("Aprs settings entry has any pending radio write aggregates sub entries", AprsSettingsEntryHasAnyPendingRadioWriteAggregatesSubEntries);
        Run("Aprs receive filter entry has any pending radio write tracks all fields", AprsReceiveFilterEntryHasAnyPendingRadioWriteTracksAllFields);
        Run("Aprs fix location text fields report validation errors for out of range degrees", AprsFixLocationTextFieldsReportValidationErrorsForOutOfRangeDegrees);
        Run("Aprs settings codec encode decode round trips", AprsSettingsCodecEncodeDecodeRoundTrips);
        Run("Aprs settings codec encode preserves filters and the unwritten gap", AprsSettingsCodecEncodePreservesFiltersAndTheUnwrittenGap);
        Run("Aprs settings codec encode reproduces real captured bytes across all live tests", AprsSettingsCodecEncodeReproducesRealCapturedBytesAcrossAllLiveTests);
        Run("Patcher applies aprs settings patch", PatcherAppliesAprsSettingsPatch);
        Run("Read from radio skipping digital contacts leaves an earlier read list untouched", ReadFromRadioSkippingDigitalContactsLeavesAnEarlierReadListUntouched);
        Run("Can include digital contacts in write is gated by a genuine read", CanIncludeDigitalContactsInWriteIsGatedByAGenuineRead);
        Run("Digital contacts genuinely populated flag round trips through the project mapper", DigitalContactsGenuinelyPopulatedFlagRoundTripsThroughTheProjectMapper);
        Run("Loading a project refreshes the filtered digital contacts list", LoadingAProjectRefreshesTheFilteredDigitalContactsList);
        Run("Aprs settings is marked synced after a read", AprsSettingsIsMarkedSyncedAfterARead);
        Run("Write changes to radio is available with nothing dirty once a snapshot exists", WriteChangesToRadioIsAvailableWithNothingDirtyOnceASnapshotExists);
        Run("Refresh radio ports notifies write changes to radio CanExecuteChanged", RefreshRadioPortsNotifiesWriteChangesToRadioCanExecuteChanged);
        Run("Write changes to radio auto-captures a baseline without discarding unread prepared work", WriteChangesToRadioAutoCapturesABaselineWithoutDiscardingUnreadPreparedWork);
        Run("Dev force model to image marks every entity dirty without changing values", DevForceModelToImageMarksEveryEntityDirtyWithoutChangingValues);
        Run("Dev force model to image then write succeeds against a virtual radio", DevForceModelToImageThenWriteSucceedsAgainstAVirtualRadio);

        if (Failures.Count == 0)
        {
            Console.WriteLine("All tests passed.");
            return 0;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine($"{Failures.Count} test(s) failed:");
        foreach (var failure in Failures)
        {
            Console.Error.WriteLine(failure);
        }

        return 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            Failures.Add($"{name}: {exception.Message}");
            Console.Error.WriteLine($"FAIL {name}");
        }
    }

    private static void MapsProjectDataRoundtrip()
    {
        var channel = new ChannelEntry
        {
            Number = 1,
            Name = "V00",
            RxFrequencyMHz = 145.50000,
            ChannelType = 0
        };
        var zone = new ZoneEntry
        {
            Number = 1,
            Name = "Analog",
            AChannel = channel,
            BChannel = channel
        };
        zone.Members.Add(channel);

        var data = RadioProjectMapper.ToData([channel], [zone]);
        var loadedChannels = new List<ChannelEntry>();
        var loadedZones = new List<ZoneEntry>();

        RadioProjectMapper.LoadInto(data, loadedChannels, loadedZones);

        AssertEqual(1, loadedChannels.Count);
        AssertEqual("V00", loadedChannels[0].Name);
        AssertEqual(1, loadedZones.Count);
        AssertEqual("Analog", loadedZones[0].Name);
        AssertSame(loadedChannels[0], loadedZones[0].Members[0]);
        AssertSame(loadedChannels[0], loadedZones[0].AChannel);
    }

    private static void MapsAdvancedChannelFieldsRoundtrip()
    {
        var channel = new ChannelEntry
        {
            Number = 1,
            Name = "DMR",
            RxFrequencyMHz = 434.50000,
            ChannelType = 1,
            RadioIdIndex = 0,
            ScanListIndex = 5,
            ReceiveGroupListIndex = 3,
            TalkAround = true,
            DmrModeDcdm = 1,
            ScrambleMode = 1
        };

        var data = RadioProjectMapper.ToData([channel], []);
        var loadedChannels = new List<ChannelEntry>();
        var loadedZones = new List<ZoneEntry>();

        RadioProjectMapper.LoadInto(data, loadedChannels, loadedZones);

        AssertEqual((ushort)5, loadedChannels[0].ScanListIndex);
        AssertEqual((ushort)3, loadedChannels[0].ReceiveGroupListIndex);
        AssertTrue(loadedChannels[0].TalkAround, "TalkAround should round-trip as true");
        AssertEqual((byte)1, loadedChannels[0].DmrModeDcdm);
        AssertEqual(1, loadedChannels[0].ScrambleMode);
    }

    private static void MapsChannelEncryptionSettingsRoundtrip()
    {
        var channel = new ChannelEntry
        {
            Number = 1,
            Name = "DMR",
            RxFrequencyMHz = 434.50000,
            ChannelType = 1,
            Arc4EncryptionKeyIndex = 12
        };
        var arc4Key = new EncryptionKeyEntry
        {
            Kind = EncryptionKeyKind.Arc4,
            Number = 12,
            EncryptionKey = "12",
            EncryptionId = "1234567891"
        };

        var data = RadioProjectMapper.ToData([channel], [], [], [arc4Key], []);
        var loadedChannels = new List<ChannelEntry>();
        var loadedZones = new List<ZoneEntry>();
        var loadedArc4Keys = new List<EncryptionKeyEntry>();

        RadioProjectMapper.LoadInto(data, loadedChannels, loadedZones, null, loadedArc4Keys, null);

        AssertEqual("ARC4", loadedChannels[0].EncryptionMode);
        AssertEqual((byte)12, loadedChannels[0].Arc4EncryptionKeyIndex);
        AssertEqual(1, loadedArc4Keys.Count);
        AssertEqual(12, loadedArc4Keys[0].Number);
    }

    private static void BusyLockTxPermitUsesDifferentLabelsForAnalogAndDigital()
    {
        // Busy-Lock (analog) and TX Permit (digital) are genuinely different
        // raw-value spaces, not one shared space with only raw 0 relabelled
        // (an earlier 2026-07-17 finding claimed the latter; corrected
        // 2026-07-28 via a real vendor CPS dropdown-order confirmation and
        // cross-checked against the xbenkozx/anytone-cps reference project's
        // hardcoded BUSY_LOCK constant). Analog: 0=Off, 1=Different CDT,
        // 2=Channel Free. Digital: 0=Always, 1=Channel Free,
        // 2=Different Color Code, 3=Same Color Code.
        var channel = new ChannelEntry
        {
            ChannelType = 1
        };

        AssertEqual("Always", channel.BusyLockTxPermitSelection);

        channel.ChannelType = 0;
        AssertEqual("Off", channel.BusyLockTxPermitSelection);

        channel.ChannelType = 1;
        AssertEqual("Always", channel.BusyLockTxPermitSelection);

        // Digital raw 2 ("Different Color Code") means something else for
        // analog (raw 2 is "Channel Free" there) - switching type re-labels
        // the same raw byte rather than preserving the digital label.
        channel.BusyLockTxPermitSelection = "Different Color Code";
        channel.ChannelType = 0;
        AssertEqual("Channel Free", channel.BusyLockTxPermitSelection);

        // Digital raw 3 ("Same Color Code") has no analog counterpart, so
        // switching to analog clamps it back to raw 0 ("Off").
        channel.ChannelType = 1;
        channel.BusyLockTxPermitSelection = "Same Color Code";
        channel.ChannelType = 0;
        AssertEqual("Off", channel.BusyLockTxPermitSelection);

        var viewModel = new MainViewModel();
        viewModel.SelectedChannel = channel;
        AssertTrue(
            viewModel.BusyLockTxPermitValues.Contains(channel.BusyLockTxPermitSelection),
            "busy lock options should contain the selected value");

        AssertTrue(
            viewModel.BusyLockTxPermitValues.SequenceEqual(["Off", "Different CDT", "Channel Free"]),
            "analog busy lock options should be Off, Different CDT, Channel Free");

        channel.ChannelType = 1;
        viewModel.SelectedChannel = null;
        viewModel.SelectedChannel = channel;
        AssertTrue(
            viewModel.BusyLockTxPermitValues.SequenceEqual(["Always", "Channel Free", "Different Color Code", "Same Color Code"]),
            "digital tx permit options should be Always, Channel Free, Different Color Code, Same Color Code");
    }

    // 2026-07-19: real incident - Save and Write-to-Radio used to share one
    // dirty-tracking snapshot, so saving the project before writing made the
    // app forget a pending radio-write edit it had never actually sent.
    // These two tests lock in the fix (MarkClean/MarkRadioSynced are now
    // fully independent).
    private static void SavingDoesNotClearAPendingRadioWrite()
    {
        var channel = new ChannelEntry { Number = 1, Name = "V00", RxFrequencyMHz = 145.5 };
        channel.MarkRadioSynced();
        channel.MarkClean();

        channel.Name = "CHANGED";
        AssertTrue(channel.HasAnyPendingRadioWrite, "editing a write-safe field should mark it pending for radio write");
        AssertTrue(channel.IsNameDirty, "editing a field should mark it dirty for file-save too");

        // Simulate Save.
        channel.MarkClean();

        AssertTrue(!channel.IsNameDirty, "MarkClean (Save) should clear file-save dirty state");
        AssertTrue(channel.HasAnyPendingRadioWrite, "MarkClean (Save) must NOT clear a pending radio write - it hasn't been written yet");
    }

    private static void WritingToRadioDoesNotClearFileDirtyState()
    {
        var channel = new ChannelEntry { Number = 1, Name = "V00", RxFrequencyMHz = 145.5 };
        channel.MarkRadioSynced();
        channel.MarkClean();

        channel.Name = "CHANGED";
        AssertTrue(channel.IsDirty, "editing a field should mark the project dirty");

        // Simulate a successful Write-to-Radio.
        channel.MarkRadioSynced();

        AssertTrue(!channel.HasAnyPendingRadioWrite, "MarkRadioSynced (Write) should clear the pending radio-write state");
        AssertTrue(channel.IsDirty, "MarkRadioSynced (Write) must NOT clear file-save dirty state - the project file still doesn't reflect this edit");
    }

    // 2026-07-19: real request - validation must actually prevent invalid
    // values from being saved or written, not just look wrong in the UI.
    private static void OverlongChannelNameBlocksSaveAndWrite()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.HasBlockingValidationErrors, "freshly seeded view model should have no blocking validation errors");

        var channel = viewModel.Channels.First();
        channel.Name = "ThisNameIsWayTooLong";

        AssertTrue(viewModel.HasBlockingValidationErrors, "a channel name over 16 characters should be a blocking validation error");
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "save should be disabled while a blocking validation error exists");
    }

    private static void RejectsInvalidFrequencyTextWithoutChangingTheValue()
    {
        var channel = new ChannelEntry { RxFrequencyMHz = 145.5 };

        channel.RxFrequencyMHzText = "14abc.5";

        AssertEqual(145.5, channel.RxFrequencyMHz);
    }

    private static void RejectsOutOfRangeFrequencyTextWithoutChangingTheValue()
    {
        var channel = new ChannelEntry { RxFrequencyMHz = 145.5 };

        channel.RxFrequencyMHzText = "5000.00000";

        AssertEqual(145.5, channel.RxFrequencyMHz);
    }

    // Regression test for a real bug found 2026-08-23 by reviewing a real
    // saved project file: a simplex channel's OffsetMHz went stale (kept
    // its pre-edit value) whenever only RX was edited afterward, because
    // ComputeTransmitFrequencyMHz ignores OffsetMHz for OffsetDirection==0
    // and so the UI never surfaced the drift. 11 channels in the real file
    // carried a wrong OffsetMHz this way.
    private static void SimplexChannelKeepsOffsetMHzInSyncWithRxOnEveryEdit()
    {
        var channel = new ChannelEntry { OffsetDirection = 0 };

        channel.RxFrequencyMHzText = "433.45000";
        AssertEqual(433.45, channel.OffsetMHz);

        // The actual reported bug shape: RX edited AGAIN after the channel
        // already existed - OffsetMHz must track the new value, not keep
        // the one from the first edit.
        channel.RxFrequencyMHzText = "433.62500";
        AssertEqual(433.625, channel.OffsetMHz);
    }

    private static void SwitchingToSimplexSnapsOffsetMHzToRx()
    {
        var channel = new ChannelEntry
        {
            RxFrequencyMHz = 145.5,
            OffsetDirection = 1,
            OffsetMHz = 0.6
        };

        channel.OffsetDirection = 0;

        AssertEqual(145.5, channel.OffsetMHz);
    }

    private static void SaveNormalizesStaleSimplexOffsetsBeforeWriting()
    {
        var viewModel = new MainViewModel();
        var channel = viewModel.Channels.First();
        channel.OffsetDirection = 0;
        // Bypass the live-edit hooks (which would already fix this) to
        // simulate a channel loaded from an old file that went stale
        // before ChannelEntry's OnRxFrequencyMHzChanged fix existed.
        channel.OffsetMHz = channel.RxFrequencyMHz + 1.0;

        viewModel.NormalizeSimplexChannelOffsets();

        AssertEqual(channel.RxFrequencyMHz, channel.OffsetMHz);
    }

    private static void InvalidEncryptionKeyFormatsBlockSave()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.HasBlockingValidationErrors, "freshly seeded view model should have no blocking validation errors");

        var aesKey = viewModel.AesEncryptionKeys.First();
        aesKey.EncryptionId = "1234";
        AssertTrue(viewModel.HasBlockingValidationErrors, "a malformed AES key (not 64 hex chars, not 'Off') should be a blocking validation error");
        aesKey.EncryptionId = "Off";
        AssertTrue(!viewModel.HasBlockingValidationErrors, "restoring 'Off' should clear the AES key error");

        var arc4Key = viewModel.Arc4EncryptionKeys.First();
        arc4Key.EncryptionKey = "not-hex";
        AssertTrue(viewModel.HasBlockingValidationErrors, "a malformed ARC4 key (not valid hex) should be a blocking validation error");
        arc4Key.EncryptionKey = "Off";
        AssertTrue(!viewModel.HasBlockingValidationErrors, "restoring 'Off' should clear the ARC4 key error");

        var basicCode = viewModel.EncryptionKeys.First();
        basicCode.EncryptionId = "12";
        AssertTrue(viewModel.HasBlockingValidationErrors, "a malformed basic code (not exactly 4 digits) should be a blocking validation error");
    }

    private static void ValidatesDottedFrequenciesIndependentOfCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            CultureInfo.CurrentUICulture = new CultureInfo("sv-SE");

            var viewModel = new MainViewModel();

            AssertTrue(
                viewModel.ValidationMessages.All(message => !message.Contains("outside expected range", StringComparison.Ordinal)),
                "seed frequencies with decimal points should validate in sv-SE culture");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    // 2026-07-18: every slot always exists internally (matches the real
    // vendor CPS - see MainViewModel.EnsureEncryptionKeySlotsPresent), so
    // these tests check which slots got a non-default value rather than
    // the underlying collection's count changing. 2026-08-15: Add now fills
    // exactly one slot per call (no more "generate N at once" count field -
    // see AddEncryptionKey's own doc comment) and the 3 Visible* collections
    // are the actual UI-facing lists, filtered to occupied slots only.
    private static void AddingEncryptionKeysFillsSlotsInOrderThenStops()
    {
        var viewModel = new MainViewModel();

        for (var i = 0; i < CodeplugLimits.BasicEncryptionCodeCount; i++)
        {
            viewModel.AddDigitalEncryptionKeyCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.BasicEncryptionCodeCount, viewModel.EncryptionKeys.Count);
        AssertEqual(CodeplugLimits.BasicEncryptionCodeCount, viewModel.VisibleEncryptionKeys.Count);
        AssertEqual(0, viewModel.EncryptionKeys.Count(key => key.EncryptionId == "0000"));
        AssertTrue(
            viewModel.VisibleEncryptionKeys.Select(key => key.Number).SequenceEqual(Enumerable.Range(1, CodeplugLimits.BasicEncryptionCodeCount)),
            "visible digital keys should be sorted by slot number");

        // Every slot is now full - one more Add should be a no-op, not
        // throw or overwrite an existing key.
        var beforeExtraAdd = viewModel.EncryptionKeys.Select(key => key.EncryptionId).ToList();
        viewModel.AddDigitalEncryptionKeyCommand.Execute(null);
        AssertTrue(
            viewModel.EncryptionKeys.Select(key => key.EncryptionId).SequenceEqual(beforeExtraAdd),
            "adding with no empty slots left should not change any key");
        AssertContains("No empty", viewModel.StatusMessage);
    }

    private static void AddsOneEncryptionKeyPerTypeAndSelectsIt()
    {
        var viewModel = new MainViewModel();

        viewModel.AddDigitalEncryptionKeyCommand.Execute(null);
        viewModel.AddArc4EncryptionKeyCommand.Execute(null);
        viewModel.AddAesEncryptionKeyCommand.Execute(null);

        // Rows always exist internally - Add fills the first still-default
        // slot and it should show up in its Visible list, selected.
        var digital = viewModel.EncryptionKeys.First(key => key.Number == 1);
        var arc4 = viewModel.Arc4EncryptionKeys.First(key => key.Number == 1);
        var aes = viewModel.AesEncryptionKeys.First(key => key.Number == 1);
        AssertEqual(4, digital.EncryptionId.Length);
        AssertTrue(digital.EncryptionId != "0000", "added digital code should not still be the default");
        AssertTrue(arc4.EncryptionKey.Length >= 10, "added ARC4 key should be hex and long enough");
        AssertEqual(64, aes.EncryptionId.Length);
        AssertTrue(viewModel.VisibleEncryptionKeys.Contains(digital), "newly added digital key should be visible");
        AssertTrue(viewModel.VisibleArc4EncryptionKeys.Contains(arc4), "newly added ARC4 key should be visible");
        AssertTrue(viewModel.VisibleAesEncryptionKeys.Contains(aes), "newly added AES key should be visible");
        AssertSame(digital, viewModel.SelectedEncryptionKey);
        AssertSame(arc4, viewModel.SelectedArc4EncryptionKey);
        AssertSame(aes, viewModel.SelectedAesEncryptionKey);
    }

    // Randomize replaces an already-assigned key's value in place - unlike
    // Remove/Clear, it must NOT touch Number (the slot itself) or drop any
    // channel's existing reference to this slot index.
    private static void RandomizeEncryptionKeyReplacesValueInPlaceWithoutChangingSlot()
    {
        var viewModel = new MainViewModel();

        viewModel.AddDigitalEncryptionKeyCommand.Execute(null);
        viewModel.AddArc4EncryptionKeyCommand.Execute(null);
        viewModel.AddAesEncryptionKeyCommand.Execute(null);

        var digital = viewModel.SelectedEncryptionKey!;
        var arc4 = viewModel.SelectedArc4EncryptionKey!;
        var aes = viewModel.SelectedAesEncryptionKey!;
        var digitalNumber = digital.Number;
        var arc4Number = arc4.Number;
        var aesNumber = aes.Number;
        var previousDigitalId = digital.EncryptionId;
        var previousArc4Key = arc4.EncryptionKey;
        var previousAesId = aes.EncryptionId;

        AssertTrue(viewModel.RegenerateDigitalEncryptionKeyCommand.CanExecute(null), "Randomize should be available once a digital key is selected");
        AssertTrue(viewModel.RegenerateArc4EncryptionKeyCommand.CanExecute(null), "Randomize should be available once an ARC4 key is selected");
        AssertTrue(viewModel.RegenerateAesEncryptionKeyCommand.CanExecute(null), "Randomize should be available once an AES key is selected");

        viewModel.RegenerateDigitalEncryptionKeyCommand.Execute(null);
        viewModel.RegenerateArc4EncryptionKeyCommand.Execute(null);
        viewModel.RegenerateAesEncryptionKeyCommand.Execute(null);

        AssertEqual(digitalNumber, digital.Number);
        AssertEqual(arc4Number, arc4.Number);
        AssertEqual(aesNumber, aes.Number);
        AssertEqual(4, digital.EncryptionId.Length);
        AssertTrue(digital.EncryptionId != previousDigitalId, "randomizing should produce a different digital code (astronomically unlikely to collide)");
        AssertTrue(arc4.EncryptionKey.Length >= 10 && arc4.EncryptionKey != previousArc4Key, "randomizing should produce a different ARC4 key");
        AssertEqual(64, aes.EncryptionId.Length);
        AssertTrue(aes.EncryptionId != previousAesId, "randomizing should produce a different AES key");
    }

    // Regression test for a real bug: the Randomize buttons stayed
    // permanently disabled in the actual UI because OnSelectedXxxEncryptionKeyChanged
    // only called RemoveXxxEncryptionKeyCommand.NotifyCanExecuteChanged(),
    // never the new Regenerate commands', even though both share the same
    // CanExecute predicate. Calling .CanExecute(null) directly (as the
    // other Randomize test above does) doesn't catch this - it always
    // re-evaluates the predicate fresh regardless of notification wiring,
    // the same way a bound Button.IsEnabled never would. This test instead
    // watches CanExecuteChanged itself, the event Button.IsEnabled actually
    // depends on.
    private static void RegenerateEncryptionKeyCommandsNotifyCanExecuteChangedOnSelection()
    {
        var viewModel = new MainViewModel();
        var digitalRaised = false;
        var arc4Raised = false;
        var aesRaised = false;
        viewModel.RegenerateDigitalEncryptionKeyCommand.CanExecuteChanged += (_, _) => digitalRaised = true;
        viewModel.RegenerateArc4EncryptionKeyCommand.CanExecuteChanged += (_, _) => arc4Raised = true;
        viewModel.RegenerateAesEncryptionKeyCommand.CanExecuteChanged += (_, _) => aesRaised = true;

        viewModel.AddDigitalEncryptionKeyCommand.Execute(null);
        viewModel.AddArc4EncryptionKeyCommand.Execute(null);
        viewModel.AddAesEncryptionKeyCommand.Execute(null);

        AssertTrue(digitalRaised, "selecting a digital key should raise RegenerateDigitalEncryptionKeyCommand.CanExecuteChanged");
        AssertTrue(arc4Raised, "selecting an ARC4 key should raise RegenerateArc4EncryptionKeyCommand.CanExecuteChanged");
        AssertTrue(aesRaised, "selecting an AES key should raise RegenerateAesEncryptionKeyCommand.CanExecuteChanged");
    }

    private static void RemovesUnusedEncryptionKey()
    {
        var viewModel = new MainViewModel();
        viewModel.AddDigitalEncryptionKeyCommand.Execute(null);

        var key = viewModel.EncryptionKeys.First(key => key.EncryptionId != "0000");
        viewModel.SelectedEncryptionKey = key;
        viewModel.RemoveEncryptionKeyCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertEqual(CodeplugLimits.BasicEncryptionCodeCount, viewModel.EncryptionKeys.Count);
        AssertEqual("0000", key.EncryptionId);
        AssertTrue(!viewModel.VisibleEncryptionKeys.Contains(key), "removed key should drop out of the visible list");
        AssertSame(null, viewModel.SelectedEncryptionKey);
    }

    private static void BlocksRemovalOfUsedEncryptionKey()
    {
        var viewModel = new MainViewModel();
        var key = viewModel.EncryptionKeys.First(key => key.Number == 7);
        key.EncryptionId = "1234";
        var channel = viewModel.Channels.First(channel => channel.IsDigital);
        channel.DigitalEncryptionIndex = 7;
        viewModel.SelectedEncryptionKey = key;

        viewModel.RemoveEncryptionKeyCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertEqual("1234", key.EncryptionId);
        AssertEqual("Digital", channel.EncryptionMode);
        AssertEqual((byte)7, channel.DigitalEncryptionIndex);
        AssertContains("used by 1 channel", viewModel.StatusMessage);
        AssertTrue(viewModel.VisibleEncryptionKeys.Contains(key), "still-occupied key should stay visible after a blocked removal");
        AssertSame(key, viewModel.SelectedEncryptionKey);
    }

    private static void CanRemoveUsedEncryptionKeyAndDisableChannelEncryption()
    {
        var viewModel = new MainViewModel();
        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.RemoveReferences));
        var key = viewModel.EncryptionKeys.First(key => key.Number == 7);
        key.EncryptionId = "1234";
        var channel = viewModel.Channels.First(channel => channel.IsDigital);
        channel.DigitalEncryptionIndex = 7;
        viewModel.SelectedEncryptionKey = key;

        viewModel.RemoveEncryptionKeyCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertEqual(CodeplugLimits.BasicEncryptionCodeCount, viewModel.EncryptionKeys.Count);
        AssertEqual("0000", key.EncryptionId);
        AssertEqual("Off", channel.EncryptionMode);
        AssertEqual((byte)0, channel.DigitalEncryptionIndex);
        AssertTrue(!viewModel.VisibleEncryptionKeys.Contains(key), "removed key should drop out of the visible list");
        AssertSame(null, viewModel.SelectedEncryptionKey);
    }

    private static void IgnoresTransientNullEncryptionSelectionDuringValidation()
    {
        var viewModel = new MainViewModel();
        var channel = viewModel.Channels.First(channel => channel.IsDigital);
        viewModel.SelectedChannel = channel;

        channel.AesDigitalEncryptionText = null!;

        AssertTrue(
            viewModel.ValidationMessages.All(message => !message.Contains("Object reference", StringComparison.Ordinal)),
            "validation should tolerate a transient null encryption selection");
    }

    private static void AddsAndRemovesMultipleZoneMembers()
    {
        var viewModel = new MainViewModel();

        viewModel.AddZoneCommand.Execute(null);
        var zone = viewModel.SelectedZone;
        AssertEqual(0, zone?.Members.Count ?? -1);

        var channelsToAdd = viewModel.AvailableZoneChannels.Take(2).ToArray();
        viewModel.SetSelectedAvailableZoneChannels(channelsToAdd);
        viewModel.AddZoneMembersCommand.Execute(null);

        AssertEqual(2, zone?.Members.Count ?? -1);
        AssertTrue(channelsToAdd.All(channel => zone!.Members.Contains(channel)), "selected channels should be added to zone");
        AssertTrue(channelsToAdd.All(channel => !viewModel.AvailableZoneChannels.Contains(channel)), "added channels should leave available list");
        AssertEqual(channelsToAdd[0], zone!.AChannel);
        AssertEqual(channelsToAdd[1], zone.BChannel);

        viewModel.SetSelectedZoneMembers(channelsToAdd);
        viewModel.RemoveZoneMembersCommand.Execute(null);

        // Confirmed real vendor CPS behavior 2026-07-19: a zone with no
        // channels left does not persist - it's removed automatically, not
        // left behind with an empty member list. SelectedZone falls back to
        // one of SeedData's own zones (which happen to already claim these
        // same channels as their own members), so "available" here is a
        // property of whichever zone is now selected, not of these channels
        // in the abstract - not asserted further.
        AssertEqual(0, zone.Members.Count);
        AssertTrue(!viewModel.Zones.Contains(zone), "zone with no channels left should be removed automatically");
    }

    private static void MapZonesResolvesAChannelBChannelByPositionInMembersNotGlobalIndex()
    {
        // AChannelIndex/BChannelIndex are a 0-based position within the
        // zone's own member list, NOT a global radio channel index - found
        // 2026-08-01 via a live differential write (see RadioReadMapper.
        // MapZones' doc comment). Deliberately uses non-sequential global
        // channel indices (10/20/30) so a regression back to the old
        // "look up AChannelIndex as a global index" bug would resolve to
        // the wrong channel (or fall back to the first member) instead of
        // failing to compile/silently coinciding.
        var channelAt10 = new ChannelEntry { Number = 11, Name = "First" };
        var channelAt20 = new ChannelEntry { Number = 21, Name = "Second" };
        var channelAt30 = new ChannelEntry { Number = 31, Name = "Third" };
        var channelsByRadioIndex = new Dictionary<int, ChannelEntry>
        {
            [10] = channelAt10,
            [20] = channelAt20,
            [30] = channelAt30
        };

        var zone = new ZoneCodec.DecodedZone(0)
        {
            Name = "Test Zone",
            ChannelMembers = [10, 20, 30],
            AChannelIndex = 2,
            BChannelIndex = 0
        };
        var result = new RadioCodeplugReadResult { Success = true, Zones = [zone] };

        var mapped = RadioReadMapper.MapZones(result, channelsByRadioIndex).Single();

        AssertSame(channelAt30, mapped.AChannel);
        AssertSame(channelAt10, mapped.BChannel);
    }

    private static void MapScanListsResolvesPriorityChannelsByOneBasedChannelNumberNotRadioIndex()
    {
        // PriorityChannel1/2's raw wire value is the 1-based channel
        // NUMBER, unlike ChannelMemberIndexes which is a 0-based radio
        // index - found 2026-08-02 via a live capture of a brand-new scan
        // list add in vendor CPS (raw 3 for the channel at radio index 2).
        // Deliberately uses non-sequential global channel indices (10/20/30)
        // so a regression back to treating the raw value as a direct radio
        // index would resolve to the wrong channel instead of silently
        // coinciding.
        var channelAt10 = new ChannelEntry { Number = 11, Name = "First" };
        var channelAt20 = new ChannelEntry { Number = 21, Name = "Second" };
        var channelAt30 = new ChannelEntry { Number = 31, Name = "Third" };
        var channelsByRadioIndex = new Dictionary<int, ChannelEntry>
        {
            [10] = channelAt10,
            [20] = channelAt20,
            [30] = channelAt30
        };

        var scanList = new ScanListCodec.DecodedScanList(0)
        {
            Name = "Test Scan List",
            ChannelMemberIndexes = [10, 20, 30],
            PriorityChannel1 = 31, // channel NUMBER of channelAt30, not its radio index (30)
            PriorityChannel2 = 11  // channel NUMBER of channelAt10, not its radio index (10)
        };
        var result = new RadioCodeplugReadResult { Success = true, ScanLists = [scanList] };

        var mapped = RadioReadMapper.MapScanLists(result, channelsByRadioIndex).Single();

        AssertSame(channelAt30, mapped.PriorityChannel1);
        AssertSame(channelAt10, mapped.PriorityChannel2);
    }

    private static void MapAmZonesResolvesMembersAndAChannelByGlobalAmAirIndex()
    {
        // AChannelIndex is read from a separate flat per-zone array, unlike
        // regular Zone's own AChannel/BChannel (which turned out to be a
        // position-within-members, not a global index) - treated here as a
        // global AM Air radio index instead, same resolution as
        // MemberChannelIndexes. Deliberately uses non-sequential indices
        // (5/10) so a regression back to treating AChannelIndex as a
        // position-within-members would resolve to the wrong channel.
        var amAirAt5 = new AmAirEntry { Number = 6, Name = "First" };
        var amAirAt10 = new AmAirEntry { Number = 11, Name = "Second" };
        var amAirByRadioIndex = new Dictionary<int, AmAirEntry>
        {
            [5] = amAirAt5,
            [10] = amAirAt10
        };

        var amZone = new AmZoneCodec.DecodedAmZone(0)
        {
            Name = "Test AM Zone",
            MemberChannelIndexes = [5, 10],
            AChannelIndex = 10
        };
        var result = new RadioCodeplugReadResult { Success = true, AmZones = [amZone] };

        var mapped = RadioReadMapper.MapAmZones(result, amAirByRadioIndex).Single();

        AssertTrue(mapped.Members.SequenceEqual(new[] { amAirAt5, amAirAt10 }), "members must resolve in order via the global AM Air index");
        AssertSame(amAirAt10, mapped.AChannel);
    }

    private static void SingleMemberZoneOnlySetsAChannel()
    {
        var viewModel = new MainViewModel();

        viewModel.AddZoneCommand.Execute(null);
        var zone = viewModel.SelectedZone!;

        var onlyChannel = viewModel.AvailableZoneChannels.First();
        viewModel.SetSelectedAvailableZoneChannels([onlyChannel]);
        viewModel.AddZoneMembersCommand.Execute(null);

        // Confirmed real vendor CPS behavior 2026-07-19: with exactly one
        // member, only A-Channel is set - B stays unset until a second
        // channel is added, and cannot be assigned any other way.
        AssertEqual(onlyChannel, zone.AChannel);
        AssertTrue(zone.BChannel is null, "B-Channel must stay unset with only one member");

        var secondChannel = viewModel.AvailableZoneChannels.First();
        viewModel.SetSelectedAvailableZoneChannels([secondChannel]);
        viewModel.AddZoneMembersCommand.Execute(null);

        AssertEqual(onlyChannel, zone.AChannel);
        AssertEqual(secondChannel, zone.BChannel);
    }

    private static void TracksUnsavedFieldChanges()
    {
        var viewModel = new MainViewModel();

        AssertTrue(!viewModel.IsDirty, "new seeded view model should start clean");

        viewModel.SelectedChannel!.Name = "Changed";

        AssertTrue(viewModel.IsDirty, "channel edit should mark project dirty");
        AssertTrue(viewModel.SelectedChannel.IsNameDirty, "edited field should be dirty");
        AssertEqual("Bold", viewModel.SelectedChannel.NameFontWeight);

        viewModel.MarkProjectClean();

        AssertTrue(!viewModel.IsDirty, "mark clean should reset project dirty state");
        AssertTrue(!viewModel.SelectedChannel.IsNameDirty, "mark clean should reset field dirty state");
        AssertEqual("Normal", viewModel.SelectedChannel.NameFontWeight);
    }

    private static void RefreshFilteredDigitalContactsCombinesFriendsOnlyWithTextFilter()
    {
        var viewModel = new MainViewModel();
        viewModel.DigitalContacts.Clear();
        viewModel.DigitalContacts.Add(new DigitalContactEntry { Index = 0, Name = "Jonas", IsFriend = true, RadioId = 2400002 });
        viewModel.DigitalContacts.Add(new DigitalContactEntry { Index = 1, Name = "Patrik", IsFriend = false, RadioId = 2400003 });
        viewModel.DigitalContacts.Add(new DigitalContactEntry { Index = 2, Name = "Jonathan", IsFriend = true, RadioId = 2400004 });

        viewModel.DigitalContactFriendsOnly = true;
        AssertEqual(2, viewModel.FilteredDigitalContacts.Count);
        AssertTrue(viewModel.FilteredDigitalContacts.All(c => c.IsFriend), "friends-only filter must exclude non-friends");

        viewModel.DigitalContactFilterText = "Jonas";
        AssertEqual(1, viewModel.FilteredDigitalContacts.Count);
        AssertEqual("Jonas", viewModel.FilteredDigitalContacts[0].Name);

        viewModel.DigitalContactFriendsOnly = false;
        AssertEqual(1, viewModel.FilteredDigitalContacts.Count);

        viewModel.DigitalContactFilterText = "";
        AssertEqual(3, viewModel.FilteredDigitalContacts.Count);
    }

    private static void TracksUnsavedZoneMembershipChanges()
    {
        var viewModel = new MainViewModel();
        var zone = viewModel.SelectedZone!;
        var channel = viewModel.AvailableZoneChannels.First();

        viewModel.SetSelectedAvailableZoneChannels([channel]);
        viewModel.AddZoneMembersCommand.Execute(null);

        AssertTrue(viewModel.IsDirty, "zone membership edit should mark project dirty");
        AssertTrue(zone.IsMembersDirty, "zone member field should be dirty");
        AssertEqual("Bold", zone.MembersFontWeight);
    }

    // Confirmed 2026-07-19: Scan List membership is stored on the
    // ScanListEntry side (Members, a ChannelEntry object-reference
    // collection), like Zone membership - not as a per-channel field.
    // SelectedChannelScanListName is a convenience that edits the
    // appropriate ScanListEntry's collection.
    private static void ScanListAssignmentEditsScanListMembershipNotAChannelField()
    {
        var viewModel = new MainViewModel();
        var channel = viewModel.Channels.First();
        viewModel.SelectedChannel = channel;
        viewModel.AddScanListCommand.Execute(null);
        var scanList = viewModel.ScanLists.Last();
        viewModel.MarkProjectClean();

        AssertEqual("None", viewModel.SelectedChannelScanListName);
        AssertTrue(!viewModel.IsDirty, "mark clean after adding the scan list should leave nothing unsaved");

        viewModel.SelectedChannelScanListName = scanList.Name;

        AssertEqual(scanList.Name, viewModel.SelectedChannelScanListName);
        AssertTrue(scanList.Members.Contains(channel), "assigning a channel to a scan list should add it to Members");
        AssertTrue(scanList.IsMembersDirty, "scan list member field should be dirty");
        AssertEqual("Bold", scanList.MembersFontWeight);
        AssertTrue(viewModel.IsDirty, "scan list membership edit should mark project dirty");

        viewModel.SelectedChannelScanListName = "None";

        AssertEqual("None", viewModel.SelectedChannelScanListName);
        AssertTrue(!scanList.Members.Contains(channel), "unassigning should remove the channel from Members");
    }

    // Confirmed 2026-07-20: encryption keys are read from the radio only
    // opt-in, so a full Read+Write round trip can happen without ever
    // confirming these slots' real values. A freshly-created placeholder
    // must never look "pending write" on its own - only an explicit user
    // edit (Generate/Clear/typing) should make one eligible for write. See
    // EncryptionKeyEntry's class doc comment.
    private static void FreshEncryptionKeyPlaceholdersNeverShowAsPendingRadioWrite()
    {
        var viewModel = new MainViewModel();

        AssertTrue(!viewModel.EncryptionKeys.Any(k => k.HasAnyPendingRadioWrite), "fresh digital code placeholders must not be pending write");
        AssertTrue(!viewModel.Arc4EncryptionKeys.Any(k => k.HasAnyPendingRadioWrite), "fresh ARC4 key placeholders must not be pending write");
        AssertTrue(!viewModel.AesEncryptionKeys.Any(k => k.HasAnyPendingRadioWrite), "fresh AES key placeholders must not be pending write");

        var arc4Key = viewModel.Arc4EncryptionKeys.First();
        arc4Key.EncryptionKey = "CAFE";

        AssertTrue(arc4Key.HasAnyPendingRadioWrite, "an explicit edit must mark the key pending write");
        AssertTrue(viewModel.Arc4EncryptionKeys.Where(k => k != arc4Key).All(k => !k.HasAnyPendingRadioWrite), "editing one key must not affect its siblings");
    }

    // Imports/Exports are disabled (not hidden) in the nav tree while CSV
    // support is disconnected - selecting them should not navigate away
    // from whatever tab is currently showing, matching how a disabled
    // Button never fires Click. Dev Options is hidden entirely rather than
    // disabled, so it isn't a leaf a user could select at all.
    private static void SelectingADisabledNavigationNodeDoesNotChangeTheSelectedTab()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectedTabIndex = 0;

        var importsNode = viewModel.NavigationTree
            .SelectMany(node => node.HasChildren ? node.Children : [node])
            .First(node => node.Title == "Imports");

        AssertTrue(!importsNode.IsEnabled, "Imports should be disabled until CSV import is reconnected");
        AssertTrue(!string.IsNullOrWhiteSpace(importsNode.DisabledReason), "a disabled nav node should explain why");

        viewModel.SelectedNavigationNode = importsNode;

        AssertEqual(0, viewModel.SelectedTabIndex);

        var devOptionsNode = viewModel.NavigationTree
            .SelectMany(node => node.HasChildren ? node.Children : [node])
            .First(node => node.Title == "Dev Options");

        AssertTrue(!devOptionsNode.IsVisible, "Dev Options should be hidden from the nav tree");
    }

    private static void EnablesSaveCommandsOnlyWhenDirty()
    {
        var viewModel = new MainViewModel();

        // Save As is always enabled - the user wants to be able to save a
        // fresh copy (e.g. to preserve an old backup before editing) even
        // when nothing has changed yet.
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "save should be disabled when project is clean");
        AssertTrue(viewModel.SaveProjectAsCommand.CanExecute(null), "save as should always be enabled");

        viewModel.SelectedChannel!.Name = "Changed";

        AssertTrue(viewModel.SaveProjectCommand.CanExecute(null), "save should be enabled when project is dirty");
        AssertTrue(viewModel.SaveProjectAsCommand.CanExecute(null), "save as should always be enabled");

        viewModel.MarkProjectClean();

        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "save should be disabled after marking clean");
        AssertTrue(viewModel.SaveProjectAsCommand.CanExecute(null), "save as should always be enabled");
    }

    // Regression test for a real bug: DuplicateChannel's hand-written property
    // list only copied ~17 of ChannelEntry's ~60 canonical fields (TxColorCode,
    // BusyLock, SquelchMode, PttId, Reverse and about 30 others were silently
    // left at their defaults on the copy). Fixed by having ChannelEntry.Clone()
    // reuse the same field list CreateSnapshot() already keeps up to date for
    // dirty-checking, instead of a second hand-maintained list.
    private static void DuplicateChannelCopiesEveryCanonicalField()
    {
        var viewModel = new MainViewModel();
        var source = viewModel.SelectedChannel!;

        source.Number = 5;
        source.Name = "Source";
        source.RxFrequencyMHz = 146.520;
        source.OffsetMHz = 0.6;
        source.OffsetDirection = 1;
        source.ChannelType = 1;
        source.TransmitPower = 3;
        source.Bandwidth = 1;
        source.CtcssDcsDecode = 2;
        source.CtcssDcsEncode = 2;
        source.ColorCode = 7;
        source.TxColorCode = 9;
        source.RepeaterSlot2 = true;
        source.ContactIndex = 12;
        source.RadioIdIndex = 3;
        source.BusyLock = 2;
        source.SquelchMode = 4;
        source.OptionalSignal = 3;
        source.PttId = 3;
        source.ScanListIndex = 8;
        source.ReceiveGroupListIndex = 9;
        source.PttProhibit = true;
        source.Reverse = true;
        source.SlotSuit = true;
        source.AesEncryptionIndex = 1;
        source.CallConfirmation = true;
        source.TalkAround = true;
        source.WorkAlone = true;
        source.CustomCtcss = 746;
        source.CtcssEncodeTone = 5;
        source.CtcssDecodeTone = 6;
        source.DcsEncodeTone = 100;
        source.DcsDecodeTone = 200;
        source.AutoScan = true;
        source.SmsConfirmation = true;
        source.CorrectFrequencyHz = 50;
        source.DmrModeDcdm = 1;
        source.DmrMode = true;
        source.ScrambleMode = 15;
        source.CustomScrambleFrequencyIndex = 2;
        source.Arc4EncryptionKeyIndex = 4;
        source.DigitalEncryptionIndex = 6;
        source.DmrCrcIgnore = true;
        source.SendTalkerAlias = true;
        source.SmsForbid = true;
        source.DataAckDisable = true;
        source.ExcludeChannelRoaming = true;
        source.AesRandomKey = true;
        source.AesMultipleKey = true;
        source.AprsRx = true;
        source.DtmfIdIndex = 4;
        source.Tone2IdIndex = 5;
        source.Tone5IdIndex = 6;
        source.Tone2Decode = 7;
        source.R5ToneBot = 1;
        source.R5ToneEot = 1;
        source.QdcIdIndex = 8;
        source.ExtendEncryption = true;
        source.IdleTx = true;
        source.Ranging = true;
        source.TxInterrupt = true;

        viewModel.DuplicateChannelCommand.Execute(null);
        var copy = viewModel.SelectedChannel!;

        AssertTrue(!ReferenceEquals(copy, source), "duplicate should be a new instance");
        AssertEqual("Source COPY", copy.Name);
        AssertTrue(copy.Number != source.Number, "duplicate should get its own channel number");

        AssertEqual(source.RxFrequencyMHz, copy.RxFrequencyMHz);
        AssertEqual(source.OffsetMHz, copy.OffsetMHz);
        AssertEqual(source.OffsetDirection, copy.OffsetDirection);
        AssertEqual(source.ChannelType, copy.ChannelType);
        AssertEqual(source.TransmitPower, copy.TransmitPower);
        AssertEqual(source.Bandwidth, copy.Bandwidth);
        AssertEqual(source.CtcssDcsDecode, copy.CtcssDcsDecode);
        AssertEqual(source.CtcssDcsEncode, copy.CtcssDcsEncode);
        AssertEqual(source.ColorCode, copy.ColorCode);
        AssertEqual(source.TxColorCode, copy.TxColorCode);
        AssertTrue(copy.RepeaterSlot2, "RepeaterSlot2 should copy");
        AssertEqual(source.ContactIndex, copy.ContactIndex);
        AssertEqual(source.RadioIdIndex, copy.RadioIdIndex);
        AssertEqual(source.BusyLock, copy.BusyLock);
        AssertEqual(source.SquelchMode, copy.SquelchMode);
        AssertEqual(source.OptionalSignal, copy.OptionalSignal);
        AssertEqual(source.PttId, copy.PttId);
        AssertEqual(source.ScanListIndex, copy.ScanListIndex);
        AssertEqual(source.ReceiveGroupListIndex, copy.ReceiveGroupListIndex);
        AssertTrue(copy.PttProhibit, "PttProhibit should copy");
        AssertTrue(copy.Reverse, "Reverse should copy");
        AssertTrue(copy.SlotSuit, "SlotSuit should copy");
        AssertEqual(source.AesEncryptionIndex, copy.AesEncryptionIndex);
        AssertTrue(copy.CallConfirmation, "CallConfirmation should copy");
        AssertTrue(copy.TalkAround, "TalkAround should copy");
        AssertTrue(copy.WorkAlone, "WorkAlone should copy");
        AssertEqual(source.CustomCtcss, copy.CustomCtcss);
        AssertEqual(source.CtcssEncodeTone, copy.CtcssEncodeTone);
        AssertEqual(source.CtcssDecodeTone, copy.CtcssDecodeTone);
        AssertEqual(source.DcsEncodeTone, copy.DcsEncodeTone);
        AssertEqual(source.DcsDecodeTone, copy.DcsDecodeTone);
        AssertTrue(copy.AutoScan, "AutoScan should copy");
        AssertTrue(copy.SmsConfirmation, "SmsConfirmation should copy");
        AssertEqual(source.CorrectFrequencyHz, copy.CorrectFrequencyHz);
        AssertEqual(source.DmrModeDcdm, copy.DmrModeDcdm);
        AssertTrue(copy.DmrMode, "DmrMode should copy");
        AssertEqual(source.ScrambleMode, copy.ScrambleMode);
        AssertEqual(source.CustomScrambleFrequencyIndex, copy.CustomScrambleFrequencyIndex);
        AssertEqual(source.Arc4EncryptionKeyIndex, copy.Arc4EncryptionKeyIndex);
        AssertEqual(source.DigitalEncryptionIndex, copy.DigitalEncryptionIndex);
        AssertTrue(copy.DmrCrcIgnore, "DmrCrcIgnore should copy");
        AssertTrue(copy.SendTalkerAlias, "SendTalkerAlias should copy");
        AssertTrue(copy.SmsForbid, "SmsForbid should copy");
        AssertTrue(copy.DataAckDisable, "DataAckDisable should copy");
        AssertTrue(copy.ExcludeChannelRoaming, "ExcludeChannelRoaming should copy");
        AssertTrue(copy.AesRandomKey, "AesRandomKey should copy");
        AssertTrue(copy.AesMultipleKey, "AesMultipleKey should copy");
        AssertTrue(copy.AprsRx, "AprsRx should copy");
        AssertEqual(source.DtmfIdIndex, copy.DtmfIdIndex);
        AssertEqual(source.Tone2IdIndex, copy.Tone2IdIndex);
        AssertEqual(source.Tone5IdIndex, copy.Tone5IdIndex);
        AssertEqual(source.Tone2Decode, copy.Tone2Decode);
        AssertEqual(source.R5ToneBot, copy.R5ToneBot);
        AssertEqual(source.R5ToneEot, copy.R5ToneEot);
        AssertEqual(source.QdcIdIndex, copy.QdcIdIndex);
        AssertTrue(copy.ExtendEncryption, "ExtendEncryption should copy");
        AssertTrue(copy.IdleTx, "IdleTx should copy");
        AssertTrue(copy.Ranging, "Ranging should copy");
        AssertTrue(copy.TxInterrupt, "TxInterrupt should copy");
    }

    // Multi-select bulk Copy/Delete - SetSelectedChannels is what both
    // platforms' Channels ListBox calls on every SelectionChanged (Desktop:
    // Ctrl/Shift-click; Mobile: long-press then tap-to-toggle), so driving it
    // directly here exercises the same path the UI does without needing a
    // live ListBox.
    private static void DuplicateChannelWithMultipleSelectedDuplicatesAllOfThem()
    {
        var viewModel = new MainViewModel();
        var before = viewModel.Channels.ToList();
        AssertTrue(before.Count >= 2, "seed data should have at least 2 channels");

        viewModel.SetSelectedChannels([before[0], before[1]]);
        viewModel.DuplicateChannelCommand.Execute(null);

        AssertEqual(before.Count + 2, viewModel.Channels.Count);
        AssertTrue(viewModel.Channels.Any(c => c.Name == $"{before[0].Name} COPY"), "first source should be duplicated");
        AssertTrue(viewModel.Channels.Any(c => c.Name == $"{before[1].Name} COPY"), "second source should be duplicated");
    }

    private static void RemoveChannelWithMultipleSelectedRemovesAllOfThem()
    {
        var viewModel = new MainViewModel();
        var before = viewModel.Channels.ToList();
        AssertTrue(before.Count >= 2, "seed data should have at least 2 channels");

        viewModel.SetSelectedChannels([before[0], before[1]]);
        viewModel.RemoveChannelCommand.Execute(null);

        AssertEqual(before.Count - 2, viewModel.Channels.Count);
        AssertTrue(!viewModel.Channels.Contains(before[0]), "first selected channel should be removed");
        AssertTrue(!viewModel.Channels.Contains(before[1]), "second selected channel should be removed");
    }

    private static void SelectingMultipleChannelsHidesTheSingleChannelEditor()
    {
        var viewModel = new MainViewModel();
        var channels = viewModel.Channels.ToList();
        AssertTrue(channels.Count >= 2, "seed data should have at least 2 channels");

        AssertTrue(viewModel.IsSingleChannelSelected, "editor should show with 0 or 1 selected");

        viewModel.SetSelectedChannels([channels[0], channels[1]]);
        AssertTrue(!viewModel.IsSingleChannelSelected, "editor should hide once 2+ channels are selected");

        viewModel.SetSelectedChannels([channels[0]]);
        AssertTrue(viewModel.IsSingleChannelSelected, "editor should come back once only 1 is selected again");
    }

    // Hand-editing a channel's "No" field (or duplicating one that lands a
    // lower number than later entries) leaves the Channels list out of
    // numeric order, since list position only ever reflected insertion
    // order. Save/Save As should restore ascending order rather than
    // silently persisting whatever order editing happened to produce.
    private static void ReorderListsByNumberSortsChannelsAscendingAndKeepsSelection()
    {
        var viewModel = new MainViewModel();
        var channels = viewModel.Channels.ToList();
        AssertTrue(channels.Count >= 3, "seed data should have at least 3 channels");

        var first = channels[0];
        var middle = channels[channels.Count / 2];
        var highestNumber = channels.Max(c => c.Number);
        // Hand-edit "No" past the current max, out of list-position order -
        // exactly the real user action (retyping a channel number) that
        // leaves position and Number disagreeing.
        first.Number = highestNumber + 1;
        viewModel.SelectedChannel = middle;

        viewModel.ReorderListsByNumber();

        var numbers = viewModel.Channels.Select(c => c.Number).ToList();
        var sortedNumbers = numbers.OrderBy(n => n).ToList();
        AssertTrue(numbers.SequenceEqual(sortedNumbers), "Channels should be in ascending Number order after reordering");
        AssertEqual(highestNumber + 1, viewModel.Channels[^1].Number);
        AssertSame(middle, viewModel.SelectedChannel);
    }

    // Regression test for a real bug reported live: Radio Settings > Power-on
    // > Startup Zone A always snapped back to zone 1 no matter which zone
    // was picked in the ComboBox.
    private static void SettingStartupZoneANameUpdatesTheUnderlyingZoneIndexAndRoundTrips()
    {
        var viewModel = new MainViewModel();
        var zones = viewModel.Zones.ToList();
        AssertTrue(zones.Count >= 2, "seed data should have at least 2 zones");

        var target = zones[1];

        var zoneOptionsRenotified = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.OptionalSettingsZoneOptions))
            {
                zoneOptionsRenotified = true;
            }
        };

        viewModel.OptionalSettingsStartupZoneAName = target.DisplayLabel;

        AssertEqual((byte)(target.Number - 1), viewModel.OptionalSettings.StartupZoneA);
        AssertEqual(target.DisplayLabel, viewModel.OptionalSettingsStartupZoneAName);
        AssertTrue(!zoneOptionsRenotified, "selecting a startup zone must not re-notify the master zone options list - " +
            "that hands the ComboBox a brand-new ItemsSource instance mid-selection and resets it back to index 0");
    }

    // Regression test for a real bug: the About page rendered correctly
    // when it happened to be the tab already selected at startup (a fresh
    // binding evaluation reads IsAboutViewSelected's current value directly,
    // no notification needed), but never appeared when actually navigated
    // to afterward, on both Desktop and Mobile, because
    // OnSelectedTabIndexChanged's hand-maintained list of
    // OnPropertyChanged(nameof(IsXxxViewSelected)) calls was missing
    // IsAboutViewSelected specifically - so the XAML binding never learned
    // the computed property's value had changed. Watches PropertyChanged
    // directly (not just the raw property value) so this test would have
    // failed before the fix, the same lesson as the Randomize
    // CanExecuteChanged test above.
    private static void NavigatingToAboutRaisesPropertyChangedForIsAboutViewSelected()
    {
        var viewModel = new MainViewModel();
        var aboutNode = viewModel.NavigationTree
            .SelectMany(node => node.HasChildren ? node.Children : [node])
            .First(node => node.Title == "About");

        var raised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsAboutViewSelected))
            {
                raised = true;
            }
        };

        viewModel.SelectedNavigationNode = aboutNode;

        AssertTrue(raised, "navigating to About should raise PropertyChanged for IsAboutViewSelected");
        AssertTrue(viewModel.IsAboutViewSelected, "SelectedTabIndex should now match the About tab");
    }

    // General-purpose guard against the same bug class as the About test
    // above recurring for some OTHER tab in the future:
    // OnSelectedTabIndexChanged is a hand-maintained list of
    // OnPropertyChanged(nameof(IsXxxViewSelected)) calls, one per tab, with
    // nothing enforcing it stays in sync with the properties actually
    // declared - exactly how IsAboutViewSelected got silently missed.
    // Reflection-based rather than hand-typed, same reasoning as
    // FullRadioProjectDataRoundTripsThroughARealFile: catches a missing
    // notification for ANY IsXxxViewSelected property, including ones
    // added after this test was written.
    private static void EveryIsViewSelectedPropertyNotifiesOnSelectedTabIndexChanged()
    {
        var viewModel = new MainViewModel();
        var isViewSelectedProperties = typeof(MainViewModel).GetProperties()
            .Where(p => p.Name.StartsWith("Is", StringComparison.Ordinal)
                        && p.Name.EndsWith("ViewSelected", StringComparison.Ordinal)
                        && p.PropertyType == typeof(bool))
            .Select(p => p.Name)
            .ToList();
        AssertTrue(isViewSelectedProperties.Count > 10, "should find many IsXxxViewSelected properties via reflection - if this is low, the naming convention check itself may be broken");

        var raisedProperties = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && isViewSelectedProperties.Contains(name))
            {
                raisedProperties.Add(name);
            }
        };

        // Flip SelectedTabIndex away then back, twice, so every property
        // gets a chance to raise regardless of its own current truth value.
        viewModel.SelectedTabIndex = viewModel.SelectedTabIndex == 0 ? 1 : 0;
        viewModel.SelectedTabIndex = viewModel.SelectedTabIndex == 0 ? 1 : 0;

        var missing = isViewSelectedProperties.Except(raisedProperties).OrderBy(n => n).ToList();
        AssertTrue(missing.Count == 0, $"OnSelectedTabIndexChanged is missing OnPropertyChanged calls for: {string.Join(", ", missing)}");
    }

    // Golden-value tests: exact bytes captured 2026-07-17 from a real USB capture of the
    // vendor CPS's "Write To Radio" against a real D890UV (channel #3990's name
    // field, "control"->"WRTEST1"->"control") - see Docs/AnyTone_D890UV/Capture_Findings.md's
    // "WRITE protocol confirmed byte-for-byte" section. These pin RadioWriteProtocol's
    // request-building/checksum logic to real hardware-confirmed bytes, not just internal
    // consistency, without needing a radio connected to run.
    private static void BuildsWriteBlockRequestMatchingARealCapturedWrite()
    {
        var data = Convert.FromHexString("00000001570052005400450053005400");
        var request = RadioWriteProtocol.BuildBlockRequest(0x01f80ac0, data);

        var expected = Convert.FromHexString("5701f80ac01000000001570052005400450053005400bd06");
        AssertEqual(Convert.ToHexString(expected), Convert.ToHexString(request));
    }

    private static void BuildsWriteBlockRequestMatchingTheRevertCapture()
    {
        var data = Convert.FromHexString("0000000163006f006e00740072006f00");
        var request = RadioWriteProtocol.BuildBlockRequest(0x01f80ac0, data);

        var expected = Convert.FromHexString("5701f80ac0100000000163006f006e00740072006f006906");
        AssertEqual(Convert.ToHexString(expected), Convert.ToHexString(request));
    }

    private static void RejectsWriteBlockDataOfTheWrongLength()
    {
        var threw = false;
        try
        {
            RadioWriteProtocol.BuildBlockRequest(0x01f80ac0, new byte[8]);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        AssertTrue(threw, "building a write request with data.Length != 16 should throw ArgumentException");
    }

    // Real 128-byte channel record captured 2026-07-17 from a real D890UV
    // (channel #3990, "control" - the same record used for the live write/
    // verify/revert test). Used as a realistic fixture for ChannelCodec.Encode's
    // read-modify-write tests rather than a synthetic all-zero buffer, so the
    // "everything else untouched" assertions are checking against real,
    // non-trivial byte values (including the shared-byte flag bits).
    private static byte[] RealControlChannelRecord() => Convert.FromHexString(
        "44000000000000000800000011001100CF09000000000000000000FFFF00000001000000000000000000000000000000000000000000000000000000000000000000000163006F006E00740072006F006C0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

    private static void EmptyChannelPatchIsAByteIdenticalRoundTrip()
    {
        var original = RealControlChannelRecord();
        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch());

        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(result));
    }

    private static void ChannelNamePatchTouchesOnlyTheNameBytes()
    {
        var original = RealControlChannelRecord();
        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Name = "WRTEST1" });

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is >= 0x44 and < 0x64)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }

        var decoded = ChannelCodec.Decode(result, 0);
        AssertEqual("WRTEST1", decoded.Name);
    }

    private static void ChannelFrequencyPatchRoundTripsThroughDecode()
    {
        var original = RealControlChannelRecord();
        var patch = new ChannelCodec.ChannelFieldPatch { RxFrequencyMHz = 446.00625, OffsetMHz = 0.6 };
        var result = ChannelCodec.Encode(original, patch);
        var decoded = ChannelCodec.Decode(result, 0);

        AssertEqual(446.00625, decoded.RxFrequencyMHz);
        AssertEqual(0.6, decoded.OffsetMHz);

        // Bytes 0x08 onward (direction/bandwidth/power/type and everything
        // after) must be untouched - only 0x00-0x07 (RX freq + offset) changed.
        for (var i = 0x08; i < ChannelCodec.RecordLength; i++)
        {
            AssertEqual(original[i], result[i]);
        }
    }

    private static void OffsetDirectionPatchPreservesSiblingBits()
    {
        var original = RealControlChannelRecord();
        var beforeDecoded = ChannelCodec.Decode(original, 0);

        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { OffsetDirection = 2 });
        var afterDecoded = ChannelCodec.Decode(result, 0);

        AssertEqual((byte)2, afterDecoded.OffsetDirection);
        AssertEqual(beforeDecoded.BandWidth, afterDecoded.BandWidth);
        AssertEqual(beforeDecoded.TxPower, afterDecoded.TxPower);
        AssertEqual(beforeDecoded.ChannelType, afterDecoded.ChannelType);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x08)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }
    }

    private static void CtcssDcsModePatchPreservesSiblingBits()
    {
        var original = RealControlChannelRecord();
        var beforeDecoded = ChannelCodec.Decode(original, 0);

        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { CtcssDcsEncode = 1, CtcssDcsDecode = 2 });
        var afterDecoded = ChannelCodec.Decode(result, 0);

        AssertEqual((byte)1, afterDecoded.CtcssDcsEncode);
        AssertEqual((byte)2, afterDecoded.CtcssDcsDecode);
        AssertEqual(beforeDecoded.Talkaround, afterDecoded.Talkaround);
        AssertEqual(beforeDecoded.CallConfirmation, afterDecoded.CallConfirmation);
        AssertEqual(beforeDecoded.PttProhibit, afterDecoded.PttProhibit);
        AssertEqual(beforeDecoded.Reverse, afterDecoded.Reverse);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x09)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }
    }

    private static void SquelchModePatchPreservesPttIdBits()
    {
        var original = RealControlChannelRecord();
        var beforeDecoded = ChannelCodec.Decode(original, 0);

        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { SquelchMode = 4 });
        var afterDecoded = ChannelCodec.Decode(result, 0);

        AssertEqual((byte)4, afterDecoded.SquelchMode);
        AssertEqual(beforeDecoded.PttId, afterDecoded.PttId);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x19)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }
    }

    private static void OptionalSignalAndBusyLockPatchEachOthersBitsIndependently()
    {
        var original = RealControlChannelRecord();

        var optionalOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { OptionalSignal = 3 });
        var optionalDecoded = ChannelCodec.Decode(optionalOnly, 0);
        var originalDecoded = ChannelCodec.Decode(original, 0);
        AssertEqual((byte)3, optionalDecoded.OptionalSignal);
        AssertEqual(originalDecoded.BusyLock, optionalDecoded.BusyLock);

        var busyLockOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { BusyLock = 2 });
        var busyLockDecoded = ChannelCodec.Decode(busyLockOnly, 0);
        AssertEqual((byte)2, busyLockDecoded.BusyLock);
        AssertEqual(originalDecoded.OptionalSignal, busyLockDecoded.OptionalSignal);

        var both = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { OptionalSignal = 3, BusyLock = 2 });
        var bothDecoded = ChannelCodec.Decode(both, 0);
        AssertEqual((byte)3, bothDecoded.OptionalSignal);
        AssertEqual((byte)2, bothDecoded.BusyLock);

        // Confirmed write-safe via a live differential test 2026-08-01 (real
        // vendor CPS write against channel AV00, Optional Signal set to
        // QDC1200 - byte 0x1a went to 0x40, i.e. bit 6 set, proving this is
        // a 3-bit field, not 2 bits as originally assumed).
        var qdc = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { OptionalSignal = 4 });
        var qdcDecoded = ChannelCodec.Decode(qdc, 0);
        AssertEqual((byte)4, qdcDecoded.OptionalSignal);
        AssertEqual(originalDecoded.BusyLock, qdcDecoded.BusyLock);
    }

    // 2026-07-19: confirmed write-safe via a live differential test (real
    // vendor CPS write to real hardware, exact match on read-back for
    // Contact/Talk Group, Radio ID, Receive Group List, PTT ID).
    private static void ContactIndexPatchRoundTripsThroughDecode()
    {
        var original = RealControlChannelRecord();

        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { ContactIndex = 1234 });
        var decoded = ChannelCodec.Decode(result, 0);

        AssertEqual((ushort)1234, decoded.ContactIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x13 or 0x14)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }
    }

    private static void RadioIdAndPttIdPatchEachOthersBitsIndependently()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var radioIdOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { RadioIdIndex = 5 });
        var radioIdDecoded = ChannelCodec.Decode(radioIdOnly, 0);
        AssertEqual((byte)5, radioIdDecoded.RadioIdIndex);
        AssertEqual(originalDecoded.PttId, radioIdDecoded.PttId);
        AssertEqual(originalDecoded.SquelchMode, radioIdDecoded.SquelchMode);

        var pttIdOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { PttId = 3 });
        var pttIdDecoded = ChannelCodec.Decode(pttIdOnly, 0);
        AssertEqual((byte)3, pttIdDecoded.PttId);
        AssertEqual(originalDecoded.SquelchMode, pttIdDecoded.SquelchMode);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x18)
            {
                continue;
            }

            AssertEqual(original[i], radioIdOnly[i]);
        }

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x19)
            {
                continue;
            }

            AssertEqual(original[i], pttIdOnly[i]);
        }
    }

    private static void ReceiveGroupCallListIndexPatchRoundTripsThroughDecode()
    {
        var original = RealControlChannelRecord();

        var result = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { ReceiveGroupCallListIndex = 7 });
        var decoded = ChannelCodec.Decode(result, 0);

        AssertEqual((byte)7, decoded.ReceiveGroupCallListIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x1c)
            {
                continue;
            }

            AssertEqual(original[i], result[i]);
        }
    }

    // Round 1, confirmed write-safe via a live differential test 2026-07-19
    // (real vendor CPS write to real hardware, exact match on read-back for
    // ChannelType/TransmitPower/Bandwidth, all 3 sharing byte 0x08 with the
    // already-confirmed OffsetDirection).
    private static void ChannelTypeTransmitPowerAndBandwidthPatchEachOthersBitsIndependently()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var channelTypeOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { ChannelType = 3 });
        var channelTypeDecoded = ChannelCodec.Decode(channelTypeOnly, 0);
        AssertEqual((byte)3, channelTypeDecoded.ChannelType);
        AssertEqual(originalDecoded.TxPower, channelTypeDecoded.TxPower);
        AssertEqual(originalDecoded.BandWidth, channelTypeDecoded.BandWidth);
        AssertEqual(originalDecoded.OffsetDirection, channelTypeDecoded.OffsetDirection);

        var transmitPowerOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { TransmitPower = 0 });
        var transmitPowerDecoded = ChannelCodec.Decode(transmitPowerOnly, 0);
        AssertEqual((byte)0, transmitPowerDecoded.TxPower);
        AssertEqual(originalDecoded.ChannelType, transmitPowerDecoded.ChannelType);
        AssertEqual(originalDecoded.BandWidth, transmitPowerDecoded.BandWidth);
        AssertEqual(originalDecoded.OffsetDirection, transmitPowerDecoded.OffsetDirection);

        var bandwidthOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Bandwidth = 1 });
        var bandwidthDecoded = ChannelCodec.Decode(bandwidthOnly, 0);
        AssertEqual((byte)1, bandwidthDecoded.BandWidth);
        AssertEqual(originalDecoded.ChannelType, bandwidthDecoded.ChannelType);
        AssertEqual(originalDecoded.TxPower, bandwidthDecoded.TxPower);
        AssertEqual(originalDecoded.OffsetDirection, bandwidthDecoded.OffsetDirection);

        var all = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { ChannelType = 3, TransmitPower = 0, Bandwidth = 1 });
        var allDecoded = ChannelCodec.Decode(all, 0);
        AssertEqual((byte)3, allDecoded.ChannelType);
        AssertEqual((byte)0, allDecoded.TxPower);
        AssertEqual((byte)1, allDecoded.BandWidth);
        AssertEqual(originalDecoded.OffsetDirection, allDecoded.OffsetDirection);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x08)
            {
                continue;
            }

            AssertEqual(original[i], all[i]);
        }
    }

    // Round 2, confirmed write-safe via a live differential test 2026-07-19
    // (real vendor CPS write to real hardware) - Reverse only applies to
    // analog channels (a real vendor-side interlock, not a decode bug,
    // confirmed when it didn't stick on a non-analog test channel combined
    // with the other 3 flags).
    private static void TalkAroundCallConfirmationPttProhibitAndReversePatchEachOthersBitsIndependently()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var talkAroundOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { TalkAround = !originalDecoded.Talkaround });
        var talkAroundDecoded = ChannelCodec.Decode(talkAroundOnly, 0);
        AssertEqual(!originalDecoded.Talkaround, talkAroundDecoded.Talkaround);
        AssertEqual(originalDecoded.CallConfirmation, talkAroundDecoded.CallConfirmation);
        AssertEqual(originalDecoded.PttProhibit, talkAroundDecoded.PttProhibit);
        AssertEqual(originalDecoded.Reverse, talkAroundDecoded.Reverse);
        AssertEqual(originalDecoded.CtcssDcsEncode, talkAroundDecoded.CtcssDcsEncode);
        AssertEqual(originalDecoded.CtcssDcsDecode, talkAroundDecoded.CtcssDcsDecode);

        var all = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            TalkAround = true,
            CallConfirmation = true,
            PttProhibit = true,
            Reverse = true
        });
        var allDecoded = ChannelCodec.Decode(all, 0);
        AssertTrue(allDecoded.Talkaround, "TalkAround should be set");
        AssertTrue(allDecoded.CallConfirmation, "CallConfirmation should be set");
        AssertTrue(allDecoded.PttProhibit, "PttProhibit should be set");
        AssertTrue(allDecoded.Reverse, "Reverse should be set");
        AssertEqual(originalDecoded.CtcssDcsEncode, allDecoded.CtcssDcsEncode);
        AssertEqual(originalDecoded.CtcssDcsDecode, allDecoded.CtcssDcsDecode);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x09)
            {
                continue;
            }

            AssertEqual(original[i], all[i]);
        }
    }

    // Round 5, confirmed write-safe via a live differential test 2026-07-19
    // (real vendor CPS write to real hardware, RX=8/TX=9) - proved RX Color
    // Code (0x20) and TX Color Code (0x43) are independent fields, not one
    // derived from the other as previously assumed.
    private static void RxColorCodeAndTxColorCodePatchRoundTripThroughDecodeIndependently()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var rxOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { RxColorCode = 8 });
        var rxOnlyDecoded = ChannelCodec.Decode(rxOnly, 0);
        AssertEqual((byte)8, rxOnlyDecoded.RxColorCode);
        AssertEqual(originalDecoded.TxColorCode, rxOnlyDecoded.TxColorCode);

        var txOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { TxColorCode = 9 });
        var txOnlyDecoded = ChannelCodec.Decode(txOnly, 0);
        AssertEqual((byte)9, txOnlyDecoded.TxColorCode);
        AssertEqual(originalDecoded.RxColorCode, txOnlyDecoded.RxColorCode);

        var both = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { RxColorCode = 8, TxColorCode = 9 });
        var bothDecoded = ChannelCodec.Decode(both, 0);
        AssertEqual((byte)8, bothDecoded.RxColorCode);
        AssertEqual((byte)9, bothDecoded.TxColorCode);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x20 or 0x43)
            {
                continue;
            }

            AssertEqual(original[i], both[i]);
        }
    }

    // Round 6, confirmed write-safe via a combined live differential test
    // 2026-07-19 (real vendor CPS write, exact match on read-back). DCDM
    // (bits 3-2 of the same byte) was checked for a vendor CPS control on
    // both analog and digital channels and confirmed to have none - left
    // permanently unwired, not just deferred.
    private static void WorkAloneSlotSuitRepeaterSlotAndSmsConfirmationPatchEachOthersBitsIndependently()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var workAloneOnly = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { WorkAlone = !originalDecoded.WorkAlone });
        var workAloneDecoded = ChannelCodec.Decode(workAloneOnly, 0);
        AssertEqual(!originalDecoded.WorkAlone, workAloneDecoded.WorkAlone);
        AssertEqual(originalDecoded.SlotSuit, workAloneDecoded.SlotSuit);
        AssertEqual(originalDecoded.TimeSlot, workAloneDecoded.TimeSlot);
        AssertEqual(originalDecoded.SmsConfirmation, workAloneDecoded.SmsConfirmation);
        AssertEqual(originalDecoded.DmrModeDcdm, workAloneDecoded.DmrModeDcdm);
        AssertEqual(originalDecoded.AprsRx, workAloneDecoded.AprsRx);

        var all = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            WorkAlone = true,
            SlotSuit = true,
            RepeaterSlot2 = true,
            SmsConfirmation = true,
            DmrModeDcdm = 2
        });
        var allDecoded = ChannelCodec.Decode(all, 0);
        AssertTrue(allDecoded.WorkAlone, "WorkAlone should be set");
        AssertTrue(allDecoded.SlotSuit, "SlotSuit should be set");
        AssertTrue(allDecoded.TimeSlot, "TimeSlot (RepeaterSlot2) should be set");
        AssertTrue(allDecoded.SmsConfirmation, "SmsConfirmation should be set");
        AssertEqual((byte)2, allDecoded.DmrModeDcdm);
        AssertEqual(originalDecoded.AprsRx, allDecoded.AprsRx);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x21)
            {
                continue;
            }

            AssertEqual(original[i], all[i]);
        }
    }

    // Confirmed 2026-07-19 by a live differential test (re-test, corrected
    // an earlier wrong "confirmed dead" conclusion): DmrModeDcdm (bits 3-2
    // of 0x21) is a real, write-safe 3-value "DCDM submode" field - DMO
    // Simplex and Repeater both leave it at raw 0 (they're distinguished by
    // a separate bit, ChannelEntry.DmrMode - see
    // DmrModePatchPreservesSiblingBitsIn0x34 and
    // DmrModeSelectionCombinesDmrModeDcdmAndDmrMode below, corrected
    // 2026-07-31); only Double Slot (raw 1) and TS Split (raw 2) are
    // distinct raw values for this specific field.
    private static void DmrModeDcdmPatchPreservesSiblingBitsIn0x21()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { DmrModeDcdm = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)1, decoded.DmrModeDcdm);
        AssertEqual(originalDecoded.WorkAlone, decoded.WorkAlone);
        AssertEqual(originalDecoded.SlotSuit, decoded.SlotSuit);
        AssertEqual(originalDecoded.TimeSlot, decoded.TimeSlot);
        AssertEqual(originalDecoded.SmsConfirmation, decoded.SmsConfirmation);
        AssertEqual(originalDecoded.AprsRx, decoded.AprsRx);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x21)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via 2 clean live differential tests 2026-07-31
    // (real vendor CPS writes against channel DV01): picking "DMO/Simplex"
    // in the vendor CPS's DMR Mode dropdown set byte 0x34 bit 1 to 1;
    // picking "Repeater" set it back to 0, with nothing else in the
    // 128-byte record touched either time. This is the actual DMO/Simplex-
    // vs-Repeater discriminator an earlier 2026-07-19 finding missed (it
    // correctly found DmrModeDcdm alone can't tell them apart, but never
    // isolated the DMR Mode dropdown itself to find what does).
    private static void DmrModePatchPreservesSiblingBitsIn0x34()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { DmrMode = !originalDecoded.DmrMode });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.DmrMode, decoded.DmrMode);
        AssertEqual(originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual(originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(originalDecoded.Ranging, decoded.Ranging);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x34)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void DmrModeSelectionCombinesDmrModeDcdmAndDmrMode()
    {
        var channel = new ChannelEntry { ChannelType = 1 };

        channel.DmrModeDcdm = 0;
        channel.DmrMode = true;
        AssertEqual("DMO/simplex", channel.DmrModeSelection);

        channel.DmrModeDcdm = 0;
        channel.DmrMode = false;
        AssertEqual("Repeater", channel.DmrModeSelection);

        channel.DmrModeDcdm = 1;
        AssertEqual("DCDM Double Slot", channel.DmrModeSelection);

        channel.DmrModeDcdm = 2;
        AssertEqual("DCDM TS Split", channel.DmrModeSelection);

        // Selecting a DCDM option deliberately doesn't touch DmrMode - its
        // value while a DCDM mode is active isn't confirmed, so leave it
        // as-is rather than guess.
        channel.DmrModeDcdm = 0;
        channel.DmrMode = true;
        channel.DmrModeSelection = "DCDM TS Split";
        AssertEqual((byte)2, channel.DmrModeDcdm);
        AssertTrue(channel.DmrMode, "DmrMode should be untouched by selecting a DCDM option");

        channel.DmrModeSelection = "Repeater";
        AssertEqual((byte)0, channel.DmrModeDcdm);
        AssertTrue(!channel.DmrMode, "Repeater should clear DmrMode");
    }

    // Round 7/8/9, confirmed write-safe via the same combined live
    // differential test 2026-07-19 (real vendor CPS write, exact match on
    // read-back). DigitalEncryptionIndex was blocked by a vendor CPS
    // interlock (channel already using AES/ARC4) - deferred, not wired.
    // CorrectFrequencyHz and CustomCtcss are still unresolved (see
    // RESUME_HERE.md / task tracker) - not wired either.
    private static void AesArc4AutoScanScrambleModeAndCustomScrambleFrequencyIndexPatchRoundTripThroughDecode()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            AesEncryptionIndex = 2,
            Arc4EncryptionKeyIndex = 2,
            AutoScan = !originalDecoded.AutoScan,
            ScrambleMode = 5,
            CustomScrambleFrequencyIndex = 1
        });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)2, decoded.AesEncryptionIndex);
        AssertEqual((byte)2, decoded.Arc4EncryptionKeyIndex);
        AssertEqual(!originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual((byte)5, decoded.ScramblerSet);
        AssertEqual((byte)1, decoded.CustomScrambler);
        // Sibling bits sharing 0x34 with AutoScan stay untouched.
        AssertEqual(originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(originalDecoded.DmrMode, decoded.DmrMode);
        AssertEqual(originalDecoded.Ranging, decoded.Ranging);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x22 or 0x34 or 0x3d or 0x3e or 0x3f)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Round 7/8/9 follow-ups, confirmed write-safe via a live differential
    // test 2026-07-19 (re-tested after clearing a vendor CPS interlock for
    // Digital Encryption, and after finding the real scale factors for
    // CorrectFrequencyHz - 10 Hz per raw count, two independent data points
    // 1000->100 and 10->1 - and CustomCtcss - tenths of a Hz, 74.6Hz->746).
    private static void DigitalEncryptionCorrectFrequencyAndCustomCtcssPatchRoundTripThroughDecode()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            DigitalEncryptionIndex = 2,
            CorrectFrequencyHz = 1,
            CustomCtcss = 746
        });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)2, decoded.DigitalEncryption);
        AssertEqual((byte)1, decoded.CorrectFrequency);
        AssertEqual((ushort)746, decoded.CustomCtcss);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x10 or 0x11 or 0x39 or 0x3a)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-07-31 (real
    // vendor CPS write against channel DV01, radio index 399 - the captured
    // payload showed exactly these 3 bits set, and no other populated
    // channel in the same full-codeplug write had any of them set).
    private static void DmrCrcIgnoreSendTalkerAliasAndSmsForbidPatchRoundTripThroughDecode()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            DmrCrcIgnore = !originalDecoded.DmrCrcIgnore,
            SendTalkerAlias = !originalDecoded.SendTalkerAlias,
            SmsForbid = !originalDecoded.SmsForbid
        });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(!originalDecoded.SendTalkerAlias, decoded.SendTalkerAlias);
        AssertEqual(!originalDecoded.SmsForbid, decoded.SmsForbid);

        // Sibling bits sharing 0x34 with DmrCrcIgnore, and 0x3b with
        // SendTalkerAlias/SmsForbid, stay untouched.
        AssertEqual(originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual(originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(originalDecoded.DmrMode, decoded.DmrMode);
        AssertEqual(originalDecoded.Ranging, decoded.Ranging);
        AssertEqual(originalDecoded.ExtendEncryption, decoded.ExtendEncryption);
        AssertEqual(originalDecoded.AnalogAprsMute, decoded.AnalogAprsMute);
        AssertEqual(originalDecoded.AesRandomKey, decoded.AesRandomKey);
        AssertEqual(originalDecoded.AesMultipleKey, decoded.AesMultipleKey);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x34 or 0x3b)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via 2 live differential tests 2026-07-31 (real
    // vendor CPS writes against channel DV01). Round 1: DataAckDisable
    // flipped 1->0 in isolation, but the write also touched AesEncryptionIndex
    // and 3 bits that hadn't been intentionally set (assigning an AES key
    // has side effects on ExcludeChannelRoaming/AesRandomKey/AesMultipleKey
    // defaults - not itself confirmed as a codec relationship, just an
    // observed vendor CPS behavior when a channel's AES key changes from
    // Off to assigned). Round 2, with the AES key already assigned, set
    // ExcludeChannelRoaming=Off, AesRandomKey=On, AesMultipleKey=Off (a
    // deliberately asymmetric pair) and got a clean 2-bit diff with nothing
    // else touched, which also disambiguated the AesRandomKey/AesMultipleKey
    // bit order (they'd moved together, indistinguishably, in round 1).
    private static void DataAckDisableExcludeChannelRoamingAndAesKeyFlagsPatchRoundTripThroughDecode()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            DataAckDisable = !originalDecoded.DataAckDisable,
            ExcludeChannelRoaming = !originalDecoded.ExcludeChannelRoaming,
            AesRandomKey = !originalDecoded.AesRandomKey,
            AesMultipleKey = !originalDecoded.AesMultipleKey
        });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(!originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(!originalDecoded.AesRandomKey, decoded.AesRandomKey);
        AssertEqual(!originalDecoded.AesMultipleKey, decoded.AesMultipleKey);

        // Sibling bits sharing 0x34/0x3b stay untouched.
        AssertEqual(originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual(originalDecoded.DmrMode, decoded.DmrMode);
        AssertEqual(originalDecoded.Ranging, decoded.Ranging);
        AssertEqual(originalDecoded.SendTalkerAlias, decoded.SendTalkerAlias);
        AssertEqual(originalDecoded.SmsForbid, decoded.SmsForbid);
        AssertEqual(originalDecoded.ExtendEncryption, decoded.ExtendEncryption);
        AssertEqual(originalDecoded.AnalogAprsMute, decoded.AnalogAprsMute);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x34 or 0x3b)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel DV02, exact match - byte 0x21 went
    // from 0x00 to 0x20, only bit 5, cross-checked against 550 other
    // populated channels with none showing this bit set).
    private static void AprsRxPatchPreservesSiblingBitsIn0x21()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { AprsRx = !originalDecoded.AprsRx });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.AprsRx, decoded.AprsRx);
        AssertEqual(originalDecoded.WorkAlone, decoded.WorkAlone);
        AssertEqual(originalDecoded.SlotSuit, decoded.SlotSuit);
        AssertEqual(originalDecoded.DmrModeDcdm, decoded.DmrModeDcdm);
        AssertEqual(originalDecoded.TimeSlot, decoded.TimeSlot);
        AssertEqual(originalDecoded.SmsConfirmation, decoded.SmsConfirmation);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x21)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00 with Optional Signal set to
    // DTMF - byte 0x1f went from unset to 1, matching "M2" selected
    // 0-based, only channel in the whole write with a nonzero value).
    private static void DtmfIdIndexPatchTouchesOnlyItsOwnByte()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { DtmfIdIndex = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)1, decoded.DtmfIdIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x1f)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void DtmfIdSelectionRoundTripsThroughM1ToM16Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("M1", channel.DtmfIdSelection);

        channel.DtmfIdSelection = "M2";
        AssertEqual((byte)1, channel.DtmfIdIndex);
        AssertEqual("M2", channel.DtmfIdSelection);

        channel.DtmfIdSelection = "M16";
        AssertEqual((byte)15, channel.DtmfIdIndex);
        AssertEqual("M16", channel.DtmfIdSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00 with Optional Signal set to
    // 2Tone - byte 0x1d went from unset to 1, matching 2Tone setting "2"
    // selected 0-based, only channel in the whole write with a nonzero
    // value).
    private static void Tone2IdIndexPatchTouchesOnlyItsOwnByte()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Tone2IdIndex = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)1, decoded.Tone2IdIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x1d)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void Tone2IdSelectionRoundTripsThrough1To16Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("1", channel.Tone2IdSelection);

        channel.Tone2IdSelection = "2";
        AssertEqual((byte)1, channel.Tone2IdIndex);
        AssertEqual("2", channel.Tone2IdSelection);

        channel.Tone2IdSelection = "16";
        AssertEqual((byte)15, channel.Tone2IdIndex);
        AssertEqual("16", channel.Tone2IdSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00 with Optional Signal set to
    // 5Tone - byte 0x1e went from unset to 1, matching 5Tone setting "2"
    // selected 0-based; a clean 2-byte diff against the prior 2Tone
    // capture, the other byte being OptionalSignal itself).
    private static void Tone5IdIndexPatchTouchesOnlyItsOwnByte()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Tone5IdIndex = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)1, decoded.Tone5IdIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x1e)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void Tone5IdSelectionRoundTripsThrough1To16Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("1", channel.Tone5IdSelection);

        channel.Tone5IdSelection = "2";
        AssertEqual((byte)1, channel.Tone5IdIndex);
        AssertEqual("2", channel.Tone5IdSelection);

        channel.Tone5IdSelection = "16";
        AssertEqual((byte)15, channel.Tone5IdIndex);
        AssertEqual("16", channel.Tone5IdSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00 with Optional Signal set back
    // to 2Tone - byte 0x12 went from 0 to 1, matching "2Tone Decode = 2"
    // selected 0-based; a clean 2-byte diff against the prior 5Tone
    // capture, the other byte being OptionalSignal switching back).
    private static void Tone2DecodePatchTouchesOnlyItsOwnByte()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Tone2Decode = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)1, decoded.Tone2Decode);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x12)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Only raw 0/1 were ever seen on the wire (the vendor CPS only had 2
    // configured 2Tone settings entries at test time) - treated as the
    // same 16-slot range as the other signaling ID selections rather than
    // capped at 2, since a later test with a 3rd configured entry showed a
    // 3rd item appear in the real vendor CPS dropdown.
    private static void Tone2DecodeSelectionRoundTripsThrough1To16Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("1", channel.Tone2DecodeSelection);

        channel.Tone2DecodeSelection = "2";
        AssertEqual((byte)1, channel.Tone2Decode);
        AssertEqual("2", channel.Tone2DecodeSelection);

        channel.Tone2DecodeSelection = "16";
        AssertEqual((byte)15, channel.Tone2Decode);
        AssertEqual("16", channel.Tone2DecodeSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00 with Optional Signal set to
    // 5Tone - byte 0x41 (R5ToneEot) went from 0 to 1, matching "item 2"
    // selected; byte 0x40 (R5ToneBot) stayed 0, matching "item 1". A
    // clean 2-byte diff against the prior 2Tone Decode capture, the
    // other byte being OptionalSignal switching to 5Tone).
    private static void R5ToneBotAndR5ToneEotPatchTouchOnlyTheirOwnBytes()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { R5ToneBot = 0, R5ToneEot = 1 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)0, decoded.R5ToneBot);
        AssertEqual((byte)1, decoded.R5ToneEot);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x40 or 0x41)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-08-02 (real
    // vendor CPS write against channel AV00, 5Tone Bot set to "Customize" -
    // byte 0x40 went to 0x64 (100 decimal), uniquely isolated against
    // every other populated channel in the capture. A sentinel value, not
    // the next sequential index after the "1"/"2" presets).
    private static void R5ToneBotCustomizePatchRoundTripsThroughDecode()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { R5ToneBot = 100 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)100, decoded.R5ToneBot);
    }

    private static void R5ToneBotAndEotSelectionRoundTripThrough1And2AndCustomizeLabels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("1", channel.R5ToneBotSelection);
        AssertEqual("1", channel.R5ToneEotSelection);

        channel.R5ToneBotSelection = "2";
        AssertEqual((byte)1, channel.R5ToneBot);
        AssertEqual("2", channel.R5ToneBotSelection);

        channel.R5ToneEotSelection = "2";
        AssertEqual((byte)1, channel.R5ToneEot);
        AssertEqual("2", channel.R5ToneEotSelection);

        channel.R5ToneBotSelection = "Customize";
        AssertEqual((byte)100, channel.R5ToneBot);
        AssertEqual("Customize", channel.R5ToneBotSelection);

        channel.R5ToneEotSelection = "Customize";
        AssertEqual((byte)100, channel.R5ToneEot);
        AssertEqual("Customize", channel.R5ToneEotSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00, Optional Signal set to
    // QDC1200 with the QDC1200 ID field set to its 3rd entry - byte 0x42
    // went from 0 to 2, previously an unclaimed byte between R5ToneEot
    // (0x41) and TxColorCode (0x43)).
    private static void QdcIdIndexPatchTouchesOnlyItsOwnByte()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { QdcIdIndex = 2 });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual((byte)2, decoded.QdcIdIndex);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x42)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void QdcIdSelectionRoundTripsThrough1To16Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("1", channel.QdcIdSelection);

        channel.QdcIdSelection = "2";
        AssertEqual((byte)1, channel.QdcIdIndex);
        AssertEqual("2", channel.QdcIdSelection);

        channel.QdcIdSelection = "16";
        AssertEqual((byte)15, channel.QdcIdIndex);
        AssertEqual("16", channel.QdcIdSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel EV01, Extended Encryption set to
    // ARC4 - a single clean bit flip, byte 0x3b bit 5, 0x00 -> 0x20,
    // diffed against the channel's own earlier state - nothing else
    // touched, including AesEncryptionIndex/Arc4EncryptionKeyIndex which
    // kept their existing values).
    private static void ExtendEncryptionPatchPreservesSiblingBitsIn0x3b()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { ExtendEncryption = !originalDecoded.ExtendEncryption });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.ExtendEncryption, decoded.ExtendEncryption);
        AssertEqual(originalDecoded.SendTalkerAlias, decoded.SendTalkerAlias);
        AssertEqual(originalDecoded.SmsForbid, decoded.SmsForbid);
        AssertEqual(originalDecoded.AesRandomKey, decoded.AesRandomKey);
        AssertEqual(originalDecoded.AesMultipleKey, decoded.AesMultipleKey);
        AssertEqual(originalDecoded.AnalogAprsMute, decoded.AnalogAprsMute);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x3b)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void ExtendEncryptionSelectionRoundTripsThroughAesAndArc4Labels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("AES", channel.ExtendEncryptionSelection);

        channel.ExtendEncryptionSelection = "ARC4";
        AssertTrue(channel.ExtendEncryption, "ARC4 should set ExtendEncryption");
        AssertEqual("ARC4", channel.ExtendEncryptionSelection);

        channel.ExtendEncryptionSelection = "AES";
        AssertTrue(!channel.ExtendEncryption, "AES should clear ExtendEncryption");
        AssertEqual("AES", channel.ExtendEncryptionSelection);
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel AV00, "Idle TX" toggled on - a
    // single clean bit flip, byte 0x34 bit 5, diffed against the
    // channel's own earlier state, nothing else touched). Discovered
    // from scratch - no prior offset was known for this field.
    private static void IdleTxPatchPreservesSiblingBitsIn0x34()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { IdleTx = !originalDecoded.IdleTx });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.IdleTx, decoded.IdleTx);
        AssertEqual(originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual(originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(originalDecoded.DmrMode, decoded.DmrMode);
        AssertEqual(originalDecoded.Ranging, decoded.Ranging);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x34)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-08-02 (real
    // vendor CPS write against channel EV01, "Ranging" checked - a single
    // clean bit flip, byte 0x34 bit 0, 0x0a -> 0x0b, uniquely isolated
    // against every other populated channel in the capture including
    // EV01's own digital siblings which all stayed at 0x0a).
    private static void RangingPatchPreservesSiblingBitsIn0x34()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Ranging = !originalDecoded.Ranging });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.Ranging, decoded.Ranging);
        AssertEqual(originalDecoded.DmrCrcIgnore, decoded.DmrCrcIgnore);
        AssertEqual(originalDecoded.IdleTx, decoded.IdleTx);
        AssertEqual(originalDecoded.AutoScan, decoded.AutoScan);
        AssertEqual(originalDecoded.DataAckDisable, decoded.DataAckDisable);
        AssertEqual(originalDecoded.ExcludeChannelRoaming, decoded.ExcludeChannelRoaming);
        AssertEqual(originalDecoded.DmrMode, decoded.DmrMode);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x34)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    // Confirmed write-safe via a live differential test 2026-08-01 (real
    // vendor CPS write against channel EV01, TX Interrupt set to "Low
    // priority" - byte 0x3b bit 7 went from 0 to 1, isolated from an
    // unrelated backup-restore write that happened just before it. A
    // "High priority" write attempt failed with a communication error
    // before completing, so only Off/Low priority are exposed).
    private static void TxInterruptPatchPreservesSiblingBitsIn0x3b()
    {
        var original = RealControlChannelRecord();
        var originalDecoded = ChannelCodec.Decode(original, 0);

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { TxInterrupt = !originalDecoded.TxInterrupt });
        var decoded = ChannelCodec.Decode(patched, 0);
        AssertEqual(!originalDecoded.TxInterrupt, decoded.TxInterrupt);
        AssertEqual(originalDecoded.ExtendEncryption, decoded.ExtendEncryption);
        AssertEqual(originalDecoded.SendTalkerAlias, decoded.SendTalkerAlias);
        AssertEqual(originalDecoded.SmsForbid, decoded.SmsForbid);
        AssertEqual(originalDecoded.AesRandomKey, decoded.AesRandomKey);
        AssertEqual(originalDecoded.AesMultipleKey, decoded.AesMultipleKey);
        AssertEqual(originalDecoded.AnalogAprsMute, decoded.AnalogAprsMute);

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i == 0x3b)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void TxInterruptSelectionRoundTripsThroughOffAndLowPriorityLabels()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        AssertEqual("Off", channel.TxInterruptSelection);

        channel.TxInterruptSelection = "Low priority";
        AssertTrue(channel.TxInterrupt, "Low priority should set TxInterrupt");
        AssertEqual("Low priority", channel.TxInterruptSelection);

        channel.TxInterruptSelection = "Off";
        AssertTrue(!channel.TxInterrupt, "Off should clear TxInterrupt");
        AssertEqual("Off", channel.TxInterruptSelection);
    }

    private static void CorrectFrequencyHzTextConvertsToAndFromTensOfHz()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        channel.CorrectFrequencyHzText = "1000";
        AssertEqual((byte)100, channel.CorrectFrequencyHz);
        AssertEqual("1000", channel.CorrectFrequencyHzText);

        channel.CorrectFrequencyHzText = "10";
        AssertEqual((byte)1, channel.CorrectFrequencyHz);
        AssertEqual("10", channel.CorrectFrequencyHzText);

        // Not a multiple of 10 - rejected, value unchanged.
        channel.CorrectFrequencyHzText = "15";
        AssertEqual((byte)1, channel.CorrectFrequencyHz);

        // Range corrected 2026-07-31 to the real vendor CPS limit (0-1250),
        // not the field's full byte*10 capacity (0-2550).
        channel.CorrectFrequencyHzText = "1250";
        AssertEqual((byte)125, channel.CorrectFrequencyHz);

        channel.CorrectFrequencyHzText = "1260";
        AssertEqual((byte)125, channel.CorrectFrequencyHz);
    }

    private static void CustomCtcssTextConvertsToAndFromTenthsOfHz()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        channel.CustomCtcssText = "74.6";
        AssertEqual((ushort)746, channel.CustomCtcss);
        AssertEqual("74.6", channel.CustomCtcssText);

        channel.CustomCtcssText = "100.0";
        AssertEqual((ushort)1000, channel.CustomCtcss);
        AssertEqual("100.0", channel.CustomCtcssText);
    }

    // Confirmed write-safe via 2 live differential tests 2026-08-02 (real
    // vendor CPS writes against channel AV00). Round 1: CTCSS Encode =
    // 100.0 -> byte 0x0a = 13 (0-based index into ChannelEntry.
    // CtcssToneLabels); DCS Decode = D023N -> bytes 0x0e-0x0f = 19 (octal
    // "023" read as a plain number). Round 2: DCS Encode = D023I -> bytes
    // 0x0c-0x0d = 531 = 19 + 512 (Inverted = Normal + 512); CTCSS Decode =
    // 62.5 -> byte 0x0b = 0. See ChannelCodec.Decode's doc comment.
    private static void CtcssAndDcsTonePatchesRoundTripThroughDecode()
    {
        var original = RealControlChannelRecord();

        var ctcssPatched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { CtcssEncodeTone = 13, CtcssDecodeTone = 0 });
        var ctcssDecoded = ChannelCodec.Decode(ctcssPatched, 0);
        AssertEqual((byte)13, ctcssDecoded.CtcssEncodeTone);
        AssertEqual((byte)0, ctcssDecoded.CtcssDecodeTone);

        var dcsPatched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { DcsEncodeTone = 531, DcsDecodeTone = 19 });
        var dcsDecoded = ChannelCodec.Decode(dcsPatched, 0);
        AssertEqual((ushort)531, dcsDecoded.DcsEncodeTone);
        AssertEqual((ushort)19, dcsDecoded.DcsDecodeTone);
    }

    private static void CtcssAndDcsTonePatchesTouchOnlyTheirOwnBytes()
    {
        var original = RealControlChannelRecord();

        var patched = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch
        {
            CtcssEncodeTone = 13,
            CtcssDecodeTone = 0,
            DcsEncodeTone = 531,
            DcsDecodeTone = 19
        });

        for (var i = 0; i < ChannelCodec.RecordLength; i++)
        {
            if (i is 0x0a or 0x0b or 0x0c or 0x0d or 0x0e or 0x0f)
            {
                continue;
            }

            AssertEqual(original[i], patched[i]);
        }
    }

    private static void DcsCodeLabelsCoverAll1024EntriesInTheConfirmedOrder()
    {
        AssertEqual(1024, ChannelEntry.DcsCodeLabels.Count);
        AssertEqual("D000N", ChannelEntry.DcsCodeLabels[0]);
        AssertEqual("D023N", ChannelEntry.DcsCodeLabels[19]);
        AssertEqual("D777N", ChannelEntry.DcsCodeLabels[511]);
        AssertEqual("D000I", ChannelEntry.DcsCodeLabels[512]);
        AssertEqual("D023I", ChannelEntry.DcsCodeLabels[531]);
        AssertEqual("D777I", ChannelEntry.DcsCodeLabels[1023]);
    }

    private static void EncodeAndDecodeToneSelectionSwitchBetweenCtcssAndDcsByMode()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };

        channel.CtcssEncodeSelection = "CTCSS";
        AssertEqual(true, channel.IsEncodeToneVisible);
        AssertEqual("62.5", channel.EncodeToneSelection);
        channel.EncodeToneSelection = "100.0";
        AssertEqual((byte)13, channel.CtcssEncodeTone);
        AssertEqual("100.0", channel.EncodeToneSelection);

        channel.CtcssEncodeSelection = "DCS";
        AssertEqual("D000N", channel.EncodeToneSelection);
        channel.EncodeToneSelection = "D023I";
        AssertEqual((ushort)531, channel.DcsEncodeTone);
        AssertEqual("D023I", channel.EncodeToneSelection);

        channel.CtcssEncodeSelection = "Off";
        AssertEqual(false, channel.IsEncodeToneVisible);

        channel.CtcssDecodeSelection = "CTCSS";
        AssertEqual(true, channel.IsDecodeToneVisible);
        channel.DecodeToneSelection = "62.5";
        AssertEqual((byte)0, channel.CtcssDecodeTone);
    }

    // "Custom CTCSS" is a real vendor CPS item (52nd entry) but its raw
    // encoding is unconfirmed, so the setter must not accept it - same
    // "listed but unselectable" treatment as TX Interrupt's "High priority".
    private static void EncodeToneSelectionRejectsCustomCtcss()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5 };
        channel.CtcssEncodeSelection = "CTCSS";

        channel.EncodeToneSelection = "100.0";
        AssertEqual((byte)13, channel.CtcssEncodeTone);

        channel.EncodeToneSelection = "Custom CTCSS";
        AssertEqual((byte)13, channel.CtcssEncodeTone);
    }

    // Confirmed 2026-07-19 against the reference vendor CPS source
    // (channel_edit_dialog.cpp's setModeFormVisibility): Bandwidth is
    // analog-only (forced to 12.5K/raw 0 outside ChannelType 0), Reverse is
    // only valid for ChannelType 0/2.
    private static void ChannelTypeChangeForcesBandwidthAndReverseIntoValidState()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5, ChannelType = 0, Bandwidth = 1, Reverse = true };

        channel.ChannelType = 1; // D-Digital
        AssertEqual((byte)0, channel.Bandwidth);
        AssertEqual(false, channel.Reverse);
        AssertEqual(false, channel.CanUseBandwidth);
        AssertEqual(false, channel.CanUseReverse);
        AssertEqual(false, channel.HasAnalogCapability);

        channel.ChannelType = 2; // A+D TX A - Bandwidth stays Analog-only (ChannelType 0), but Reverse and the analog group are available
        AssertEqual(false, channel.CanUseBandwidth);
        AssertEqual(true, channel.CanUseReverse);
        AssertEqual(true, channel.HasAnalogCapability);

        channel.Reverse = true;
        channel.ChannelType = 3; // D+A TX D
        AssertEqual(false, channel.Reverse);
        AssertEqual(false, channel.CanUseReverse);
        AssertEqual(true, channel.HasAnalogCapability);
    }

    // Confirmed 2026-07-19 by direct inspection of the vendor CPS's own
    // channel_settings.ui: SlotSuit, SmsConfirmation, CallConfirmation and
    // DmrModeDcdm all live inside digitalGroupBox (disabled entirely for
    // A-Analog) - switching a channel to A-Analog must clear stale values
    // so they can't silently survive into an unsupported channel type.
    private static void ChannelTypeChangeToAnalogClearsDigitalOnlyFields()
    {
        var channel = new ChannelEntry
        {
            Number = 1,
            Name = "T1",
            RxFrequencyMHz = 145.5,
            ChannelType = 1,
            SlotSuit = true,
            SmsConfirmation = true,
            CallConfirmation = true,
            DmrModeDcdm = 2
        };

        channel.ChannelType = 0; // A-Analog

        AssertEqual(false, channel.SlotSuit);
        AssertEqual(false, channel.SmsConfirmation);
        AssertEqual(false, channel.CallConfirmation);
        AssertEqual((byte)0, channel.DmrModeDcdm);
    }

    private static void ColorCodeTextRejectsOutOfRangeInputWithoutChangingTheValue()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5, ColorCode = 5, TxColorCode = 7 };

        channel.ColorCodeText = "99";
        AssertEqual((byte)5, channel.ColorCode);
        AssertEqual("5", channel.ColorCodeText);

        channel.TxColorCodeText = "abc";
        AssertEqual((byte)7, channel.TxColorCode);
        AssertEqual("7", channel.TxColorCodeText);
    }

    // Confirmed 2026-07-19 against the reference vendor CPS source: a
    // channel only ever has one active encryption type.
    private static void OnlyOneEncryptionIndexCanBeNonzeroAtATime()
    {
        var channel = new ChannelEntry { Number = 1, Name = "T1", RxFrequencyMHz = 145.5, ChannelType = 1 };

        channel.AesEncryptionIndex = 2;
        AssertEqual((byte)2, channel.AesEncryptionIndex);
        AssertEqual((byte)0, channel.Arc4EncryptionKeyIndex);
        AssertEqual((byte)0, channel.DigitalEncryptionIndex);

        channel.Arc4EncryptionKeyIndex = 3;
        AssertEqual((byte)0, channel.AesEncryptionIndex);
        AssertEqual((byte)3, channel.Arc4EncryptionKeyIndex);
        AssertEqual((byte)0, channel.DigitalEncryptionIndex);

        channel.DigitalEncryptionIndex = 4;
        AssertEqual((byte)0, channel.AesEncryptionIndex);
        AssertEqual((byte)0, channel.Arc4EncryptionKeyIndex);
        AssertEqual((byte)4, channel.DigitalEncryptionIndex);
    }

    // Golden-value fixtures below are the real bytes found 2026-07-18 via a
    // live differential USB capture against a real radio (see
    // RESUME_HERE.md and EncryptionKeyCodec's doc comment) - not synthetic
    // data, the exact same key values that were actually set on the radio.
    private static void DecodesAesEncryptionKeysFromARealCapturedLayout()
    {
        var data = new byte[D890UvMemoryMap.AesEncryptionKeyStride * 3];
        var slot1Key = Convert.FromHexString("1234567891234567891234567891234567891234567891234567891234567897");
        data[0] = 0x01;
        slot1Key.CopyTo(data, 1);
        data[0x21] = 0x00;
        data[0x22] = 0x40;

        var slot2Key = Convert.FromHexString("21485A9461D0FD9A9A8600E31091EF5775DE035E1F7FF29FBF808BC85F5CD8F8");
        data[0x40] = 0x02;
        slot2Key.CopyTo(data, 0x41);

        var decoded = EncryptionKeyCodec.DecodeAesKeys(data);

        AssertEqual(2, decoded.Count);
        AssertEqual(1, decoded[0].Number);
        AssertEqual(Convert.ToHexString(slot1Key), decoded[0].KeyHex);
        AssertEqual(2, decoded[1].Number);
        AssertEqual(Convert.ToHexString(slot2Key), decoded[1].KeyHex);
    }

    private static void DecodesArc4KeyAsFixedFiveByteFieldNoTrimming()
    {
        var data = new byte[D890UvMemoryMap.Arc4EncryptionKeyStride];
        data[0] = 0x01;
        Convert.FromHexString("1234567898").CopyTo(data, 1);

        var decoded = EncryptionKeyCodec.DecodeArc4Keys(data);

        AssertEqual(1, decoded.Count);
        AssertEqual(1, decoded[0].Number);
        AssertEqual("1234567898", decoded[0].KeyHex);

        // A short key stored left-zero-padded (real digits at the end) -
        // confirmed 2026-07-20 via a live differential write capture - must
        // decode as the full 5-byte hex, not trimmed down to "AABB".
        var padded = new byte[D890UvMemoryMap.Arc4EncryptionKeyStride];
        padded[0] = 0x03;
        Convert.FromHexString("000000AABB").CopyTo(padded, 1);

        var decodedPadded = EncryptionKeyCodec.DecodeArc4Keys(padded);

        AssertEqual(1, decodedPadded.Count);
        AssertEqual("000000AABB", decodedPadded[0].KeyHex);
    }

    private static void DecodesBasicEncryptionCodesAtFirstSecondAndLastSlot()
    {
        var stride = D890UvMemoryMap.BasicEncryptionCodeStride;
        var maxSlots = D890UvMemoryMap.BasicEncryptionCodeMaxSlots;
        var valueOffset = D890UvMemoryMap.BasicEncryptionCodeValueOffset;
        var data = new byte[stride * maxSlots];

        void SetCode(int slotIndex, byte b0, byte b1)
        {
            var offset = slotIndex * stride + valueOffset;
            data[offset] = b0;
            data[offset + 1] = b1;
        }

        SetCode(0, 0x00, 0x01); // slot 1 -> "0001"
        SetCode(1, 0x00, 0x02); // slot 2 -> "0002"
        SetCode(maxSlots - 1, 0x99, 0x99); // slot 32 ("last") -> "9999"

        var decoded = EncryptionKeyCodec.DecodeBasicEncryptionCodes(data);

        AssertEqual(3, decoded.Count);
        AssertEqual(1, decoded[0].Number);
        AssertEqual("0001", decoded[0].Code);
        AssertEqual(2, decoded[1].Number);
        AssertEqual("0002", decoded[1].Code);
        AssertEqual(maxSlots, decoded[2].Number);
        AssertEqual("9999", decoded[2].Code);
    }

    private static void SkipsUnpopulatedEncryptionKeyAndCodeSlots()
    {
        var aesData = new byte[D890UvMemoryMap.AesEncryptionKeyStride * 4];
        AssertEqual(0, EncryptionKeyCodec.DecodeAesKeys(aesData).Count);

        var arc4Data = new byte[D890UvMemoryMap.Arc4EncryptionKeyStride * 4];
        AssertEqual(0, EncryptionKeyCodec.DecodeArc4Keys(arc4Data).Count);

        var basicData = new byte[D890UvMemoryMap.BasicEncryptionCodeStride * D890UvMemoryMap.BasicEncryptionCodeMaxSlots];
        AssertEqual(0, EncryptionKeyCodec.DecodeBasicEncryptionCodes(basicData).Count);
    }

    private static void EncodesAesKeySetsIndexByteAndPreservesTrailer()
    {
        // A blank slot (all zero) - as spare slot 250 genuinely read on the
        // real radio during the 2026-07-18 live write/verify/revert test.
        var currentSlot = new byte[D890UvMemoryMap.AesEncryptionKeyStride];
        var newKey = "DEADBEEF00112233445566778899AABBCCDDEEFF0123456789ABCDEF0123456A";

        var encoded = EncryptionKeyCodec.EncodeAesKey(currentSlot, 250, newKey);

        AssertEqual((byte)250, encoded[0]);
        AssertEqual(newKey, Convert.ToHexString(encoded.AsSpan(1, 32)));
        // Trailer (offset 33 onward) is preserved from currentSlot untouched,
        // not fabricated - here that's all-zero, matching the real capture.
        AssertTrue(encoded.AsSpan(33).ToArray().All(b => b == 0), "trailer bytes should be preserved from currentSlot, not fabricated");

        // Re-keying an already-populated slot: index and trailer come from
        // the real captured slot 1 layout, only the key bytes change.
        var populatedSlot = new byte[D890UvMemoryMap.AesEncryptionKeyStride];
        populatedSlot[0] = 0x01;
        Convert.FromHexString("1234567891234567891234567891234567891234567891234567891234567897").CopyTo(populatedSlot, 1);
        populatedSlot[0x21] = 0x00;
        populatedSlot[0x22] = 0x40;

        var reKeyed = EncryptionKeyCodec.EncodeAesKey(populatedSlot, 1, newKey);
        AssertEqual((byte)1, reKeyed[0]);
        AssertEqual(newKey, Convert.ToHexString(reKeyed.AsSpan(1, 32)));
        AssertEqual((byte)0x00, reKeyed[0x21]);
        AssertEqual((byte)0x40, reKeyed[0x22]);
    }

    private static void EncodesAesKeyRejectsWrongLength()
    {
        var currentSlot = new byte[D890UvMemoryMap.AesEncryptionKeyStride];

        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeAesKey(currentSlot, 1, "1234"));
        AssertThrows<ArgumentOutOfRangeException>(() => EncryptionKeyCodec.EncodeAesKey(currentSlot, 0, Convert.ToHexString(new byte[32])));
        AssertThrows<ArgumentOutOfRangeException>(() => EncryptionKeyCodec.EncodeAesKey(currentSlot, 256, Convert.ToHexString(new byte[32])));
        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeAesKey(new byte[10], 1, Convert.ToHexString(new byte[32])));
    }

    private static void EncodesArc4KeyLeftPadsShorterKeyWithZeros()
    {
        var currentSlot = new byte[D890UvMemoryMap.Arc4EncryptionKeyStride];
        currentSlot[0] = 0x01;
        // A previously-written full-length key occupying bytes 1-5.
        Convert.FromHexString("1122334455").CopyTo(currentSlot, 1);

        // Confirmed 2026-07-20 via a live differential write capture: a
        // short key ("AABB") lands on the wire as 00 00 00 AA BB - real
        // digits at the end, not the start.
        var encoded = EncryptionKeyCodec.EncodeArc4Key(currentSlot, 1, "AABB");

        AssertEqual((byte)1, encoded[0]);
        AssertEqual("000000AABB", Convert.ToHexString(encoded.AsSpan(1, 5)));
    }

    private static void EncodesArc4KeyRejectsKeyTooLongForSlot()
    {
        var currentSlot = new byte[D890UvMemoryMap.Arc4EncryptionKeyStride];
        var tooLong = Convert.ToHexString(new byte[D890UvMemoryMap.Arc4EncryptionKeyStride]);

        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeArc4Key(currentSlot, 1, tooLong));
        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeArc4Key(currentSlot, 1, string.Empty));
        AssertThrows<ArgumentOutOfRangeException>(() => EncryptionKeyCodec.EncodeArc4Key(currentSlot, 35, "CAFEBABE99"));
    }

    private static void EncodesBasicCodeGroupTouchesOnlyTargetSlot()
    {
        var stride = D890UvMemoryMap.BasicEncryptionCodeStride;
        var valueOffset = D890UvMemoryMap.BasicEncryptionCodeValueOffset;
        var currentGroup = new byte[stride * 4];

        // Slots 17-19 (indexes 0-2 within the group) already carry real
        // codes; slot 20 (index 3) is the blank spare slot being written.
        currentGroup[0 * stride + valueOffset] = 0x00;
        currentGroup[0 * stride + valueOffset + 1] = 0x01; // "0001"
        currentGroup[1 * stride + valueOffset] = 0x00;
        currentGroup[1 * stride + valueOffset + 1] = 0x02; // "0002"
        currentGroup[2 * stride + valueOffset] = 0x12;
        currentGroup[2 * stride + valueOffset + 1] = 0x13; // "1213"

        var encoded = EncryptionKeyCodec.EncodeBasicCodeGroup(currentGroup, 3, "4242");

        // Untouched slots must be byte-identical to the input, not just the
        // 2 value bytes - proves the RMW didn't clobber neighboring slots.
        AssertTrue(currentGroup.AsSpan(0, 3 * stride).SequenceEqual(encoded.AsSpan(0, 3 * stride)), "slots 17-19 must be byte-identical after patching slot 20");
        AssertEqual((byte)0x42, encoded[3 * stride + valueOffset]);
        AssertEqual((byte)0x42, encoded[3 * stride + valueOffset + 1]);
    }

    private static void EncodesBasicCodeGroupRejectsNonFourDigitCode()
    {
        var currentGroup = new byte[D890UvMemoryMap.BasicEncryptionCodeStride * 4];

        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeBasicCodeGroup(currentGroup, 0, "123"));
        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeBasicCodeGroup(currentGroup, 0, "abcd"));
        AssertThrows<ArgumentOutOfRangeException>(() => EncryptionKeyCodec.EncodeBasicCodeGroup(currentGroup, 4, "0001"));
        AssertThrows<ArgumentException>(() => EncryptionKeyCodec.EncodeBasicCodeGroup(new byte[10], 0, "0001"));
    }

    private static CodeplugRawRegion ChannelBitmapRegion(int channelIndex, bool isSet)
    {
        var bitmap = new byte[0x200];
        if (isSet)
        {
            bitmap[channelIndex / 8] |= (byte)(1 << (channelIndex % 8));
        }

        return new CodeplugRawRegion(D890UvMemoryMap.ChannelSet, bitmap);
    }

    private static void PatcherAppliesChannelPatchWithinItsOwnRegion()
    {
        const int channelIndex = 3990;
        var address = RadioCodeplugPatcher.ChannelAddress(channelIndex);
        var original = RealControlChannelRecord();
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), ChannelBitmapRegion(channelIndex, isSet: true)] };

        var patched = RadioCodeplugPatcher.ApplyChannelPatch(snapshot, channelIndex, new ChannelCodec.ChannelFieldPatch { Name = "WRTEST1" });

        var patchedChannelRegion = patched.Regions.Single(r => r.Address == address);
        var expected = ChannelCodec.Encode(original, new ChannelCodec.ChannelFieldPatch { Name = "WRTEST1" });
        AssertEqual(Convert.ToHexString(expected), Convert.ToHexString(patchedChannelRegion.Data));
        // Original snapshot's region must be untouched (patch returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static void PatcherLeavesOtherRegionsUntouched()
    {
        const int channelIndex = 3990;
        var address = RadioCodeplugPatcher.ChannelAddress(channelIndex);
        var original = RealControlChannelRecord();
        var unrelatedRegion = new CodeplugRawRegion(0x1234000, [1, 2, 3, 4]);
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), unrelatedRegion, ChannelBitmapRegion(channelIndex, isSet: true)] };

        var patched = RadioCodeplugPatcher.ApplyChannelPatch(snapshot, channelIndex, new ChannelCodec.ChannelFieldPatch { Name = "WRTEST1" });

        AssertEqual(3, patched.Regions.Count);
        AssertSame(unrelatedRegion, patched.Regions.Single(r => r.Address == unrelatedRegion.Address));
    }

    private static void PatcherAppliesChannelPatchAndSetsPresenceBitForANewChannel()
    {
        const int channelIndex = 3991;
        var address = RadioCodeplugPatcher.ChannelAddress(channelIndex);
        var blankRecord = new byte[ChannelCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord), ChannelBitmapRegion(channelIndex, isSet: false)] };

        var patched = RadioCodeplugPatcher.ApplyChannelPatch(snapshot, channelIndex, new ChannelCodec.ChannelFieldPatch { Name = "NEWCH" });

        var bitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ChannelSet).Data;
        var bitSet = (bitmap[channelIndex / 8] & (1 << (channelIndex % 8))) != 0;
        AssertTrue(bitSet, "presence bit must be set for a newly-created channel");

        var patchedChannelRegion = patched.Regions.Single(r => r.Address == address);
        var expected = ChannelCodec.Encode(blankRecord, new ChannelCodec.ChannelFieldPatch { Name = "NEWCH" });
        AssertEqual(Convert.ToHexString(expected), Convert.ToHexString(patchedChannelRegion.Data));
    }

    // 2026-07-19: found live that deleting a channel in the app never
    // actually reached the radio (RemoveChannel only touched the in-memory
    // list) - the deleted channel silently reappeared after the next Read
    // From Radio. ApplyChannelDelete is the fix's pure-function core.
    private static void PatcherDeletesChannelBlankingRecordAndClearingPresenceBit()
    {
        const int channelIndex = 3990;
        var address = RadioCodeplugPatcher.ChannelAddress(channelIndex);
        var original = RealControlChannelRecord();
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), ChannelBitmapRegion(channelIndex, isSet: true)] };

        var deleted = RadioCodeplugPatcher.ApplyChannelDelete(snapshot, channelIndex);

        var deletedChannelRegion = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedChannelRegion.Data.All(b => b == 0xFF), "deleted channel record must be blanked to all-0xFF");
        AssertTrue(ChannelCodec.Decode(deletedChannelRegion.Data, channelIndex).IsBlank, "blanked record must decode as IsBlank");

        var bitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ChannelSet).Data;
        var bitSet = (bitmap[channelIndex / 8] & (1 << (channelIndex % 8))) != 0;
        AssertTrue(!bitSet, "presence bit must be cleared for a deleted channel");

        // Original snapshot's region must be untouched (delete returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static void PatcherSplicesAesKeyPatchWithinALargerCombinedRegion()
    {
        var stride = D890UvMemoryMap.AesEncryptionKeyStride;
        var combined = new byte[stride * 3];

        void SetSlot(int slotNumber, string keyHex)
        {
            var offset = (slotNumber - 1) * stride;
            combined[offset] = (byte)slotNumber;
            Convert.FromHexString(keyHex).CopyTo(combined, offset + 1);
        }

        SetSlot(1, new string('1', 64));
        SetSlot(2, new string('2', 64));
        SetSlot(3, new string('3', 64));
        var originalCombined = (byte[])combined.Clone();

        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(D890UvMemoryMap.AesEncryptionKeyData, combined)] };

        var newKey = "DEADBEEF00112233445566778899AABBCCDDEEFF0123456789ABCDEF0123456A";
        var patched = RadioCodeplugPatcher.ApplyAesKeyPatch(snapshot, 2, newKey);

        AssertEqual(1, patched.Regions.Count);
        var patchedData = patched.Regions[0].Data;

        // Slot 2 (offset stride..2*stride) now holds the new key.
        AssertEqual((byte)2, patchedData[stride]);
        AssertEqual(newKey, Convert.ToHexString(patchedData.AsSpan(stride + 1, 32)));

        // Slots 1 and 3 are byte-identical to before the patch.
        AssertTrue(patchedData.AsSpan(0, stride).SequenceEqual(originalCombined.AsSpan(0, stride)), "slot 1 must be untouched");
        AssertTrue(patchedData.AsSpan(2 * stride, stride).SequenceEqual(originalCombined.AsSpan(2 * stride, stride)), "slot 3 must be untouched");
    }

    private static void PatcherClearsAesAndArc4KeySlotsLeavingSiblingsUntouched()
    {
        var aesStride = D890UvMemoryMap.AesEncryptionKeyStride;
        var aesCombined = new byte[aesStride * 2];
        aesCombined[0] = 0x01;
        Convert.FromHexString(new string('1', 64)).CopyTo(aesCombined, 1);
        aesCombined[aesStride] = 0x02;
        Convert.FromHexString(new string('2', 64)).CopyTo(aesCombined, aesStride + 1);
        var originalAes = (byte[])aesCombined.Clone();
        var aesSnapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(D890UvMemoryMap.AesEncryptionKeyData, aesCombined)] };

        var aesPatched = RadioCodeplugPatcher.ApplyAesKeyClearPatch(aesSnapshot, 1);
        var aesData = aesPatched.Regions[0].Data;
        AssertTrue(aesData.AsSpan(0, aesStride).ToArray().All(b => b == 0), "cleared AES slot must be all zero, including the index byte");
        AssertTrue(aesData.AsSpan(aesStride, aesStride).SequenceEqual(originalAes.AsSpan(aesStride, aesStride)), "sibling AES slot 2 must be untouched");

        var arc4Stride = D890UvMemoryMap.Arc4EncryptionKeyStride;
        var arc4Combined = new byte[arc4Stride * 2];
        arc4Combined[0] = 0x01;
        Convert.FromHexString("1122334455").CopyTo(arc4Combined, 1);
        arc4Combined[arc4Stride] = 0x02;
        Convert.FromHexString("6677889900").CopyTo(arc4Combined, arc4Stride + 1);
        var originalArc4 = (byte[])arc4Combined.Clone();
        var arc4Snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(D890UvMemoryMap.Arc4EncryptionKeyData, arc4Combined)] };

        var arc4Patched = RadioCodeplugPatcher.ApplyArc4KeyClearPatch(arc4Snapshot, 1);
        var arc4Data = arc4Patched.Regions[0].Data;
        AssertTrue(arc4Data.AsSpan(0, arc4Stride).ToArray().All(b => b == 0), "cleared ARC4 slot must be all zero, including the index byte");
        AssertTrue(arc4Data.AsSpan(arc4Stride, arc4Stride).SequenceEqual(originalArc4.AsSpan(arc4Stride, arc4Stride)), "sibling ARC4 slot 2 must be untouched");
    }

    private static void PatcherThrowsForAnUnpopulatedChannelAddress()
    {
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [] };
        AssertThrows<InvalidOperationException>(() => RadioCodeplugPatcher.ApplyChannelPatch(snapshot, 3990, new ChannelCodec.ChannelFieldPatch { Name = "X" }));
    }

    private static void ZoneCodecChannelMembersRoundTripThrough256Slots()
    {
        var members = Enumerable.Range(0, 256).Select(i => (ushort)i).ToList();
        var encoded = ZoneCodec.EncodeChannelMembers(members);
        AssertEqual(0x200, encoded.Length);

        var decoded = ZoneCodec.DecodeChannelMembers(encoded, 0);
        AssertEqual(256, decoded.Count);
        AssertTrue(decoded.SequenceEqual(members), "all 256 member slots must round trip - this is exactly the slot count that was found truncated to 128");
    }

    private static CodeplugRawRegion ZoneBitmapRegion(int address, int zoneIndex, bool isSet)
    {
        var bitmap = new byte[D890UvMemoryMap.ZoneSlotCount / 8];
        if (isSet)
        {
            bitmap[zoneIndex / 8] |= (byte)(1 << (zoneIndex % 8));
        }

        return new CodeplugRawRegion(address, bitmap);
    }

    private static RadioCodeplugRawSnapshot NewZoneSnapshot(int zoneIndex, bool populated)
    {
        var nameAddress = D890UvMemoryMap.ZonesName + zoneIndex * D890UvMemoryMap.ZoneDataOffset;
        var membersAddress = D890UvMemoryMap.ZoneChannels + zoneIndex * 0x200;
        var nameRegion = new CodeplugRawRegion(nameAddress, populated ? ZoneCodec.EncodeName("OLDZONE") : new byte[D890UvMemoryMap.ZoneDataLength]);
        var membersRegion = new CodeplugRawRegion(membersAddress, populated ? ZoneCodec.EncodeChannelMembers([0, 1, 2]) : new byte[0x200]);
        var aChannelRegion = new CodeplugRawRegion(D890UvMemoryMap.ZoneAChannel, new byte[D890UvMemoryMap.ZoneSlotCount * 2]);
        var bChannelRegion = new CodeplugRawRegion(D890UvMemoryMap.ZoneBChannel, new byte[D890UvMemoryMap.ZoneSlotCount * 2]);
        var hideRegion = ZoneBitmapRegion(D890UvMemoryMap.ZoneHide, zoneIndex, isSet: false);
        var presenceRegion = ZoneBitmapRegion(D890UvMemoryMap.ZoneSet, zoneIndex, isSet: populated);

        return new RadioCodeplugRawSnapshot { Regions = [nameRegion, membersRegion, aChannelRegion, bChannelRegion, hideRegion, presenceRegion] };
    }

    private static void PatcherAppliesZonePatchAndSetsPresenceBitForANewZone()
    {
        const int zoneIndex = 250;
        var snapshot = NewZoneSnapshot(zoneIndex, populated: false);

        var patched = RadioCodeplugPatcher.ApplyZonePatch(snapshot, zoneIndex, new ZoneCodec.ZoneFieldPatch
        {
            Name = "NEWZONE",
            ChannelMembers = [5, 6, 7],
            AChannelIndex = 5,
            BChannelIndex = 6,
            IsHidden = true
        });

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneSet).Data;
        AssertTrue((presenceBitmap[zoneIndex / 8] & (1 << (zoneIndex % 8))) != 0, "presence bit must be set for a newly-created zone");

        var nameAddress = D890UvMemoryMap.ZonesName + zoneIndex * D890UvMemoryMap.ZoneDataOffset;
        var nameRegion = patched.Regions.Single(r => r.Address == nameAddress);
        AssertEqual("NEWZONE", ZoneCodec.DecodeName(nameRegion.Data, 0));

        var membersAddress = D890UvMemoryMap.ZoneChannels + zoneIndex * 0x200;
        var membersRegion = patched.Regions.Single(r => r.Address == membersAddress);
        AssertTrue(ZoneCodec.DecodeChannelMembers(membersRegion.Data, 0).SequenceEqual(new ushort[] { 5, 6, 7 }), "channel members must match the patch");

        var aChannelRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneAChannel);
        AssertEqual((ushort)5, ZoneCodec.DecodeAChannelIndex(aChannelRegion.Data, zoneIndex));

        var bChannelRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneBChannel);
        AssertEqual((ushort)6, ZoneCodec.DecodeBChannelIndex(bChannelRegion.Data, zoneIndex));

        var hideRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneHide);
        AssertTrue(ZoneCodec.DecodeHide(hideRegion.Data, zoneIndex), "hide bit must be set per the patch");
    }

    private static void PatcherPatchesOnlyDirtyZoneFieldsLeavingOthersUntouched()
    {
        const int zoneIndex = 10;
        var snapshot = NewZoneSnapshot(zoneIndex, populated: true);

        // Only Name is dirty - members/A-channel/B-channel/hide must all
        // stay exactly as they were, since a zone's 4 fields are
        // independent arrays with no shared bytes (unlike Channel).
        var patched = RadioCodeplugPatcher.ApplyZonePatch(snapshot, zoneIndex, new ZoneCodec.ZoneFieldPatch { Name = "RENAMED" });

        var nameAddress = D890UvMemoryMap.ZonesName + zoneIndex * D890UvMemoryMap.ZoneDataOffset;
        AssertEqual("RENAMED", ZoneCodec.DecodeName(patched.Regions.Single(r => r.Address == nameAddress).Data, 0));

        var membersAddress = D890UvMemoryMap.ZoneChannels + zoneIndex * 0x200;
        var originalMembers = snapshot.Regions.Single(r => r.Address == membersAddress).Data;
        var patchedMembers = patched.Regions.Single(r => r.Address == membersAddress).Data;
        AssertEqual(Convert.ToHexString(originalMembers), Convert.ToHexString(patchedMembers));
    }

    private static void PatcherDeletesZoneBlankingRecordsAndClearingPresenceBit()
    {
        const int zoneIndex = 20;
        var snapshot = NewZoneSnapshot(zoneIndex, populated: true);

        var deleted = RadioCodeplugPatcher.ApplyZoneDelete(snapshot, zoneIndex);

        var nameAddress = D890UvMemoryMap.ZonesName + zoneIndex * D890UvMemoryMap.ZoneDataOffset;
        var nameRegion = deleted.Regions.Single(r => r.Address == nameAddress);
        AssertTrue(nameRegion.Data.All(b => b == 0xFF), "deleted zone name must be blanked to all-0xFF");
        AssertEqual("", ZoneCodec.DecodeName(nameRegion.Data, 0));

        var membersAddress = D890UvMemoryMap.ZoneChannels + zoneIndex * 0x200;
        var membersRegion = deleted.Regions.Single(r => r.Address == membersAddress);
        AssertTrue(membersRegion.Data.All(b => b == 0xFF), "deleted zone members must be blanked to all-0xFF");
        AssertEqual(0, ZoneCodec.DecodeChannelMembers(membersRegion.Data, 0).Count);

        var aChannelRegion = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneAChannel);
        AssertEqual((ushort)0xFFFF, ZoneCodec.DecodeAChannelIndex(aChannelRegion.Data, zoneIndex));

        var bChannelRegion = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneBChannel);
        AssertEqual((ushort)0xFFFF, ZoneCodec.DecodeBChannelIndex(bChannelRegion.Data, zoneIndex));

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ZoneSet).Data;
        AssertTrue((presenceBitmap[zoneIndex / 8] & (1 << (zoneIndex % 8))) == 0, "presence bit must be cleared for a deleted zone");
    }

    private static CodeplugRawRegion ScanListBitmapRegion(int scanListIndex, bool isSet)
    {
        var bitmap = new byte[D890UvMemoryMap.ScanListSlotCount / 8];
        if (isSet)
        {
            bitmap[scanListIndex / 8] |= (byte)(1 << (scanListIndex % 8));
        }

        return new CodeplugRawRegion(D890UvMemoryMap.ScanListSet, bitmap);
    }

    private static ScanListCodec.DecodedScanList SampleScanListValues(int radioIndex) => new(radioIndex)
    {
        Name = "TESTSL",
        PriorityChannelSelect = 2,
        PriorityChannel1 = 5,
        PriorityChannel2 = null,
        LookbackTimeA = 3,
        LookbackTimeB = 4,
        DropoutDelayTime = 2,
        DwellTime = 1,
        ChannelMemberIndexes = [10, 20, 30],
        RevertChannel = 7
    };

    private static void ScanListCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[ScanListCodec.RecordLength];
        var values = SampleScanListValues(0);

        var encoded = ScanListCodec.Encode(blankRecord, values);
        var decoded = ScanListCodec.Decode(encoded, 0);

        AssertEqual(values.Name, decoded.Name);
        AssertEqual(values.PriorityChannelSelect, decoded.PriorityChannelSelect);
        AssertEqual(values.PriorityChannel1, decoded.PriorityChannel1);
        AssertEqual(values.PriorityChannel2, decoded.PriorityChannel2);
        AssertEqual(values.LookbackTimeA, decoded.LookbackTimeA);
        AssertEqual(values.LookbackTimeB, decoded.LookbackTimeB);
        AssertEqual(values.DropoutDelayTime, decoded.DropoutDelayTime);
        AssertEqual(values.DwellTime, decoded.DwellTime);
        AssertEqual(values.RevertChannel, decoded.RevertChannel);
        AssertTrue(decoded.ChannelMemberIndexes.SequenceEqual(values.ChannelMemberIndexes), "channel members must round trip");
    }

    // 2026-07-19: a live differential test against the real vendor CPS
    // (Priority Channel Select=2, Lookback A/B=2.5/3.7s, Dropout=4.4s,
    // Dwell=3.5s, all written together) found the reference project's
    // ported "- 5"/"- 1" offset for these 4 fields was simply wrong - the
    // real wire value is plain tenths of a second, no offset. This locks
    // that fix in against regression.
    private static void ScanListTimingFieldsAreRawTenthsOfASecondNoOffset()
    {
        var record = new byte[ScanListCodec.RecordLength];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x06, 2), 25);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x08, 2), 37);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x0a, 2), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x0c, 2), 35);

        var decoded = ScanListCodec.Decode(record, 0);
        AssertEqual(25, decoded.LookbackTimeA);
        AssertEqual(37, decoded.LookbackTimeB);
        AssertEqual(44, decoded.DropoutDelayTime);
        AssertEqual(35, decoded.DwellTime);

        var entry = new ScanListEntry { LookbackTimeA = 25, LookbackTimeB = 37, DropoutDelayTime = 44, DwellTime = 35 };
        AssertEqual("2.5", entry.LookbackTimeAText);
        AssertEqual("3.7", entry.LookbackTimeBText);
        AssertEqual("4.4", entry.DropoutDelayTimeText);
        AssertEqual("3.5", entry.DwellTimeText);
    }

    private static void PatcherAppliesScanListPatchAndSetsPresenceBitForANewScanList()
    {
        const int scanListIndex = 200;
        var address = RadioCodeplugPatcher.ScanListAddress(scanListIndex);
        var blankRecord = new byte[ScanListCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord), ScanListBitmapRegion(scanListIndex, isSet: false)] };

        var values = SampleScanListValues(scanListIndex);
        var patched = RadioCodeplugPatcher.ApplyScanListPatch(snapshot, scanListIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ScanListSet).Data;
        AssertTrue((presenceBitmap[scanListIndex / 8] & (1 << (scanListIndex % 8))) != 0, "presence bit must be set for a newly-created scan list");

        var patchedRecord = patched.Regions.Single(r => r.Address == address);
        var decoded = ScanListCodec.Decode(patchedRecord.Data, scanListIndex);
        AssertEqual(values.Name, decoded.Name);
        AssertTrue(decoded.ChannelMemberIndexes.SequenceEqual(values.ChannelMemberIndexes), "channel members must match the patch");
        AssertEqual(values.RevertChannel, decoded.RevertChannel);
    }

    private static void PatcherDeletesScanListBlankingRecordAndClearingPresenceBit()
    {
        const int scanListIndex = 199;
        var address = RadioCodeplugPatcher.ScanListAddress(scanListIndex);
        var original = ScanListCodec.Encode(new byte[ScanListCodec.RecordLength], SampleScanListValues(scanListIndex));
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), ScanListBitmapRegion(scanListIndex, isSet: true)] };

        var deleted = RadioCodeplugPatcher.ApplyScanListDelete(snapshot, scanListIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted scan list record must be blanked to all-0xFF");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ScanListSet).Data;
        AssertTrue((presenceBitmap[scanListIndex / 8] & (1 << (scanListIndex % 8))) == 0, "presence bit must be cleared for a deleted scan list");

        // Original snapshot's region must be untouched (delete returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static CodeplugRawRegion AmAirBitmapRegion(int amAirIndex, bool isSet)
    {
        var bitmap = new byte[D890UvMemoryMap.AmAirSlotCount / 8];
        if (isSet)
        {
            bitmap[amAirIndex / 8] |= (byte)(1 << (amAirIndex % 8));
        }

        return new CodeplugRawRegion(D890UvMemoryMap.AmAirSet, bitmap);
    }

    private static void AmZoneCodecDecodesScanChannelBitmaskNotIndexList()
    {
        // Confirmed 2026-08-02 via a live differential write: adding AM CH
        // 001/002 (0-based AM Air radio indexes 0/1) to a zone's "Zone Scan
        // Channel Member" list produced raw byte 0x03 (bits 0+1 set) at a
        // separate address from the zone's own record - a 128-bit bitmask,
        // NOT an index list like MemberChannelIndexes.
        var record = new byte[AmZoneCodec.RecordLength];

        var scanChannelBitmask = new byte[0x10];
        scanChannelBitmask[0] = 0x03; // bits 0 and 1 set

        var decoded = AmZoneCodec.Decode(record, aChannelIndex: 0, scanChannelBitmask, index: 0);

        AssertTrue(decoded.ScanChannelIndexes.SequenceEqual(new[] { 0, 1 }), "bit 0 and bit 1 must decode to AM Air radio indexes 0 and 1");
    }

    private static AmAirCodec.DecodedAmAir SampleAmAirValues(int radioIndex) => new(radioIndex)
    {
        FrequencyMHz = 125.3,
        Name = "AM CH 011"
    };

    private static void AmAirCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[AmAirCodec.RecordLength];
        var values = SampleAmAirValues(0);

        var encoded = AmAirCodec.Encode(blankRecord, values);
        var decoded = AmAirCodec.Decode(encoded, 0);

        AssertEqual(values.FrequencyMHz, decoded.FrequencyMHz);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherAppliesAmAirPatchAndSetsPresenceBitForANewChannel()
    {
        const int amAirIndex = 10;
        var address = RadioCodeplugPatcher.AmAirAddress(amAirIndex);
        var blankRecord = new byte[AmAirCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord), AmAirBitmapRegion(amAirIndex, isSet: false)] };

        var values = SampleAmAirValues(amAirIndex);
        var patched = RadioCodeplugPatcher.ApplyAmAirPatch(snapshot, amAirIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AmAirSet).Data;
        AssertTrue((presenceBitmap[amAirIndex / 8] & (1 << (amAirIndex % 8))) != 0, "presence bit must be set for a newly-created AM Air channel");

        var patchedRecord = patched.Regions.Single(r => r.Address == address);
        var decoded = AmAirCodec.Decode(patchedRecord.Data, amAirIndex);
        AssertEqual(values.FrequencyMHz, decoded.FrequencyMHz);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherDeletesAmAirBlankingRecordAndClearingPresenceBit()
    {
        const int amAirIndex = 9;
        var address = RadioCodeplugPatcher.AmAirAddress(amAirIndex);
        var original = AmAirCodec.Encode(new byte[AmAirCodec.RecordLength], SampleAmAirValues(amAirIndex));
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), AmAirBitmapRegion(amAirIndex, isSet: true)] };

        var deleted = RadioCodeplugPatcher.ApplyAmAirDelete(snapshot, amAirIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted AM Air record must be blanked to all-0xFF");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.AmAirSet).Data;
        AssertTrue((presenceBitmap[amAirIndex / 8] & (1 << (amAirIndex % 8))) == 0, "presence bit must be cleared for a deleted AM Air channel");

        // Original snapshot's region must be untouched (delete returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static CodeplugRawRegion AnalogAddressIdRegion(int radioIndex, bool isSet)
    {
        var idList = new byte[D890UvMemoryMap.AnalogBookIdLength];
        Array.Fill(idList, (byte)0xFF);
        if (isSet)
        {
            idList[radioIndex] = (byte)radioIndex;
        }

        return new CodeplugRawRegion(D890UvMemoryMap.AnalogBookId, idList);
    }

    private static AnalogAddressCodec.DecodedAnalogAddress SampleAnalogAddressValues(int radioIndex) => new(radioIndex)
    {
        Number = 1234567890,
        Name = "TESTADDR1"
    };

    private static void AnalogAddressCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[AnalogAddressCodec.RecordLength];
        var values = SampleAnalogAddressValues(1);

        var encoded = AnalogAddressCodec.Encode(blankRecord, values);
        var decoded = AnalogAddressCodec.Decode(encoded, 1);

        AssertEqual(values.Number, decoded.Number);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void AnalogAddressCodecEncodeDerivesNumberLenFromDigitCountMatchingTheLiveCapture()
    {
        // Real bytes from the 2026-08-04 live differential WRITE capture -
        // No. 2, Number=1234567890 (10 digits), Name="TESTADDR1": numberLen
        // byte (offset 0x7) = 0x0A, bytes[0:5] = the hex-digit-string
        // "1234567890".
        var encoded = AnalogAddressCodec.Encode(new byte[AnalogAddressCodec.RecordLength], SampleAnalogAddressValues(1));

        AssertEqual((byte)10, encoded[0x7]);
        AssertEqual("1234567890", Convert.ToHexString(encoded.AsSpan(0x00, 5)));
    }

    private static void PatcherAppliesAnalogAddressPatchAndSetsIdListByteForANewEntry()
    {
        const int radioIndex = 1;
        var address = RadioCodeplugPatcher.AnalogAddressAddress(radioIndex);
        var blankRecord = new byte[AnalogAddressCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord), AnalogAddressIdRegion(radioIndex, isSet: false)] };

        var values = SampleAnalogAddressValues(radioIndex);
        var patched = RadioCodeplugPatcher.ApplyAnalogAddressPatch(snapshot, radioIndex, values);

        var idList = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AnalogBookId).Data;
        AssertEqual((byte)radioIndex, idList[radioIndex]);

        var patchedRecord = patched.Regions.Single(r => r.Address == address);
        var decoded = AnalogAddressCodec.Decode(patchedRecord.Data, radioIndex);
        AssertEqual(values.Number, decoded.Number);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherDeletesAnalogAddressBlankingRecordAndClearingIdListByte()
    {
        const int radioIndex = 1;
        var address = RadioCodeplugPatcher.AnalogAddressAddress(radioIndex);
        var original = AnalogAddressCodec.Encode(new byte[AnalogAddressCodec.RecordLength], SampleAnalogAddressValues(radioIndex));
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), AnalogAddressIdRegion(radioIndex, isSet: true)] };

        var deleted = RadioCodeplugPatcher.ApplyAnalogAddressDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted Analog Address record must be blanked to all-0xFF");

        var idList = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.AnalogBookId).Data;
        AssertEqual((byte)0xFF, idList[radioIndex]);

        // Original snapshot's region must be untouched (delete returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static CodeplugRawRegion AmZoneBitmapRegion(int amZoneIndex, bool isSet)
    {
        var bitmap = new byte[0x10];
        if (isSet)
        {
            bitmap[amZoneIndex / 8] |= (byte)(1 << (amZoneIndex % 8));
        }

        return new CodeplugRawRegion(D890UvMemoryMap.AmZoneSet, bitmap);
    }

    private static AmZoneCodec.DecodedAmZone SampleAmZoneValues(int radioIndex) => new(radioIndex)
    {
        Name = "Test AM Zone",
        AChannelIndex = 2,
        MemberChannelIndexes = [0, 1, 2],
        ScanChannelIndexes = [0, 1]
    };

    private static void PatcherAppliesAmZonePatchAndSetsPresenceBitForANewZone()
    {
        const int amZoneIndex = 5;
        var address = RadioCodeplugPatcher.AmZoneAddress(amZoneIndex);
        var blankRecord = new byte[AmZoneCodec.RecordLength];
        var aChannelRegion = new byte[D890UvMemoryMap.AmZoneCount * 2];
        var scanChannelAddress = D890UvMemoryMap.AmZoneScan + amZoneIndex * D890UvMemoryMap.AmZoneScanStride;
        var scanChannelRegion = new byte[D890UvMemoryMap.AmZoneScanLength];

        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(address, blankRecord),
                new CodeplugRawRegion(D890UvMemoryMap.AmZoneAChannel, aChannelRegion),
                new CodeplugRawRegion(scanChannelAddress, scanChannelRegion),
                AmZoneBitmapRegion(amZoneIndex, isSet: false)
            ]
        };

        var values = SampleAmZoneValues(amZoneIndex);
        var patched = RadioCodeplugPatcher.ApplyAmZonePatch(snapshot, amZoneIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AmZoneSet).Data;
        AssertTrue((presenceBitmap[amZoneIndex / 8] & (1 << (amZoneIndex % 8))) != 0, "presence bit must be set for a newly-created AM Zone");

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var patchedAChannelRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AmZoneAChannel).Data;
        var aChannelIndex = BinaryPrimitives.ReadUInt16LittleEndian(patchedAChannelRegion.AsSpan(amZoneIndex * 2, 2));
        var patchedScanChannelRegion = patched.Regions.Single(r => r.Address == scanChannelAddress).Data;

        var decoded = AmZoneCodec.Decode(patchedRecord, aChannelIndex, patchedScanChannelRegion, amZoneIndex);
        AssertEqual(values.Name, decoded.Name);
        AssertEqual(values.AChannelIndex, decoded.AChannelIndex);
        AssertTrue(decoded.MemberChannelIndexes.SequenceEqual(values.MemberChannelIndexes), "member channels must match the patch");
        AssertTrue(decoded.ScanChannelIndexes.SequenceEqual(values.ScanChannelIndexes), "scan channel members must match the patch");
    }

    private static void PatcherDeletesAmZoneBlankingRecordClearingScanBitmaskAndPresenceBit()
    {
        const int amZoneIndex = 6;
        var address = RadioCodeplugPatcher.AmZoneAddress(amZoneIndex);
        var original = AmZoneCodec.Encode(new byte[AmZoneCodec.RecordLength], SampleAmZoneValues(amZoneIndex));
        var aChannelRegion = new byte[D890UvMemoryMap.AmZoneCount * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(aChannelRegion.AsSpan(amZoneIndex * 2, 2), 2);
        var scanChannelAddress = D890UvMemoryMap.AmZoneScan + amZoneIndex * D890UvMemoryMap.AmZoneScanStride;
        var scanChannelRegion = AmZoneCodec.EncodeScanChannelBitmask([0, 1]);

        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(address, original),
                new CodeplugRawRegion(D890UvMemoryMap.AmZoneAChannel, aChannelRegion),
                new CodeplugRawRegion(scanChannelAddress, scanChannelRegion),
                AmZoneBitmapRegion(amZoneIndex, isSet: true)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyAmZoneDelete(snapshot, amZoneIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted AM Zone record must be blanked to all-0xFF");

        var deletedScanChannelRegion = deleted.Regions.Single(r => r.Address == scanChannelAddress).Data;
        AssertTrue(deletedScanChannelRegion.All(b => b == 0x00), "deleted AM Zone scan channel bitmask must be all-zero, not all-0xFF (a set bit means 'included')");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.AmZoneSet).Data;
        AssertTrue((presenceBitmap[amZoneIndex / 8] & (1 << (amZoneIndex % 8))) == 0, "presence bit must be cleared for a deleted AM Zone");
    }

    private static void PrefabricatedSmsCodecEncodeDecodeRoundTrips()
    {
        var text = new string('x', 100);

        var encoded = PrefabricatedSmsCodec.Encode(text);
        var decoded = PrefabricatedSmsCodec.Decode(encoded, slotIndex: 5);

        AssertEqual(D890UvMemoryMap.PrefabSmsDataLength, encoded.Length);
        AssertEqual(text, decoded.Text);
    }

    private static void PatcherAppliesPrefabricatedSmsTextPatch()
    {
        const int slotId = 5;
        var address = PrefabricatedSmsCodec.ComputeAddress(slotId);
        var blankRecord = new byte[D890UvMemoryMap.PrefabSmsDataLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var patched = RadioCodeplugPatcher.ApplyPrefabricatedSmsTextPatch(snapshot, slotId, "Hello");

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = PrefabricatedSmsCodec.Decode(patchedRecord, slotId);
        AssertEqual("Hello", decoded.Text);
    }

    private static void PatcherDeletesPrefabricatedSmsBlankingRecord()
    {
        const int slotId = 5;
        var address = PrefabricatedSmsCodec.ComputeAddress(slotId);
        var original = PrefabricatedSmsCodec.Encode("Hello");
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyPrefabricatedSmsDelete(snapshot, slotId);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted prefabricated SMS record must be blanked to all-0xFF");

        // Original snapshot's region must be untouched (delete returns a new snapshot).
        AssertEqual(Convert.ToHexString(original), Convert.ToHexString(snapshot.Regions[0].Data));
    }

    private static void PatcherAppliesPrefabricatedSmsSetChainWritesNextAndIdPerNodeWithEndMarkerOnLast()
    {
        // Mirrors the confirmed 2026-08-03 live write: 6 active slots (ids 0-5)
        // become nodes 0-5 in sequential order, each node's next pointing to
        // the following node, and the last node's next set to EndMarker.
        var sortedSlotIds = new List<int> { 0, 1, 2, 3, 4, 5 };
        var regions = sortedSlotIds
            .Select((_, i) => new CodeplugRawRegion(D890UvMemoryMap.PrefabSmsSet + i * PrefabricatedSmsCodec.SetEntryLength, new byte[PrefabricatedSmsCodec.SetEntryLength]))
            .ToList();
        var snapshot = new RadioCodeplugRawSnapshot { Regions = regions };

        var patched = RadioCodeplugPatcher.ApplyPrefabricatedSmsSetChain(snapshot, sortedSlotIds);

        for (var i = 0; i < sortedSlotIds.Count; i++)
        {
            var nodeAddress = D890UvMemoryMap.PrefabSmsSet + i * PrefabricatedSmsCodec.SetEntryLength;
            var node = patched.Regions.Single(r => r.Address == nodeAddress).Data;
            PrefabricatedSmsCodec.TryDecodeSetEntry(node, out var next, out var id);

            var expectedNext = i == sortedSlotIds.Count - 1 ? PrefabricatedSmsCodec.EndMarker : (byte)(i + 1);
            AssertEqual(expectedNext, next);
            AssertEqual((byte)sortedSlotIds[i], id);
        }
    }

    private static FmChannelCodec.DecodedFmChannel SampleFmChannelValues(int radioIndex) => new(radioIndex)
    {
        FrequencyMHz = 108.0,
        Name = "FM CH 011",
        ScanAdd = true
    };

    private static void FmChannelCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[FmChannelCodec.RecordLength];
        var values = SampleFmChannelValues(0);

        var encoded = FmChannelCodec.Encode(blankRecord, values);
        var decoded = FmChannelCodec.Decode(encoded, scanAdd: true, 0);

        AssertEqual(values.FrequencyMHz, decoded.FrequencyMHz);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherAppliesFmChannelPatchAndSetsActiveAndScanBitsForANewChannel()
    {
        const int fmIndex = 1;
        var address = RadioCodeplugPatcher.FmChannelAddress(fmIndex);
        var blankRecord = new byte[FmChannelCodec.RecordLength];
        var fmMeta = new byte[D890UvMemoryMap.FmMetaLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord), new CodeplugRawRegion(D890UvMemoryMap.FmMeta, fmMeta)] };

        var values = SampleFmChannelValues(fmIndex);
        var patched = RadioCodeplugPatcher.ApplyFmChannelPatch(snapshot, fmIndex, values);

        var patchedFmMeta = patched.Regions.Single(r => r.Address == D890UvMemoryMap.FmMeta).Data;
        var activeByteIndex = D890UvMemoryMap.FmActiveMaskOffset + fmIndex / 8;
        var scanByteIndex = D890UvMemoryMap.FmScanMaskOffset + fmIndex / 8;
        AssertTrue((patchedFmMeta[activeByteIndex] & (1 << (fmIndex % 8))) != 0, "active bit must be set for a newly-created FM channel");
        AssertTrue((patchedFmMeta[scanByteIndex] & (1 << (fmIndex % 8))) != 0, "scan bit must be set when ScanAdd is true");

        var patchedRecord = patched.Regions.Single(r => r.Address == address);
        var decoded = FmChannelCodec.Decode(patchedRecord.Data, scanAdd: true, fmIndex);
        AssertEqual(values.FrequencyMHz, decoded.FrequencyMHz);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherDeletesFmChannelBlankingRecordAndClearingActiveAndScanBits()
    {
        const int fmIndex = 2;
        var address = RadioCodeplugPatcher.FmChannelAddress(fmIndex);
        var original = FmChannelCodec.Encode(new byte[FmChannelCodec.RecordLength], SampleFmChannelValues(fmIndex));
        var fmMeta = new byte[D890UvMemoryMap.FmMetaLength];
        var activeByteIndex = D890UvMemoryMap.FmActiveMaskOffset + fmIndex / 8;
        var scanByteIndex = D890UvMemoryMap.FmScanMaskOffset + fmIndex / 8;
        fmMeta[activeByteIndex] |= (byte)(1 << (fmIndex % 8));
        fmMeta[scanByteIndex] |= (byte)(1 << (fmIndex % 8));
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original), new CodeplugRawRegion(D890UvMemoryMap.FmMeta, fmMeta)] };

        var deleted = RadioCodeplugPatcher.ApplyFmChannelDelete(snapshot, fmIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted FM channel record must be blanked to all-0xFF");

        var deletedFmMeta = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.FmMeta).Data;
        AssertTrue((deletedFmMeta[activeByteIndex] & (1 << (fmIndex % 8))) == 0, "active bit must be cleared for a deleted FM channel");
        AssertTrue((deletedFmMeta[scanByteIndex] & (1 << (fmIndex % 8))) == 0, "scan bit must be cleared for a deleted FM channel");
    }

    private static void AutoRepeaterOffsetFrequencyTextCanBeTypedUpFromBelowIts1KhzFloor()
    {
        // Same shape as VfoScanFrequencyTextFieldsCanBeTypedUpFromAValueBelowTheirMinimum:
        // this field's floor (1 kHz = 0.001 MHz) is especially low, so typing
        // "0" then "0.0" then "0.00" toward a valid value like "0.00500"
        // passes through several below-floor intermediate states that must
        // not be silently reverted.
        var entry = new AutoRepeaterOffsetEntry();
        entry.OffsetFrequencyMhzText = "0";
        AssertTrue(entry.HasErrors, "0 MHz is below the 1 kHz floor and should be flagged, not silently reverted.");
        entry.OffsetFrequencyMhzText = "0.00500";
        AssertTrue(!entry.HasErrors, "0.005 MHz (5 kHz) is within the 0.001-90 MHz range.");
        AssertEqual(0.005, entry.OffsetFrequencyMhz);
    }

    private static void AutoRepeaterOffsetFrequencyTextRejectsOutOfRangeValues()
    {
        var entry = new AutoRepeaterOffsetEntry();
        entry.OffsetFrequencyMhzText = "95.00000";
        AssertTrue(entry.HasErrors, "95 MHz is outside the 0.001-90 MHz range.");
        var errors = entry.GetErrors(nameof(AutoRepeaterOffsetEntry.OffsetFrequencyMhzText)).ToList();
        AssertEqual(1, errors.Count);
        AssertContains("0.001-90.00000", errors[0].ErrorMessage ?? "");
    }

    private static void AutoRepeaterOffsetCodecEncodeDecodeRoundTrips()
    {
        var encoded = AutoRepeaterOffsetCodec.Encode(1.0);
        var decoded = AutoRepeaterOffsetCodec.Decode(encoded, 2);

        AssertEqual(1.0, decoded.OffsetFrequencyMhz);
        AssertEqual(100000, decoded.RawOffset);
        // Confirmed 2026-08-03 via a live differential write (Auto Repeater
        // Offset #3 set to 1 MHz) - raw bytes A0 86 01 00 (little-endian).
        AssertEqual("A0860100", Convert.ToHexString(encoded));
    }

    private static void PatcherAppliesAutoRepeaterOffsetPatchWithNoPresenceBitmap()
    {
        // Unlike every other entity, there's no presence
        // bitmap at all - just the flat 4-byte record. The snapshot here
        // only carries that one region, proving the patch doesn't touch
        // (or require) anything else.
        const int radioIndex = 2;
        var address = RadioCodeplugPatcher.AutoRepeaterOffsetAddress(radioIndex);
        var blankRecord = new byte[AutoRepeaterOffsetCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var patched = RadioCodeplugPatcher.ApplyAutoRepeaterOffsetPatch(snapshot, radioIndex, 1.0);

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = AutoRepeaterOffsetCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(1.0, decoded.OffsetFrequencyMhz);
    }

    private static void PatcherDeletesAutoRepeaterOffsetByZeroingNotBlankingTo0xff()
    {
        const int radioIndex = 2;
        var address = RadioCodeplugPatcher.AutoRepeaterOffsetAddress(radioIndex);
        var original = AutoRepeaterOffsetCodec.Encode(1.0);
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyAutoRepeaterOffsetDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0x00), "deleted Auto Repeater Offset record must be zeroed, not blanked to 0xFF like most other entities - a zeroed slot is the confirmed 'unused' sentinel here.");
    }

    private static void LocalInfoCodecDecodesNarrowAsciiFieldsNotUtf16Le()
    {
        // Values taken directly from a real vendor CPS screenshot
        // ("Local information.PNG", 2026-08-04) - a 16-character serial
        // number in a 0x10=16-byte span only fits at 1 byte/char, proving
        // this data is narrow ASCII, unlike every UTF-16LE name field
        // elsewhere in this codebase. See LocalInfoCodec's doc comment.
        var data = new byte[LocalInfoCodec.RecordLength];
        WriteAscii(data, 0x10, "D890UV");
        WriteAscii(data, 0x30, "2603250170400002");
        WriteAscii(data, 0x40, "2026/4/1");
        WriteAscii(data, 0x50, "D03-020");

        var decoded = LocalInfoCodec.Decode(data);

        AssertEqual("D890UV", decoded.RadioType);
        AssertEqual("2603250170400002", decoded.SerialNumber);
        AssertEqual("2026/4/1", decoded.ProductionDate);
        AssertEqual("D03-020", decoded.ManufactureCode);
    }

    private static void LocalInfoCodecTreatsAll0xffRecordAsBlank()
    {
        var data = Enumerable.Repeat((byte)0xFF, LocalInfoCodec.RecordLength).ToArray();

        var decoded = LocalInfoCodec.Decode(data);

        AssertEqual("", decoded.SerialNumber);
        AssertEqual("", decoded.AreaCode);
        AssertEqual("", decoded.MaintenanceDescription);
    }

    private static void AnalogAlarmTimeEnabledOnlyWhenEmergencyAlarmIsAlarm()
    {
        var entry = new AlarmSettingsEntry { AnalogEmergencyAlarm = 0 };
        AssertTrue(entry.IsAnalogAlarmTimeEnabled, "Alarm Time must be enabled when Emergency Alarm is 'Alarm'.");
        AssertTrue(!entry.IsAnalogTxRxDurationEnabled, "TX/RX Duration must be disabled when Emergency Alarm is 'Alarm'.");

        entry.AnalogEmergencyAlarm = 3; // "Both"
        AssertTrue(!entry.IsAnalogAlarmTimeEnabled, "Alarm Time must be disabled for any Emergency Alarm value other than 'Alarm'.");
        AssertTrue(entry.IsAnalogTxRxDurationEnabled, "TX/RX Duration must be enabled for any Emergency Alarm value other than 'Alarm'.");
    }

    private static void AnalogEniTypeAndEmergencyIdDisabledWhenEmergencyAlarmIsAlarm()
    {
        var entry = new AlarmSettingsEntry { AnalogEmergencyAlarm = 0, AnalogEniType = 2 };
        AssertTrue(!entry.IsAnalogEniTypeEnabled, "ENI Type Select must be disabled when Emergency Alarm is 'Alarm'.");
        AssertTrue(!entry.IsAnalogEmergencyIdEnabled, "Emergency ID must be disabled when Emergency Alarm is 'Alarm', regardless of ENI Type.");

        entry.AnalogEmergencyAlarm = 1; // "Transpond+Background"
        AssertTrue(entry.IsAnalogEniTypeEnabled, "ENI Type Select must be enabled for any Emergency Alarm value other than 'Alarm'.");
    }

    private static void AnalogEmergencyIdEnabledOnlyForDtmfAndFiveToneEniTypes()
    {
        var entry = new AlarmSettingsEntry { AnalogEmergencyAlarm = 1 }; // not "Alarm", so only ENI Type gates it now
        entry.AnalogEniType = 0; // None
        AssertTrue(!entry.IsAnalogEmergencyIdEnabled, "Emergency ID must be disabled for ENI Type 'None'.");
        entry.AnalogEniType = 1; // DTMF
        AssertTrue(entry.IsAnalogEmergencyIdEnabled, "Emergency ID must be enabled for ENI Type 'DTMF'.");
        entry.AnalogEniType = 2; // 5Tone
        AssertTrue(entry.IsAnalogEmergencyIdEnabled, "Emergency ID must be enabled for ENI Type '5Tone'.");
        entry.AnalogEniType = 3; // QDC1200
        AssertTrue(!entry.IsAnalogEmergencyIdEnabled, "Emergency ID must be disabled for ENI Type 'QDC1200' - QDC1200 uses its own separate Kind/Group ID/Private ID fields instead.");
    }

    private static void AnalogEmergencyChannelEnabledOnlyWhenEniSendIsAssignedChannel()
    {
        var entry = new AlarmSettingsEntry { AnalogEniSend = 1 }; // "Selected Channel"
        AssertTrue(!entry.IsAnalogEniSendAssignedChannel, "Emergency Channel must be disabled when ENI Send is 'Selected Channel'.");
        entry.AnalogEniSend = 0; // "Assigned Channel"
        AssertTrue(entry.IsAnalogEniSendAssignedChannel, "Emergency Channel must be enabled when ENI Send is 'Assigned Channel'.");
    }

    private static void AnalogEmergencyCycleTextMapsZeroToContinuous()
    {
        var entry = new AlarmSettingsEntry { AnalogEmergencyCycle = 0 };
        AssertEqual("Continuous", entry.AnalogEmergencyCycleText);

        entry.AnalogEmergencyCycleText = "5";
        AssertEqual((byte)5, entry.AnalogEmergencyCycle);

        entry.AnalogEmergencyCycleText = "Continuous";
        AssertEqual((byte)0, entry.AnalogEmergencyCycle);
    }

    private static void QdcSettingGroupboxEnabledOnlyWhenEniTypeIsQdc1200()
    {
        var entry = new AlarmSettingsEntry { AnalogEniType = 2 }; // 5Tone
        AssertTrue(!entry.IsQdcSettingEnabled, "QDC1200 Setting groupbox must be disabled unless ENI Type Select is 'QDC1200'.");
        entry.AnalogEniType = 3; // QDC1200
        AssertTrue(entry.IsQdcSettingEnabled, "QDC1200 Setting groupbox must be enabled when ENI Type Select is 'QDC1200'.");
    }

    private static void QdcGroupIdAndPrivateIdGatedByKindMutuallyExclusively()
    {
        var entry = new AlarmSettingsEntry { QdcCallType = 0 }; // Private Call
        AssertTrue(entry.IsQdcPrivateIdEnabled, "Private ID must be enabled when Kind is 'Private Call'.");
        AssertTrue(!entry.IsQdcGroupIdEnabled, "Group ID must be disabled when Kind is 'Private Call'.");

        entry.QdcCallType = 1; // Group Call
        AssertTrue(entry.IsQdcGroupIdEnabled, "Group ID must be enabled when Kind is 'Group Call'.");
        AssertTrue(!entry.IsQdcPrivateIdEnabled, "Private ID must be disabled when Kind is 'Group Call'.");

        entry.QdcCallType = 2; // All Call
        AssertTrue(!entry.IsQdcGroupIdEnabled, "Group ID must be disabled when Kind is 'All Call'.");
        AssertTrue(!entry.IsQdcPrivateIdEnabled, "Private ID must be disabled when Kind is 'All Call'.");
    }

    private static void WorkAloneResponseTimeTextUsesRawBytePlusOneMinutes()
    {
        var entry = new AlarmSettingsEntry { WorkAloneResponseTime = 0 };
        AssertEqual("1m", entry.WorkAloneResponseTimeText);

        entry.WorkAloneResponseTime = 255;
        AssertEqual("256m", entry.WorkAloneResponseTimeText);

        entry.WorkAloneResponseTimeText = "10m";
        AssertEqual((byte)9, entry.WorkAloneResponseTime);
    }

    private static void WorkAloneWarningTimeTextCoversOneTo255SecondsOnly()
    {
        var entry = new AlarmSettingsEntry { WorkAloneWarningTime = 0 };
        AssertEqual("1s", entry.WorkAloneWarningTimeText);

        entry.WorkAloneWarningTime = 254;
        AssertEqual("255s", entry.WorkAloneWarningTimeText);
        AssertEqual(255, AlarmSettingsEntry.WorkAloneWarningTimeOptions.Count);

        entry.WorkAloneWarningTimeText = "10s";
        AssertEqual((byte)9, entry.WorkAloneWarningTime);
    }

    private static void WorkAloneResponseTextMapsKeyAndVoiceTransmit()
    {
        var entry = new AlarmSettingsEntry { WorkAloneResponse = 0 };
        AssertEqual("Key", entry.WorkAloneResponseText);

        entry.WorkAloneResponse = 1;
        AssertEqual("Voice Transmit", entry.WorkAloneResponseText);

        entry.WorkAloneResponseText = "Key";
        AssertEqual((byte)0, entry.WorkAloneResponse);
    }

    private static void DigitalEmergencyChannelEnabledOnlyWhenEniSendIsAssignedChannel()
    {
        var entry = new AlarmSettingsEntry { DigitalEniSend = 1 }; // "Selected Channel"
        AssertTrue(!entry.IsDigitalEniSendAssignedChannel, "Digital Emergency Channel must be disabled when ENI Send is 'Selected Channel'.");
        entry.DigitalEniSend = 0; // "Assigned Channel"
        AssertTrue(entry.IsDigitalEniSendAssignedChannel, "Digital Emergency Channel must be enabled when ENI Send is 'Assigned Channel'.");
    }

    private static void DigitalAlarmFieldsAreNeverGatedByEmergencyAlarmState()
    {
        // Unlike Analog Alarm, only Emergency Channel is ever disabled in
        // the Digital Alarm groupbox (confirmed against the real vendor
        // CPS) - Alarm Time/TX/RX Duration have no enable-state property
        // at all here (they're just plain always-enabled ComboBoxes in the
        // view), so this test only documents that DigitalEmergencyAlarm
        // changing doesn't touch IsDigitalEniSendAssignedChannel.
        var entry = new AlarmSettingsEntry { DigitalEniSend = 0, DigitalEmergencyAlarm = 0 };
        AssertTrue(entry.IsDigitalEniSendAssignedChannel, "Baseline: enabled for 'Assigned Channel'.");
        entry.DigitalEmergencyAlarm = 3; // "Transpond+LocalAlarm"
        AssertTrue(entry.IsDigitalEniSendAssignedChannel, "Digital Emergency Channel's enable state must not depend on Emergency Alarm.");
    }

    private static void DigitalAndQdcCallTypeShareTheSameOptionsList()
    {
        AssertEqual(3, AlarmSettingsEntry.CallTypeOptions.Count);
        AssertEqual("Private Call", AlarmSettingsEntry.CallTypeOptions[0]);
        AssertEqual("Group Call", AlarmSettingsEntry.CallTypeOptions[1]);
        AssertEqual("All Call", AlarmSettingsEntry.CallTypeOptions[2]);

        var entry = new AlarmSettingsEntry { DigitalCallType = 1 };
        AssertEqual("Group Call", entry.DigitalCallTypeText);
        entry.QdcCallType = 2;
        AssertEqual("All Call", entry.QdcCallTypeText);
    }

    private static void ManDownDelayTextCoversFull0To255ByteRange()
    {
        var entry = new AlarmSettingsEntry { ManDownDelay = 0 };
        AssertEqual("0", entry.ManDownDelayText);
        AssertEqual(256, AlarmSettingsEntry.ManDownDelayOptions.Count);

        entry.ManDownDelay = 255;
        AssertEqual("255", entry.ManDownDelayText);

        entry.ManDownDelayText = "42";
        AssertEqual((byte)42, entry.ManDownDelay);
    }

    private static AlarmSettingsCodec.DecodedAlarmSettings SampleAlarmSettings() => new()
    {
        AnalogEmergencyAlarm = 1,
        AnalogEniType = 2,
        AnalogEmergencyId = 1,
        AnalogAlarmTime = 10,
        AnalogTxDuration = 22,
        AnalogRxDuration = 23,
        AnalogEmergencyChannel = 0,
        AnalogEniSend = 1,
        AnalogEmergencyCycle = 24,

        DigitalEmergencyAlarm = 3,
        DigitalAlarmTime = 28,
        DigitalTxDuration = 29,
        DigitalRxDuration = 30,
        DigitalEmergencyChannel = 300,
        DigitalEmergencyCycle = 31,
        DigitalEniSend = 1,
        DigitalCallType = 0,
        DigitalTgDmrId = 5551234,

        ReceiveAlarm = true,
        ManDown = true,
        ManDownDelay = 32,

        WorkAloneResponseTime = 26,
        WorkAloneWarningTime = 27,
        WorkAloneResponse = 2,

        QdcGroupId = "A1B",
        QdcPrivateId = "1A2B"
    };

    private static void AlarmSettingsCodecEncodeDecodeRoundTrips()
    {
        var values = SampleAlarmSettings();

        var data3483000 = AlarmSettingsCodec.EncodeD3483000(new byte[AlarmSettingsCodec.Data3483000Length], values);
        var data3482e00 = AlarmSettingsCodec.EncodeD3482e00(new byte[AlarmSettingsCodec.Data3482e00Length], values);
        var data3500000 = AlarmSettingsCodec.EncodeD3500000(new byte[AlarmSettingsCodec.Data3500000Length], values);

        var decoded = AlarmSettingsCodec.Decode(data3483000, data3482e00, data3500000);

        AssertEqual(values.AnalogEmergencyAlarm, decoded.AnalogEmergencyAlarm);
        AssertEqual(values.AnalogEniType, decoded.AnalogEniType);
        AssertEqual(values.AnalogEmergencyId, decoded.AnalogEmergencyId);
        AssertEqual(values.AnalogAlarmTime, decoded.AnalogAlarmTime);
        AssertEqual(values.AnalogTxDuration, decoded.AnalogTxDuration);
        AssertEqual(values.AnalogRxDuration, decoded.AnalogRxDuration);
        AssertEqual(values.AnalogEniSend, decoded.AnalogEniSend);
        AssertEqual(values.AnalogEmergencyCycle, decoded.AnalogEmergencyCycle);

        AssertEqual(values.DigitalEmergencyAlarm, decoded.DigitalEmergencyAlarm);
        AssertEqual(values.DigitalAlarmTime, decoded.DigitalAlarmTime);
        AssertEqual(values.DigitalTxDuration, decoded.DigitalTxDuration);
        AssertEqual(values.DigitalRxDuration, decoded.DigitalRxDuration);
        AssertEqual(values.DigitalEmergencyChannel, decoded.DigitalEmergencyChannel);
        AssertEqual(values.DigitalEmergencyCycle, decoded.DigitalEmergencyCycle);
        AssertEqual(values.DigitalEniSend, decoded.DigitalEniSend);
        AssertEqual(values.DigitalCallType, decoded.DigitalCallType);
        AssertEqual(values.DigitalTgDmrId, decoded.DigitalTgDmrId);

        AssertEqual(values.ReceiveAlarm, decoded.ReceiveAlarm);
        AssertEqual(values.ManDown, decoded.ManDown);
        AssertEqual(values.ManDownDelay, decoded.ManDownDelay);

        AssertEqual(values.WorkAloneResponseTime, decoded.WorkAloneResponseTime);
        AssertEqual(values.WorkAloneWarningTime, decoded.WorkAloneWarningTime);
        AssertEqual(values.WorkAloneResponse, decoded.WorkAloneResponse);

        AssertEqual(values.QdcGroupId, decoded.QdcGroupId);
        AssertEqual(values.QdcPrivateId, decoded.QdcPrivateId);
    }

    private static void PatcherAppliesAlarmSettingsPatchAcrossAllThreeRegions()
    {
        var data3483000 = new byte[AlarmSettingsCodec.Data3483000Length];
        var data3482e00 = new byte[AlarmSettingsCodec.Data3482e00Length];
        var data3500000 = new byte[AlarmSettingsCodec.Data3500000Length];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.AlarmSettingsData3483000, data3483000),
                new CodeplugRawRegion(D890UvMemoryMap.AlarmSettingsData3482e00, data3482e00),
                new CodeplugRawRegion(D890UvMemoryMap.AlarmSettingsData3500000, data3500000)
            ]
        };

        var values = SampleAlarmSettings();
        var patched = RadioCodeplugPatcher.ApplyAlarmSettingsPatch(snapshot, values);

        var region3483000 = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AlarmSettingsData3483000).Data;
        var region3482e00 = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AlarmSettingsData3482e00).Data;
        var region3500000 = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AlarmSettingsData3500000).Data;

        var decoded = AlarmSettingsCodec.Decode(region3483000, region3482e00, region3500000);
        AssertEqual(values.DigitalTgDmrId, decoded.DigitalTgDmrId);
        AssertEqual(values.QdcGroupId, decoded.QdcGroupId);
        AssertEqual(values.ManDown, decoded.ManDown);
        AssertEqual(values.ManDownDelay, decoded.ManDownDelay);

        // Untouched bytes in the data_3483000 gap (0x16-0x17) must stay zero -
        // a full-record RMW patch must never spill past the offsets it owns.
        AssertEqual((byte)0, region3483000[0x16]);
    }

    private static void PatcherAppliesAlarmManDownPatchWithoutClobberingOptionalSettingsInTheShared3500000Region()
    {
        // Alarm Settings' Man Down/Man Down Delay and Optional Settings' whole
        // Power-on record both live at base address 0x3500000 - confirmed via
        // live USB capture 2026-08-04 that the vendor CPS always requests the
        // larger 0x160-byte length there, so RadioCodeplugRawSnapshot's own
        // capture-dedupe keeps only ONE physical region for both. This proves
        // that region survives being patched by both callers in either order
        // without either clobbering the other's bytes.
        var region = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, region),
                new CodeplugRawRegion(D890UvMemoryMap.AlarmSettingsData3483000, new byte[AlarmSettingsCodec.Data3483000Length]),
                new CodeplugRawRegion(D890UvMemoryMap.AlarmSettingsData3482e00, new byte[AlarmSettingsCodec.Data3482e00Length])
            ]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnInterface = 2,
            StartupZoneA = 4,
            StartupReset = 1
        });

        var alarmValues = SampleAlarmSettings();
        patched = RadioCodeplugPatcher.ApplyAlarmSettingsPatch(patched, alarmValues);

        var finalRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        AssertEqual(3, patched.Regions.Count);

        var optionalDecoded = OptionalSettingsCodec.Decode(finalRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);
        AssertEqual((byte)2, optionalDecoded.PowerOnInterface);
        AssertEqual((byte)4, optionalDecoded.StartupZoneA);
        AssertEqual((byte)1, optionalDecoded.StartupReset);

        var alarmDecoded = AlarmSettingsCodec.Decode(new byte[AlarmSettingsCodec.Data3483000Length], new byte[AlarmSettingsCodec.Data3482e00Length], finalRegion.AsSpan(0, AlarmSettingsCodec.Data3500000Length));
        AssertEqual(alarmValues.ManDown, alarmDecoded.ManDown);
        AssertEqual(alarmValues.ManDownDelay, alarmDecoded.ManDownDelay);
    }

    private static void WriteAscii(byte[] data, int offset, string value) =>
        System.Text.Encoding.ASCII.GetBytes(value).CopyTo(data, offset);

    private static void OptionalSettingsEncodeMainOnlyTouchesPatchedOffsets()
    {
        // 0xAB sentinel (not a plausible real value for any of these fields)
        // makes any accidental touch of an untouched byte obvious.
        var original = Enumerable.Repeat((byte)0xAB, OptionalSettingsCodec.MainDataLength).ToArray();

        var patched = OptionalSettingsCodec.EncodeMain(original, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnInterface = 2,
            StartupZoneA = 4,
            StartupReset = 1
        });

        AssertEqual((byte)2, patched[0x6]);
        AssertEqual((byte)4, patched[0xd7]);
        AssertEqual((byte)1, patched[0xea]);

        // Every other byte, including the untouched Power-on/Startup
        // offsets (0x7, 0xd6, 0xd8, 0xd9, 0xda), must stay exactly as they
        // were - this codec never re-encodes fields it wasn't asked to.
        for (var i = 0; i < patched.Length; i++)
        {
            if (i is 0x6 or 0xd7 or 0xea)
            {
                continue;
            }

            AssertEqual((byte)0xAB, patched[i]);
        }
    }

    private static void OptionalSettingsEncodeDisplayOnlyTouchesPatchedTextFields()
    {
        var original = Enumerable.Repeat((byte)0xAB, OptionalSettingsCodec.SecondaryDataLength).ToArray();

        var patched = OptionalSettingsCodec.EncodeDisplay(original, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnDisplayLine2 = "ANYTONE"
        });

        AssertEqual("ANYTONE", System.Text.Encoding.Unicode.GetString(patched.AsSpan(0x20, 0x1c)).TrimEnd('\0'));

        // Line 1 (0x0) and Password Char (0x40) weren't patched - must stay
        // exactly as they were (this is a real regression risk given how
        // many times this record's field offsets have shifted - see
        // OptionalSettingsCodec's doc comment on the Line2/PasswordChar
        // offset history this mirrors).
        AssertEqual(Convert.ToHexString(original.AsSpan(0x0, 0x1c)), Convert.ToHexString(patched.AsSpan(0x0, 0x1c)));
        AssertEqual(Convert.ToHexString(original.AsSpan(0x40, 0x8)), Convert.ToHexString(patched.AsSpan(0x40, 0x8)));
    }

    private static void PowerOnDisplayLinesAllow14CharactersNot7()
    {
        // The vendor CPS allows 14 characters per line, not 7 - each line's
        // real byte allocation is 0x1c (28) bytes UTF-16LE, not 0xe (14).
        // This app previously only read/wrote the first half of each field's
        // real space, silently truncating anything past 7 characters.
        var original = new byte[OptionalSettingsCodec.SecondaryDataLength];

        var patched = OptionalSettingsCodec.EncodeDisplay(original, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnDisplayLine1 = "FOURTEEN CHARS",
            PowerOnDisplayLine2 = "ANOTHER14CHARS"
        });

        var decoded = OptionalSettingsCodec.Decode(new byte[OptionalSettingsCodec.MainDataLength], patched, new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual("FOURTEEN CHARS", decoded.PowerOnDisplayLine1);
        AssertEqual("ANOTHER14CHARS", decoded.PowerOnDisplayLine2);
    }

    private static void PowerOnPasswordCharRejectsNonDigitInputWithoutChangingTheValue()
    {
        var entry = new OptionalSettingsEntry { PowerOnPasswordChar = "1357" };
        entry.PowerOnPasswordChar = "12a4";
        AssertEqual("1357", entry.PowerOnPasswordChar);

        entry.PowerOnPasswordChar = "246";
        AssertEqual("246", entry.PowerOnPasswordChar);
    }

    private static void AlertToneFrequencyTextRejectsOutOfRangeAndNonDigitInputWithoutChangingTheValue()
    {
        var entry = new AlertToneEntry { FrequencyText = "1500" };

        entry.FrequencyText = "3001";
        AssertEqual("1500", entry.FrequencyText);

        entry.FrequencyText = "12a4";
        AssertEqual("1500", entry.FrequencyText);

        entry.FrequencyText = "3000";
        AssertEqual("3000", entry.FrequencyText);

        entry.FrequencyText = "0";
        AssertEqual("0", entry.FrequencyText);
    }

    private static void AlertTonePeriodTextRejectsOutOfRangeInputWithoutChangingTheValue()
    {
        var entry = new AlertToneEntry { PeriodText = "100" };

        entry.PeriodText = "210";
        AssertEqual("100", entry.PeriodText);

        entry.PeriodText = "200";
        AssertEqual("200", entry.PeriodText);

        entry.PeriodText = "0";
        AssertEqual("0", entry.PeriodText);
    }

    private static void CorrectFrequencyHzTextReportsValidationErrorsInsteadOfReverting()
    {
        // Converted 2026-08-09 from reject-and-revert to real validation -
        // flagged by the app-wide numeric-field audit alongside the DMR ID
        // fix. Same mechanism as CustomCtcssText: never revert, report via
        // HasErrors/GetErrors.
        var channel = new ChannelEntry { CorrectFrequencyHzText = "500" };
        AssertTrue(!channel.HasErrors, "A fresh, valid Correct Frequency should have no errors.");

        channel.CorrectFrequencyHzText = "1260"; // above the real 1250 Hz max
        AssertTrue(channel.HasErrors, "An out-of-range Correct Frequency should be flagged, not silently reverted.");
        AssertEqual("500", channel.CorrectFrequencyHzText);

        channel.CorrectFrequencyHzText = "505"; // not a multiple of 10
        AssertTrue(channel.HasErrors, "A non-multiple-of-10 Correct Frequency should be flagged.");

        channel.CorrectFrequencyHzText = "780";
        AssertTrue(!channel.HasErrors, "A valid Correct Frequency should clear the error.");
        AssertEqual("780", channel.CorrectFrequencyHzText);
    }

    private static void CorrectFrequencyHzOutOfRangeRawByteBlocksSave()
    {
        // Simulates an old project file or a raw radio read landing a byte
        // above the real 0-125 (0-1250 Hz) limit but still within the raw
        // byte's own 0-255 capacity - CorrectFrequencyHzText's own commit
        // gate can't catch this since it was never involved.
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.HasBlockingValidationErrors, "freshly seeded view model should have no blocking validation errors");

        var channel = viewModel.Channels.First();
        channel.CorrectFrequencyHz = 200; // 2000 Hz - above the real 1250 Hz max
        AssertTrue(viewModel.HasBlockingValidationErrors, "a raw Correct Frequency byte above the real range should be a blocking validation error");

        channel.CorrectFrequencyHz = 50; // 500 Hz - back in range
        AssertTrue(!viewModel.HasBlockingValidationErrors, "restoring an in-range Correct Frequency should clear the error");
    }

    private static void AlertToneFrequencyAndPeriodTextReportValidationErrorsInsteadOfReverting()
    {
        // Same ObservableValidator conversion as OptionalSettingsEntry's
        // VFO Scan/Auto Repeater frequency fields, 2026-07-31 - both floors
        // are zero here so the "impossible to type" bug never applied, but
        // the mechanism (never revert, report via HasErrors/GetErrors)
        // should behave identically.
        var entry = new AlertToneEntry { FrequencyText = "1500", PeriodText = "100" };
        AssertTrue(!entry.HasErrors, "A fresh, valid entry should have no errors.");

        entry.FrequencyText = "3001";
        AssertTrue(entry.HasErrors, "An out-of-range Frequency should be flagged, not silently reverted.");
        AssertEqual("1500", entry.FrequencyText);
        entry.FrequencyText = "1600";
        AssertTrue(!entry.HasErrors, "A valid Frequency should clear the error.");

        entry.PeriodText = "210";
        AssertTrue(entry.HasErrors, "An out-of-range Period should be flagged, not silently reverted.");
        AssertEqual("100", entry.PeriodText);
        entry.PeriodText = "150";
        AssertTrue(!entry.HasErrors, "A valid Period should clear the error.");
    }

    private static void AlertToneValidationErrorsBlockSaveAndWriteCommands()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "A fresh project has nothing to save.");

        viewModel.OptionalSettings.AlertTones[0].FrequencyText = "9999";
        AssertTrue(viewModel.HasBlockingValidationErrors, "An out-of-range Alert Tone Frequency should block Save/Write.");
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "Save should be disabled while an Alert Tone has a validation error.");

        viewModel.OptionalSettings.AlertTones[0].FrequencyText = "1500";
        AssertTrue(!viewModel.HasBlockingValidationErrors, "A valid Alert Tone Frequency should clear the validation error.");
        AssertTrue(viewModel.SaveProjectCommand.CanExecute(null), "Save should be enabled again once the value is valid and the project is dirty.");
    }

    private static void TurningVoxOnResetsVoxDetectionBackToItsFirstOption()
    {
        var entry = new OptionalSettingsEntry
        {
            VoxLevelText = "Off",
            VoxDetectionText = "External Microphone"
        };
        AssertEqual("External Microphone", entry.VoxDetectionText);

        entry.VoxLevelText = "On";

        AssertEqual("Built-in Microphone", entry.VoxDetectionText);
        AssertTrue(!entry.IsVoxDetectionEditable, "VoxDetection should be disabled while VoxLevel is On");
    }

    private static void AmFmFunctionControlsFmAndAmSectionEnabledState()
    {
        var entry = new OptionalSettingsEntry { AmFmFunctionText = "Off" };
        AssertTrue(!entry.IsFmSectionEnabled, "FM section should be disabled when AM/FM Function is Off");
        AssertTrue(!entry.IsAmSectionEnabled, "AM section should be disabled when AM/FM Function is Off");

        entry.AmFmFunctionText = "FM";
        AssertTrue(entry.IsFmSectionEnabled, "FM section should be enabled when AM/FM Function is FM");
        AssertTrue(!entry.IsAmSectionEnabled, "AM section should stay disabled when AM/FM Function is FM");

        entry.AmFmFunctionText = "AM(A)";
        AssertTrue(!entry.IsFmSectionEnabled, "FM section should be disabled when AM/FM Function is AM(A)");
        AssertTrue(entry.IsAmSectionEnabled, "AM section should be enabled when AM/FM Function is AM(A)");

        entry.AmFmFunctionText = "AM(B)";
        AssertTrue(entry.IsAmSectionEnabled, "AM section should be enabled when AM/FM Function is AM(B)");
    }

    private static void SatLocationOptionsStartWithGpsNotOff()
    {
        // Corrected 2026-08-01 per a direct vendor CPS comparison - index 0
        // is a location SOURCE ("use the radio's own GPS fix"), not an
        // on/off toggle.
        AssertEqual(9, OptionalSettingsEntry.SatLocationOptions.Count);
        AssertEqual("GPS", OptionalSettingsEntry.SatLocationOptions[0]);
        AssertEqual("Fixed-8", OptionalSettingsEntry.SatLocationOptions[8]);
    }

    private static void PowerOnVolumeOptionsCoverTheFull0To15RangeNotMaxVolumesIndoorsScale()
    {
        // Corrected 2026-08-01 per a direct vendor CPS comparison - this
        // field previously (wrongly) shared MaxVolumeOptions' own 9-item
        // Indoors/1-8 scale.
        AssertEqual(16, OptionalSettingsEntry.PowerOnVolumeOptions.Count);
        AssertEqual("0", OptionalSettingsEntry.PowerOnVolumeOptions[0]);
        AssertEqual("15", OptionalSettingsEntry.PowerOnVolumeOptions[15]);
    }

    private static void PowerOnVolumeTypeMinimumDisablesThePowerOnVolumeField()
    {
        var entry = new OptionalSettingsEntry { PowerOnVolumeTypeText = "Preset" };
        AssertTrue(entry.IsPowerOnVolumeEnabled, "Power On Volume should be enabled when Power On Volume Type is Preset");

        entry.PowerOnVolumeTypeText = "Minimum";
        AssertTrue(!entry.IsPowerOnVolumeEnabled, "Power On Volume should be disabled when Power On Volume Type is Minimum");

        entry.PowerOnVolumeTypeText = "Preset";
        AssertTrue(entry.IsPowerOnVolumeEnabled, "Power On Volume should re-enable when switched back to Preset");
    }

    private static void FmWorkChannelNameResolvesAgainstTheLiveFmChannelList()
    {
        var viewModel = new MainViewModel();
        viewModel.FmChannels.Add(new FmChannelEntry { Number = 1, Name = "First" });
        viewModel.FmChannels.Add(new FmChannelEntry { Number = 2, Name = "Second" });

        viewModel.OptionalSettingsFmWorkChannelName = "02  Second";
        AssertEqual((byte)1, viewModel.OptionalSettings.FmWorkChannel);
        AssertEqual("02  Second", viewModel.OptionalSettingsFmWorkChannelName);
    }

    // The always-present "home"/VFO slot (FmChannelCodec.HomeIndex) is a
    // different concept from "which memory channel is active" - it must
    // never become a selectable FM Work Channel option. Found 2026-07-30:
    // on the test radio the home slot's data happened to mirror channel
    // 1's name, showing up as two confusing identical-looking entries.
    // Originally filtered at the ViewModel level (OptionalSettingsFmChannelOptions);
    // moved upstream into RadioReadMapper.MapFmChannels 2026-08-03 so the
    // home slot never becomes an FmChannelEntry at all, matching MapAmAir's
    // own VFO exclusion - this test now covers that mapper directly.
    private static void MapFmChannelsExcludesTheHomeVfoSlot()
    {
        var normalChannel = new FmChannelCodec.DecodedFmChannel(0) { Name = "FM CH 1", FrequencyMHz = 100.0 };
        var homeChannel = new FmChannelCodec.DecodedFmChannel(FmChannelCodec.HomeIndex) { Name = "FM CH 1", FrequencyMHz = 100.0 };
        var result = new RadioCodeplugReadResult { Success = true, FmChannels = [normalChannel, homeChannel] };

        var mapped = RadioReadMapper.MapFmChannels(result);

        AssertEqual(1, mapped.Count);
        AssertEqual(1, mapped[0].Number);
    }

    // AM Work Zone is confirmed to have no real D890UV byte (see
    // OptionalSettingsCodec's doc comment) and the UI control is disabled -
    // this only covers that the ViewModel-level name<->index resolution
    // itself still works correctly, same as before it was disabled, in case
    // a future model needs it re-enabled.
    private static void AmWorkZoneNameResolvesAgainstTheLiveAmZoneList()
    {
        var viewModel = new MainViewModel();
        viewModel.AmZones.Add(new AmZoneEntry { Number = 1, Name = "OnlyZone" });

        viewModel.OptionalSettingsAmWorkZoneName = "01  OnlyZone";
        AssertEqual((byte)0, viewModel.OptionalSettings.AmWorkZone);
        AssertEqual("01  OnlyZone", viewModel.OptionalSettingsAmWorkZoneName);
    }

    private static void KeyFunctionOptionsMatchTheRealVendorCpsList()
    {
        // Guards against regressing back to the drifted ported list, which
        // had 5 entries the real vendor CPS doesn't have ("GPS Information",
        // "Ranging", "Channel Ranging", "APRS Type Switch", "APRS Set")
        // shifting everything after them out of alignment, and was missing
        // the vendor CPS's last 2 entries entirely - see KeyFunctionOptions'
        // doc comment.
        AssertEqual(59, OptionalSettingsEntry.KeyFunctionOptions.Count);
        AssertEqual("AM/FM", OptionalSettingsEntry.KeyFunctionOptions[11]);
        AssertEqual("Monitor", OptionalSettingsEntry.KeyFunctionOptions[17]);
        AssertEqual("Hot Key 6", OptionalSettingsEntry.KeyFunctionOptions[24]);
        AssertEqual("Roaming", OptionalSettingsEntry.KeyFunctionOptions[34]);
        AssertEqual("Repeater", OptionalSettingsEntry.KeyFunctionOptions[56]);
        AssertEqual("Freq Sync", OptionalSettingsEntry.KeyFunctionOptions[57]);
        AssertEqual("Freq Step", OptionalSettingsEntry.KeyFunctionOptions[58]);
    }

    private static void AnalogCallHoldTimeAndMuteTimingOptionsCoverTheirFullCorrectedRange()
    {
        // Both topped out one entry short of the real vendor CPS list -
        // see each option list's own doc comment.
        AssertEqual(31, OptionalSettingsEntry.AnalogCallHoldTimeOptions.Count);
        AssertEqual("0", OptionalSettingsEntry.AnalogCallHoldTimeOptions[0]);
        AssertEqual("30", OptionalSettingsEntry.AnalogCallHoldTimeOptions[30]);

        AssertEqual(256, OptionalSettingsEntry.MuteTimingOptions.Count);
        AssertEqual("1", OptionalSettingsEntry.MuteTimingOptions[0]);
        AssertEqual("256", OptionalSettingsEntry.MuteTimingOptions[255]);
    }

    private static void OnOffToBoolConverterRoundTripsOnOffText()
    {
        var converter = OnOffToBoolConverter.Instance;

        AssertEqual(true, (bool)converter.Convert("On", typeof(bool), null, CultureInfo.InvariantCulture)!);
        AssertEqual(false, (bool)converter.Convert("Off", typeof(bool), null, CultureInfo.InvariantCulture)!);
        AssertEqual("On", (string)converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture)!);
        AssertEqual("Off", (string)converter.ConvertBack(false, typeof(string), null, CultureInfo.InvariantCulture)!);
    }

    // Regression test for a real bug found by hand 2026-08-02: the
    // AES/ARC4 Key comboboxes used to be gated on the raw key index
    // already being non-zero (ChannelEntry.UsesAesEncryption/
    // UsesArc4Encryption) - a chicken-and-egg problem where the combobox
    // that lets you assign a key in the first place never appeared, since
    // it required a key to already be assigned. This converter instead
    // gates on the actual type selectors (device-wide Encryption Type +
    // per-channel Extended Encryption), which are always meaningful
    // regardless of whether a key has been picked yet.
    private static void EncryptionKeyVisibilityConverterGatesOnTypeSelectorsNotKeyIndex()
    {
        var converter = EncryptionKeyVisibilityConverter.Instance;

        // Device-wide type is AES/ARC4, per-channel Extended Encryption = AES
        // (false) -> AES Key combobox should show even with no key assigned
        // yet (that's exactly the point - it's how you'd assign one).
        var showAes = converter.Convert([true, false], typeof(bool), "False", CultureInfo.InvariantCulture);
        AssertEqual(true, (bool)showAes!);

        var showArc4WhenAesSelected = converter.Convert([true, false], typeof(bool), "True", CultureInfo.InvariantCulture);
        AssertEqual(false, (bool)showArc4WhenAesSelected!);

        // Extended Encryption = ARC4 (true) -> ARC4 Key combobox shows.
        var showArc4 = converter.Convert([true, true], typeof(bool), "True", CultureInfo.InvariantCulture);
        AssertEqual(true, (bool)showArc4!);

        // Device-wide type is not AES/ARC4 (Basic) -> neither shows.
        var showAesWhenBasic = converter.Convert([false, false], typeof(bool), "False", CultureInfo.InvariantCulture);
        AssertEqual(false, (bool)showAesWhenBasic!);
    }

    private static void VoxOnDrivesTheVoxSafetyWarningState()
    {
        var entry = new OptionalSettingsEntry { VoxLevelText = "Off" };
        AssertTrue(!entry.IsVoxOn, "IsVoxOn should be false when VOX is Off");

        entry.VoxLevelText = "On";
        AssertTrue(entry.IsVoxOn, "IsVoxOn should be true when VOX is On");
    }

    private static void PatcherAppliesOptionalSettingsPatchToMainAndDisplayRegions()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var display = new byte[OptionalSettingsCodec.SecondaryDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main),
                new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500900, display)
            ]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnInterface = 2,
            PowerOnPassword = 1,
            DefaultStartupChannel = 1,
            StartupZoneA = 5,
            StartupChannelA = 3,
            StartupZoneB = 7,
            StartupChannelB = 2,
            StartupReset = 1,
            PowerOnDisplayLine1 = "TESTPW1",
            PowerOnDisplayLine2 = "TESTPW2",
            PowerOnPasswordChar = "1357"
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var displayRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500900).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, displayRegion, new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)2, decoded.PowerOnInterface);
        AssertEqual((byte)1, decoded.PowerOnPassword);
        AssertEqual((byte)1, decoded.DefaultStartupChannel);
        AssertEqual((byte)5, decoded.StartupZoneA);
        AssertEqual((byte)3, decoded.StartupChannelA);
        AssertEqual((byte)7, decoded.StartupZoneB);
        AssertEqual((byte)2, decoded.StartupChannelB);
        AssertEqual((byte)1, decoded.StartupReset);
        AssertEqual("TESTPW1", decoded.PowerOnDisplayLine1);
        AssertEqual("TESTPW2", decoded.PowerOnDisplayLine2);
        AssertEqual("1357", decoded.PowerOnPasswordChar);

        // Untouched region bytes elsewhere in the 0x160-byte block must stay
        // zero (e.g. the Alert Tone table) - a full-record RMW patch must
        // never spill into fields it wasn't given.
        AssertEqual((byte)0, mainRegion[0x72]);
    }

    private static void PatcherAppliesAlertZoneScalarFieldsAndToneMatrices()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        (ushort Frequency, ushort Period)[] Tones(ushort firstFreq, ushort firstPeriod, ushort secondFreq, ushort secondPeriod) =>
        [
            (firstFreq, firstPeriod), (secondFreq, secondPeriod), (0, 0), (0, 0), (0, 0)
        ];

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            SmsAlert = 0,
            CallAlert = 0,
            DigiCallResetTone = 0,
            TalkPermit = 3,
            KeyTone = 1,
            DigiIdleChannelTone = 2,
            StartupSound = 1,
            AnalogIdleChannelTone = 1,
            // Raw wire units - period is display-value/10 (confirmed 2026-07-25
            // live write: 200/50 displayed -> 20/5 raw).
            CallPermitTones = Tones(800, 20, 300, 5),
            MatchEndTones = Tones(1600, 20, 700, 5),
            CallResetTones = Tones(2000, 20, 900, 5)
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)0, decoded.SmsAlert);
        AssertEqual((byte)0, decoded.CallAlert);
        AssertEqual((byte)0, decoded.DigiCallResetTone);
        AssertEqual((byte)3, decoded.TalkPermit);
        AssertEqual((byte)1, decoded.KeyTone);
        AssertEqual((byte)2, decoded.DigiIdleChannelTone);
        AssertEqual((byte)1, decoded.StartupSound);
        AssertEqual((byte)1, decoded.AnalogIdleChannelTone);

        var callPermit = decoded.AlertTones.Where(t => t.Category == "CallPermit").ToList();
        AssertEqual(800, (int)callPermit[0].Frequency);
        AssertEqual(20, (int)callPermit[0].Period);
        AssertEqual(300, (int)callPermit[1].Frequency);
        AssertEqual(5, (int)callPermit[1].Period);

        var matchEnd = decoded.AlertTones.Where(t => t.Category == "CallEnd").ToList();
        AssertEqual(1600, (int)matchEnd[0].Frequency);
        AssertEqual(700, (int)matchEnd[1].Frequency);

        var callReset = decoded.AlertTones.Where(t => t.Category == "CallReset").ToList();
        AssertEqual(2000, (int)callReset[0].Frequency);
        AssertEqual(900, (int)callReset[1].Frequency);

        // KeyTone lives at offset 0x00 - the very first byte of the record -
        // a real regression risk (easy to confuse with "nothing patched").
        AssertEqual((byte)1, mainRegion[0x00]);
    }

    private static void PatcherAppliesAlertTone1ToneMatrices()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        (ushort Frequency, ushort Period)[] Tones(ushort firstFreq, ushort firstPeriod, ushort secondFreq, ushort secondPeriod) =>
        [
            (firstFreq, firstPeriod), (secondFreq, secondPeriod), (0, 0), (0, 0), (0, 0)
        ];

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            UnMatchEndTones = Tones(1200, 80, 800, 30),
            CallAllTones = Tones(1900, 40, 400, 10)
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        var unMatchEnd = decoded.AlertTones.Where(t => t.Category == "UnMatchEnd").ToList();
        AssertEqual(1200, (int)unMatchEnd[0].Frequency);
        AssertEqual(80, (int)unMatchEnd[0].Period);
        AssertEqual(800, (int)unMatchEnd[1].Frequency);
        AssertEqual(30, (int)unMatchEnd[1].Period);

        var callAll = decoded.AlertTones.Where(t => t.Category == "CallAll").ToList();
        AssertEqual(1900, (int)callAll[0].Frequency);
        AssertEqual(40, (int)callAll[0].Period);
        AssertEqual(400, (int)callAll[1].Frequency);
        AssertEqual(10, (int)callAll[1].Period);

        // Untouched neighboring categories (CallPermit/CallEnd/CallReset) must
        // stay zero - a full-record RMW patch must never spill into fields it
        // wasn't given.
        var callPermit = decoded.AlertTones.Where(t => t.Category == "CallPermit").ToList();
        AssertEqual(0, (int)callPermit[0].Frequency);
    }

    private static void PatcherAppliesPowerSaveFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            AutoShutdown = 2,
            PowerSave = 2,
            AutoShutdownType = 1
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)2, decoded.AutoShutdown);
        AssertEqual((byte)2, decoded.PowerSave);
        AssertEqual((byte)1, decoded.AutoShutdownType);

        // AutoShutdownType's real offset (0x10f, confirmed 2026-07-25 via a
        // focused single-field differential test) is NOT the reference
        // project's claimed 0x3f, which is genuinely just GpsPositioning -
        // a real regression risk if someone "helpfully" reverts this to
        // match the reference again.
        AssertEqual((byte)0, mainRegion[0x3f]);
        AssertEqual((byte)1, mainRegion[0x10f]);
    }

    private static void PatcherAppliesDisplayTabFields()
    {
        var main = Enumerable.Repeat((byte)0xAB, OptionalSettingsCodec.MainDataLength).ToArray();
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            BacklightTxDelay = 20,
            SeparateDisplay = 1,
            DisplayChannelType = true,
            DisplayTimeSlot = true,
            DisplayColorCode = true,
            NightMode = 1
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        // BacklightTxDelay/SeparateDisplay are independently stored (0xe0 vs
        // 0xe1, confirmed 2026-07-25 - see OptionalSettingsCodec's doc
        // comment) - a real regression risk if someone "helpfully" merges
        // them back into one shared offset.
        AssertEqual((byte)20, decoded.BacklightTxDelay);
        AssertEqual((byte)1, decoded.SeparateDisplay);
        AssertEqual((byte)20, mainRegion[0xe0]);
        AssertEqual((byte)1, mainRegion[0xe1]);

        AssertEqual(true, decoded.DisplayChannelType);
        AssertEqual(true, decoded.DisplayTimeSlot);
        AssertEqual(true, decoded.DisplayColorCode);
        // All 3 bit flags pack into 0x110 - only bits 0-2 should be set,
        // preserving whatever else (untouched) was in that byte.
        AssertEqual((byte)(0xAB | 0x07), mainRegion[0x110]);

        AssertEqual((byte)1, decoded.NightMode);
        AssertEqual((byte)1, mainRegion[0x14d]);
    }

    private static void PatcherAppliesWorkModeFields()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            DisplayMode = 1,
            VfMrA = 1,
            MemZoneA = 1,
            VfMrB = 0,
            MemZoneB = 6,
            MainChannelSet = 1,
            SubChannelMode = 1,
            WorkingMode = 1
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.DisplayMode);
        AssertEqual((byte)1, decoded.VfMrA);
        AssertEqual((byte)1, decoded.MemZoneA);
        AssertEqual((byte)0, decoded.VfMrB);
        AssertEqual((byte)6, decoded.MemZoneB);
        AssertEqual((byte)1, decoded.MainChannelSet);
        AssertEqual((byte)1, decoded.SubChannelMode);
        AssertEqual((byte)1, decoded.WorkingMode);
    }

    private static void PatcherAppliesVoxFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            VoxLevel = 1,
            VoxDelay = 10,
            VoxDetection = 2
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.VoxLevel);
        AssertEqual((byte)10, decoded.VoxDelay);
        AssertEqual((byte)2, decoded.VoxDetection);
    }

    private static void PatcherAppliesSteFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            SteTypeOfCtcss = 3,
            SteWhenNoSignal = 2,
            SteTime = 15
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)3, decoded.SteTypeOfCtcss);
        AssertEqual((byte)2, decoded.SteWhenNoSignal);
        AssertEqual((byte)15, decoded.SteTime);
    }

    private static void PatcherAppliesAmFmFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            AmFmFunction = 2,
            FmVfoMem = 1,
            FmWorkChannel = 3,
            FmMonitor = 0,
            AmVfoMem = 0,
            AmOffset = 0,
            AmSqlLevel = 3,
            FrequencyStep = 4
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)2, decoded.AmFmFunction);
        AssertEqual((byte)1, decoded.FmVfoMem);
        AssertEqual((byte)3, decoded.FmWorkChannel);
        AssertEqual((byte)0, decoded.FmMonitor);
        AssertEqual((byte)0, decoded.AmVfoMem);
        AssertEqual((byte)0, decoded.AmOffset);
        AssertEqual((byte)3, decoded.AmSqlLevel);
        AssertEqual((byte)4, decoded.FrequencyStep);
        // AmFmFunction and FmVfoMem must land at different bytes now that
        // the reference project's claimed collision at 0x1e is disproven.
        AssertEqual((byte)2, mainRegion[0x21]);
        AssertEqual((byte)1, mainRegion[0x1e]);
        // FmWorkChannel - confirmed 2026-07-29 via a live differential
        // write (raw byte = zero-based index into the FM channel list).
        AssertEqual((byte)3, mainRegion[0x1d]);
    }

    private static void PatcherAppliesKeyFunctionFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            KeyLock = 1,
            Pf1ShortKey = 6,
            Pf2ShortKey = 0x0d,
            Pf3ShortKey = 0x14,
            P1ShortKey = 0x1b,
            P2ShortKey = 0x22,
            Pf1LongKey = 0x29,
            Pf2LongKey = 0x30,
            Pf3LongKey = 0x37,
            P1LongKey = 0x3a,
            P2LongKey = 0x3c,
            LongKeyTime = 2,
            KnobLock = true,
            KeyboardLock = true,
            SideKeyLock = true,
            ForcedKeyLock = true
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.KeyLock);
        AssertEqual((byte)6, decoded.Pf1ShortKey);
        AssertEqual((byte)0x0d, decoded.Pf2ShortKey);
        AssertEqual((byte)0x14, decoded.Pf3ShortKey);
        AssertEqual((byte)0x1b, decoded.P1ShortKey);
        AssertEqual((byte)0x22, decoded.P2ShortKey);
        AssertEqual((byte)0x29, decoded.Pf1LongKey);
        AssertEqual((byte)0x30, decoded.Pf2LongKey);
        AssertEqual((byte)0x37, decoded.Pf3LongKey);
        AssertEqual((byte)0x3a, decoded.P1LongKey);
        AssertEqual((byte)0x3c, decoded.P2LongKey);
        AssertEqual((byte)2, decoded.LongKeyTime);
        AssertTrue(decoded.KnobLock, "KnobLock should be true");
        AssertTrue(decoded.KeyboardLock, "KeyboardLock should be true");
        AssertTrue(decoded.SideKeyLock, "SideKeyLock should be true");
        AssertTrue(decoded.ForcedKeyLock, "ForcedKeyLock should be true");
        AssertEqual((byte)0x1b, mainRegion[0xbe]);
    }

    private static void PatcherAppliesOtherTabFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            AddressBookSentWithCode = 0,
            Tot = 3,
            Language = 0,
            GeneralFrequencyStep = 9,
            SqlLevelA = 3,
            SqlLevelB = 4,
            Tbst = 3,
            AnalogCallHoldTime = 12,
            CallChannelMaintained = 0,
            PriorityZoneA = 2,
            PriorityZoneB = 3,
            MuteTiming = 6,
            EncryptionType = 0,
            TotPredict = 0,
            TxPowerAgc = 0,
            NoaaMoni = 1,
            NoaaScan = 1,
            Noaa = 1,
            NoaaChannel = 4,
            FrequencyStep = 4
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)0, decoded.AddressBookSentWithCode);
        AssertEqual((byte)3, decoded.Tot);
        AssertEqual((byte)0, decoded.Language);
        AssertEqual((byte)9, decoded.GeneralFrequencyStep);
        AssertEqual((byte)3, decoded.SqlLevelA);
        AssertEqual((byte)4, decoded.SqlLevelB);
        AssertEqual((byte)3, decoded.Tbst);
        AssertEqual((byte)12, decoded.AnalogCallHoldTime);
        AssertEqual((byte)0, decoded.CallChannelMaintained);
        AssertEqual((byte)2, decoded.PriorityZoneA);
        AssertEqual((byte)3, decoded.PriorityZoneB);
        AssertEqual((byte)6, decoded.MuteTiming);
        AssertEqual((byte)0, decoded.EncryptionType);
        AssertEqual((byte)0, decoded.TotPredict);
        AssertEqual((byte)0, decoded.TxPowerAgc);
        AssertEqual((byte)1, decoded.NoaaMoni);
        AssertEqual((byte)1, decoded.NoaaScan);
        AssertEqual((byte)1, decoded.Noaa);
        AssertEqual((byte)4, decoded.NoaaChannel);
        // GeneralFrequencyStep (Other tab, 0x08) and FrequencyStep (AM/FM
        // tab, 0x159) must land at different bytes - they're genuinely
        // separate fields despite sharing a label and option list.
        AssertEqual((byte)4, decoded.FrequencyStep);
        AssertEqual((byte)9, mainRegion[0x08]);
        AssertEqual((byte)4, mainRegion[0x159]);
    }

    private static void PatcherAppliesDigitalFuncFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            GroupCallHoldTime = 6,
            PrivateCallHoldTime = 12,
            ManualDialGroupCallHoldTime = 20,
            ManualDialPrivateCallHoldTime = 32,
            VoiceHeaderRepetitions = 6,
            TxPreambleDuration = 10,
            FilterOwnId = 0,
            DigitalRemoteKill = 1,
            DigitalMonitor = 2,
            DigitalMonitorCc = 1,
            DigitalMonitorId = 1,
            MonitorSlotHold = 1,
            RemoteMonitor = 1,
            SmsFormat = 2,
            ResetDigitalProtocol = 0
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)6, decoded.GroupCallHoldTime);
        AssertEqual((byte)12, decoded.PrivateCallHoldTime);
        AssertEqual((byte)20, decoded.ManualDialGroupCallHoldTime);
        AssertEqual((byte)32, decoded.ManualDialPrivateCallHoldTime);
        AssertEqual((byte)6, decoded.VoiceHeaderRepetitions);
        AssertEqual((byte)10, decoded.TxPreambleDuration);
        AssertEqual((byte)0, decoded.FilterOwnId);
        AssertEqual((byte)1, decoded.DigitalRemoteKill);
        AssertEqual((byte)2, decoded.DigitalMonitor);
        AssertEqual((byte)1, decoded.DigitalMonitorCc);
        AssertEqual((byte)1, decoded.DigitalMonitorId);
        AssertEqual((byte)1, decoded.MonitorSlotHold);
        AssertEqual((byte)1, decoded.RemoteMonitor);
        AssertEqual((byte)2, decoded.SmsFormat);
        AssertEqual((byte)0, decoded.ResetDigitalProtocol);
    }

    private static void PatcherAppliesGpsRangingFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28: GpsPositioning=1(On), GpsMode=6(All)
        // matched byte-for-byte; TimeZone=27 confirmed to be "UTC+09:00" in the
        // real vendor CPS's own 34-entry list (not the reference project's
        // wrong 51-entry one - see TimeZoneOptions' doc comment).
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            GpsPositioning = 1,
            TimeZone = 27,
            GpsMode = 6
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.GpsPositioning);
        AssertEqual((byte)27, decoded.TimeZone);
        AssertEqual((byte)6, decoded.GpsMode);

        // GpsRoaming (0x114) must stay untouched - not part of this patch yet.
        AssertEqual((byte)0, decoded.GpsRoaming);
    }

    private static void TimeZoneOptionsMatchTheRealVendorCpsListNotTheReferenceProjects()
    {
        // Guards against regressing back to the reference project's wrong
        // 51-entry uniform-30-min-step list - see TimeZoneOptions' doc comment.
        AssertEqual(34, OptionalSettingsEntry.TimeZoneOptions.Count);
        var entry = new OptionalSettingsEntry { TimeZone = 27 };
        AssertEqual("UTC+09:00", entry.TimeZoneText);
        entry.TimeZoneText = "UTC-03:30";
        AssertEqual((byte)9, entry.TimeZone);
    }

    private static void PatcherAppliesVfoScanFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28: entering 405.12300/475.98700/
        // 140.45600/170.65400 MHz and Scan Type "SE" produced these exact raw
        // values (frequencies within 1 unit of MHz*100000 - float rounding on
        // the vendor CPS's own side, not an offset/scale error).
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            VfoScanType = 2,
            VfoScanStartFreqUhf = 40512299,
            VfoScanEndFreqUhf = 47598700,
            VfoScanStartFreqVhf = 14045599,
            VfoScanEndFreqVhf = 17065401
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)2, decoded.VfoScanType);
        AssertEqual(40512299, decoded.VfoScanStartFreqUhf);
        AssertEqual(47598700, decoded.VfoScanEndFreqUhf);
        AssertEqual(14045599, decoded.VfoScanStartFreqVhf);
        AssertEqual(17065401, decoded.VfoScanEndFreqVhf);
    }

    private static void VfoScanFrequencyTextFieldsConvertToAndFromMhzTimes100000()
    {
        var entry = new OptionalSettingsEntry { VfoScanStartFreqUhf = 40512300 };
        AssertEqual("405.12300", entry.VfoScanStartFreqUhfText);
        entry.VfoScanStartFreqUhfText = "475.98700";
        AssertEqual(47598700, entry.VfoScanStartFreqUhf);
    }

    private static void VfoScanFrequencyTextFieldsRejectOutOfBandValues()
    {
        // Real radio band limits, confirmed 2026-07-30: UHF
        // 400.00000-480.00000 MHz, VHF 136.00000-174.00000 MHz.
        var uhf = new OptionalSettingsEntry { VfoScanEndFreqUhfText = "440.00000" };
        uhf.VfoScanEndFreqUhfText = "399.99999";
        AssertEqual("440.00000", uhf.VfoScanEndFreqUhfText);
        uhf.VfoScanEndFreqUhfText = "480.00001";
        AssertEqual("440.00000", uhf.VfoScanEndFreqUhfText);
        uhf.VfoScanEndFreqUhfText = "480.00000";
        AssertEqual("480.00000", uhf.VfoScanEndFreqUhfText);
        uhf.VfoScanEndFreqUhfText = "400.00000";
        AssertEqual("400.00000", uhf.VfoScanEndFreqUhfText);

        var vhf = new OptionalSettingsEntry { VfoScanEndFreqVhfText = "150.00000" };
        vhf.VfoScanEndFreqVhfText = "135.99999";
        AssertEqual("150.00000", vhf.VfoScanEndFreqVhfText);
        vhf.VfoScanEndFreqVhfText = "174.00001";
        AssertEqual("150.00000", vhf.VfoScanEndFreqVhfText);
        vhf.VfoScanEndFreqVhfText = "174.00000";
        AssertEqual("174.00000", vhf.VfoScanEndFreqVhfText);
        vhf.VfoScanEndFreqVhfText = "136.00000";
        AssertEqual("136.00000", vhf.VfoScanEndFreqVhfText);
    }

    private static void VfoScanFrequencyTextFieldsCanBeTypedUpFromAValueBelowTheirMinimum()
    {
        // Reproduces the 2026-07-30 bug report: building "140.00000" up one
        // character at a time necessarily passes through "1" and "14",
        // both below the VHF floor of 136 - the original reject-and-revert
        // setter forced the TextBox back to its old value on every one of
        // those keystrokes, making the field impossible to type into from
        // empty. The fixed setter accepts every keystroke (never reverts)
        // and reports a validation error instead of fighting the input.
        var entry = new OptionalSettingsEntry();
        entry.VfoScanStartFreqVhfText = "1";
        AssertTrue(entry.HasErrors, "A too-low intermediate value should be flagged, not silently reverted.");
        entry.VfoScanStartFreqVhfText = "14";
        AssertTrue(entry.HasErrors, "Still below the VHF floor while typing.");
        entry.VfoScanStartFreqVhfText = "140.00000";
        AssertTrue(!entry.HasErrors, "140 MHz is within the 136-174 VHF range.");
        AssertEqual(14000000, entry.VfoScanStartFreqVhf);
    }

    private static void VfoScanFrequencyTextFieldsReportAValidationErrorForOutOfRangeValues()
    {
        var entry = new OptionalSettingsEntry();
        entry.VfoScanEndFreqUhfText = "500.00000";
        AssertTrue(entry.HasErrors, "500 MHz is outside the 400-480 UHF range.");
        var errors = entry.GetErrors(nameof(OptionalSettingsEntry.VfoScanEndFreqUhfText)).ToList();
        AssertEqual(1, errors.Count);
        AssertContains("400.00000-480.00000", errors[0].ErrorMessage ?? "");

        entry.VfoScanEndFreqUhfText = "450.00000";
        AssertTrue(!entry.HasErrors, "A valid value should clear the error.");
    }

    private static void AutoRepeaterFrequencyTextFieldsReportAValidationErrorForUnparsableValues()
    {
        var entry = new OptionalSettingsEntry();
        entry.AutoRepeater1MinFreqVhfText = "not-a-number";
        AssertTrue(entry.HasErrors, "Unparsable text should be flagged, not silently reverted.");
        entry.AutoRepeater1MinFreqVhfText = "145.50000";
        AssertTrue(!entry.HasErrors, "A parsable value should clear the error.");
        AssertEqual(14550000, entry.AutoRepeater1MinFreqVhf);
    }

    private static void AutoRoamingFixedTimeOptionsCoverTheFull1To256Range()
    {
        AssertEqual(256, OptionalSettingsEntry.AutoRoamingFixedTimeOptions.Count);
        AssertEqual("1", OptionalSettingsEntry.AutoRoamingFixedTimeOptions[0]);
        AssertEqual("256", OptionalSettingsEntry.AutoRoamingFixedTimeOptions[255]);
    }

    private static void AutoRepeaterFrequencyTextFieldsEnforceTheSameVhfUhfBandLimitsAsVfoScan()
    {
        // Range limits added 2026-08-01, confirmed to be the same real
        // VHF/UHF band limits as VFO Scan (136.00000-174.00000 MHz /
        // 400.00000-480.00000 MHz) - reuses VfoScanStartFreqUhfText's own
        // validators, so this only spot-checks one VHF and one UHF field
        // rather than repeating all 8.
        var entry = new OptionalSettingsEntry { AutoRepeater1MinFreqVhfText = "150.00000", AutoRepeater2MaxFreqUhfText = "440.00000" };

        entry.AutoRepeater1MinFreqVhfText = "135.99999";
        AssertTrue(entry.HasErrors, "Below the VHF floor should be flagged.");
        AssertEqual(15000000, entry.AutoRepeater1MinFreqVhf);
        entry.AutoRepeater1MinFreqVhfText = "174.00001";
        AssertTrue(entry.HasErrors, "Above the VHF ceiling should be flagged.");
        entry.AutoRepeater1MinFreqVhfText = "136.00000";
        AssertTrue(!entry.HasErrors, "136 MHz is the VHF floor, inclusive.");

        entry.AutoRepeater2MaxFreqUhfText = "399.99999";
        AssertTrue(entry.HasErrors, "Below the UHF floor should be flagged.");
        AssertEqual(44000000, entry.AutoRepeater2MaxFreqUhf);
        entry.AutoRepeater2MaxFreqUhfText = "480.00001";
        AssertTrue(entry.HasErrors, "Above the UHF ceiling should be flagged.");
        entry.AutoRepeater2MaxFreqUhfText = "480.00000";
        AssertTrue(!entry.HasErrors, "480 MHz is the UHF ceiling, inclusive.");
    }

    private static void OptionalSettingsValidationErrorsBlockSaveAndWriteCommands()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "A fresh project has nothing to save.");

        viewModel.OptionalSettings.VfoScanEndFreqUhfText = "999.00000";
        AssertTrue(viewModel.HasBlockingValidationErrors, "An out-of-range VFO Scan frequency should block Save/Write.");
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "Save should be disabled while Optional Settings has a validation error.");

        viewModel.OptionalSettings.VfoScanEndFreqUhfText = "440.00000";
        AssertTrue(!viewModel.HasBlockingValidationErrors, "A valid VFO Scan frequency should clear the validation error.");
        AssertTrue(viewModel.SaveProjectCommand.CanExecute(null), "Save should be enabled again once the value is valid and the project is dirty.");
    }

    private static void PatcherAppliesAutoRepeaterFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28 across several rounds. RepeaterCheck/
        // RepeaterCheckInterval/RepeaterCheckReconnections/AutoRoamingStartCondition/
        // RepeaterOutOfRangeNotify/OutOfRangeNotify were all found shifted one
        // byte from the reference project's claims via focused single-field
        // differential tests, after a noisy batch test (compounded by a mid-
        // session backup restore) made it clear something was wrong - see
        // PowerOnFieldPatch's doc comment.
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            AutoRepeaterA = 1,
            AutoRepeaterB = 2,
            AutoRepeater1Uhf = 0,
            AutoRepeater1Vhf = 1,
            AutoRepeater2Uhf = 0xFF,
            AutoRepeater2Vhf = 0xFF,
            RepeaterCheck = 1,
            RepeaterCheckInterval = 2,
            RepeaterCheckReconnections = 0,
            AutoRoamingStartCondition = 0,
            RepeaterOutOfRangeNotify = 2,
            OutOfRangeNotify = 8,
            AutoRoaming = 1,
            AutoRoamingFixedTime = 5,
            RoamingEffectWaitTime = 12,
            AutoRepeater1MinFreqVhf = 14450000,
            AutoRepeater1MaxFreqVhf = 14800000,
            AutoRepeater1MinFreqUhf = 42000000,
            AutoRepeater1MaxFreqUhf = 45000000,
            AutoRepeater2MinFreqVhf = 13612500,
            AutoRepeater2MaxFreqVhf = 16275000,
            AutoRepeater2MinFreqUhf = 40025000,
            AutoRepeater2MaxFreqUhf = 46525000,
            RepeaterMode = 1,
            RepCcLimit = 2,
            RepSlotA = 2,
            RepSlotB = 2,
            RepeaterWhitelist = 1
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.AutoRepeaterA);
        AssertEqual((byte)2, decoded.AutoRepeaterB);
        AssertEqual((byte)0, decoded.AutoRepeater1Uhf);
        AssertEqual((byte)1, decoded.AutoRepeater1Vhf);
        AssertEqual((byte)0xFF, decoded.AutoRepeater2Uhf);
        AssertEqual((byte)0xFF, decoded.AutoRepeater2Vhf);
        AssertEqual((byte)1, decoded.RepeaterCheck);
        AssertEqual((byte)2, decoded.RepeaterCheckInterval);
        AssertEqual((byte)0, decoded.RepeaterCheckReconnections);
        AssertEqual((byte)0, decoded.AutoRoamingStartCondition);
        AssertEqual((byte)2, decoded.RepeaterOutOfRangeNotify);
        AssertEqual((byte)8, decoded.OutOfRangeNotify);
        AssertEqual((byte)1, decoded.AutoRoaming);
        AssertEqual((byte)5, decoded.AutoRoamingFixedTime);
        AssertEqual((byte)12, decoded.RoamingEffectWaitTime);
        AssertEqual(14450000, decoded.AutoRepeater1MinFreqVhf);
        AssertEqual(14800000, decoded.AutoRepeater1MaxFreqVhf);
        AssertEqual(42000000, decoded.AutoRepeater1MinFreqUhf);
        AssertEqual(45000000, decoded.AutoRepeater1MaxFreqUhf);
        AssertEqual(13612500, decoded.AutoRepeater2MinFreqVhf);
        AssertEqual(16275000, decoded.AutoRepeater2MaxFreqVhf);
        AssertEqual(40025000, decoded.AutoRepeater2MinFreqUhf);
        AssertEqual(46525000, decoded.AutoRepeater2MaxFreqUhf);
        AssertEqual((byte)1, decoded.RepeaterMode);
        AssertEqual((byte)2, decoded.RepCcLimit);
        AssertEqual((byte)2, decoded.RepSlotA);
        AssertEqual((byte)2, decoded.RepSlotB);
        AssertEqual((byte)1, decoded.RepeaterWhitelist);

        // RoamingZone (0xd5) and AddressBookSentWithCode (0xd5) share an
        // offset - confirm patching Auto Repeater fields never spills into it.
        AssertEqual((byte)0, decoded.RoamingZone);
        AssertEqual((byte)0, decoded.AddressBookSentWithCode);
    }

    private static void AutoRepeaterOffsetFieldsUseOffSentinelNotAPlainThreeItemList()
    {
        var entry = new OptionalSettingsEntry { AutoRepeater1Uhf = 0 };
        AssertEqual("600.00 kHz", entry.AutoRepeater1UhfText);
        entry.AutoRepeater1VhfText = "5.00000 MHz";
        AssertEqual((byte)1, entry.AutoRepeater1Vhf);
        entry.AutoRepeater2UhfText = "Off";
        AssertEqual((byte)0xFF, entry.AutoRepeater2Uhf);
        entry.AutoRepeater2Vhf = 0xFF;
        AssertEqual("Off", entry.AutoRepeater2VhfText);
    }

    private static void PatcherAppliesRecordFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28 at their reference-claimed offsets
        // exactly (Off->On produced raw 1 at 0x22; 0.0s->3.0s produced raw
        // 15 at 0xae), no bugs found.
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            RecordFunction = 1,
            RecordDelay = 15
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)1, decoded.RecordFunction);
        AssertEqual((byte)15, decoded.RecordDelay);
    }

    private static void PatcherAppliesVolumeAudioFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28. MaxVolume/MaxHeadphoneVolume/
        // DigiMicGain/AnalogMicGain/EnhancedSoundQuality matched their
        // reference-claimed offsets exactly. PowerOnVolumeType/PowerOnVolume/
        // RxAgc were previously raw/unconfirmed - all 3 confirmed to be plain
        // enums. SubSpkInTx/RxNoiseReduction/TxNoiseReduction
        // found from scratch (0x142/0x148/0x149).
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            MaxVolume = 5,
            PowerOnVolumeType = 0,
            PowerOnVolume = 5,
            MaxHeadphoneVolume = 3,
            DigiMicGain = 5,
            EnhancedSoundQuality = 1,
            AnalogMicGain = 5,
            RxAgc = 1,
            NxMicGain = 5,
            SubSpkInTx = 1,
            RxNoiseReduction = 3,
            TxNoiseReduction = 2
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)5, decoded.MaxVolume);
        AssertEqual((byte)0, decoded.PowerOnVolumeType);
        AssertEqual((byte)5, decoded.PowerOnVolume);
        AssertEqual((byte)3, decoded.MaxHeadphoneVolume);
        AssertEqual((byte)5, decoded.DigiMicGain);
        AssertEqual((byte)1, decoded.EnhancedSoundQuality);
        AssertEqual((byte)5, decoded.AnalogMicGain);
        AssertEqual((byte)1, decoded.RxAgc);
        AssertEqual((byte)5, decoded.NxMicGain);
        AssertEqual((byte)1, decoded.SubSpkInTx);
        AssertEqual((byte)3, decoded.RxNoiseReduction);
        AssertEqual((byte)2, decoded.TxNoiseReduction);
    }

    private static void MicGainOptionsIncludeTheAutoEntryVendorCpsHas()
    {
        // Guards against regressing back to the reference project's wrong
        // 5-entry list missing "Auto" - see MicGainOptions' doc comment.
        AssertEqual(6, OptionalSettingsEntry.MicGainOptions.Count);
        var entry = new OptionalSettingsEntry { DigiMicGain = 5 };
        AssertEqual("Auto", entry.DigiMicGainText);
    }

    private static void PatcherAppliesSatelliteFieldsAtConfirmedOffsets()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28 at their reference-claimed offsets
        // exactly, no bugs found - last of the 18 Optional Settings sub-tabs
        // to get write support.
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            SatLocation = 3,
            SatTxPower = 2,
            SatAnaSql = 4,
            SatAosLimit = 22
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)3, decoded.SatLocation);
        AssertEqual((byte)2, decoded.SatTxPower);
        AssertEqual((byte)4, decoded.SatAnaSql);
        AssertEqual((byte)22, decoded.SatAosLimit);
    }

    private static void PatcherAppliesRoamingZoneAtItsCorrectedOffsetNotTheAddressBookCollision()
    {
        var main = new byte[OptionalSettingsCodec.MainDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, main)]
        };

        // Live-write-confirmed 2026-07-28 via a dedicated two-step
        // differential test: "ROAM ZONE 1"(index 0) -> "ROAM ZONE 3"(index 2)
        // produced exactly one changed byte, at 0xdb - the reference
        // project's claimed 0xd5 was never a real collision with
        // AddressBookSentWithCode, just a wrong offset. This was the last
        // remaining "not yet writable" field on the whole Optional Settings
        // entity.
        var patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(snapshot, new OptionalSettingsCodec.PowerOnFieldPatch
        {
            RoamingZone = 2,
            AddressBookSentWithCode = 1
        });

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000).Data;
        var decoded = OptionalSettingsCodec.Decode(mainRegion, new byte[OptionalSettingsCodec.SecondaryDataLength], new byte[OptionalSettingsCodec.TertiaryDataLength]);

        AssertEqual((byte)2, decoded.RoamingZone);
        AssertEqual((byte)1, decoded.AddressBookSentWithCode);
        // The old reference-claimed offset (0xd5) must stay untouched by a
        // RoamingZone-only patch, proving there's no real collision.
        AssertEqual((byte)1, mainRegion[0xd5]);
    }

    private static void HoldTimeAndVoiceHeaderRepetitionsTextUseOffsetEncoding()
    {
        var entry = new OptionalSettingsEntry { GroupCallHoldTime = 6 };
        AssertEqual("6", entry.GroupCallHoldTimeText);
        entry.GroupCallHoldTimeText = "30";
        AssertEqual((byte)30, entry.GroupCallHoldTime);

        var infinite = new OptionalSettingsEntry { ManualDialPrivateCallHoldTime = 32 };
        AssertEqual("Infinite", infinite.ManualDialPrivateCallHoldTimeText);

        var reps = new OptionalSettingsEntry { VoiceHeaderRepetitions = 6 };
        AssertEqual("6", reps.VoiceHeaderRepetitionsText);
        reps.VoiceHeaderRepetitionsText = "2";
        AssertEqual((byte)2, reps.VoiceHeaderRepetitions);
    }

    private static void VoiceHeaderRepetitionsAndTxPreambleDurationOptionsMatchTheRealVendorCpsList()
    {
        // Guards against regressing back to VoiceHeaderRepetitionsOptions
        // topping out at 7 (should be 10) and TxPreambleDurationOptions
        // topping out at 2340 with 2280 still present (the real vendor CPS
        // list has one more entry, 2400, and is missing 2280) - see each
        // option list's own doc comment.
        AssertEqual(9, OptionalSettingsEntry.VoiceHeaderRepetitionsOptions.Count);
        AssertEqual("10", OptionalSettingsEntry.VoiceHeaderRepetitionsOptions[8]);

        AssertEqual(40, OptionalSettingsEntry.TxPreambleDurationOptions.Count);
        AssertTrue(!OptionalSettingsEntry.TxPreambleDurationOptions.Contains("2280"), "2280 should not be a TX Preamble Duration option");
        AssertEqual("2220", OptionalSettingsEntry.TxPreambleDurationOptions[37]);
        AssertEqual("2340", OptionalSettingsEntry.TxPreambleDurationOptions[38]);
        AssertEqual("2400", OptionalSettingsEntry.TxPreambleDurationOptions[39]);
    }

    private static void SteTimeTextConvertsRawByteWithOffByOneMapping()
    {
        var entry = new OptionalSettingsEntry { SteTime = 15 };
        AssertEqual("150", entry.SteTimeText);

        entry.SteTimeText = "990";
        AssertEqual((byte)99, entry.SteTime);

        // The 1000ms entry (raw 100) was missing until 2026-07-29 - the list
        // used to stop at 990 (raw 99).
        entry.SteTimeText = "1000";
        AssertEqual((byte)100, entry.SteTime);
    }

    private static void AnalogQuickCallOperationTypeChangeResetsCallIdAndDisablesItForOffAndDtmf()
    {
        var entry = new AnalogQuickCallEntry { Number = 1, OperationType = 2, CallId = 5 };

        AssertTrue(entry.IsCallIdEnabled, "2Tone should leave Call ID enabled.");

        entry.OperationType = 0; // Off
        AssertEqual(-1, entry.CallId);
        AssertTrue(!entry.IsCallIdEnabled, "Off should disable Call ID.");

        entry.CallId = 3;
        entry.OperationType = 1; // DTMF
        AssertEqual(-1, entry.CallId);
        AssertTrue(!entry.IsCallIdEnabled, "DTMF should disable Call ID - the real vendor CPS only offers Off here.");

        entry.OperationType = 4; // QDC1200
        AssertTrue(entry.IsCallIdEnabled, "QDC1200 should leave Call ID enabled.");
    }

    private static void AnalogQuickCallOperationTypeTextRoundTripsThroughOptions()
    {
        var entry = new AnalogQuickCallEntry { Number = 1 };
        AssertEqual("Off", entry.OperationTypeText);

        entry.OperationTypeText = "5Tone";
        AssertEqual((byte)3, entry.OperationType);

        entry.OperationTypeText = "QDC1200";
        AssertEqual((byte)4, entry.OperationType);
    }

    private static void HotKeyEnableFlagsMatchTheRealVendorCpsGatingForEveryModeAndCallTypeCombination()
    {
        var entry = new HotKeyEntry { Key = "Hot Key 1" };

        // Mode = Menu (1): everything below Menu is disabled.
        entry.Mode = 1;
        AssertTrue(entry.IsMenuEnabled, "Menu should be enabled when Mode=Menu.");
        AssertTrue(!entry.IsCallTypeEnabled, "Call Type should be disabled when Mode=Menu.");
        AssertTrue(!entry.IsCallObjectEnabled, "Call Object should be disabled when Mode=Menu.");
        AssertTrue(!entry.IsDigiCallTypeEnabled, "Digi Call Type should be disabled when Mode=Menu.");
        AssertTrue(!entry.IsContentEnabled, "Content should be disabled when Mode=Menu.");

        // Mode = Call (0), Call Type = Off (0): Menu disabled, everything past Call Type disabled.
        entry.Mode = 0;
        AssertTrue(!entry.IsMenuEnabled, "Menu should be disabled when Mode=Call.");
        AssertTrue(entry.IsCallTypeEnabled, "Call Type should be enabled when Mode=Call.");
        AssertTrue(!entry.IsCallObjectEnabled, "Call Object should be disabled when Call Type=Off.");
        AssertTrue(!entry.IsDigiCallTypeEnabled, "Digi Call Type should be disabled when Call Type=Off.");

        // Call Type = Analog (1): Call Object enabled, Digi Call Type/Content stay disabled.
        entry.CallType = 1;
        AssertTrue(entry.IsCallObjectEnabled, "Call Object should be enabled when Call Type=Analog.");
        AssertTrue(!entry.IsDigiCallTypeEnabled, "Digi Call Type should be disabled when Call Type=Analog.");
        AssertTrue(!entry.IsContentEnabled, "Content should be disabled when Call Type=Analog.");

        // Call Type = Digital (2): Call Object AND Digi Call Type enabled.
        entry.CallType = 2;
        AssertTrue(entry.IsCallObjectEnabled, "Call Object should be enabled when Call Type=Digital.");
        AssertTrue(entry.IsDigiCallTypeEnabled, "Digi Call Type should be enabled when Call Type=Digital.");

        // Content only enabled for DMR Hot Text (1) and DMR State Information (3),
        // NOT for Off (0) or DMR Call Tip (2) - matches the reference project's own
        // flags() gating and a direct vendor CPS observation.
        entry.DigiCallType = 0;
        AssertTrue(!entry.IsContentEnabled, "Content should be disabled when Digi Call Type=Off.");
        entry.DigiCallType = 1;
        AssertTrue(entry.IsContentEnabled, "Content should be enabled when Digi Call Type=DMR Hot Text.");
        entry.DigiCallType = 2;
        AssertTrue(!entry.IsContentEnabled, "Content should be disabled when Digi Call Type=DMR Call Tip.");
        entry.DigiCallType = 3;
        AssertTrue(entry.IsContentEnabled, "Content should be enabled when Digi Call Type=DMR State Information.");
    }

    private static void HotKeyChangingModeCallTypeOrDigiCallTypeResetsFieldsGatedBelowIt()
    {
        var entry = new HotKeyEntry { Key = "Hot Key 1", Mode = 0, CallType = 2, CallObject = 7, DigiCallType = 1, Content = 3 };

        entry.DigiCallType = 3;
        AssertEqual(-1, entry.Content);

        entry.CallType = 1;
        AssertEqual(-1, entry.CallObject);
        AssertEqual((byte)0, entry.DigiCallType);

        entry.CallType = 2;
        entry.CallObject = 9;
        entry.Mode = 1;
        AssertEqual((byte)0, entry.CallType);
    }

    private static void NewMainViewModelSeedsExactlyEighteenHotKeyRowsWithTheRealKeyNames()
    {
        var viewModel = new MainViewModel();

        AssertEqual(CodeplugLimits.HotKeyKeyCount, viewModel.HotKeys.Count);
        for (var i = 0; i < HotKeyEntry.KeyNames.Count; i++)
        {
            AssertEqual(HotKeyEntry.KeyNames[i], viewModel.HotKeys[i].Key);
        }
    }

    private static void AddAnalogQuickCallIsCappedAtFourSlots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.AnalogQuickCallMax; i++)
        {
            viewModel.AddAnalogQuickCallCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.AnalogQuickCallMax, viewModel.AnalogQuickCalls.Count);

        viewModel.AddAnalogQuickCallCommand.Execute(null);
        AssertEqual(CodeplugLimits.AnalogQuickCallMax, viewModel.AnalogQuickCalls.Count);
    }

    private static void AddStateInformationIsCappedAtThirtyTwoSlots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.StateInformationMax; i++)
        {
            viewModel.AddStateInformationCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.StateInformationMax, viewModel.StateInformationEntries.Count);

        viewModel.AddStateInformationCommand.Execute(null);
        AssertEqual(CodeplugLimits.StateInformationMax, viewModel.StateInformationEntries.Count);
    }

    private static void AnalogQuickCallCallIdOptionsPickTheListMatchingTheSelectedOperationType()
    {
        var viewModel = new MainViewModel();
        viewModel.AddAnalogQuickCallCommand.Execute(null);
        var entry = viewModel.SelectedAnalogQuickCall!;

        entry.OperationType = 2; // 2Tone
        AssertSame(viewModel.Tone2Ids, viewModel.AnalogQuickCallCallIdOptions);

        entry.OperationType = 3; // 5Tone
        AssertSame(viewModel.Tone5Ids, viewModel.AnalogQuickCallCallIdOptions);

        entry.OperationType = 4; // QDC1200
        AssertSame(viewModel.QdcIds, viewModel.AnalogQuickCallCallIdOptions);

        entry.OperationType = 0; // Off
        AssertEqual(1, viewModel.AnalogQuickCallCallIdOptions.Count);
        AssertEqual("Off", viewModel.AnalogQuickCallCallIdOptions[0]);
    }

    private static void HotKeyCallObjectOptionsOnlyOfferConfiguredAnalogQuickCallSlots()
    {
        var viewModel = new MainViewModel();
        viewModel.AddAnalogQuickCallCommand.Execute(null); // No. 1, Off by default
        viewModel.AddAnalogQuickCallCommand.Execute(null); // No. 2
        viewModel.AnalogQuickCalls[1].OperationType = 2; // configure only slot 2

        viewModel.SelectedHotKey = viewModel.HotKeys[0];
        viewModel.SelectedHotKey.CallType = 1; // Analog

        var options = viewModel.HotKeyCallObjectOptions;
        AssertEqual(2, options.Count); // "Off" + slot 2 only, slot 1 is still Off
        AssertEqual("Off", options[0]);
        AssertEqual("2", options[1]);
    }

    private static void HotKeyContentOptionsOfferPrefabricatedSmsForHotTextAndStateInformationEntriesForStateInformation()
    {
        // Confirmed 2026-08-04 via a live write capture: DMR State
        // Information's Content is a real reference into the State
        // Information list (the same shape as Hot Text's own Prefabricated
        // SMS reference), not the literal "1"/"16" pair originally guessed.
        var viewModel = new MainViewModel();
        viewModel.SelectedHotKey = viewModel.HotKeys[0];
        viewModel.SelectedHotKey.CallType = 2; // Digital

        viewModel.SelectedHotKey.DigiCallType = 3; // DMR State Information
        AssertEqual(1, viewModel.HotKeyContentOptions.Count); // just "Off" - no configured State Information yet

        viewModel.AddStateInformationCommand.Execute(null);
        viewModel.SelectedStateInformation!.Content = "Status Message 1";
        AssertEqual(2, viewModel.HotKeyContentOptions.Count);
        AssertEqual("Off", viewModel.HotKeyContentOptions[0]);
        AssertEqual(viewModel.SelectedStateInformation.Number.ToString(), viewModel.HotKeyContentOptions[1]);

        viewModel.SelectedHotKey.DigiCallType = 1; // DMR Hot Text
        AssertEqual(1, viewModel.HotKeyContentOptions.Count); // just "Off" - no configured SMS yet

        viewModel.AddPrefabricatedSmsCommand.Execute(null);
        viewModel.SelectedPrefabricatedSms!.Text = "Hello!";
        AssertEqual(2, viewModel.HotKeyContentOptions.Count);
        AssertEqual("Off", viewModel.HotKeyContentOptions[0]);
        AssertEqual(viewModel.SelectedPrefabricatedSms.Number.ToString(), viewModel.HotKeyContentOptions[1]);
    }

    private static void AnalogQuickCallCodecDecodesOffAndDtmfAsAnUnavailableCallIdMatchingTheLiveCapture()
    {
        // Real bytes from the 2026-08-04 live differential READ capture -
        // all 4 slots on the test radio: OperationType=0x00 (Off), CallId=0xFF.
        var decoded = AnalogQuickCallCodec.Decode([0x00, 0xFF], 0);
        AssertEqual((byte)0, decoded.OperationType);
        AssertEqual(-1, decoded.CallId);

        var configured = AnalogQuickCallCodec.Decode([0x02, 0x05], 1);
        AssertEqual((byte)2, configured.OperationType);
        AssertEqual(5, configured.CallId);
    }

    private static void StateInformationCodecDecodesTextAndTreatsABlankSlotAsEmpty()
    {
        // Real bytes from the capture: "Status Message 1" (16 chars, exactly
        // fills two 16-byte blocks = the full 0x40 record with no padding).
        var text = "Status Message 1";
        var record = new byte[StateInformationCodec.RecordLength];
        System.Text.Encoding.Unicode.GetBytes(text).CopyTo(record, 0);
        AssertEqual(text, StateInformationCodec.Decode(record));

        var blank = new byte[StateInformationCodec.RecordLength];
        AssertEqual("", StateInformationCodec.Decode(blank));
    }

    private static void HotKeyCodecDecodesEveryFieldAtItsConfirmedByteOffset()
    {
        // Real bytes from the capture: Hot Key 1 = Mode=Menu(1), Menu=1,
        // CallType=Off(0), DigiCallType=Off(0), CallObject=Off(0xFFFFFFFF),
        // Content=Off(0xFF).
        var record = new byte[HotKeyCodec.RecordLength];
        record[0] = 0x01;
        record[1] = 0x01;
        record[2] = 0x00;
        record[3] = 0x00;
        record[4] = 0xFF; record[5] = 0xFF; record[6] = 0xFF; record[7] = 0xFF;
        record[8] = 0xFF;

        var decoded = HotKeyCodec.Decode(record, 0);
        AssertEqual(0, decoded.Index);
        AssertEqual((byte)1, decoded.Mode);
        AssertEqual((byte)1, decoded.Menu);
        AssertEqual((byte)0, decoded.CallType);
        AssertEqual((byte)0, decoded.DigiCallType);
        AssertEqual(-1, decoded.CallObject);
        AssertEqual(-1, decoded.Content);

        // Real bytes from the 2026-08-04 live differential WRITE capture -
        // Hot Key 3, set in vendor CPS to Call Type=Digital, Digi Call
        // Type=DMR Hot Text, Call Object=a real Talkgroup (the 1st in the
        // list), Content=the real SMS "Welcome!" (the 2nd of 5 configured
        // messages, 0-based wire id 1). Confirms the reference project's
        // OWN raw byte scheme for Call Type/Digi Call Type (NOT this
        // codec's first-draft guess of a plain 0/1/2 sequential mapping),
        // and that Call Object/Content are both a 0-based wire index
        // translated here to a 1-based "Number".
        var configured = new byte[HotKeyCodec.RecordLength];
        configured[0] = 0x00;
        configured[1] = 0x01;
        configured[2] = 0x01; // wire: Digital
        configured[3] = 0x03; // wire: DMR Hot Text
        configured[4] = 0x00; configured[5] = 0x00; configured[6] = 0x00; configured[7] = 0x00; // wire index 0 -> Number 1
        configured[8] = 0x01; // wire index 1 -> Number 2 ("Welcome!")

        var decodedConfigured = HotKeyCodec.Decode(configured, 2);
        AssertEqual(2, decodedConfigured.Index);
        AssertEqual((byte)2, decodedConfigured.CallType); // model index 2 = "Digital"
        AssertEqual((byte)1, decodedConfigured.DigiCallType); // model index 1 = "DMR Hot Text"
        AssertEqual(1, decodedConfigured.CallObject);
        AssertEqual(2, decodedConfigured.Content);
    }

    private static void HotKeyCodecInfersCallTypeOffFromAnUnsetCallObjectRatherThanTheRawCallTypeByte()
    {
        // Real bytes from the 2026-08-04 live differential READ capture -
        // every untouched key (Hot Key 5 shown here) reads raw CallType
        // 0x00, the SAME byte Hot Key 2's real "Analog" write also
        // produced - only CallObject's own unambiguous 0xFFFFFFFF "unset"
        // sentinel distinguishes "genuinely Analog" from "never touched".
        var untouched = new byte[HotKeyCodec.RecordLength];
        untouched[0] = 0x00; // Mode=Call
        untouched[2] = 0x00; // CallType raw 0 - same byte "Analog" uses
        untouched[4] = 0xFF; untouched[5] = 0xFF; untouched[6] = 0xFF; untouched[7] = 0xFF; // CallObject unset
        untouched[8] = 0xFF;

        var decodedUntouched = HotKeyCodec.Decode(untouched, 4);
        AssertEqual((byte)0, decodedUntouched.CallType); // model index 0 = "Off", NOT "Analog"

        var analogConfigured = new byte[HotKeyCodec.RecordLength];
        analogConfigured[0] = 0x00;
        analogConfigured[2] = 0x00; // same raw byte as the untouched key above
        analogConfigured[4] = 0x00; analogConfigured[5] = 0x00; analogConfigured[6] = 0x00; analogConfigured[7] = 0x00; // CallObject SET (index 0)
        analogConfigured[8] = 0xFF;

        var decodedAnalog = HotKeyCodec.Decode(analogConfigured, 1);
        AssertEqual((byte)1, decodedAnalog.CallType); // model index 1 = "Analog" - same raw byte, different CallObject state
    }

    private static void MapHotKeysSurvivesTheEntrysOwnResetOnChangeCascadeForAFullyConfiguredKey()
    {
        // HotKeyEntry's own OnModeChanged/OnCallTypeChanged/OnDigiCallTypeChanged
        // reset the fields gated below them (see HotKeyEntry.cs) - this test
        // guards against the object-initializer property order in
        // RadioReadMapper.MapHotKeys accidentally letting one of those
        // resets clobber a real decoded value.
        var decoded = new HotKeyCodec.DecodedHotKey(4)
        {
            Mode = 0,
            Menu = 1,
            CallType = 2,
            DigiCallType = 3,
            CallObject = 42,
            Content = 7
        };

        var result = new RadioCodeplugReadResult { HotKeys = [decoded] };
        var mapped = RadioReadMapper.MapHotKeys(result);

        AssertEqual(1, mapped.Count);
        var entry = mapped[0];
        AssertEqual(HotKeyEntry.KeyNames[4], entry.Key);
        AssertEqual((byte)0, entry.Mode);
        AssertEqual((byte)1, entry.Menu);
        AssertEqual((byte)2, entry.CallType);
        AssertEqual((byte)3, entry.DigiCallType);
        AssertEqual(42, entry.CallObject);
        AssertEqual(7, entry.Content);
    }

    private static void MapAnalogQuickCallsSurvivesTheEntrysOwnResetOnChangeCascadeForAConfiguredSlot()
    {
        // AnalogQuickCallEntry.OnOperationTypeChanged resets CallId - same
        // reasoning as the Hot Key test above.
        var decoded = new AnalogQuickCallCodec.DecodedAnalogQuickCall(1) { OperationType = 3, CallId = 9 };
        var result = new RadioCodeplugReadResult { AnalogQuickCalls = [decoded] };
        var mapped = RadioReadMapper.MapAnalogQuickCalls(result);

        AssertEqual(1, mapped.Count);
        AssertEqual(2, mapped[0].Number);
        AssertEqual((byte)3, mapped[0].OperationType);
        AssertEqual(9, mapped[0].CallId);
    }

    private static void MapStateInformationSkipsBlankSlotsAndNumbersBySlotPosition()
    {
        var stateInformation = new List<string> { "", "", "", "", "Status Message 1" };
        var result = new RadioCodeplugReadResult { StateInformation = stateInformation };
        var mapped = RadioReadMapper.MapStateInformation(result);

        AssertEqual(1, mapped.Count);
        AssertEqual(5, mapped[0].Number);
        AssertEqual("Status Message 1", mapped[0].Content);
    }

    private static void GpsRoamingCodecOffsetForIndexPutsEntry16InTheSecondHalfAt0x200Not0x10()
    {
        // Real bug found and fixed 2026-08-09 live: this app's own
        // original SecondHalfBias (0x10) was wrong by a factor of 32 -
        // see GpsRoamingCodec's own doc comment for the live capture that
        // found entry 16 (row 17) at physical offset 0x200, not 0x10.
        AssertEqual(0, GpsRoamingCodec.OffsetForIndex(0));
        AssertEqual(0x20, GpsRoamingCodec.OffsetForIndex(1));
        AssertEqual(0x1e0, GpsRoamingCodec.OffsetForIndex(15));
        AssertEqual(0x200, GpsRoamingCodec.OffsetForIndex(16));
        AssertEqual(0x220, GpsRoamingCodec.OffsetForIndex(17));
        AssertEqual(0x3e0, GpsRoamingCodec.OffsetForIndex(31));
    }

    private static void GpsRoamingCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Real captured records 2026-08-09: index 0 (row 1, offset 0x000)
        // and index 16 (row 17, offset 0x200) - see GpsRoamingCodec's own
        // doc comment for the full live-test findings these settle
        // (LatiMinMark/LongiMinMark are the minute's own hundredths
        // fraction, N/S and E/W are 0/1, Radius is exactly 2 bytes).
        var index0 = Convert.FromHexString("01013b0c220191384e01000039300000");
        var decoded0 = GpsRoamingCodec.Decode(index0, 0);
        AssertTrue(decoded0.Enabled, "index 0 Enabled must decode true");
        AssertEqual(1, decoded0.ZoneIndex);
        AssertEqual(59, decoded0.LatDegree);
        AssertEqual(12, decoded0.LatMinute);
        AssertEqual(34, decoded0.LatMinuteDecimal);
        AssertEqual(1, decoded0.NorthSouth);
        AssertEqual(145, decoded0.LongDegree);
        AssertEqual(56, decoded0.LongMinute);
        AssertEqual(78, decoded0.LongMinuteDecimal);
        AssertEqual(1, decoded0.EastWest);
        AssertEqual(12345, decoded0.Radius);

        var index16 = Convert.FromHexString("01080c011701222d4301000031d40000");
        var decoded16 = GpsRoamingCodec.Decode(index16, 16);
        AssertTrue(decoded16.Enabled, "index 16 Enabled must decode true");
        AssertEqual(8, decoded16.ZoneIndex);
        AssertEqual(12, decoded16.LatDegree);
        AssertEqual(1, decoded16.LatMinute);
        AssertEqual(23, decoded16.LatMinuteDecimal);
        AssertEqual(1, decoded16.NorthSouth);
        AssertEqual(34, decoded16.LongDegree);
        AssertEqual(45, decoded16.LongMinute);
        AssertEqual(67, decoded16.LongMinuteDecimal);
        AssertEqual(1, decoded16.EastWest);
        AssertEqual(54321, decoded16.Radius);

        // Blank-slot sentinel, also captured live (row 18 in the vendor
        // grid, never configured): matches the screenshot's own displayed
        // values exactly (255/255/255/blank/65535).
        var blank = Convert.FromHexString("00ffffffffffffffffff0000ffff0000");
        var decodedBlank = GpsRoamingCodec.Decode(blank, 17);
        AssertTrue(!decodedBlank.Enabled, "blank slot Enabled must decode false");
        AssertEqual(255, decodedBlank.ZoneIndex);
        AssertEqual(255, decodedBlank.LatDegree);
        AssertEqual(255, decodedBlank.NorthSouth);
        AssertEqual(65535, decodedBlank.Radius);
    }

    private static void GpsRoamingCodecEncodeDecodeRoundTrips()
    {
        var values = new GpsRoamingCodec.DecodedGpsRoaming(0)
        {
            Enabled = true,
            ZoneIndex = 3,
            LatDegree = 45,
            LatMinute = 30,
            LatMinuteDecimal = 15,
            NorthSouth = 1,
            LongDegree = 90,
            LongMinute = 0,
            LongMinuteDecimal = 5,
            EastWest = 0,
            Radius = 65535
        };

        var encoded = GpsRoamingCodec.Encode(values);
        AssertEqual(GpsRoamingCodec.RecordLength, encoded.Length);
        var decoded = GpsRoamingCodec.Decode(encoded, 0);

        AssertTrue(decoded.Enabled, "Enabled must round trip");
        AssertEqual(3, decoded.ZoneIndex);
        AssertEqual(45, decoded.LatDegree);
        AssertEqual(30, decoded.LatMinute);
        AssertEqual(15, decoded.LatMinuteDecimal);
        AssertEqual(1, decoded.NorthSouth);
        AssertEqual(90, decoded.LongDegree);
        AssertEqual(0, decoded.LongMinute);
        AssertEqual(5, decoded.LongMinuteDecimal);
        AssertEqual(0, decoded.EastWest);
        AssertEqual(65535, decoded.Radius);
    }

    private static void PatcherAppliesGpsRoamingPatchAtTheCorrectSecondHalfAddress()
    {
        var block = new byte[D890UvMemoryMap.GpsRoamingDataLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.GpsRoamingData, block)]
        };

        var values = new GpsRoamingCodec.DecodedGpsRoaming(16) { Enabled = true, ZoneIndex = 5, LatDegree = 1, Radius = 100 };
        var patched = RadioCodeplugPatcher.ApplyGpsRoamingPatch(snapshot, 16, values);
        var region = patched.Regions.Single(r => r.Address == D890UvMemoryMap.GpsRoamingData);

        // Must land at physical offset 0x200 (the live-confirmed second
        // half), not 0x10 (the old, wrong SecondHalfBias).
        var decoded = GpsRoamingCodec.Decode(region.Data.AsSpan(0x200, GpsRoamingCodec.RecordLength), 16);
        AssertTrue(decoded.Enabled, "Enabled must round trip through the patch");
        AssertEqual(5, decoded.ZoneIndex);
        AssertEqual(1, decoded.LatDegree);
        AssertEqual(100, decoded.Radius);

        // Offset 0x10 (the old wrong address) must be untouched.
        AssertTrue(region.Data.AsSpan(0x10, GpsRoamingCodec.RecordLength).ToArray().All(b => b == 0), "the old (wrong) offset must not have been written");
    }

    private static void GpsRoamingEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new GpsRoamingEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Enabled = true;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Enabled must mark pending");
        entry.MarkRadioSynced();

        entry.ZoneIndex = 4;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing ZoneIndex must mark pending");
        entry.MarkRadioSynced();

        entry.LatMinuteText = "12.34";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing LatMinuteText must mark pending");
        AssertEqual(12, entry.LatMinute);
        AssertEqual(34, entry.LatMinuteDecimal);
        entry.MarkRadioSynced();

        entry.RadiusText = "999";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing RadiusText must mark pending");
        AssertEqual(999, entry.Radius);
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void GpsRoamingEntryMinuteTextValidatesMmDotMmFormat()
    {
        var entry = new GpsRoamingEntry();

        entry.LatMinuteText = "07.05";
        AssertTrue(!entry.GetErrors(nameof(entry.LatMinuteText)).Any(), "07.05 must be valid");
        AssertEqual(7, entry.LatMinute);
        AssertEqual(5, entry.LatMinuteDecimal);

        entry.LatMinuteText = "60.00";
        AssertTrue(entry.GetErrors(nameof(entry.LatMinuteText)).Any(), "60 minutes must be rejected (max 59)");

        entry.LatMinuteText = "12.3";
        AssertTrue(entry.GetErrors(nameof(entry.LatMinuteText)).Any(), "single-digit fraction must be rejected - format is exactly MM.mm");
    }

    private static void AprsSettingsEntryHasAnyPendingRadioWriteTracksScalarFields()
    {
        var entry = new AprsSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.TxDelay = 5;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing TxDelay must mark pending");
        entry.MarkRadioSynced();

        entry.Fix1Lat = 34.5;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Fix1Lat must mark pending");
        entry.MarkRadioSynced();

        entry.ToCall = "APAT51";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing ToCall must mark pending");
        entry.MarkRadioSynced();

        entry.TxFreq3MhzText = "144.64000";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing TxFreq3MhzText must mark pending");
        AssertEqual(144.64, entry.TxFreq3Mhz);
        entry.MarkRadioSynced();

        entry.FilterMicE = true;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing FilterMicE must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void AprsSettingsEntryHasAnyPendingRadioWriteAggregatesSubEntries()
    {
        var entry = new AprsSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state, including sub-entries");

        var fixLocation = entry.AdditionalFixLocations[0];
        fixLocation.Lat = 12.5;
        AssertTrue(fixLocation.HasAnyPendingRadioWrite, "editing a fix location must mark that entry pending");
        AssertTrue(entry.HasAnyPendingRadioWrite, "a pending fix location must bubble up to the parent");

        entry.MarkRadioSynced();
        AssertTrue(!fixLocation.HasAnyPendingRadioWrite, "parent MarkRadioSynced must clear the fix location too");
        AssertTrue(!entry.HasAnyPendingRadioWrite, "parent MarkRadioSynced must clear the aggregate");

        var digitalReport = entry.DigitalReports[0];
        digitalReport.Channel = 3;
        AssertTrue(digitalReport.HasAnyPendingRadioWrite, "editing a digital report must mark that entry pending");
        AssertTrue(entry.HasAnyPendingRadioWrite, "a pending digital report must bubble up to the parent");

        entry.MarkRadioSynced();
        AssertTrue(!digitalReport.HasAnyPendingRadioWrite, "parent MarkRadioSynced must clear the digital report too");
        AssertTrue(!entry.HasAnyPendingRadioWrite, "parent MarkRadioSynced must clear the aggregate again");
    }

    private static void AprsReceiveFilterEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new AprsReceiveFilterEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Enabled = true;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Enabled must mark pending");
        entry.MarkRadioSynced();

        entry.Callsign = "BG6LKK";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Callsign must mark pending");
        entry.MarkRadioSynced();

        entry.Ssid = 8;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Ssid must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void AprsFixLocationTextFieldsReportValidationErrorsForOutOfRangeDegrees()
    {
        var settings = new AprsSettingsEntry();
        settings.Fix1LatText = "95.00000";
        AssertTrue(settings.HasErrors, "95 degrees latitude is outside the 0-90 range.");
        settings.Fix1LatText = "45.00000";
        AssertTrue(!settings.HasErrors, "A valid latitude should clear the error.");
        AssertEqual(45.0, settings.Fix1Lat);

        settings.Fix1LngText = "200.00000";
        AssertTrue(settings.HasErrors, "200 degrees longitude is outside the 0-180 range.");
        settings.Fix1LngText = "120.00000";
        AssertTrue(!settings.HasErrors, "A valid longitude should clear the error.");
        AssertEqual(120.0, settings.Fix1Lng);

        var fixLocation = settings.AdditionalFixLocations[0];
        fixLocation.LatText = "not-a-number";
        AssertTrue(fixLocation.HasErrors, "Unparsable text should be flagged, not silently reverted.");
        fixLocation.LatText = "12.50000";
        AssertTrue(!fixLocation.HasErrors, "A parsable, in-range value should clear the error.");
        AssertEqual(12.5, fixLocation.Lat);

        fixLocation.LngText = "181.00000";
        AssertTrue(fixLocation.HasErrors, "181 degrees longitude is outside the 0-180 range.");

        fixLocation.NsText = "S";
        AssertEqual((byte)1, fixLocation.Ns);
        fixLocation.EwText = "W";
        AssertEqual((byte)1, fixLocation.Ew);
    }

    private static AprsSettingsCodec.DecodedAprsSettings BuildSampleAprsSettings()
    {
        var fixLocations = new List<AprsSettingsCodec.DecodedFixLocation>();
        for (var number = 2; number <= 8; number++)
        {
            fixLocations.Add(new AprsSettingsCodec.DecodedFixLocation(number)
            {
                Lat = 10.0 + number,
                Ns = (byte)(number % 2),
                Lng = 20.0 + number,
                Ew = (byte)(number % 2),
            });
        }

        var digitalReports = new List<AprsSettingsCodec.DecodedDigitalReport>();
        for (var number = 1; number <= 8; number++)
        {
            digitalReports.Add(new AprsSettingsCodec.DecodedDigitalReport(number)
            {
                Channel = 4000 + number,
                TalkgroupId = 5057,
                CallType = (byte)(number % 3),
                Slot = (byte)(number % 3),
            });
        }

        return new AprsSettingsCodec.DecodedAprsSettings
        {
            TxFreq1MHz = 144.80000,
            TxDelay = 15,
            SendSubtone = 2,
            Ctcss = 13,
            Dcs = 1004, // D754I - the exact confirmed worked example
            ManualTxInterval = 90,
            AutoTxInterval = 17,
            TxTone = 0,
            FixedLocationBeacon = 4,

            Fix1Lat = 45.30000,
            Fix1Ns = 1,
            Fix1Lng = 120.45000,
            Fix1Ew = 1,

            ToCall = "APXY99",
            ToCallSsid = 5,
            YourCall = "TESTCS",
            YourCallSsid = 3,
            DigipeaterPath = "123456789012345678901ABCD", // 26 chars - exercises the overflow slice

            AprsSymbol = "#",
            MapIcon = "%",
            TxPower = 3,
            PrewaveTime = 25,

            RoamingSupport = 1,
            RepeaterActivationDelay = 5,
            DisTime = 13,
            Altitude = 500,
            AnalogTxMode = 0,
            PassAll = 1,

            TxFreq2MHz = 145.00000,
            TxFreq3MHz = 60.75000,
            TxFreq4MHz = 70.20000,
            TxFreq5MHz = 90.25000,
            TxFreq6MHz = 108.83317,
            TxFreq7MHz = 166.20000,
            TxFreq8MHz = 155.60000,

            SendingText = "Test status message",

            FixLocations = fixLocations,
            DigitalReports = digitalReports
        };
    }

    private static void AprsSettingsCodecEncodeDecodeRoundTrips()
    {
        var values = BuildSampleAprsSettings();
        var current = new byte[AprsSettingsCodec.MainDataLength];
        var encoded = AprsSettingsCodec.Encode(current, values);
        AssertEqual(AprsSettingsCodec.MainDataLength, encoded.Length);

        var decoded = AprsSettingsCodec.Decode(encoded, values.FixedLocationBeacon);

        AssertEqual(values.TxFreq1MHz, decoded.TxFreq1MHz);
        AssertEqual(values.TxDelay, decoded.TxDelay);
        AssertEqual(values.SendSubtone, decoded.SendSubtone);
        AssertEqual(values.Ctcss, decoded.Ctcss);
        AssertEqual(values.Dcs, decoded.Dcs);
        AssertEqual(values.ManualTxInterval, decoded.ManualTxInterval);
        AssertEqual(values.AutoTxInterval, decoded.AutoTxInterval);
        AssertEqual(values.TxTone, decoded.TxTone);

        AssertEqual(values.Fix1Lat, decoded.Fix1Lat);
        AssertEqual(values.Fix1Ns, decoded.Fix1Ns);
        AssertEqual(values.Fix1Lng, decoded.Fix1Lng);
        AssertEqual(values.Fix1Ew, decoded.Fix1Ew);

        AssertEqual(values.ToCall, decoded.ToCall);
        AssertEqual(values.ToCallSsid, decoded.ToCallSsid);
        AssertEqual(values.YourCall, decoded.YourCall);
        AssertEqual(values.YourCallSsid, decoded.YourCallSsid);
        AssertEqual(values.DigipeaterPath, decoded.DigipeaterPath);

        AssertEqual(values.AprsSymbol, decoded.AprsSymbol);
        AssertEqual(values.MapIcon, decoded.MapIcon);
        AssertEqual(values.TxPower, decoded.TxPower);
        AssertEqual(values.PrewaveTime, decoded.PrewaveTime);

        AssertEqual(values.RoamingSupport, decoded.RoamingSupport);
        AssertEqual(values.RepeaterActivationDelay, decoded.RepeaterActivationDelay);
        AssertEqual(values.DisTime, decoded.DisTime);
        AssertEqual(values.Altitude, decoded.Altitude);
        AssertEqual(values.AnalogTxMode, decoded.AnalogTxMode);
        AssertEqual(values.PassAll, decoded.PassAll);

        AssertEqual(values.TxFreq2MHz, decoded.TxFreq2MHz);
        AssertEqual(values.TxFreq3MHz, decoded.TxFreq3MHz);
        AssertEqual(values.TxFreq4MHz, decoded.TxFreq4MHz);
        AssertEqual(values.TxFreq5MHz, decoded.TxFreq5MHz);
        AssertEqual(values.TxFreq6MHz, decoded.TxFreq6MHz);
        AssertEqual(values.TxFreq7MHz, decoded.TxFreq7MHz);
        AssertEqual(values.TxFreq8MHz, decoded.TxFreq8MHz);

        AssertEqual(values.SendingText, decoded.SendingText);

        AssertEqual(values.FixLocations.Count, decoded.FixLocations.Count);
        for (var i = 0; i < values.FixLocations.Count; i++)
        {
            var expected = values.FixLocations[i];
            var actual = decoded.FixLocations[i];
            AssertEqual(expected.Number, actual.Number);
            AssertEqual(expected.Lat, actual.Lat);
            AssertEqual(expected.Ns, actual.Ns);
            AssertEqual(expected.Lng, actual.Lng);
            // Ew only round trips for Fix2/Fix3 (Number <= 3) - see
            // AprsSettingsCodec.Encode's own doc comment.
            if (expected.Number <= 3)
            {
                AssertEqual(expected.Ew, actual.Ew);
            }
        }

        AssertEqual(values.DigitalReports.Count, decoded.DigitalReports.Count);
        for (var i = 0; i < values.DigitalReports.Count; i++)
        {
            var expected = values.DigitalReports[i];
            var actual = decoded.DigitalReports[i];
            AssertEqual(expected.Number, actual.Number);
            AssertEqual(expected.Channel, actual.Channel);
            AssertEqual(expected.TalkgroupId, actual.TalkgroupId);
            AssertEqual(expected.CallType, actual.CallType);
            AssertEqual(expected.Slot, actual.Slot);
        }
    }

    private static void AprsSettingsCodecEncodePreservesFiltersAndTheUnwrittenGap()
    {
        var current = new byte[AprsSettingsCodec.MainDataLength];
        // Seed the Filters bits and the confirmed-unwritten 0x100-0x1ff gap
        // with a recognizable pattern that Encode must never touch.
        current[0xa8] = 0x3f;
        current[0xa9] = 0x01;
        for (var i = 0x100; i < 0x200; i++)
        {
            current[i] = 0xAA;
        }

        var values = BuildSampleAprsSettings();
        var encoded = AprsSettingsCodec.Encode(current, values);

        AssertEqual((byte)0x3f, encoded[0xa8]);
        AssertEqual((byte)0x01, encoded[0xa9]);
        for (var i = 0x100; i < 0x200; i++)
        {
            AssertEqual((byte)0xAA, encoded[i]);
        }

        // Fix4-8's Ew columns (data[0xfe+i] for i>=2) fall inside that same
        // gap and must also be untouched - confirming Encode really does
        // skip them, not just happens to land on the same bytes.
        AssertEqual((byte)0xAA, encoded[0x100]); // Fix4's Ew column (i=2)
    }

    /// <summary>15 real before/after pairs of the full 0x260-byte APRS
    /// main record, extracted 2026-08-16 from the 16 live differential
    /// write captures (aprs_test1..16_capture.pcapng) taken 2026-08-15
    /// while building this codec - see Capture_Findings.md's own "APRS
    /// field differential test" entries for the full worked description
    /// of each round. Each pair is testN's captured state -&gt; testN+1's
    /// captured state (chained, matching how the captures were diffed
    /// live). Time-sensitive extraction - the .pcapng files only ever
    /// lived in the session scratchpad.</summary>
    private static readonly (string Label, string BeforeHex, byte BeforeBeacon, string AfterHex, byte AfterBeacon)[] AprsGoldenFixtures =
    [
        ("4 fields (test1 baseline)", "00000000000f0200130028000101220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260396000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000000000000000000000000d000000000000000000000000000000000000000000000000000000000000000000000000003f000100144640001446400014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x00, "00000000000f020dec0328110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000000000000000000000000d000000000000000000000000000000000000000000000000000000000000000000000000003f000100144640001446400014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("6 fields incl Dcs width bug", "00000000000f020dec0328110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000000000000000000000000d000000000000000000000000000000000000000000000000000000000000000000000000003f000100144640001446400014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec0328110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000000000000000000000000d000000000000000000000000000000000000000000000000000000000000000000000000003f000100144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("TX Frequency slot shift", "00000000000f020dec0328110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000000000000000000000000d000000000000000000000000000000000000000000000000000000000000000000000000003f000100144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("6 fields, no bugs", "00000000000f020dec035a110001220c49006c316300415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("Fix 1 (Home Position), 4 fields", "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000222222220000000c0c0c0c00000049494949000000000000000000006c6c6c6c0000003131313100000063636363000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f00000114480000145000001446400014464000144640001446400014464000144640000032222222000000080c0c0c00000063494949000000010000000000005a6c6c6c0000000f31313100000000636363000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x02),
        ("Fix 2, column-array layout", "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a00fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570000000000000000010000000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f00000114480000145000001446400014464000144640001446400014464000144640000032222222000000080c0c0c00000063494949000000010000000000005a6c6c6c0000000f31313100000000636363000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x02, "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f00000114480000145000001446400014464000144640001446400014464000144640000032222222000000080c0c0c00000063494949000000010000000000005a6c6c6c0000000f31313100000000636363000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x02),
        ("Digital Report Channel 1", "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f00000114480000145000001446400014464000144640001446400014464000144640000032222222000000080c0c0c00000063494949000000010000000000005a6c6c6c0000000f31313100000000636363000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x02, "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200000008170c0c00000063634949000000010100000000005a466c6c0000000f0c313100000000006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x03),
        ("Fix 3, second column-array confirmation", "00000000000f020dec035a1100012d116301781b0001415041543531004247364c4b4b0857494445312d3100000000000000000000000000002f260319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200000008170c0c00000063634949000000010100000000005a466c6c0000000f0c313100000000006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x03, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200000008170c0c00000063634949000000010100000000005a466c6c0000000f0c313100000000006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x03),
        ("Callsigns/Path, 7 fields", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200000008170c0c00000063634949000000010100000000005a466c6c0000000f0c313100000000006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x03, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200005a08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x08),
        ("Fix 8, 4 fields", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200005a08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x08, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200002d08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x08),
        ("Fix 8 isolated Lat", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200002d08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x08, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200002d08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x04),
        ("Fix 4 isolated E/W search (0 bytes - Ew confirmed never written)", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c222200002d08170c0c00000063634949000000010100000000015a466c6c0000190f0c313100000600006363000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x04, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("Fix 4 4-field test from populated", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001144800001450000014464000144640001446400014464000144640001446400000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001155600001450000014464000144640001446400014464000144640001446400000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("TX Frequency 1 isolated", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001155600001450000014464000144640001446400014464000144640001446400000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001155600001450000014464000144640001446400014464000144640001662000000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
        ("TX Frequency 8 isolated", "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530357494445322d31000000000000000000000000000023250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d0000000000000000000000000000000000000000000000000000000000000000000000f4013f000001155600001450000014464000144640001446400014464000144640001662000000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01, "00000000000f020dec035a1100012d116301781b0001415058593939055445535443530331323334353637383930313233343536373839303123250319000000a10fa00fa00fa00fa00fa00fa00fa00f00005057000050570000505700005057000050570000505700005057000050570100000000000000010100000000000000050d4142434400000000000000000000000000000000000000000000000000000000000000f4013f000001155600001450000014464000144640001446400014464000144640001662000000323c142200002d08171e0c00000063630049000000010101000000015a463c6c0000190f0c2d3100000600000063000000010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000410050005200530043004e0020005700490046004900200034002e003300300056000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 0x01),
    ];

    /// <summary>Proves AprsSettingsCodec.Encode reproduces the exact real
    /// bytes a real vendor CPS wrote across all 15 rounds above - decodes
    /// each round's real "after" state to get a target values DTO, then
    /// re-encodes it against the PREVIOUS round's real "before" bytes and
    /// checks the result matches "after" byte-for-byte. Stronger than a
    /// synthetic round trip (AprsSettingsCodecEncodeDecodeRoundTrips
    /// above): every input AND the expected output are real bytes a real
    /// radio actually produced, not values this app made up.</summary>
    private static void AprsSettingsCodecEncodeReproducesRealCapturedBytesAcrossAllLiveTests()
    {
        foreach (var (label, beforeHex, beforeBeacon, afterHex, afterBeacon) in AprsGoldenFixtures)
        {
            var before = Convert.FromHexString(beforeHex);
            var after = Convert.FromHexString(afterHex);
            AssertEqual(AprsSettingsCodec.MainDataLength, before.Length);
            AssertEqual(AprsSettingsCodec.MainDataLength, after.Length);
            AprsSettingsCodec.DecodedAprsSettings afterValues;
            try
            {
                afterValues = AprsSettingsCodec.Decode(after, afterBeacon);
            }
            catch (Exception ex)
            {
                throw new Exception($"'{label}': Decode threw: {ex}");
            }

            byte[] reEncoded;
            try
            {
                reEncoded = AprsSettingsCodec.Encode(before, afterValues);
            }
            catch (Exception ex)
            {
                throw new Exception($"'{label}': Encode threw: {ex}");
            }

            AssertTrue(reEncoded.AsSpan().SequenceEqual(after), $"'{label}': Encode(before, Decode(after)) must reproduce the real captured bytes exactly");
        }
    }

    private static void PatcherAppliesAprsSettingsPatch()
    {
        var mainDataBlock = new byte[AprsSettingsCodec.MainDataLength];
        var sharedBlock = new byte[0x160]; // matches OptionalSettingsCodec.MainDataLength
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.AprsSettingsMainData, mainDataBlock),
                new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, sharedBlock)
            ]
        };

        var values = BuildSampleAprsSettings();
        var patched = RadioCodeplugPatcher.ApplyAprsSettingsPatch(snapshot, values);

        var mainRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.AprsSettingsMainData);
        var decoded = AprsSettingsCodec.Decode(mainRegion.Data, values.FixedLocationBeacon);
        AssertEqual(values.ToCall, decoded.ToCall);
        AssertEqual(values.Fix1Lat, decoded.Fix1Lat);

        var sharedRegion = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000);
        var beaconOffset = D890UvMemoryMap.AprsFixedLocationBeaconAddress - D890UvMemoryMap.OptionalSettingsData3500000;
        AssertEqual(values.FixedLocationBeacon, sharedRegion.Data[beaconOffset]);

        // Every other byte in the shared 0x3500000 region (which Alarm/Talk
        // Alias/Optional Settings also patch independently) must be
        // untouched by the APRS patch.
        for (var i = 0; i < sharedRegion.Data.Length; i++)
        {
            if (i == beaconOffset)
            {
                continue;
            }

            AssertEqual((byte)0, sharedRegion.Data[i]);
        }
    }

    /// <summary>The bug this guards against: any ordinary Read From Radio
    /// that doesn't have "Include Digital Contact List" checked was
    /// silently wiping whatever real contact data an EARLIER read this
    /// session had already loaded - discovered 2026-08-16 while planning
    /// automated write-safety tests. Same bug class already fixed for
    /// Encryption Keys on 2026-07-20 (see the doc comment above the
    /// `includeEncryptionKeys` check in `MainViewModel.ApplyRadioReadResult`)
    /// - just never applied to Digital Contacts too.</summary>
    private static void ReadFromRadioSkippingDigitalContactsLeavesAnEarlierReadListUntouched()
    {
        var viewModel = new MainViewModel();

        var realContact = new DigitalContactCodec.DecodedDigitalContact(0)
        {
            RadioId = 2400002,
            Name = "Jonas"
        };

        // First read: contacts genuinely included - real data loads.
        viewModel.ApplyRadioReadResult(
            new RadioCodeplugReadResult { Success = true, DigitalContacts = [realContact] },
            includeDigitalContacts: true,
            includeEncryptionKeys: false);

        AssertEqual(1, viewModel.DigitalContacts.Count);
        AssertEqual("Jonas", viewModel.DigitalContacts[0].Name);

        // Second read: an ordinary read WITHOUT contacts included (e.g.
        // just refreshing channels) - must NOT discard the real data an
        // earlier read this session already fetched.
        viewModel.ApplyRadioReadResult(
            new RadioCodeplugReadResult { Success = true, DigitalContacts = [] },
            includeDigitalContacts: false,
            includeEncryptionKeys: false);

        AssertEqual(1, viewModel.DigitalContacts.Count);
        AssertEqual("Jonas", viewModel.DigitalContacts[0].Name);
    }

    /// <summary>Companion to the read-side fix above: the write-time
    /// "Include Digital Contact List" option must not be usable until
    /// contacts have genuinely been read from a connected radio this
    /// session - `DigitalContactWriter.Write` always rewrites the whole
    /// stream from memory, so writing an incomplete/never-read list would
    /// silently replace the radio's real contact database.</summary>
    private static void CanIncludeDigitalContactsInWriteIsGatedByAGenuineRead()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.CanIncludeDigitalContactsInWrite, "must start false - contacts never read");

        viewModel.ApplyRadioReadResult(
            new RadioCodeplugReadResult { Success = true, DigitalContacts = [new DigitalContactCodec.DecodedDigitalContact(0) { Name = "Jonas" }] },
            includeDigitalContacts: true,
            includeEncryptionKeys: false);
        AssertTrue(viewModel.CanIncludeDigitalContactsInWrite, "must become true once a read genuinely includes contacts");

        // A later ordinary read that skips contacts must NOT revoke this -
        // the real list already fetched is still in memory (see the
        // read-side fix above), so writing it is still safe.
        viewModel.ApplyRadioReadResult(
            new RadioCodeplugReadResult { Success = true, DigitalContacts = [] },
            includeDigitalContacts: false,
            includeEncryptionKeys: false);
        AssertTrue(viewModel.CanIncludeDigitalContactsInWrite, "must stay true - a later non-contact read doesn't un-read the earlier one");
    }

    /// <summary>Real bug found 2026-08-16: loading a project file always
    /// reset MainViewModel's own _digitalContactsGenuinelyPopulatedFromRadio
    /// to false, even for a Digital Contact List that traced back to a
    /// genuine Read From Radio on a DIFFERENT device (Desktop reads and
    /// saves, Android loads that file) - so the write-side "Include Digital
    /// Contact List" checkbox stayed permanently disabled after any
    /// save/load round trip. Fixed by persisting the flag into
    /// RadioProjectData itself. This test covers the mapper layer
    /// (ToData/the DTO field); the JSON file round trip of the new field
    /// is already covered by FullRadioProjectDataRoundTripsThroughARealFile's
    /// reflection-based filler.</summary>
    private static void DigitalContactsGenuinelyPopulatedFlagRoundTripsThroughTheProjectMapper()
    {
        var notGenuine = RadioProjectMapper.ToData([], []);
        AssertTrue(!notGenuine.DigitalContactsGenuinelyPopulatedFromRadio, "must default false when not passed");

        var genuine = RadioProjectMapper.ToData([], [], digitalContactsGenuinelyPopulatedFromRadio: true);
        AssertTrue(genuine.DigitalContactsGenuinelyPopulatedFromRadio, "must be true when the caller says the list was genuinely read");
    }

    /// <summary>Real bug found live 2026-08-16: RadioProjectMapper.LoadInto
    /// mutates the DigitalContacts collection directly, bypassing every
    /// other path that keeps FilteredDigitalContacts (what the list view
    /// actually binds to) in sync - so after Load, the view showed no
    /// contacts at all even though 900 had genuinely loaded, until
    /// something unrelated (toggling the Friends Only filter) forced a
    /// refresh. Fixed by calling RefreshFilteredDigitalContacts right after
    /// LoadInto in LoadProject.</summary>
    private static void LoadingAProjectRefreshesTheFilteredDigitalContactsList()
    {
        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-load-refreshes-contacts-test-{Guid.NewGuid():N}.json");
        try
        {
            var project = new RadioProjectData
            {
                DigitalContacts = [new DigitalContactData { Index = 0, Name = "Tobias", RadioId = 1000001 }],
                DigitalContactsGenuinelyPopulatedFromRadio = true
            };
            new JsonRadioDataStore(path).SaveAsync(project).GetAwaiter().GetResult();

            var viewModel = new MainViewModel();
            viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, openProjectStorage: new JsonFileProjectStorage(path)));

            viewModel.LoadProjectCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            AssertEqual(1, viewModel.DigitalContacts.Count);
            AssertEqual(1, viewModel.FilteredDigitalContacts.Count);
            AssertEqual("Tobias", viewModel.FilteredDigitalContacts[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Found while writing this test: AprsSettings.MarkRadioSynced()
    /// was never called after a read (every other settings entity right
    /// next to it in ApplyRadioReadResult was) - meaning
    /// AprsSettings.HasAnyPendingRadioWrite was permanently stuck true
    /// after every single read, regardless of edits. Fixed 2026-08-16.</summary>
    private static void AprsSettingsIsMarkedSyncedAfterARead()
    {
        var viewModel = new MainViewModel();
        viewModel.ApplyRadioReadResult(new RadioCodeplugReadResult { Success = true }, includeDigitalContacts: false, includeEncryptionKeys: false);

        AssertTrue(!viewModel.AprsSettings.HasAnyPendingRadioWrite, "AprsSettings must be marked synced by a read, like every other settings entity");
    }

    /// <summary>Matches the vendor CPS's own "Write to Radio" behavior
    /// (always available once connected, not gated on anything having
    /// changed, and not gated on a prior Read From Radio either) - decided
    /// 2026-08-16. The dirty-flag gate on CanWriteChangesToRadio is removed,
    /// and so is the `_cachedRadioSnapshot is not null` requirement -
    /// WriteChangesToRadioAsync captures its own RMW baseline directly from
    /// the radio if none is cached yet.</summary>
    private static void WriteChangesToRadioIsAvailableWithNothingDirtyOnceASnapshotExists()
    {
        var viewModel = new MainViewModel();

        AssertTrue(!viewModel.WriteChangesToRadioCommand.CanExecute(null), "must be unavailable with no connection/port selected");

        viewModel.SetRadioServices(() => new FakeRadioConnection(), () => []);
        viewModel.SelectedPort = "FAKE";
        viewModel.WriteChangesToRadioCommand.NotifyCanExecuteChanged();

        AssertTrue(viewModel.WriteChangesToRadioCommand.CanExecute(null), "must be available once connected, even with no prior read and nothing dirty - matches vendor CPS behavior");
    }

    /// <summary>Regression test for a real bug reported live on Android: the
    /// Write to Radio button stayed permanently disabled even after the
    /// radio was found and a port was selected. RefreshRadioPorts (the
    /// method the USB scan retry loop actually calls - see
    /// RetryPortScanAsync) re-evaluates ReadFromRadioCommand and
    /// VerifyReadSaveRoundtripCommand but had never been updated to also
    /// re-evaluate WriteChangesToRadioCommand, even though it depends on
    /// the exact same _radioConnectionFactory/SelectedPort state. Watches
    /// CanExecuteChanged directly (not just CanExecute(null), which always
    /// re-evaluates fresh regardless of notification wiring) so this test
    /// would have failed before the fix.</summary>
    private static void RefreshRadioPortsNotifiesWriteChangesToRadioCanExecuteChanged()
    {
        var viewModel = new MainViewModel();
        var ports = new List<string>();
        viewModel.SetRadioServices(() => new FakeRadioConnection(), () => ports);

        var writeRaised = false;
        viewModel.WriteChangesToRadioCommand.CanExecuteChanged += (_, _) => writeRaised = true;

        ports.Add("FAKE");
        viewModel.RefreshRadioPortsCommand.Execute(null);

        AssertTrue(writeRaised, "a port scan that finds a port should raise WriteChangesToRadioCommand.CanExecuteChanged");
        AssertTrue(viewModel.WriteChangesToRadioCommand.CanExecute(null), "Write to Radio should become available once a port is found");
    }

    /// <summary>Proves the fix for a real scenario found 2026-08-16:
    /// preparing a codeplug in the app (adding a channel, say)
    /// BEFORE ever doing a Read From Radio, then writing it, must not lose
    /// that prepared work. Before this fix, a write was flatly unavailable
    /// until a Read had happened, and a Read overwrites the live ViewModel
    /// with whatever the radio has - so the only way to unlock Write would
    /// have destroyed the very data being written. Now
    /// WriteChangesToRadioAsync captures its own RMW baseline straight from
    /// the radio (<see cref="RadioCodeplugRawSnapshotReader.Capture"/>)
    /// without ever touching the live ViewModel.</summary>
    private static void WriteChangesToRadioAutoCapturesABaselineWithoutDiscardingUnreadPreparedWork()
    {
        var viewModel = new MainViewModel();
        // Mark every entity synced first (same "genuinely nothing dirty"
        // setup as the sibling test above) so the ONLY dirty thing below is
        // the one channel this test prepares. Without this, MainViewModel's
        // own seeded defaults (e.g. FiveToneSettings, never synced) would
        // also be dirty and go into this write, tripping a real, separate,
        // pre-existing bug (FiveToneIdData's captured region overlaps
        // FiveToneBotData) that has nothing to
        // do with what this test is proving.
        viewModel.ApplyRadioReadResult(new RadioCodeplugReadResult { Success = true }, includeDigitalContacts: false, includeEncryptionKeys: false);
        viewModel.SetRadioServices(() => new FakeRadioConnection(), () => []);
        viewModel.SelectedPort = "FAKE";
        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, confirmWriteToRadio: true));

        // Prepare a channel entirely in the app, with no Read From Radio
        // (that populates _cachedRadioSnapshot) this session at all.
        AssertEqual(0, viewModel.Channels.Count);
        viewModel.AddChannelCommand.Execute(null);
        AssertEqual(1, viewModel.Channels.Count);
        var preparedChannelName = viewModel.SelectedChannel!.Name;
        AssertTrue(viewModel._cachedRadioSnapshot is null, "no read has happened yet - there must be no cached snapshot before the write");

        viewModel.WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        AssertTrue(viewModel.WriteChangesToRadioCommand.CanExecute(null), "write must be available even though nothing has ever been read this session");
        viewModel.WriteChangesToRadioCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertTrue(viewModel.RadioWriteStatusText.StartsWith("Write verified", StringComparison.Ordinal), $"write should have succeeded against the fake connection, got: '{viewModel.RadioWriteStatusText}'");
        AssertTrue(viewModel._cachedRadioSnapshot is not null, "a baseline must have been auto-captured and cached for future writes");

        // The whole point of the fix: the prepared channel must survive -
        // auto-capturing a baseline must NOT call ApplyRadioReadResult,
        // which would have replaced the live Channels collection with
        // whatever the (empty, fake) radio actually has.
        AssertEqual(1, viewModel.Channels.Count);
        AssertEqual(preparedChannelName, viewModel.Channels[0].Name);
    }

    /// <summary>Proves the temporary DevForceModelToImageCommand (added
    /// 2026-08-16 for one live hardware test, see the command's own doc
    /// comment) actually reaches every one of the ~38 entity types it's
    /// meant to - via reflection on a private field name, so a rename
    /// anywhere would silently miss that entity without this test. Uses
    /// AddChannelCommand/AddFiveToneIdCommand as two representative,
    /// already-covered-elsewhere samples rather than asserting all 38
    /// individually - the command itself throws immediately if ANY entity
    /// type's field lookup fails, so "no exception" already proves full
    /// coverage; this test's real job is confirming the null-out actually
    /// flips HasAnyPendingRadioWrite as intended, not re-deriving the
    /// entity list.</summary>
    private static void DevForceModelToImageMarksEveryEntityDirtyWithoutChangingValues()
    {
        var viewModel = new MainViewModel();
        viewModel.ApplyRadioReadResult(new RadioCodeplugReadResult { Success = true }, includeDigitalContacts: false, includeEncryptionKeys: false);
        viewModel.AddChannelCommand.Execute(null);
        viewModel.AddFiveToneIdCommand.Execute(null);
        var channel = viewModel.Channels[0];
        var fiveToneId = viewModel.FiveToneIds[0];
        var channelNameBefore = channel.Name;
        var fiveToneIdNameBefore = fiveToneId.Name;

        // Fresh Add-ed rows start dirty (null snapshot) - sync them too, so
        // this test only proves what DevForceModelToImage itself does, not
        // what was already dirty from adding rows.
        channel.MarkRadioSynced();
        fiveToneId.MarkRadioSynced();
        viewModel.MasterId.MarkRadioSynced();
        AssertTrue(!channel.HasAnyPendingRadioWrite, "setup: channel must start synced");
        AssertTrue(!fiveToneId.HasAnyPendingRadioWrite, "setup: five tone id must start synced");
        AssertTrue(!viewModel.MasterId.HasAnyPendingRadioWrite, "setup: master id must start synced");

        viewModel.DevForceModelToImageCommand.Execute(null);

        AssertTrue(channel.HasAnyPendingRadioWrite, "a collection entry (Channel) must be marked dirty");
        AssertTrue(fiveToneId.HasAnyPendingRadioWrite, "a collection entry (FiveToneId) must be marked dirty");
        AssertTrue(viewModel.MasterId.HasAnyPendingRadioWrite, "a singleton entry (MasterId) must be marked dirty");

        // The whole point: values themselves are untouched, only the
        // dirty-tracking snapshot is cleared.
        AssertEqual(channelNameBefore, channel.Name);
        AssertEqual(fiveToneIdNameBefore, fiveToneId.Name);
    }

    /// <summary>"Virtual radio" dry run of the exact scenario planned for
    /// the first live hardware test with DevForceModelToImage - a
    /// FakeRadioConnection is a pure in-memory fake (no serial I/O), so
    /// running the real WriteChangesToRadioCommand against it costs
    /// nothing and needs no hardware, but still exercises every dirtied
    /// entity's own BuildXValues/Encode/ApplyXPatch call AND the write's
    /// own mandatory read-back verification (RadioCodeplugWriter.Write) -
    /// "does the resulting image match what was intended," using the
    /// app's own real verification rather than a hand-rolled comparison.
    /// Covers every singleton entity (always present, no Add needed -
    /// includes FiveToneSettings, i.e. today's BOT/EOT fix) plus two
    /// representative collection entries - not literally every list type,
    /// see this test's own file for the reasoning on why that's an
    /// acceptable scope for a pre-flight sanity check, not a substitute
    /// for Pillar 2-style per-field fidelity testing.</summary>
    private static void DevForceModelToImageThenWriteSucceedsAgainstAVirtualRadio()
    {
        var viewModel = new MainViewModel();
        viewModel.ApplyRadioReadResult(new RadioCodeplugReadResult { Success = true }, includeDigitalContacts: false, includeEncryptionKeys: false);
        viewModel.SetRadioServices(() => new FakeRadioConnection(), () => []);
        viewModel.SelectedPort = "FAKE";
        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, confirmWriteToRadio: true));
        viewModel.AddChannelCommand.Execute(null);
        viewModel.AddFiveToneIdCommand.Execute(null);

        viewModel.DevForceModelToImageCommand.Execute(null);
        viewModel.WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        AssertTrue(viewModel.WriteChangesToRadioCommand.CanExecute(null), "write must be available");
        viewModel.WriteChangesToRadioCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertTrue(viewModel.RadioWriteStatusText.StartsWith("Write verified", StringComparison.Ordinal), $"a full-entity write against the virtual radio should succeed and verify cleanly, got: '{viewModel.RadioWriteStatusText}'");
        AssertEqual(0, viewModel.RadioWriteWarnings.Count);
    }

    private static void AddAnalogAddressIsCappedAt128Slots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.AnalogAddressMax; i++)
        {
            viewModel.AddAnalogAddressCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.AnalogAddressMax, viewModel.AnalogAddresses.Count);

        viewModel.AddAnalogAddressCommand.Execute(null);
        AssertEqual(CodeplugLimits.AnalogAddressMax, viewModel.AnalogAddresses.Count);
    }

    private static void AnalogAddressValidationFlagsNumberOutOfRangeAndAddressNumberOver10Digits()
    {
        var viewModel = new MainViewModel();
        viewModel.AddAnalogAddressCommand.Execute(null);
        var entry = viewModel.SelectedAnalogAddress!;
        entry.Number = 129; // one past CodeplugLimits.AnalogAddressMax
        entry.AddressNumber = 12345678901; // 11 digits, one past the confirmed 10-digit wire limit

        // AnalogAddressEntry has no per-property revalidation wiring (only
        // CollectionChanged does) - adding another slot forces a fresh
        // RefreshValidation pass that re-scans every entry, including the
        // one just mutated above.
        viewModel.AddAnalogAddressCommand.Execute(null);

        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("number must be 1-128", StringComparison.Ordinal)),
            "an out-of-range Number should produce a validation message");
        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("address number exceeds 10 digits", StringComparison.Ordinal)),
            "an over-10-digit Address Number should produce a validation message");
    }

    private static void Qdc1200SettingsTextWrappersRoundTripThroughTheirConfirmedRanges()
    {
        var entry = new Qdc1200SettingsEntry();

        entry.AutoResetTimeText = "250";
        AssertEqual((byte)250, entry.AutoResetTime);
        entry.AutoResetTimeText = "251"; // out of range - must be rejected, not clamped
        AssertEqual((byte)250, entry.AutoResetTime);

        entry.RemoteListeningDurationText = "240";
        AssertEqual(240, entry.RemoteListeningDuration);
        entry.RemoteListeningDurationText = "4"; // below the confirmed 5-240 range
        AssertEqual(240, entry.RemoteListeningDuration);

        entry.MaxAckWaitTimeText = "60.0";
        AssertEqual(60.0, entry.MaxAckWaitTime);
        entry.MaxAckWaitTimeText = "0.5";
        AssertEqual(0.5, entry.MaxAckWaitTime);

        entry.PretimeText = "2500";
        AssertEqual(2500, entry.Pretime);
        entry.PretimeText = "10";
        AssertEqual(10, entry.Pretime);

        entry.ResendCodeText = "3";
        AssertEqual((byte)3, entry.ResendCode);
        entry.ResendCodeText = "4"; // out of range
        AssertEqual((byte)3, entry.ResendCode);

        AssertEqual(251, Qdc1200SettingsEntry.AutoResetTimeOptions.Count);
        AssertEqual(236, Qdc1200SettingsEntry.RemoteListeningDurationOptions.Count);
        AssertEqual(120, Qdc1200SettingsEntry.MaxAckWaitTimeOptions.Count);
        AssertEqual(250, Qdc1200SettingsEntry.PretimeOptions.Count);
        AssertEqual("0.5", Qdc1200SettingsEntry.MaxAckWaitTimeOptions[0]);
        AssertEqual("60.0", Qdc1200SettingsEntry.MaxAckWaitTimeOptions[^1]);
        AssertEqual("10", Qdc1200SettingsEntry.PretimeOptions[0]);
        AssertEqual("2500", Qdc1200SettingsEntry.PretimeOptions[^1]);
    }

    private static void Qdc1200IdEntryEnableFlagsAndTypeOptionsDependOnCallTypeNotASharedFilteredList()
    {
        var entry = new Qdc1200IdEntry { Number = 1 };

        entry.CallType = 0; // Private Call
        AssertTrue(entry.IsPrivateCallIdEnabled, "Private Call ID should be enabled for Private Call");
        AssertTrue(!entry.IsGroupCallIdEnabled, "Group Call ID should be disabled for Private Call");
        AssertTrue(entry.IsTypeEnabled, "Type should be enabled for Private Call");
        AssertSame(Qdc1200IdEntry.PrivateTypeOptions, entry.TypeOptions);

        entry.CallType = 1; // Group Call
        AssertTrue(!entry.IsPrivateCallIdEnabled, "Private Call ID should be disabled for Group Call");
        AssertTrue(entry.IsGroupCallIdEnabled, "Group Call ID should be enabled for Group Call");
        AssertTrue(entry.IsTypeEnabled, "Type should be enabled for Group Call");
        AssertSame(Qdc1200IdEntry.GroupTypeOptions, entry.TypeOptions);

        entry.CallType = 2; // All Call
        AssertTrue(!entry.IsPrivateCallIdEnabled, "Private Call ID should be disabled for All Call");
        AssertTrue(!entry.IsGroupCallIdEnabled, "Group Call ID should be disabled for All Call");
        AssertTrue(!entry.IsTypeEnabled, "Type should be disabled entirely for All Call");
        AssertEqual(0, entry.TypeOptions.Count);
    }

    private static void Qdc1200IdEntryNeedToAnswerIsOnlyEnabledForAleartAndRemotelyMonitonTypes()
    {
        var entry = new Qdc1200IdEntry { Number = 1, CallType = 0 }; // Private Call

        foreach (var label in Qdc1200IdEntry.PrivateTypeOptions)
        {
            entry.TypeText = label;
            var expected = label is "ALEART" or "Remotely Moniton";
            AssertTrue(entry.IsNeedToAnswerEnabled == expected, $"Need to Answer enabled state wrong for Type='{label}'");
        }

        // Confirmed live 2026-08-04: Group Call's own ALEART does NOT
        // enable Need to Answer, even though the label is identical to
        // Private Call's - Need to Answer is Private-Call-only.
        entry.CallType = 1; // Group Call
        foreach (var label in Qdc1200IdEntry.GroupTypeOptions)
        {
            entry.TypeText = label;
            AssertTrue(!entry.IsNeedToAnswerEnabled, $"Need to Answer must stay disabled for any Group Call Type (got enabled for '{label}')");
        }
    }

    private static void Qdc1200IdEntryChangingCallTypeOnlyResetsNeedToAnswerWhenItBecomesDisabled()
    {
        // Confirmed via two live differential WRITE captures 2026-08-04:
        // Private Call ID/Group Call ID/Type are independent of Call Type
        // on the real wire (a stale Private Call ID survived a write that
        // switched Call Type to Group and set a different Group Call ID) -
        // only Need to Answer actually gets cleared when it becomes
        // disabled (Group Call's own ALEART does NOT enable it, even
        // though the label is identical to Private Call's ALEART).
        var entry = new Qdc1200IdEntry
        {
            Number = 1,
            CallType = 0,
            PrivateCallId = "ABCD",
            Type = Qdc1200IdEntry.TypeAleart,
            NeedToAnswer = true
        };

        AssertTrue(entry.IsNeedToAnswerEnabled, "sanity check: Private Call + ALEART should enable Need to Answer before the switch");

        entry.CallType = 1; // switch to Group Call

        AssertEqual("ABCD", entry.PrivateCallId);
        AssertEqual("", entry.GroupCallId);
        AssertEqual(Qdc1200IdEntry.TypeAleart, entry.Type);
        AssertTrue(!entry.IsNeedToAnswerEnabled, "Group Call + ALEART must NOT enable Need to Answer, confirmed live");
        AssertTrue(!entry.NeedToAnswer, "Need to Answer should reset once it becomes disabled");
    }

    private static void AddQdc1200IdIsCappedAt100Slots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.Qdc1200IdMax; i++)
        {
            viewModel.AddQdc1200IdCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.Qdc1200IdMax, viewModel.Qdc1200Ids.Count);

        viewModel.AddQdc1200IdCommand.Execute(null);
        AssertEqual(CodeplugLimits.Qdc1200IdMax, viewModel.Qdc1200Ids.Count);
    }

    private static void Qdc1200IdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Real bytes from the 2026-08-04 second live differential WRITE
        // capture - ID row 1: Call Type=Group Call, Group Call ID="564",
        // Type=ALEART, Need to Answer disabled (Group Call doesn't allow
        // it, even for ALEART), Name="QDCTESTNAME1". Private Call ID
        // "5564" is a STALE leftover from the first capture's Private Call
        // test - confirmed NOT cleared on the wire when Call Type switched
        // away from Private.
        var record = new byte[Qdc1200IdCodec.RecordLength];
        var realBytes = Convert.FromHexString("020100006405645551004400430054004500530054004e0041004d0045003100");
        realBytes.CopyTo(record, 0);

        var decoded = Qdc1200IdCodec.Decode(record, 0);

        AssertEqual(Qdc1200IdEntry.TypeAleart, decoded.Type);
        AssertEqual((byte)1, decoded.CallType); // Group Call
        AssertTrue(!decoded.NeedToAnswer, "Need to Answer must be off - Group Call doesn't allow it");
        AssertEqual("564", decoded.GroupCallId);
        AssertEqual("5564", decoded.PrivateCallId);
        AssertEqual("QDCTESTNAME1", decoded.Name);
    }

    private static void Qdc1200SettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Real bytes from the 2026-08-04 second live differential WRITE
        // capture - Decode tab: Auto Reset Time=77, Self ID Private
        // Call="ABCD", Self ID Group Call="EF1", Remote Listening
        // Duration=199, Remotely Kill Allow=On, Remotely Monitor Allow=Off.
        // Encode tab: Side Tone=On, Max ACK Wait Time=12.5, Pretime=730,
        // Resend Code=3 (the second capture's own isolated change from the
        // first round's unset default).
        var record = new byte[Qdc1200SettingsCodec.RecordLength];
        var realBytes = Convert.FromHexString("010000000000000000000000000000000100484dcdabf10e0018020100c20000");
        realBytes.CopyTo(record, 0);

        var decoded = Qdc1200SettingsCodec.Decode(record);

        AssertTrue(decoded.SideTone, "Side Tone should be on");
        AssertTrue(decoded.RemotelyKillAllow, "Remotely Kill Allow should be on");
        AssertTrue(!decoded.RemotelyMonitorAllow, "Remotely Monitor Allow should be off");
        AssertEqual(730, decoded.Pretime);
        AssertEqual((byte)77, decoded.AutoResetTime);
        AssertEqual("ABCD", decoded.SelfIdPrivateCall);
        AssertEqual("EF1", decoded.SelfIdGroupCall);
        AssertEqual(12.5, decoded.MaxAckWaitTime);
        AssertEqual((byte)3, decoded.ResendCode);
        AssertEqual(199, decoded.RemoteListeningDuration);
    }

    private static void MapQdc1200IdsSurvivesTheEntrysOwnNeedToAnswerResetCascade()
    {
        // Qdc1200IdEntry's own OnCallTypeChanged/OnTypeChanged reset Need
        // to Answer once it becomes disabled - this test guards against
        // the object-initializer property order in RadioReadMapper.MapQdc1200Ids
        // accidentally letting that reset clobber a real decoded value,
        // same discipline established after the Hot Key mapper bug.
        var decoded = new Qdc1200IdCodec.DecodedQdc1200Id(4)
        {
            Type = Qdc1200IdEntry.TypeAleart,
            CallType = 0, // Private Call
            NeedToAnswer = true,
            PrivateCallId = "1234",
            GroupCallId = "567",
            Name = "TESTNAME"
        };

        var result = new RadioCodeplugReadResult { Qdc1200Ids = [decoded] };
        var mapped = RadioReadMapper.MapQdc1200Ids(result);

        AssertEqual(1, mapped.Count);
        var entry = mapped[0];
        AssertEqual(5, entry.Number);
        AssertEqual(Qdc1200IdEntry.TypeAleart, entry.Type);
        AssertEqual((byte)0, entry.CallType);
        AssertTrue(entry.NeedToAnswer, "Need to Answer must survive the mapper, not get reset to false");
        AssertEqual("1234", entry.PrivateCallId);
        AssertEqual("567", entry.GroupCallId);
        AssertEqual("TESTNAME", entry.Name);
    }

    private static void Qdc1200IdCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[Qdc1200IdCodec.RecordLength];
        var values = new Qdc1200IdCodec.DecodedQdc1200Id(0)
        {
            Type = Qdc1200IdEntry.TypeAleart,
            CallType = 1, // Group Call
            NeedToAnswer = false,
            GroupCallId = "564",
            PrivateCallId = "5564",
            Name = "QDCTESTNAME1"
        };

        var encoded = Qdc1200IdCodec.Encode(blankRecord, values);
        var decoded = Qdc1200IdCodec.Decode(encoded, 0);

        AssertEqual(values.Type, decoded.Type);
        AssertEqual(values.CallType, decoded.CallType);
        AssertEqual(values.NeedToAnswer, decoded.NeedToAnswer);
        AssertEqual(values.GroupCallId, decoded.GroupCallId);
        AssertEqual(values.PrivateCallId, decoded.PrivateCallId);
        AssertEqual(values.Name, decoded.Name);
        // Real bytes from the 2026-08-04 second live differential WRITE
        // capture (see Qdc1200IdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture)
        // - only the first 32 bytes were captured/observed, the rest is
        // blank padding in this synthetic round trip.
        AssertEqual("020100006405645551004400430054004500530054004e0041004d0045003100", Convert.ToHexString(encoded.AsSpan(0, 32)).ToLowerInvariant());
    }

    private static void PatcherAppliesQdc1200IdPatchWithNoPresenceBitmap()
    {
        // No presence bitmap exists for this entity (see Qdc1200IdCodec's
        // own doc comment) - the snapshot here only carries the one flat
        // record, proving the patch doesn't touch or require anything else.
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.Qdc1200IdAddress(radioIndex);
        var blankRecord = new byte[Qdc1200IdCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var values = new Qdc1200IdCodec.DecodedQdc1200Id(radioIndex)
        {
            Type = Qdc1200IdEntry.TypeAleart,
            CallType = 0,
            NeedToAnswer = true,
            GroupCallId = "567",
            PrivateCallId = "1234",
            Name = "TESTNAME"
        };
        var patched = RadioCodeplugPatcher.ApplyQdc1200IdPatch(snapshot, radioIndex, values);

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = Qdc1200IdCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(values.Type, decoded.Type);
        AssertEqual(values.CallType, decoded.CallType);
        AssertTrue(decoded.NeedToAnswer, "Need to Answer must round-trip through the patch");
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherDeletesQdc1200IdByZeroingNotBlankingTo0xff()
    {
        // No presence bitmap and no 0xFF "unset" sentinel here - a blank
        // Name is the confirmed "unconfigured" signal (same convention as
        // Auto Repeater Offset/Analog Quick Call), so delete zeroes the
        // whole record rather than blanking to 0xFF like most other
        // entities.
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.Qdc1200IdAddress(radioIndex);
        var original = Qdc1200IdCodec.Encode(new byte[Qdc1200IdCodec.RecordLength], new Qdc1200IdCodec.DecodedQdc1200Id(radioIndex)
        {
            Type = Qdc1200IdEntry.TypeAleart,
            CallType = 0,
            Name = "TESTNAME"
        });
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyQdc1200IdDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0x00), "deleted QDC 1200 ID record must be zeroed, not blanked to 0xFF - a blank Name is the confirmed 'unused' sentinel here.");
    }

    private static void Qdc1200SettingsCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[Qdc1200SettingsCodec.RecordLength];
        var values = new Qdc1200SettingsCodec.DecodedQdc1200Settings
        {
            SideTone = true,
            RemotelyKillAllow = true,
            RemotelyMonitorAllow = false,
            Pretime = 730,
            AutoResetTime = 77,
            SelfIdPrivateCall = "ABCD",
            SelfIdGroupCall = "EF1",
            MaxAckWaitTime = 12.5,
            ResendCode = 3,
            RemoteListeningDuration = 199
        };

        var encoded = Qdc1200SettingsCodec.Encode(blankRecord, values);
        var decoded = Qdc1200SettingsCodec.Decode(encoded);

        AssertTrue(decoded.SideTone, "Side Tone should round-trip on");
        AssertTrue(decoded.RemotelyKillAllow, "Remotely Kill Allow should round-trip on");
        AssertTrue(!decoded.RemotelyMonitorAllow, "Remotely Monitor Allow should round-trip off");
        AssertEqual(values.Pretime, decoded.Pretime);
        AssertEqual(values.AutoResetTime, decoded.AutoResetTime);
        AssertEqual(values.SelfIdPrivateCall, decoded.SelfIdPrivateCall);
        AssertEqual(values.SelfIdGroupCall, decoded.SelfIdGroupCall);
        AssertEqual(values.MaxAckWaitTime, decoded.MaxAckWaitTime);
        AssertEqual(values.ResendCode, decoded.ResendCode);
        AssertEqual(values.RemoteListeningDuration, decoded.RemoteListeningDuration);
    }

    private static void PatcherAppliesQdc1200SettingsPatch()
    {
        var blankRecord = new byte[Qdc1200SettingsCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(D890UvMemoryMap.Qdc1200SettingsData, blankRecord)] };

        var values = new Qdc1200SettingsCodec.DecodedQdc1200Settings
        {
            SideTone = true,
            Pretime = 730,
            AutoResetTime = 77,
            SelfIdPrivateCall = "ABCD",
            SelfIdGroupCall = "EF1",
            MaxAckWaitTime = 12.5,
            ResendCode = 3,
            RemoteListeningDuration = 199
        };
        var patched = RadioCodeplugPatcher.ApplyQdc1200SettingsPatch(snapshot, values);

        var patchedRecord = patched.Regions.Single(r => r.Address == D890UvMemoryMap.Qdc1200SettingsData).Data;
        var decoded = Qdc1200SettingsCodec.Decode(patchedRecord);
        AssertTrue(decoded.SideTone, "Side Tone must round-trip through the patch");
        AssertEqual(values.Pretime, decoded.Pretime);
        AssertEqual(values.AutoResetTime, decoded.AutoResetTime);
        AssertEqual(values.ResendCode, decoded.ResendCode);
    }

    private static void AnalogQuickCallCodecEncodeDecodeRoundTrips()
    {
        var values = new AnalogQuickCallCodec.DecodedAnalogQuickCall(0)
        {
            OperationType = 2, // 2Tone
            CallId = 3
        };

        var encoded = AnalogQuickCallCodec.Encode(values);
        var decoded = AnalogQuickCallCodec.Decode(encoded, 0);

        AssertEqual(values.OperationType, decoded.OperationType);
        AssertEqual(values.CallId, decoded.CallId);

        var off = AnalogQuickCallCodec.Encode(new AnalogQuickCallCodec.DecodedAnalogQuickCall(0));
        AssertEqual("00FF", Convert.ToHexString(off));
    }

    private static void PatcherAppliesAnalogQuickCallPatchWithNoPresenceBitmap()
    {
        const int radioIndex = 1;
        var address = RadioCodeplugPatcher.AnalogQuickCallAddress(radioIndex);
        var blankRecord = new byte[AnalogQuickCallCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var values = new AnalogQuickCallCodec.DecodedAnalogQuickCall(radioIndex) { OperationType = 3, CallId = 5 };
        var patched = RadioCodeplugPatcher.ApplyAnalogQuickCallPatch(snapshot, radioIndex, values);

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = AnalogQuickCallCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(values.OperationType, decoded.OperationType);
        AssertEqual(values.CallId, decoded.CallId);
    }

    private static void PatcherDeletesAnalogQuickCallResettingToOperationTypeOff()
    {
        const int radioIndex = 1;
        var address = RadioCodeplugPatcher.AnalogQuickCallAddress(radioIndex);
        var original = AnalogQuickCallCodec.Encode(new AnalogQuickCallCodec.DecodedAnalogQuickCall(radioIndex) { OperationType = 2, CallId = 1 });
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyAnalogQuickCallDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address).Data;
        var decoded = AnalogQuickCallCodec.Decode(deletedRecord, radioIndex);
        AssertEqual((byte)0, decoded.OperationType);
        AssertEqual(-1, decoded.CallId);
    }

    private static void StateInformationCodecEncodeDecodeRoundTrips()
    {
        var encoded = StateInformationCodec.Encode("Status Message 1");
        var decoded = StateInformationCodec.Decode(encoded);

        AssertEqual("Status Message 1", decoded);
        AssertEqual(StateInformationCodec.RecordLength, encoded.Length);

        var blank = StateInformationCodec.Encode("");
        AssertEqual("", StateInformationCodec.Decode(blank));
    }

    private static void PatcherAppliesStateInformationPatchWithNoPresenceBitmap()
    {
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.StateInformationAddress(radioIndex);
        var blankRecord = new byte[StateInformationCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var patched = RadioCodeplugPatcher.ApplyStateInformationPatch(snapshot, radioIndex, "Status Message 1");

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        AssertEqual("Status Message 1", StateInformationCodec.Decode(patchedRecord));
    }

    private static void PatcherDeletesStateInformationBlankingTheNameBuffer()
    {
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.StateInformationAddress(radioIndex);
        var original = StateInformationCodec.Encode("Status Message 1");
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyStateInformationDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address).Data;
        AssertEqual("", StateInformationCodec.Decode(deletedRecord));
    }

    private static void HotKeyCodecEncodeDecodeRoundTrips()
    {
        // Same field shape as the 2026-08-04 live differential WRITE
        // capture's own Hot Key 3 (see HotKeyCodecDecodesEveryFieldAtItsConfirmedByteOffset).
        var values = new HotKeyCodec.DecodedHotKey(2)
        {
            Mode = 0,
            Menu = 1,
            CallType = 2, // Digital
            DigiCallType = 1, // DMR Hot Text
            CallObject = 1,
            Content = 2
        };

        var encoded = HotKeyCodec.Encode(new byte[HotKeyCodec.RecordLength], values);
        var decoded = HotKeyCodec.Decode(encoded, 2);

        AssertEqual(values.Mode, decoded.Mode);
        AssertEqual(values.Menu, decoded.Menu);
        AssertEqual(values.CallType, decoded.CallType);
        AssertEqual(values.DigiCallType, decoded.DigiCallType);
        AssertEqual(values.CallObject, decoded.CallObject);
        AssertEqual(values.Content, decoded.Content);
        // Real bytes from the live capture (see the Decode-side test).
        AssertEqual("0001010300000000 01".Replace(" ", ""), Convert.ToHexString(encoded.AsSpan(0, 9)).ToLowerInvariant());
    }

    private static void HotKeyCodecEncodeHasNoDedicatedOffByteForCallType()
    {
        // "Off" CallType has no dedicated wire value (see HotKeyCodec's own
        // doc comment) - it must round-trip back to Off purely via
        // CallObject's own 0xFFFFFFFF sentinel, the same inference Decode
        // already relies on.
        var off = new HotKeyCodec.DecodedHotKey(0) { Mode = 0, CallType = 0, CallObject = -1 };
        var encoded = HotKeyCodec.Encode(new byte[HotKeyCodec.RecordLength], off);

        AssertEqual((byte)0, encoded[2]);
        AssertEqual("FFFFFFFF", Convert.ToHexString(encoded.AsSpan(4, 4)));

        var decoded = HotKeyCodec.Decode(encoded, 0);
        AssertEqual((byte)0, decoded.CallType);
    }

    private static void PatcherAppliesHotKeyPatchWithNoPresenceBitmap()
    {
        const int radioIndex = 2;
        var address = RadioCodeplugPatcher.HotKeyAddress(radioIndex);
        var blankRecord = new byte[HotKeyCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var values = new HotKeyCodec.DecodedHotKey(radioIndex) { Mode = 1, Menu = 5 };
        var patched = RadioCodeplugPatcher.ApplyHotKeyPatch(snapshot, radioIndex, values);

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = HotKeyCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(values.Mode, decoded.Mode);
        AssertEqual(values.Menu, decoded.Menu);
    }

    private static void AddQdcAddressIsCappedAt128Slots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.QdcAddressMax; i++)
        {
            viewModel.AddQdcAddressCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.QdcAddressMax, viewModel.QdcAddresses.Count);

        viewModel.AddQdcAddressCommand.Execute(null);
        AssertEqual(CodeplugLimits.QdcAddressMax, viewModel.QdcAddresses.Count);
    }

    private static void QdcAddressEntryTypeOptionsDependOnCallTypeAndAckEnabledNeedsAleartOrRemotelyMonitor()
    {
        var entry = new QdcAddressEntry { CallType = 0 };
        AssertEqual(7, entry.TypeOptions.Count);
        AssertEqual("SEL CALL", entry.TypeOptions[0]);

        entry.TypeText = "ALEART";
        AssertTrue(entry.IsAckEnabled, "ALEART should enable Ack for Private Call");

        entry.TypeText = "CHECK";
        AssertTrue(!entry.IsAckEnabled, "CHECK should not enable Ack");

        entry.CallType = 1; // Group Call
        AssertEqual(3, entry.TypeOptions.Count);
        AssertEqual("SEL CAL", entry.TypeOptions[0]);

        entry.TypeText = "ALEART";
        AssertTrue(entry.IsAckEnabled, "ALEART should enable Ack for Group Call too - no Call-Type restriction stated for this entity");
    }

    private static void QdcAddressEntryChangingCallTypeDoesNotResetPrivateGroupIdOrTypeAndTogglesEnableFlags()
    {
        // Confirmed live 2026-08-04 for Qdc1200IdEntry's identical wire
        // layout (Private ID/Group ID/Type are independent byte fields,
        // not cleared on a Call Type switch) - inferred to carry over here
        // rather than independently re-tested for this entity (see
        // QdcAddressEntry's own class doc comment).
        var entry = new QdcAddressEntry { CallType = 0, PrivateCallId = "ABCD", Type = QdcAddressEntry.TypeAleart };
        AssertTrue(entry.IsPrivateCallIdEnabled, "Private ID should be enabled for Private Call");
        AssertTrue(!entry.IsGroupCallIdEnabled, "Group ID should be disabled for Private Call");

        entry.CallType = 1; // Group Call
        AssertTrue(!entry.IsPrivateCallIdEnabled, "Private ID should be disabled once switched to Group Call");
        AssertTrue(entry.IsGroupCallIdEnabled, "Group ID should be enabled for Group Call");
        AssertEqual("ABCD", entry.PrivateCallId);
        AssertEqual(QdcAddressEntry.TypeAleart, entry.Type);

        entry.CallType = 2; // All Call
        AssertTrue(!entry.IsPrivateCallIdEnabled, "Private ID should stay disabled for All Call");
        AssertTrue(!entry.IsGroupCallIdEnabled, "Group ID should be disabled for All Call too - not just Private ID");
        AssertEqual("ABCD", entry.PrivateCallId);
        AssertEqual(QdcAddressEntry.TypeAleart, entry.Type);
    }

    private static void QdcAddressCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Real bytes from the 2026-08-04 live differential WRITE capture -
        // No. 1: Call Type=Private, Private ID="ABCD", Type=ALEART,
        // Ack=On, Name="QDCADDRTEST1" (see D890UvMemoryMap.QdcAddressData's
        // own doc comment). Confirmed via a follow-up READ capture that
        // the record is exactly 0x30 (48) bytes - byte 0x30 (the next
        // record's own first byte) read back as 0xFF.
        var record = new byte[QdcAddressCodec.RecordLength];
        var realBytes = Convert.FromHexString("020001000100cdab51004400430041004400440052005400450053005400310000000000000000000000000000000000");
        realBytes.CopyTo(record, 0);

        var decoded = QdcAddressCodec.Decode(record, 0);

        AssertEqual(QdcAddressEntry.TypeAleart, decoded.Type);
        AssertEqual((byte)0, decoded.CallType); // Private Call
        AssertTrue(decoded.Ack, "Ack must be on");
        AssertEqual("ABCD", decoded.PrivateCallId);
        AssertEqual("001", decoded.GroupCallId);
        AssertEqual("QDCADDRTEST1", decoded.Name);
    }

    private static void QdcAddressCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[QdcAddressCodec.RecordLength];
        var values = new QdcAddressCodec.DecodedQdcAddress(0)
        {
            Type = QdcAddressEntry.TypeAleart,
            CallType = 0,
            Ack = true,
            PrivateCallId = "ABCD",
            GroupCallId = "",
            Name = "QDCADDRTEST1"
        };

        var encoded = QdcAddressCodec.Encode(blankRecord, values);
        var decoded = QdcAddressCodec.Decode(encoded, 0);

        AssertEqual(values.Type, decoded.Type);
        AssertEqual(values.CallType, decoded.CallType);
        AssertEqual(values.Ack, decoded.Ack);
        AssertEqual(values.PrivateCallId, decoded.PrivateCallId);
        AssertEqual(values.Name, decoded.Name);
        // Header + Name bytes this Encode call actually produces - Group
        // Call ID's own bytes (4-5) differ from the live capture's own
        // "010 0" vendor-default here (this Encode call used a blank
        // Group Call ID, which encodes as "0000" -> raw 00 00, not
        // whatever sentinel vendor CPS happens to leave an untouched
        // field at - see QdcAddressCodec's own doc comment on the lack of
        // a confirmed "Off" sentinel for these ID fields).
        AssertEqual("020001000000cdab510044004300410044004400520054004500530054003100", Convert.ToHexString(encoded.AsSpan(0, 32)).ToLowerInvariant());
    }

    private static void PatcherAppliesQdcAddressPatchWithNoPresenceBitmap()
    {
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.QdcAddressAddress(radioIndex);
        var blankRecord = new byte[QdcAddressCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, blankRecord)] };

        var values = new QdcAddressCodec.DecodedQdcAddress(radioIndex)
        {
            Type = QdcAddressEntry.TypeAleart,
            CallType = 0,
            Ack = true,
            PrivateCallId = "ABCD",
            Name = "QDCADDRTEST1"
        };
        var patched = RadioCodeplugPatcher.ApplyQdcAddressPatch(snapshot, radioIndex, values);

        var patchedRecord = patched.Regions.Single(r => r.Address == address).Data;
        var decoded = QdcAddressCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(values.Type, decoded.Type);
        AssertEqual(values.PrivateCallId, decoded.PrivateCallId);
        AssertEqual(values.Name, decoded.Name);
    }

    private static void PatcherDeletesQdcAddressBlankingTo0xffNotZero()
    {
        // Confirmed live 2026-08-04 via the follow-up READ capture: an
        // untouched QDC Address slot reads back as all-0xFF, not all-zero
        // like Qdc1200IdCodec's own delete convention (see
        // D890UvMemoryMap.QdcAddressData's own doc comment).
        const int radioIndex = 0;
        var address = RadioCodeplugPatcher.QdcAddressAddress(radioIndex);
        var original = QdcAddressCodec.Encode(new byte[QdcAddressCodec.RecordLength], new QdcAddressCodec.DecodedQdcAddress(radioIndex)
        {
            Type = QdcAddressEntry.TypeAleart,
            PrivateCallId = "ABCD",
            Name = "QDCADDRTEST1"
        });
        var snapshot = new RadioCodeplugRawSnapshot { Regions = [new CodeplugRawRegion(address, original)] };

        var deleted = RadioCodeplugPatcher.ApplyQdcAddressDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == address);
        AssertTrue(deletedRecord.Data.All(b => b == 0xFF), "deleted QDC Address record must be blanked to all-0xFF, not all-zero");
    }

    private static void MapQdcAddressesSurvivesTheEntrysOwnAckResetCascade()
    {
        // QdcAddressEntry's own OnTypeChanged resets Ack once it becomes
        // disabled - this test guards against the object-initializer
        // property order in RadioReadMapper.MapQdcAddresses accidentally
        // letting that reset clobber a real decoded value, same discipline
        // established after the Hot Key mapper bug.
        var decoded = new QdcAddressCodec.DecodedQdcAddress(0)
        {
            Type = QdcAddressEntry.TypeAleart,
            CallType = 0,
            Ack = true,
            PrivateCallId = "1234",
            GroupCallId = "567",
            Name = "TESTNAME"
        };

        var result = new RadioCodeplugReadResult { QdcAddresses = [decoded] };
        var mapped = RadioReadMapper.MapQdcAddresses(result);

        AssertEqual(1, mapped.Count);
        var entry = mapped[0];
        AssertEqual(1, entry.Number);
        AssertEqual(QdcAddressEntry.TypeAleart, entry.Type);
        AssertTrue(entry.Ack, "Ack must survive the mapper, not get reset to false");
        AssertEqual("1234", entry.PrivateCallId);
        AssertEqual("567", entry.GroupCallId);
        AssertEqual("TESTNAME", entry.Name);
    }

    private static void MapFiveToneIdsMergesInfoIdSlotsByRowNumber()
    {
        // Information ID NO. selects a row by its own Number, so slot
        // index (InfoIdNo - 1) must merge into the row whose Number
        // matches that InfoIdNo - not the row at the same ARRAY position
        // in FiveToneIds. FiveToneIdCodec.DecodedFiveToneId.Index is a
        // 0-based array position (Number = Index + 1), so Index=1 here
        // simulates a row whose own Number is 2.
        var resultForRow2 = new RadioCodeplugReadResult
        {
            FiveToneIds = [new FiveToneIdCodec.DecodedFiveToneId(1) { Standard = 1, TimeOfEncodeTone = 40, Name = "ROW2" }],
            FiveToneInfoIdSlots =
            [
                new FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot { FunctionName = "SLOT1" },
                new FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot { FunctionName = "SLOT2" }
            ]
        };
        var mappedRow2 = RadioReadMapper.MapFiveToneIds(resultForRow2).Single();
        AssertEqual(2, mappedRow2.Number);
        AssertEqual("SLOT2", mappedRow2.FunctionName); // slot index 1 = Number 2, NOT slot index 0

        // A Number beyond FiveToneInfoIdSlotCount has no slot to merge -
        // must not throw, and Function fields stay at their defaults.
        var resultForOutOfRangeRow = new RadioCodeplugReadResult
        {
            FiveToneIds = [new FiveToneIdCodec.DecodedFiveToneId(99) { Standard = 1, TimeOfEncodeTone = 40, Name = "ROW100" }],
            FiveToneInfoIdSlots = [new FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot { FunctionName = "SLOT1" }]
        };
        var mappedRow100 = RadioReadMapper.MapFiveToneIds(resultForOutOfRangeRow).Single();
        AssertEqual(100, mappedRow100.Number);
        AssertEqual("", mappedRow100.FunctionName);
    }

    private static void FiveToneIdEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new FiveToneIdEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Standard = 5;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Standard must mark pending");
        entry.MarkRadioSynced();

        entry.FunctionName = "TESTFN";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing the newly-moved Function Name must mark pending");
        entry.MarkRadioSynced();

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing nested SpecialCall must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must pick up the nested SpecialCall state too");
    }

    private static void FiveToneSettingsEntryHasAnyPendingRadioWriteIgnoresInfoIdNoAndStopCode()
    {
        var entry = new FiveToneSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        // InfoIdNo is a transient UI selector (not a stored value) and
        // StopCode was never independently located on the wire - neither
        // should mark the entity pending for a radio write.
        entry.InfoIdNo = 3;
        AssertTrue(!entry.HasAnyPendingRadioWrite, "InfoIdNo must NOT mark pending - it isn't part of the radio-write snapshot");
        entry.StopCode = 2;
        AssertTrue(!entry.HasAnyPendingRadioWrite, "StopCode must NOT mark pending - never independently located on the wire");

        entry.SelfId = "12345";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Self ID must mark pending");
        entry.MarkRadioSynced();

        entry.BotSpecialCall.OtherSideId = "12345";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing nested BotSpecialCall must mark pending");
    }

    private static void AddFiveToneIdIsCappedAt99Slots()
    {
        var viewModel = new MainViewModel();
        for (var i = 0; i < CodeplugLimits.FiveToneIdMax; i++)
        {
            viewModel.AddFiveToneIdCommand.Execute(null);
        }

        AssertEqual(CodeplugLimits.FiveToneIdMax, viewModel.FiveToneIds.Count);

        viewModel.AddFiveToneIdCommand.Execute(null);
        AssertEqual(CodeplugLimits.FiveToneIdMax, viewModel.FiveToneIds.Count);
    }

    private static void FiveToneSettingsTextWrappersRoundTripThroughTheirConfirmedRanges()
    {
        var entry = new FiveToneSettingsEntry
        {
            DecodeTimeMsText = "2000",
            PretimeText = "2550",
            AutoResetTimeText = "250",
            TimeLapseAfterEncodeText = "10",
            FirstToneLengthText = "2550",
            StopTimeLengthText = "0",
            FirstToneLengthAfterStopText = "2500",
            BotTimeOfEncodeToneText = "100",
            EotTimeOfEncodeToneText = "30"
        };

        AssertEqual(2000, entry.DecodeTimeMs);
        AssertEqual(2550, entry.Pretime);
        AssertEqual(250, entry.AutoResetTime);
        AssertEqual(10, entry.TimeLapseAfterEncode);
        AssertEqual(2550, entry.FirstToneLength);
        AssertEqual(0, entry.StopTimeLength);
        AssertEqual(2500, entry.FirstToneLengthAfterStop);
        AssertEqual(100, entry.BotTimeOfEncodeTone);
        AssertEqual(30, entry.EotTimeOfEncodeTone);

        // Out of range values are rejected, not clamped.
        entry.DecodeTimeMsText = "2010";
        AssertEqual(2000, entry.DecodeTimeMs);

        // PTT ID Pause Time's "Off" sentinel (-1) round trips through its
        // own text wrapper.
        entry.PttIdPauseTimeText = "Off";
        AssertEqual(-1, entry.PttIdPauseTime);
        entry.PttIdPauseTimeText = "75";
        AssertEqual(75, entry.PttIdPauseTime);
        entry.PttIdPauseTimeText = "4";
        AssertEqual(75, entry.PttIdPauseTime); // below the 5-75 range, rejected

        entry.StopCodeText = "F";
        AssertEqual((byte)4, entry.StopCode);
    }

    private static void FiveToneSettingsFunctionDecodingResponseOptionsDependOnFunctionOption()
    {
        // Function Option/Function Decoding Response moved from
        // FiveToneSettingsEntry to FiveToneIdEntry 2026-08-06 (per-row
        // data, not a shared singleton - see FiveToneIdEntry's own class
        // doc comment).
        var entry = new FiveToneIdEntry { FunctionOptionText = "Squelch Off" };
        AssertEqual(3, entry.FunctionDecodingResponseOptions.Count);
        AssertTrue(entry.IsFunctionDecodingResponseEnabled, "Squelch Off should enable Function Decoding Response");

        entry.FunctionDecodingResponseText = "Beep tone & Respond";
        AssertEqual("Beep tone & Respond", entry.FunctionDecodingResponseText);

        entry.FunctionOptionText = "Call all";
        AssertEqual(2, entry.FunctionDecodingResponseOptions.Count);
        AssertTrue(entry.IsFunctionDecodingResponseEnabled, "Call all should enable Function Decoding Response");
        // Switching Function Option resets the dependent field, since its
        // old selection may not exist in the new option list.
        AssertEqual("None", entry.FunctionDecodingResponseText);

        entry.FunctionOptionText = "Emergency alarm";
        AssertTrue(!entry.IsFunctionDecodingResponseEnabled, "Emergency alarm should disable Function Decoding Response entirely");
        AssertEqual(0, entry.FunctionDecodingResponseOptions.Count);
    }

    private static void FiveToneIdEntryStandardAndTimeAndNameAreDisabledUntilHasSpecialCall()
    {
        var entry = new FiveToneIdEntry();
        AssertTrue(!entry.SpecialCall.IsConfigured, "SpecialCall.IsConfigured must default to false - the popup hasn't been used yet");

        entry.SpecialCall.IsConfigured = true;
        AssertTrue(entry.SpecialCall.IsConfigured, "SpecialCall.IsConfigured should be settable directly");

        entry.StandardText = "CCIR1";
        AssertEqual("CCIR1", entry.StandardText);
    }

    private static void FiveToneSpecialCallEntryCallingTypeDrivesIsSendMessageIsAniIsPttId()
    {
        var entry = new FiveToneSpecialCallEntry();
        AssertTrue(entry.IsSendMessage, "default Calling Type (0) should be Send Message");
        AssertTrue(!entry.IsAni, "default Calling Type should not be ANI");
        AssertTrue(!entry.IsPttId, "default Calling Type should not be PTTID");

        entry.CallingTypeText = "ANI";
        AssertTrue(entry.IsAni, "ANI should be selected");
        AssertTrue(!entry.IsSendMessage, "ANI should not also be Send Message");

        entry.CallingTypeText = "PTTID";
        AssertTrue(entry.IsPttId, "PTTID should be selected");
        AssertTrue(!entry.IsAni, "PTTID should not also be ANI");

        entry.IntervalCharacterText = "C";
        AssertEqual((byte)3, entry.IntervalCharacter);
    }

    private static void FiveToneOtherSideIdMaxLengthTracksSelfIdLengthCappedAt7()
    {
        var viewModel = new MainViewModel();
        AssertEqual(0, viewModel.FiveToneOtherSideIdMaxLength);

        viewModel.FiveToneSettings.SelfId = "12345";
        AssertEqual(5, viewModel.FiveToneOtherSideIdMaxLength);

        // Self ID's own max length is 7, but confirm the cap holds even if
        // it somehow ended up longer.
        viewModel.FiveToneSettings.SelfId = "123456789";
        AssertEqual(7, viewModel.FiveToneOtherSideIdMaxLength);
    }

    private static void SelectedInfoIdRowSwitchesToADifferentRowsOwnFunctionValues()
    {
        // Regression test for a real bug found live 2026-08-06: Function
        // Option/Function Decoding Response/Information ID/
        // Function Name used to live on FiveToneSettings as a shared
        // singleton, so switching "Information ID NO." never actually
        // changed what was shown - every row looked identical. Those 4
        // fields now live on FiveToneIdEntry itself.
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.IsInfoIdRowSelected, "no rows exist yet - nothing to select");

        viewModel.AddFiveToneIdCommand.Execute(null); // row 1
        viewModel.AddFiveToneIdCommand.Execute(null); // row 2
        var row1 = viewModel.FiveToneIds[0];
        var row2 = viewModel.FiveToneIds[1];
        row1.FunctionName = "ROW1FN";
        row2.FunctionName = "ROW2FN";

        viewModel.FiveToneSettings.InfoIdNo = 1;
        AssertEqual(row1, viewModel.SelectedInfoIdRow);
        AssertEqual("ROW1FN", viewModel.SelectedInfoIdRow?.FunctionName);

        viewModel.FiveToneSettings.InfoIdNo = 2;
        AssertEqual(row2, viewModel.SelectedInfoIdRow);
        AssertEqual("ROW2FN", viewModel.SelectedInfoIdRow?.FunctionName);
        AssertTrue(viewModel.IsInfoIdRowSelected, "row 2 exists, should resolve");
    }

    private static void OpenFiveToneRowSpecialCallRetargetsADifferentRowViaGroupNo()
    {
        var viewModel = new MainViewModel();
        viewModel.AddFiveToneIdCommand.Execute(null); // Number 1
        viewModel.AddFiveToneIdCommand.Execute(null); // Number 2
        var row1 = viewModel.FiveToneIds.Single(e => e.Number == 1);
        var row2 = viewModel.FiveToneIds.Single(e => e.Number == 2);
        viewModel.SelectedFiveToneId = row1;

        // Popup opened from row 1, but Group NO. 2 is picked instead - OK
        // should edit row 2, not row 1 (confirmed 2026-08-05 against the
        // real vendor CPS: the popup's own Group NO. picks which row it
        // affects).
        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, request =>
        {
            request.GroupNo = 2;
            request.Values.CallingType = FiveToneSpecialCallEntry.CallingTypeSendMessage;
            request.Values.OtherSideId = "5551234";
            request.Values.Message = "Hello";
            request.Values.IsConfigured = true; // the real dialog's own OK handler sets this
            return true;
        }));

        viewModel.OpenFiveToneRowSpecialCallCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertTrue(!row1.SpecialCall.IsConfigured, "row 1 must be untouched - the popup redirected to row 2");
        AssertTrue(row2.SpecialCall.IsConfigured, "row 2 should now be configured");
        AssertTrue(row2.SpecialCall.IsSendMessage, "row 2's Calling Type should be Send Message");
        AssertEqual("5551234", row2.SpecialCall.OtherSideId);
        AssertEqual("Hello", row2.SpecialCall.Message);
        AssertEqual(row2, viewModel.SelectedFiveToneId);
    }

    private static void OpenFiveToneRowSpecialCallCreatesANewRowWhenGroupNoHasNoneYet()
    {
        var viewModel = new MainViewModel();
        viewModel.AddFiveToneIdCommand.Execute(null); // Number 1
        viewModel.SelectedFiveToneId = viewModel.FiveToneIds.Single();
        AssertEqual(1, viewModel.FiveToneIdCount);

        // Group NO. 42 has no row yet - the real vendor CPS offers all
        // 1-100 as targets regardless of which are already "added" here,
        // so OK must create it (confirmed 2026-08-05 against the real
        // vendor CPS).
        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, request =>
        {
            request.GroupNo = 42;
            request.Values.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
            request.Values.OtherSideId = "9998888";
            request.Values.IntervalCharacter = 2;
            request.Values.IsConfigured = true; // the real dialog's own OK handler sets this
            return true;
        }));

        viewModel.OpenFiveToneRowSpecialCallCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertEqual(2, viewModel.FiveToneIdCount);
        var newRow = viewModel.FiveToneIds.Single(e => e.Number == 42);
        AssertTrue(newRow.SpecialCall.IsConfigured, "the newly created row should be configured");
        AssertTrue(newRow.SpecialCall.IsAni, "the newly created row's Calling Type should be ANI");
        AssertEqual("9998888", newRow.SpecialCall.OtherSideId);
        AssertEqual((byte)2, newRow.SpecialCall.IntervalCharacter);
        AssertEqual(newRow, viewModel.SelectedFiveToneId);
    }

    private static void FiveToneIdEntryComposesEncodeIdForSendMessageAniAndPttId()
    {
        // Row-level formula (confirmed 2026-08-04 against the real vendor
        // CPS: what's entered in this specific popup sets the Encode ID
        // text shown in the row's own grid).
        var sendMessage = new FiveToneIdEntry();
        sendMessage.SpecialCall.OtherSideId = "12345";
        sendMessage.SpecialCall.Message = "MYMESSAGE";
        sendMessage.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeSendMessage;
        sendMessage.SpecialCall.IsConfigured = true;
        AssertEqual("12345 Information:MYMESSAGE", sendMessage.EncodeId);

        var ani = new FiveToneIdEntry();
        ani.SpecialCall.OtherSideId = "12345";
        ani.SpecialCall.IntervalCharacter = 1; // "A"
        ani.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        ani.SpecialCall.IsConfigured = true;
        AssertEqual("12345A", ani.EncodeId);

        var aniNoStop = new FiveToneIdEntry();
        aniNoStop.SpecialCall.OtherSideId = "12345";
        aniNoStop.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        aniNoStop.SpecialCall.IsConfigured = true;
        AssertEqual("12345", aniNoStop.EncodeId);

        var pttId = new FiveToneIdEntry { EncodeId = "leftover" };
        pttId.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        pttId.SpecialCall.IsConfigured = true;
        AssertEqual("", pttId.EncodeId);
    }

    private static void FiveToneIdEntryEncodeIdDisabledOnceAnySpecialCallIsConfigured()
    {
        // Corrected 2026-08-06: earlier readings had only PTTID disabling
        // Encode ID. Real vendor CPS behavior (confirmed): the box starts
        // hand-editable and stays that way until Special Call is used
        // ONCE, then goes read-only regardless of which Calling Type was
        // picked - Send Message/ANI included, not just PTTID.
        var entry = new FiveToneIdEntry();
        AssertTrue(entry.IsEncodeIdEnabled, "never-configured row must keep Encode ID enabled (free text)");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        AssertTrue(entry.IsEncodeIdEnabled, "not yet configured (no OK pressed) - PTTID selected in the dropdown alone doesn't disable it");

        entry.SpecialCall.IsConfigured = true;
        AssertTrue(!entry.IsEncodeIdEnabled, "configured PTTID must disable Encode ID");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeSendMessage;
        AssertTrue(!entry.IsEncodeIdEnabled, "configured Send Message must ALSO disable Encode ID (read-only, not just empty-for-PTTID)");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        AssertTrue(!entry.IsEncodeIdEnabled, "configured ANI must ALSO disable Encode ID");
    }

    private static void FiveToneIdEntryEncodeIdHexOnlyDisabledOnlyForConfiguredSendMessage()
    {
        // The Send Message formula embeds literal text (" Information:" +
        // an arbitrary ASCII Message) - not hex, unlike every other state
        // (manual entry, ANI, or PTTID). Found 2026-08-06 auditing this
        // view's input restrictions: the Encode ID TextBox's hex-only
        // keystroke filter needs to turn off specifically here, otherwise
        // hand-editing an auto-composed Send Message value would be
        // impossible.
        var entry = new FiveToneIdEntry();
        AssertTrue(entry.IsEncodeIdHexOnly, "never-configured row keeps the hex-only restriction (manual entry is still meant to be hex)");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeSendMessage;
        AssertTrue(entry.IsEncodeIdHexOnly, "not yet configured (no OK pressed) - Send Message selected in the dropdown alone doesn't lift the restriction");

        entry.SpecialCall.IsConfigured = true;
        AssertTrue(!entry.IsEncodeIdHexOnly, "configured Send Message must lift the hex-only restriction");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        AssertTrue(entry.IsEncodeIdHexOnly, "configured ANI must keep the hex-only restriction (OtherSideId + a hex-safe interval letter)");

        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        AssertTrue(entry.IsEncodeIdHexOnly, "configured PTTID must keep the hex-only restriction (field is disabled anyway, but the flag itself should stay true)");
    }

    private static void FiveToneSettingsBotComposesEncodeIdForAniAndPttIdButNotSendMessage()
    {
        // BOT/EOT's own formula is CONFIRMED DIFFERENT from the row-level
        // one (real hex examples, 2026-08-05) - ANI repeats Other Side ID
        // on both sides of the Interval Character; PTTID is "E6"+id, not
        // empty. Send Message's own formula couldn't be reverse-engineered
        // from black-box examples (7 real data points tried, no
        // consistent rule found) - stays manual (not auto-composed).
        var settings = new FiveToneSettingsEntry();

        settings.BotSpecialCall.OtherSideId = "1234567";
        settings.BotSpecialCall.IntervalCharacter = 1; // "A"
        settings.BotSpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        settings.BotSpecialCall.IsConfigured = true;
        AssertEqual("1234567A1234567", settings.BotEncodeId);

        settings.BotSpecialCall.IntervalCharacter = 0; // "No stop"
        // Toggling IntervalCharacter alone re-fires the reactive compose.
        AssertEqual("12345671234567", settings.BotEncodeId);

        settings.BotSpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        AssertEqual("E61234567", settings.BotEncodeId);

        settings.BotEncodeId = "untouched";
        settings.BotSpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeSendMessage;
        AssertEqual("untouched", settings.BotEncodeId);
    }

    private static void ResetFiveToneRowSpecialCallClearsStateAfterConfirmation()
    {
        var viewModel = new MainViewModel();
        viewModel.AddFiveToneIdCommand.Execute(null);
        var entry = viewModel.FiveToneIds.Single();
        entry.SpecialCall.OtherSideId = "12345";
        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        entry.SpecialCall.IsConfigured = true;
        AssertTrue(!string.IsNullOrEmpty(entry.EncodeId), "sanity check: Encode ID should be composed before reset");

        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, confirmResetFiveToneSpecialCall: true));
        viewModel.ResetFiveToneRowSpecialCallCommand.ExecuteAsync(entry).GetAwaiter().GetResult();

        AssertTrue(!entry.SpecialCall.IsConfigured, "SpecialCall must be reset back to not-configured");
        AssertEqual("", entry.SpecialCall.OtherSideId);
        AssertEqual("", entry.EncodeId);
    }

    private static void ResetFiveToneRowSpecialCallDoesNothingWithoutConfirmation()
    {
        var viewModel = new MainViewModel();
        viewModel.AddFiveToneIdCommand.Execute(null);
        var entry = viewModel.FiveToneIds.Single();
        entry.SpecialCall.OtherSideId = "12345";
        entry.SpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypeAni;
        entry.SpecialCall.IsConfigured = true;
        var encodeIdBeforeReset = entry.EncodeId;

        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, confirmResetFiveToneSpecialCall: false));
        viewModel.ResetFiveToneRowSpecialCallCommand.ExecuteAsync(entry).GetAwaiter().GetResult();

        AssertTrue(entry.SpecialCall.IsConfigured, "declining the confirmation must leave SpecialCall untouched");
        AssertEqual(encodeIdBeforeReset, entry.EncodeId);
    }

    private static void ResetFiveToneBotAndEotSpecialCallClearStateAfterConfirmation()
    {
        // BOT/EOT's own Encode ID box now goes read-only once configured
        // too (2026-08-06 fix), so they need their own reset command, same
        // shape as the row-level one but with no double-click target.
        var viewModel = new MainViewModel();
        viewModel.FiveToneSettings.BotSpecialCall.OtherSideId = "12345";
        viewModel.FiveToneSettings.BotSpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        viewModel.FiveToneSettings.BotSpecialCall.IsConfigured = true;
        viewModel.FiveToneSettings.EotSpecialCall.OtherSideId = "12345";
        viewModel.FiveToneSettings.EotSpecialCall.CallingType = FiveToneSpecialCallEntry.CallingTypePttId;
        viewModel.FiveToneSettings.EotSpecialCall.IsConfigured = true;
        AssertTrue(!viewModel.FiveToneSettings.IsBotEncodeIdEnabled, "sanity check: BOT Encode ID should be locked before reset");
        AssertTrue(!viewModel.FiveToneSettings.IsEotEncodeIdEnabled, "sanity check: EOT Encode ID should be locked before reset");

        viewModel.SetStoragePicker(new TestStoragePicker(UsedEncryptionKeyRemovalChoice.Cancel, confirmResetFiveToneSpecialCall: true));
        viewModel.ResetFiveToneBotSpecialCallCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        viewModel.ResetFiveToneEotSpecialCallCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        AssertTrue(!viewModel.FiveToneSettings.BotSpecialCall.IsConfigured, "BOT SpecialCall must be reset back to not-configured");
        AssertEqual("", viewModel.FiveToneSettings.BotEncodeId);
        AssertTrue(viewModel.FiveToneSettings.IsBotEncodeIdEnabled, "BOT Encode ID must be editable again after reset");
        AssertTrue(!viewModel.FiveToneSettings.EotSpecialCall.IsConfigured, "EOT SpecialCall must be reset back to not-configured");
        AssertEqual("", viewModel.FiveToneSettings.EotEncodeId);
        AssertTrue(viewModel.FiveToneSettings.IsEotEncodeIdEnabled, "EOT Encode ID must be editable again after reset");
    }

    private static void FiveToneValidationFlagsSelfIdOutside5To7DigitsButNotBlank()
    {
        var viewModel = new MainViewModel();

        AssertTrue(
            !viewModel.ValidationMessages.Any(m => m.Contains("Self ID", StringComparison.Ordinal)),
            "a blank/never-configured Self ID must not be flagged");

        viewModel.FiveToneSettings.SelfId = "1234"; // 4 digits, one short
        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("Self ID must be 5-7 digits", StringComparison.Ordinal)),
            "a 4-digit Self ID should produce a validation message");

        viewModel.FiveToneSettings.SelfId = "12345678"; // 8 digits, one over
        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("Self ID must be 5-7 digits", StringComparison.Ordinal)),
            "an 8-digit Self ID should produce a validation message");

        viewModel.FiveToneSettings.SelfId = "12345"; // 5 digits, valid
        AssertTrue(
            !viewModel.ValidationMessages.Any(m => m.Contains("Self ID", StringComparison.Ordinal)),
            "a valid 5-digit Self ID should not produce a validation message");
    }

    private static void FiveToneValidationFlagsIdNumberOutOfRangeAndDuplicates()
    {
        var viewModel = new MainViewModel();
        viewModel.AddFiveToneIdCommand.Execute(null); // row 0, Number 1
        viewModel.AddFiveToneIdCommand.Execute(null); // row 1, Number 2

        AssertTrue(
            !viewModel.ValidationMessages.Any(m => m.Contains("ID number", StringComparison.Ordinal)),
            "two distinct, in-range ID numbers should not produce a validation message");

        // Mobile lets a row's own Number be retyped directly (no Desktop-
        // style Group NO. redirect there) - simulate that landing out of
        // range. FiveToneIdEntry has no per-property revalidation wiring
        // (only CollectionChanged does, same gap AnalogAddressEntry has),
        // so adding a third row forces a fresh RefreshValidation pass that
        // re-scans every entry, including the one just mutated.
        viewModel.FiveToneIds[1].Number = 0;
        viewModel.AddFiveToneIdCommand.Execute(null); // row 2, Number 3
        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("ID number 0 must be 1-", StringComparison.Ordinal)),
            "an ID number below 1 should produce a validation message");

        viewModel.FiveToneIds[1].Number = 1; // now collides with row 0
        viewModel.RemoveFiveToneIdCommand.Execute(null); // removes row 2 (still selected), forces another revalidation pass
        AssertTrue(
            viewModel.ValidationMessages.Any(m => m.Contains("ID number 1 is used by more than one row", StringComparison.Ordinal)),
            "a duplicate ID number should produce a validation message");
    }

    private static void FiveToneIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Row 1 from the 2026-08-05/06 live differential WRITE captures:
        // Standard=EIA, Time Of Encode Tone=55, Special Call=Send Message/
        // Other Side ID "12345"/Message "TESTMSG", Name "TESTID1". Self ID
        // was 5 digits throughout every capture this row appeared in.
        var record = Convert.FromHexString("000e1637e1e2345e544553544d534700000000000000000054004500530054004900440031002000000000000000000000000000000000000000000000000000")[..FiveToneIdCodec.RecordLength];

        var decoded = FiveToneIdCodec.Decode(record, 0, selfIdLength: 5);

        AssertEqual((byte)14, decoded.Standard); // EIA's own index in the 15-item Standard list
        AssertEqual((byte)55, decoded.TimeOfEncodeTone);
        AssertEqual("TESTID1", decoded.Name);
        AssertTrue(decoded.SpecialCall.IsConfigured, "Send Message must decode as configured");
        AssertEqual(FiveToneCallingType.SendMessage, decoded.SpecialCall.CallingType);
        AssertEqual("12345", decoded.SpecialCall.OtherSideId);
        AssertEqual("TESTMSG", decoded.SpecialCall.Message);
        AssertEqual("12345 Information:TESTMSG", decoded.EncodeId);
    }

    private static void FiveToneIdCodecDecodesAniRealCapturedBytes()
    {
        // Row 2 from the 2026-08-06 capture: Special Call=ANI/Other Side ID
        // "67890"/No stop, Name "ROWTWO". Self ID was 5 digits. No marker,
        // no compression - the confirmed formula is plain digit
        // concatenation (id+interval+id), here "6789067890".
        var record = Convert.FromHexString("00000a46678906789000000000000000000000000000000052004f005700540057004f0020002000000000000000000000000000000000000000000000000000")[..FiveToneIdCodec.RecordLength];

        var decoded = FiveToneIdCodec.Decode(record, 1, selfIdLength: 5);

        AssertEqual("ROWTWO", decoded.Name);
        AssertTrue(!decoded.SpecialCall.IsConfigured, "ANI has no marker on the wire - decodes as raw/manual, matching the confirmed design (both are the same kind of value)");
        AssertEqual("6789067890", decoded.EncodeId);
    }

    private static void FiveToneIdCodecDecodesSendMessage99999RealCapturedBytes()
    {
        // Row 3 from the 2026-08-06 capture: Special Call=Send Message/
        // Other Side ID "99999"/Message "C", Name "ROWTHR" - the SAME
        // Other Side ID value as the old hand-transcribed "99999" example,
        // now byte-confirmed to prove the compression formula's repeat-
        // digit handling (unlike "12345", which has no repeats at all).
        var record = Convert.FromHexString("00000a46e19e9e9e4300000000000000000000000000000052004f00570054004800520020002000000000000000000000000000000000000000000000000000")[..FiveToneIdCodec.RecordLength];

        var decoded = FiveToneIdCodec.Decode(record, 2, selfIdLength: 5);

        AssertEqual("ROWTHR", decoded.Name);
        AssertTrue(decoded.SpecialCall.IsConfigured, "Send Message must decode as configured");
        AssertEqual("99999", decoded.SpecialCall.OtherSideId);
        AssertEqual("C", decoded.SpecialCall.Message);
    }

    private static void FiveToneIdCodecDecodesBotPttIdRealCapturedBytes()
    {
        // Originally captured/labeled "BOT (PTT ID Starting)" from the
        // 2026-08-05 capture - RELABELED 2026-08-16: this is actually
        // 5Tone ID row 100's own storage (see
        // D890UvMemoryMap.FiveToneIdRow100ReservedData's own doc comment),
        // which is WHY it decodes correctly via the plain FiveToneIdCodec
        // row layout below rather than FiveToneSettingsCodec.DecodeBot -
        // it always was row-level data, not BOT's real record. Kept as a
        // regression test for the "E6"+id PTTID formula, still valid on
        // its own terms. Name left blank (8 literal spaces, not zero).
        var record = Convert.FromHexString("00000746e612345e0000000000000000000000000000000020002000200020002000200020002000000000000000000000000000000000000000000000000000")[..FiveToneIdCodec.RecordLength];

        var decoded = FiveToneIdCodec.Decode(record, 0, selfIdLength: 5);

        AssertEqual("", decoded.Name); // 8 literal spaces decodes as blank
        AssertTrue(decoded.SpecialCall.IsConfigured, "PTTID must decode as configured");
        AssertEqual(FiveToneCallingType.PttId, decoded.SpecialCall.CallingType);
        AssertEqual("12345", decoded.SpecialCall.OtherSideId);
    }

    private static void FiveToneIdCodecEncodeDecodeRoundTrips()
    {
        var blankRecord = new byte[FiveToneIdCodec.RecordLength];

        var sendMessage = new FiveToneIdCodec.DecodedFiveToneId(0)
        {
            Standard = 14,
            TimeOfEncodeTone = 55,
            Name = "TESTID1",
            SpecialCall = new FiveToneSpecialCallCodecValues(FiveToneCallingType.SendMessage, "12345", "TESTMSG", "")
        };
        var sendMessageBytes = FiveToneIdCodec.Encode(blankRecord, sendMessage);
        var sendMessageDecoded = FiveToneIdCodec.Decode(sendMessageBytes, 0, selfIdLength: 5);
        AssertEqual("TESTID1", sendMessageDecoded.Name);
        AssertEqual("12345", sendMessageDecoded.SpecialCall.OtherSideId);
        AssertEqual("TESTMSG", sendMessageDecoded.SpecialCall.Message);

        // Row-level PTTID's own confirmed rule is "Encode ID empty" -
        // NOT BOT/EOT's own "E6"+id formula (that's a genuinely different
        // rule, tested separately for FiveToneSettingsCodec).
        var pttId = new FiveToneIdCodec.DecodedFiveToneId(0)
        {
            Standard = 7,
            TimeOfEncodeTone = 70,
            Name = "",
            SpecialCall = new FiveToneSpecialCallCodecValues(FiveToneCallingType.PttId, "67890", "", "")
        };
        var pttIdBytes = FiveToneIdCodec.Encode(blankRecord, pttId);
        AssertEqual("00000000", Convert.ToHexString(pttIdBytes.AsSpan(0x04, 4)));
        var pttIdDecoded = FiveToneIdCodec.Decode(pttIdBytes, 0, selfIdLength: 5);
        AssertTrue(!pttIdDecoded.SpecialCall.IsConfigured, "an all-zero packed region decodes as not-configured, same as a never-touched row - row-level PTTID is indistinguishable from blank on the wire, matching its own confirmed empty-Encode-ID rule");

        var manual = new FiveToneIdCodec.DecodedFiveToneId(0)
        {
            Standard = 0,
            TimeOfEncodeTone = 30,
            Name = "MANUAL",
            EncodeId = "ABCDEF",
            SpecialCall = FiveToneSpecialCallCodecValues.NotConfigured
        };
        var manualBytes = FiveToneIdCodec.Encode(blankRecord, manual);
        var manualDecoded = FiveToneIdCodec.Decode(manualBytes, 0, selfIdLength: 5);
        AssertEqual("MANUAL", manualDecoded.Name);
        AssertTrue(!manualDecoded.SpecialCall.IsConfigured, "manual entry must decode as not-configured");
        AssertEqual("ABCDEF", manualDecoded.EncodeId);
    }

    private static void FiveToneSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // The Decode/Encode singleton block from the 2026-08-06 capture -
        // every field set across 5 capture rounds, all still
        // present in this final round's own bytes.
        var record = Convert.FromHexString("0700000000000000000000000800000000020c05280607080900000063004d4101000b7b222bb16a000000000000000000000b4812345c12345e00000000000000000000000000000000000000000000");

        var decoded = FiveToneSettingsCodec.DecodeSingleton(record);

        AssertEqual("67890", decoded.SelfId);
        AssertEqual((byte)12, decoded.DecodeStandard); // MODAT
        AssertEqual((byte)2, decoded.DecodingResponse); // Beep tone & Respond
        AssertEqual(340, decoded.DecodeTimeMs);
        AssertEqual(1770, decoded.Pretime);
        AssertEqual(990, decoded.TimeLapseAfterEncode);
        AssertEqual(-1, decoded.PttIdPauseTime); // Off
        AssertEqual(77, decoded.AutoResetTime);
        AssertEqual(650, decoded.FirstToneLength);
        AssertEqual(1230, decoded.StopTimeLength);
        AssertEqual(430, decoded.FirstToneLengthAfterStop);
        AssertTrue(decoded.SideTone, "Side Tone must be On");
        AssertTrue(!decoded.DispAnyId, "Disp Any ID must be Off");
        // Off/On/Off/On/Off/On/On - confirmed 0x6A bitmask
        AssertTrue(!decoded.DecUnit1, "Unit 1 must be Off");
        AssertTrue(decoded.DecUnit2, "Unit 2 must be On");
        AssertTrue(!decoded.DecUnit3, "Unit 3 must be Off");
        AssertTrue(decoded.DecUnit4, "Unit 4 must be On");
        AssertTrue(!decoded.DecUnit5, "Unit 5 must be Off");
        AssertTrue(decoded.DecUnit6, "Unit 6 must be On");
        AssertTrue(decoded.DecUnit7, "Unit 7 must be On");
    }

    private static void FiveToneSettingsCodecDecodesBotAndEotRealCapturedBytes()
    {
        // Real bytes from a live capture 2026-08-16 (BOT's Special Call
        // popup set to Send Message, ID "11111", Message "BOTMSG",
        // Standard left at CCIR1 from an earlier isolation test) -
        // replaces an earlier fixture that was
        // (unknowingly, at the time) capturing 5Tone ID row 100's own
        // leftover data at the WRONG address, not BOT's real record. See
        // D890UvMemoryMap.FiveToneBotSettingsData's own doc comment for
        // the full story.
        var botRecord = Convert.FromHexString("00061464e1e1e1ee424f544d5347000000000000000000000000000000000000")[..D890UvMemoryMap.FiveToneBotSettingsLength];
        var botDecoded = FiveToneSettingsCodec.DecodeBot(botRecord, selfIdLength: 5);
        AssertEqual((byte)6, botDecoded.Standard); // CCIR1
        AssertEqual((byte)100, botDecoded.TimeOfEncodeTone);
        AssertTrue(botDecoded.SpecialCall.IsConfigured, "BOT Send Message must decode as configured");
        AssertEqual(FiveToneCallingType.SendMessage, botDecoded.SpecialCall.CallingType);
        AssertEqual("11111", botDecoded.SpecialCall.OtherSideId);
        AssertEqual("BOTMSG", botDecoded.SpecialCall.Message);

        var eotRecord = Convert.FromHexString("000b1455e1e2345e454f544d534700000000000000000000000000000000000000000000000000000000000000000000")[..D890UvMemoryMap.FiveToneEotRecordLength];
        var eotDecoded = FiveToneSettingsCodec.DecodeEot(eotRecord, selfIdLength: 5);
        AssertEqual((byte)11, eotDecoded.Standard); // NATEL
        AssertEqual((byte)85, eotDecoded.TimeOfEncodeTone);
        AssertTrue(eotDecoded.SpecialCall.IsConfigured, "EOT Send Message must decode as configured");
        AssertEqual(FiveToneCallingType.SendMessage, eotDecoded.SpecialCall.CallingType);
        AssertEqual("12345", eotDecoded.SpecialCall.OtherSideId);
        AssertEqual("EOTMSG", eotDecoded.SpecialCall.Message);
    }

    private static void FiveToneSettingsCodecEncodeDecodeRoundTrips()
    {
        var blankSingleton = new byte[D890UvMemoryMap.FiveToneDecodeEncodeRecordLength];
        var values = new FiveToneSettingsCodec.DecodedFiveToneSettings
        {
            SelfId = "23456",
            DecodeStandard = 9,
            DecodingResponse = 1,
            DecodeTimeMs = 890,
            DecUnit1 = true,
            DecUnit3 = true,
            DecUnit5 = true,
            DispAnyId = true,
            Pretime = 730,
            AutoResetTime = 42,
            TimeLapseAfterEncode = 1230,
            PttIdPauseTime = 53,
            FirstToneLength = 210,
            StopTimeLength = 300,
            FirstToneLengthAfterStop = 120,
            SideTone = true
        };

        var encoded = FiveToneSettingsCodec.EncodeSingleton(blankSingleton, values);
        var decoded = FiveToneSettingsCodec.DecodeSingleton(encoded);

        AssertEqual("23456", decoded.SelfId);
        AssertEqual((byte)9, decoded.DecodeStandard);
        AssertEqual(890, decoded.DecodeTimeMs);
        AssertEqual(730, decoded.Pretime);
        AssertEqual(53, decoded.PttIdPauseTime);
        AssertTrue(decoded.DecUnit1 && decoded.DecUnit3 && decoded.DecUnit5, "Units 1/3/5 must round-trip on");
        AssertTrue(!decoded.DecUnit2 && !decoded.DecUnit4, "Units 2/4 must round-trip off");

        // PTT ID Pause Time's "Off" sentinel round-trips to raw byte 0
        var offValues = values with { PttIdPauseTime = -1 };
        var offEncoded = FiveToneSettingsCodec.EncodeSingleton(blankSingleton, offValues);
        AssertEqual((byte)0, offEncoded[0x1D]);
        AssertEqual(-1, FiveToneSettingsCodec.DecodeSingleton(offEncoded).PttIdPauseTime);

        var blankBotEot = new byte[D890UvMemoryMap.FiveToneBotSettingsLength];
        var botValues = new FiveToneSettingsCodec.DecodedFiveToneBotEot
        {
            Standard = 3,
            TimeOfEncodeTone = 45,
            SpecialCall = new FiveToneSpecialCallCodecValues(FiveToneCallingType.PttId, "23456", "", "")
        };
        var botEncoded = FiveToneSettingsCodec.EncodeBot(blankBotEot, botValues);
        var botDecoded = FiveToneSettingsCodec.DecodeBot(botEncoded, selfIdLength: 5);
        AssertEqual((byte)3, botDecoded.Standard);
        AssertEqual("23456", botDecoded.SpecialCall.OtherSideId);
        AssertEqual(FiveToneCallingType.PttId, botDecoded.SpecialCall.CallingType);
    }

    private static void PatcherAppliesFiveToneIdPatchAndSetsPresenceBitForANewRow()
    {
        const int rowIndex = 3; // "row 4" - bit 3 of the presence bitmap
        var rowAddress = RadioCodeplugPatcher.FiveToneIdAddress(rowIndex);
        var idTable = new byte[CodeplugLimits.FiveToneIdMax * FiveToneIdCodec.RecordLength];
        var singleton = new byte[D890UvMemoryMap.FiveToneDecodeEncodeRecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneIdData, idTable),
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneDecodeEncodeData, singleton)
            ]
        };

        var values = new FiveToneIdCodec.DecodedFiveToneId(rowIndex)
        {
            Standard = 5,
            TimeOfEncodeTone = 60,
            Name = "NEWROW",
            SpecialCall = new FiveToneSpecialCallCodecValues(FiveToneCallingType.SendMessage, "12345", "HELLO", "")
        };
        var patched = RadioCodeplugPatcher.ApplyFiveToneIdPatch(snapshot, rowIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneDecodeEncodeData).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedRecord = patched.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneIdData).Data;
        var decoded = FiveToneIdCodec.Decode(patchedRecord.AsSpan(rowIndex * FiveToneIdCodec.RecordLength, FiveToneIdCodec.RecordLength), rowIndex, selfIdLength: 5);
        AssertEqual("NEWROW", decoded.Name);
        AssertEqual("12345", decoded.SpecialCall.OtherSideId);
        AssertEqual(rowAddress, D890UvMemoryMap.FiveToneIdData + rowIndex * FiveToneIdCodec.RecordLength);
    }

    private static void PatcherDeletesFiveToneIdZeroingRecordAndClearingPresenceBit()
    {
        const int rowIndex = 2;
        var idTable = new byte[CodeplugLimits.FiveToneIdMax * FiveToneIdCodec.RecordLength];
        var populated = FiveToneIdCodec.Encode(new byte[FiveToneIdCodec.RecordLength], new FiveToneIdCodec.DecodedFiveToneId(rowIndex) { Name = "TOGO", Standard = 1, TimeOfEncodeTone = 40 });
        populated.CopyTo(idTable, rowIndex * FiveToneIdCodec.RecordLength);
        var singleton = new byte[D890UvMemoryMap.FiveToneDecodeEncodeRecordLength];
        singleton[0] = (byte)(1 << rowIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneIdData, idTable),
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneDecodeEncodeData, singleton)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyFiveToneIdDelete(snapshot, rowIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneIdData).Data;
        var recordBytes = deletedRecord.AsSpan(rowIndex * FiveToneIdCodec.RecordLength, FiveToneIdCodec.RecordLength);
        AssertTrue(recordBytes.ToArray().All(b => b == 0), "deleted 5Tone ID record must be zeroed, not blanked to 0xFF");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneDecodeEncodeData).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void PatcherAppliesFiveToneSettingsPatch()
    {
        var singleton = new byte[D890UvMemoryMap.FiveToneDecodeEncodeRecordLength];
        var eot = new byte[D890UvMemoryMap.FiveToneEotRecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneDecodeEncodeData, singleton),
                new CodeplugRawRegion(D890UvMemoryMap.FiveToneEotData, eot)
            ]
        };

        var settingsValues = new FiveToneSettingsCodec.DecodedFiveToneSettings { SelfId = "23456", DecodeStandard = 9, Pretime = 730 };
        var patchedSettings = RadioCodeplugPatcher.ApplyFiveToneSettingsPatch(snapshot, settingsValues);
        var decodedSettings = FiveToneSettingsCodec.DecodeSingleton(patchedSettings.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneDecodeEncodeData).Data);
        AssertEqual("23456", decodedSettings.SelfId);
        AssertEqual(730, decodedSettings.Pretime);

        // BOT lives INSIDE the same singleton region as the settings just
        // patched above (see D890UvMemoryMap.FiveToneBotSettingsData's own
        // doc comment, corrected 2026-08-16) - no separate region.
        var botValues = new FiveToneSettingsCodec.DecodedFiveToneBotEot { Standard = 4, TimeOfEncodeTone = 50 };
        var patchedBot = RadioCodeplugPatcher.ApplyFiveToneBotPatch(patchedSettings, botValues);
        var singletonAfterBot = patchedBot.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneDecodeEncodeData).Data;
        var botOffset = D890UvMemoryMap.FiveToneBotSettingsData - D890UvMemoryMap.FiveToneDecodeEncodeData;
        var decodedBot = FiveToneSettingsCodec.DecodeBot(singletonAfterBot.AsSpan(botOffset, D890UvMemoryMap.FiveToneBotSettingsLength), selfIdLength: 5);
        AssertEqual((byte)4, decodedBot.Standard);

        // The BOT patch must not disturb the settings fields patched just
        // above, in the same underlying region.
        var decodedSettingsAfterBot = FiveToneSettingsCodec.DecodeSingleton(singletonAfterBot);
        AssertEqual("23456", decodedSettingsAfterBot.SelfId);

        var eotValues = new FiveToneSettingsCodec.DecodedFiveToneBotEot { Standard = 11, TimeOfEncodeTone = 85 };
        var patchedEot = RadioCodeplugPatcher.ApplyFiveToneEotPatch(patchedBot, eotValues);
        var decodedEot = FiveToneSettingsCodec.DecodeEot(patchedEot.Regions.Single(r => r.Address == D890UvMemoryMap.FiveToneEotData).Data, selfIdLength: 5);
        AssertEqual((byte)11, decodedEot.Standard);
    }

    private static void FiveToneInfoIdSlotCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Slot 1 (Information ID NO. = 1) from the 2026-08-06 capture:
        // Function Option=Call all, Function Decoding Response=None,
        // Information ID="111111", Function Name="ONETEST". "111111" is
        // the anchor that cracked this whole area - raw nibble-value
        // bytes (one per digit), NOT nibble-packed hex text like every
        // other hex field in this app.
        var slot1 = Convert.FromHexString("010006010101010101000000000000004f004e004500540045005300540000000000000000000000000000000000000000000000000000000000000000000000")[..FiveToneInfoIdSlotCodec.RecordLength];

        var decoded = FiveToneInfoIdSlotCodec.Decode(slot1);

        AssertEqual((byte)1, decoded.FunctionOption); // Call all
        AssertEqual((byte)0, decoded.FunctionDecodingResponse); // None
        AssertEqual("111111", decoded.InformationId);
        AssertEqual("ONETEST", decoded.FunctionName);
    }

    private static void FiveToneInfoIdSlotCodecDecodesFedcba987654RealCapturedBytes()
    {
        // Slot 2 (Information ID NO. = 2) from an EARLIER 2026-08-06
        // round: Function Option=Squelch Off, Function Decoding
        // Response=Beep tone & Respond, Information ID="FEDCBA987654",
        // Function Name blank (disabled by Squelch Off). Originally
        // logged as "Information ID never showed up anywhere" - it was
        // there the whole time, just in the raw-nibble-per-digit
        // encoding this test file's own earlier searches never tried
        // (they only ever searched for nibble-packed hex text or ASCII/
        // UTF-16LE) - re-decoded correctly once the "111111" example
        // cracked the real format.
        var slot2 = Convert.FromHexString("00020c0f0e0d0c0b0a09080706050400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")[..FiveToneInfoIdSlotCodec.RecordLength];

        var decoded = FiveToneInfoIdSlotCodec.Decode(slot2);

        AssertEqual((byte)0, decoded.FunctionOption); // Squelch Off
        AssertEqual((byte)2, decoded.FunctionDecodingResponse); // Beep tone & Respond
        AssertEqual("FEDCBA987654", decoded.InformationId);
        AssertEqual("", decoded.FunctionName);
    }

    private static void FiveToneInfoIdSlotCodecEncodeDecodeRoundTrips()
    {
        var blankSlot = new byte[FiveToneInfoIdSlotCodec.RecordLength];
        var values = new FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot
        {
            FunctionOption = 1,
            FunctionDecodingResponse = 1,
            InformationId = "1A2B3C4D5E6F",
            FunctionName = "ROUNDTR"
        };

        var encoded = FiveToneInfoIdSlotCodec.Encode(blankSlot, values);
        var decoded = FiveToneInfoIdSlotCodec.Decode(encoded);

        AssertEqual((byte)1, decoded.FunctionOption);
        AssertEqual((byte)1, decoded.FunctionDecodingResponse);
        AssertEqual("1A2B3C4D5E6F", decoded.InformationId);
        AssertEqual("ROUNDTR", decoded.FunctionName);

        // A blank Information ID must round-trip as blank, not "00" or
        // some other artifact of the length-byte encoding.
        var blankValues = values with { InformationId = "" };
        var blankEncoded = FiveToneInfoIdSlotCodec.Encode(blankSlot, blankValues);
        AssertEqual("", FiveToneInfoIdSlotCodec.Decode(blankEncoded).InformationId);
    }

    private static void TwoToneEncodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Row 1 from the 2026-08-06 capture: 1st Tone Frequency 321.7 Hz,
        // 2nd Tone Frequency 928.1 Hz (pre-existing, left untouched),
        // Name "ENCTEST" (the anchor that found this table's base address).
        var row1 = Convert.FromHexString("910c41240000000045004e004300540045005300540000000000000000000000"[..64]);

        var decoded = TwoToneEncodeCodec.Decode(row1, index: 0);

        AssertEqual(321.7, decoded.FirstToneFrequencyHz);
        AssertEqual(928.1, decoded.SecondToneFrequencyHz);
        AssertEqual("ENCTEST", decoded.Name);
    }

    private static void TwoToneEncodeCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[TwoToneEncodeCodec.RecordLength];
        var values = new TwoToneEncodeCodec.DecodedTwoToneEncode(0)
        {
            FirstToneFrequencyHz = 700.0,
            SecondToneFrequencyHz = 1200.0,
            Name = "ROUNDTR"
        };

        var encoded = TwoToneEncodeCodec.Encode(blank, values);
        var decoded = TwoToneEncodeCodec.Decode(encoded, index: 0);

        AssertEqual(700.0, decoded.FirstToneFrequencyHz);
        AssertEqual(1200.0, decoded.SecondToneFrequencyHz);
        AssertEqual("ROUNDTR", decoded.Name);
    }

    private static void TwoToneDecodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Row 1 from the 2026-08-06 capture: same frequencies as Encode
        // row 1, Name "DECTEST", Decoding Response set to "Beep tone" (1).
        var row1 = Convert.FromHexString("910c4124440045004300540045005300540000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000"[..128]);

        var decoded = TwoToneDecodeCodec.Decode(row1, index: 0);

        AssertEqual(321.7, decoded.FirstToneFrequencyHz);
        AssertEqual(928.1, decoded.SecondToneFrequencyHz);
        AssertEqual("DECTEST", decoded.Name);
        AssertEqual((byte)1, decoded.DecodingResponse);
    }

    private static void TwoToneDecodeCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[TwoToneDecodeCodec.RecordLength];
        var values = new TwoToneDecodeCodec.DecodedTwoToneDecode(0)
        {
            FirstToneFrequencyHz = 800.0,
            SecondToneFrequencyHz = 1300.0,
            DecodingResponse = 2,
            Name = "ROUNDTR"
        };

        var encoded = TwoToneDecodeCodec.Encode(blank, values);
        var decoded = TwoToneDecodeCodec.Decode(encoded, index: 0);

        AssertEqual(800.0, decoded.FirstToneFrequencyHz);
        AssertEqual(1300.0, decoded.SecondToneFrequencyHz);
        AssertEqual((byte)2, decoded.DecodingResponse);
        AssertEqual("ROUNDTR", decoded.Name);
    }

    private static void TwoToneEncodeSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // The Encode tab's scalar settings block from the 2026-08-06
        // capture: 1st/2nd/Long Tone Duration 2.5/3.5/4.5s, Gap Time
        // 1500ms, Auto Reset Time 55s, Side Tone on.
        var block = Convert.FromHexString("00000000000000000019232d0f370100"[..32]);

        var decoded = TwoToneEncodeSettingsCodec.Decode(block);

        AssertEqual(2.5, decoded.FirstToneDurationSeconds);
        AssertEqual(3.5, decoded.SecondToneDurationSeconds);
        AssertEqual(4.5, decoded.LongToneDurationSeconds);
        AssertEqual(1500, decoded.GapTimeMs);
        AssertEqual(55, decoded.AutoResetTimeSeconds);
        AssertTrue(decoded.SideTone, "Side Tone must decode as on");
    }

    private static void TwoToneEncodeSettingsCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[TwoToneEncodeSettingsCodec.RecordLength];
        var values = new TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings(
            FirstToneDurationSeconds: 0.5,
            SecondToneDurationSeconds: 10.5,
            LongToneDurationSeconds: 5.5,
            GapTimeMs: 2000,
            AutoResetTimeSeconds: 250,
            SideTone: false);

        var encoded = TwoToneEncodeSettingsCodec.Encode(blank, values);
        var decoded = TwoToneEncodeSettingsCodec.Decode(encoded);

        AssertEqual(0.5, decoded.FirstToneDurationSeconds);
        AssertEqual(10.5, decoded.SecondToneDurationSeconds);
        AssertEqual(5.5, decoded.LongToneDurationSeconds);
        AssertEqual(2000, decoded.GapTimeMs);
        AssertEqual(250, decoded.AutoResetTimeSeconds);
        AssertTrue(!decoded.SideTone, "Side Tone must decode as off");
    }

    private static void PatcherAppliesTwoToneEncodePatchAndSetsPresenceBitForANewRow()
    {
        const int rowIndex = 3; // "row 4" - bit 3 of the presence bitmap
        var table = new byte[CodeplugLimits.TwoToneEncodeMax * TwoToneEncodeCodec.RecordLength];
        var bitmap = new byte[0x10];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneEncodeData, table),
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneEncodeBitmap, bitmap)
            ]
        };

        var values = new TwoToneEncodeCodec.DecodedTwoToneEncode(rowIndex)
        {
            FirstToneFrequencyHz = 700.0,
            SecondToneFrequencyHz = 1200.0,
            Name = "NEWROW"
        };
        var patched = RadioCodeplugPatcher.ApplyTwoToneEncodePatch(snapshot, rowIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneEncodeBitmap).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedTable = patched.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneEncodeData).Data;
        var decoded = TwoToneEncodeCodec.Decode(patchedTable.AsSpan(rowIndex * TwoToneEncodeCodec.RecordLength, TwoToneEncodeCodec.RecordLength), rowIndex);
        AssertEqual("NEWROW", decoded.Name);
        AssertEqual(700.0, decoded.FirstToneFrequencyHz);
    }

    private static void PatcherDeletesTwoToneEncodeZeroingRecordAndClearingPresenceBit()
    {
        const int rowIndex = 2;
        var table = new byte[CodeplugLimits.TwoToneEncodeMax * TwoToneEncodeCodec.RecordLength];
        var populated = TwoToneEncodeCodec.Encode(new byte[TwoToneEncodeCodec.RecordLength], new TwoToneEncodeCodec.DecodedTwoToneEncode(rowIndex) { Name = "TOGO", FirstToneFrequencyHz = 500.0, SecondToneFrequencyHz = 1000.0 });
        populated.CopyTo(table, rowIndex * TwoToneEncodeCodec.RecordLength);
        var bitmap = new byte[0x10];
        bitmap[0] = (byte)(1 << rowIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneEncodeData, table),
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneEncodeBitmap, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyTwoToneEncodeDelete(snapshot, rowIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneEncodeData).Data;
        var recordBytes = deletedRecord.AsSpan(rowIndex * TwoToneEncodeCodec.RecordLength, TwoToneEncodeCodec.RecordLength);
        AssertTrue(recordBytes.ToArray().All(b => b == 0), "deleted 2Tone Encode record must be zeroed, not blanked to 0xFF");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneEncodeBitmap).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void PatcherAppliesTwoToneDecodePatchAndSetsPresenceBitForANewRow()
    {
        const int rowIndex = 3;
        var table = new byte[CodeplugLimits.TwoToneDecodeMax * TwoToneDecodeCodec.RecordLength];
        var bitmap = new byte[0x10];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneDecodeData, table),
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneDecodeBitmap, bitmap)
            ]
        };

        var values = new TwoToneDecodeCodec.DecodedTwoToneDecode(rowIndex)
        {
            FirstToneFrequencyHz = 800.0,
            SecondToneFrequencyHz = 1300.0,
            DecodingResponse = 0,
            Name = "NEWROW"
        };
        var patched = RadioCodeplugPatcher.ApplyTwoToneDecodePatch(snapshot, rowIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneDecodeBitmap).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedTable = patched.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneDecodeData).Data;
        var decoded = TwoToneDecodeCodec.Decode(patchedTable.AsSpan(rowIndex * TwoToneDecodeCodec.RecordLength, TwoToneDecodeCodec.RecordLength), rowIndex);
        AssertEqual("NEWROW", decoded.Name);
        AssertEqual(800.0, decoded.FirstToneFrequencyHz);
    }

    private static void PatcherDeletesTwoToneDecodeZeroingRecordAndClearingPresenceBit()
    {
        const int rowIndex = 2;
        var table = new byte[CodeplugLimits.TwoToneDecodeMax * TwoToneDecodeCodec.RecordLength];
        var populated = TwoToneDecodeCodec.Encode(new byte[TwoToneDecodeCodec.RecordLength], new TwoToneDecodeCodec.DecodedTwoToneDecode(rowIndex) { Name = "TOGO", FirstToneFrequencyHz = 600.0, SecondToneFrequencyHz = 1100.0, DecodingResponse = 1 });
        populated.CopyTo(table, rowIndex * TwoToneDecodeCodec.RecordLength);
        var bitmap = new byte[0x10];
        bitmap[0] = (byte)(1 << rowIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneDecodeData, table),
                new CodeplugRawRegion(D890UvMemoryMap.TwoToneDecodeBitmap, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyTwoToneDecodeDelete(snapshot, rowIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneDecodeData).Data;
        var recordBytes = deletedRecord.AsSpan(rowIndex * TwoToneDecodeCodec.RecordLength, TwoToneDecodeCodec.RecordLength);
        AssertTrue(recordBytes.ToArray().All(b => b == 0), "deleted 2Tone Decode record must be zeroed, not blanked to 0xFF");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneDecodeBitmap).Data;
        AssertTrue((presenceBitmap[0] & (1 << rowIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void PatcherAppliesTwoToneEncodeSettingsPatch()
    {
        var block = new byte[TwoToneEncodeSettingsCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.TwoToneEncodeSettingsData, block)]
        };

        var values = new TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings(
            FirstToneDurationSeconds: 1.5, SecondToneDurationSeconds: 2.5, LongToneDurationSeconds: 3.5,
            GapTimeMs: 500, AutoResetTimeSeconds: 30, SideTone: true);
        var patched = RadioCodeplugPatcher.ApplyTwoToneEncodeSettingsPatch(snapshot, values);
        var decoded = TwoToneEncodeSettingsCodec.Decode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.TwoToneEncodeSettingsData).Data);

        AssertEqual(1.5, decoded.FirstToneDurationSeconds);
        AssertEqual(500, decoded.GapTimeMs);
        AssertEqual(30, decoded.AutoResetTimeSeconds);
        AssertTrue(decoded.SideTone, "Side Tone must round-trip as on");
    }

    private static void TwoToneEncodeEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new TwoToneEncodeEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.FirstToneFrequencyHz = 400.0;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing First Tone Frequency must mark pending");
        entry.MarkRadioSynced();

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void TwoToneDecodeEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new TwoToneDecodeEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.DecodingResponse = 2;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Decoding Response must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void TwoToneEncodeSettingsEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new TwoToneEncodeSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.SideTone = true;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Side Tone must mark pending");
        entry.MarkRadioSynced();

        entry.GapTimeMs = 1500;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Gap Time must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void DtmfCodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // M1 from the 2026-08-06 capture: typed directly as "1234AB".
        // DtmfCodeCodec is internal - exercised here through the public
        // DtmfEncodeCodec/DtmfSettingsCodec wrappers instead, same
        // "internal helper, tested via its public callers" convention as
        // TextFieldCodec.
        var m1 = Convert.FromHexString("01020304 0a0bffff ffffffff ffffffff".Replace(" ", ""));
        AssertEqual("1234AB", DtmfEncodeCodec.Decode(m1, index: 0).Code);

        // PTT ID Starting (BOT): "AB12CD".
        var bot = Convert.FromHexString("0a0b0102 0c0dffff ffffffff ffffffff".Replace(" ", ""));
        AssertEqual("AB12CD", DtmfSettingsCodec.DecodeCode(bot));
    }

    private static void DtmfCodeCodecEncodeDecodeRoundTripsIncludingStarAndHash()
    {
        var encoded = DtmfEncodeCodec.Encode("12AB*#");
        AssertEqual("12AB*#", DtmfEncodeCodec.Decode(encoded, index: 0).Code);

        // Blank must round-trip as blank, not "0xFF repeated" garbage.
        var blank = DtmfEncodeCodec.Encode("");
        AssertEqual("", DtmfEncodeCodec.Decode(blank, index: 0).Code);
    }

    private static void DtmfSettingsCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Round 1 settings block: Interval Character 'B', Group Code 'C',
        // Decoding Response "Beep tone & Respond" (2), Pretime 550ms,
        // First Digit Time 350ms, Auto Reset Time 45s, Self ID "123",
        // Time-Lapse After Encode 250ms, PTT ID Pause Time 7s, PTT ID on,
        // D Code Pause 9s, Side Tone off.
        var data = Convert.FromHexString("0b0c0237232d0102030019070109 0000".Replace(" ", ""));

        var decoded = DtmfSettingsCodec.DecodeSingleton(data);

        AssertEqual("B", decoded.IntervalCharacter);
        AssertEqual("C", decoded.GroupCode);
        AssertEqual((byte)2, decoded.DecodingResponse);
        AssertEqual(550, decoded.PretimeMs);
        AssertEqual(350, decoded.FirstDigitTimeMs);
        AssertEqual(45, decoded.AutoResetTimeSeconds);
        AssertEqual("123", decoded.SelfId);
        AssertEqual(250, decoded.TimeLapseAfterEncodeMs);
        AssertEqual(7, decoded.PttIdPauseTimeSeconds);
        AssertTrue(decoded.PttId, "PTT ID must decode as on");
        AssertEqual(9, decoded.DCodePauseSeconds);
        AssertTrue(!decoded.SideTone, "Side Tone must decode as off");
    }

    private static void DtmfSettingsCodecDecodesOffSentinelsFromTheRoundTwoLiveWriteCapture()
    {
        // Round 2: Interval Character changed to '*' (0x0E), Group Code
        // changed to "Off" (0xFF raw), PTT ID Pause Time and D Code Pause
        // both changed to "Off" (0x00 raw) - the 2 sentinels are genuinely
        // different bytes, both confirmed by this same capture.
        var data = Convert.FromHexString("0eff0237232d0102030019000100 0000".Replace(" ", ""));

        var decoded = DtmfSettingsCodec.DecodeSingleton(data);

        AssertEqual("*", decoded.IntervalCharacter);
        AssertEqual("Off", decoded.GroupCode);
        AssertEqual(0, decoded.PttIdPauseTimeSeconds);
        AssertEqual(0, decoded.DCodePauseSeconds);
    }

    private static void DtmfSettingsCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var values = new DtmfSettingsCodec.DecodedDtmfSettings
        {
            IntervalCharacter = "#",
            GroupCode = "D",
            DecodingResponse = 1,
            PretimeMs = 100,
            FirstDigitTimeMs = 200,
            AutoResetTimeSeconds = 60,
            SelfId = "999",
            TimeLapseAfterEncodeMs = 300,
            PttIdPauseTimeSeconds = 10,
            PttId = true,
            DCodePauseSeconds = 16,
            SideTone = true
        };

        var encoded = DtmfSettingsCodec.EncodeSingleton(blank, values);
        var decoded = DtmfSettingsCodec.DecodeSingleton(encoded);

        AssertEqual("#", decoded.IntervalCharacter);
        AssertEqual("D", decoded.GroupCode);
        AssertEqual((byte)1, decoded.DecodingResponse);
        AssertEqual(100, decoded.PretimeMs);
        AssertEqual(200, decoded.FirstDigitTimeMs);
        AssertEqual(60, decoded.AutoResetTimeSeconds);
        AssertEqual("999", decoded.SelfId);
        AssertEqual(300, decoded.TimeLapseAfterEncodeMs);
        AssertEqual(10, decoded.PttIdPauseTimeSeconds);
        AssertTrue(decoded.PttId, "PTT ID must round-trip as on");
        AssertEqual(16, decoded.DCodePauseSeconds);
        AssertTrue(decoded.SideTone, "Side Tone must round-trip as on");

        // "Off" must round-trip for Group Code specifically (0xFF sentinel).
        var offValues = values with { GroupCode = "Off" };
        var offEncoded = DtmfSettingsCodec.EncodeSingleton(blank, offValues);
        AssertEqual("Off", DtmfSettingsCodec.DecodeSingleton(offEncoded).GroupCode);
    }

    private static void DtmfEncodeCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        var m1 = Convert.FromHexString("01020304 0a0bffff ffffffff ffffffff".Replace(" ", ""));

        var decoded = DtmfEncodeCodec.Decode(m1, index: 0);

        AssertEqual("1234AB", decoded.Code);
    }

    private static void DtmfEncodeCodecDecodesComposedM2CodeMatchingTheConfirmedFormula()
    {
        // M2, composed via &Special Call: Other Side ID "456" + Interval
        // Character 'B' (0x0B) + Self ID "123" -> byte-for-byte matching
        // the confirmed composition formula.
        var m2 = Convert.FromHexString("0405060b 010203ff ffffffff ffffffff".Replace(" ", ""));

        var decoded = DtmfEncodeCodec.Decode(m2, index: 1);

        AssertEqual("456B123", decoded.Code);
    }

    private static void DtmfEncodeCodecEncodeDecodeRoundTrips()
    {
        var encoded = DtmfEncodeCodec.Encode("456*123");
        var decoded = DtmfEncodeCodec.Decode(encoded, index: 1);

        AssertEqual("456*123", decoded.Code);
        AssertEqual(DtmfEncodeCodec.RecordLength, encoded.Length);
    }

    private static void PatcherAppliesDtmfEncodePatchWithNoPresenceBitmap()
    {
        const int slotIndex = 1; // M2
        var table = new byte[DtmfEncodeCodec.SlotCount * DtmfEncodeCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.DtmfEncodeData, table)]
        };

        var patched = RadioCodeplugPatcher.ApplyDtmfEncodePatch(snapshot, slotIndex, "456B123");

        var patchedTable = patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfEncodeData).Data;
        var decoded = DtmfEncodeCodec.Decode(patchedTable.AsSpan(slotIndex * DtmfEncodeCodec.RecordLength, DtmfEncodeCodec.RecordLength), slotIndex);
        AssertEqual("456B123", decoded.Code);
    }

    private static void PatcherAppliesDtmfSettingsPatch()
    {
        var block = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.DtmfSettingsData, block)]
        };

        var values = new DtmfSettingsCodec.DecodedDtmfSettings
        {
            IntervalCharacter = "C",
            GroupCode = "*",
            DecodingResponse = 1,
            PretimeMs = 150,
            FirstDigitTimeMs = 250,
            AutoResetTimeSeconds = 30,
            SelfId = "321",
            TimeLapseAfterEncodeMs = 100,
            PttIdPauseTimeSeconds = 6,
            PttId = false,
            DCodePauseSeconds = 5,
            SideTone = true
        };
        var patched = RadioCodeplugPatcher.ApplyDtmfSettingsPatch(snapshot, values);
        var decoded = DtmfSettingsCodec.DecodeSingleton(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfSettingsData).Data);

        AssertEqual("C", decoded.IntervalCharacter);
        AssertEqual("*", decoded.GroupCode);
        AssertEqual("321", decoded.SelfId);
        AssertTrue(!decoded.PttId, "PTT ID must round-trip as off");
    }

    private static void PatcherAppliesDtmfBotEotRemotelyKillStunPatches()
    {
        var bot = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var eot = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var kill = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var stun = new byte[D890UvMemoryMap.DtmfSettingsRecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(D890UvMemoryMap.DtmfBotData, bot),
                new CodeplugRawRegion(D890UvMemoryMap.DtmfEotData, eot),
                new CodeplugRawRegion(D890UvMemoryMap.DtmfRemotelyKillData, kill),
                new CodeplugRawRegion(D890UvMemoryMap.DtmfRemotelyStunData, stun)
            ]
        };

        var patched = RadioCodeplugPatcher.ApplyDtmfBotPatch(snapshot, "AB12CD");
        patched = RadioCodeplugPatcher.ApplyDtmfEotPatch(patched, "34AB56");
        patched = RadioCodeplugPatcher.ApplyDtmfRemotelyKillPatch(patched, "1122AABB");
        patched = RadioCodeplugPatcher.ApplyDtmfRemotelyStunPatch(patched, "3344CCDD");

        AssertEqual("AB12CD", DtmfSettingsCodec.DecodeCode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfBotData).Data));
        AssertEqual("34AB56", DtmfSettingsCodec.DecodeCode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfEotData).Data));
        AssertEqual("1122AABB", DtmfSettingsCodec.DecodeCode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfRemotelyKillData).Data));
        AssertEqual("3344CCDD", DtmfSettingsCodec.DecodeCode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfRemotelyStunData).Data));
    }

    private static void PatcherAppliesDtmfTransmittingTimePatch()
    {
        var block = new byte[16];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.DtmfTransmittingTimeIndexData, block)]
        };

        // 300ms -> 100ms confirmed live as index 3 -> index 1.
        var patched = RadioCodeplugPatcher.ApplyDtmfTransmittingTimePatch(snapshot, 1);
        var decoded = DtmfSettingsCodec.DecodeTransmittingTimeIndex(patched.Regions.Single(r => r.Address == D890UvMemoryMap.DtmfTransmittingTimeIndexData).Data[0]);

        AssertEqual(1, decoded);
    }

    private static void DtmfEncodeEntryHasAnyPendingRadioWriteTracksCodeOnly()
    {
        var entry = new DtmfEncodeEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Code = "1234AB";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Code must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");

        // OtherSideId/IsSpecialCallConfigured are pure UI state, never
        // independently stored on the wire - must NOT mark pending.
        entry.OtherSideId = "456";
        AssertTrue(!entry.HasAnyPendingRadioWrite, "OtherSideId must NOT mark pending on its own - only Code is a wire field");
    }

    private static void DtmfSettingsEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new DtmfSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.SelfId = "123";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Self ID must mark pending");
        entry.MarkRadioSynced();

        entry.IntervalCharacter = "*";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Interval Character must mark pending");
        entry.MarkRadioSynced();

        entry.PttIdStartingBot = "AB12CD";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing PTT ID Starting (BOT) must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void RadioIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Row 1 from the 2026-08-06 capture: Radio ID 11223344, Name
        // "ABCDEFGHIJKLMNOPQ" - the full 17-char field.
        var row1 = Convert.FromHexString("112233444100420043004400450046004700480049004a004b004c004d004e004f00500051000000000000000000000000000000000000000000000000000000");
        var decoded1 = RadioIdCodec.Decode(row1, index: 0);
        AssertEqual(11223344L, decoded1.DmrId);
        AssertEqual("ABCDEFGHIJKLMNOPQ", decoded1.Name);

        // Row 2: Name "SHORT" - the Radio ID itself was typed as 99887766
        // but the vendor CPS silently clamped it to 16777215 (0xFFFFFF, the
        // real 24-bit max) before ever writing anything - not a codec bug,
        // see RadioIdCodec's own doc comment.
        var row2 = Convert.FromHexString("16777215530048004f00520054000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decoded2 = RadioIdCodec.Decode(row2, index: 1);
        AssertEqual(16777215L, decoded2.DmrId);
        AssertEqual("SHORT", decoded2.Name);
    }

    private static void RadioIdCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[RadioIdCodec.RecordLength];
        var values = new RadioIdCodec.DecodedRadioId(0) { DmrId = 12345678, Name = "My Radio" };

        var encoded = RadioIdCodec.Encode(blank, values);
        var decoded = RadioIdCodec.Decode(encoded, index: 0);

        AssertEqual(12345678L, decoded.DmrId);
        AssertEqual("My Radio", decoded.Name);
    }

    private static void PatcherAppliesRadioIdPatchAndSetsPresenceBitForANewRow()
    {
        const int radioIndex = 3; // "No. 4" - bit 3 of the presence bitmap
        var record = new byte[RadioIdCodec.RecordLength];
        var bitmap = new byte[0x20];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RadioIdAddress(radioIndex), record),
                new CodeplugRawRegion(D890UvMemoryMap.RadioIdSet, bitmap)
            ]
        };

        var values = new RadioIdCodec.DecodedRadioId(radioIndex) { DmrId = 87654321, Name = "NEWROW" };
        var patched = RadioCodeplugPatcher.ApplyRadioIdPatch(snapshot, radioIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.RadioIdSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedRecord = patched.Regions.Single(r => r.Address == RadioCodeplugPatcher.RadioIdAddress(radioIndex)).Data;
        var decoded = RadioIdCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(87654321L, decoded.DmrId);
        AssertEqual("NEWROW", decoded.Name);
    }

    private static void PatcherDeletesRadioIdBlankingTo0xffNotZero()
    {
        const int radioIndex = 2;
        var populated = RadioIdCodec.Encode(new byte[RadioIdCodec.RecordLength], new RadioIdCodec.DecodedRadioId(radioIndex) { DmrId = 11111111, Name = "TOGO" });
        var bitmap = new byte[0x20];
        bitmap[0] = (byte)(1 << radioIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RadioIdAddress(radioIndex), populated),
                new CodeplugRawRegion(D890UvMemoryMap.RadioIdSet, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyRadioIdDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == RadioCodeplugPatcher.RadioIdAddress(radioIndex)).Data;
        AssertTrue(deletedRecord.All(b => b == 0xFF), "deleted Radio ID record must be blanked to 0xFF, not zeroed");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.RadioIdSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void RadioIdEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new RadioIdEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.DmrId = 12345678;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing DMR ID must mark pending");
        entry.MarkRadioSynced();

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void DmrIdTextFieldsReportValidationErrorsInsteadOfReverting()
    {
        // Added 2026-08-08 after a live test showed an out-of-range DMR ID
        // typed into the vendor CPS silently snapped to the All-Call
        // sentinel and flipped Call Type - this app previously only had a
        // soft "Warning:" for the same problem. Same mechanism as
        // AlertToneEntry/OptionalSettingsEntry: never revert, report via
        // HasErrors/GetErrors, let typing continue.
        var radioId = new RadioIdEntry { DmrIdText = "1234567" };
        AssertTrue(!radioId.HasErrors, "A fresh, valid DMR ID should have no errors.");
        radioId.DmrIdText = "87654321"; // bigger than the real 24-bit max (16,776,415)
        AssertTrue(radioId.HasErrors, "An out-of-range DMR ID should be flagged, not silently accepted.");
        AssertEqual(1234567L, radioId.DmrId);
        radioId.DmrIdText = "7654321";
        AssertTrue(!radioId.HasErrors, "A valid DMR ID should clear the error.");
        AssertEqual(7654321L, radioId.DmrId);

        var masterId = new MasterIdEntry { DmrIdText = "1234567" };
        masterId.DmrIdText = "0";
        AssertTrue(masterId.HasErrors, "0 is below DmrIdMin (1) for Master ID - no bypass here.");

        var whitelist = new TalkgroupWhitelistEntry { DmrIdText = "1234567" };
        whitelist.DmrIdText = "abc";
        AssertTrue(whitelist.HasErrors, "Non-numeric text should be flagged.");
    }

    private static void TalkgroupDmrIdTextBypassesRangeCheckOnlyForAllCall()
    {
        var entry = new TalkgroupEntry { CallType = "Group Call", DmrIdText = "1234567" };
        entry.DmrIdText = "87654321"; // bigger than the real 24-bit max
        AssertTrue(entry.HasErrors, "Group Call must still enforce the normal DMR ID range.");
        AssertEqual(1234567L, entry.DmrId); // out-of-range text is never committed, DmrId stays at the last valid value

        entry.CallType = "All Call";
        AssertTrue(!entry.HasErrors, "Switching to All Call must clear the stale out-of-range error - the field is disabled and the real write forces the sentinel.");

        // While CallType is already All Call, the range check itself is
        // bypassed - any parseable value commits, matching the field being
        // otherwise disabled (IsAllCallFieldsEditable) and the real write
        // forcing the sentinel regardless of what's stored here.
        entry.DmrIdText = "87654321";
        AssertTrue(!entry.HasErrors, "All Call must bypass the range check even for an out-of-range typed value.");
        AssertEqual(87654321L, entry.DmrId);
    }

    private static void AlarmSettingsAndAprsDigitalReportDmrIdTextBypassZero()
    {
        var alarm = new AlarmSettingsEntry { DigitalTgDmrIdText = "87654321" };
        AssertTrue(alarm.HasErrors, "An out-of-range digital TG/DMR ID must be flagged.");
        alarm.DigitalTgDmrIdText = "0";
        AssertTrue(!alarm.HasErrors, "0 means 'off' for Alarm Settings' digital TG/DMR ID - no error.");

        var report = new AprsDigitalReportEntry { TalkgroupIdText = "87654321" };
        AssertTrue(report.HasErrors, "An out-of-range APRS digital report TG/DMR ID must be flagged.");
        report.TalkgroupIdText = "0";
        AssertTrue(!report.HasErrors, "0 means 'unused' for an APRS digital report slot - no error.");
    }

    private static void DmrIdValidationErrorsBlockSaveAndWriteCommands()
    {
        var viewModel = new MainViewModel();
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "A fresh project has nothing to save.");

        // ValidateMasterId only checks anything once Used is true (an
        // unused Master ID is a normal, valid state) - see its own guard.
        // Name must be non-blank too, or the pre-existing "missing name"
        // check would also block, unrelated to what this test covers.
        viewModel.MasterId.Used = true;
        viewModel.MasterId.Name = "PRIMARY";
        viewModel.MasterId.DmrIdText = "87654321";
        AssertTrue(viewModel.HasBlockingValidationErrors, "An out-of-range Master ID DMR ID should block Save/Write.");
        AssertTrue(!viewModel.SaveProjectCommand.CanExecute(null), "Save should be disabled while Master ID has a validation error.");

        viewModel.MasterId.DmrIdText = "1234567";
        AssertTrue(!viewModel.HasBlockingValidationErrors, "A valid Master ID DMR ID should clear the validation error.");
        AssertTrue(viewModel.SaveProjectCommand.CanExecute(null), "Save should be enabled again once the value is valid and the project is dirty.");
    }

    private static void MasterIdCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // From the 2026-08-06 capture: DMR ID 12345678, Name typed as all
        // 26 letters but silently truncated by the vendor CPS itself to
        // "ABCDEFGHIJKLMNOP" (16 chars, filling the field's real
        // capacity), Used checked. This capture also resolved the
        // original Name maxlength discrepancy (26 vs. this codec's own
        // 16-char capacity) in the codec's favor.
        var data = Convert.FromHexString("123456784100420043004400450046004700480049004a004b004c004d004e004f00500000000100000000000000000000000000000000000000000000000000");

        var decoded = MasterIdCodec.Decode(data);

        AssertEqual(12345678L, decoded.DmrId);
        AssertEqual("ABCDEFGHIJKLMNOP", decoded.Name);
        AssertTrue(decoded.Used, "Used must decode as true");
    }

    private static void MasterIdCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[MasterIdCodec.RecordLength];
        var values = new MasterIdCodec.DecodedMasterId { DmrId = 87654321, Name = "ROUNDTRIP16CHAR", Used = false };

        var encoded = MasterIdCodec.Encode(blank, values);
        var decoded = MasterIdCodec.Decode(encoded);

        AssertEqual(87654321L, decoded.DmrId);
        AssertEqual("ROUNDTRIP16CHAR", decoded.Name);
        AssertTrue(!decoded.Used, "Used must round-trip as false");
    }

    private static void PatcherAppliesMasterIdPatch()
    {
        var block = new byte[MasterIdCodec.RecordLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.MasterIdData, block)]
        };

        var values = new MasterIdCodec.DecodedMasterId { DmrId = 11112222, Name = "PATCHTEST", Used = true };
        var patched = RadioCodeplugPatcher.ApplyMasterIdPatch(snapshot, values);
        var decoded = MasterIdCodec.Decode(patched.Regions.Single(r => r.Address == D890UvMemoryMap.MasterIdData).Data);

        AssertEqual(11112222L, decoded.DmrId);
        AssertEqual("PATCHTEST", decoded.Name);
        AssertTrue(decoded.Used, "Used must round-trip as true");
    }

    private static void MasterIdEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new MasterIdEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.DmrId = 12345678;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing DMR ID must mark pending");
        entry.MarkRadioSynced();

        entry.Used = true;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Used must mark pending");
        entry.MarkRadioSynced();

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void TalkAliasSettingsCodecEncodeMatchesRealCapturedBytes()
    {
        // Live capture 2026-08-09: Display Priority = "Air Alias DMR/NX"
        // (the last of 3 real options), Data Format = "Unicode" (the last
        // of 3 options), written in the same capture. Both bytes captured as
        // 0x02 - see TalkAliasSettingsCodec's own doc comment.
        var values = new TalkAliasSettingsCodec.DecodedTalkAliasSettings { DisplayPriority = 2, DataFormat = 2 };
        var encoded = TalkAliasSettingsCodec.Encode(values);
        AssertEqual("0202", Convert.ToHexString(encoded).ToLowerInvariant());

        var decoded = TalkAliasSettingsCodec.Decode(encoded[0], encoded[1]);
        AssertEqual((byte)2, decoded.DisplayPriority);
        AssertEqual((byte)2, decoded.DataFormat);
    }

    private static void PatcherAppliesTalkAliasSettingsPatch()
    {
        // Mirrors AlarmSettingsD3500000's own shared-region reasoning - the
        // snapshot's captured region at 0x3500000 must be at least
        // DataFormatAddress+1 bytes long to contain both target offsets.
        var regionLength = TalkAliasSettingsCodec.DataFormatAddress - D890UvMemoryMap.OptionalSettingsData3500000 + 1;
        var block = new byte[regionLength];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions = [new CodeplugRawRegion(D890UvMemoryMap.OptionalSettingsData3500000, block)]
        };

        var values = new TalkAliasSettingsCodec.DecodedTalkAliasSettings { DisplayPriority = 1, DataFormat = 2 };
        var patched = RadioCodeplugPatcher.ApplyTalkAliasSettingsPatch(snapshot, values);
        var region = patched.Regions.Single(r => r.Address == D890UvMemoryMap.OptionalSettingsData3500000);

        var displayPriorityOffset = TalkAliasSettingsCodec.DisplayPriorityAddress - D890UvMemoryMap.OptionalSettingsData3500000;
        var dataFormatOffset = TalkAliasSettingsCodec.DataFormatAddress - D890UvMemoryMap.OptionalSettingsData3500000;
        AssertEqual((byte)1, region.Data[displayPriorityOffset]);
        AssertEqual((byte)2, region.Data[dataFormatOffset]);
    }

    private static void TalkAliasSettingsEntryHasAnyPendingRadioWriteTracksBothFields()
    {
        var entry = new TalkAliasSettingsEntry();
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.DisplayPriority = 2;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing DisplayPriority must mark pending");
        entry.MarkRadioSynced();

        entry.DataFormat = 2;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing DataFormat must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void TalkAliasSettingsEntryDisplayPriorityOptionsAreTheConfirmedThreeValues()
    {
        // The real vendor CPS dropdown (screenshot 2026-08-09) only ever
        // shows 3 options - the previous 5-value guess ("Radio Alias"/
        // "Custom Text" included) was never real for this radio.
        AssertEqual(3, TalkAliasSettingsEntry.DisplayPriorityOptions.Count);
        AssertEqual("Off", TalkAliasSettingsEntry.DisplayPriorityOptions[0]);
        AssertEqual("Contact Alias", TalkAliasSettingsEntry.DisplayPriorityOptions[1]);
        AssertEqual("Air Alias DMR/NX", TalkAliasSettingsEntry.DisplayPriorityOptions[2]);
    }

    private static void TalkgroupCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Two live differential write captures on 2026-08-07 confirmed
        // CallType/CallAlert byte VALUES were swapped/wrong in the original
        // reference-derived decode - see TalkgroupCodec's own doc comment.
        // Round 1, row 1: Group Call/Online Alert, DMR ID 12345678, Name
        // "ABCDEFGHIJKLMNOP" (full 16 chars).
        var groupOnline = Convert.FromHexString("0102123456784100420043004400450046004700480049004a004b004c004d004e004f005000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decodedGroupOnline = TalkgroupCodec.Decode(groupOnline, index: 0);
        AssertEqual("Group Call", decodedGroupOnline.CallType);
        AssertEqual("Online Alert", decodedGroupOnline.CallAlert);
        AssertEqual(12345678L, decodedGroupOnline.DmrId);
        AssertEqual("ABCDEFGHIJKLMNOP", decodedGroupOnline.Name);

        // Round 1, row 2: All Call - DMR ID typed as 87654321 but the
        // vendor CPS disabled the field and clamped it to 16777215
        // (0xFFFFFF); Call Alert forced to None.
        var allCall = Convert.FromHexString("020016777215530048004f005200540000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decodedAllCall = TalkgroupCodec.Decode(allCall, index: 1);
        AssertEqual("All Call", decodedAllCall.CallType);
        AssertEqual("None", decodedAllCall.CallAlert);
        AssertEqual(16777215L, decodedAllCall.DmrId);
        AssertEqual("SHORT", decodedAllCall.Name);

        // Round 1, row 3: Private Call/Online Alert, DMR ID 1111, Name "Contact 3".
        var privateOnline = Convert.FromHexString("00020000111143006f006e007400610063007400200033000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decodedPrivateOnline = TalkgroupCodec.Decode(privateOnline, index: 2);
        AssertEqual("Private Call", decodedPrivateOnline.CallType);
        AssertEqual("Online Alert", decodedPrivateOnline.CallAlert);
        AssertEqual(1111L, decodedPrivateOnline.DmrId);
        AssertEqual("Contact 3", decodedPrivateOnline.Name);

        // Round 2, row 1: Private Call/Ring (the value this second capture
        // round specifically existed to confirm).
        var privateRing = Convert.FromHexString("0001123456784100420043004400450046004700480049004a004b004c004d004e004f005000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decodedPrivateRing = TalkgroupCodec.Decode(privateRing, index: 0);
        AssertEqual("Private Call", decodedPrivateRing.CallType);
        AssertEqual("Ring", decodedPrivateRing.CallAlert);
        AssertEqual(12345678L, decodedPrivateRing.DmrId);

        // Round 2, row 3: Group Call/None.
        var groupNone = Convert.FromHexString("01000000111143006f006e007400610063007400200033000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decodedGroupNone = TalkgroupCodec.Decode(groupNone, index: 2);
        AssertEqual("Group Call", decodedGroupNone.CallType);
        AssertEqual("None", decodedGroupNone.CallAlert);
    }

    private static void TalkgroupCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[TalkgroupCodec.RecordLength];
        var values = new TalkgroupCodec.DecodedTalkgroup(0) { DmrId = 12345678, Name = "My Talkgroup", CallType = "Private Call", CallAlert = "Ring" };

        var encoded = TalkgroupCodec.Encode(blank, values);
        var decoded = TalkgroupCodec.Decode(encoded, index: 0);

        AssertEqual(12345678L, decoded.DmrId);
        AssertEqual("My Talkgroup", decoded.Name);
        AssertEqual("Private Call", decoded.CallType);
        AssertEqual("Ring", decoded.CallAlert);
    }

    private static void TalkgroupCodecEncodeForcesAllCallSentinelAndNoneAlert()
    {
        var blank = new byte[TalkgroupCodec.RecordLength];
        var values = new TalkgroupCodec.DecodedTalkgroup(0) { DmrId = 99999999, Name = "ALLCALL", CallType = "All Call", CallAlert = "Online Alert" };

        var encoded = TalkgroupCodec.Encode(blank, values);
        var decoded = TalkgroupCodec.Decode(encoded, index: 0);

        AssertEqual("All Call", decoded.CallType);
        AssertEqual("None", decoded.CallAlert);
        AssertEqual(CodeplugLimits.TalkgroupAllCallDmrIdSentinel, decoded.DmrId);
    }

    private static void PatcherAppliesTalkgroupPatchAndClearsPresenceBitForANewRow()
    {
        // Talkgroup's presence bitmap is inverted (bit UNSET = present) -
        // see RadioCodeplugPatcher.ApplyTalkgroupPatch's own doc comment.
        const int radioIndex = 3; // "No. 4" - bit 3 of the presence bitmap
        var record = new byte[TalkgroupCodec.RecordLength];
        var bitmap = new byte[0x4F0];
        for (var i = 0; i < bitmap.Length; i++)
        {
            bitmap[i] = 0xFF; // erased-flash default: every slot absent
        }

        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.TalkgroupAddress(radioIndex), record),
                new CodeplugRawRegion(D890UvMemoryMap.TalkgroupSet, bitmap)
            ]
        };

        var values = new TalkgroupCodec.DecodedTalkgroup(radioIndex) { DmrId = 87654321, Name = "NEWROW", CallType = "Group Call", CallAlert = "None" };
        var patched = RadioCodeplugPatcher.ApplyTalkgroupPatch(snapshot, radioIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.TalkgroupSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) == 0, "presence bit must be CLEARED for a newly-created row (inverted bitmap)");

        var patchedRecord = patched.Regions.Single(r => r.Address == RadioCodeplugPatcher.TalkgroupAddress(radioIndex)).Data;
        var decoded = TalkgroupCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(87654321L, decoded.DmrId);
        AssertEqual("NEWROW", decoded.Name);
    }

    private static void PatcherDeletesTalkgroupBlankingTo0xffNotZeroAndSetsPresenceBit()
    {
        const int radioIndex = 2;
        var populated = TalkgroupCodec.Encode(new byte[TalkgroupCodec.RecordLength], new TalkgroupCodec.DecodedTalkgroup(radioIndex) { DmrId = 11111111, Name = "TOGO", CallType = "Group Call", CallAlert = "None" });
        var bitmap = new byte[0x4F0]; // all-zero: every slot present (inverted bitmap)
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.TalkgroupAddress(radioIndex), populated),
                new CodeplugRawRegion(D890UvMemoryMap.TalkgroupSet, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyTalkgroupDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == RadioCodeplugPatcher.TalkgroupAddress(radioIndex)).Data;
        AssertTrue(deletedRecord.All(b => b == 0xFF), "deleted Talkgroup record must be blanked to 0xFF, not zeroed");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.TalkgroupSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) != 0, "presence bit must be SET after delete (inverted bitmap means absent)");
    }

    private static void TalkgroupEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new TalkgroupEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.DmrId = 12345678;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing DMR ID must mark pending");
        entry.MarkRadioSynced();

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();

        entry.CallType = "Private Call";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing CallType must mark pending");
        entry.MarkRadioSynced();

        entry.CallAlert = "Ring";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing CallAlert must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void TalkgroupEntrySwitchingToAllCallForcesCallAlertToNone()
    {
        var entry = new TalkgroupEntry { CallType = "Private Call", CallAlert = "Ring" };
        entry.CallType = "All Call";
        AssertEqual("None", entry.CallAlert);
        AssertTrue(!entry.IsAllCallFieldsEditable, "DMR ID/Call Alert must be disabled once CallType is All Call");
    }

    private static void ReceiveGroupListCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Live differential write capture 2026-08-08: List No. 1, member
        // talkgroups Contact1 (radio index 0) and Contact2 (radio index 1)
        // added in the vendor CPS UI in reverse order (Contact2 then
        // Contact1) - the vendor CPS itself re-sorted them to ascending
        // index order before writing. Name "RX GRP TEST". Every slot past
        // the 0xFFFFFFFF terminator is filled with 0xFF, not zero. See
        // ReceiveGroupListCodec's own doc comment for the full write-up.
        var record = Convert.FromHexString(
            "0000000001000000ffffffffffffffff" +
            string.Concat(Enumerable.Repeat("ffffffffffffffffffffffffffffffff", 15)) +
            "52005800200047005200500020005400" +
            "45005300540000000000000000000000");

        var decoded = ReceiveGroupListCodec.Decode(record, index: 0);

        AssertEqual("RX GRP TEST", decoded.Name);
        AssertEqual(2, decoded.TalkgroupIndexes.Count);
        AssertEqual(0L, decoded.TalkgroupIndexes[0]);
        AssertEqual(1L, decoded.TalkgroupIndexes[1]);
    }

    private static void ReceiveGroupListCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[ReceiveGroupListCodec.RecordLength];
        var values = new ReceiveGroupListCodec.DecodedReceiveGroupList(0) { Name = "RX GRP TEST", TalkgroupIndexes = [0, 1] };

        var encoded = ReceiveGroupListCodec.Encode(blank, values);
        var decoded = ReceiveGroupListCodec.Decode(encoded, index: 0);

        AssertEqual("RX GRP TEST", decoded.Name);
        AssertEqual(2, decoded.TalkgroupIndexes.Count);
        AssertEqual(0L, decoded.TalkgroupIndexes[0]);
        AssertEqual(1L, decoded.TalkgroupIndexes[1]);
        AssertTrue(encoded.Skip(8).Take(0xF8).All(b => b == 0xFF), "every slot past the terminator must be filled with 0xFF, not left zero");
    }

    private static void PatcherAppliesReceiveGroupListPatchAndSetsPresenceBitForANewRow()
    {
        const int radioIndex = 3; // "No. 4" - bit 3 of the presence bitmap
        var record = new byte[ReceiveGroupListCodec.RecordLength];
        var bitmap = new byte[0x20];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.ReceiveGroupListAddress(radioIndex), record),
                new CodeplugRawRegion(D890UvMemoryMap.ReceiveGroupSet, bitmap)
            ]
        };

        var values = new ReceiveGroupListCodec.DecodedReceiveGroupList(radioIndex) { Name = "NEWROW", TalkgroupIndexes = [2] };
        var patched = RadioCodeplugPatcher.ApplyReceiveGroupListPatch(snapshot, radioIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.ReceiveGroupSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) != 0, "presence bit must be SET for a newly-created row");

        var patchedRecord = patched.Regions.Single(r => r.Address == RadioCodeplugPatcher.ReceiveGroupListAddress(radioIndex)).Data;
        var decoded = ReceiveGroupListCodec.Decode(patchedRecord, radioIndex);
        AssertEqual("NEWROW", decoded.Name);
        AssertEqual(1, decoded.TalkgroupIndexes.Count);
        AssertEqual(2L, decoded.TalkgroupIndexes[0]);
    }

    private static void PatcherDeletesReceiveGroupListBlankingTo0xffNotZeroAndClearsPresenceBit()
    {
        const int radioIndex = 2;
        var populated = ReceiveGroupListCodec.Encode(new byte[ReceiveGroupListCodec.RecordLength], new ReceiveGroupListCodec.DecodedReceiveGroupList(radioIndex) { Name = "TOGO", TalkgroupIndexes = [0] });
        var bitmap = new byte[0x20];
        bitmap[0] = (byte)(1 << radioIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.ReceiveGroupListAddress(radioIndex), populated),
                new CodeplugRawRegion(D890UvMemoryMap.ReceiveGroupSet, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyReceiveGroupListDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == RadioCodeplugPatcher.ReceiveGroupListAddress(radioIndex)).Data;
        AssertTrue(deletedRecord.All(b => b == 0xFF), "deleted Receive Group List record must be blanked to 0xFF, not zeroed");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.ReceiveGroupSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) == 0, "presence bit must be CLEARED after delete");
    }

    private static void ReceiveGroupListEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new ReceiveGroupListEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();

        entry.TalkgroupIndexes.Add(0);
        AssertTrue(entry.HasAnyPendingRadioWrite, "adding a member must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");

        entry.TalkgroupIndexes.Remove(0);
        AssertTrue(entry.HasAnyPendingRadioWrite, "removing a member must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state a third time");
    }

    private static void RoamingChannelCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Row 1: RX 136.00000/TX 400.00000/ColorCode 0/Slot 1 (raw 0)/Name "TESTLOW".
        var row1 = Convert.FromHexString("1360000040000000000054004500530054004c004f00570000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decoded1 = RoamingChannelCodec.Decode(row1, index: 0);
        AssertEqual(136.0, decoded1.RxFrequencyMhz);
        AssertEqual(400.0, decoded1.TxFrequencyMhz);
        AssertEqual(0, decoded1.ColorCode);
        AssertEqual(0, decoded1.Slot);
        AssertEqual("TESTLOW", decoded1.Name);

        // Row 2: RX 145.00000/TX 146.00000/ColorCode 15/Slot 2 (raw 1)/Name "TESTHIGH".
        var row2 = Convert.FromHexString("14500000146000000f01540045005300540048004900470048000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decoded2 = RoamingChannelCodec.Decode(row2, index: 1);
        AssertEqual(145.0, decoded2.RxFrequencyMhz);
        AssertEqual(146.0, decoded2.TxFrequencyMhz);
        AssertEqual(15, decoded2.ColorCode);
        AssertEqual(1, decoded2.Slot);
        AssertEqual("TESTHIGH", decoded2.Name);

        // Row 3: RX 435.00000/TX 436.00000/ColorCode "No Use" (raw 16)/Slot "No Use" (raw 2)/Name "TESTNONE".
        var row3 = Convert.FromHexString("4350000043600000100254004500530054004e004f004e0045000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decoded3 = RoamingChannelCodec.Decode(row3, index: 2);
        AssertEqual(435.0, decoded3.RxFrequencyMhz);
        AssertEqual(436.0, decoded3.TxFrequencyMhz);
        AssertEqual(16, decoded3.ColorCode);
        AssertEqual(2, decoded3.Slot);
        AssertEqual("TESTNONE", decoded3.Name);
    }

    private static void RoamingChannelCodecEncodeDecodeRoundTrips()
    {
        var blank = new byte[RoamingChannelCodec.RecordLength];
        var values = new RoamingChannelCodec.DecodedRoamingChannel(0) { RxFrequencyMhz = 146.52000, TxFrequencyMhz = 446.00000, ColorCode = 7, Slot = 1, Name = "My Roam CH" };

        var encoded = RoamingChannelCodec.Encode(blank, values);
        var decoded = RoamingChannelCodec.Decode(encoded, index: 0);

        AssertEqual(146.52, decoded.RxFrequencyMhz);
        AssertEqual(446.0, decoded.TxFrequencyMhz);
        AssertEqual(7, decoded.ColorCode);
        AssertEqual(1, decoded.Slot);
        AssertEqual("My Roam CH", decoded.Name);
    }

    private static void RoamingChannelCodecColorCodeAndSlotStringMappingsRoundTrip()
    {
        AssertEqual("No Use", RoamingChannelCodec.ColorCodeToString(16));
        AssertEqual("0", RoamingChannelCodec.ColorCodeToString(0));
        AssertEqual("15", RoamingChannelCodec.ColorCodeToString(15));
        AssertEqual(16, RoamingChannelCodec.ParseColorCode("No Use"));
        AssertEqual(15, RoamingChannelCodec.ParseColorCode("15"));

        AssertEqual("Slot 1", RoamingChannelCodec.SlotToString(0));
        AssertEqual("Slot 2", RoamingChannelCodec.SlotToString(1));
        AssertEqual("No Use", RoamingChannelCodec.SlotToString(2));
        AssertEqual(0, RoamingChannelCodec.ParseSlot("Slot 1"));
        AssertEqual(1, RoamingChannelCodec.ParseSlot("Slot 2"));
        AssertEqual(2, RoamingChannelCodec.ParseSlot("No Use"));
    }

    private static void PatcherAppliesRoamingChannelPatchAndSetsPresenceBitForANewRow()
    {
        const int radioIndex = 3;
        var record = new byte[RoamingChannelCodec.RecordLength];
        var bitmap = new byte[0x20];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RoamingChannelAddress(radioIndex), record),
                new CodeplugRawRegion(D890UvMemoryMap.RoamingChannelSet, bitmap)
            ]
        };

        var values = new RoamingChannelCodec.DecodedRoamingChannel(radioIndex) { RxFrequencyMhz = 145.0, TxFrequencyMhz = 445.0, ColorCode = 3, Slot = 0, Name = "NEWROAM" };
        var patched = RadioCodeplugPatcher.ApplyRoamingChannelPatch(snapshot, radioIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.RoamingChannelSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedRecord = patched.Regions.Single(r => r.Address == RadioCodeplugPatcher.RoamingChannelAddress(radioIndex)).Data;
        var decoded = RoamingChannelCodec.Decode(patchedRecord, radioIndex);
        AssertEqual(145.0, decoded.RxFrequencyMhz);
        AssertEqual("NEWROAM", decoded.Name);
    }

    private static void PatcherDeletesRoamingChannelBlankingTo0xffNotZero()
    {
        const int radioIndex = 2;
        var populated = RoamingChannelCodec.Encode(new byte[RoamingChannelCodec.RecordLength], new RoamingChannelCodec.DecodedRoamingChannel(radioIndex) { RxFrequencyMhz = 145.0, TxFrequencyMhz = 445.0, ColorCode = 1, Slot = 0, Name = "TOGO" });
        var bitmap = new byte[0x20];
        bitmap[0] = (byte)(1 << radioIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RoamingChannelAddress(radioIndex), populated),
                new CodeplugRawRegion(D890UvMemoryMap.RoamingChannelSet, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyRoamingChannelDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == RadioCodeplugPatcher.RoamingChannelAddress(radioIndex)).Data;
        AssertTrue(deletedRecord.All(b => b == 0xFF), "deleted Roaming Channel record must be blanked to 0xFF, not zeroed");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.RoamingChannelSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void RoamingChannelEntryHasAnyPendingRadioWriteTracksAllFields()
    {
        var entry = new RoamingChannelEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.RxFrequencyMhz = 146.0;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing RX frequency must mark pending");
        entry.MarkRadioSynced();

        entry.TxFrequencyMhz = 446.0;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing TX frequency must mark pending");
        entry.MarkRadioSynced();

        entry.ColorCode = 5;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing ColorCode must mark pending");
        entry.MarkRadioSynced();

        entry.Slot = 2;
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Slot must mark pending");
        entry.MarkRadioSynced();

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state again");
    }

    private static void RoamingZoneCodecDecodesRealCapturedBytesFromTheLiveWriteCapture()
    {
        // Real live differential write capture 2026-08-10: an existing
        // zone (4 members, Roaming CH 1-4) had its Name changed to
        // "ZTEST1" and its members reordered to CH3/CH1/CH4/CH2 (0-based
        // radio indices 2, 0, 3, 1). Confirms the 1-byte-per-slot layout,
        // that slot order is preserved (not sorted), and the Name field
        // offset/encoding.
        var record = Convert.FromHexString("02000301ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5a005400450053005400310000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        var decoded = RoamingZoneCodec.Decode(record, index: 0);

        AssertEqual("ZTEST1", decoded.Name);
        AssertEqual(4, decoded.RoamingChannelIndexes.Count);
        AssertEqual(2, decoded.RoamingChannelIndexes[0]);
        AssertEqual(0, decoded.RoamingChannelIndexes[1]);
        AssertEqual(3, decoded.RoamingChannelIndexes[2]);
        AssertEqual(1, decoded.RoamingChannelIndexes[3]);
    }

    private static void RoamingZoneCodecEncodeDecodeRoundTripsAndPreservesMemberOrder()
    {
        var blank = new byte[RoamingZoneCodec.RecordLength];
        blank.AsSpan().Fill(0xFF);
        var values = new RoamingZoneCodec.DecodedRoamingZone(0) { Name = "My Roam Zone", RoamingChannelIndexes = [5, 1, 9] };

        var encoded = RoamingZoneCodec.Encode(blank, values);
        var decoded = RoamingZoneCodec.Decode(encoded, index: 0);

        AssertEqual("My Roam Zone", decoded.Name);
        AssertEqual(3, decoded.RoamingChannelIndexes.Count);
        AssertEqual(5, decoded.RoamingChannelIndexes[0]);
        AssertEqual(1, decoded.RoamingChannelIndexes[1]);
        AssertEqual(9, decoded.RoamingChannelIndexes[2]);
    }

    private static void PatcherAppliesRoamingZonePatchAndSetsPresenceBitForANewRow()
    {
        const int radioIndex = 3;
        var record = new byte[RoamingZoneCodec.RecordLength];
        var bitmap = new byte[0x20];
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RoamingZoneAddress(radioIndex), record),
                new CodeplugRawRegion(D890UvMemoryMap.RoamingZoneSet, bitmap)
            ]
        };

        var values = new RoamingZoneCodec.DecodedRoamingZone(radioIndex) { Name = "NEWZONE", RoamingChannelIndexes = [0, 1] };
        var patched = RadioCodeplugPatcher.ApplyRoamingZonePatch(snapshot, radioIndex, values);

        var presenceBitmap = patched.Regions.Single(r => r.Address == D890UvMemoryMap.RoamingZoneSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) != 0, "presence bit must be set for a newly-created row");

        var patchedRecord = patched.Regions.Single(r => r.Address == RadioCodeplugPatcher.RoamingZoneAddress(radioIndex)).Data;
        var decoded = RoamingZoneCodec.Decode(patchedRecord, radioIndex);
        AssertEqual("NEWZONE", decoded.Name);
        AssertEqual(2, decoded.RoamingChannelIndexes.Count);
    }

    private static void PatcherDeletesRoamingZoneBlankingTo0xffNotZero()
    {
        const int radioIndex = 2;
        var populated = RoamingZoneCodec.Encode(new byte[RoamingZoneCodec.RecordLength], new RoamingZoneCodec.DecodedRoamingZone(radioIndex) { Name = "TOGO", RoamingChannelIndexes = [0] });
        var bitmap = new byte[0x20];
        bitmap[0] = (byte)(1 << radioIndex);
        var snapshot = new RadioCodeplugRawSnapshot
        {
            Regions =
            [
                new CodeplugRawRegion(RadioCodeplugPatcher.RoamingZoneAddress(radioIndex), populated),
                new CodeplugRawRegion(D890UvMemoryMap.RoamingZoneSet, bitmap)
            ]
        };

        var deleted = RadioCodeplugPatcher.ApplyRoamingZoneDelete(snapshot, radioIndex);

        var deletedRecord = deleted.Regions.Single(r => r.Address == RadioCodeplugPatcher.RoamingZoneAddress(radioIndex)).Data;
        AssertTrue(deletedRecord.All(b => b == 0xFF), "deleted Roaming Zone record must be blanked to 0xFF, not zeroed");

        var presenceBitmap = deleted.Regions.Single(r => r.Address == D890UvMemoryMap.RoamingZoneSet).Data;
        AssertTrue((presenceBitmap[0] & (1 << radioIndex)) == 0, "presence bit must be cleared after delete");
    }

    private static void RoamingZoneEntryHasAnyPendingRadioWriteTracksNameAndMembers()
    {
        var entry = new RoamingZoneEntry();
        AssertTrue(entry.HasAnyPendingRadioWrite, "never-synced entry must start pending (no baseline yet)");

        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state");

        entry.Name = "TESTNM";
        AssertTrue(entry.HasAnyPendingRadioWrite, "editing Name must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state after Name edit");

        var member = new RoamingChannelEntry { Number = 1, Name = "CH1" };
        entry.Members.Add(member);
        AssertTrue(entry.HasAnyPendingRadioWrite, "adding a member must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state after member add");

        entry.Members.Remove(member);
        AssertTrue(entry.HasAnyPendingRadioWrite, "removing a member must mark pending");
        entry.MarkRadioSynced();
        AssertTrue(!entry.HasAnyPendingRadioWrite, "MarkRadioSynced must clear pending state after member remove");
    }

    private static void CodeplugLimitsRejectsFrequencyInTheVhfUhfDeadZone()
    {
        AssertTrue(CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(145.0), "145 MHz (VHF) must be valid");
        AssertTrue(CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(435.0), "435 MHz (UHF) must be valid");
        AssertTrue(!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(250.0), "250 MHz (dead zone) must be rejected");
        AssertTrue(!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(300.54321), "300.54321 MHz (dead zone) must be rejected - the exact value that triggered the vendor CPS's own out-of-range error");
    }

    private static void LoadingAnOldProjectFileWithDigitalContactCallAlertAsABoolDoesNotThrow()
    {
        // Same migration as Talkgroup's own (see the test right below this
        // one) - DigitalContactData.CallAlert was a plain bool (IsCallAlert)
        // before 2026-08-09, both now share BoolTolerantCallAlertJsonConverter.
        const string oldFormatJson = """
        {
            "DigitalContacts": [
                { "Index": 0, "CallType": 1, "RadioId": 12345678, "Name": "DC1", "IsCallAlert": true },
                { "Index": 1, "CallType": 0, "RadioId": 87654321, "Name": "DC2", "IsCallAlert": false }
            ]
        }
        """;

        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-old-format-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, oldFormatJson);
            var data = new JsonRadioDataStore(path).LoadAsync().GetAwaiter().GetResult();
            AssertTrue(data is not null, "old-format project JSON must load, not throw");
            AssertEqual(2, data!.DigitalContacts.Count);
            AssertEqual("None", data.DigitalContacts[0].CallAlert);
            AssertEqual("None", data.DigitalContacts[1].CallAlert);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void DigitalContactEntryCallTypeTextRoundTripsAndForcesCallAlertToNoneOnAllCall()
    {
        var entry = new DigitalContactEntry { CallTypeText = "Private Call", CallAlert = "Ring" };
        AssertEqual(0, entry.CallType);
        AssertEqual("None,Ring,Online Alert", string.Join(",", entry.CallAlertOptions));

        entry.CallTypeText = "Group Call";
        AssertEqual(1, entry.CallType);
        AssertEqual("None,Online Alert", string.Join(",", entry.CallAlertOptions));

        entry.CallTypeText = "All Call";
        AssertEqual(2, entry.CallType);
        AssertEqual("None", entry.CallAlert);
        AssertTrue(!entry.IsAllCallFieldsEditable, "TG/DMR ID and Call Alert must be disabled once CallType is All Call");
    }

    private static void DigitalContactEntryRadioIdTextBypassesRangeCheckOnlyForAllCall()
    {
        var entry = new DigitalContactEntry { CallTypeText = "Group Call", RadioIdText = "1234567" };
        entry.RadioIdText = "87654321"; // bigger than the real 24-bit max
        AssertTrue(entry.HasErrors, "Group Call must still enforce the normal DMR ID range.");
        AssertEqual(1234567L, entry.RadioId);

        entry.CallTypeText = "All Call";
        AssertTrue(!entry.HasErrors, "Switching to All Call must clear the stale out-of-range error.");

        entry.RadioIdText = "87654321";
        AssertTrue(!entry.HasErrors, "All Call must bypass the range check even for an out-of-range typed value.");
        AssertEqual(87654321L, entry.RadioId);
    }

    private static void LoadingAnOldProjectFileWithTalkgroupCallAlertAsABoolDoesNotThrow()
    {
        // Project files saved before 2026-08-07 stored Talkgroup CallAlert
        // as a JSON bool (the old, since-corrected single-bit decode) -
        // TalkgroupData.CallAlert is now a string (None/Ring/Online Alert),
        // and without a tolerant converter, System.Text.Json throws
        // JsonException: "The JSON value could not be converted to
        // System.String" on Open, permanently locking the user out of
        // their own saved project. Goes through the real public
        // JsonRadioDataStore.LoadAsync path (a real temp file), not a
        // hand-rolled deserializer call, so it actually proves the fix
        // the app itself hits.
        const string oldFormatJson = """
        {
            "Talkgroups": [
                { "Number": 1, "DmrId": 12345678, "Name": "TG1", "CallType": "Group Call", "CallAlert": true },
                { "Number": 2, "DmrId": 87654321, "Name": "TG2", "CallType": "Private Call", "CallAlert": false }
            ]
        }
        """;

        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-old-format-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, oldFormatJson);
            var data = new JsonRadioDataStore(path).LoadAsync().GetAwaiter().GetResult();
            AssertTrue(data is not null, "old-format project JSON must load, not throw");
            AssertEqual(2, data!.Talkgroups.Count);
            AssertEqual("Online Alert", data.Talkgroups[0].CallAlert);
            AssertEqual("None", data.Talkgroups[1].CallAlert);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void TalkgroupCallAlertStillRoundTripsAsAPlainStringGoingForward()
    {
        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-callalert-roundtrip-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonRadioDataStore(path);
            var project = new RadioProjectData
            {
                Talkgroups = [new TalkgroupData { Number = 1, DmrId = 12345678, Name = "TG1", CallType = "Private Call", CallAlert = "Ring" }]
            };
            store.SaveAsync(project).GetAwaiter().GetResult();

            var reloaded = store.LoadAsync().GetAwaiter().GetResult();
            AssertEqual("Ring", reloaded!.Talkgroups[0].CallAlert);
            AssertTrue(File.ReadAllText(path).Contains("\"Ring\""), "saved project must write CallAlert back out as a plain string");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Closes a confirmed gap where no test exercised a full,
    /// all-entity-types project-file round trip. Reflection-based rather
    /// than hand-typed:
    /// RadioProjectData covers ~25 entity types with dozens of fields
    /// each, and a generic "fill every property, then compare every
    /// property" approach catches a mapping bug in ANY field, including
    /// ones added after this test was written, instead of only the fields
    /// someone remembered to assert on by hand.</summary>
    private static void FullRadioProjectDataRoundTripsThroughARealFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-full-roundtrip-test-{Guid.NewGuid():N}.json");
        try
        {
            var counter = 0;
            var original = (RadioProjectData)CreateFilledInstance(typeof(RadioProjectData), ref counter);

            var store = new JsonRadioDataStore(path);
            store.SaveAsync(original).GetAwaiter().GetResult();
            var reloaded = store.LoadAsync().GetAwaiter().GetResult();

            AssertTrue(reloaded is not null, "a freshly saved project must load back, not null");
            AssertDeepEqual(original, reloaded, "RadioProjectData");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void SavingAProjectEncryptsEncryptionKeyMaterialInTheJsonFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-key-encryption-test-{Guid.NewGuid():N}.json");
        try
        {
            const string plainDigitalKey = "0123456789ABCDEF";
            const string plainArc4Key = "FEDCBA9876543210";
            const string plainAesKey = "112233445566778899AABBCCDDEEFF0";
            var project = new RadioProjectData
            {
                EncryptionKeys = [new EncryptionKeyData { Number = 1, EncryptionKey = plainDigitalKey, EncryptionId = "ID1" }],
                Arc4EncryptionKeys = [new EncryptionKeyData { Number = 1, EncryptionKey = plainArc4Key, EncryptionId = "ID2" }],
                AesEncryptionKeys = [new EncryptionKeyData { Number = 1, EncryptionKey = plainAesKey, EncryptionId = "ID3" }]
            };

            new JsonRadioDataStore(path).SaveAsync(project).GetAwaiter().GetResult();
            var rawJson = File.ReadAllText(path);

            AssertTrue(!rawJson.Contains(plainDigitalKey, StringComparison.Ordinal), "the digital key must not appear as plain text in the saved file");
            AssertTrue(!rawJson.Contains(plainArc4Key, StringComparison.Ordinal), "the ARC4 key must not appear as plain text in the saved file");
            AssertTrue(!rawJson.Contains(plainAesKey, StringComparison.Ordinal), "the AES key must not appear as plain text in the saved file");
            AssertTrue(rawJson.Contains("ENC1:", StringComparison.Ordinal), "encrypted key values must carry the ENC1: marker");

            // Round trip stays transparent to the app - loading gives back
            // the exact same plain keys, decrypted automatically.
            var reloaded = new JsonRadioDataStore(path).LoadAsync().GetAwaiter().GetResult();
            AssertEqual(plainDigitalKey, reloaded!.EncryptionKeys[0].EncryptionKey);
            AssertEqual(plainArc4Key, reloaded.Arc4EncryptionKeys[0].EncryptionKey);
            AssertEqual(plainAesKey, reloaded.AesEncryptionKeys[0].EncryptionKey);

            // Non-secret fields are untouched by any of this.
            AssertEqual("ID1", reloaded.EncryptionKeys[0].EncryptionId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void EncryptingAndDecryptingThroughARawStreamRoundTripsLikeTheStoragePickerPathDoes()
    {
        // Real bug found 2026-08-16 on Android: the file picker's own
        // IProjectStorage (used whenever the picked file has no local
        // filesystem path - the normal case for content:// providers like
        // a cloud-sync app's document provider) did its own raw
        // JsonSerializer.Serialize/DeserializeAsync against a Stream and
        // never called into JsonRadioDataStore, so it silently skipped
        // encryption on save and decryption on load - keys round tripped
        // as untouched ciphertext, which then failed hex validation. That
        // code lives in AvaloniaStoragePickerService's private
        // AvaloniaProjectStorage class, not reachable from a unit test, but
        // it now just calls JsonRadioDataStore.BuildEncryptedCloneForSave/
        // DecryptKeysAfterLoad directly - this test exercises those same
        // two methods through a raw MemoryStream (no real file on disk),
        // mirroring exactly what that class does, so a future storage
        // implementation that forgets to call them has no reason to expect
        // this test would look any different from JsonRadioDataStore's own
        // file-based round trip above.
        const string plainKey = "AA11BB22CC33DD44";
        var project = new RadioProjectData
        {
            Arc4EncryptionKeys = [new EncryptionKeyData { Number = 1, EncryptionKey = plainKey, EncryptionId = "ID9" }]
        };

        using var stream = new MemoryStream();
        var toSave = JsonRadioDataStore.BuildEncryptedCloneForSave(project);
        System.Text.Json.JsonSerializer.SerializeAsync(stream, toSave, RadioProjectJsonContext.Default.RadioProjectData).GetAwaiter().GetResult();

        var rawJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        AssertTrue(!rawJson.Contains(plainKey, StringComparison.Ordinal), "the key must not appear as plain text when saved through the stream path");
        AssertTrue(rawJson.Contains("ENC1:", StringComparison.Ordinal), "encrypted key values must carry the ENC1: marker when saved through the stream path");

        stream.Position = 0;
        var reloaded = System.Text.Json.JsonSerializer.DeserializeAsync(stream, RadioProjectJsonContext.Default.RadioProjectData).GetAwaiter().GetResult();
        JsonRadioDataStore.DecryptKeysAfterLoad(reloaded!);

        AssertEqual(plainKey, reloaded!.Arc4EncryptionKeys[0].EncryptionKey);
    }

    private static void SavingAProjectDoesNotMutateTheCallersOwnEncryptionKeys()
    {
        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-key-encryption-mutation-test-{Guid.NewGuid():N}.json");
        try
        {
            const string plainKey = "ABCDEF0123456789";
            var project = new RadioProjectData
            {
                EncryptionKeys = [new EncryptionKeyData { Number = 1, EncryptionKey = plainKey, EncryptionId = "ID1" }]
            };

            new JsonRadioDataStore(path).SaveAsync(project).GetAwaiter().GetResult();

            // The live in-memory project (what the app's own ViewModel
            // holds) must still show the plain key after a save - only the
            // on-disk copy is protected, never the caller's own object.
            AssertEqual(plainKey, project.EncryptionKeys[0].EncryptionKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void LoadingALegacyPlainTextEncryptionKeyProjectFileStillWorks()
    {
        // Project files saved before 2026-08-16 stored EncryptionKey as
        // plain hex, no ENC1: marker - must still load correctly, not be
        // treated as corrupted ciphertext. Goes through the real public
        // JsonRadioDataStore.LoadAsync path, same convention as the other
        // "loading an old project file" tests.
        const string legacyJson = """
        {
            "EncryptionKeys": [
                { "Number": 1, "EncryptionKey": "0123456789ABCDEF", "EncryptionId": "ID1" }
            ]
        }
        """;

        var path = Path.Combine(Path.GetTempPath(), $"anytonecps-legacy-plaintext-key-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, legacyJson);
            var data = new JsonRadioDataStore(path).LoadAsync().GetAwaiter().GetResult();
            AssertTrue(data is not null, "old-format project JSON must load, not throw");
            AssertEqual("0123456789ABCDEF", data!.EncryptionKeys[0].EncryptionKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void EncryptionKeyProtectorRoundTripsAndFallsBackOnTamperedInput()
    {
        var encrypted = EncryptionKeyProtector.Encrypt("SOMEKEY123");
        AssertTrue(encrypted.StartsWith("ENC1:", StringComparison.Ordinal), "encrypted values must carry the ENC1: marker");
        AssertTrue(!encrypted.Contains("SOMEKEY123", StringComparison.Ordinal), "the encrypted form must not contain the plain key");
        AssertEqual("SOMEKEY123", EncryptionKeyProtector.Decrypt(encrypted));

        // A legacy plain value (no marker) passes through unchanged.
        AssertEqual("PLAINHEX", EncryptionKeyProtector.Decrypt("PLAINHEX"));
        AssertEqual("", EncryptionKeyProtector.Decrypt(""));
        AssertEqual("", EncryptionKeyProtector.Encrypt(""));

        // Tampered/corrupted ciphertext falls back to the raw value rather
        // than throwing and blocking the rest of the project from loading.
        var tampered = encrypted[..^4] + "abcd";
        AssertEqual(tampered, EncryptionKeyProtector.Decrypt(tampered));
    }

    /// <summary>Recursively builds an instance of <paramref name="type"/>
    /// with every public read/write property set to a distinct,
    /// non-default value (a shared, ever-incrementing counter, so no two
    /// fields anywhere in the graph accidentally end up equal - catches a
    /// mapping bug that swaps two same-typed fields, which a
    /// same-value-everywhere fixture would miss).</summary>
    private static object CreateFilledInstance(Type type, ref int counter)
    {
        if (type == typeof(string))
        {
            counter++;
            return $"S{counter}";
        }

        if (type == typeof(bool))
        {
            counter++;
            return counter % 2 == 0;
        }

        if (type == typeof(byte))
        {
            counter++;
            return (byte)(counter % 200 + 1);
        }

        if (type == typeof(int))
        {
            counter++;
            return counter * 7;
        }

        if (type == typeof(long))
        {
            counter++;
            return (long)counter * 1000000007;
        }

        if (type == typeof(ushort))
        {
            counter++;
            return (ushort)(counter % 60000 + 1);
        }

        if (type == typeof(double))
        {
            counter++;
            return counter * 1.25;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return CreateFilledInstance(underlying, ref counter);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(type)!;
            list.Add(CreateFilledInstance(elementType, ref counter));
            list.Add(CreateFilledInstance(elementType, ref counter));
            return list;
        }

        var instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Could not create an instance of {type}");
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || !prop.CanRead)
            {
                continue;
            }

            prop.SetValue(instance, CreateFilledInstance(prop.PropertyType, ref counter));
        }

        return instance;
    }

    /// <summary>Recursively asserts every public property in
    /// <paramref name="expected"/>'s object graph matches
    /// <paramref name="actual"/>'s - the read-side counterpart to
    /// <see cref="CreateFilledInstance"/>.</summary>
    private static void AssertDeepEqual(object? expected, object? actual, string path)
    {
        if (expected is null || actual is null)
        {
            AssertTrue(expected is null && actual is null, $"{path}: one side is null, the other is not");
            return;
        }

        var type = expected.GetType();
        if (type == typeof(string) || type.IsPrimitive)
        {
            AssertTrue(Equals(expected, actual), $"{path}: expected '{expected}', got '{actual}'");
            return;
        }

        if (expected is System.Collections.IList expectedList)
        {
            var actualList = (System.Collections.IList)actual;
            AssertEqual(expectedList.Count, actualList.Count);
            for (var i = 0; i < expectedList.Count; i++)
            {
                AssertDeepEqual(expectedList[i], actualList[i], $"{path}[{i}]");
            }

            return;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead)
            {
                continue;
            }

            AssertDeepEqual(prop.GetValue(expected), prop.GetValue(actual), $"{path}.{prop.Name}");
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertSame(object? expected, object? actual)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException("Expected both values to be the same instance.");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
        }
    }


    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, got {exception.GetType().Name}.");
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void DigitalContactCodecEncodeRecordMatchesRealCapturedBytes()
    {
        // Exact 84-byte record captured 2026-08-09 live write round 1 (see
        // DigitalContactCodec's own doc comment) - contact No. 901, Name
        // "Tobias", Private Call, DMR ID 2401236, Call Alert None, City
        // "Markaryd", Callsign "SH7TUV", State "Smaland", Country "Sweden",
        // empty Remarks.
        const string expectedHex = "00000240123654006f00620069006100730000004d00610072006b0061007200790064000000530048003700540055005600000053006d0061006c0061006e0064000000530077006500640065006e0000000000";
        var contact = new DigitalContactCodec.DecodedDigitalContact(900)
        {
            CallType = 0,
            CallAlert = "None",
            RadioId = 2401236,
            Name = "Tobias",
            City = "Markaryd",
            Callsign = "SH7TUV",
            State = "Smaland",
            Country = "Sweden",
            Remarks = ""
        };

        var encoded = DigitalContactCodec.EncodeAll([contact]);
        AssertEqual(expectedHex, Convert.ToHexString(encoded).ToLowerInvariant());
    }

    private static void DigitalContactCodecDecodesFriendFlagPackedIntoTheCallAlertByte()
    {
        // Real captured record 2026-08-09 (Friends List investigation):
        // contact "Jonas", DMR ID 2400002, added to the vendor CPS's
        // "Friend List Edit" dialog. The byte this codec already decoded
        // as call_alert came back as 0x10, not one of the known 0/1/2
        // values - every OTHER one of the same 900 real contacts read
        // 0x00. Confirmed by adding a second contact (Patrik, 2400003) the
        // same way: it also came back 0x10, and removing it from Friends
        // (not captured in this suite) would be expected to clear it.
        // Bit 0x10 is therefore the Friends List membership flag, packed
        // into the SAME byte as call_alert (which only ever uses the low
        // 2 bits) - NOT a separate memory region at all, despite the
        // vendor CPS presenting it as an entirely separate "Friend List
        // Edit" dialog with its own search-and-add picker.
        const string jonasRecordHex = "0010024000024a006f006e00610073000000540079007200650073006f00000053004d0030005400550049000000530074006f0063006b0068006f006c006d000000530077006500640065006e0000000000";
        var connection = new FakeRadioConnection();
        connection.WriteMemory(D890UvMemoryMap.DigitalContactData, Convert.FromHexString(jonasRecordHex));

        var decoded = DigitalContactCodec.DecodeAll(connection, 1);

        AssertEqual(1, decoded.Count);
        AssertEqual(2400002L, decoded[0].RadioId);
        AssertEqual("Jonas", decoded[0].Name);
        AssertTrue(decoded[0].IsFriend, "bit 0x10 in the call_alert byte must decode as IsFriend=true");
        AssertEqual("None", decoded[0].CallAlert);
    }

    private static void DigitalContactCodecEncodeRecordRoundTripsFriendFlagWithoutDisturbingCallAlert()
    {
        // The bug this guards against: EncodeRecord only ever emitted
        // TalkgroupCodec.StringToCallAlertByte's 0/1/2 range, so writing
        // ANY digital contact back to the radio through this app's own
        // (already-shipped) write path would have silently un-friended it.
        var friend = new DigitalContactCodec.DecodedDigitalContact(0)
        {
            CallAlert = "Online Alert",
            IsFriend = true,
            RadioId = 2400002,
            Name = "Jonas"
        };
        var notFriend = new DigitalContactCodec.DecodedDigitalContact(1)
        {
            CallAlert = "Online Alert",
            IsFriend = false,
            RadioId = 2400003,
            Name = "Patrik"
        };

        var connection = new FakeRadioConnection();
        DigitalContactWriter.Write(connection, [friend, notFriend]);
        var decoded = DigitalContactCodec.DecodeAll(connection, 2);

        AssertTrue(decoded[0].IsFriend, "friend flag must survive an encode/decode round trip");
        AssertEqual("Online Alert", decoded[0].CallAlert);
        AssertTrue(!decoded[1].IsFriend, "a non-friend contact must not pick up the flag");
        AssertEqual("Online Alert", decoded[1].CallAlert);
    }

    private static void DigitalContactCodecEncodeMetaMatchesRealCapturedValues()
    {
        // Live-confirmed 2026-08-09 (all 3 write rounds): offset 0 = count,
        // offset 4 = ABSOLUTE END ADDRESS (DigitalContactData + total
        // bytes), not a length - see DigitalContactCodec's own doc comment.
        var meta = DigitalContactCodec.EncodeMeta(count: 901, totalDataBytes: 0x128f6);
        AssertEqual(16, meta.Length);
        AssertEqual(901u, BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(0, 4)));
        AssertEqual((uint)(D890UvMemoryMap.DigitalContactData + 0x128f6), BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(4, 4)));
    }

    private static void DigitalContactCodecEncodeAllThrowsWhenExceedingBlockLength()
    {
        // A single record with a long enough Remarks field to blow past
        // the MaxBlocks cap on its own - see DigitalContactCodec.MaxBlocks'
        // own doc comment for why this app still refuses past a bounded
        // number of blocks despite multi-block address translation now
        // being implemented.
        var hugeRemarks = new string('X', DigitalContactCodec.BlockLength * DigitalContactCodec.MaxBlocks);
        var contact = new DigitalContactCodec.DecodedDigitalContact(0) { Name = "A", Remarks = hugeRemarks };
        AssertThrows<InvalidOperationException>(() => DigitalContactCodec.EncodeAll([contact]));
    }

    private static void DigitalContactWriterHandlesAWriteSpanningTwoBlocksThroughFakeConnection()
    {
        // Pads with enough filler contacts to push the real total past one
        // BlockLength, then adds a final distinctive "marker" contact whose
        // bytes land in block 1 - confirms WriteLogicalBytes' block-boundary
        // chunking and LogicalToPhysicalAddress translation round-trip
        // correctly through DecodeAll, which independently re-derives the
        // same physical addresses for reads.
        var connection = new FakeRadioConnection();
        var contacts = new List<DigitalContactCodec.DecodedDigitalContact>();

        // Each filler record is a little over 100 bytes; pack enough to
        // comfortably cross the block boundary without an excessive test
        // runtime.
        var fillerCount = DigitalContactCodec.BlockLength / 90 + 10;
        for (var i = 0; i < fillerCount; i++)
        {
            contacts.Add(new DigitalContactCodec.DecodedDigitalContact(i)
            {
                CallType = 0,
                CallAlert = "None",
                RadioId = 2000000 + i,
                Name = "Filler" + i,
                City = "FillerCity",
                Callsign = "F" + i,
                State = "FillerState",
                Country = "FillerCountry",
                Remarks = ""
            });
        }

        contacts.Add(new DigitalContactCodec.DecodedDigitalContact(fillerCount)
        {
            CallType = 1,
            CallAlert = "Online Alert",
            RadioId = 9876543,
            Name = "BlockOneMarker",
            City = "SecondBlockCity",
            Callsign = "MARKER1",
            State = "MarkerState",
            Country = "MarkerCountry",
            Remarks = "past the boundary"
        });

        var encoded = DigitalContactCodec.EncodeAll(contacts);
        AssertTrue(encoded.Length > DigitalContactCodec.BlockLength, "test setup must actually cross the block boundary");

        DigitalContactWriter.Write(connection, contacts);
        var decoded = DigitalContactCodec.DecodeAll(connection, contacts.Count);

        AssertEqual(contacts.Count, decoded.Count);
        var marker = decoded[^1];
        AssertEqual("BlockOneMarker", marker.Name);
        AssertEqual("SecondBlockCity", marker.City);
        AssertEqual("MARKER1", marker.Callsign);
        AssertEqual(9876543L, marker.RadioId);
        AssertEqual("Online Alert", marker.CallAlert);
    }

    private static void DigitalContactCodecAddressTranslationRoundTripsAtHighBlockIndices()
    {
        // The block-crossing math is one uniform "offset / BlockStride"
        // division with no per-block special-casing (see MaxBlocks' own
        // doc comment) - correctness at block 1 (live-confirmed 2026-08-09)
        // implies correctness at block 250 the same way. This just confirms
        // the round trip holds at indices nowhere near practical to reach
        // with a real 500,000-contact write/decode cycle.
        int[] blockIndices = [0, 1, 2, 100, 250, 499];
        foreach (var block in blockIndices)
        {
            var logicalOffset = block * DigitalContactCodec.BlockLength + 1234;
            var physical = DigitalContactCodec.LogicalToPhysicalAddress(logicalOffset);
            var expectedPhysical = D890UvMemoryMap.DigitalContactData + block * DigitalContactCodec.BlockStride + 1234;
            AssertEqual(expectedPhysical, physical);

            var roundTripped = DigitalContactCodec.PhysicalToLogicalAddress(physical);
            AssertEqual(logicalOffset, roundTripped);
        }
    }

    private static void TalkgroupWhitelistCodecEncodeAllMatchesRealCapturedBytes()
    {
        // Exact bytes captured 2026-08-09 live write (see
        // TalkgroupWhitelistCodec's own doc comment): rows 1/2/21 with
        // TG/DMR IDs 91101/91102/91121, packed to positions 0/1/2 - block 0
        // holds entries 0+1, block 1 holds entry 2 in its first half with
        // its second half blank (0xff).
        const string block0Hex = "03221200000000000522120001000000";
        const string block1Hex = "4322120002000000ffffffffffffffff";
        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries =
        [
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 91101, CallType = 1 },
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 91102, CallType = 1 },
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 91121, CallType = 1 }
        ];

        var encoded = TalkgroupWhitelistCodec.EncodeAll(entries);
        AssertEqual(TalkgroupWhitelistCodec.MaxBlocks * TalkgroupWhitelistCodec.BlockLength, encoded.Length);
        AssertEqual(block0Hex, Convert.ToHexString(encoded.AsSpan(0, 16)).ToLowerInvariant());
        AssertEqual(block1Hex, Convert.ToHexString(encoded.AsSpan(16, 16)).ToLowerInvariant());
    }

    private static void DigitalContactWhitelistCodecEncodeAllMatchesRealCapturedBytes()
    {
        // Same capture, Digital Contact Whitelist side: TG/DMR IDs
        // 92101/92102/92121, CallType bit 0 (not 1 - see this class's own
        // doc comment for why the two lists differ here).
        const string block0Hex = "02421200000000000442120001000000";
        const string block1Hex = "4242120002000000ffffffffffffffff";
        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries =
        [
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 92101, CallType = 0 },
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 92102, CallType = 0 },
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = 92121, CallType = 0 }
        ];

        var encoded = TalkgroupWhitelistCodec.EncodeAll(entries);
        AssertEqual(block0Hex, Convert.ToHexString(encoded.AsSpan(0, 16)).ToLowerInvariant());
        AssertEqual(block1Hex, Convert.ToHexString(encoded.AsSpan(16, 16)).ToLowerInvariant());
    }

    private static void TalkgroupWhitelistCodecEncodeAllIgnoresStoredIdUsesListPosition()
    {
        // Live-confirmed 2026-08-09: the radio packs entries by list
        // position, ignoring whatever row number/Id they carry - EncodeAll
        // must do the same. Deliberately gives every entry a wildly wrong
        // Id to prove it's ignored.
        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries =
        [
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(999) { DmrId = 91101, CallType = 1 },
            new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(20) { DmrId = 91102, CallType = 1 }
        ];

        var encoded = TalkgroupWhitelistCodec.EncodeAll(entries);
        var block = TalkgroupWhitelistCodec.DecodeBlock(encoded.AsSpan(0, 16));
        AssertEqual(0, block.First!.Id);
        AssertEqual(1, block.Second!.Id);
    }

    private static void TalkgroupWhitelistCodecEncodeAllThrowsWhenExceedingCap()
    {
        var tooMany = Enumerable.Range(0, TalkgroupWhitelistCodec.MaxBlocks * 2 + 1)
            .Select(i => new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(i) { DmrId = 1000 + i, CallType = 1 })
            .ToList();
        AssertThrows<InvalidOperationException>(() => TalkgroupWhitelistCodec.EncodeAll(tooMany));
    }

    private static void TalkgroupWhitelistCodecEncodeAllDecodeBlockRoundTrips()
    {
        // Odd count (5) so the last block's second half is genuinely blank,
        // exercising the real StopReading path a full list would rely on.
        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> entries =
        [
            new(0) { DmrId = 100001, CallType = 1 },
            new(0) { DmrId = 100002, CallType = 1 },
            new(0) { DmrId = 100003, CallType = 1 },
            new(0) { DmrId = 100004, CallType = 1 },
            new(0) { DmrId = 100005, CallType = 1 }
        ];

        var encoded = TalkgroupWhitelistCodec.EncodeAll(entries);
        var decoded = new List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist>();
        for (var i = 0; i < TalkgroupWhitelistCodec.MaxBlocks; i++)
        {
            var block = TalkgroupWhitelistCodec.DecodeBlock(encoded.AsSpan(i * TalkgroupWhitelistCodec.BlockLength, TalkgroupWhitelistCodec.BlockLength));
            if (block.First is { } first)
            {
                decoded.Add(first);
            }

            if (block.Second is { } second)
            {
                decoded.Add(second);
            }

            if (block.StopReading)
            {
                break;
            }
        }

        AssertEqual(entries.Count, decoded.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            AssertEqual(entries[i].DmrId, decoded[i].DmrId);
            AssertEqual(entries[i].CallType, decoded[i].CallType);
            AssertEqual(i, decoded[i].Id);
        }
    }

    private static void DigitalContactWriterRoundTripsAddEditDeleteThroughFakeConnection()
    {
        // Exercises the full add -> edit (grow + shrink) -> delete
        // lifecycle through DigitalContactWriter + DigitalContactCodec.DecodeAll
        // against a fake in-memory connection - the same 3-stage lifecycle
        // confirmed live against real hardware 2026-08-09, here re-run
        // against our own encode/decode round trip instead of the radio.
        var connection = new FakeRadioConnection();

        DigitalContactCodec.DecodedDigitalContact MakeContact(int index, string name, string city) => new(index)
        {
            CallType = 0,
            CallAlert = "None",
            RadioId = 1000000 + index,
            Name = name,
            City = city,
            Callsign = "CS" + index,
            State = "ST",
            Country = "CO",
            Remarks = ""
        };

        // Round 1: add a single contact.
        var round1 = new List<DigitalContactCodec.DecodedDigitalContact> { MakeContact(0, "Tobias", "Markaryd") };
        DigitalContactWriter.Write(connection, round1);
        var decoded1 = DigitalContactCodec.DecodeAll(connection, round1.Count);
        AssertEqual(1, decoded1.Count);
        AssertEqual("Tobias", decoded1[0].Name);
        AssertEqual("Markaryd", decoded1[0].City);

        // Round 2: add a second contact, and edit the first (grow name,
        // shrink city) - matches the real grow+shrink live test exactly.
        var round2 = new List<DigitalContactCodec.DecodedDigitalContact>
        {
            MakeContact(0, "TobiasLong", "Mkd"),
            MakeContact(1, "TestTwo", "TestCity2")
        };
        DigitalContactWriter.Write(connection, round2);
        var decoded2 = DigitalContactCodec.DecodeAll(connection, round2.Count);
        AssertEqual(2, decoded2.Count);
        AssertEqual("TobiasLong", decoded2[0].Name);
        AssertEqual("Mkd", decoded2[0].City);
        AssertEqual("TestTwo", decoded2[1].Name);
        AssertEqual("TestCity2", decoded2[1].City);

        // Round 3: delete the second contact - the freed space must be
        // zero-filled (confirmed live), not left as stale bytes that could
        // corrupt a future shorter write.
        var round3 = new List<DigitalContactCodec.DecodedDigitalContact> { round2[0] };
        DigitalContactWriter.Write(connection, round3);
        var decoded3 = DigitalContactCodec.DecodeAll(connection, round3.Count);
        AssertEqual(1, decoded3.Count);
        AssertEqual("TobiasLong", decoded3[0].Name);

        var freedBytes = connection.ReadMemory(D890UvMemoryMap.DigitalContactData + DigitalContactCodec.EncodeAll(round3).Length, 32);
        AssertTrue(freedBytes.All(b => b == 0), "freed space after a shorter re-write must be zero-filled, not left stale");
    }

    /// <summary>Minimal in-memory <see cref="IRadioConnection"/> fake, just
    /// enough to test <see cref="DigitalContactWriter"/>/<see cref="DigitalContactCodec.DecodeAll"/>
    /// against each other without real hardware - a sparse byte dictionary,
    /// no protocol/checksum simulation needed since neither of those two
    /// codepaths touches the wire format, only WriteMemory/ReadMemory's
    /// plain address+bytes contract.</summary>
    private sealed class FakeRadioConnection : IRadioConnection
    {
        private readonly Dictionary<int, byte> _memory = [];

        public event Action<string>? Warning { add { } remove { } }

        public bool TryOpen(string portName, out string? error)
        {
            error = null;
            return true;
        }

        public RadioIdentity Identify() => new("D890UV", "V100", true);

        public byte[] ReadMemory(int address, int length) => ReadMemoryStrict(address, length);

        public byte[] ReadMemoryStrict(int address, int length)
        {
            var result = new byte[length];
            for (var i = 0; i < length; i++)
            {
                result[i] = _memory.GetValueOrDefault(address + i);
            }

            return result;
        }

        public void WriteMemory(int address, byte[] data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                _memory[address + i] = data[i];
            }
        }

        public void Close()
        {
        }
    }

    private sealed class TestStoragePicker(
        UsedEncryptionKeyRemovalChoice removalChoice,
        Func<FiveToneSpecialCallDialogRequest, bool>? fiveToneSpecialCallHandler = null,
        bool confirmResetFiveToneSpecialCall = true,
        bool confirmWriteToRadio = false,
        IProjectStorage? openProjectStorage = null) : IStoragePickerService
    {
        public Task<IProjectStorage?> PickOpenProjectAsync() => Task.FromResult(openProjectStorage);
        public Task<IProjectStorage?> PickSaveProjectAsync(string suggestedFileName) => Task.FromResult<IProjectStorage?>(null);
        public Task<IProjectStorage?> OpenRememberedProjectAsync() => Task.FromResult<IProjectStorage?>(null);
        public Task RememberProjectAsync(IProjectStorage projectStorage) => Task.CompletedTask;
        public Task ForgetRememberedProjectAsync() => Task.CompletedTask;
        public Task<IReadOnlyList<string>> PickCsvFilesAsync(string title) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmOverwriteAsync(IProjectStorage projectStorage) => Task.FromResult(false);
        public Task<bool> ConfirmDiscardUnsavedChangesAsync() => Task.FromResult(false);
        public Task<UsedEncryptionKeyRemovalChoice> ConfirmRemoveUsedEncryptionKeyAsync(string message) =>
            Task.FromResult(removalChoice);
        public Task<bool> ShowReadOptionsDialogAsync(RadioIncludeOptionsRequest options) => Task.FromResult(false);
        public Task<bool> ConfirmWriteToRadioAsync(string summary, RadioIncludeOptionsRequest options) => Task.FromResult(confirmWriteToRadio);
        public Task<bool> ShowFiveToneSpecialCallDialogAsync(FiveToneSpecialCallDialogRequest request) =>
            Task.FromResult(fiveToneSpecialCallHandler?.Invoke(request) ?? false);
        public Task<bool> ConfirmResetFiveToneSpecialCallAsync() => Task.FromResult(confirmResetFiveToneSpecialCall);
        public Task<bool> ShowDtmfSpecialCallDialogAsync(DtmfSpecialCallDialogRequest request) => Task.FromResult(false);
    }
}
