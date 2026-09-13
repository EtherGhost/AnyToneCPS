using System;

namespace AnyToneCPS.Services;

/// <summary>Signals when MainViewModel construction (including its own
/// synchronous validator warm-up, see MainViewModel.WarmUpValidatedModelTypes'
/// doc comment) has actually finished - Android's native splash screen
/// (MainActivity.cs) polls IsReady via SetKeepOnScreenCondition to stay up
/// for the whole delay, not just its own default ~1s icon animation, which
/// would otherwise dismiss well before construction is done and leave a
/// blank view on screen for the remainder.</summary>
public static class AppStartupSignal
{
    public static bool IsReady { get; private set; }

    public static void SignalReady()
    {
        IsReady = true;
    }
}
