using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using LethalCasino.Custom;
using LethalCasinoTweaks.Components;
using Unity.Netcode;
using UnityEngine;

namespace LethalCasinoTweaks.Patches;

[HarmonyPatch(typeof(Blackjack))]
public class BlackjackPatch
{
    public static readonly int FullDeckSize = 52;
    public static readonly float FDeckCardYOffset = .001f;
    public static float FInitialDeckLocalYPos = .9462f;
    public static float FEmptyDeckLocalYPos = FInitialDeckLocalYPos - (FDeckCardYOffset * FullDeckSize);

    public static List<Card>? InstanceCardsInPlay = null;

    /**
     * When an instance of the prefab is loaded, we store some initial values for later use. 
     *
     * This implicitly requires the prefab to be a singleton, else we can get conflicting
     * values if they are instanced in multiple places on the same map.
     */
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
    
    /// Start ShuffleDeck Patches

    /**
     * Here we patch `ShuffleDeck` so that, before it actually applies the shuffle,
     * we filter out any cards currently in play.
     *
     * We can't use a `ref` param because of `IEnumerable` shenanigans, so we make a copy
     * and use methods to mutate `ts` in place.
     */
    [HarmonyPatch("ShuffleDeck")]
    [HarmonyPrefix]
    public static void ShuffleDeckPrefix(List<Card> ts)
    {
        if (InstanceCardsInPlay == null)
        {
            LethalCasinoTweaks.Logger.LogDebug("Skipping shuffle deck behavior, InstanceCardsInPlay is null");
            return;
        }
        
        LethalCasinoTweaks.Logger.LogDebug($"Dropping {InstanceCardsInPlay.Count} in-play cards from new deck");

        var cardsToIncludeInShuffle = new List<Card>();

        // we modify both lists in this loop so that if we support multiple decks in the future
        // then we only need to modify `CreateDeckPrefix`
        foreach (var card in ts)
        {
            var inPlayIdx = InstanceCardsInPlay.FindIndex(x => x.suit == card.suit && x.face == card.face);
            if (inPlayIdx != -1)
            {
                InstanceCardsInPlay.RemoveAt(inPlayIdx);
            }
            else
            {
                cardsToIncludeInShuffle.Add(card);
            }
        }

        ts.Clear();
        ts.AddRange(cardsToIncludeInShuffle);
    }
    
    /// End ShuffleDeck Patches

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

        LethalCasinoTweaks.Logger.LogDebug("Storing cards in play before creating/shuffling deck");
        __state = true;

        InstanceCardsInPlay = __instance.playerCards.SelectMany(c => c).ToList();

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
        var bridge = __instance.GetComponentInParent<BlackjackGameStateRpcBridge>();
        if (bridge == null)
        {
            LethalCasinoTweaks.Logger.LogDebug("Failed to find RPC bridge");
            return;
        }

        if (__state)
        {
            LethalCasinoTweaks.Logger.LogDebug("Playing shuffle sound for all clients");
            bridge.OnShuffleClientRpc();
        }
        else
        {
            LethalCasinoTweaks.Logger.LogDebug("Didn't create a deck, skipping shuffle sound");
        }

        InstanceCardsInPlay = null; // clear out the state used by the shuffler
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
     * Notify clients when a card is dealt so that their deck position can reflect it.
     */
    [HarmonyPatch("DealCard")]
    [HarmonyPostfix]
    private static void DealCardPostfix(Blackjack __instance)
    {
        var bridge = __instance.GetComponentInParent<BlackjackGameStateRpcBridge>();
        if (bridge == null)
        {
            LethalCasinoTweaks.Logger.LogDebug("Failed to find RPC bridge");
            return;
        }
        bridge.UpdateDeckCountClientRpc(__instance.deck.Count);
    }

    /// End DealCard Patches
    
    /// Start ResetGameState Patches
    
    /**
     * Ensure we reset the double down state when the game finishes.
     */
    [HarmonyPatch("ResetGameState")]
    [HarmonyPostfix]
    private static void ResetGameStatePostfix(Blackjack __instance)
    {
        var doubleDownFeature = __instance.GetComponentInParent<BlackjackDoubleDownFeature>();
        if (doubleDownFeature == null)
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find DoubleDownFeature");
            return;
        }
        doubleDownFeature.ResetDoubleDownState();
    }

    /// End ResetGameState Patches

    /// Start Update Patches
    
    /**
     * Ensure we reset the double down state when the game finishes.
     */
    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    private static void UpdatePrefix(Blackjack __instance)
    {
        var doubleDownFeature = __instance.GetComponentInParent<BlackjackDoubleDownFeature>();
        if (doubleDownFeature == null)
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find DoubleDownFeature");
            return;
        }

        doubleDownFeature.ApplyFeature();
    }

    /// End Update Patches

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
