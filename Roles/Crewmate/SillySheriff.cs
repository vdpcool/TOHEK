using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;

using static TOHE.Options;

namespace TOHE.Roles.Crewmate;
public static class SillySheriff
{
    private static readonly int Id = 9900;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    private static OptionItem MisfireKillsTarget;
    private static OptionItem ShotLimitOpt;
    private static OptionItem Probability;
    public static OptionItem IsInfoPoor;
    public static OptionItem IsClumsy;
    private static OptionItem CanKillAllAlive;
    public static OptionItem CanKillNeutrals;
    public static OptionItem CanKillNeutralsMode;
    public static OptionItem CanKillMadmate;
    public static OptionItem CanKillCharmed;
    public static OptionItem CanKillLovers;
    public static OptionItem CanKillSidekicks;
    public static OptionItem CanKillEgoists;
    public static OptionItem CanKillInfected;
    public static OptionItem CanKillContagious;
    public static OptionItem SidekickSillySheriffCanGoBerserk;
    public static OptionItem SetNonCrewCanKill;
    public static OptionItem NonCrewCanKillCrew;
    public static OptionItem NonCrewCanKillImp;
    public static OptionItem NonCrewCanKillNeutral;
    public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = new();

    public static Dictionary<byte, float> ShotLimit = new();
    public static Dictionary<byte, float> CurrentKillCooldown = new();
    public static readonly string[] KillOption =
        {
            "SheriffCanKillAll", "SheriffCanKillSeparately"
        };
        public static readonly string[] rates =
        {
            "Rate0",  "Rate5",  "Rate10", "Rate20", "Rate30", "Rate40",
            "Rate50", "Rate60", "Rate70", "Rate80", "Rate90", "Rate100",
        };
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.SillySheriff);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff])
                .SetValueFormat(OptionFormat.Seconds);
            MisfireKillsTarget = BooleanOptionItem.Create(Id + 12, "SheriffMisfireKillsTarget", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            ShotLimitOpt = IntegerOptionItem.Create(Id + 13, "SheriffShotLimit", new(1, 15, 1), 15, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff])
                .SetValueFormat(OptionFormat.Times);
            Probability = StringOptionItem.Create(Id + 14, "Probability", rates[1..], 0, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillAllAlive = BooleanOptionItem.Create(Id + 15, "SheriffCanKillAllAlive", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            IsInfoPoor = BooleanOptionItem.Create(Id + 16, "SheriffIsInfoPoor", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            IsClumsy = BooleanOptionItem.Create(Id + 17, "SheriffIsClumsy", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillMadmate = BooleanOptionItem.Create(Id + 18, "SheriffCanKillMadmate", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillCharmed = BooleanOptionItem.Create(Id + 19, "SheriffCanKillCharmed", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillLovers = BooleanOptionItem.Create(Id + 20, "SheriffCanKillLovers", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillSidekicks = BooleanOptionItem.Create(Id + 21, "SheriffCanKillSidekick", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillEgoists = BooleanOptionItem.Create(Id + 22, "SheriffCanKillEgoist", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillInfected = BooleanOptionItem.Create(Id + 23, "SheriffCanKillInfected", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillContagious = BooleanOptionItem.Create(Id + 24, "SheriffCanKillContagious", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillNeutrals = StringOptionItem.Create(Id + 25, "SheriffCanKillNeutrals", KillOption, 0, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            CanKillNeutralsMode = StringOptionItem.Create(Id + 26, "SheriffCanKillNeutralsMode", KillOption, 0, TabGroup.CrewmateRoles, false).SetParent(CanKillNeutrals);
            SetUpNeutralOptions(Id + 32);
            SidekickSillySheriffCanGoBerserk = BooleanOptionItem.Create(Id + 27, "SidekickSillySheriffCanGoBerserk", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            SetNonCrewCanKill = BooleanOptionItem.Create(Id + 28, "SillySheriffSetMadCanKill", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SillySheriff]);
            NonCrewCanKillImp = BooleanOptionItem.Create(Id + 29, "SheriffMadCanKillImp", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
            NonCrewCanKillCrew = BooleanOptionItem.Create(Id + 31, "SheriffMadCanKillCrew", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
            NonCrewCanKillNeutral = BooleanOptionItem.Create(Id + 30, "SheriffMadCanKillNeutral", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
        }
        public static void SetUpNeutralOptions(int Id)
        {
            foreach (var neutral in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x.IsNeutral() && x is not CustomRoles.Pestilence))
            {
                if (neutral is CustomRoles.Executioner
                            or CustomRoles.NWitch
                            or CustomRoles.Jester) continue;
                SetUpKillTargetOption(neutral, Id, true, CanKillNeutrals);
                Id++;
            }
        }
        public static void SetUpKillTargetOption(CustomRoles role, int Id, bool defaultValue = true, OptionItem parent = null)
        {
            if (parent == null) parent = CustomRoleSpawnChances[CustomRoles.SillySheriff];
            var roleName = Utils.GetRoleName(role);
            Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), roleName) } };
            KillTargetOptions[role] = BooleanOptionItem.Create(Id, "SheriffCanKill%role%", defaultValue, TabGroup.CrewmateRoles, false).SetParent(parent);
            KillTargetOptions[role].ReplacementDictionary = replacementDic;
        }
        public static void Init()
        {
            playerIdList = new();
            ShotLimit = new();
            CurrentKillCooldown = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            CurrentKillCooldown.Add(playerId, KillCooldown.GetFloat());

            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);

            ShotLimit.TryAdd(playerId, ShotLimitOpt.GetFloat());
            Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole()} : 残り{ShotLimit[playerId]}発", "ApprenticeSheriff");
        }
        public static bool IsEnable => playerIdList.Count > 0;
        private static void SendRPC(byte playerId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSillySheriffShotLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(ShotLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte SheriffId = reader.ReadByte();
            float Limit = reader.ReadSingle();
            if (ShotLimit.ContainsKey(SheriffId))
                ShotLimit[SheriffId] = Limit;
            else
                ShotLimit.Add(SheriffId, ShotLimitOpt.GetFloat());
        }
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? CurrentKillCooldown[id] : 0f;
        public static bool CanUseKillButton(byte playerId)
            => !Main.PlayerStates[playerId].IsDead
            && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
            && ShotLimit[playerId] > 0;

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target, string Process)
        {
            int Chance = (Probability as StringOptionItem).GetChance();
            int chance = UnityEngine.Random.Range(1, 101);
            switch (Process)
            {
                case "RemoveShotLimit":
                    ShotLimit[killer.PlayerId]--;
                    Logger.Info($"{killer.GetNameWithRole()} : 残り{ShotLimit[killer.PlayerId]}発", "ApprenticeSheriff");
                    SendRPC(killer.PlayerId);
                    break;
                case "Suicide":
                    if ((target.CanBeKilledByASheriff() && chance <= Chance) || (!target.CanBeKilledByASheriff() && chance >= Chance))
                    {
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
                        killer.RpcMurderPlayer(killer);
                        if (MisfireKillsTarget.GetBool())
                            killer.RpcMurderPlayer(target);
                        return false;
                    }
                    break;
            }
            return true;
        }
        public static string GetShotLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Color.yellow : Color.white, ShotLimit.TryGetValue(playerId, out var shotLimit) ? $"({shotLimit})" : "Invalid");
        public static bool CanBeKilledByASheriff(this PlayerControl player)
        {
            var cRole = player.GetCustomRole();
            return cRole.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => true,
                CustomRoleTypes.Madmate => KillTargetOptions.TryGetValue(CustomRoles.Madmate, out var option) && option.GetBool(),
                CustomRoleTypes.Neutral => CanKillNeutrals.GetValue() == 0 || !KillTargetOptions.TryGetValue(cRole, out var option) || option.GetBool(),
                _ => false,
            };
        }
    internal static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        throw new NotImplementedException();
    }
}