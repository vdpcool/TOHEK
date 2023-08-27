using HarmonyLib;
using UnityEngine;

namespace TOHE.Patches;

class CopyGameCode
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public class GameStartManagerStartPatch
    {
        public static void Postfix(GameStartManager __instance)
        {
            if (Main.AutoCopyGameCode.Value)
            {
                string code = InnerNet.GameCode.IntToGameName(AmongUsClient.Instance.GameId);
                GUIUtility.systemCopyBuffer = code;
            }
        }
    }
}