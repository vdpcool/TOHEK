using HarmonyLib;
using TOHE.Patches;
using UnityEngine;

namespace TOHE;

//��Դ��https://github.com/tukasa0001/TownOfHost/pull/1265
[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class OptionsMenuBehaviourStartPatch
{
    private static ClientOptionItem UnlockFPS;
    private static ClientOptionItem AutoStart;
    private static ClientOptionItem AutoPlayAgain;
    private static ClientOptionItem ForceOwnLanguage;
    private static ClientOptionItem ForceOwnLanguageRoleName;
    private static ClientOptionItem EnableCustomButton;
    private static ClientOptionItem EnableCustomSoundEffect;
    private static ClientOptionItem SwitchVanilla;
    private static ClientOptionItem DumpLog;
    private static ClientOptionItem CanPublic;
    private static ClientOptionItem ModeForSmallScreen;
    private static ClientOptionItem ShowLobbyCode;
    //private static ClientOptionItem VersionCheat;
    //private static ClientOptionItem GodMode;

    public static void Postfix(OptionsMenuBehaviour __instance)
    {
        if (__instance.DisableMouseMovement == null) return;

        Main.SwitchVanilla.Value = false;
        if (Main.ResetOptions || !DebugModeManager.AmDebugger)
        {
            Main.ResetOptions = false;
            Main.VersionCheat.Value = false;
            Main.GodMode.Value = false;
        }

        if (UnlockFPS == null || UnlockFPS.ToggleButton == null)
        {
            UnlockFPS = ClientOptionItem.Create("UnlockFPS", Main.UnlockFPS, __instance, UnlockFPSButtonToggle);
            static void UnlockFPSButtonToggle()
            {
                Application.targetFrameRate = Main.UnlockFPS.Value ? 144 : 60;
                Logger.SendInGame(string.Format(Translator.GetString("FPSSetTo"), Application.targetFrameRate));
            }
        }
        if (AutoStart == null || AutoStart.ToggleButton == null)
        {
            AutoStart = ClientOptionItem.Create("AutoStart", Main.AutoStart, __instance, AutoStartButtonToggle);
            static void AutoStartButtonToggle()
            {
                if (Main.AutoStart.Value == false && GameStates.IsCountDown)
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(Translator.GetString("CancelStartCountDown"));
                }
            }
        }
        if (AutoPlayAgain == null || AutoPlayAgain.ToggleButton == null)
        {
            AutoPlayAgain = ClientOptionItem.Create("AutoPlayAgain", Main.AutoPlayAgain, __instance, AutoPlayAgainButtonToggle);
            static void AutoPlayAgainButtonToggle()
            {
                if (Main.AutoPlayAgain.Value == false)
                {
                    Options.AutoPlayAgainCountdown.GetBool();
                }
            }
        }
        if (ForceOwnLanguage == null || ForceOwnLanguage.ToggleButton == null)
        {
            ForceOwnLanguage = ClientOptionItem.Create("ForceOwnLanguage", Main.ForceOwnLanguage, __instance);
        }
        if (ForceOwnLanguageRoleName == null || ForceOwnLanguageRoleName.ToggleButton == null)
        {
            ForceOwnLanguageRoleName = ClientOptionItem.Create("ForceOwnLanguageRoleName", Main.ForceOwnLanguageRoleName, __instance);
        }
        if (EnableCustomButton == null || EnableCustomButton.ToggleButton == null)
        {
            EnableCustomButton = ClientOptionItem.Create("EnableCustomButton", Main.EnableCustomButton, __instance);
        }
        if (EnableCustomSoundEffect == null || EnableCustomSoundEffect.ToggleButton == null)
        {
            EnableCustomSoundEffect = ClientOptionItem.Create("EnableCustomSoundEffect", Main.EnableCustomSoundEffect, __instance);
        }
        if (SwitchVanilla == null || SwitchVanilla.ToggleButton == null)
        {
            SwitchVanilla = ClientOptionItem.Create("SwitchVanilla", Main.SwitchVanilla, __instance, SwitchVanillaButtonToggle);
            static void SwitchVanillaButtonToggle()
            {
                Harmony.UnpatchAll();
                Main.Instance.Unload();
            }
        }
        if (DumpLog == null || DumpLog.ToggleButton == null)
        {
            DumpLog = ClientOptionItem.Create("DumpLog", Main.DumpLog, __instance, DumpLogButtonToggle);
            static void DumpLogButtonToggle()
            {
                Utils.DumpLog();
            }
        }
        if (CanPublic == null || CanPublic.ToggleButton == null)
        {
            CanPublic = ClientOptionItem.Create("CanPublic", Main.CanPublic, __instance);
        }
        if (ModeForSmallScreen == null || ModeForSmallScreen.ToggleButton == null)
        {
            ModeForSmallScreen = ClientOptionItem.Create("ModeForSmallScreen", Main.ModeForSmallScreen, __instance);
        }
        if (ShowLobbyCode == null || ShowLobbyCode.ToggleButton == null)
        {
            ShowLobbyCode = ClientOptionItem.Create("ShowLobbyCode", Main.ShowLobbyCode, __instance);
        }
        /*      if ((VersionCheat == null || VersionCheat.ToggleButton == null) && DebugModeManager.AmDebugger)
                {
                    VersionCheat = ClientOptionItem.Create("VersionCheat", Main.VersionCheat, __instance);
                }
                if ((GodMode == null || GodMode.ToggleButton == null) && DebugModeManager.AmDebugger)
                {
                    GodMode = ClientOptionItem.Create("GodMode", Main.GodMode, __instance);
                } */
    }
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close))]
public static class OptionsMenuBehaviourClosePatch
{
    public static void Postfix()
    {
        if (ClientOptionItem.CustomBackground != null)
        {
            ClientOptionItem.CustomBackground.gameObject.SetActive(false);
        }
    }
}