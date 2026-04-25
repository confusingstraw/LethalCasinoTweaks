using System;
using System.Linq;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using LethalCasino;
using LethalCasino.Custom;
using LethalCasinoTweaks.Patches;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace LethalCasinoTweaks.Components;
public class BlackjackDoubleDownFeature : NetworkBehaviour
{
    public static readonly int MagicPlayerIndexOffset = 1000;

    private static readonly Regex PlayerHandPositionRegex = new("^Player([1-4])HandPosition$", RegexOptions.Compiled);

    private bool[] _doubleDownState = new bool[4];

    private void Awake()
    {
        LethalCasinoTweaks.Logger.LogDebug("[BlackjackDoubleDownFeature] Awake");

        if (LethalCasinoTweaks.InputActions?.DoubleDownKey != null)
        {
            LethalCasinoTweaks.InputActions.DoubleDownKey.performed += OnActivateDoubleDown;
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning("[BlackjackDoubleDownFeature] DoubleDown input action not ready");
        }

        UpdateKeyBindTooltipHint();
    }

    public void UpdateKeyBindTooltipHint()
    {
        var instance = GetComponentInParent<Blackjack>();
        if (instance == null)
        {
            LethalCasinoTweaks.Logger.LogDebug(
                "[BlackjackDoubleDownFeature] Failed to find blackjack sibling component");
            return;
        }

        var trigger = GetHitPrefabTrigger();
        if (trigger)
        {
            trigger!.hoverTip = FormatHoverTip();
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning("[BlackjackDoubleDownFeature] Failed to find hit prefab trigger");
        }
    }

    /**
     * Runs on key-press of the double down keybind
     */
    private void OnActivateDoubleDown(InputAction.CallbackContext ctx)
    {
        try {
            var localPlayerController = GameNetworkManager.Instance.localPlayerController;
            if (!localPlayerController)
            {
                LethalCasinoTweaks.Logger.LogWarning("[BlackjackDoubleDownFeature] Missing local player controller");
                return;
            }

            var instance = GetComponentInParent<Blackjack>();
            if (instance == null)
            {
                LethalCasinoTweaks.Logger.LogDebug(
                    "[BlackjackDoubleDownFeature] Failed to find blackjack sibling component");
                return;
            }

            var playerIdx = GetLocalPlayerIndex(instance, localPlayerController);
            if (playerIdx == -1)
            {
                LethalCasinoTweaks.Logger.LogDebug("No player index");
                return;
            }

            if (!IsHoveringBetControls(instance, localPlayerController))
            {
                LethalCasinoTweaks.Logger.LogDebug("Not hovering bet controls");
                return;
            }

            if (LethalCasinoTweaks.InputActions?.DoubleDownKey?.triggered == true)
            {
                DoubleDownServerRpc(localPlayerController, playerIdx);
            }
        } catch (Exception e) {
            LethalCasinoTweaks.Logger.LogError($"[BlackjackDoubleDownFeature] Error applying double down feature: {e}");
        }
    }

    public bool ShouldAllowGameInProgressBet(PlayerControllerB playerController, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("[BlackjackDoubleDownFeature] Calling ShouldAllowGameInProgressBet");

        if (IsMatchingMagicPlayerIndex(playerIdx) && !HasPlayerDoubledDown(playerIdx))
        {
            return true;
        }

        return false;
    }

    public bool HasPlayerDoubledDown(int playerIdx)
    {
        if (playerIdx is < 0 or > 3)
        {
            throw new ArgumentException("playerIdx must be between 0 and 3");
        }

        return _doubleDownState[playerIdx];
    }

    public void ResetDoubleDownState()
    {
        _doubleDownState = new bool[4];
    }
    
    /**
     * If we successfully triggered a double down on the server, apply the related gameplay behaviors.
     */
    public void ServerPostDoubleDownSuccess(Blackjack instance, NetworkBehaviourReference playerRef, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("[JoinGameSuccessfulClientRpcPatch] Calling ServerPostDoubleDownSuccess");
        
        if (!IsMatchingMagicPlayerIndex(playerIdx))
        {
            LethalCasinoTweaks.Logger.LogDebug($"[BlackjackDoubleDownFeature] Skipping non-double down success");
            return;
        }

        SetHasPlayerDoubledDown(playerIdx, true);
        instance.DestroyBetControlsClientRpc(playerIdx);
        instance.TakeTurnAsPlayerServerRpc(playerRef, playerIdx, "hit");
        instance.TakeTurnAsPlayerServerRpc(playerRef, playerIdx, "stand");
        instance.ShowWarningMessageClientRpc(playerRef, "You doubled down!", "", isWarning: false);
    }

    private void SetHasPlayerDoubledDown(int playerIdx, bool hasDoubledDown)
    {
        if (playerIdx is < 0 or > 3)
        {
            throw new ArgumentException("playerIdx must be between 0 and 3");
        }

        _doubleDownState[playerIdx] = hasDoubledDown;
    }

