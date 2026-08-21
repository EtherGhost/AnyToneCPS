using System.Collections.Generic;
using System.Linq;

namespace AnyToneCPS.Services;

public static class AnyToneChannelTemplate
{
    public static readonly string[] Headers =
    [
        "No.",
        "Channel Name",
        "Receive Frequency",
        "Transmit Frequency",
        "Channel Type",
        "Transmit Power",
        "Band Width",
        "CTCSS/DCS Decode",
        "CTCSS/DCS Encode",
        "Contact/Talk Group",
        "Contact/Talk Group Call Type",
        "Contact/Talk Group TG/DMR ID",
        "Radio ID",
        "Busy Lock/TX Permit",
        "Squelch Mode",
        "Optional Signal",
        "DTMF ID",
        "2Tone ID",
        "5Tone ID",
        "PTT ID",
        "RX Color Code",
        "Slot",
        "Scan List",
        "Receive Group List",
        "PTT Prohibit",
        "Reverse",
        "Digital Duplex",
        "Slot Suit",
        "AES Digital Encryption",
        "Digital Encryption",
        "Call Confirmation",
        "Talk Around(Simplex)",
        "Work Alone",
        "Custom CTCSS",
        "2TONE Decode",
        "Ranging",
        "Idle TX",
        "APRS RX",
        "Analog APRS PTT Mode",
        "Digital APRS PTT Mode",
        "APRS Report Type",
        "Digital APRS Report Channel",
        "Correct Frequency[Hz]",
        "SMS Confirmation",
        "Exclude channel from roaming",
        "DMR MODE",
        "DataACK Disable",
        "R5toneBot",
        "R5ToneEot",
        "Auto Scan",
        "Ana APRS Mute",
        "Send Talker Alias DMR/NX",
        "AnaAprsTxPath",
        "ARC4",
        "ex_emg_kind",
        "Rpga_Mdc",
        "DisturEn",
        "DisturFreq",
        "dmr_crc_ignore",
        "compand",
        "tx_talkalaes",
        "dup_call",
        "tx_int",
        "BtRxState",
        "idle_tx",
        "nxdn_wn",
        "NxdnRpga",
        "nxdnSqCon",
        "NxdnTxBusy",
        "NxDnPttId",
        "EnRan",
        "DeRan",
        "NxdnEncry",
        "NxdnGroupId",
        "NxdnIdNum",
        "NxdnStateNum",
        "txcc"
    ];

    private static readonly string[] AnalogValues =
    [
        "100", "V00 145.500", "145.50000", "145.50000", "A-Analog", "Turbo", "25K", "Off", "Off", "Contact1",
        "Group Call", "12345678", "My Radio", "Off", "Carrier", "Off", "1", "1", "1", "Off", "1", "1",
        "None", "None", "Off", "Off", "Off", "Off", "Normal Encryption", "Off", "Off", "Off", "Off", "251.1",
        "1", "Off", "Off", "Off", "Off", "Off", "Off", "1", "0", "Off", "0", "1", "0", "0", "0", "0",
        "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0",
        "0", "0", "0", "0", "0", "0", "0", "0", "1"
    ];

    private static readonly string[] DmrValues =
    [
        "400", "DMRV1", "145.37500", "145.37500", "D-Digital", "Turbo", "12.5K", "Off", "Off", "Contact1",
        "Group Call", "12345678", "My Radio", "Always", "Carrier", "Off", "1", "1", "1", "Off", "1", "1",
        "None", "None", "Off", "Off", "Off", "Off", "Normal Encryption", "Off", "Off", "On", "Off", "131.8",
        "1", "Off", "On", "Off", "Off", "Off", "Off", "1", "0", "Off", "0", "0", "1", "0", "0", "0",
        "0", "0", "0", "0", "0", "0", "0", "13", "0", "0", "0", "0", "0", "0", "0", "1", "0", "0",
        "0", "0", "0", "0", "0", "255", "0", "0", "1"
    ];

    public static IReadOnlyDictionary<string, string> AnalogDefaults { get; } = ToDictionary(AnalogValues);
    public static IReadOnlyDictionary<string, string> DmrDefaults { get; } = ToDictionary(DmrValues);

    public static IReadOnlyDictionary<string, string> GetDefaults(bool isDigital)
    {
        return isDigital ? DmrDefaults : AnalogDefaults;
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(IReadOnlyList<string> values)
    {
        return Headers
            .Select((header, index) => (header, value: values[index]))
            .ToDictionary(pair => pair.header, pair => pair.value);
    }
}
