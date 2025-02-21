using BepInEx.Logging;
using HarmonyLib;
using LethalCasino.Custom;

namespace LethalCasinoTweaks.Patches;

[HarmonyPatch(typeof(Blackjack))]
public class BlackjackPatch
{
    /**
     * `CreateDeck` is what actually restores the playing deck
     * to its initial state. We hook into it and only allow
     * the call to go through if we deem it necessary.
     */
    [HarmonyPatch("CreateDeck")]
    [HarmonyPrefix]
    private static bool CreateDeckPrefix(Blackjack __instance)
    {
        if (!ShouldCreateDeck(__instance))
        {
            LethalCasinoTweaks.Logger.LogDebug("Skipping create deck while non-empty");
            return false;
        }

        LethalCasinoTweaks.Logger.LogDebug("Creating deck");
        return true;
    }
    
    /**
     * Because we lazily re-create the deck, we need to make sure
     * that it is in a good state any time the underlying class
     * tries to use it. `DealCard` is the main place where that happens
     * aside from `CreateDeck`.
     */
    [HarmonyPatch("DealCard")]
    [HarmonyPrefix]
    private static bool DealCardPrefix(Blackjack __instance)
    {
        if (ShouldCreateDeck(__instance))
        {
            LethalCasinoTweaks.Logger.LogDebug("Reshuffling empty deck before dealing");
            __instance.CreateDeck();
        }

        return true;
    }

    /**
     * If the deck hasn't yet been constructed or if it is empty
     * we will allow it to be recreated, else skip.
     */
    private static bool ShouldCreateDeck(Blackjack instance)
    {
        return instance.deck == null || instance.deck.Count == 0;
    }
}