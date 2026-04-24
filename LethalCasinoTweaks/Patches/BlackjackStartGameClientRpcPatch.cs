using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LethalCasino.Custom;
using UnityEngine;

namespace LethalCasinoTweaks.Patches;

/**
 * Patches the `StartGameClientRpc` method so that it no longer plays the shuffle sound.
 */
[HarmonyPatch(typeof(Blackjack), "StartGameClientRpc")]
public static class BlackjackStartGameClientRpcPatch
{
    private static readonly MethodInfo MPlayOneShot =
        AccessTools.Method(typeof(AudioSource), "PlayOneShot", [typeof(AudioClip), typeof(float)]);

    private static readonly FieldInfo FAudioSource =
        AccessTools.Field(typeof(Blackjack), "audioSource");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        LethalCasinoTweaks.Logger.LogDebug("[StartGameClientRpc] Generating reverse patch code");
        var list = instructions.ToList();
        
        var startIndex = -1;
        var endIndex = -1;
            
        for (var i = 0; i < list.Count; i++)
        {
            if (startIndex == -1)
            {
                var nextIndex = i + 1;
                if (
                    list[i].opcode == OpCodes.Ldarg_0 &&
                    nextIndex < list.Count &&
                    list[nextIndex].opcode == OpCodes.Ldfld &&
                    (FieldInfo)list[nextIndex].operand == FAudioSource)
                {
                    startIndex = i;
                }
            } else 
            {
                if (list[i].opcode == OpCodes.Callvirt && (MethodInfo)list[i].operand == MPlayOneShot)
                {
                    endIndex = i;
                }
            }
        }

        if (startIndex > -1 && endIndex > -1)
        {
            LethalCasinoTweaks.Logger.LogDebug($"[StartGameClientRpc] Found instructions to patch, removing: [{startIndex}, {endIndex}]");
            list.RemoveRange(startIndex, (endIndex - startIndex) + 1);
        }
        else
        {
            LethalCasinoTweaks.Logger.LogWarning($"[StartGameClientRpc] Failed to find audioSource call to patch: [{startIndex}, {endIndex}]");
        }
            
        return list.AsEnumerable();
    }
}
