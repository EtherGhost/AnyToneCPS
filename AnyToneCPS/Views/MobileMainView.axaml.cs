using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AnyToneCPS.Models;
using AnyToneCPS.ViewModels;
using System;
using System.Collections;
using System.Linq;

namespace AnyToneCPS.Views;

public partial class MobileMainView : UserControl
{
    private IInputPane? _inputPane;
    private MobileEditorMode _editorMode = MobileEditorMode.None;
    private const double FocusRevealPadding = 24;
    private const double InputPaneExtraReserve = 72;

    // Long-press-to-select for the Channels list (no touch equivalent of
    // Desktop's Ctrl/Shift-click - see MainView.axaml.cs's own
    // ChannelsList_OnSelectionChanged for that side). A DispatcherTimer
    // started on PointerPressed and cancelled on PointerMoved-past-tolerance/
    // PointerReleased stands in for a real long-press gesture, since this
    // Avalonia version has no built-in Holding gesture recognizer.
    private DispatcherTimer? _channelLongPressTimer;
    private Point _channelLongPressStartPoint;
    private bool _channelSelectionModeActive;
    // Real touch input reports several DIP of jitter even from a finger held
    // still - 12 was too tight and cancelled the long-press almost every
    // time before it could fire on real hardware. 24 matches Android's own
    // usual touch-slop ballpark.
    private const double ChannelLongPressToleranceDip = 24;
    private static readonly TimeSpan ChannelLongPressDuration = TimeSpan.FromMilliseconds(500);

    public MobileMainView()
    {
        InitializeComponent();
        AddHandler(InputElement.GotFocusEvent, InputElement_OnGotFocus, RoutingStrategies.Tunnel);

        // Tunnel, not the XAML "TextInput=..." attribute's default Bubble
        // routing - TextBox's own internal insert-into-text logic runs
        // first on Bubble (same control), so a Bubble handler is always
        // too late to block the character.
        PowerOnPasswordCharBox.AddHandler(InputElement.TextInputEvent, DigitOnlyInput.RejectNonDigits, RoutingStrategies.Tunnel);

        // Same reasoning as the TextInput handler above - ListBoxItem's own
        // built-in pointer-press handling (selection/click) runs on Bubble
        // and marks the event Handled, which would stop a plain Bubble
        // handler on ChannelList itself from ever seeing it. Tunnel fires
        // on the way down, before that happens.
        ChannelList.AddHandler(InputElement.PointerPressedEvent, ChannelList_OnPointerPressed, RoutingStrategies.Tunnel);
        ChannelList.AddHandler(InputElement.PointerMovedEvent, ChannelList_OnPointerMoved, RoutingStrategies.Tunnel);
        ChannelList.AddHandler(InputElement.PointerReleasedEvent, ChannelList_OnPointerReleased, RoutingStrategies.Tunnel);
        ChannelList.AddHandler(InputElement.PointerCaptureLostEvent, ChannelList_OnPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var topLevel = TopLevel.GetTopLevel(this);
        _inputPane = topLevel?.InputPane;
        if (_inputPane is not null)
        {
            _inputPane.StateChanged += InputPane_OnStateChanged;
            UpdateInputPaneSpacer();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_inputPane is not null)
        {
            _inputPane.StateChanged -= InputPane_OnStateChanged;
            _inputPane = null;
        }

        base.OnUnloaded(e);
    }

    private void NavigationTreeLeaf_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: NavigationTreeNode { TabIndex: { } tabIndex } node } && DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedTabIndex = tabIndex;
            if (node.SubTabIndex is { } subTabIndex)
            {
                viewModel.SelectedOptionalSettingsSubTabIndex = subTabIndex;
            }

