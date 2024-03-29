﻿using Hazel;
using System.Collections.Generic;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class ChiefOfPolice
{
    private static readonly int Id = 1748596;
    private static List<byte> playerIdList = new();
    public static Dictionary<byte, int> PoliceLimit = new();
    public static OptionItem SkillCooldown;
    public static OptionItem CanImpostorAndNeutarl;
    public static OptionItem ChiefOfPoliceCountMode;
    public static OptionItem RecruitedSheriff;
    public static OptionItem RecruitedSillySheriff;
    public static OptionItem RecruitedGrudgeSheriff;

    public static readonly string[] chiefOfPoliceCountMode =
    {
        "ChiefOfPoliceCountMode.KillKiller",
        "ChiefOfPoliceCountMode.Warn",
    };
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.ChiefOfPolice);
        SkillCooldown = FloatOptionItem.Create(Id + 10, "ChiefOfPoliceSkillCooldown", new(2.5f, 900f, 2.5f), 20f, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice])
            .SetValueFormat(OptionFormat.Seconds);
        CanImpostorAndNeutarl = BooleanOptionItem.Create(Id + 15, "PoliceCanImpostorAndNeutral", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice]);
        ChiefOfPoliceCountMode = StringOptionItem.Create(Id + 20, "ChiefOfPoliceCountMode", chiefOfPoliceCountMode, 0, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice]);
        RecruitedSheriff = BooleanOptionItem.Create(Id + 25, "RecruitedSheriff", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice]);
        RecruitedSillySheriff = BooleanOptionItem.Create(Id + 30, "RecruitedSillySheriff", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice]);
        RecruitedGrudgeSheriff = BooleanOptionItem.Create(Id + 35, "RecruitedGrudgeSheriff", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ChiefOfPolice]);
    }
    public static void Init()
    {
        playerIdList = new();
        PoliceLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        PoliceLimit.TryAdd(playerId, 1);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetPoliceLimlit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(PoliceLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (PoliceLimit.ContainsKey(PlayerId))
            PoliceLimit[PlayerId] = Limit;
        else
            PoliceLimit.Add(PlayerId, 1);
    }
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && (PoliceLimit.TryGetValue(playerId, out var x) ? x : 1) >= 1;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? SkillCooldown.GetFloat() : 300f;
    public static string GetSkillLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Utils.GetRoleColor(CustomRoles.ChiefOfPolice) : Color.gray, PoliceLimit.TryGetValue(playerId, out var policeLimit) ? $"({policeLimit})" : "Invalid");
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        PoliceLimit[killer.PlayerId]--;
        if (CanBeSheriff(target))
        {
            target.RpcSetCustomRole(CustomRoles.Sheriff);
            var targetId = target.PlayerId;
            foreach (var player in Main.AllAlivePlayerControls)
            {
                if (player.PlayerId == targetId)
                {
                    Sheriff.Add(player.PlayerId);
                    Sheriff.Add(player.PlayerId);
                }
            }
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sheriff), GetString("SheriffSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sheriff), GetString("BeSheriffByPolice")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
        }
        else
        {
            if (ChiefOfPoliceCountMode.GetInt() == 1)
            {
                killer.RpcMurderPlayerV3(killer);
                return true;
            }
            if (ChiefOfPoliceCountMode.GetInt() == 2)
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sheriff), GetString("NotSheriff!!!")));
                return true;
            }
        }
        return false;
    }
    public static bool OnCheckMurder1(PlayerControl killer, PlayerControl target)
    {
        PoliceLimit[killer.PlayerId]--;
        if (CanBeSillySheriff(target))
        {
            target.RpcSetCustomRole(CustomRoles.SillySheriff);
            var targetId = target.PlayerId;
            foreach (var player in Main.AllAlivePlayerControls)
            {
                if (player.PlayerId == targetId)
                {
                    SillySheriff.Add(player.PlayerId);
                    SillySheriff.Add(player.PlayerId);
                }
            }
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SillySheriff), GetString("SheriffSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SillySheriff), GetString("BeSheriffByPolice")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
        }
        else
        {
            if (ChiefOfPoliceCountMode.GetInt() == 1)
            {
                killer.RpcMurderPlayerV3(killer);
                return true;
            }
            if (ChiefOfPoliceCountMode.GetInt() == 2)
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SillySheriff), GetString("NotSillySheriff!!!")));
                return true;
            }
        }
        return false;
    }
    public static bool OnCheckMurder2(PlayerControl killer, PlayerControl target)
    {
        PoliceLimit[killer.PlayerId]--;
        if (CanBeGrudgeSheriff(target))
        {
            target.RpcSetCustomRole(CustomRoles.GrudgeSheriff);
            var targetId = target.PlayerId;
            foreach (var player in Main.AllAlivePlayerControls)
            {
                if (player.PlayerId == targetId)
                {
                    GrudgeSheriff.Add(player.PlayerId);
                    GrudgeSheriff.Add(player.PlayerId);
                }
            }
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.GrudgeSheriff), GetString("SheriffSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.GrudgeSheriff), GetString("BeSheriffByPolice")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
        }
        else
        {
            if (ChiefOfPoliceCountMode.GetInt() == 1)
            {
                killer.RpcMurderPlayerV3(killer);
                return true;
            }
            if (ChiefOfPoliceCountMode.GetInt() == 2)
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.GrudgeSheriff), GetString("NotGrudgeSheriff!!!")));
                return true;
            }
        }
        return false;
    }
    public static bool CanBeSheriff(this PlayerControl pc)
    {
        return pc != null && (pc.GetCustomRole().IsCrewmate() && pc.CanUseKillButton()) || pc.GetCustomRole().IsNeutral() && pc.CanUseKillButton() && CanImpostorAndNeutarl.GetBool()|| pc.GetCustomRole().IsImpostor() && CanImpostorAndNeutarl.GetBool();
    }
    public static bool CanBeSillySheriff(this PlayerControl pc)
    {
        return pc != null && (pc.GetCustomRole().IsCrewmate() && pc.CanUseKillButton()) || pc.GetCustomRole().IsNeutral() && pc.CanUseKillButton() && CanImpostorAndNeutarl.GetBool()|| pc.GetCustomRole().IsImpostor() && CanImpostorAndNeutarl.GetBool();
    }
    public static bool CanBeGrudgeSheriff(this PlayerControl pc)
    {
        return pc != null && (pc.GetCustomRole().IsCrewmate() && pc.CanUseKillButton()) || pc.GetCustomRole().IsNeutral() && pc.CanUseKillButton() && CanImpostorAndNeutarl.GetBool()|| pc.GetCustomRole().IsImpostor() && CanImpostorAndNeutarl.GetBool();
    }
}