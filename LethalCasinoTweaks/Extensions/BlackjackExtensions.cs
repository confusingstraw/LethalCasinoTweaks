using System.Linq;
using GameNetcodeStuff;
using LethalCasino.Custom;
using LethalCasinoTweaks.Components;
using Unity.Netcode;
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
            LethalCasinoTweaks.Logger.LogWarning("[BlackjackExtensions] Failed to find CardDeck transform");                
        }
    }

    /**
     * Utility method wrap `gameInProgress` checks with double down logic.
     */
    public static bool IsUnableToPlaceBet(this Blackjack instance, NetworkBehaviourReference playerRef, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("[BlackjackExtensions] Calling CanAttemptToPlaceBet");
        if (!playerRef.TryGet<PlayerControllerB>(out var playerController))
        {
            LethalCasinoTweaks.Logger.LogWarning("[BlackjackExtensions] Failed to extract player controller");
            return instance.gameInProgress;
        }

        if (instance.gameInProgress)
        {
            var doubleDownFeature = instance.GetComponentInParent<BlackjackDoubleDownFeature>();
            return !doubleDownFeature.ShouldAllowGameInProgressBet(playerController, playerIdx);
        }

        return instance.gameInProgress;
    }

    public static void ServerSuccessfullyPlacedBet(this Blackjack instance, NetworkBehaviourReference playerRef, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogInfo("[BlackjackExtensions] Calling ServerSuccessfullyPlacedBet");
        var doubleDownFeature = instance.GetComponentInParent<BlackjackDoubleDownFeature>();
        if (doubleDownFeature)
        {
            doubleDownFeature.ServerPostDoubleDownSuccess(instance, playerRef, playerIdx);
        }
    }
}
