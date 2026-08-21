using System.Collections.Generic;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// One entry in <see cref="MainViewModel.NavigationTree"/>. Category nodes
/// (e.g. "Common", "DMR") have <see cref="Children"/> and no
/// <see cref="TabIndex"/>; leaf nodes have a <see cref="TabIndex"/> selecting
/// which top-level content section to show - every entity gets its own
/// independent value, same as Channels/Zones.
/// </summary>
public sealed record NavigationTreeNode(
    string Title,
    int? TabIndex = null,
    IReadOnlyList<NavigationTreeNode>? Children = null,
    int? SubTabIndex = null)
{
    public IReadOnlyList<NavigationTreeNode> Children { get; init; } = Children ?? [];

    // Bool-typed for direct XAML IsVisible binding (MobileMainView.axaml's
    // nav flyout, which renders this tree as Expanders/buttons instead of a
    // TreeView) - Avalonia bindings can't express "Children.Count > 0"
    // inline without a converter.
    public bool HasChildren => Children.Count > 0;
    public bool IsLeaf => TabIndex is not null;

    // Hides a leaf entirely (e.g. Dev Options - not meant for a public
    // build) - default true so every existing node stays visible.
    public bool IsVisible { get; init; } = true;

    // Greys out a leaf and blocks navigation to it (see
    // MainViewModel.OnSelectedNavigationNodeChanged) without hiding it -
    // for a feature that's visibly on the roadmap but not usable yet (e.g.
    // Imports/Exports while CSV support is disabled), matching how a
    // disabled vendor CPS field gets a tooltip instead of being deleted.
    public bool IsEnabled { get; init; } = true;
    public string? DisabledReason { get; init; }
}