    [ServerRpc]
    private void DoubleDownServerRpc(NetworkBehaviourReference playerRef, int playerIdx)
    {
        LethalCasinoTweaks.Logger.LogDebug("[BlackjackDoubleDownFeature] Calling DoubleDownServerRpc");

        var instance = GetComponentInParent<Blackjack>();
        if (instance == null)
        {
            LethalCasinoTweaks.Logger.LogError("[BlackjackDoubleDownFeature] Failed to find blackjack sibling component");
            return;
        }
        if (!playerRef.TryGet<PlayerControllerB>(out var playerController))
        {
            LethalCasinoTweaks.Logger.LogError("[BlackjackDoubleDownFeature] Missing remote player controller");
            return;
        }
        if (HasPlayerDoubledDown(playerIdx))
        {
            instance.ShowWarningMessageClientRpc(playerRef, "Cannot double down", "You've already doubled down", isWarning: true);
            return;
        }

        var heldItem = playerController.ItemSlots[playerController.currentItemSlot];
        if (!heldItem)
        {
            instance.ShowWarningMessageClientRpc(playerRef, "Cannot double down", "You're not holding an item", isWarning: true);
            return;
        }

        var scrapToBet = heldItem.GetComponent<CustomScrapController>();
        if (!scrapToBet)
        {
            instance.ShowWarningMessageClientRpc(playerRef, "Cannot double down", "You're not holding an item", isWarning: true);
            return;
        }

        var currentBet = instance.gambledScrap[playerIdx]
            .Sum(scrap => scrap.GetComponent<CustomScrapController>()?.originalScrapValue ?? 0);

        if (heldItem.scrapValue > currentBet)
        {
            instance.ShowWarningMessageClientRpc(playerRef, "Cannot double down", "The extra bet must be of equal or lesser value than your wager", isWarning: true);
            return;
        }

        var cards = instance.playerCards[playerIdx];
        if (cards.Count != 2)
        {
            instance.ShowWarningMessageClientRpc(playerRef, "Cannot double down", "You must have exactly two cards", isWarning: true);
            return;
        }

        LethalCasinoTweaks.Logger.LogDebug("[BlackjackDoubleDownFeature] Submitting double down wager");
        instance.JoinGameServerRpc(new NetworkBehaviourReference(playerController), playerIdx - MagicPlayerIndexOffset);
    }
    
    /**
     * Checks whether the playerIdx matches an ongoing "double down" request. This is signaled
     * by the `LastOriginalPlayerIdx` being set to `playerIdx - MagicPlayerIndexOffset`.
     */
    private static bool IsMatchingMagicPlayerIndex(int playerIdx)
    {
        var lastPlayerIdx = BlackjackJoinGameServerRpcPatch.LastOriginalPlayerIdx;
        return lastPlayerIdx != null && (lastPlayerIdx == (playerIdx - MagicPlayerIndexOffset));
    }

    private static int GetLocalPlayerIndex(Blackjack instance, PlayerControllerB localPlayerController)
    {
        if (!localPlayerController || !localPlayerController.hoveringOverTrigger)
        {
            return -1;
        }
        
        var trigger = localPlayerController.hoveringOverTrigger.gameObject;
        var currentTransform = trigger.transform;

        while (currentTransform)
        {
            var match = PlayerHandPositionRegex.Match(currentTransform.name);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var handPosition))
            {
                return handPosition - 1;
            }
            currentTransform = currentTransform.parent;
        }

        return -1;
    }

    private static bool IsHoveringBetControls(Blackjack instance, PlayerControllerB localPlayerController)
    {
        if (!localPlayerController || !localPlayerController.hoveringOverTrigger)
        {
            return false;
        }
        
        var trigger = localPlayerController.hoveringOverTrigger.gameObject;

        return trigger.name.Contains("Hit");
    }

    private static InteractTrigger? GetHitPrefabTrigger()
    {
        var hitPrefab = Plugin.Prefabs["BlackjackBetControls"].transform.Find("Hit");
        return hitPrefab ? hitPrefab.gameObject.GetComponent<InteractTrigger>() : null;
    }

    private static (InputAction?, InputAction?) GetInputActions()
    {
        var doubleDownBinding = LethalCasinoTweaks.InputActions?.DoubleDownKey;
        var interactBinding = InputSystem.actions.FindAction("Interact");
        return (interactBinding, doubleDownBinding);
    }

    /**
     * Renders the double down keybind to a string and appends it to the default
     * prefab string.
     */
    private static string FormatHoverTip()
    {
        var doubleDownBindingDisplayName = "Not Bound";
        var interactBindingDisplayName = "Not Bound";
        
        var (interactAction, doubleDownAction) = GetInputActions();

        var interactBinding = interactAction?.bindings.First();
        if (interactBinding != null)
        {
            interactBindingDisplayName = interactBinding.Value.ToDisplayString();
        }
        
        var doubleDownBinding = doubleDownAction?.bindings.First();
        if (doubleDownBinding != null)
        {
            doubleDownBindingDisplayName = doubleDownBinding.Value.ToDisplayString();
        }

        return $"Hit : [{interactBindingDisplayName}]\nDouble Down : [{doubleDownBindingDisplayName}]";
    }
}
