using HarmonyLib;
using LethalCasino.Custom;

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