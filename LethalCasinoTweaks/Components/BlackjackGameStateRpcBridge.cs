using LethalCasino.Custom;
using LethalCasinoTweaks.Extensions;
using LethalCasinoTweaks.Patches;
using Unity.Netcode;

namespace LethalCasinoTweaks.Components;

public class BlackjackGameStateRpcBridge : BlackjackComponent
{
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
