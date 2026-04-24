using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalCasinoTweaks.Components;
using UnityEngine;

namespace LethalCasinoTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("mrgrm7.LethalCasino")]
public class LethalCasinoTweaks : BaseUnityPlugin
{
    public static LethalCasinoTweaks Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    
    public static ConfigEntry<KeyboardShortcut>? DoubleDownKey { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        DoubleDownKey = Config.Bind("Controls", "DoubleDownKey", new KeyboardShortcut(KeyCode.U, Array.Empty<KeyCode>()), "Key to perform Double Down action in Blackjack");
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
