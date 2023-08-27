using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TOHE.Patches;

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.ShowButtons))]
public class EndGameManagerPatch
{
    public static bool IsRestarting { get; private set; }
    private static readonly string _playAgainText = "Re-entering lobby in {0}s";
    private static readonly TextMeshPro autoPlayAgainText;

    public static void Postfix(EndGameManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        IsRestarting = false;

        new LateTask(() =>
        {
            Logger.Msg("Beginning Auto Play Again Countdown!", "AutoPlayAgain");
            IsRestarting = true;
            BeginAutoPlayAgainCountdown(__instance, WaitForSeconds(1));
        }, 0.5f, "Auto Play Again");
    }

    private static int WaitForSeconds(int v)
    {
        throw new NotImplementedException();
    }

    public static void CancelPlayAgain()
    {
        IsRestarting = false;
        if (autoPlayAgainText != null) autoPlayAgainText.gameObject.SetActive(false);
    }

    private static void BeginAutoPlayAgainCountdown(EndGameManager endGameManager, int seconds)
    {
        if (!IsRestarting) return;
        if (endGameManager == null) return;
        EndGameNavigation navigation = endGameManager.Navigation;
        if (navigation == null) return;

        if (autoPlayAgainText == null)
        {
        }

        if (seconds == 0) navigation.NextGame();
        else new LateTask(() => BeginAutoPlayAgainCountdown(endGameManager, seconds - 1), 1.1f);
    }
}