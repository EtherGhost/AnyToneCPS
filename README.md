# AnyToneCPS

AnyToneCPS is an open-source, cross-platform CPS (codeplug programming
software) for the AnyTone D890UV DMR radio. It talks to the radio directly
over its USB serial protocol - reverse-engineered from scratch, byte offset
by byte offset, against real hardware - so it does not depend on or wrap
the vendor's Windows-only CPS.

## Screenshots

| Desktop | Mobile - channel editor | Mobile - radio view |
| --- | --- | --- |
| ![Desktop](docs/screenshots/desktop.png) | ![Mobile channel editor](docs/screenshots/mobile-channel-detail.png) | ![Mobile radio view](docs/screenshots/mobile-radio-view.png) |

## Status

Early but functional. The app already covers the large majority of the
D890UV's codeplug:

- Channels, Zones, Scan Lists, Receive Group Lists, Auto Repeater Offsets
- Roaming Channels, Roaming Zones, GPS Roaming
- Radio ID list, Talkgroups, Digital Contacts (+ whitelist), Talkgroup
  whitelist, Master ID, Prefabricated SMS
- Analog Address Book, AM Air Band, AM Zone, FM Broadcast
- APRS Settings and APRS Receive Filters
- Signaling: QDC1200, QDC Address Book, 5Tone, 2Tone, DTMF
- Hot Keys, State Information
- Talk Alias Settings, Alarm Settings
- The full Radio Settings tree (power-on, display, work mode, VOX/BT, STE,
  AM/FM, key function, GPS/ranging, VFO scan, auto repeater, record,
  volume/audio, satellite, and more)
- Encryption Keys (Digital, ARC4, AES)

Read From Radio and Write To Radio both work over USB, independently
verified against live USB captures of the vendor CPS rather than assumed
from documentation. It is not a complete drop-in replacement for the vendor
CPS yet - some fields and radio behaviors are still being confirmed one at
a time against real hardware before their write path is trusted.

Project data is saved as a JSON project file, not CSV - CSV import/export
exists in the codebase but is currently disabled while the channel data
model is being migrated to a stronger typed representation; it is not a
supported workflow right now.

## Disclaimer

This is a hobby project, built and maintained in spare time - not an
official or supported product.

- **Built and tested against the AnyTone D890UV specifically.** Other
  AnyTone models have their own codeplug layout and protocol quirks that
  are not accounted for here - do not point this app at a different model.
- **Use it at your own risk.** Writing to a radio can go wrong: during
  development, a failed write left a test D890UV showing a programming
  error and factory-reset. Back up your codeplug with the vendor CPS (or at
  least do a Read From Radio and save the project file) before writing
  anything you'd be upset to lose.
- **This is a solo project, not a collaborative one.** Bug reports are
  welcome, but there's no guaranteed response time and no promise any given
  one gets fixed - I'll get to things if and when I decide to. The source is
  here to be read and forked, not to gather contributors.
- **Not affiliated with, endorsed by, or supported by AnyTone** in any way.
  All protocol and codeplug details here come from independent reverse
  engineering against real hardware, not from AnyTone documentation.
- Provided under the MIT license (see [`LICENSE`](LICENSE)): no warranty of
  any kind, used entirely at your own risk.

## Platforms

- **Desktop** - the primary development target. Tested on Linux (Fedora),
  including a NativeAOT-published build and Fedora RPM packaging. Should
  also run via Avalonia on Windows/macOS through `dotnet run`, but that has
  not been tested.
- **Android** - fully working, including live USB read/write to the radio
  from the phone itself. Ships as a NativeAOT-published APK.
- **Browser** - builds and runs, not yet exercised against real hardware
  (no USB serial access from a browser sandbox).
- **iOS** - project scaffolding exists; no iOS toolchain has been used to
  build or test it yet.

## Install

Download the latest build from the
[Releases](https://github.com/EtherGhost/AnyToneCPS/releases) page.

**Android**: install the APK (`adb install -r anytonecps.apk`, or just open
it on the phone). Updating over an existing install can fail with a
signature error if the new build was signed differently from what's already
on the phone - uninstall the old one first if that happens (this clears
whatever project file is saved in the app's own storage, so back it up
first if you care about it).

**Linux desktop** - pick one:

- RPM (Fedora): `sudo dnf install ./anytone-cps-<version>-1.fc44.x86_64.rpm`,
  then run `anytone-cps`.
- Flatpak: `flatpak install --user anytone-cps-<version>.flatpak`, then run
  `flatpak run se.tobbe.AnyToneCPS`. Asks for raw USB device access on
  install, since that's how it reaches the radio.
- AppImage: `chmod +x AnyToneCPS-<version>-x86_64.AppImage`, then run it
  directly - no install step.

**Windows/macOS**: not packaged yet.

## Technology

Built with .NET 10, Avalonia UI, and CommunityToolkit.Mvvm.

## Channel labels

Each channel row shows either `FM` or `DMR`, derived directly from the
channel's `Channel Type`: `A-Analog` shows as `FM`, every other channel
type shows as `DMR`.

An optional info badge can appear to the left of `FM`/`DMR`, derived
entirely from the channel's own data: `RPTR` for a repeater channel, a
frequency band label (`VHF`/`UHF`), `JAKT` if the name contains it, `ENC`/
`ARC4` depending on which encryption is in use, `SCRA` if scrambling is on,
or `RX` for a receive-only channel.

## Project data

The app saves project data as JSON under the user's application data
directory:

```text
AnyToneCPS/SE_Field_Comms_D890UV_v1.dat
```

Settings are saved under the user's app data directory as:

```text
AnyToneCPS/settings.json
```

Paths and naming are still provisional and are likely to change as model
and export support solidify.

## License

MIT - see [`LICENSE`](LICENSE).
