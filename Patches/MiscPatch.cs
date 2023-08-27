using HarmonyLib;
using System;
using AmongUs.Data.Player;
using AmongUs.Data.Legacy;
using Discord;

namespace TOHE.Patches;

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
public static class AmBanned
{
    public static void Postfix(out bool __result) => __result = false;
}

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.FileName), MethodType.Getter)]
public static class SaveManagerPatch
{
    public static void Postfix(ref string __result) => __result += "_TOHE-K";
}

[HarmonyPatch(typeof(LegacySaveManager), nameof(LegacySaveManager.GetPrefsName))]
public static class LegacySaveManagerPatch
{
    public static void Postfix(ref string __result) => __result += "_TOHE-K";
}

[HarmonyPatch(typeof(PlayerPurchasesData), nameof(PlayerPurchasesData.GetPurchase))]
public static class GetPurchasePatch
{
    public static bool Prefix(out bool __result)
    {
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPurchasesData), nameof(PlayerPurchasesData.SetPurchased))]
public static class SetPurchasedPatch
{
    public static bool Prefix() => false;
}

[HarmonyPatch(typeof(ActivityManager), nameof(ActivityManager.UpdateActivity))]
public class DiscordRPC
{
    private static string lobbycode = "";
    private static string region = "";
    public static void Prefix([HarmonyArgument(0)] Activity activity)
    {
        var details = $"TOHE-K v{Main.PluginVersion}";
        activity.Details = details;

        try
        {
            if (activity.State != "In Menus")
            {
                if (Main.ShowLobbyCode.Value)
                {
                    int maxSize = GameOptionsManager.Instance.currentNormalGameOptions.MaxPlayers;
                    if (GameStates.IsLobby)
                    {
                        lobbycode = GameStartManager.Instance.GameRoomNameCode.text;
                        region = ServerManager.Instance.CurrentRegion.Name;
                        if (region == "North America") region = "North America";
                        if (region == "Europe") region = "Europe";
                        if (region == "Asia") region = "Asia";
                        if (region == "模组服务器北美洲MNA") region = "Modded North America";
                        if (region == "模组服务器北美洲MEU") region = "Modded Europe";
                        if (region == "模组服务器北美洲MAS") region = "Modded Asia";
                    }

                    if (lobbycode != "" && region != "")
                    {
                        details = $"TOHEK v{Main.PluginVersion} - {lobbycode} ({region})";
                    }

                    activity.Details = details;
                }
                else
                {
                    details = $"TOHEK v{Main.PluginVersion}";
                }
            }
        }

        catch (ArgumentException ex)
        {
            Logger.Error("Error in updating Discord RPC", "DiscordPatch");
            Logger.Exception(ex, "DiscordPatch");
            details = $"TOHEK v{Main.PluginVersion}";
            activity.Details = details;
        }
    }
}