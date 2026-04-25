using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCasinoTweaks.Components;
using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace LethalCasinoTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("mrgrm7.LethalCasino")]
[BepInDependency("com.rune580.LethalCompanyInputUtils")]
public class LethalCasinoTweaks : BaseUnityPlugin
{
    public static LethalCasinoTweaks Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    
    public class LcCasinoTweaksInputActions : LcInputActions
    {
        [InputAction(KeyboardControl.U, Name = "Double Down")]
        public InputAction? DoubleDownKey { get; set; }
    }
    
    internal static LcCasinoTweaksInputActions? InputActions { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;
        InputActions = new LcCasinoTweaksInputActions();

        Patch();
        AttachNetworkBridgeToPrefab();
        AttachDoubleDownFeature();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }

    /**
     * Extend the Blackjack prefab so that our network bridge can send messages to clients.
     */
    internal static void AttachNetworkBridgeToPrefab()
    {
        var blackjackPrefab = LethalCasino.Plugin.Prefabs["Blackjack"];
        blackjackPrefab.AddComponent<BlackjackGameStateRpcBridge>();
    }

    /**
     * Add double down functionality to the parent prefab.
     */
    internal static void AttachDoubleDownFeature()
    {
        var blackjackPrefab = LethalCasino.Plugin.Prefabs["Blackjack"];
        blackjackPrefab.AddComponent<BlackjackDoubleDownFeature>();
    }
}
