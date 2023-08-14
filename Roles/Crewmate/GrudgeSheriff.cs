using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hazel;
using UnityEngine;
using AmongUs.GameOptions;

using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class GrudgeSheriff
    {
        private static readonly int Id = 36100;
        public static List<byte> playerIdList = new();

        private static OptionItem Cooldown;
        private static OptionItem MisfireKillsTarget;
        private static OptionItem ShotLimitOpt;
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
        public static OptionItem SidekickGrudgeSheriffCanGoBerserk;
        public static OptionItem SetNonCrewCanKill;
        public static OptionItem NonCrewCanKillCrew;
        public static OptionItem NonCrewCanKillImp;
        public static OptionItem NonCrewCanKillNeutral;
        public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = new();
        public static Dictionary<byte, PlayerControl> KillWaitPlayerSelect = new();
        public static Dictionary<byte, PlayerControl> KillWaitPlayer = new();
        public static Dictionary<byte, float> ShotLimit = new();
        public static Dictionary<byte, bool> IsCoolTimeOn = new();
        public static readonly string[] KillOption =
        {
            "SheriffCanKillAll", "SheriffCanKillSeparately"
        };
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.GrudgeSheriff);
            Cooldown = FloatOptionItem.Create(Id + 10, "Cooldown", new(0f, 180f, 2.5f), 30f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.GrudgeSheriff])
                .SetValueFormat(OptionFormat.Seconds);
            MisfireKillsTarget = BooleanOptionItem.Create(Id + 11, "SheriffMisfireKillsTarget", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            ShotLimitOpt = IntegerOptionItem.Create(Id + 12, "SheriffShotLimit", new(1, 15, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.GrudgeSheriff])
                .SetValueFormat(OptionFormat.Times);
            CanKillAllAlive = BooleanOptionItem.Create(Id + 13, "SheriffCanKillAllAlive", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            SetUpKillTargetOption(CustomRoles.Madmate, Id + 14);
            IsInfoPoor = BooleanOptionItem.Create(Id + 15, "SheriffIsInfoPoor", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            IsClumsy = BooleanOptionItem.Create(Id + 16, "SheriffIsClumsy", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillMadmate = BooleanOptionItem.Create(Id + 17, "SheriffCanKillMadmate", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillCharmed = BooleanOptionItem.Create(Id + 18, "SheriffCanKillCharmed", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillLovers = BooleanOptionItem.Create(Id + 19, "SheriffCanKillLovers", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillSidekicks = BooleanOptionItem.Create(Id + 20, "SheriffCanKillSidekick", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillEgoists = BooleanOptionItem.Create(Id + 21, "SheriffCanKillEgoist", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillInfected = BooleanOptionItem.Create(Id + 22, "SheriffCanKillInfected", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillContagious = BooleanOptionItem.Create(Id + 23, "SheriffCanKillContagious", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillNeutrals = StringOptionItem.Create(Id + 24, "SheriffCanKillNeutrals", KillOption, 0, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            CanKillNeutralsMode = StringOptionItem.Create(Id + 25, "SheriffCanKillNeutralsMode", KillOption, 0, TabGroup.CrewmateRoles, false).SetParent(CanKillNeutrals);
            SetUpNeutralOptions(Id + 32);
            SidekickGrudgeSheriffCanGoBerserk = BooleanOptionItem.Create(Id + 27, "SidekickGrudgeSheriffCanGoBerserk", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            SetNonCrewCanKill = BooleanOptionItem.Create(Id + 28, "GrudgeSheriffSetMadCanKill", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.GrudgeSheriff]);
            NonCrewCanKillImp = BooleanOptionItem.Create(Id + 29, "SheriffMadCanKillImp", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
            NonCrewCanKillCrew = BooleanOptionItem.Create(Id + 31, "SheriffMadCanKillCrew", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
            NonCrewCanKillNeutral = BooleanOptionItem.Create(Id + 30, "SheriffMadCanKillNeutral", true, TabGroup.CrewmateRoles, false).SetParent(SetNonCrewCanKill);
        }
        public static void SetUpNeutralOptions(int Id)
        {
            foreach (var neutral in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x.IsNeutral() && x is not CustomRoles.KB_Normal and not CustomRoles.Hotpotato and not CustomRoles.Coldpotato and not CustomRoles.Pestilence))
            {
                SetUpKillTargetOption(neutral, Id, true, CanKillNeutralsMode);
                Id++;
            }
        }
        public static void SetUpKillTargetOption(CustomRoles role, int Id, bool defaultValue = true, OptionItem parent = null)
        {
            if (parent == null) parent = CustomRoleSpawnChances[CustomRoles.GrudgeSheriff];
            var roleName = Utils.GetRoleName(role);
            Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), roleName) } };
            KillTargetOptions[role] = BooleanOptionItem.Create(Id, "SheriffCanKill%role%", defaultValue, TabGroup.CrewmateRoles, false).SetParent(parent);
            KillTargetOptions[role].ReplacementDictionary = replacementDic;
        }
        public static void Init()
        {
            playerIdList = new();
            KillWaitPlayer = new();
            KillWaitPlayerSelect = new();
            ShotLimit = new();
            IsCoolTimeOn = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            KillWaitPlayerSelect.Add(playerId, null);
            IsCoolTimeOn.Add(playerId, true);
            var pc = Utils.GetPlayerById(playerId);
            pc.AddVentSelect();

            ShotLimit.TryAdd(playerId, ShotLimitOpt.GetFloat());
            Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole()} : 残り{ShotLimit[playerId]}発", "GrudgeSheriff");
        }
        public static bool IsEnable => playerIdList.Count > 0;
        private static void SendRPC(byte playerId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGrudgeSheriffShotLimit, SendOption.Reliable, -1);
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
        public static void ApplyGameOptions(byte playerId)
        {
            AURoleOptions.EngineerCooldown = CanUseKillButton(playerId) ? (IsCoolTimeOn[playerId] ? Cooldown.GetFloat() : 0f) : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1.0f;
        }
        public static void OnReportDeadBody()
        {
            KillWaitPlayer.Clear();

            foreach(var GSheriffId in playerIdList)
            {
                KillWaitPlayerSelect[GSheriffId] = null;
                IsCoolTimeOn[GSheriffId] = true;
            }
        }
        public static bool CanUseKillButton(byte playerId)
            => !Main.PlayerStates[playerId].IsDead
            && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
            && ShotLimit[playerId] > 0;

        public static void SetAbilityButtonText(HudManager __instance) => __instance.AbilityButton.OverrideText($"{GetString("ChangeButtonText")}");

        // ================================================以下、ベントに入った時の処理
        public static void OnEnterVent(PlayerControl pc)
        {
            var playerId = pc.PlayerId;
            IsCoolTimeOn[playerId] = false;
            pc.MarkDirtySettings();

            if (playerIdList.Contains(playerId))
            {
                KillWaitPlayerSelect[playerId] = pc.VentPlayerSelect(() =>
                {
                    KillWaitPlayer.Add(playerId, KillWaitPlayerSelect[playerId]);
                    IsCoolTimeOn[playerId] = true;
                    pc.MarkDirtySettings();
                });

                Utils.NotifyRoles(SpecifySeer: pc);
            }
        }
        // ================================================以下、キル処理
        public static void FixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;
            if (GameStates.IsInTask && KillWaitPlayer.ContainsKey(playerId))
            {
                if (!player.IsAlive())
                {
                    KillWaitPlayerSelect[playerId] = null;
                    KillWaitPlayer.Remove(playerId);
                }
                else
                {
                    Vector2 GSpos = player.transform.position;//GSの位置

                    var target = KillWaitPlayer[playerId];
                    float targetDistance = Vector2.Distance(GSpos, target.transform.position);

                    var KillRange = GameOptionsData.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                    if (targetDistance <= KillRange && player.CanMove && target.CanMove)
                    {
                        //RPC.PlaySoundRPC(playerId, Sounds.KillSound);
                        ShotLimit[playerId]--;
                        Logger.Info($"{player.GetNameWithRole()} : 残り{ShotLimit[playerId]}発", "GrudgeSheriff");
                        SendRPC(playerId);
                        player.RpcResetAbilityCooldown();

                        if (!target.CanBeKilledByGrudgeSheriff())
                        {
                            Main.PlayerStates[playerId].deathReason = PlayerState.DeathReason.Misfire;
                            player.SetRealKiller(player);
                            player.RpcMurderPlayer(player);
                            Utils.MarkEveryoneDirtySettings();
                            KillWaitPlayerSelect[playerId] = null;
                            KillWaitPlayer.Remove(player.PlayerId);
                            Utils.NotifyRoles();

                            if (!MisfireKillsTarget.GetBool()) return;
                        }
                        target.SetRealKiller(player);
                        player.RpcMurderPlayer(target);
                        Utils.MarkEveryoneDirtySettings();
                        KillWaitPlayerSelect[playerId] = null;
                        KillWaitPlayer.Remove(player.PlayerId);
                        Utils.NotifyRoles();
                    }
                }
            }
        }
        public static string GetShotLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Color.yellow : Color.gray, ShotLimit.TryGetValue(playerId, out var shotLimit) ? $"({shotLimit})" : "Invalid");
        private static bool CanBeKilledByGrudgeSheriff(this PlayerControl player)
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

        // ================================================以下、表示系処理
        public static string GetGuardPlayerText(PlayerControl Gsheriff, bool hud, bool isMeeting = false)
        {
            var GsheriffId = Gsheriff.PlayerId;
            if (Gsheriff == null || !CanUseKillButton(GsheriffId) || isMeeting) return "";

            var str = new StringBuilder();
            if (hud)
            {
                if (KillWaitPlayerSelect[GsheriffId] == null)
                    str.Append(GetString("SelectPlayerTagBefore"));
                else
                {
                    str.Append(GetString("SelectPlayerTag"));
                    str.Append(KillWaitPlayerSelect[GsheriffId].GetRealName());
                }
            }
            else
            {
                if (KillWaitPlayerSelect[GsheriffId] == null)
                    str.Append(GetString("SelectPlayerTagMiniBefore"));
                else
                {
                    str.Append(GetString("SelectPlayerTagMini"));
                    str.Append(KillWaitPlayerSelect[GsheriffId].GetRealName());
                }
            }
            return str.ToString();
        }
    }
}