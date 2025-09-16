using System.Reflection;
using LethalCasino.Custom;
using LethalCasinoTweaks.Patches;
using Unity.Netcode;
using UnityEngine;

namespace LethalCasinoTweaks.Components;

public class BlackjackGameStateRpcBridge : NetworkBehaviour
{
    /**
     * Attach networking behaviors from netcode patcher.
     */
    private void Awake()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }

    /**
     * When the count of cards in the deck changes, update the deck position
     */
    [ClientRpc]
    public void UpdateDeckCountClientRpc(int count)
    {
        var instance = GetComponentInParent<Blackjack>();
        if (instance == null)
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find blackjack sibling component");
            return;
        }

        float nextYPos;

        if (count == 0)
        {
            LethalCasinoTweaks.Logger.LogDebug("Adjusting CardDeck transform: deck is empty, resetting position");
            nextYPos = BlackjackPatch.FInitialDeckLocalYPos;
        }
        else
        {
            LethalCasinoTweaks.Logger.LogDebug($"Adjusting CardDeck transform: {count}");
            nextYPos = BlackjackPatch.FEmptyDeckLocalYPos + (count * BlackjackPatch.FDeckCardYOffset);
        }
        
        LethalCasinoTweaks.Logger.LogDebug("Adjusting CardDeck transform for shuffle");
        instance.SetCardDeckLocalYPosition(nextYPos);
    }
    
    /**
     * When shuffling the deck reset its position and play a sound
     */
    [ClientRpc]
    public void OnShuffleClientRpc()
    {
        var instance = GetComponentInParent<Blackjack>();
        if (instance == null)
        {
            LethalCasinoTweaks.Logger.LogWarning("Failed to find blackjack sibling component");
            return;
        }

        LethalCasinoTweaks.Logger.LogDebug("Adjusting CardDeck transform for shuffle");
        instance.SetCardDeckLocalYPosition(BlackjackPatch.FInitialDeckLocalYPos);
        instance.audioSource.PlayOneShot(LethalCasino.Plugin.Sounds["ShuffleDeck"], 1f);
    }
}

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
}