            ShowChannelList();
            ShowZoneList();
            ShowRoamingChannelList();
            ShowRadioIdList();
            ShowTalkgroupList();
            ShowScanListList();
            ShowRoamingZoneList();
            ShowReceiveGroupListList();
            ShowAutoRepeaterOffsetList();
            ShowAnalogAddressList();
            ShowGpsRoamingList();
            ShowTalkgroupWhitelistList();
            ShowDigitalContactWhitelistList();
            ShowPrefabricatedSmsList();
            ShowAmAirList();
            ShowAmZoneList();
            ShowFmChannelList();
            ShowAnalogQuickCallList();
            ShowStateInformationList();
            ShowHotKeyList();
            ShowQdc1200IdList();
            ShowQdcAddressList();
            ShowFiveToneIdList();
            ShowTwoToneEncodeEntryList();
            ShowTwoToneDecodeEntryList();
            ShowAprsReceiveFilterList();
            ShowDigitalKeyList();
            ShowArc4KeyList();
            ShowAesKeyList();
            NavigationButton.Flyout?.Hide();
        }
    }

    private void NavigationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NavigationButton.Flyout?.Hide();
    }

    // 2026-08-01: same "chevron alone is too small a target" fix as
    // Desktop's MainView.axaml.cs - the category header TextBlock (only
    // ever visible for HasChildren nodes, see the DataTemplate below) now
    // also toggles expand/collapse, on top of the built-in chevron.
    private void NavigationTreeHeader_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.FindAncestorOfType<TreeViewItem>() is { } item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    private void ChannelList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (_channelSelectionModeActive)
        {
            // ChannelList's own SelectionMode="Multiple, Toggle" (set by
            // EnterChannelSelectionMode) already toggled the tapped row by
            // the time Tapped fires - just leave selection mode once the
            // user has tapped every selected row back off.
            if (DataContext is MainViewModel { SelectedChannels.Count: 0 })
            {
                ExitChannelSelectionMode();
            }

            return;
        }

        if (DataContext is MainViewModel { SelectedChannel: not null })
        {
            ShowChannelEditor();
        }
    }

    private void ChannelList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedChannels(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void ChannelList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_channelSelectionModeActive || sender is not Control control)
        {
            return;
        }

        _channelLongPressStartPoint = e.GetPosition(control);
        _channelLongPressTimer?.Stop();
        _channelLongPressTimer = new DispatcherTimer { Interval = ChannelLongPressDuration };
        _channelLongPressTimer.Tick += ChannelLongPressTimer_OnTick;
        _channelLongPressTimer.Start();
    }

    private void ChannelLongPressTimer_OnTick(object? sender, EventArgs e)
    {
        _channelLongPressTimer?.Stop();
        _channelLongPressTimer = null;
        EnterChannelSelectionMode();
    }

    private void ChannelList_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_channelLongPressTimer is null || sender is not Control control)
        {
            return;
        }

        var current = e.GetPosition(control);
        var dx = current.X - _channelLongPressStartPoint.X;
        var dy = current.Y - _channelLongPressStartPoint.Y;
        if (Math.Sqrt(dx * dx + dy * dy) > ChannelLongPressToleranceDip)
        {
            _channelLongPressTimer.Stop();
            _channelLongPressTimer = null;
        }
    }

    private void ChannelList_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _channelLongPressTimer?.Stop();
        _channelLongPressTimer = null;
    }

    private void ChannelList_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _channelLongPressTimer?.Stop();
        _channelLongPressTimer = null;
    }

    private void EnterChannelSelectionMode()
    {
        if (_channelSelectionModeActive)
        {
            return;
        }

        _channelSelectionModeActive = true;
        // Toggle mode: every tap flips that row's own selection, instead of
        // Multiple mode's default of replacing the whole selection with just
        // the tapped row - the touch equivalent of Ctrl-click.
        ChannelList.SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle;
        ChannelListTitle.IsVisible = false;
        ChannelSelectionCountText.IsVisible = true;
        // Cancel replaces "+" rather than adding a 4th button to the row -
        // see ChannelListHeader's own doc comment in the XAML for why (this
        // row has no wrap/clip, a 4th button runs off the screen edge).
        ChannelAddButton.IsVisible = false;
        ChannelSelectionCancelButton.IsVisible = true;
        if (DataContext is MainViewModel { SelectedChannel: { } channel })
        {
            ChannelList.SelectedItems?.Clear();
            ChannelList.SelectedItems?.Add(channel);
        }
    }

    private void ExitChannelSelectionMode()
    {
        _channelSelectionModeActive = false;
        ChannelList.SelectionMode = SelectionMode.Single;
        ChannelListTitle.IsVisible = true;
        ChannelSelectionCountText.IsVisible = false;
        ChannelAddButton.IsVisible = true;
        ChannelSelectionCancelButton.IsVisible = false;
        ChannelList.SelectedItems?.Clear();
    }

    private void ChannelSelectionCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        ExitChannelSelectionMode();
    }

    private void ZoneList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedZone: not null })
        {
            ShowZoneEditor();
        }
    }

    private void RoamingChannelList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedRoamingChannel: not null })
        {
            ShowRoamingChannelEditor();
        }
    }

    private void RadioIdList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedRadioId: not null })
        {
            ShowRadioIdEditor();
        }
    }

    private void TalkgroupList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedTalkgroup: not null })
        {
            ShowTalkgroupEditor();
        }
    }

    private void ScanListList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedScanList: not null })
        {
            ShowScanListEditor();
        }
    }

    private void RoamingZoneList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedRoamingZone: not null })
        {
            ShowRoamingZoneEditor();
        }
    }

    private void ReceiveGroupListList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedReceiveGroupList: not null })
        {
            ShowReceiveGroupListEditor();
        }
    }

    private void DigitalContactList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedDigitalContact: not null })
        {
            ShowDigitalContactEditor();
        }
    }

    private void AutoRepeaterOffsetList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAutoRepeaterOffset: not null })
        {
            ShowAutoRepeaterOffsetEditor();
        }
    }

    private void AnalogAddressList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAnalogAddress: not null })
        {
            ShowAnalogAddressEditor();
        }
    }

    private void GpsRoamingList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedGpsRoaming: not null })
        {
            ShowGpsRoamingEditor();
        }
    }

    private void TalkgroupWhitelistList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedTalkgroupWhitelistEntry: not null })
        {
            ShowTalkgroupWhitelistEditor();
        }
    }

    private void DigitalContactWhitelistList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedDigitalContactWhitelistEntry: not null })
        {
            ShowDigitalContactWhitelistEditor();
        }
    }

    private void PrefabricatedSmsList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedPrefabricatedSms: not null })
        {
            ShowPrefabricatedSmsEditor();
        }
    }

    private void AmAirList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAmAir: not null })
        {
            ShowAmAirEditor();
        }
    }

    private void AmZoneList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAmZone: not null })
        {
            ShowAmZoneEditor();
        }
    }

    private void FmChannelList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedFmChannel: not null })
        {
            ShowFmChannelEditor();
        }
    }

    private void AnalogQuickCallList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAnalogQuickCall: not null })
        {
            ShowAnalogQuickCallEditor();
        }
    }

    private void StateInformationList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedStateInformation: not null })
        {
            ShowStateInformationEditor();
        }
    }

    private void HotKeyList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedHotKey: not null })
        {
            ShowHotKeyEditor();
        }
    }

    private void Qdc1200IdList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedQdc1200Id: not null })
        {
            ShowQdc1200IdEditor();
        }
    }

    private void QdcAddressList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedQdcAddress: not null })
        {
            ShowQdcAddressEditor();
        }
    }

    private void FiveToneIdList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedFiveToneId: not null })
        {
            ShowFiveToneIdEditor();
        }
    }

    private void TwoToneEncodeEntryList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedTwoToneEncodeEntry: not null })
        {
            ShowTwoToneEncodeEntryEditor();
        }
    }

    private void TwoToneDecodeEntryList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedTwoToneDecodeEntry: not null })
        {
            ShowTwoToneDecodeEntryEditor();
        }
    }

    private void AprsReceiveFilterList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAprsReceiveFilter: not null })
        {
            ShowAprsReceiveFilterEditor();
        }
    }

    private void DigitalKeyList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedEncryptionKey: not null })
        {
            ShowDigitalKeyEditor();
        }
    }

    private void Arc4KeyList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedArc4EncryptionKey: not null })
        {
            ShowArc4KeyEditor();
        }
    }

    private void AesKeyList_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedAesEncryptionKey: not null })
        {
            ShowAesKeyEditor();
        }
    }

    private void BackToChannelList_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowChannelList();
    }

    private void BackToZoneList_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowZoneList();
    }

    private void PreviousChannel_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedChannel(-1);
    }

    private void NextChannel_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedChannel(1);
    }

    private void PreviousZone_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedZone(-1);
    }

    private void NextZone_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedZone(1);
    }

    private void EditorBack_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_editorMode == MobileEditorMode.Channel)
        {
            ShowChannelList();
        }
        else if (_editorMode == MobileEditorMode.Zone)
        {
            ShowZoneList();
        }
        else if (_editorMode == MobileEditorMode.RoamingChannel)
        {
            ShowRoamingChannelList();
        }
        else if (_editorMode == MobileEditorMode.RadioId)
        {
            ShowRadioIdList();
        }
        else if (_editorMode == MobileEditorMode.Talkgroup)
        {
            ShowTalkgroupList();
        }
        else if (_editorMode == MobileEditorMode.ScanList)
        {
            ShowScanListList();
        }
        else if (_editorMode == MobileEditorMode.RoamingZone)
        {
            ShowRoamingZoneList();
        }
        else if (_editorMode == MobileEditorMode.ReceiveGroupList)
        {
            ShowReceiveGroupListList();
        }
        else if (_editorMode == MobileEditorMode.AutoRepeaterOffset)
        {
            ShowAutoRepeaterOffsetList();
        }
        else if (_editorMode == MobileEditorMode.AnalogAddress)
        {
            ShowAnalogAddressList();
        }
        else if (_editorMode == MobileEditorMode.GpsRoaming)
        {
            ShowGpsRoamingList();
        }
        else if (_editorMode == MobileEditorMode.TalkgroupWhitelist)
        {
            ShowTalkgroupWhitelistList();
        }
        else if (_editorMode == MobileEditorMode.DigitalContactWhitelist)
        {
            ShowDigitalContactWhitelistList();
        }
        else if (_editorMode == MobileEditorMode.PrefabricatedSms)
        {
            ShowPrefabricatedSmsList();
        }
        else if (_editorMode == MobileEditorMode.AmAir)
        {
            ShowAmAirList();
        }
        else if (_editorMode == MobileEditorMode.AmZone)
        {
            ShowAmZoneList();
        }
        else if (_editorMode == MobileEditorMode.FmChannel)
        {
            ShowFmChannelList();
        }
        else if (_editorMode == MobileEditorMode.AnalogQuickCall)
        {
            ShowAnalogQuickCallList();
        }
        else if (_editorMode == MobileEditorMode.StateInformation)
        {
            ShowStateInformationList();
        }
        else if (_editorMode == MobileEditorMode.HotKey)
        {
            ShowHotKeyList();
        }
        else if (_editorMode == MobileEditorMode.Qdc1200Id)
        {
            ShowQdc1200IdList();
        }
        else if (_editorMode == MobileEditorMode.QdcAddress)
        {
            ShowQdcAddressList();
        }
        else if (_editorMode == MobileEditorMode.FiveToneId)
        {
            ShowFiveToneIdList();
        }
        else if (_editorMode == MobileEditorMode.TwoToneEncodeEntry)
        {
            ShowTwoToneEncodeEntryList();
        }
        else if (_editorMode == MobileEditorMode.TwoToneDecodeEntry)
        {
            ShowTwoToneDecodeEntryList();
        }
        else if (_editorMode == MobileEditorMode.AprsReceiveFilter)
        {
            ShowAprsReceiveFilterList();
        }
        else if (_editorMode == MobileEditorMode.DigitalContact)
        {
            ShowDigitalContactList();
        }
        else if (_editorMode == MobileEditorMode.DigitalKey)
        {
            ShowDigitalKeyList();
        }
        else if (_editorMode == MobileEditorMode.Arc4Key)
        {
            ShowArc4KeyList();
        }
        else if (_editorMode == MobileEditorMode.AesKey)
        {
            ShowAesKeyList();
        }
    }

    private void EditorPrevious_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_editorMode == MobileEditorMode.Channel)
        {
            MoveSelectedChannel(-1);
        }
        else if (_editorMode == MobileEditorMode.Zone)
        {
            MoveSelectedZone(-1);
        }
        else if (_editorMode == MobileEditorMode.RoamingChannel)
        {
            MoveSelectedRoamingChannel(-1);
        }
        else if (_editorMode == MobileEditorMode.RadioId)
        {
            MoveSelectedRadioId(-1);
        }
        else if (_editorMode == MobileEditorMode.Talkgroup)
        {
            MoveSelectedTalkgroup(-1);
        }
        else if (_editorMode == MobileEditorMode.ScanList)
        {
            MoveSelectedScanList(-1);
        }
        else if (_editorMode == MobileEditorMode.RoamingZone)
        {
            MoveSelectedRoamingZone(-1);
        }
        else if (_editorMode == MobileEditorMode.ReceiveGroupList)
        {
            MoveSelectedReceiveGroupList(-1);
        }
        else if (_editorMode == MobileEditorMode.AutoRepeaterOffset)
        {
            MoveSelectedAutoRepeaterOffset(-1);
        }
        else if (_editorMode == MobileEditorMode.AnalogAddress)
        {
            MoveSelectedAnalogAddress(-1);
        }
        else if (_editorMode == MobileEditorMode.GpsRoaming)
        {
            MoveSelectedGpsRoaming(-1);
        }
        else if (_editorMode == MobileEditorMode.TalkgroupWhitelist)
        {
            MoveSelectedTalkgroupWhitelist(-1);
        }
        else if (_editorMode == MobileEditorMode.DigitalContactWhitelist)
        {
            MoveSelectedDigitalContactWhitelist(-1);
        }
        else if (_editorMode == MobileEditorMode.PrefabricatedSms)
        {
            MoveSelectedPrefabricatedSms(-1);
        }
        else if (_editorMode == MobileEditorMode.AmAir)
        {
            MoveSelectedAmAir(-1);
        }
        else if (_editorMode == MobileEditorMode.AmZone)
        {
            MoveSelectedAmZone(-1);
        }
        else if (_editorMode == MobileEditorMode.FmChannel)
        {
            MoveSelectedFmChannel(-1);
        }
        else if (_editorMode == MobileEditorMode.AnalogQuickCall)
        {
            MoveSelectedAnalogQuickCall(-1);
        }
        else if (_editorMode == MobileEditorMode.StateInformation)
        {
            MoveSelectedStateInformation(-1);
        }
        else if (_editorMode == MobileEditorMode.HotKey)
        {
            MoveSelectedHotKey(-1);
        }
        else if (_editorMode == MobileEditorMode.Qdc1200Id)
        {
            MoveSelectedQdc1200Id(-1);
        }
        else if (_editorMode == MobileEditorMode.QdcAddress)
        {
            MoveSelectedQdcAddress(-1);
        }
        else if (_editorMode == MobileEditorMode.FiveToneId)
        {
            MoveSelectedFiveToneId(-1);
        }
        else if (_editorMode == MobileEditorMode.TwoToneEncodeEntry)
        {
            MoveSelectedTwoToneEncodeEntry(-1);
        }
        else if (_editorMode == MobileEditorMode.TwoToneDecodeEntry)
        {
            MoveSelectedTwoToneDecodeEntry(-1);
        }
        else if (_editorMode == MobileEditorMode.AprsReceiveFilter)
        {
            MoveSelectedAprsReceiveFilter(-1);
        }
        else if (_editorMode == MobileEditorMode.DigitalContact)
        {
            MoveSelectedDigitalContact(-1);
        }
        else if (_editorMode == MobileEditorMode.DigitalKey)
        {
            MoveSelectedDigitalEncryptionKey(-1);
        }
        else if (_editorMode == MobileEditorMode.Arc4Key)
        {
            MoveSelectedArc4EncryptionKey(-1);
        }
        else if (_editorMode == MobileEditorMode.AesKey)
        {
            MoveSelectedAesEncryptionKey(-1);
        }
    }

    private void EditorNext_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_editorMode == MobileEditorMode.Channel)
        {
            MoveSelectedChannel(1);
        }
        else if (_editorMode == MobileEditorMode.Zone)
        {
            MoveSelectedZone(1);
        }
        else if (_editorMode == MobileEditorMode.RoamingChannel)
        {
            MoveSelectedRoamingChannel(1);
        }
        else if (_editorMode == MobileEditorMode.RadioId)
        {
            MoveSelectedRadioId(1);
        }
        else if (_editorMode == MobileEditorMode.Talkgroup)
        {
            MoveSelectedTalkgroup(1);
        }
        else if (_editorMode == MobileEditorMode.ScanList)
        {
            MoveSelectedScanList(1);
        }
        else if (_editorMode == MobileEditorMode.RoamingZone)
        {
            MoveSelectedRoamingZone(1);
        }
        else if (_editorMode == MobileEditorMode.ReceiveGroupList)
        {
            MoveSelectedReceiveGroupList(1);
        }
        else if (_editorMode == MobileEditorMode.AutoRepeaterOffset)
        {
            MoveSelectedAutoRepeaterOffset(1);
        }
        else if (_editorMode == MobileEditorMode.AnalogAddress)
        {
            MoveSelectedAnalogAddress(1);
        }
        else if (_editorMode == MobileEditorMode.GpsRoaming)
        {
            MoveSelectedGpsRoaming(1);
        }
        else if (_editorMode == MobileEditorMode.TalkgroupWhitelist)
        {
            MoveSelectedTalkgroupWhitelist(1);
        }
        else if (_editorMode == MobileEditorMode.DigitalContactWhitelist)
        {
            MoveSelectedDigitalContactWhitelist(1);
        }
        else if (_editorMode == MobileEditorMode.PrefabricatedSms)
        {
            MoveSelectedPrefabricatedSms(1);
        }
        else if (_editorMode == MobileEditorMode.AmAir)
        {
            MoveSelectedAmAir(1);
        }
        else if (_editorMode == MobileEditorMode.AmZone)
        {
            MoveSelectedAmZone(1);
        }
        else if (_editorMode == MobileEditorMode.FmChannel)
        {
            MoveSelectedFmChannel(1);
        }
        else if (_editorMode == MobileEditorMode.AnalogQuickCall)
        {
            MoveSelectedAnalogQuickCall(1);
        }
        else if (_editorMode == MobileEditorMode.StateInformation)
        {
            MoveSelectedStateInformation(1);
        }
        else if (_editorMode == MobileEditorMode.HotKey)
        {
            MoveSelectedHotKey(1);
        }
        else if (_editorMode == MobileEditorMode.Qdc1200Id)
        {
            MoveSelectedQdc1200Id(1);
        }
        else if (_editorMode == MobileEditorMode.QdcAddress)
        {
            MoveSelectedQdcAddress(1);
        }
        else if (_editorMode == MobileEditorMode.FiveToneId)
        {
            MoveSelectedFiveToneId(1);
        }
        else if (_editorMode == MobileEditorMode.TwoToneEncodeEntry)
        {
            MoveSelectedTwoToneEncodeEntry(1);
        }
        else if (_editorMode == MobileEditorMode.TwoToneDecodeEntry)
        {
            MoveSelectedTwoToneDecodeEntry(1);
        }
        else if (_editorMode == MobileEditorMode.AprsReceiveFilter)
        {
            MoveSelectedAprsReceiveFilter(1);
        }
        else if (_editorMode == MobileEditorMode.DigitalContact)
        {
            MoveSelectedDigitalContact(1);
        }
        else if (_editorMode == MobileEditorMode.DigitalKey)
        {
            MoveSelectedDigitalEncryptionKey(1);
        }
        else if (_editorMode == MobileEditorMode.Arc4Key)
        {
            MoveSelectedArc4EncryptionKey(1);
        }
        else if (_editorMode == MobileEditorMode.AesKey)
        {
            MoveSelectedAesEncryptionKey(1);
        }
    }

    private void ShowChannelList()
    {
        if (_channelSelectionModeActive)
        {
            ExitChannelSelectionMode();
        }

        ChannelListHeader.IsVisible = true;
        ChannelList.IsVisible = true;
        ChannelEditorHeader.IsVisible = false;
        ChannelEditorTabs.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowChannelEditor()
    {
        ChannelListHeader.IsVisible = false;
        ChannelList.IsVisible = false;
        ChannelEditorHeader.IsVisible = true;
        ChannelEditorTabs.IsVisible = true;
        // Always land on "Common" (index 0, visible for every channel type)
        // rather than leaving SelectedIndex on whichever tab was showing
        // before - the Analog/Digital tabs are now hidden per channel type
        // (not just their content), so staying on a now-hidden tab would
        // leave the content area showing the wrong channel type's fields.
        ChannelEditorTabs.SelectedIndex = 0;
        _editorMode = MobileEditorMode.Channel;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowZoneList()
    {
        ZoneListHeader.IsVisible = true;
        ZoneList.IsVisible = true;
        ZoneEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowZoneEditor()
    {
        ZoneListHeader.IsVisible = false;
        ZoneList.IsVisible = false;
        ZoneEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.Zone;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRoamingChannelList()
    {
        RoamingChannelListHeader.IsVisible = true;
        RoamingChannelList.IsVisible = true;
        RoamingChannelEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRoamingChannelEditor()
    {
        RoamingChannelListHeader.IsVisible = false;
        RoamingChannelList.IsVisible = false;
        RoamingChannelEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.RoamingChannel;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRadioIdList()
    {
        RadioIdListHeader.IsVisible = true;
        RadioIdList.IsVisible = true;
        RadioIdEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRadioIdEditor()
    {
        RadioIdListHeader.IsVisible = false;
        RadioIdList.IsVisible = false;
        RadioIdEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.RadioId;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTalkgroupList()
    {
        TalkgroupListHeader.IsVisible = true;
        TalkgroupList.IsVisible = true;
        TalkgroupEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTalkgroupEditor()
    {
        TalkgroupListHeader.IsVisible = false;
        TalkgroupList.IsVisible = false;
        TalkgroupEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.Talkgroup;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowScanListList()
    {
        ScanListListHeader.IsVisible = true;
        ScanListList.IsVisible = true;
        ScanListEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowScanListEditor()
    {
        ScanListListHeader.IsVisible = false;
        ScanListList.IsVisible = false;
        ScanListEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.ScanList;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRoamingZoneList()
    {
        RoamingZoneListHeader.IsVisible = true;
        RoamingZoneList.IsVisible = true;
        RoamingZoneEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowRoamingZoneEditor()
    {
        RoamingZoneListHeader.IsVisible = false;
        RoamingZoneList.IsVisible = false;
        RoamingZoneEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.RoamingZone;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowReceiveGroupListList()
    {
        ReceiveGroupListListHeader.IsVisible = true;
        ReceiveGroupListList.IsVisible = true;
        ReceiveGroupListEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowReceiveGroupListEditor()
    {
        ReceiveGroupListListHeader.IsVisible = false;
        ReceiveGroupListList.IsVisible = false;
        ReceiveGroupListEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.ReceiveGroupList;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalContactList()
    {
        DigitalContactListHeader.IsVisible = true;
        DigitalContactList.IsVisible = true;
        DigitalContactEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalContactEditor()
    {
        DigitalContactListHeader.IsVisible = false;
        DigitalContactList.IsVisible = false;
        DigitalContactEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.DigitalContact;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAutoRepeaterOffsetList()
    {
        AutoRepeaterOffsetListHeader.IsVisible = true;
        AutoRepeaterOffsetList.IsVisible = true;
        AutoRepeaterOffsetEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAutoRepeaterOffsetEditor()
    {
        AutoRepeaterOffsetListHeader.IsVisible = false;
        AutoRepeaterOffsetList.IsVisible = false;
        AutoRepeaterOffsetEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AutoRepeaterOffset;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAnalogAddressList()
    {
        AnalogAddressListHeader.IsVisible = true;
        AnalogAddressList.IsVisible = true;
        AnalogAddressEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAnalogAddressEditor()
    {
        AnalogAddressListHeader.IsVisible = false;
        AnalogAddressList.IsVisible = false;
        AnalogAddressEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AnalogAddress;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowGpsRoamingList()
    {
        GpsRoamingListHeader.IsVisible = true;
        GpsRoamingList.IsVisible = true;
        GpsRoamingEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowGpsRoamingEditor()
    {
        GpsRoamingListHeader.IsVisible = false;
        GpsRoamingList.IsVisible = false;
        GpsRoamingEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.GpsRoaming;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTalkgroupWhitelistList()
    {
        TalkgroupWhitelistListHeader.IsVisible = true;
        TalkgroupWhitelistList.IsVisible = true;
        TalkgroupWhitelistEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTalkgroupWhitelistEditor()
    {
        TalkgroupWhitelistListHeader.IsVisible = false;
        TalkgroupWhitelistList.IsVisible = false;
        TalkgroupWhitelistEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.TalkgroupWhitelist;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalContactWhitelistList()
    {
        DigitalContactWhitelistListHeader.IsVisible = true;
        DigitalContactWhitelistList.IsVisible = true;
        DigitalContactWhitelistEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalContactWhitelistEditor()
    {
        DigitalContactWhitelistListHeader.IsVisible = false;
        DigitalContactWhitelistList.IsVisible = false;
        DigitalContactWhitelistEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.DigitalContactWhitelist;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowPrefabricatedSmsList()
    {
        PrefabricatedSmsListHeader.IsVisible = true;
        PrefabricatedSmsList.IsVisible = true;
        PrefabricatedSmsEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowPrefabricatedSmsEditor()
    {
        PrefabricatedSmsListHeader.IsVisible = false;
        PrefabricatedSmsList.IsVisible = false;
        PrefabricatedSmsEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.PrefabricatedSms;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAmAirList()
    {
        AmAirListHeader.IsVisible = true;
        AmAirList.IsVisible = true;
        AmAirEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAmAirEditor()
    {
        AmAirListHeader.IsVisible = false;
        AmAirList.IsVisible = false;
        AmAirEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AmAir;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAmZoneList()
    {
        AmZoneListHeader.IsVisible = true;
        AmZoneList.IsVisible = true;
        AmZoneEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAmZoneEditor()
    {
        AmZoneListHeader.IsVisible = false;
        AmZoneList.IsVisible = false;
        AmZoneEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AmZone;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowFmChannelList()
    {
        FmChannelListHeader.IsVisible = true;
        FmChannelList.IsVisible = true;
        FmChannelEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowFmChannelEditor()
    {
        FmChannelListHeader.IsVisible = false;
        FmChannelList.IsVisible = false;
        FmChannelEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.FmChannel;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAnalogQuickCallList()
    {
        AnalogQuickCallListHeader.IsVisible = true;
        AnalogQuickCallList.IsVisible = true;
        AnalogQuickCallEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAnalogQuickCallEditor()
    {
        AnalogQuickCallListHeader.IsVisible = false;
        AnalogQuickCallList.IsVisible = false;
        AnalogQuickCallEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AnalogQuickCall;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowStateInformationList()
    {
        StateInformationListHeader.IsVisible = true;
        StateInformationList.IsVisible = true;
        StateInformationEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowStateInformationEditor()
    {
        StateInformationListHeader.IsVisible = false;
        StateInformationList.IsVisible = false;
        StateInformationEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.StateInformation;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowHotKeyList()
    {
        HotKeyListHeader.IsVisible = true;
        HotKeyList.IsVisible = true;
        HotKeyEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowHotKeyEditor()
    {
        HotKeyListHeader.IsVisible = false;
        HotKeyList.IsVisible = false;
        HotKeyEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.HotKey;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowQdc1200IdList()
    {
        Qdc1200IdListHeader.IsVisible = true;
        Qdc1200IdList.IsVisible = true;
        Qdc1200IdEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowQdc1200IdEditor()
    {
        Qdc1200IdListHeader.IsVisible = false;
        Qdc1200IdList.IsVisible = false;
        Qdc1200IdEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.Qdc1200Id;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowQdcAddressList()
    {
        QdcAddressListHeader.IsVisible = true;
        QdcAddressList.IsVisible = true;
        QdcAddressEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowQdcAddressEditor()
    {
        QdcAddressListHeader.IsVisible = false;
        QdcAddressList.IsVisible = false;
        QdcAddressEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.QdcAddress;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowFiveToneIdList()
    {
        FiveToneIdListHeader.IsVisible = true;
        FiveToneIdList.IsVisible = true;
        FiveToneIdEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowFiveToneIdEditor()
    {
        FiveToneIdListHeader.IsVisible = false;
        FiveToneIdList.IsVisible = false;
        FiveToneIdEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.FiveToneId;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTwoToneEncodeEntryList()
    {
        TwoToneEncodeEntryListHeader.IsVisible = true;
        TwoToneEncodeEntryList.IsVisible = true;
        TwoToneEncodeEntryEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTwoToneEncodeEntryEditor()
    {
        TwoToneEncodeEntryListHeader.IsVisible = false;
        TwoToneEncodeEntryList.IsVisible = false;
        TwoToneEncodeEntryEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.TwoToneEncodeEntry;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTwoToneDecodeEntryList()
    {
        TwoToneDecodeEntryListHeader.IsVisible = true;
        TwoToneDecodeEntryList.IsVisible = true;
        TwoToneDecodeEntryEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowTwoToneDecodeEntryEditor()
    {
        TwoToneDecodeEntryListHeader.IsVisible = false;
        TwoToneDecodeEntryList.IsVisible = false;
        TwoToneDecodeEntryEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.TwoToneDecodeEntry;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAprsReceiveFilterList()
    {
        AprsReceiveFilterListHeader.IsVisible = true;
        AprsReceiveFilterList.IsVisible = true;
        AprsReceiveFilterEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAprsReceiveFilterEditor()
    {
        AprsReceiveFilterListHeader.IsVisible = false;
        AprsReceiveFilterList.IsVisible = false;
        AprsReceiveFilterEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AprsReceiveFilter;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalKeyList()
    {
        DigitalKeyListHeader.IsVisible = true;
        DigitalKeyList.IsVisible = true;
        DigitalKeyEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowDigitalKeyEditor()
    {
        DigitalKeyListHeader.IsVisible = false;
        DigitalKeyList.IsVisible = false;
        DigitalKeyEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.DigitalKey;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowArc4KeyList()
    {
        Arc4KeyListHeader.IsVisible = true;
        Arc4KeyList.IsVisible = true;
        Arc4KeyEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowArc4KeyEditor()
    {
        Arc4KeyListHeader.IsVisible = false;
        Arc4KeyList.IsVisible = false;
        Arc4KeyEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.Arc4Key;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAesKeyList()
    {
        AesKeyListHeader.IsVisible = true;
        AesKeyList.IsVisible = true;
        AesKeyEditorPanel.IsVisible = false;
        _editorMode = MobileEditorMode.None;
        EditorNavigationBar.IsVisible = false;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void ShowAesKeyEditor()
    {
        AesKeyListHeader.IsVisible = false;
        AesKeyList.IsVisible = false;
        AesKeyEditorPanel.IsVisible = true;
        _editorMode = MobileEditorMode.AesKey;
        EditorNavigationBar.IsVisible = true;
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(0);
    }

    private void MoveSelectedChannel(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.Channels.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedChannel is null
            ? -1
            : viewModel.Channels.IndexOf(viewModel.SelectedChannel);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.Channels.Count - 1);
        viewModel.SelectedChannel = viewModel.Channels[nextIndex];
        ShowChannelEditor();
    }

    private void MoveSelectedZone(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.Zones.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedZone is null
            ? -1
            : viewModel.Zones.IndexOf(viewModel.SelectedZone);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.Zones.Count - 1);
        viewModel.SelectedZone = viewModel.Zones[nextIndex];
        ShowZoneEditor();
    }

    private void MoveSelectedRoamingChannel(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.RoamingChannels.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedRoamingChannel is null
            ? -1
            : viewModel.RoamingChannels.IndexOf(viewModel.SelectedRoamingChannel);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.RoamingChannels.Count - 1);
        viewModel.SelectedRoamingChannel = viewModel.RoamingChannels[nextIndex];
        ShowRoamingChannelEditor();
    }

    private void MoveSelectedRadioId(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.RadioIds.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedRadioId is null ? -1 : viewModel.RadioIds.IndexOf(viewModel.SelectedRadioId);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.RadioIds.Count - 1);
        viewModel.SelectedRadioId = viewModel.RadioIds[nextIndex];
        ShowRadioIdEditor();
    }

    private void MoveSelectedTalkgroup(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.Talkgroups.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedTalkgroup is null ? -1 : viewModel.Talkgroups.IndexOf(viewModel.SelectedTalkgroup);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.Talkgroups.Count - 1);
        viewModel.SelectedTalkgroup = viewModel.Talkgroups[nextIndex];
        ShowTalkgroupEditor();
    }

    private void MoveSelectedScanList(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.ScanLists.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedScanList is null ? -1 : viewModel.ScanLists.IndexOf(viewModel.SelectedScanList);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.ScanLists.Count - 1);
        viewModel.SelectedScanList = viewModel.ScanLists[nextIndex];
        ShowScanListEditor();
    }

    private void MoveSelectedRoamingZone(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.RoamingZones.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedRoamingZone is null ? -1 : viewModel.RoamingZones.IndexOf(viewModel.SelectedRoamingZone);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.RoamingZones.Count - 1);
        viewModel.SelectedRoamingZone = viewModel.RoamingZones[nextIndex];
        ShowRoamingZoneEditor();
    }

    private void MoveSelectedReceiveGroupList(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.ReceiveGroupLists.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedReceiveGroupList is null ? -1 : viewModel.ReceiveGroupLists.IndexOf(viewModel.SelectedReceiveGroupList);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.ReceiveGroupLists.Count - 1);
        viewModel.SelectedReceiveGroupList = viewModel.ReceiveGroupLists[nextIndex];
        ShowReceiveGroupListEditor();
    }

    private void MoveSelectedDigitalContact(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.FilteredDigitalContacts.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedDigitalContact is null ? -1 : viewModel.FilteredDigitalContacts.IndexOf(viewModel.SelectedDigitalContact);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.FilteredDigitalContacts.Count - 1);
        viewModel.SelectedDigitalContact = viewModel.FilteredDigitalContacts[nextIndex];
        ShowDigitalContactEditor();
    }

    private void MoveSelectedAutoRepeaterOffset(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AutoRepeaterOffsets.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAutoRepeaterOffset is null ? -1 : viewModel.AutoRepeaterOffsets.IndexOf(viewModel.SelectedAutoRepeaterOffset);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AutoRepeaterOffsets.Count - 1);
        viewModel.SelectedAutoRepeaterOffset = viewModel.AutoRepeaterOffsets[nextIndex];
        ShowAutoRepeaterOffsetEditor();
    }

    private void MoveSelectedAnalogAddress(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AnalogAddresses.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAnalogAddress is null ? -1 : viewModel.AnalogAddresses.IndexOf(viewModel.SelectedAnalogAddress);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AnalogAddresses.Count - 1);
        viewModel.SelectedAnalogAddress = viewModel.AnalogAddresses[nextIndex];
        ShowAnalogAddressEditor();
    }

    private void MoveSelectedGpsRoaming(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.GpsRoamingEntries.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedGpsRoaming is null ? -1 : viewModel.GpsRoamingEntries.IndexOf(viewModel.SelectedGpsRoaming);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.GpsRoamingEntries.Count - 1);
        viewModel.SelectedGpsRoaming = viewModel.GpsRoamingEntries[nextIndex];
        ShowGpsRoamingEditor();
    }

    private void MoveSelectedTalkgroupWhitelist(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.TalkgroupWhitelist.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedTalkgroupWhitelistEntry is null ? -1 : viewModel.TalkgroupWhitelist.IndexOf(viewModel.SelectedTalkgroupWhitelistEntry);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.TalkgroupWhitelist.Count - 1);
        viewModel.SelectedTalkgroupWhitelistEntry = viewModel.TalkgroupWhitelist[nextIndex];
        ShowTalkgroupWhitelistEditor();
    }

    private void MoveSelectedDigitalContactWhitelist(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.DigitalContactWhitelist.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedDigitalContactWhitelistEntry is null ? -1 : viewModel.DigitalContactWhitelist.IndexOf(viewModel.SelectedDigitalContactWhitelistEntry);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.DigitalContactWhitelist.Count - 1);
        viewModel.SelectedDigitalContactWhitelistEntry = viewModel.DigitalContactWhitelist[nextIndex];
        ShowDigitalContactWhitelistEditor();
    }

    private void MoveSelectedPrefabricatedSms(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.PrefabricatedSmsMessages.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedPrefabricatedSms is null ? -1 : viewModel.PrefabricatedSmsMessages.IndexOf(viewModel.SelectedPrefabricatedSms);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.PrefabricatedSmsMessages.Count - 1);
        viewModel.SelectedPrefabricatedSms = viewModel.PrefabricatedSmsMessages[nextIndex];
        ShowPrefabricatedSmsEditor();
    }

    private void MoveSelectedAmAir(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AmAirChannels.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAmAir is null ? -1 : viewModel.AmAirChannels.IndexOf(viewModel.SelectedAmAir);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AmAirChannels.Count - 1);
        viewModel.SelectedAmAir = viewModel.AmAirChannels[nextIndex];
        ShowAmAirEditor();
    }

    private void MoveSelectedAmZone(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AmZones.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAmZone is null ? -1 : viewModel.AmZones.IndexOf(viewModel.SelectedAmZone);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AmZones.Count - 1);
        viewModel.SelectedAmZone = viewModel.AmZones[nextIndex];
        ShowAmZoneEditor();
    }

    private void MoveSelectedFmChannel(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.FmChannels.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedFmChannel is null ? -1 : viewModel.FmChannels.IndexOf(viewModel.SelectedFmChannel);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.FmChannels.Count - 1);
        viewModel.SelectedFmChannel = viewModel.FmChannels[nextIndex];
        ShowFmChannelEditor();
    }

    private void MoveSelectedAnalogQuickCall(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AnalogQuickCalls.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAnalogQuickCall is null ? -1 : viewModel.AnalogQuickCalls.IndexOf(viewModel.SelectedAnalogQuickCall);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AnalogQuickCalls.Count - 1);
        viewModel.SelectedAnalogQuickCall = viewModel.AnalogQuickCalls[nextIndex];
        ShowAnalogQuickCallEditor();
    }

    private void MoveSelectedStateInformation(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.StateInformationEntries.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedStateInformation is null ? -1 : viewModel.StateInformationEntries.IndexOf(viewModel.SelectedStateInformation);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.StateInformationEntries.Count - 1);
        viewModel.SelectedStateInformation = viewModel.StateInformationEntries[nextIndex];
        ShowStateInformationEditor();
    }

    private void MoveSelectedHotKey(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.HotKeys.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedHotKey is null ? -1 : viewModel.HotKeys.IndexOf(viewModel.SelectedHotKey);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.HotKeys.Count - 1);
        viewModel.SelectedHotKey = viewModel.HotKeys[nextIndex];
        ShowHotKeyEditor();
    }

    private void MoveSelectedQdc1200Id(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.Qdc1200Ids.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedQdc1200Id is null ? -1 : viewModel.Qdc1200Ids.IndexOf(viewModel.SelectedQdc1200Id);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.Qdc1200Ids.Count - 1);
        viewModel.SelectedQdc1200Id = viewModel.Qdc1200Ids[nextIndex];
        ShowQdc1200IdEditor();
    }

    private void MoveSelectedQdcAddress(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.QdcAddresses.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedQdcAddress is null ? -1 : viewModel.QdcAddresses.IndexOf(viewModel.SelectedQdcAddress);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.QdcAddresses.Count - 1);
        viewModel.SelectedQdcAddress = viewModel.QdcAddresses[nextIndex];
        ShowQdcAddressEditor();
    }

    private void MoveSelectedFiveToneId(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.FiveToneIds.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedFiveToneId is null ? -1 : viewModel.FiveToneIds.IndexOf(viewModel.SelectedFiveToneId);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.FiveToneIds.Count - 1);
        viewModel.SelectedFiveToneId = viewModel.FiveToneIds[nextIndex];
        ShowFiveToneIdEditor();
    }

    private void MoveSelectedTwoToneEncodeEntry(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.TwoToneEncodeEntries.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedTwoToneEncodeEntry is null ? -1 : viewModel.TwoToneEncodeEntries.IndexOf(viewModel.SelectedTwoToneEncodeEntry);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.TwoToneEncodeEntries.Count - 1);
        viewModel.SelectedTwoToneEncodeEntry = viewModel.TwoToneEncodeEntries[nextIndex];
        ShowTwoToneEncodeEntryEditor();
    }

    private void MoveSelectedTwoToneDecodeEntry(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.TwoToneDecodeEntries.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedTwoToneDecodeEntry is null ? -1 : viewModel.TwoToneDecodeEntries.IndexOf(viewModel.SelectedTwoToneDecodeEntry);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.TwoToneDecodeEntries.Count - 1);
        viewModel.SelectedTwoToneDecodeEntry = viewModel.TwoToneDecodeEntries[nextIndex];
        ShowTwoToneDecodeEntryEditor();
    }

    private void MoveSelectedAprsReceiveFilter(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.AprsReceiveFilters.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAprsReceiveFilter is null ? -1 : viewModel.AprsReceiveFilters.IndexOf(viewModel.SelectedAprsReceiveFilter);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.AprsReceiveFilters.Count - 1);
        viewModel.SelectedAprsReceiveFilter = viewModel.AprsReceiveFilters[nextIndex];
        ShowAprsReceiveFilterEditor();
    }

    // Pages through VisibleEncryptionKeys/VisibleArc4EncryptionKeys/
    // VisibleAesEncryptionKeys (occupied slots only) - not the full
    // always-32/34/255 collection, matching what's actually on screen.
    private void MoveSelectedDigitalEncryptionKey(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.VisibleEncryptionKeys.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedEncryptionKey is null ? -1 : viewModel.VisibleEncryptionKeys.IndexOf(viewModel.SelectedEncryptionKey);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.VisibleEncryptionKeys.Count - 1);
        viewModel.SelectedEncryptionKey = viewModel.VisibleEncryptionKeys[nextIndex];
        ShowDigitalKeyEditor();
    }

    private void MoveSelectedArc4EncryptionKey(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.VisibleArc4EncryptionKeys.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedArc4EncryptionKey is null ? -1 : viewModel.VisibleArc4EncryptionKeys.IndexOf(viewModel.SelectedArc4EncryptionKey);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.VisibleArc4EncryptionKeys.Count - 1);
        viewModel.SelectedArc4EncryptionKey = viewModel.VisibleArc4EncryptionKeys[nextIndex];
        ShowArc4KeyEditor();
    }

    private void MoveSelectedAesEncryptionKey(int offset)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.VisibleAesEncryptionKeys.Count == 0)
        {
            return;
        }

        var currentIndex = viewModel.SelectedAesEncryptionKey is null ? -1 : viewModel.VisibleAesEncryptionKeys.IndexOf(viewModel.SelectedAesEncryptionKey);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, viewModel.VisibleAesEncryptionKeys.Count - 1);
        viewModel.SelectedAesEncryptionKey = viewModel.VisibleAesEncryptionKeys[nextIndex];
        ShowAesKeyEditor();
    }

    private void AvailableZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableZoneChannels(GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void ZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedZoneMembers(GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void AvailableScanListChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableScanListChannels(GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void ScanListMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedScanListMemberChannels(GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private static ChannelEntry[] GetSelectedChannels(IEnumerable? selectedItems)
    {
        return selectedItems?.OfType<ChannelEntry>().ToArray() ?? [];
    }

    private static AmAirEntry[] GetSelectedAmAirChannels(IEnumerable? selectedItems)
    {
        return selectedItems?.OfType<AmAirEntry>().ToArray() ?? [];
    }

    private static TalkgroupEntry[] GetSelectedTalkgroups(IEnumerable? selectedItems)
    {
        return selectedItems?.OfType<TalkgroupEntry>().ToArray() ?? [];
    }

    private static RoamingChannelEntry[] GetSelectedRoamingChannels(IEnumerable? selectedItems)
    {
        return selectedItems?.OfType<RoamingChannelEntry>().ToArray() ?? [];
    }

    private void AvailableRoamingZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableRoamingZoneChannels(GetSelectedRoamingChannels(listBox.SelectedItems));
        }
    }

    private void RoamingZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedRoamingZoneMembers(GetSelectedRoamingChannels(listBox.SelectedItems));
        }
    }

    private void AvailableReceiveGroupListTalkgroupsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableReceiveGroupListTalkgroups(GetSelectedTalkgroups(listBox.SelectedItems));
        }
    }

    private void ReceiveGroupListMemberTalkgroupsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedReceiveGroupListMemberTalkgroups(GetSelectedTalkgroups(listBox.SelectedItems));
        }
    }

    private void AvailableAmZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableAmZoneChannels(GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AmZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAmZoneMembers(GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AvailableAmZoneScanChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableAmZoneScanChannels(GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AmZoneScanChannelMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAmZoneScanChannelMembers(GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void InputElement_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox textBox)
        {
            return;
        }

        ScrollFocusedTextBoxIntoView(textBox);
        DispatcherTimer.RunOnce(() => ScrollFocusedTextBoxIntoView(textBox), TimeSpan.FromMilliseconds(120));
        DispatcherTimer.RunOnce(() => ScrollFocusedTextBoxIntoView(textBox), TimeSpan.FromMilliseconds(360));
    }

    private void InputPane_OnStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        UpdateInputPaneSpacer();
        ScrollFocusedTextBoxIntoView();
    }

    private void ScrollFocusedTextBoxIntoView(TextBox textBox)
    {
        if (!textBox.IsFocused)
        {
            return;
        }

        var topLeft = textBox.TranslatePoint(default, MainScrollViewer);
        if (topLeft is null)
        {
            return;
        }

        var viewportTop = 0d;
        var viewportBottom = MainScrollViewer.Viewport.Height;
        if (_inputPane?.State == InputPaneState.Open)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var scrollViewerTop = topLevel is null ? 0 : MainScrollViewer.TranslatePoint(default, topLevel)?.Y ?? 0;
            viewportBottom = Math.Min(viewportBottom, Math.Max(0, _inputPane.OccludedRect.Top - scrollViewerTop));
        }

        viewportBottom = Math.Max(viewportTop, viewportBottom - FocusRevealPadding);
        var controlTop = topLeft.Value.Y;
        var controlBottom = controlTop + textBox.Bounds.Height;
        var nextY = MainScrollViewer.Offset.Y;

        if (controlBottom > viewportBottom)
        {
            nextY += controlBottom - viewportBottom;
        }
        else if (controlTop < viewportTop + FocusRevealPadding)
        {
            nextY -= viewportTop + FocusRevealPadding - controlTop;
        }

        var maxY = Math.Max(0, MainScrollViewer.Extent.Height - MainScrollViewer.Viewport.Height);
        MainScrollViewer.Offset = MainScrollViewer.Offset.WithY(Math.Clamp(nextY, 0, maxY));
    }

    private void ScrollFocusedTextBoxIntoView()
    {
        var textBox = MainScrollViewer.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.IsFocused);
        if (textBox is not null)
        {
            ScrollFocusedTextBoxIntoView(textBox);
            DispatcherTimer.RunOnce(() => ScrollFocusedTextBoxIntoView(textBox), TimeSpan.FromMilliseconds(120));
        }
    }

    private void UpdateInputPaneSpacer()
    {
        if (_inputPane is null || _inputPane.State != InputPaneState.Open)
        {
            InputPaneSpacer.Height = 0;
            return;
        }

        var occludedRect = _inputPane.OccludedRect;
        InputPaneSpacer.Height = Math.Max(0, Bounds.Height - occludedRect.Top + InputPaneExtraReserve);
    }

    // Button in the 5Tone ID detail panel - Mobile's own double-tap gesture
    // (Desktop still has one, see MainView.axaml.cs) was dropped 2026-08-07
    // once the row became a summary-only list item instead of an inline
    // edit form (nothing left on the row itself to double-tap).
    private void FiveToneResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FiveToneIdEntry entry } && DataContext is MainViewModel viewModel)
        {
            viewModel.ResetFiveToneRowSpecialCallCommand.Execute(entry);
        }
    }

    private enum MobileEditorMode
    {
        None,
        Channel,
        Zone,
        RoamingChannel,
        RadioId,
        Talkgroup,
        ScanList,
        RoamingZone,
        ReceiveGroupList,
        AutoRepeaterOffset,
        AnalogAddress,
        GpsRoaming,
        TalkgroupWhitelist,
        DigitalContactWhitelist,
        PrefabricatedSms,
        AmAir,
        AmZone,
        FmChannel,
        AnalogQuickCall,
        StateInformation,
        HotKey,
        Qdc1200Id,
        QdcAddress,
        FiveToneId,
        TwoToneEncodeEntry,
        TwoToneDecodeEntry,
        AprsReceiveFilter,
        DigitalContact,
        DigitalKey,
        Arc4Key,
        AesKey
    }
}
