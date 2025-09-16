using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCasinoTweaks.Components;
using LobbyCompatibility.Attributes;
using LobbyCompatibility.Enums;

namespace LethalCasinoTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("mrgrm7.LethalCasino", BepInDependency.DependencyFlags.HardDependency)]
[LobbyCompatibility(CompatibilityLevel.Everyone, VersionStrictness.None)]
public class LethalCasinoTweaks : BaseUnityPlugin
{
    public static LethalCasinoTweaks Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();
        AttachNetworkBridgeToPrefab();

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
}