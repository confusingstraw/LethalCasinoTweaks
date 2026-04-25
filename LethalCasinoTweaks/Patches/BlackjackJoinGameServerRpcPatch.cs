using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LethalCasino.Custom;
using LethalCasinoTweaks.Components;
using LethalCasinoTweaks.Extensions;
using Unity.Netcode;

namespace LethalCasinoTweaks.Patches;

/**
 * Patches the `JoinGameServerRpc` method so that we can allow mid-game bets (supports doubling down).
 */
[HarmonyPatch(typeof(Blackjack), "JoinGameServerRpc")]
public class BlackjackJoinGameServerRpcPatch
{
    private static readonly FieldInfo FRpcExecStage =
        AccessTools.Field(typeof(NetworkBehaviour), "__rpc_exec_stage");
    
    private static readonly FieldInfo FGameInProgressField =
        AccessTools.Field(typeof(Blackjack), "gameInProgress");

    private static readonly MethodInfo MIsUnableToPlaceBet =
        AccessTools.Method(typeof(BlackjackExtensions), "IsUnableToPlaceBet");

    private static readonly MethodInfo MServerSuccessfullyPlacedBet =
        AccessTools.Method(typeof(BlackjackExtensions), "ServerSuccessfullyPlacedBet");

    public static int? LastOriginalPlayerIdx;

    /**
     * Modifies `JoinGameServerRpc` so the `gameInProgress` field check instead become a call to our
     * `IsUnableToPlaceBet` extension method, where we wire in some double down logic.
     *
     * It also adds a call to our `ServerSuccessfullyPlacedBet` extension method at the end of the function,
     * when we know a bet was allowed.
     */
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        LethalCasinoTweaks.Logger.LogDebug("[JoinGameServerRpc] Generating reverse patch code");

        var successfullyReplaced = false;

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldfld && (instruction.operand is FieldInfo opField) && opField == FGameInProgressField)
            {
                LethalCasinoTweaks.Logger.LogDebug("[JoinGameServerRpc] Successfully patched gameInProgress field");
                /*
                 * The original IL instructions were:
                 * ldarg.0
                 * ldfld gameInProgress
                 *
                 * This was to load the `gameInProgress` field for performing a conditional jump.
                 * We are replacing it with a method call, so we leave the `ldarg.0` in-place, since
                 * we need that for the purposes of calling an instance method.
                 */
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Call, MIsUnableToPlaceBet);
                successfullyReplaced = true;
            }
            else if (instruction.opcode == OpCodes.Ret)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Call, MServerSuccessfullyPlacedBet);
                yield return instruction;
            }
            else
            {
                yield return instruction;
            }
        }

        if (!successfullyReplaced)
        {
            LethalCasinoTweaks.Logger.LogWarning("[JoinGameServerRpc] Failed to generate reverse patch code");
        }
    }

    /**
     * Track the original offset playerIdx in state, then adjust the parameter to the "real" value
     * so that `JoinGameServerRpc` can run normally.
     */
    [HarmonyPrefix]
    private static void JoinGameServerRpcPrefix(out int __state, Blackjack __instance, NetworkBehaviourReference playerRef, ref int playerIdx)
    {
        // we track the RPC state so that we only run our logic when the code is executing as the server
        __state = (int)FRpcExecStage.GetValue(__instance);
        if (!__instance.IsServer || __state != 1) return;

        LethalCasinoTweaks.Logger.LogWarning($"[JoinGameServerRpc] Triggering prefix: {playerIdx}");
        
        LastOriginalPlayerIdx = playerIdx;

        if (playerIdx < 0)
        {
            playerIdx += BlackjackDoubleDownFeature.MagicPlayerIndexOffset;
            LethalCasinoTweaks.Logger.LogWarning($"[JoinGameServerRpc] Changed index from {LastOriginalPlayerIdx} to {playerIdx}");
        }
    }
    
    /**
     * Unset the tracked value.
     */
    [HarmonyPostfix]
    private static void JoinGameServerRpcPostfix(int __state, Blackjack __instance, NetworkBehaviourReference playerRef, int playerIdx)
    {
        if (!__instance.IsServer || __state != 1) return;

        LethalCasinoTweaks.Logger.LogWarning($"[JoinGameServerRpc] Triggering postfix {playerIdx}");
        LastOriginalPlayerIdx = null;
    }
}
