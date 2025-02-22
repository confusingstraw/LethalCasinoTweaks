using HarmonyLib;
using LethalCasino.Custom;
using UnityEngine;
using UnityEngine.UIElements.Collections;

namespace LethalCasinoTweaks.Patches;

[HarmonyPatch(typeof(Blackjack))]
public class BlackjackPatch
{
    public static readonly int FullDeckSize = 52;
    public static readonly float FDeckCardYOffset = .001f;
    public static float FInitialDeckLocalYPos = .9462f;
    public static float FEmptyDeckLocalYPos = FInitialDeckLocalYPos - (FDeckCardYOffset * FullDeckSize);

    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void StartPostfix(Blackjack __instance)
    {
        var cardDeck = GetCardDeckTransform(__instance);
        if (cardDeck != null)
        {
            FInitialDeckLocalYPos = cardDeck.transform.localPosition.y;
            FEmptyDeckLocalYPos = FInitialDeckLocalYPos - (FDeckCardYOffset * FullDeckSize);
        }
        else
        {
            LethalCasinoTweaks.Logger.LogDebug("Failed to find card deck on start, using default position");
        }
    }

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
    
    /**
    * Here we adjust the local Y position of the deck so that it appears like a card was dealt.
    * This is so that the height of the deck corresponds to the number of cards remaining.
    */
    [HarmonyPatch("DealCardClientRpc")]
    [HarmonyPrefix]
    private static void DealCardClientRpcPrefix(Blackjack __instance)
    {
        var cardDeck = GetCardDeckTransform(__instance);

        if (cardDeck != null)
        {
            var localPos = cardDeck.localPosition;
            float nextYPos;

            if (__instance.deck == null)
            {
                LethalCasinoTweaks.Logger.LogDebug("Adjusting CardDeck transform: deck is null on this client");
                nextYPos = FInitialDeckLocalYPos;
            }
            else
            {
                LethalCasinoTweaks.Logger.LogDebug($"Adjusting CardDeck transform: {__instance.deck.Count}");
                nextYPos = FEmptyDeckLocalYPos + (__instance.deck.Count * FDeckCardYOffset);
            }

            cardDeck.localPosition = new Vector3(localPos.x, nextYPos, localPos.z);
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find CardDeck transform");                
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
    
    /**
     * Retrieve the CardDeck transform from within the Blackjack prefab instance
     */
    private static Transform? GetCardDeckTransform(Blackjack instance)
    {
        return instance.transform.Find("CardDeck");
    }
}