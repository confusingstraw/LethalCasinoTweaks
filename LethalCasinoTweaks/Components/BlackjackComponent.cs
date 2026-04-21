using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace LethalCasinoTweaks.Components;

public abstract class BlackjackComponent : NetworkBehaviour
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
}
