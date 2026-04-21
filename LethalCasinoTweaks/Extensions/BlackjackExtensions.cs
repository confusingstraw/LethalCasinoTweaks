using System.Linq;
using GameNetcodeStuff;
using LethalCasino.Custom;
using UnityEngine;

namespace LethalCasinoTweaks.Extensions;

public static class BlackjackExtensions
{
    /**
     * Utility method to allow setting the deck position from outside the instance.
     */
    public static void SetCardDeckLocalYPosition(this Blackjack instance, float yPos)
    {
        var cardDeck = instance.transform.Find("CardDeck");

        if (cardDeck != null)
        {
            var localPos = cardDeck.localPosition;
            cardDeck.localPosition = new Vector3(localPos.x, yPos, localPos.z);
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find CardDeck transform");                
        }
    }

    /**
     * Implements double down logic
     */
    public static bool CanDoubleDown(this Blackjack instance, PlayerControllerB playerController, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("Calling CanDoubleDown");
        
        var heldItem = playerController.ItemSlots[playerController.currentItemSlot];
        if (!heldItem)
        {
            return false;
        }

        var scrapToBet = heldItem.GetComponent<CustomScrapController>();
        if (!scrapToBet)
        {
            return false;
        }

        var currentHand = instance.playerCards[playerIdx];
        var currentBet = instance.gambledScrap[playerIdx]
            .Sum(scrap => scrap.GetComponent<CustomScrapController>()?.originalScrapValue ?? 0);

        return currentHand.Count != 2 || scrapToBet.originalScrapValue > currentBet;
    }

    /**
     * Utility method wrap `gameInProgress` checks with double down logic.
     *
     * TODO: implement statefulness around tracking existing double-downs
     */
    public static bool IsUnableToPlaceBet(this Blackjack instance, PlayerControllerB playerController, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("Calling CanAttemptToPlaceBet");

        if (instance.gameInProgress)
        {
            return !instance.CanDoubleDown(playerController, playerIdx);
        }

        return instance.gameInProgress;
    }
}
