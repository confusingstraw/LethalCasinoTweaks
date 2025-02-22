using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LethalCasino.Custom;
using UnityEngine;

namespace LethalCasinoTweaks.Patches;

[HarmonyPatch(typeof(Blackjack))]
public class BlackjackPatch
{
    /// Start CreateDeck Patches

    /**
     * `CreateDeck` is what actually restores the playing deck
     * to its initial state. We hook into it and only allow
     * the call to go through if we deem it necessary.
     *
     * The patch state is used to track whether we created a deck or not.
     */
    [HarmonyPatch("CreateDeck")]
    [HarmonyPrefix]
    private static bool CreateDeckPrefix(Blackjack __instance, out bool __state)
    {
        if (!ShouldCreateDeck(__instance))
        {
            LethalCasinoTweaks.Logger.LogDebug("Skipping create deck while non-empty");
            __state = false;
            return false;
        }

        LethalCasinoTweaks.Logger.LogDebug("Creating/shuffling deck");
        __state = true;
        return true;
    }
    
    /**
     * Here we check if the deck actually got created/shuffled and play the
     * sound across all clients if it did.
     */
    [HarmonyPatch("CreateDeck")]
    [HarmonyPostfix]
    private static void CreateDeckPostfix(Blackjack __instance, bool __state)
    {
        if (__state)
        {
            LethalCasinoTweaks.Logger.LogDebug("Playing shuffle sound for all clients");
            __instance.PlaySoundClientRpc("ShuffleDeck", 1f);   
        }
        else
        {
            LethalCasinoTweaks.Logger.LogDebug("Didn't create a deck, skipping shuffle sound");
        }
    }
    
    /// End CreateDeck Patches
    
    /// Start DealCard Patches
    
    /**
     * Because we lazily re-create the deck, we need to make sure
     * that it is in a good state any time the underlying class
     * tries to use it. `DealCard` is the main place where that happens
     * aside from `CreateDeck`.
     */
    [HarmonyPatch("DealCard")]
    [HarmonyPrefix]
    private static void DealCardPrefix(Blackjack __instance)
    {
        if (ShouldCreateDeck(__instance))
        {
            LethalCasinoTweaks.Logger.LogDebug("Reshuffling empty deck before dealing");
            __instance.CreateDeck();
        }
    }

    /// End DealCard Patches
    
    /// Utilities

    /**
     * If the deck hasn't yet been constructed or if it is empty
     * we will allow it to be recreated, else skip.
     */
    private static bool ShouldCreateDeck(Blackjack instance)
    {
        return instance.deck == null || instance.deck.Count == 0;
    }
}

[HarmonyPatch(typeof(Blackjack), "StartGameClientRpc")]
public static class BlackjackStartGameClientRpcPatch
{
    private static readonly MethodInfo MPlayOneShot =
        AccessTools.Method(typeof(AudioSource), "PlayOneShot", [typeof(AudioClip), typeof(float)]);

    private static readonly FieldInfo FAudioSource =
        AccessTools.Field(typeof(Blackjack), "audioSource");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        LethalCasinoTweaks.Logger.LogDebug("Generating reverse patch code");
        var list = instructions.ToList();
        
        var startIndex = -1;
        var endIndex = -1;
            
        for (var i = 0; i < list.Count; i++)
        {
            if (startIndex == -1)
            {
                var nextIndex = i + 1;
                if (
                    list[i].opcode == OpCodes.Ldarg_0 &&
                    nextIndex < list.Count &&
                    list[nextIndex].opcode == OpCodes.Ldfld &&
                    (FieldInfo)list[nextIndex].operand == FAudioSource)
                {
                    startIndex = i;
                }
            } else 
            {
                if (list[i].opcode == OpCodes.Callvirt && (MethodInfo)list[i].operand == MPlayOneShot)
                {
                    endIndex = i;
                }
            }
        }
            
        if (startIndex > -1 && endIndex > -1)
        {
            LethalCasinoTweaks.Logger.LogDebug($"Found instructions to patch, removing: [{startIndex}, {endIndex}]");
            list.RemoveRange(startIndex, (endIndex - startIndex) + 1);
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning($"Failed to find audioSource call to patch: [{startIndex}, {endIndex}]");
        }
            
        return list.AsEnumerable();
    }
}
