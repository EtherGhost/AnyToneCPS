using System.Collections;
using System.Linq;
using AnyToneCPS.Models;

namespace AnyToneCPS.Views;

/// <summary>
/// Shared by the various "Available X / Members" multi-select ListBox pairs
/// (Zone, ScanList, Receive Group List, AM Zone, ...) each detail view's own
/// code-behind uses to turn a ListBox's raw SelectedItems into a typed
/// array for the matching MainViewModel.SetSelectedXxx call. Split out of
/// MainView.axaml.cs 2026-08-10 when each entity's detail panel moved into
/// its own UserControl (see ChannelDetailView's own doc comment) - these 3
/// helpers are used by more than one of those new files, so they couldn't
/// just move into any single one of them.
/// </summary>
internal static class ListSelectionHelper
{
    public static ChannelEntry[] GetSelectedChannels(IEnumerable? selectedItems) =>
        selectedItems?.OfType<ChannelEntry>().ToArray() ?? [];

    public static AmAirEntry[] GetSelectedAmAirChannels(IEnumerable? selectedItems) =>
        selectedItems?.OfType<AmAirEntry>().ToArray() ?? [];

    public static TalkgroupEntry[] GetSelectedTalkgroups(IEnumerable? selectedItems) =>
        selectedItems?.OfType<TalkgroupEntry>().ToArray() ?? [];

    public static RoamingChannelEntry[] GetSelectedRoamingChannels(IEnumerable? selectedItems) =>
        selectedItems?.OfType<RoamingChannelEntry>().ToArray() ?? [];
}
