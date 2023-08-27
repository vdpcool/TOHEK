using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using TOHE.Modules;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
class CheckProtectPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        Logger.Info("CheckProtect発生: " + __instance.GetNameWithRole() + "=>" + target.GetNameWithRole(), "CheckProtect");

        if (__instance.Is(CustomRoles.EvilSpirit))
        {
            if (target.Is(CustomRoles.Spiritcaller))
            {
                Spiritcaller.ProtectSpiritcaller();
            }
            else
            {
                Spiritcaller.HauntPlayer(target);
            }

            __instance.RpcResetAbilityCooldown();
            return true;
        }

        if (__instance.Is(CustomRoles.Sheriff))
        {
            if (__instance.Data.IsDead)
            {
                Logger.Info("守護をブロックしました。", "CheckProtect");
                return false;
            }
        }
        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
class CheckMurderPatch
{
    public static int YLLevel = 0;
    public static int YLdj = 1;
    public static int YLCS = 0;
    public static Dictionary<byte, float> TimeSinceLastKill = new();
    private static Dictionary<byte, float> NowCooldown;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = NowCooldown[id]; 
    public static void Update()
    {
        for (byte i = 0; i < 15; i++)
        {
            if (TimeSinceLastKill.ContainsKey(i))
            {
                TimeSinceLastKill[i] += Time.deltaTime;
                if (15f < TimeSinceLastKill[i]) TimeSinceLastKill.Remove(i);
            }
        }
    }
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        var killer = __instance; //読み替え変数

        Logger.Info($"{killer.GetNameWithRole()} => {target.GetNameWithRole()}", "CheckMurder");

        //死人はキルできない
        if (killer.Data.IsDead)
        {
            Logger.Info($"{killer.GetNameWithRole()}は死亡しているためキャンセルされました。", "CheckMurder");
            return false;
        }

        //不正キル防止処理
        if (target.Data == null || //PlayerDataがnullじゃないか確認
            target.inVent || target.inMovingPlat //targetの状態をチェック
        )
        {
            Logger.Info("目标处于无法被击杀状态，击杀被取消", "CheckMurder");
            return false;
        }
        if (target.Data.IsDead) //同じtargetへの同時キルをブロック
        {
            Logger.Info("目标处于死亡状态，击杀被取消", "CheckMurder");
            return false;
        }
        if (MeetingHud.Instance != null) //会議中でないかの判定
        {
            Logger.Info("会议中，击杀被取消", "CheckMurder");
            return false;
        }

        var divice = Options.CurrentGameMode == CustomGameMode.SoloKombat ? 3000f : 2000f;
        var pivice = Options.CurrentGameMode == CustomGameMode.HotPotato ? 3000f : 2000f;
        float minTime = Mathf.Max(0.02f, AmongUsClient.Instance.Ping / divice * 6f); //※AmongUsClient.Instance.Pingの値はミリ秒(ms)なので÷1000
        //TimeSinceLastKillに値が保存されていない || 保存されている時間がminTime以上 => キルを許可
        //↓許可されない場合
        if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime)
        {
            Logger.Info("击杀间隔过短，击杀被取消", "CheckMurder");
            return false;
        }
        TimeSinceLastKill[killer.PlayerId] = 0f;

        if (target.Is(CustomRoles.Antidote))
        {
            if (Main.KilledAntidote.ContainsKey(killer.PlayerId))
            {
                // Key already exists, update the value
                Main.KilledAntidote[killer.PlayerId] += 1;// Main.AllPlayerKillCooldown.TryGetValue(killer.PlayerId, out float kcd) ? (kcd - Options.AntidoteCDOpt.GetFloat() > 0 ? kcd - Options.AntidoteCDOpt.GetFloat() : 0f) : 0f;
            }
            else
            {
                // Key doesn't exist, add the key-value pair
                Main.KilledAntidote.Add(killer.PlayerId, 1);// Main.AllPlayerKillCooldown.TryGetValue(killer.PlayerId, out float kcd) ? (kcd - Options.AntidoteCDOpt.GetFloat() > 0 ? kcd - Options.AntidoteCDOpt.GetFloat() : 0f) : 0f);
            }

        }

        killer.ResetKillCooldown();

        //キル可能判定
        if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton())
        {
            Logger.Info(killer.GetNameWithRole() + "击杀者不被允许使用击杀键，击杀被取消", "CheckMurder");
            return false;
        }

        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            SoloKombatManager.OnPlayerAttack(killer, target);
            return false;
        }
        if (Options.CurrentGameMode == CustomGameMode.HotPotato)
        {
            HotPotatoManager.OnPlayerAttack(killer, target);
            return false;
        }

        //実際のキラーとkillerが違う場合の入れ替え処理
        if (Sniper.IsEnable) Sniper.TryGetSniper(target.PlayerId, ref killer);
        if (killer != __instance) Logger.Info($"Real Killer={killer.GetNameWithRole()}", "CheckMurder");

        //鹈鹕肚子里的人无法击杀
        if (Pelican.IsEaten(target.PlayerId))
            return false;

        //阻止对活死人的操作
        if (target.Is(CustomRoles.Glitch))
            return false;

        if (target.Is(CustomRoles.Shaman) && !killer.Is(CustomRoles.NWitch))
        {
            if (Main.ShamanTarget != byte.MaxValue && target.IsAlive())
            { 
                target = Utils.GetPlayerById(Main.ShamanTarget);
                Main.ShamanTarget = byte.MaxValue;
            }
        }

        // 赝品检查
        if (Counterfeiter.OnClientMurder(killer)) return false;
        if (Pursuer.OnClientMurder(killer)) return false;
        if (Addict.IsImmortal(target)) return false;

        //判定凶手技能
        if (killer.PlayerId != target.PlayerId)
        {
            //非自杀场景下才会触发
            switch (killer.GetCustomRole())
            {
                //==========内鬼阵营==========//
                case CustomRoles.BountyHunter: //必须在击杀发生前处理
                    BountyHunter.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.SerialKiller:
                    SerialKiller.OnCheckMurder(killer);
                    break;
                case CustomRoles.Vampire:
                    if (!Vampire.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Poisoner:
                    if (!Poisoner.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Warlock:
                    if (!Main.CheckShapeshift[killer.PlayerId] && !Main.isCurseAndKill[killer.PlayerId])
                    { //Warlockが変身時以外にキルしたら、呪われる処理
                        if (target.Is(CustomRoles.Needy) || target.Is(CustomRoles.Lazy)) return false;
                        Main.isCursed = true;
                        killer.SetKillCooldown();
                        //RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.RPCPlayCustomSound("Line");
                        Main.CursedPlayers[killer.PlayerId] = target;
                        Main.WarlockTimer.Add(killer.PlayerId, 0f);
                        Main.isCurseAndKill[killer.PlayerId] = true;
                        //RPC.RpcSyncCurseAndKill();
                        return false;
                    }
                    if (Main.CheckShapeshift[killer.PlayerId])
                    {//呪われてる人がいないくて変身してるときに通常キルになる
                        killer.RpcCheckAndMurder(target);
                        return false;
                    }
                    if (Main.isCurseAndKill[killer.PlayerId]) killer.RpcGuardAndKill(target);
                    return false;
                case CustomRoles.Assassin:
                    if (!Assassin.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Witch:
                    if (!Witch.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.HexMaster:
                    if (!HexMaster.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Puppeteer:
                    if (target.Is(CustomRoles.Needy) || target.Is(CustomRoles.Lazy) || Medic.ProtectList.Contains(target.PlayerId)) return false;
                    Main.PuppeteerList[target.PlayerId] = killer.PlayerId;
                    killer.SetKillCooldown();
                    killer.RPCPlayCustomSound("Line");
                    Utils.NotifyRoles(SpecifySeer: killer);
                    return false;
                case CustomRoles.NWitch:
                    //  if (target.Is(CustomRoles.Needy) || target.Is(CustomRoles.Lazy)) return false;
                    Main.TaglockedList[target.PlayerId] = killer.PlayerId;
                    killer.SetKillCooldown();
                    Utils.NotifyRoles(SpecifySeer: killer);
                    return false;
                case CustomRoles.CovenLeader:
                    //  if (target.Is(CustomRoles.Needy) || target.Is(CustomRoles.Lazy)) return false;
                    Main.CovenLeaderList[target.PlayerId] = killer.PlayerId;
                    killer.SetKillCooldown();
                    Utils.NotifyRoles(SpecifySeer: killer);
                    return false;
                case CustomRoles.Capitalism:
                    if (!Main.CapitalismAddTask.ContainsKey(target.PlayerId))
                        Main.CapitalismAddTask.Add(target.PlayerId, 0);
                    Main.CapitalismAddTask[target.PlayerId]++;
                    if (!Main.CapitalismAssignTask.ContainsKey(target.PlayerId))
                        Main.CapitalismAssignTask.Add(target.PlayerId, 0);
                    Main.CapitalismAssignTask[target.PlayerId]++;
                    Logger.Info($"资本主义 {killer.GetRealName()} 又开始祸害人了：{target.GetRealName()}", "Capitalism Add Task");
                    killer.RpcGuardAndKill(killer);
                    killer.SetKillCooldown();
                    return false;
                case CustomRoles.Bomber:
                    return false;
                case CustomRoles.Gangster:
                    if (Gangster.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.BallLightning:
                    if (BallLightning.CheckBallLightningMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Greedier:
                    Greedier.OnCheckMurder(killer);
                    break;
                case CustomRoles.QuickShooter:
                    QuickShooter.QuickShooterKill(killer);
                    break;
                case CustomRoles.Sans:
                    Sans.OnCheckMurder(killer);
                    break;
                case CustomRoles.Juggernaut:
                    Juggernaut.OnCheckMurder(killer);
                    break;
                case CustomRoles.Hangman:
                    if (!Hangman.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Swooper:
                    if (!Swooper.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Wraith:
                    if (!Wraith.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Vandalism:
                    Vandalism.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Prophet:
                    Prophet.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.ChiefOfPolice:
                    ChiefOfPolice.OnCheckMurder(killer, target);
                    ChiefOfPolice.OnCheckMurder1(killer, target);
                    ChiefOfPolice.OnCheckMurder2(killer, target);
                    return false;
                case CustomRoles.ElectOfficials:
                    ElectOfficials.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.BSR:
                    BSR.OnCheckMurder(killer,target);
                    break;
                case CustomRoles.SpeedUp:
                    SpeedUp.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Romantic:
                    if (!Romantic.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.VengefulRomantic:
                    if (!VengefulRomantic.OnCheckMurder(killer, target)) return false;
                    break;

                //==========中立阵营==========//
                case CustomRoles.PlagueBearer:
                    if (!PlagueBearer.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Pirate:
                    if (!Pirate.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Arsonist:
                    killer.SetKillCooldown(Options.ArsonistDouseTime.GetFloat());
                    if (!Main.isDoused[(killer.PlayerId, target.PlayerId)] && !Main.ArsonistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.ArsonistTimer.Add(killer.PlayerId, (target, 0f));
                        Utils.NotifyRoles(SpecifySeer: __instance);
                        RPC.SetCurrentDousingTarget(killer.PlayerId, target.PlayerId);
                    }
                    return false;
                case CustomRoles.HatarakiMan:
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.SetRealKiller(killer);
                    Main.PlayerStates[target.PlayerId].SetDead();
                    killer.SetKillCooldownV2();
                    target.RpcMurderPlayerV3(target);
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                    return true;
                }
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    return false;
                }
                var pg1 = IRandom.Instance;
                    if (pg1.Next(0, 100) < Options.SpecialAgentrobability.GetInt())
                    {
                        killer.SetRealKiller(target);
                        target.RpcMurderPlayerV3(killer);
                        Logger.Info($"{target.GetRealName()} 任务工反杀：{killer.GetRealName()}", "HatarakiMan Kill");
                        return false;
                    }
                    break;
                case CustomRoles.XiaoMu:
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    return false;
                }
                var Fg = IRandom.Instance;
                int xiaomu = Fg.Next(1, 3);
                    if (xiaomu == 1)
                    {
                        if (killer.PlayerId != target.PlayerId || target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper)
                        {
                            NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu1")));
                            killer.RPCPlayCustomSound("Congrats");
                            target.RPCPlayCustomSound("Congrats");
                            float delay;
                            if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat()) delay = 0f;
                            else delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);
                            delay = Math.Max(delay, 0.15f);
                            if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                            Logger.Info($"{killer.GetNameWithRole()} 击杀萧暮自动报告 => {target.GetNameWithRole()}", "XiaoMu");
                            new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Bait Self Report");
                        }
                    } else if (xiaomu == 2)
                    {
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu2")));
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发原地不能动 => {target.GetNameWithRole()}", "XiaoMu");
                        var tmpSpeed1 = Main.AllPlayerSpeed[killer.PlayerId];
                        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
                        killer.MarkDirtySettings();
                        new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed1;
                            ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                            killer.MarkDirtySettings();
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                        }, Options.TrapperBlockMoveTime.GetFloat(), "Trapper BlockMove");
                    } else if (xiaomu == 3)
                    {
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发cd114514 => {target.GetNameWithRole()}", "XiaoMu");
                        Main.AllPlayerKillCooldown[killer.PlayerId] = 114514f;
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu3")));
                    } else
                    {
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发若报告尸体则报告人知道凶手是谁 => {target.GetNameWithRole()}", "XiaoMu");
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu4")));
                    }
                    break;
                case CustomRoles.MengJiangGirl:
                    var Mg = IRandom.Instance;
                    int mengjiang = Mg.Next(0, 15);
                    PlayerControl mengjiangp = Utils.GetPlayerById(mengjiang);
                    if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 0)
                    {
                        if (mengjiangp.GetCustomRole().IsCrewmate())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到船员，设置为船员", "MengJiang");
                            break;
                        }
                    }
                    else if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 1)
                    {
                        if (mengjiangp.GetCustomRole().IsImpostor())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到内鬼，设置为内鬼", "MengJiang");
                            break;
                        }
                    }
                    else if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 2)
                    {
                        if (mengjiangp.GetCustomRole().IsNeutral())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到中立，设置为中立", "MengJiang");
                            break;
                        }
                    }
                    else
                    {
                        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Cry;
                    }
                    break;
                case CustomRoles.Slaveowner:
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (Main.ForSlaveowner.Contains(player.PlayerId))
                        {
                            Main.ForSlaveowner.Remove(target.PlayerId);
                            CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                        }
                    }
                    break;
                case CustomRoles.Swapper:
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                    {
                        var pos = target.transform.position;
                        var dis = Vector2.Distance(pos, pc.transform.position);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        Swapper.OnCheckMurder(killer, target);
                    }
                    return false;
                case CustomRoles.Amnesiac:
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                    {
                        var pos = target.transform.position;
                        var dis = Vector2.Distance(pos, pc.transform.position);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                    return false;
                case CustomRoles.LoveCutter:
                    if (killer.Is(CustomRoles.Arsonist) || killer.Is(CustomRoles.PlatonicLover) || killer.Is(CustomRoles.Totocalcio))
                    if (killer.Is(CustomRoles.SillySheriff)) break;
                    LoveCutterGuard(killer, target);
                    Utils.NotifyRoles();
                    return false;
                case CustomRoles.TimeMaster:
                    if (Main.TimeMasterInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                        if (Main.TimeMasterInProtect[target.PlayerId] + Options.TimeMasterSkillDuration.GetInt() >= Utils.GetTimeStamp(DateTime.Now))
                        {
                            foreach (var player in Main.AllPlayerControls)
                            {
                                if (Main.TimeMasterbacktrack.ContainsKey(player.PlayerId))
                                {
                                    var position = Main.TimeMasterbacktrack[player.PlayerId];
                                    Utils.TP(player.NetTransform, position);
                                }
                            }
                            killer.SetKillCooldownV2(target: target, forceAnime: true);
                            return false;
                        }
                    break;
                case CustomRoles.Indomitable:
                    Main.ShieldPlayer = byte.MaxValue;
                Utils.TP(killer.NetTransform, target.GetTruePosition());
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                    target.RpcGuardAndKill();
                        new LateTask(() =>
                        {
                            target?.NoCheckStartMeeting(target?.Data);
                        }, 10.0f, "Skill Remain Message");
                new LateTask(() =>
                {
                    target.RpcMurderPlayerV3(target);
                }, 23.0f, "Skill Remain Message");
                return false;
                case CustomRoles.Masochism:
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    Main.MasochismKillMax[target.PlayerId]++;
                killer.RPCPlayCustomSound("DM");
                target.Notify(string.Format(GetString("MasochismKill"), Main.MasochismKillMax[target.PlayerId]));
                    if (Main.MasochismKillMax[target.PlayerId] >= Options.KillMasochismMax.GetInt())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Masochism);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                    }
                return false;
            case CustomRoles.PlaguesGod:
                killer.SetRealKiller(target);
                target.RpcMurderPlayerV3(killer);
                Logger.Info($"{target.GetRealName()} 万疫之神反杀：{killer.GetRealName()}", "Veteran Kill");
                return false;
            case CustomRoles.Fugitive:
                var Fi = IRandom.Instance;
                int rndNum = Fi.Next(0, 100);
                if (rndNum >= 10 && rndNum < 20)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Masochism);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 20 && rndNum < 30)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Bull);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 30 && rndNum < 40)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 40 && rndNum < 50)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 50 && rndNum < 60)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jealousy);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 60 && rndNum < 70)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.BloodKnight);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 70 && rndNum < 80)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 80 && rndNum < 90)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.God);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 90 && rndNum < 100)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                    break;
            case CustomRoles.Bull:
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                foreach (var player in Main.AllPlayerControls)
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                    if (player == target) continue;
                    if (Vector2.Distance(target.transform.position, player.transform.position) <= Options.BullRadius.GetFloat())
                    {
                        player.SetRealKiller(target);
                        player.RpcMurderPlayerV3(player);
                        Main.BullKillMax[target.PlayerId]++;
                    }
                }
                if (Main.BullKillMax[target.PlayerId] >= Options.BullKill.GetInt())
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Bull);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                return false;
                case CustomRoles.Seeker: //必须在击杀发生前处理
                    Seeker.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.SpecialAgent:
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.SetRealKiller(killer);
                    Main.PlayerStates[target.PlayerId].SetDead();
                    killer.SetKillCooldownV2();
                    target.RpcMurderPlayerV3(target);
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                    return true;
                }
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    Logger.Info($"{target.GetRealName()} 特务反杀：{killer.GetRealName()}", "SpecialAgent Kill");
                    return false;
                }
                var pg = IRandom.Instance;
                if (pg.Next(0, 100) < Options.SpecialAgentrobability.GetInt())
                    {
                        killer.SetRealKiller(target);
                        target.RpcMurderPlayerV3(killer);
                        Logger.Info($"{target.GetRealName()} 特务反杀：{killer.GetRealName()}", "SpecialAgent Kill");
                        return false;
                    }
                    break;
                case CustomRoles.Yandere:
                    if (!Main.NeedKillYandere.Contains(target.PlayerId))
                    {
                        killer.Data.IsDead = true;
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                        killer.RpcMurderPlayerV3(killer);
                        Main.PlayerStates[killer.PlayerId].SetDead();
                        Logger.Info($"{killer.GetRealName()} 击杀了非目标玩家，壮烈牺牲了（bushi）", "y");
                        return false;
                    }
                    break;
                case CustomRoles.Tom:
                    if (Main.TomKill[target.PlayerId] < Options.TomMax.GetInt())
                    {
                        CustomSoundsManager.RPCPlayCustomSoundAll("TomAAA");
                        Main.TomKill[target.PlayerId]++;
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                        target.RpcGuardAndKill(killer);
                        var tmpSpeed2 = Main.AllPlayerSpeed[target.PlayerId];
                        Main.AllPlayerSpeed[target.PlayerId] = Options.TomSpeed.GetInt();
                        target.MarkDirtySettings();
                        new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Options.TomSpeed.GetInt() + tmpSpeed2;
                            target.MarkDirtySettings();
                        }, Options.TomSecond.GetFloat(), "Trapper BlockMove");
                        return false;
                    }
                    break;
                case CustomRoles.NBakery:
                    Bakery.NBakeryKilledTasks(target.PlayerId);
                    break;
                case CustomRoles.Revolutionist:
                    killer.SetKillCooldown(Options.RevolutionistDrawTime.GetFloat());
                    if (!Main.isDraw[(killer.PlayerId, target.PlayerId)] && !Main.RevolutionistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.RevolutionistTimer.TryAdd(killer.PlayerId, (target, 0f));
                        Utils.NotifyRoles(SpecifySeer: __instance);
                        RPC.SetCurrentDrawTarget(killer.PlayerId, target.PlayerId);
                    }
                    return false;
                case CustomRoles.Farseer:
                    killer.SetKillCooldown(Farseer.FarseerRevealTime.GetFloat());
                    if (!Main.isRevealed[(killer.PlayerId, target.PlayerId)] && !Main.FarseerTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.FarseerTimer.TryAdd(killer.PlayerId, (target, 0f));
                        Utils.NotifyRoles(SpecifySeer: __instance);
                        RPC.SetCurrentRevealTarget(killer.PlayerId, target.PlayerId);
                    }
                    return false;
                case CustomRoles.Innocent:
                    target.RpcMurderPlayerV3(killer);
                    return false;
                case CustomRoles.Pelican:
                    if (Pelican.CanEat(killer, target.PlayerId))
                    {
                        Pelican.EatPlayer(killer, target);
                        killer.RpcGuardAndKill(killer);
                        killer.SetKillCooldown();
                        killer.RPCPlayCustomSound("Eat");
                        target.RPCPlayCustomSound("Eat");
                    }
                    return false;
                case CustomRoles.FFF:
                    if (!target.Is(CustomRoles.Lovers) && !target.Is(CustomRoles.Ntr))
                    {
                        killer.Data.IsDead = true;
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                        killer.RpcMurderPlayerV3(killer);
                        Main.PlayerStates[killer.PlayerId].SetDead();
                        Logger.Info($"{killer.GetRealName()} 击杀了非目标玩家，壮烈牺牲了（bushi）", "FFF");
                        return false;
                    }
                    break;
                case CustomRoles.Gamer:
                    Gamer.CheckGamerMurder(killer, target);
                    return false;
                case CustomRoles.DarkHide:
                    DarkHide.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Provocateur:
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                    killer.RpcMurderPlayerV3(target);
                    killer.RpcMurderPlayerV3(killer);
                    killer.SetRealKiller(target);
                    Main.Provoked.TryAdd(killer.PlayerId, target.PlayerId);
                    return false;
                case CustomRoles.Totocalcio:
                    Totocalcio.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.PlatonicLover:
                    PlatonicLover.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.ET:
                    ET.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Succubus:
                    Succubus.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.CursedSoul:
                    CursedSoul.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Infectious:
                    Infectious.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Monarch:
                    Monarch.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Deputy:
                    Deputy.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Jackal:
                    if (Jackal.OnCheckMurder(killer, target))
                    return false;
                    break;
                case CustomRoles.Solicited:
                    if (killer.Is(CustomRoles.Captain))
                    {
                        return true;
                    }
                    break;
                case CustomRoles.Shaman:
                    if (Main.ShamanTargetChoosen == false)
                    {
                        Main.ShamanTarget = target.PlayerId;
                        killer.RpcGuardAndKill(killer);
                        Main.ShamanTargetChoosen = true;
                    }
                    else killer.Notify(GetString("ShamanTargetAlreadySelected"));
                    return false;
                case CustomRoles.Luckey:
                if (target.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    return false;
                }
                var rd = IRandom.Instance;
                if (rd.Next(0, 100) < Options.LuckeyProbability.GetInt())
                    {
                    var Lc = IRandom.Instance;
                    if (Lc.Next(0, 100) < Options.LuckeyCanSeeKillility.GetInt())
                    {
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                        target.RpcGuardAndKill(killer);
                        return false;
                    }
                    else
                    {
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                }
                break;
                case CustomRoles.Exorcist:
                if (!Main.ForExorcist.Contains(target.PlayerId))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Exorcist), GetString("NotExorcist")));
                    return true;
                }
                else
                {
                    Main.ForExorcist.Remove(target.PlayerId);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    Main.ExorcistMax[killer.PlayerId]++;
                    if (Main.ExorcistMax[killer.PlayerId] >= Options.MaxExorcist.GetInt())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Exorcist);
                        CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
                    }
                    return true;
                }
                case CustomRoles.YinLang:
                    if (killer.Is(CustomRoles.YinLang))
                    {
                        if (YLdj >= 1 && YLdj <= 5)
                        {
                            if (killer.PlayerId != target.PlayerId)
                            {
                                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                                {
                                    var pos = target.transform.position;
                                    var dis = Vector2.Distance(pos, pc.transform.position);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    Logger.Info("银狼击杀开始", "YL");
                                    NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                                    Logger.Info($"{target.GetNameWithRole()} |系统警告| => {target.GetNameWithRole()}", "YinLang");
                                    var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                                    Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                                    ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                                    target.MarkDirtySettings();
                                    new LateTask(() =>
                                    {
                                        Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed;
                                        ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                                        target.MarkDirtySettings();
                                        RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                                    }, 10, "Trapper BlockMove");
                                    YLLevel += 1;
                                    YLCS = YinLang.YLSJ.GetInt();
                                    if (YLLevel == YinLang.YLSJ.GetInt())
                                    {
                                        YLdj += 1;
                                        YLLevel = 0;
                                    }
                                    killer.Notify(string.Format(GetString("YinLangLevel"), YLdj, YLLevel, YLCS - YLLevel));
                                    return false;
                                }
                            }
                        }
                        else if (YLdj >= 6 && YLdj <= 10)
                        {
                            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                            {
                                var pos = target.transform.position;
                                var dis = Vector2.Distance(pos, pc.transform.position);
                                killer.SetKillCooldownV2(target: target, forceAnime: true);
                                Logger.Info("银狼击杀开始", "YL");
                                NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang2")));
                                Logger.Info($"{target.GetNameWithRole()} |是否允许更改| => {target.GetNameWithRole()}", "YinLang");
                                Main.AllPlayerSpeed[target.PlayerId] = 0.01f;
                                var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                                ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                                target.MarkDirtySettings();
                                //    NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                                Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed - 0.01f;
                                ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                                target.MarkDirtySettings();
                                RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                                Main.AllPlayerSpeed[target.PlayerId] = 0.01f;
                                YLLevel += 1;
                                YLCS = YinLang.YLSJ.GetInt();
                                if (YLLevel == YinLang.YLSJ.GetInt())
                                {
                                    YLdj += 1;
                                    YLLevel = 0;
                                }
                                killer.Notify(string.Format(GetString("YinLangLevel"), YLdj, YLLevel, YLCS - YLLevel));
                                return false;
                            }
                        }
                        else
                        {
                            Utils.TP(killer.NetTransform, target.GetTruePosition());
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                            Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
                            target.SetRealKiller(killer);
                            Main.PlayerStates[target.PlayerId].SetDead();
                            target.RpcMurderPlayerV3(target);
                            killer.SetKillCooldownV2();
                            NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang3")));
                            killer.Notify(GetString("YinLangNotMax"));
                            return false;
                        }
                    }
                    return false;
                case CustomRoles.SchrodingerCat:
                    if (killer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.noteam == false)
                    {
                        return true;
                    }
                    if (target.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.noteam == true && (!target.Is(CustomRoles.Lovers)))
                    {
                        foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>())
                        {
                            if (role == CustomRoles.SchrodingerCat)
                            {
                                if (killer.GetCustomRole().IsCrewmate())
                                {
                                    SchrodingerCat.iscrew = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#ffffff");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#ffffff");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                else if (killer.GetCustomRole().IsImpostorTeam())
                                {
                                    SchrodingerCat.isimp = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#FF0000");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#FF0000");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#FF0000");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                else if (killer.Is(CustomRoles.BloodKnight))
                                {
                                    SchrodingerCat.isbk = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#630000");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#630000");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#630000");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                else if (killer.Is(CustomRoles.Gamer))
                                {
                                    SchrodingerCat.isgam = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#68bc71");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#68bc71");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#68bc71");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                if (killer.Is(CustomRoles.Jackal) || killer.Is(CustomRoles.Sidekick))
                                {
                                    SchrodingerCat.isjac = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#00b4eb");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#00b4eb");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#00b4eb");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                if (killer.Is(CustomRoles.YinLang))
                                {
                                    SchrodingerCat.isyl = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#6A5ACD");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#6A5ACD");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#6A5ACD");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                if (killer.Is(CustomRoles.PlaguesGod))
                                {
                                    SchrodingerCat.ispg = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#101010");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#101010");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#101010");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                if (killer.Is(CustomRoles.DarkHide))
                                {
                                    SchrodingerCat.isdh = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#483d8b");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#483d8b");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#483d8b");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                                if (killer.Is(CustomRoles.OpportunistKiller))
                                {
                                    SchrodingerCat.isok = true;
                                    SchrodingerCat.noteam = false;
                                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                                    Main.roleColors.TryAdd(role, "#CC6600");
                                    NameColorManager.Add(target.PlayerId, target.PlayerId, "#CC6600");
                                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#CC6600");
                                    target.RpcGuardAndKill(killer);
                                    killer.RpcGuardAndKill(target);
                                    return false;
                                }
                            }
                        }
                    }
                    else
                    {
                        return true;
                    }
                    break;
                case CustomRoles.RewardOfficer:
                    if (RewardOfficer.OnCheckMurder(killer, target))
                        return false;
                    break;
                //==========船员职业==========//
                case CustomRoles.Sheriff:
                    if (!Sheriff.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.SillySheriff:
                    SillySheriff.OnCheckMurder(killer, target, Process: "RemoveShotLimit");
                    if (!SillySheriff.OnCheckMurder(killer, target, Process: "Suicide"))
                        return false;
                    else if (target.Is(CustomRoles.LoveCutter))
                    {
                        LoveCutterGuard(killer, target);
                        return false;
                    }
                    break;
                case CustomRoles.Copycat:
                    if (!Copycat.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.SwordsMan:
                    if (!SwordsMan.OnCheckMurder(killer))
                        return false;
                    break;
                case CustomRoles.Medic:
                    Medic.OnCheckMurderForMedic(killer, target);
                    return false;
                case CustomRoles.Captain:
                    Captain.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Scout:
                    Scout.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.QSR:
                    QSR.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Counterfeiter:
                    if (Counterfeiter.CanBeClient(target) && Counterfeiter.CanSeel(killer.PlayerId))
                        Counterfeiter.SeelToClient(killer, target);
                    return false;
                case CustomRoles.Pursuer:
                    if (Pursuer.CanBeClient(target) && Pursuer.CanSeel(killer.PlayerId))
                        Pursuer.SeelToClient(killer, target);
                    return false;
            }
        }

        if (killer.Is(CustomRoles.Virus)) Virus.OnCheckMurder(killer, target);
        else if (killer.Is(CustomRoles.Spiritcaller)) Spiritcaller.OnCheckMurder(target);

        // 击杀前检查
        if (!killer.RpcCheckAndMurder(target, true))
            return false;
        if (Merchant.OnClientMurder(killer, target)) return false;

        // Don't infect when Shielded
        if (killer.Is(CustomRoles.Virus))
        {
            Virus.OnCheckMurder(killer, target);
        }
        // Consigliere
        if (killer.Is(CustomRoles.EvilDiviner))
        {

            if (!EvilDiviner.OnCheckMurder(killer, target))
                return false;
        }
        if (killer.Is(CustomRoles.Ritualist))
        {
            
            if (!Ritualist.OnCheckMurder(killer, target))
                return false;
        }
        //被起诉人上空包弹
        if (Main.QSRInProtect.Contains(killer.PlayerId))
            return false;

        if (killer.Is(CustomRoles.Swift) && !target.Is(CustomRoles.Glitch) && !target.Is(CustomRoles.Pestilence))
        {
            target.RpcMurderPlayerV3(target);
            killer.RpcGuardAndKill(killer);
        //    target.RpcGuardAndKill(target);
            target.SetRealKiller(killer);
                killer.SetKillCooldownV2();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                return false;
        }

        //凶手被传染
        if (Main.ForSourcePlague.Contains(target.PlayerId))
        {
            Main.ForSourcePlague.Add(killer.PlayerId);
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.SourcePlague))
                {
                    NameColorManager.Add(player.PlayerId, killer.PlayerId, "#999999");
                }
            }
        }

        //护士急救
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Nurse))
                {
                        if (target.PlayerId == pc.PlayerId || Main.NnurseHelepMax[pc.PlayerId] >= Options.NurseMax.GetInt() || !pc.IsAlive())
                            Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.NnurseHelepMax[pc.PlayerId]++;
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
                        Main.ForNnurse.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.NnurseHelep.Add(target.PlayerId);
                        Main.NnurseHelep.Add(pc.PlayerId);
                        killer.SetKillCooldownV2();
                        NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Nurse), GetString("HelpByNurse")));
                        var tmpSpeed1 = Main.AllPlayerSpeed[pc.PlayerId];
                        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                        var tmpSpeed2 = Main.AllPlayerSpeed[target.PlayerId];
                        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                        pc.MarkDirtySettings();
                        target.MarkDirtySettings();
                        new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[pc.PlayerId] = Main.AllPlayerSpeed[pc.PlayerId] - Main.MinSpeed + tmpSpeed1;
                            pc.MarkDirtySettings();
                            Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed2;
                            target.MarkDirtySettings();
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                            RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                            Main.NnurseHelep.Remove(target.PlayerId);
                            if (!pc.IsAlive())
                            {
                                target.RpcMurderPlayerV3(target);
                            }
                        }, Options.NurseSkillDuration.GetFloat(), "Trapper BlockMove");
                        return false;
                    }
                }
            }
        }

        if (Main.ForYandere.Contains(target.PlayerId))
        {
            foreach (var player in Main.AllAlivePlayerControls)
            {
                if (player.Is(CustomRoles.Yandere))
                {
                    player.RpcMurderPlayerV3(player);
                }
            }
        }

        // 清道夫清理尸体
        if (killer.Is(CustomRoles.Scavenger))
        {
            Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
            target.SetRealKiller(killer);
            Main.PlayerStates[target.PlayerId].SetDead();
            target.RpcMurderPlayerV3(target);
            killer.SetKillCooldown();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByScavenger")));
            return false;
        }

        // 肢解者肢解受害者
        if (killer.Is(CustomRoles.OverKiller) && killer.PlayerId != target.PlayerId)
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Dismembered;
            new LateTask(() =>
            {
                if (!Main.OverDeadPlayerList.Contains(target.PlayerId)) Main.OverDeadPlayerList.Add(target.PlayerId);
                var ops = target.GetTruePosition();
                var rd = IRandom.Instance;
                for (int i = 0; i < 20; i++)
                {
                    Vector2 location = new(ops.x + ((float)(rd.Next(0, 201) - 100) / 100), ops.y + ((float)(rd.Next(0, 201) - 100) / 100));
                    location += new Vector2(0, 0.3636f);

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None, -1);
                    NetHelpers.WriteVector2(location, writer);
                    writer.Write(target.NetTransform.lastSequenceId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    target.NetTransform.SnapTo(location);
                    killer.MurderPlayer(target);

                    if (target.Is(CustomRoles.Avanger))
                    {
                        var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId || Pelican.IsEaten(x.PlayerId) || Medic.ProtectList.Contains(x.PlayerId)).ToList();
                        var rp = pcList[IRandom.Instance.Next(0, pcList.Count)];
                        Main.PlayerStates[rp.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                        rp.SetRealKiller(target);
                        rp.RpcMurderPlayerV3(rp);
                    }

                    MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.None, -1);
                    messageWriter.WriteNetObject(target);
                    AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                }
                Utils.TP(killer.NetTransform, ops);
            }, 0.05f, "OverKiller Murder");
        }

        //==キル処理==
        __instance.RpcMurderPlayerV3(target);
        //============

        return false;
    }

    private static void LoveCutterGuard(PlayerControl killer, PlayerControl target)
    {
        target.RpcGuardAndKill(target);
        target.RpcGuardAndKill(target);
        Main.LoveCutterKilledCount[target.PlayerId] += 1;
        RPC.SendRPCLoveCutterGuard(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: target);
        Logger.Info($"{target.GetNameWithRole()} : {Main.LoveCutterKilledCount[target.PlayerId]}回目", "LoveCutter");
        NameColorManager.Add(target.PlayerId, target.PlayerId, Utils.GetRoleColorCode(CustomRoles.LoveCutter));
        Utils.CheckLoveCutterWin(target.Data);
    }

    public static bool RpcCheckAndMurder(PlayerControl killer, PlayerControl target, bool check = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (target == null) target = killer;

        //被外星人干扰
        if (Main.ForET.Contains(killer.PlayerId))
        {
            killer.SetKillCooldownV2(target: target, forceAnime: true);
            return false;
        }

        //Jackal can kill Sidekick
        if (killer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick) && !Options.JackalCanKillSidekick.GetBool())
            return false;
        //Sidekick can kill Jackal
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal) && !Options.SidekickCanKillJackal.GetBool())
            return false;
        //禁止内鬼刀叛徒
        if (killer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && !Options.ImpCanKillMadmate.GetBool())
            return false;

        // Guardian can't die on task completion
        if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted())
            return false;

        // Traitor can't kill Impostors but Impostors can kill it
        if (killer.Is(CustomRoles.Traitor) && target.Is(CustomRoleTypes.Impostor))
            return false;

        // Refugee
        if (killer.Is(CustomRoles.Refugee) && target.Is(CustomRoles.Refugee))
            return false;

        // Romantic partner is protected
        if (Romantic.BetPlayer.ContainsValue(target.PlayerId) && Romantic.isPartnerProtected) return false;

        //禁止叛徒刀内鬼
        if (killer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && !Options.MadmateCanKillImp.GetBool())
            return false;
        //Bitten players cannot kill Vampire
        if (killer.Is(CustomRoles.Infected) && target.Is(CustomRoles.Infectious))
            return false;
        //Vampire cannot kill bitten players
        if (killer.Is(CustomRoles.Infectious) && target.Is(CustomRoles.Infected))
            return false;
        //Bitten players cannot kill each other
        if (killer.Is(CustomRoles.Infected) && target.Is(CustomRoles.Infected) && !Infectious.TargetKnowOtherTarget.GetBool())
            return false;
        //Sidekick can kill Sidekick
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && !Options.SidekickCanKillSidekick.GetBool())
            return false;

        //Rogue can kill Rogue
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && !Options.RogueKnowEachOther.GetBool())
            return false;

        //Coven
        if (killer.GetCustomRole().IsCoven() && target.GetCustomRole().IsCoven())
            return false;

        //医生护盾检查
        if (Medic.OnCheckMurder(killer, target))
            return false;

        if (target.Is(CustomRoles.Medic))
            Medic.IsDead(target);

        if (PlagueBearer.OnCheckMurderPestilence(killer, target))
            return false;

        if (Jackal.ResetKillCooldownWhenSbGetKilled.GetBool() && !killer.Is(CustomRoles.Jackal) && !target.Is(CustomRoles.Jackal) && !GameStates.IsMeeting)
            Jackal.AfterPlayerDiedTask(killer);

        if (target.Is(CustomRoles.BoobyTrap) && Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && !GameStates.IsMeeting)
        {
            Main.BoobyTrapBody.Add(target.PlayerId);
            Main.BoobyTrapKiller.Add(target.PlayerId);
        }

        if (target.Is(CustomRoles.Lucky))
        {
            var rd = IRandom.Instance;
            if (rd.Next(0, 100) < Options.LuckyProbability.GetInt())
            {
                killer.RpcGuardAndKill(target);
                return false;
            }
        }

        switch (target.GetCustomRole())
        {
            //击杀幸运儿
            case CustomRoles.Luckey:
                var rd = IRandom.Instance;
                if (rd.Next(0, 100) < Options.LuckeyProbability.GetInt())
                {
                    killer.RpcGuardAndKill(target);
                    return false;
                }
                break;
            //击杀呪狼
            case CustomRoles.CursedWolf:
                if (Main.CursedWolfSpellCount[target.PlayerId] <= 0) break;
                killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(target);
                Main.CursedWolfSpellCount[target.PlayerId] -= 1;
                RPC.SendRPCCursedWolfSpellCount(target.PlayerId);
                Logger.Info($"{target.GetNameWithRole()} : {Main.CursedWolfSpellCount[target.PlayerId]}回目", "CursedWolf");
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Spell;
                killer.RpcMurderPlayerV3(killer);
                return false;
            case CustomRoles.Jinx:
                if (Main.JinxSpellCount[target.PlayerId] <= 0) break;
                killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(target);
                Main.JinxSpellCount[target.PlayerId] -= 1;
                RPC.SendRPCJinxSpellCount(target.PlayerId);
                Logger.Info($"{target.GetNameWithRole()} : {Main.JinxSpellCount[target.PlayerId]}回目", "Jinx");
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Jinx;
                killer.RpcMurderPlayerV3(killer);
                return false;
            //击杀老兵
            case CustomRoles.Veteran:
                if (Main.VeteranInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                    if (Main.VeteranInProtect[target.PlayerId] + Options.VeteranSkillDuration.GetInt() >= Utils.GetTimeStamp())
                    {
                        killer.SetRealKiller(target);
                        target.RpcMurderPlayerV3(killer);
                        Logger.Info($"{target.GetRealName()} 老兵反弹击杀：{killer.GetRealName()}", "Veteran Kill");
                        return false;
                    }
                break;
            //检查明星附近是否有人
            case CustomRoles.SuperStar:
                if (Main.AllAlivePlayerControls.Where(x =>
                    x.PlayerId != killer.PlayerId &&
                    x.PlayerId != target.PlayerId &&
                    Vector2.Distance(x.GetTruePosition(), target.GetTruePosition()) < 2f
                    ).ToList().Count >= 1) return false;
                break;
            //玩家被击杀事件
            case CustomRoles.Gamer:
                if (!Gamer.CheckMurder(killer, target))
                    return false;
                break;
            //嗜血骑士技能生效中
            case CustomRoles.BloodKnight:
                if (BloodKnight.InProtect(target.PlayerId))
                {
                    killer.RpcGuardAndKill(target);
                    target.RpcGuardAndKill();
                    target.Notify(GetString("BKOffsetKill"));
                    return false;
                }
                break;
            case CustomRoles.Banshee:
                if (Banshee.InProtect(target.PlayerId))
                {
                    killer.RpcGuardAndKill(target);
                    target.RpcGuardAndKill();
                    target.Notify(GetString("BKOffsetKill"));
                    return false;
                }
                break;
                //击杀挑衅者
                case CustomRoles.Rudepeople:
                    if (Main.RudepeopleInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                        if (Main.RudepeopleInProtect[target.PlayerId] + Options.RudepeopleSkillDuration.GetInt() >= Utils.GetTimeStamp(DateTime.Now))
                        {
                            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                            killer.RpcMurderPlayerV3(target);
                            killer.RpcMurderPlayerV3(killer);
                            killer.SetRealKiller(target);
                        CustomWinnerHolder.WinnerIds.Remove(killer.PlayerId);
                        return false;
                        }
                    break;
                //击杀银狼
                case CustomRoles.YinLang:
                    killer.RPCPlayCustomSound("ylkq");
                    Logger.Info("银狼被杀，开始执行114514", "YinLang");
                    Main.AllPlayerKillCooldown[killer.PlayerId] = 114514f;
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang4")));
                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                {
                    var pos = target.transform.position;
                    var dis = Vector2.Distance(pos, pc.transform.position);
                    Logger.Info("执行减速", "YL");
                    //NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang2")));
                    Logger.Info($"{killer.GetNameWithRole()} |是否允许更改| => {killer.GetNameWithRole()}", "YinLang");
                    Main.AllPlayerSpeed[killer.PlayerId] = 0.01f;
                    var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
                    Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                    ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
                    killer.MarkDirtySettings();
                    //    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                    Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed - 0.01f;
                    ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                    killer.MarkDirtySettings();
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                    Main.AllPlayerSpeed[killer.PlayerId] = 0.01f;
                    break;
                }
                break;
            case CustomRoles.QX:
                var shields = Options.QXShields.GetInt();
                if (killer.Is(CustomRoles.QX) && shields > 0)
                {
                    shields--;
                    return false;
                }
                if (target.Is(CustomRoles.QX) && shields > 0)
                {
                    killer.RpcGuardAndKill(killer);
                    shields--;
                    return false;
                }
                if (killer.Is(CustomRoles.QX) && shields == 0)
                {
                    killer.RpcMurderPlayerV3(killer);
                    return false;
                }
                break;
            case CustomRoles.Mascot:
                if (target.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    killer.RpcMurderPlayerV3(killer);
                    return false;
                }
                var rdmas = IRandom.Instance;
                if (rdmas.Next(0, 100) < Options.MascotPro.GetInt())
                {
                    var pcList1 = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId).ToList();
                    var Fr1 = pcList1[IRandom.Instance.Next(0, pcList1.Count)];

                    if (Options.MascotKiller.GetBool() == true)
                    {
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        killer.SetRealKiller(killer);
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                    else if (Fr1.GetCustomRole().IsMascot())
                    {
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        killer.SetRealKiller(killer);
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                    else
                    {
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        Fr1.SetRealKiller(Fr1);
                        Fr1.RpcMurderPlayerV3(Fr1);
                    }
                }
                break;
            case CustomRoles.Wildling:
                if (Wildling.InProtect(target.PlayerId))
                {
                    killer.RpcGuardAndKill(target);
                    target.RpcGuardAndKill();
                    target.Notify(GetString("BKOffsetKill"));
                    return false;
                }
                break;
            case CustomRoles.Spiritcaller:
                if (Spiritcaller.InProtect(target))
                {
                    killer.RpcGuardAndKill(target);
                    target.RpcGuardAndKill();
                    return false;
                }
                break;
        }

        //妖魔鬼怪快离开
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Exorcist))
                {
                    Main.ForExorcist.Add(killer.PlayerId);
                    RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);
                    NameColorManager.Add(pc.PlayerId, killer.PlayerId, "#FF0000");
                }
            }
        }
        //牛仔套圈
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Cowboy))
                {
                    if (Main.MaxCowboy[pc.PlayerId] >= Options.CowboyMax.GetInt() || pc.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
                        Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.MaxCowboy[pc.PlayerId]++;
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);

                        new LateTask(() =>
                        {
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            pc.RpcGuardAndKill();
                            Utils.NotifyRoles();
                        }, 0.2f, ("Come!"));
                        return false;
                    }                   
                }
            }
        }
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.GuideKillRadius.GetFloat()) continue;
                if (pc.Is(CustomRoles.Guide) && killer.GetCustomRole().IsImpostor() && Main.GuideMax[pc.PlayerId] <= Options.GuideKillMax.GetInt())
                {
                    Main.GuideMax[pc.PlayerId]++;
                    foreach (var player in Main.AllAlivePlayerControls)
                    {
                        if (!player.IsModClient()) player.KillFlash();
                        if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                        if (player == killer) continue;
                        if (player == pc) continue;
                        if (player == target) continue;
                        if (Vector2.Distance(pc.transform.position, player.transform.position) <= Options.GuideKillRadius.GetFloat())
                        {
                            Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.ForGuide;
                            player.SetRealKiller(pc);
                            killer.RpcMurderPlayerV3(player);
                        }
                    }
                }
            }
        }
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Jealousy))
                {
                    if (killer.PlayerId == pc.PlayerId && Main.ForJealousy.Contains(target.PlayerId))
                    {
                        pc.ResetKillCooldown();
                    }         
                    else
                    {
                        Main.ForJealousy.Add(killer.PlayerId);
                        if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
                        var Jealousy = killer.PlayerId;
                        pc.Notify(string.Format(GetString("ForJealousy!!!"), Main.AllPlayerNames[Jealousy]));
                        NameColorManager.Add(pc.PlayerId, killer.PlayerId, "#996666");
                        pc.RPCPlayCustomSound("anagry");
                    }  
                }
            }
        }
        if (killer.PlayerId != target.PlayerId)
        {
            if (killer.Is(CustomRoles.Hemophobia) && !Main.ForHemophobia.Contains(killer.PlayerId)) return false;
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.HemophobiaRadius.GetFloat()) continue;
                
                if (pc.Is(CustomRoles.Hemophobia))
                {
                    if (killer.PlayerId == pc.PlayerId)
                        Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.HemophobiaInKill.Remove(pc.PlayerId);
                        Main.HemophobiaInKill.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.ForHemophobia.Add(pc.PlayerId);
                        if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
                        pc.Notify(GetString("HemophobiaOnGuard"), Options.HemophobiaSeconds.GetFloat());
                    }                    
                }                   
            }
        }
        //保镖保护
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.BodyguardProtectRadius.GetFloat()) continue;
                if (pc.Is(CustomRoles.Bodyguard))
                {
                    if (pc.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
                        Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                        pc.RpcMurderPlayerV3(killer);
                        pc.SetRealKiller(killer);
                        pc.RpcMurderPlayerV3(pc);
                        Logger.Info($"{pc.GetRealName()} 挺身而出与歹徒 {killer.GetRealName()} 同归于尽", "Bodyguard");
                        return false;
                    }
                }
            }
        }

        //首刀保护
        if (Main.ShieldPlayer != byte.MaxValue && Main.ShieldPlayer == target.PlayerId && Utils.IsAllAlive)
        {
            Main.ShieldPlayer = byte.MaxValue;
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill();
            return false;
        }

        //首刀叛变
        if (Options.MadmateSpawnMode.GetInt() == 1 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && Utils.CanBeMadmate(target))
        {
            Main.MadmateNum++;
            target.RpcSetCustomRole(CustomRoles.Madmate);
            ExtendedPlayerControl.RpcSetCustomRole(target.PlayerId, CustomRoles.Madmate);
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
            Logger.Info("设置职业:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Madmate.ToString(), "Assign " + CustomRoles.Madmate.ToString());
            return false;
        }

        if (!check) killer.RpcMurderPlayerV3(target);
        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
class MurderPlayerPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}{(target.protectedByGuardian ? "(Protected)" : "")}", "MurderPlayer");

        if (RandomSpawn.CustomNetworkTransformPatch.NumOfTP.TryGetValue(__instance.PlayerId, out var num) && num > 2) RandomSpawn.CustomNetworkTransformPatch.NumOfTP[__instance.PlayerId] = 3;
        if (!target.protectedByGuardian)
            Camouflage.RpcSetSkin(target, ForceRevert: true);
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();
        if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;

        if (Main.OverDeadPlayerList.Contains(target.PlayerId)) return;

        PlayerControl killer = __instance; //読み替え変数

        //実際のキラーとkillerが違う場合の入れ替え処理
        if (Sniper.IsEnable)
        {
            if (Sniper.TryGetSniper(target.PlayerId, ref killer))
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Sniped;
            }
        }
        if (killer != __instance)
        {
            Logger.Info($"Real Killer={killer.GetNameWithRole()}", "MurderPlayer");

        }
        if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.etc)
        {
            //死因が設定されていない場合は死亡判定
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
        }

        //看看UP是不是被首刀了
        if (Main.FirstDied == byte.MaxValue && target.Is(CustomRoles.Youtuber))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Youtuber); //UP主被首刀了，哈哈哈哈哈
            CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
        }

        //冤魂！
        if (Main.FirstDied == byte.MaxValue && target.GetCustomRole().IsCrewmate() && !target.CanUseKillButton() && Options.CanWronged.GetBool())
        {
            target.RpcSetCustomRole(CustomRoles.Wronged);
            var taskState = target.GetPlayerTaskState();
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
            taskState.CompletedTasksCount++;
        }

        //记录首刀
        if (Main.FirstDied == byte.MaxValue)
            Main.FirstDied = target.PlayerId;

        if (target.Is(CustomRoles.Bait))
        {
            if (killer.PlayerId != target.PlayerId || (target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith) || !killer.Is(CustomRoles.Oblivious) || (killer.Is(CustomRoles.Oblivious) && !Options.ObliviousBaitImmune.GetBool()))
            {
                killer.RPCPlayCustomSound("Congrats");
                target.RPCPlayCustomSound("Congrats");
                float delay;
                if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat()) delay = 0f;
                else delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);
                delay = Math.Max(delay, 0.15f);
                if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                Logger.Info($"{killer.GetNameWithRole()} 击杀诱饵 => {target.GetNameWithRole()}", "MurderPlayer");
                new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Bait Self Report");
            }
        }

        if (target.Is(CustomRoles.Burst) && !killer.Data.IsDead)
        {
            target.SetRealKiller(killer);
            Main.BurstBodies.Add(target.PlayerId);
            if (killer.PlayerId != target.PlayerId && !killer.Is(CustomRoles.Pestilence))
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Burst), GetString("BurstNotify")));
                new LateTask(() =>
                {
                    if (!killer.inVent && !killer.Data.IsDead && !GameStates.IsMeeting)
                    {
                        target.RpcMurderPlayerV3(killer);
                        killer.SetRealKiller(target);
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                    }
                    else
                    {
                        killer.RpcGuardAndKill();
                        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Burst), GetString("BurstFailed")));                        
                    }
                    Main.BurstBodies.Remove(target.PlayerId);
                }, Options.BurstKillDelay.GetFloat(), "Burst Suicide");
            }   
        }

        if (target.Is(CustomRoles.Trapper) && killer != target)
            killer.TrapperKilled(target);

        switch (target.GetCustomRole())
        {
            case CustomRoles.BallLightning:
                if (killer != target)
                    BallLightning.MurderPlayer(killer, target);
                break;
        }
        switch (killer.GetCustomRole())
        {
            case CustomRoles.BoobyTrap:
                if (!Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && killer != target)
                {
                    if (!Main.BoobyTrapBody.Contains(target.PlayerId)) Main.BoobyTrapBody.Add(target.PlayerId);
                    if (!Main.KillerOfBoobyTrapBody.ContainsKey(target.PlayerId)) Main.KillerOfBoobyTrapBody.Add(target.PlayerId, killer.PlayerId);
                    Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
                    killer.RpcMurderPlayerV3(killer);
                }
                break;
            case CustomRoles.SwordsMan:
                if (killer != target)
                    SwordsMan.OnMurder(killer);
                break;
            case CustomRoles.BloodKnight:
                BloodKnight.OnMurderPlayer(killer, target);
                break;
            case CustomRoles.Wildling:
                Wildling.OnMurderPlayer(killer, target);
                break;
            case CustomRoles.Banshee:
                Banshee.OnMurderPlayer(killer, target);
                break;
        }

        if (killer.Is(CustomRoles.TicketsStealer) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("TicketsStealerGetTicket"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Options.TicketsPerKill.GetFloat()).ToString("0.0#####")));
        
        if (killer.Is(CustomRoles.Pickpocket) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("PickpocketGetVote"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Pickpocket.VotesPerKill.GetFloat()).ToString("0.0#####")));

        if (target.Is(CustomRoles.Avanger))
        {
            var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToList();
            var rp = pcList[IRandom.Instance.Next(0, pcList.Count)];
            Main.PlayerStates[rp.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
            rp.SetRealKiller(target);
            rp.RpcMurderPlayerV3(rp);
        }

        foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Mediumshiper)))
            pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshiperKnowPlayerDead")));

        if (Executioner.Target.ContainsValue(target.PlayerId))
            Executioner.ChangeRoleByTarget(target);
        if (Lawyer.Target.ContainsValue(target.PlayerId))
            Lawyer.ChangeRoleByTarget(target);
        if (Yandere.Target.ContainsValue(target.PlayerId))
            Yandere.ChangeRoleByTarget(target);
        //Yandere.OnPlayerDead(target);
        Hacker.AddDeadBody(target);
        Mortician.OnPlayerDead(target);
        Bloodhound.OnPlayerDead(target);
        Tracefinder.OnPlayerDead(target);
        Vulture.OnPlayerDead(target);

        Utils.AfterPlayerDeathTasks(target);

        Main.PlayerStates[target.PlayerId].SetDead();
        target.SetRealKiller(killer, true); //既に追加されてたらスキップ
        Utils.CountAlivePlayers(true);

        Camouflager.isDead(target);
        Utils.TargetDies(__instance, target);

        if (Options.LowLoadMode.GetBool())
        {
            __instance.MarkDirtySettings();
            target.MarkDirtySettings();
            Utils.NotifyRoles(killer);
            Utils.NotifyRoles(target);
        }
        else
        {
            Utils.SyncAllSettings();
            Utils.NotifyRoles();
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
class ShapeshiftPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance?.GetNameWithRole()} => {target?.GetNameWithRole()}", "Shapeshift");

        var shapeshifter = __instance;
        var shapeshifting = shapeshifter.PlayerId != target.PlayerId;

        if (Main.CheckShapeshift.TryGetValue(shapeshifter.PlayerId, out var last) && last == shapeshifting)
        {
            Logger.Info($"{__instance?.GetNameWithRole()}:Cancel Shapeshift.Prefix", "Shapeshift");
            return;
        }

        Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;
        Main.ShapeshiftTarget[shapeshifter.PlayerId] = target.PlayerId;

        Sniper.OnShapeshift(shapeshifter, shapeshifting);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!shapeshifting) Camouflage.RpcSetSkin(__instance);

        if (Pelican.IsEaten(shapeshifter.PlayerId) || GameStates.IsVoting)
            goto End;

        switch (shapeshifter.GetCustomRole())
        {
            case CustomRoles.EvilTracker:
                EvilTracker.OnShapeshift(shapeshifter, target, shapeshifting);
                break;
            case CustomRoles.FireWorks:
                FireWorks.ShapeShiftState(shapeshifter, shapeshifting);
                break;
            case CustomRoles.Warlock:
                if (Main.CursedPlayers[shapeshifter.PlayerId] != null)//呪われた人がいるか確認
                {
                    if (shapeshifting && !Main.CursedPlayers[shapeshifter.PlayerId].Data.IsDead)//変身解除の時に反応しない
                    {
                        var cp = Main.CursedPlayers[shapeshifter.PlayerId];
                        Vector2 cppos = cp.transform.position;//呪われた人の位置
                        Dictionary<PlayerControl, float> cpdistance = new();
                        float dis;
                        foreach (PlayerControl p in Main.AllAlivePlayerControls)
                        {
                            if (p.PlayerId == cp.PlayerId) continue;
                            if (!Options.WarlockCanKillSelf.GetBool() && p.PlayerId == shapeshifter.PlayerId) continue;
                            if (!Options.WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;
                            if (Pelican.IsEaten(p.PlayerId) || Medic.ProtectList.Contains(p.PlayerId)) continue;
                            dis = Vector2.Distance(cppos, p.transform.position);
                            cpdistance.Add(p, dis);
                            Logger.Info($"{p?.Data?.PlayerName}の位置{dis}", "Warlock");
                        }
                        if (cpdistance.Count >= 1)
                        {
                            var min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();//一番小さい値を取り出す
                            PlayerControl targetw = min.Key;
                            if (cp.RpcCheckAndMurder(targetw, true))
                            {
                                targetw.SetRealKiller(shapeshifter);
                                Logger.Info($"{targetw.GetNameWithRole()}was killed", "Warlock");
                                cp.RpcMurderPlayerV3(targetw);//殺す
                                shapeshifter.RpcGuardAndKill(shapeshifter);
                                shapeshifter.Notify(GetString("WarlockControlKill"));
                            }
                        }
                        else
                        {
                            shapeshifter.Notify(GetString("WarlockNoTarget"));
                        }
                        Main.isCurseAndKill[shapeshifter.PlayerId] = false;
                    }
                    Main.CursedPlayers[shapeshifter.PlayerId] = null;
                }
                break;
            case CustomRoles.Escapee:
                if (shapeshifting)
                {
                    if (Main.EscapeeLocation.ContainsKey(shapeshifter.PlayerId))
                    {
                        var position = Main.EscapeeLocation[shapeshifter.PlayerId];
                        Main.EscapeeLocation.Remove(shapeshifter.PlayerId);
                        Logger.Msg($"{shapeshifter.GetNameWithRole()}:{position}", "EscapeeTeleport");
                        Utils.TP(shapeshifter.NetTransform, position);
                        shapeshifter.RPCPlayCustomSound("Teleport");
                    }
                    else
                    {
                        Main.EscapeeLocation.Add(shapeshifter.PlayerId, shapeshifter.GetTruePosition());
                    }
                }
                break;
            case CustomRoles.Swapper:
                Main.isCursed = false;
                break;
            case CustomRoles.Amnesiac:
                Main.isCursed = false;
                break;
            case CustomRoles.Miner:
                if (Main.LastEnteredVent.ContainsKey(shapeshifter.PlayerId))
                {
                    int ventId = Main.LastEnteredVent[shapeshifter.PlayerId].Id;
                    var vent = Main.LastEnteredVent[shapeshifter.PlayerId];
                    var position = Main.LastEnteredVentLocation[shapeshifter.PlayerId];
                    Logger.Msg($"{shapeshifter.GetNameWithRole()}:{position}", "MinerTeleport");
                    Utils.TP(shapeshifter.NetTransform, new Vector2(position.x, position.y));
                }
                break;
            case CustomRoles.Bomber:
                if (shapeshifting)
                {
                    Logger.Info("炸弹爆炸了", "Boom");
                    CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                    foreach (var tg in Main.AllPlayerControls)
                    {
                        if (!tg.IsModClient()) tg.KillFlash();
                        var pos = shapeshifter.transform.position;
                        var dis = Vector2.Distance(pos, tg.transform.position);

                        if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId)) continue;
                        if (dis > Options.BomberRadius.GetFloat()) continue;
                        if (tg.PlayerId == shapeshifter.PlayerId) continue;

                        Main.PlayerStates[tg.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        tg.SetRealKiller(shapeshifter);
                        tg.RpcMurderPlayerV3(tg);
                        Medic.IsDead(tg);
                    }
                    new LateTask(() =>
                    {
                        var totalAlive = Main.AllAlivePlayerControls.Count();
                        //自分が最後の生き残りの場合は勝利のために死なない
                        if (totalAlive > 0 && !GameStates.IsEnded)
                        {
                            Main.PlayerStates[shapeshifter.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
                            shapeshifter.RpcMurderPlayerV3(shapeshifter);
                        }
                        Utils.NotifyRoles();
                    }, 1.5f, "Bomber Suiscide");
                }
                break;
            case CustomRoles.Assassin:
                Assassin.OnShapeshift(shapeshifter, shapeshifting);
                break;
            case CustomRoles.ImperiusCurse:
                if (shapeshifting)
                {
                    new LateTask(() =>
                    {
                        if (!(!GameStates.IsInTask || !shapeshifter.IsAlive() || !target.IsAlive() || shapeshifter.inVent || target.inVent))
                        {
                            var originPs = target.GetTruePosition();
                            Utils.TP(target.NetTransform, shapeshifter.GetTruePosition());
                            Utils.TP(shapeshifter.NetTransform, originPs);
                        }
                    }, 1.5f, "ImperiusCurse TP");
                }
                break;
            case CustomRoles.QuickShooter:
                QuickShooter.OnShapeshift(shapeshifter, shapeshifting);
                break;
            case CustomRoles.Camouflager:
                if (shapeshifting)
                    Camouflager.OnShapeshift();
                if (!shapeshifting)
                    Camouflager.OnReportDeadBody();
                break;
            case CustomRoles.Hacker:
                Hacker.OnShapeshift(shapeshifter, shapeshifting, target);
                break;
            case CustomRoles.Disperser:
                if (shapeshifting)
                    Disperser.DispersePlayers(shapeshifter);
                break;
            case CustomRoles.Dazzler:
                if (shapeshifting)
                    Dazzler.OnShapeshift(shapeshifter, target);
                break;
            case CustomRoles.Deathpact:
                if (shapeshifting) 
                    Deathpact.OnShapeshift(shapeshifter, target);
                break;
            case CustomRoles.Devourer:
                if (shapeshifting) 
                    Devourer.OnShapeshift(shapeshifter, target);
                break;
            case CustomRoles.Twister:
                Twister.TwistPlayers(shapeshifter);
                break;
            case CustomRoles.Blackmailer:
                if (shapeshifting)
                {
                    if (!target.IsAlive())
                    {
                        NameNotifyManager.Notify(__instance, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("NotAssassin")));
                        break;
                    }
                    Blackmailer.ForBlackmailer.Add(target.PlayerId);
                }
                break;
        }

    End:

        //変身解除のタイミングがずれて名前が直せなかった時のために強制書き換え
        if (!shapeshifting)
        {
            new LateTask(() =>
            {
                Utils.NotifyRoles(NoCache: true);
            },
            1.2f, "ShapeShiftNotify");
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport;
    public static Dictionary<byte, List<GameData.PlayerInfo>> WaitReport = new();
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
    {
        if (GameStates.IsMeeting) return false;
        if (Options.DisableMeeting.GetBool()) return false;
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat) return false;
        if (Options.CurrentGameMode == CustomGameMode.HotPotato) return false;
        if (!CanReport[__instance.PlayerId])
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole()}:通報禁止中のため可能になるまで待機します", "ReportDeadBody");
            return false;
        }

        Logger.Info($"{__instance.GetNameWithRole()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");

        foreach (var kvp in Main.PlayerStates)
        {
            var pc = Utils.GetPlayerById(kvp.Key);
            kvp.Value.LastRoom = pc.GetPlainShipRoom();
        }

        if (!AmongUsClient.Instance.AmHost) return true;

        try
        {
            //通報者が死んでいる場合、本処理で会議がキャンセルされるのでここで止める
            if (__instance.Data.IsDead) return false;

            //=============================================
            //以下、检查是否允许本次会议
            //=============================================

            var killer = target?.Object?.GetRealKiller();
            var killerRole = killer?.GetCustomRole();

            //杀戮机器无法报告或拍灯
            if (__instance.Is(CustomRoles.Minimalism)) return false;
            //禁止小黑人报告
            if (((Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive) && Options.DisableReportWhenCC.GetBool()) return false;

            if (target == null) //拍灯事件
            {
                if (__instance.Is(CustomRoles.Jester) && !Options.JesterCanUseButton.GetBool()) return false;
                if (__instance.Is(CustomRoles.EIReverso))
                    {
                    __instance?.NoCheckStartMeeting(__instance?.Data);
                    return false;
                    }
            }
            if (target != null) //拍灯事件
            {
                if (Bloodhound.UnreportablePlayers.Contains(target.PlayerId)) return false;
                if (Vulture.UnreportablePlayers.Contains(target.PlayerId)) return false;

                if (__instance.Is(CustomRoles.Bloodhound) && !Copycat.playerIdList.Contains(__instance.PlayerId))
                {
                    if (killer != null)
                    {
                        Bloodhound.OnReportDeadBody(__instance, target, killer);
                    }
                    else
                    {
                        __instance.Notify(GetString("BloodhoundNoTrack"));
                    }
                    
                    return false;
                }
                if (Vulture.UnreportablePlayers.Contains(target.PlayerId)) return false;

                if (__instance.Is(CustomRoles.Vulture))
                {
                    Vulture.OnReportDeadBody(__instance, target);
                    __instance.RpcGuardAndKill(__instance);
                    __instance.Notify(GetString("VultureReportBody"));
                    Logger.Info($"{__instance.GetRealName()} ate {target.PlayerName} corpse", "Vulture");
                    return false;
                }
                // 失忆者无法报告尸体
                if (__instance.Is(CustomRoles.Amnesiac))
                {
                    Amnesiac.OnReportDeadBody(__instance, target);
                    Logger.Info("失忆者正常进入无法报告", "Amnesiac");
                    return false;
                }
                // 失忆者无法报告尸体第二次尝试阻止！！！
                if (__instance.Is(CustomRoles.Amnesiac))
                {
                    Amnesiac.OnReportDeadBody(__instance, target);
                    Logger.Info("失忆者正常进入无法报告--第二次尝试阻止", "Amnesiac");
                    return false;
                }
                //反转侠报告尸体
                if (__instance.Is(CustomRoles.EIReverso))
                {
                    __instance?.ReportDeadBody(null);
                    return false;
                }
                // 清洁工来扫大街咯
                if (__instance.Is(CustomRoles.Cleaner))
                {
                    Main.CleanerBodies.Remove(target.PlayerId);
                    Main.CleanerBodies.Add(target.PlayerId);
                    __instance.RpcGuardAndKill(__instance);
                    __instance.Notify(GetString("CleanerCleanBody"));
                //  __instance.ResetKillCooldown();
                    __instance.SetKillCooldownV3(Options.KillCooldownAfterCleaning.GetFloat());
                    Logger.Info($"{__instance.GetRealName()} 清理了 {target.PlayerName} 的尸体", "Cleaner");
                    return false;
                }
                if (__instance.Is(CustomRoles.Medusa))
                {
                    Main.MedusaBodies.Remove(target.PlayerId);
                    Main.MedusaBodies.Add(target.PlayerId);
                    __instance.RpcGuardAndKill(__instance);
                    __instance.Notify(GetString("MedusaStoneBody"));
              //      __instance.ResetKillCooldown();
                    __instance.SetKillCooldownV3(Medusa.KillCooldownAfterStoneGazing.GetFloat());
                    Logger.Info($"{__instance.GetRealName()} stoned {target.PlayerName} body", "Medusa");
                    return false;
                }

                // 清洁工来扫大街咯
                if (__instance.Is(CustomRoles.Janitor))
                {
                    Main.JanitorBodies.Remove(target.PlayerId);
                    Main.JanitorBodies.Add(target.PlayerId);
                    __instance.RpcGuardAndKill(__instance);
                    __instance.Notify(GetString("JanitorCleanBody"));
                //  __instance.ResetKillCooldown();
                    __instance.SetKillCooldownV3(Options.CooldownAfterJaniting.GetFloat());
                    Logger.Info($"{__instance.GetRealName()} 清理了 {target.PlayerName} 的尸体", "Janitor");
                    return false;
                }

                // 被赌杀的尸体无法被报告
                if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled) return false;

                // 清道夫的尸体无法被报告
                if (killerRole == CustomRoles.Scavenger) return false;

                // 银狼的尸体无法被报告
                if (killerRole == CustomRoles.YinLang) return false;

                // 被清理的尸体无法报告
                if (Main.CleanerBodies.Contains(target.PlayerId)) return false;

                // 被清理的尸体无法报告
                if (Main.JanitorBodies.Contains(target.PlayerId)) return false;

                // 被清理的尸体无法报告
                if (Main.MedusaBodies.Contains(target.PlayerId)) return false;

                // 胆小鬼不敢报告
                var tpc = Utils.GetPlayerById(target.PlayerId);
                if (__instance.Is(CustomRoles.Oblivious))
                {
                    if (!tpc.Is(CustomRoles.Bait) || (tpc.Is(CustomRoles.Bait) && Options.ObliviousBaitImmune.GetBool())) /* && (target?.Object != null)*/
                    {
                        return false;
                    } 
                }

                //嫉妒狂击杀
                if (killer.Is(CustomRoles.Jealousy))
                {
                    if (!Main.ForJealousy.Contains(target.PlayerId))
                    {
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                    else
                    {
                        Main.JealousyMax[killer.PlayerId]++;
                    }
                    if (Main.JealousyMax[killer.PlayerId] >= Options.JealousyKillMax.GetInt())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jealousy);
                        CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
                    }
                }

        //紊乱者击杀
        if (killer.Is(CustomRoles.Disorder))
        {
            var Dd = IRandom.Instance;
            if (Dd.Next(0, 100) < Options.Disorderility.GetInt())
            {
                var Ie = IRandom.Instance;
                int Kl = Ie.Next(0, 100);
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DisorderKillCooldown.GetFloat();
                }
                if (Kl >= 30 && Kl < 40)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DepressedKillCooldown.GetFloat();
                }
                if (Kl >= 40 && Kl < 50)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CleanerKillCooldown.GetFloat();
                }
                if (Kl >= 50 && Kl < 60)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.BomberKillCooldown.GetFloat();
                }
                if (Kl >= 60 && Kl < 70)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CapitalismSkillCooldown.GetFloat();
                }
                if (Kl >= 70 && Kl < 80)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.ScavengerKillCooldown.GetFloat();
                }
                if (Kl >= 80 && Kl < 90)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.MNKillCooldown.GetFloat();
                }
                if (Kl >= 90 && Kl < 100)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.RevolutionistCooldown.GetFloat();
                }
            }
        }
                //奴隶主奴隶
                if (killer.Is(CustomRoles.Slaveowner))
                {
                    if (target.GetCustomRole().IsCrewmate())
                    {
                        if (Main.SlaveownerMax[killer.PlayerId] >= Options.ForSlaveownerSlav.GetInt())
                        {
                            return false;
                        }
                        if (Options.TargetcanSeeSlaveowner.GetBool())
                        {
                            NameNotifyManager.Notify(Utils.GetPlayerById(target.PlayerId), Utils.ColorString(Utils.GetRoleColor(CustomRoles.Slaveowner), GetString("SlavBySlaveowner")));
                            Main.ForSlaveowner.Add(target.PlayerId);
                            Main.SlaveownerMax[killer.PlayerId]++;
                            killer.ResetKillCooldown();
                            killer.SetKillCooldown();
                            killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                            CustomWinnerHolder.WinnerIds.Remove(target.PlayerId);
                            return false;
                        }
                        else
                        {
                            Main.ForSlaveowner.Add(target.PlayerId);
                            Main.SlaveownerMax[killer.PlayerId]++;
                            killer.ResetKillCooldown();
                            killer.SetKillCooldown();
                            killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                            CustomWinnerHolder.WinnerIds.Remove(target.PlayerId);
                            return false;
                        }
                    }
                    else
                    {
                        killer.ResetKillCooldown();
                        killer.SetKillCooldown();
                    }
                }

                if (killer.Is(CustomRoles.King))
                {
                    killer.ResetKillCooldown();
                    killer.SetKillCooldown();
                    killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#FFCC00");
                    if (Main.KingCanKill.Contains(killer.PlayerId))
                    {
                        foreach (var player in Main.AllPlayerControls)
                        {
                            if (Main.ForKing.Contains(player.PlayerId))
                            {
                                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Execution;
                                player.SetRealKiller(killer);
                                player.RpcMurderPlayerV3(player);
                            }
                        }
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King);
                        CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
                    }
                    if (!Main.ForKing.Contains(target.PlayerId))
                    {
                        Main.ForKing.Add(target.PlayerId);
                        bool AllPlayerForKing = true;
                        foreach (var player in Main.AllPlayerControls)
                        {
                            if (player == killer) continue;
                            if (Main.ForKing.Contains(player.PlayerId))
                            {
                                NameColorManager.Add(killer.PlayerId, player.PlayerId, "#FFCC00");
                            }
                            if (!Main.ForKing.Contains(player.PlayerId))
                            {
                                AllPlayerForKing = false;
                                continue;
                            }
                        }
                        if (AllPlayerForKing)
                        {
                            NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.King), GetString("EnterVentToWinandKillPlayerToWin")));
                            Main.KingCanpc.Add(killer.PlayerId);
                            Main.KingCanKill.Add(killer.PlayerId);
                        }
                    }
                    else
                    {
                    return false;
                    }
                }

                //爆破狂技能
                if (killer.Is(CustomRoles.DemolitionManiac))
                {
                    if (Options.DemolitionManiacKillPlayerr.GetInt() == 0)
                    {
                        Main.DemolitionManiacKill.Add(target.PlayerId);
                        killer.SetKillCooldownV2(target: Utils.GetPlayerById(target.PlayerId), forceAnime: true);
                        return false;
                    }
                    if (Options.DemolitionManiacKillPlayerr.GetInt() == 1)
                    {
                        killer.SetKillCooldownV2(target: Utils.GetPlayerById(target.PlayerId), forceAnime: true);
                        Main.InBoom.Remove(killer.PlayerId);
                        Main.InBoom.Add(killer.PlayerId, Utils.GetTimeStamp());
                        Main.ForDemolition.Add(target.PlayerId);
                        new LateTask(() =>
                        {
                            Utils.GetPlayerById(target.PlayerId).RpcMurderPlayerV3(Utils.GetPlayerById(target.PlayerId));
                            Utils.GetPlayerById(target.PlayerId).SetRealKiller(killer);

                            CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                            foreach (var player in Main.AllPlayerControls)
                            {
                            }
                        }, Options.DemolitionManiacWait.GetFloat(), "DemolitionManiacBoom!!!");
                        return false;
                    }
                }

                //抑郁者赌命
                if (killer.Is(CustomRoles.Depressed))
                {
                    if (killer.Is(CustomRoles.OldThousand))
                    {
                        killer.SetKillCooldownV2();
                    }
                        var rd = IRandom.Instance;
                    if (rd.Next(0, 100) < Options.DepressedIdioctoniaProbability.GetInt())
                    {
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Depression;
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                }

                //瘟疫之源感染
                if (killer.Is(CustomRoles.SourcePlague))
                {
                    killer.ResetKillCooldown();
                    killer.SetKillCooldown();
                    killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#999999");
                    //是否目标已经被感染了
                    if (!Main.ForSourcePlague.Contains(target.PlayerId))
                    {
                        Main.ForSourcePlague.Add(target.PlayerId);
                        bool AllPlayerForWY = true;
                        //看看所有玩家有没有有人没被感染
                        foreach (var player in Main.AllPlayerControls)
                        {
                            if (player == killer) continue;
                            if (Main.ForSourcePlague.Contains(player.PlayerId))
                            {
                                NameColorManager.Add(killer.PlayerId, player.PlayerId, "#999999");
                            }
                            if (!Main.ForSourcePlague.Contains(player.PlayerId))
                            {
                                AllPlayerForWY = false;
                                continue;
                            }
                        }
                        if (AllPlayerForWY)
                        {
                            killer.RpcSetCustomRole(CustomRoles.PlaguesGod);
                        }
                    }
                    else
                    {
                        return false;
                    }
                    return false;
                }

                //暗恋者暗恋
                if (killer.Is(CustomRoles.Crush))
                {
                    Main.CrushMax[killer.PlayerId]++;
                    if (Main.CrushMax[killer.PlayerId] == 1 && !Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.Captain) && !Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.Believer))
                    {
                        killer.RpcSetCustomRole(CustomRoles.Lovers);
                        Utils.GetPlayerById(target.PlayerId).RpcSetCustomRole(CustomRoles.Lovers);
                        killer.ResetKillCooldown();
                        killer.SetKillCooldown();
                        killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                        return false;
                    }
                    else if (Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.Captain) && Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.Believer))
                    {
                        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("CrushInvalidTarget")));
                        return false;
                    }
                    else
                    {
                        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("CrushInvalidTarget")));
                        return false;
                    }
                }

                //执行者召开会议
                if (killer.Is(CustomRoles.Executor))
                {
                    Utils.GetPlayerById(target.PlayerId)?.ReportDeadBody(null);
                }

                if (killer.Is(CustomRoles.PlagueDoctor))
                {
                    PlagueDoctor.CanInfectInt[killer.PlayerId]++;
                    if (PlagueDoctor.CanInfectInt[killer.PlayerId] <= PlagueDoctor.InfectTimes.GetInt() && !PlagueDoctor.InfectList.Contains(target.PlayerId))
                    {
                        PlagueDoctor.InfectList.Add(target.PlayerId);
                        PlagueDoctor.InfectNum += 1;
                        PlagueDoctor.SendRPC(killer.PlayerId);
                        PlagueDoctor.InfectInt[target.PlayerId] = 100f;
                        Logger.Info($"成功感染", "pdd");
                        killer.ResetKillCooldown();
                        killer.SetKillCooldown();
                        killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                        return false;
                    }
                    else if (PlagueDoctor.InfectList.Contains(target.PlayerId))
                    {
                        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlagueDoctor), GetString("Coutains")));
                        PlagueDoctor.CanInfectInt[killer.PlayerId]--;
                        PlagueDoctor.SendRPC(killer.PlayerId);
                        return false;
                    }
                    else if (PlagueDoctor.CanInfectInt[killer.PlayerId] > PlagueDoctor.InfectTimes.GetInt())
                    {
                        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlagueDoctor), GetString("SkillMax")));
                        PlagueDoctor.CanInfectInt[killer.PlayerId]--;
                        PlagueDoctor.SendRPC(killer.PlayerId);
                        return false;
                    }
                }
                if (Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.PlagueDoctor) && PlagueDoctor.Infectmurder.GetBool())
                {
                    new LateTask(() =>
                    {
                        if (!Utils.GetPlayerById(target.PlayerId).IsAlive() && !PlagueDoctor.InfectList.Contains(killer.PlayerId))
                        {
                            PlagueDoctor.InfectList.Add(killer.PlayerId);
                            PlagueDoctor.InfectNum += 1;
                            Logger.Info($"成功感染", "pdd");
                            PlagueDoctor.InfectInt[killer.PlayerId] = 100f;
                        }
                    }, 0.1f);
                    return true;
                }

                if (killer.Is(CustomRoles.Fake))
                {
                    if (Main.FakeMax[killer.PlayerId] < Options.Fakemax.GetInt())
                    {
                        killer.ResetKillCooldown();
                        killer.SetKillCooldown();
                        killer.RpcGuardAndKill(Utils.GetPlayerById(target.PlayerId));
                        Main.FakeMax[killer.PlayerId]++;
                        return false;
                    }
                    else
                    {
                        Main.NotKiller.Add(killer.PlayerId);
                        Main.ForFake.Add(target.PlayerId);
                        Main.NeedFake.TryAdd(killer.PlayerId, target.PlayerId);
                        Utils.TP(killer.NetTransform, Utils.GetPlayerById(target.PlayerId).GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        Utils.TP(Utils.GetPlayerById(target.PlayerId).NetTransform, Pelican.GetBlackRoomPS());
                        Utils.GetPlayerById(target.PlayerId).SetRealKiller(killer);
                        Main.PlayerStates[target.PlayerId].SetDead();
                        Utils.GetPlayerById(target.PlayerId).RpcMurderPlayerV3(Utils.GetPlayerById(target.PlayerId));
                        killer.SetKillCooldownV2();
                        NameNotifyManager.Notify(Utils.GetPlayerById(target.PlayerId), Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByFake")));
                        return false;
                    }           
                }

                // 报告了诡雷尸体
                if (Main.BoobyTrapBody.Contains(target.PlayerId) && __instance.IsAlive())
                {
                    if (!Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool())
                    {
                        var killerID = Main.KillerOfBoobyTrapBody[target.PlayerId];
                        Main.PlayerStates[__instance.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        __instance.SetRealKiller(Utils.GetPlayerById(killerID));

                        __instance.RpcMurderPlayerV3(__instance);
                        RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                        if (!Main.BoobyTrapBody.Contains(__instance.PlayerId)) Main.BoobyTrapBody.Add(__instance.PlayerId);
                        if (!Main.KillerOfBoobyTrapBody.ContainsKey(__instance.PlayerId)) Main.KillerOfBoobyTrapBody.Add(__instance.PlayerId, killerID);
                        return false;
                    }
                    else
                    {
                        var killerID2 = target.PlayerId;
                        Main.PlayerStates[__instance.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        __instance.SetRealKiller(Utils.GetPlayerById(killerID2));

                        __instance.RpcMurderPlayerV3(__instance);
                        RPC.PlaySoundRPC(killerID2, Sounds.KillSound);
                        return false;
                    }
                }

                if (target.Object.Is(CustomRoles.Unreportable)) return false;
            }

            if (Options.SyncButtonMode.GetBool() && target == null)
            {
                Logger.Info("最大:" + Options.SyncedButtonCount.GetInt() + ", 現在:" + Options.UsedButtonCount, "ReportDeadBody");
                if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                {
                    Logger.Info("使用可能ボタン回数が最大数を超えているため、ボタンはキャンセルされました。", "ReportDeadBody");
                    return false;
                }
                else Options.UsedButtonCount++;
                if (Options.SyncedButtonCount.GetFloat() == Options.UsedButtonCount)
                {
                    Logger.Info("使用可能ボタン回数が最大数に達しました。", "ReportDeadBody");
                }
            }

            AfterReportTasks(__instance, target);

        }
        catch (Exception e)
        {
            Logger.Exception(e, "ReportDeadBodyPatch");
            Logger.SendInGame("Error: " + e.ToString());
        }

        return true;
    }
    public static void AfterReportTasks(PlayerControl player, GameData.PlayerInfo target)
    {
        //=============================================
        //以下、ボタンが押されることが確定したものとする。
        //=============================================

        if (target == null) //ボタン
        {
            if (player.Is(CustomRoles.Mayor))
            {
                Main.MayorUsedButtonCount[player.PlayerId] += 1;
            }
            else if (player.Is(CustomRoles.Chairman))
            {
                Main.ChairmanUsedButtonCount[player.PlayerId] += 1;
            }
        }
        if (target == null) //ボタン
        {
            if (player.Is(CustomRoles.Snitch))
            {
                Main.SnitchUsedButtonCount[player.PlayerId] += 1;
            }
        }
        else
        {
            var tpc = Utils.GetPlayerById(target.PlayerId);
            if (tpc != null && !tpc.IsAlive())
            {
                // 侦探报告
                if (player.Is(CustomRoles.Detective) && player.PlayerId != target.PlayerId)
                {
                    string msg;
                    msg = string.Format(GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetDisplayRoleName());
                    if (Options.DetectiveCanknowKiller.GetBool())
                    {
                        var realKiller = tpc.GetRealKiller();
                        if (realKiller == null) msg += "；" + GetString("DetectiveNoticeKillerNotFound");
                        else msg += "；" + string.Format(GetString("DetectiveNoticeKiller"), realKiller.GetDisplayRoleName());
                    }
                    Main.DetectiveNotify.Add(player.PlayerId, msg);
                }
            }

            if (Main.InfectedBodies.Contains(target.PlayerId)) Virus.OnKilledBodyReport(player);
        }

        Main.LastVotedPlayerInfo = null;
        Main.ArsonistTimer.Clear();
        Main.FarseerTimer.Clear();
        Main.PuppeteerList.Clear();
        Main.TaglockedList.Clear();
        Main.GuesserGuessed.Clear();
        Main.VeteranInProtect.Clear();
        Main.GrenadierBlinding.Clear();
        Main.RudepeopleInProtect.Clear();
        Main.QSRInProtect.Clear();
        Main.CovenLeaderList.Clear();
        Main.ForNnurse.Clear();
        Main.MadGrenadierBlinding.Clear();
        Divinator.didVote.Clear();
        Oracle.didVote.Clear();
        Bloodhound.Clear();
        Vulture.Clear();

        Camouflager.OnReportDeadBody();
        Psychic.OnReportDeadBody();
        BountyHunter.OnReportDeadBody();
        SerialKiller.OnReportDeadBody();
        Sniper.OnReportDeadBody();
        Vampire.OnStartMeeting();
        Poisoner.OnStartMeeting();
        Pelican.OnReportDeadBody();
        Counterfeiter.OnReportDeadBody();
        BallLightning.OnReportDeadBody();
        QuickShooter.OnReportDeadBody();
        Eraser.OnReportDeadBody();
        Hacker.OnReportDeadBody();
        Judge.OnReportDeadBody();
    //    Councillor.OnReportDeadBody();
        Greedier.OnReportDeadBody();
        Tracker.OnReportDeadBody();
        Addict.OnReportDeadBody();
        Deathpact.OnReportDeadBody();
        ParityCop.OnReportDeadBody();
        GrudgeSheriff.OnReportDeadBody();
        Doomsayer.OnReportDeadBody();
        Seeker.OnReportDeadBody();
        Romantic.OnReportDeadBody();

        Mortician.OnReportDeadBody(player, target);
        Tracefinder.OnReportDeadBody(player, target);
        Mediumshiper.OnReportDeadBody(target);
        Spiritualist.OnReportDeadBody(target);
        
        foreach (var x in Main.RevolutionistStart)
        {
            var tar = Utils.GetPlayerById(x.Key);
            if (tar == null) continue;
            tar.Data.IsDead = true;
            Main.PlayerStates[tar.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
            tar.RpcExileV2();
            Main.PlayerStates[tar.PlayerId].SetDead();
            Logger.Info($"{tar.GetRealName()} 因会议革命失败", "Revolutionist");
        }
        Main.RevolutionistTimer.Clear();
        Main.RevolutionistStart.Clear();
        Main.RevolutionistLastTime.Clear();

        Main.AllPlayerControls
            .Where(pc => Main.CheckShapeshift.ContainsKey(pc.PlayerId))
            .Do(pc => Camouflage.RpcSetSkin(pc, RevertToDefault: true));

        MeetingTimeManager.OnReportDeadBody();

        Utils.NotifyRoles(isForMeeting: true, NoCache: true, CamouflageIsForMeeting: true, GuesserIsForMeeting: true);

        Utils.SyncAllSettings();
    }
    public static async void ChangeLocalNameAndRevert(string name, int time)
    {
        //async Taskじゃ警告出るから仕方ないよね。
        var revertName = PlayerControl.LocalPlayer.name;
        PlayerControl.LocalPlayer.RpcSetNameEx(name);
        await Task.Delay(time);
        PlayerControl.LocalPlayer.RpcSetNameEx(revertName);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
class FixedUpdatePatch
{
    private static long LastFixedUpdate = new();
    private static StringBuilder Mark = new(20);
    private static StringBuilder Suffix = new(120);
    private static int LevelKickBufferTime = 10;
    private static Dictionary<byte, int> BufferTime = new();
    public static void Postfix(PlayerControl __instance)
    {
        var player = __instance;

        if (!GameStates.IsModHost) return;
        Utils.ApplyModeratorSuffix(__instance);
        Utils.ApplyTesterSuffix(__instance);

        bool lowLoad = false;
        if (Options.LowLoadMode.GetBool())
        {
            BufferTime.TryAdd(player.PlayerId, 10);
            BufferTime[player.PlayerId]--;
            if (BufferTime[player.PlayerId] > 0) lowLoad = true;
            else BufferTime[player.PlayerId] = 10;
        }

        Sniper.OnFixedUpdate(player);
        Zoom.OnFixedUpdate();
        if (!lowLoad)
        {
            NameNotifyManager.OnFixedUpdate(player);
            TargetArrow.OnFixedUpdate(player);
            LocateArrow.OnFixedUpdate(player);
        }


        if (AmongUsClient.Instance.AmHost)
        {//実行クライアントがホストの場合のみ実行
            if (GameStates.IsLobby && ((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken || !Main.AllowPublicRoom) && AmongUsClient.Instance.IsGamePublic)
                AmongUsClient.Instance.ChangeGamePublic(false);

            if (GameStates.IsInTask && ReportDeadBodyPatch.CanReport[__instance.PlayerId] && ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Count > 0)
            {
                var info = ReportDeadBodyPatch.WaitReport[__instance.PlayerId][0];
                ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Clear();
                Logger.Info($"{__instance.GetNameWithRole()}:通報可能になったため通報処理を行います", "ReportDeadbody");
                __instance.ReportDeadBody(info);
            }

            //踢出低等级的人
            if (!lowLoad && GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
                (player.Data.PlayerLevel != 0 && player.Data.PlayerLevel < Options.KickLowLevelPlayer.GetInt()) ||
                player.Data.FriendCode == ""
                ))
            {
                LevelKickBufferTime--;
                if (LevelKickBufferTime <= 0)
                {
                    LevelKickBufferTime = 20;
                    AmongUsClient.Instance.KickPlayer(player.GetClientId(), false);
                    string msg = string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags());
                    Logger.SendInGame(msg);
                    Logger.Info(msg, "LowLevel Kick");
                }
            }

            DoubleTrigger.OnFixedUpdate(player);
            Vampire.OnFixedUpdate(player);
            Poisoner.OnFixedUpdate(player);
            BountyHunter.FixedUpdate(player);
            SerialKiller.FixedUpdate(player);
            GrudgeSheriff.FixedUpdate(player);
            Seeker.FixedUpdate(player);
            RewardOfficer.FixedUpdate(player);
            if (GameStates.IsInTask)
                if (player.Is(CustomRoles.PlagueBearer) && PlagueBearer.IsPlaguedAll(player))
                {
                    player.RpcSetCustomRole(CustomRoles.Pestilence);
                    player.Notify(GetString("PlagueBearerToPestilence"));
                    player.RpcGuardAndKill(player);
                    if (!PlagueBearer.PestilenceList.Contains(player.PlayerId))
                        PlagueBearer.PestilenceList.Add(player.PlayerId);
                    PlagueBearer.SetKillCooldownPestilence(player.PlayerId);
                    PlagueBearer.playerIdList.Remove(player.PlayerId);
                }

            #region 女巫处理
            if (GameStates.IsInTask && Main.WarlockTimer.ContainsKey(player.PlayerId))//処理を1秒遅らせる
            {
                if (player.IsAlive())
                {
                    if (Main.WarlockTimer[player.PlayerId] >= 1f)
                    {
                        player.RpcResetAbilityCooldown();
                        Main.isCursed = false;//変身クールを１秒に変更
                        player.SyncSettings();
                        Main.WarlockTimer.Remove(player.PlayerId);
                    }
                    else Main.WarlockTimer[player.PlayerId] = Main.WarlockTimer[player.PlayerId] + Time.fixedDeltaTime;//時間をカウント
                }
                else
                {
                    Main.WarlockTimer.Remove(player.PlayerId);
                }
            }
            //ターゲットのリセット
            #endregion

            #region 纵火犯浇油处理
            if (GameStates.IsInTask && Main.ArsonistTimer.ContainsKey(player.PlayerId))//アーソニストが誰かを塗っているとき
            {
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                {
                    Main.ArsonistTimer.Remove(player.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: __instance);
                    RPC.ResetCurrentDousingTarget(player.PlayerId);
                }
                else
                {
                    var ar_target = Main.ArsonistTimer[player.PlayerId].Item1;//塗られる人
                    var ar_time = Main.ArsonistTimer[player.PlayerId].Item2;//塗った時間
                    if (!ar_target.IsAlive())
                    {
                        Main.ArsonistTimer.Remove(player.PlayerId);
                    }
                    else if (ar_time >= Options.ArsonistDouseTime.GetFloat())//時間以上一緒にいて塗れた時
                    {
                        player.SetKillCooldown();
                        Main.ArsonistTimer.Remove(player.PlayerId);//塗が完了したのでDictionaryから削除
                        Main.isDoused[(player.PlayerId, ar_target.PlayerId)] = true;//塗り完了
                        player.RpcSetDousedPlayer(ar_target, true);
                        Utils.NotifyRoles(SpecifySeer: player);//名前変更
                        RPC.ResetCurrentDousingTarget(player.PlayerId);
                    }
                    else
                    {

                        float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                        float dis = Vector2.Distance(player.transform.position, ar_target.transform.position);//距離を出す
                        if (dis <= range)//一定の距離にターゲットがいるならば時間をカウント
                        {
                            Main.ArsonistTimer[player.PlayerId] = (ar_target, ar_time + Time.fixedDeltaTime);
                        }
                        else//それ以外は削除
                        {
                            Main.ArsonistTimer.Remove(player.PlayerId);
                            Utils.NotifyRoles(SpecifySeer: player);
                            RPC.ResetCurrentDousingTarget(player.PlayerId);

                            Logger.Info($"Canceled: {player.GetNameWithRole()}", "Arsonist");
                        }
                    }
                }
            }
            #endregion

            #region 革命家拉人处理
            if (GameStates.IsInTask && Main.RevolutionistTimer.ContainsKey(player.PlayerId))//当革命家拉拢一个玩家时
            {
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                {
                    Main.RevolutionistTimer.Remove(player.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: player);
                    RPC.ResetCurrentDrawTarget(player.PlayerId);
                }
                else
                {
                    var rv_target = Main.RevolutionistTimer[player.PlayerId].Item1;//拉拢的人
                    var rv_time = Main.RevolutionistTimer[player.PlayerId].Item2;//拉拢时间
                    if (!rv_target.IsAlive())
                    {
                        Main.RevolutionistTimer.Remove(player.PlayerId);
                    }
                    else if (rv_time >= Options.RevolutionistDrawTime.GetFloat())//在一起时间超过多久
                    {
                        player.SetKillCooldown();
                        Main.RevolutionistTimer.Remove(player.PlayerId);//拉拢完成从字典中删除
                        Main.isDraw[(player.PlayerId, rv_target.PlayerId)] = true;//完成拉拢
                        player.RpcSetDrawPlayer(rv_target, true);
                        Utils.NotifyRoles(SpecifySeer: player);
                        RPC.ResetCurrentDrawTarget(player.PlayerId);
                        if (IRandom.Instance.Next(1, 100) <= Options.RevolutionistKillProbability.GetInt())
                        {
                            rv_target.SetRealKiller(player);
                            Main.PlayerStates[rv_target.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                            player.RpcMurderPlayerV3(rv_target);
                            Main.PlayerStates[rv_target.PlayerId].SetDead();
                            Logger.Info($"Revolutionist: {player.GetNameWithRole()} killed {rv_target.GetNameWithRole()}", "Revolutionist");
                        }
                    }
                    else
                    {
                        float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                        float dis = Vector2.Distance(player.transform.position, rv_target.transform.position);//超出距离
                        if (dis <= range)//在一定距离内则计算时间
                        {
                            Main.RevolutionistTimer[player.PlayerId] = (rv_target, rv_time + Time.fixedDeltaTime);
                        }
                        else//否则删除
                        {
                            Main.RevolutionistTimer.Remove(player.PlayerId);
                            Utils.NotifyRoles(SpecifySeer: __instance);
                            RPC.ResetCurrentDrawTarget(player.PlayerId);

                            Logger.Info($"Canceled: {__instance.GetNameWithRole()}", "Revolutionist");
                        }
                    }
                }
            }
            if (GameStates.IsInTask && player.IsDrawDone() && player.IsAlive())
            {
                if (Main.RevolutionistStart.ContainsKey(player.PlayerId)) //如果存在字典
                {
                    if (Main.RevolutionistLastTime.ContainsKey(player.PlayerId))
                    {
                        long nowtime = Utils.GetTimeStamp();
                        if (Main.RevolutionistLastTime[player.PlayerId] != nowtime) Main.RevolutionistLastTime[player.PlayerId] = nowtime;
                        int time = (int)(Main.RevolutionistLastTime[player.PlayerId] - Main.RevolutionistStart[player.PlayerId]);
                        int countdown = Options.RevolutionistVentCountDown.GetInt() - time;
                        Main.RevolutionistCountdown.Clear();
                        if (countdown <= 0)//倒计时结束
                        {
                            Utils.GetDrawPlayerCount(player.PlayerId, out var y);
                            foreach (var pc in y.Where(x => x != null && x.IsAlive()))
                            {
                                pc.Data.IsDead = true;
                                Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                                pc.RpcMurderPlayerV3(pc);
                                Main.PlayerStates[pc.PlayerId].SetDead();
                                Utils.NotifyRoles(SpecifySeer: pc);
                            }
                            player.Data.IsDead = true;
                            Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                            player.RpcMurderPlayerV3(player);
                            Main.PlayerStates[player.PlayerId].SetDead();
                        }
                        else
                        {
                            Main.RevolutionistCountdown.Add(player.PlayerId, countdown);
                        }
                    }
                    else
                    {
                        Main.RevolutionistLastTime.TryAdd(player.PlayerId, Main.RevolutionistStart[player.PlayerId]);
                    }
                }
                else //如果不存在字典
                {
                    Main.RevolutionistStart.TryAdd(player.PlayerId, Utils.GetTimeStamp());
                }
            }
            #endregion

            Farseer.OnPostFix(player);
            Addict.FixedUpdate(player);
            Deathpact.OnFixedUpdate(player);

            if (!lowLoad)
            {
                //检查老兵技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.Veteran))
                {
                    if (Main.VeteranInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.VeteranSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.VeteranInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(string.Format(GetString("VeteranOffGuard"), Main.VeteranNumOfUsed[player.PlayerId]));
                    }
                }

                //检查掷雷兵技能是否生效
                if (GameStates.IsInTask && player.Is(CustomRoles.Grenadier))
                {
                    if (Main.GrenadierBlinding.TryGetValue(player.PlayerId, out var gtime) && gtime + Options.GrenadierSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.GrenadierBlinding.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("GrenadierSkillStop"));
                        Utils.MarkEveryoneDirtySettings();
                    }
                    if (Main.MadGrenadierBlinding.TryGetValue(player.PlayerId, out var mgtime) && mgtime + Options.GrenadierSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.MadGrenadierBlinding.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("GrenadierSkillStop"));
                        Utils.MarkEveryoneDirtySettings();
                    }
                }

                //检查挑衅者技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.Rudepeople))
                    {
                        if (Main.RudepeopleInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.RudepeopleSkillDuration.GetInt() < Utils.GetTimeStamp())
                        {
                            Main.RudepeopleInProtect.Remove(player.PlayerId);
                            player.RpcGuardAndKill();
                        player.Notify(string.Format(GetString("RudepeopleOffGuard")));
                        }
                    }

                //检查时间之主技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.TimeMaster))
                {
                    if (Main.TimeMasterInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.TimeMasterSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.TimeMasterInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("TimeMasterSkillStop"));
                    }
                }

                //检查时停者的技能是否生效
                if (GameStates.IsInTask && player.Is(CustomRoles.TimeStops))
                {
                    if (Main.TimeStopsInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.TimeStopsSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.TimeStopsInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("TimeStopsSkillStop"));
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            Main.TimeStopsstop.Remove(pc.PlayerId);
                        }
                    }
                }                

                //检查爆破狂的炸弹是否到了引爆时间
                if (GameStates.IsInTask && player.Is(CustomRoles.DemolitionManiac))
                {
                    if (Main.InBoom.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.DemolitionManiacWait.GetFloat() < Utils.GetTimeStamp())
                    {
                        
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            if (!pc.IsModClient()) pc.KillFlash();
                            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) continue;
                            if (Main.ForDemolition.Contains(pc.PlayerId))
                            {
                                foreach (var BoomPlayer in Main.AllPlayerControls)
                                {
                                    if (Vector2.Distance(pc.transform.position, BoomPlayer.transform.position) <= Options.DemolitionManiacRadius.GetFloat())
                                    {
                                        Main.PlayerStates[BoomPlayer.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                                        BoomPlayer.SetRealKiller(player);
                                        BoomPlayer.RpcMurderPlayerV3(BoomPlayer);
                                    }
                                }
                            }                           
                        }
                    }
                }

                //检查护士是否已经完成救治
                if (GameStates.IsInTask && player.Is(CustomRoles.Nurse))
                {
                    if (Main.ForNnurse.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.NurseSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.ForNnurse.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("ForNnurseCanHelp"));
                        Main.NnurseHelep.Remove(player.PlayerId);
                    }
                }

                //检查恐血者技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.Hemophobia))
                {
                    if (Main.HemophobiaInKill.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.HemophobiaSeconds.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.HemophobiaInKill.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("HemophobiaSkillStop"));
                        Main.ForHemophobia.Remove(player.PlayerId);
                    }
                }

                if (GameStates.IsInTask && PlagueDoctor.SetImmunitytime.GetBool())
                {
                    if (LastFixedUpdate == Utils.GetTimeStamp()) return;
                    LastFixedUpdate = Utils.GetTimeStamp();
                    if (PlagueDoctor.Immunitytimes > 0)
                    {
                        PlagueDoctor.Immunitytimes--;
                        PlagueDoctor.ImmunityGone = false;
                    }
                    else if (PlagueDoctor.Immunitytimes <= 0)
                    {
                        PlagueDoctor.ImmunityGone = true;
                    }
                }

                //检查马里奥是否完成
                if (GameStates.IsInTask && player.Is(CustomRoles.Mario) && Main.MarioVentCount[player.PlayerId] > Options.MarioVentNumWin.GetInt())
                {
                    Main.MarioVentCount[player.PlayerId] = Options.MarioVentNumWin.GetInt();
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                    CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                }
                if (GameStates.IsInTask && player.Is(CustomRoles.Vulture) && Vulture.BodyReportCount[player.PlayerId] >= Vulture.NumberOfReportsToWin.GetInt())
                {
                    Vulture.BodyReportCount[player.PlayerId] = Vulture.NumberOfReportsToWin.GetInt();
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
                    CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                }

                Pelican.OnFixedUpdate();
                BallLightning.OnFixedUpdate();
                Swooper.OnFixedUpdate(player);
                Wraith.OnFixedUpdate(player);
                Chameleon.OnFixedUpdate(player);
                BloodKnight.OnFixedUpdate(player);
                Spiritcaller.OnFixedUpdate(player);
                Banshee.OnFixedUpdate(player);
                PlagueDoctor.OnFixedUpdate(player);
                Yandere.OnFixedUpdate(player);

                if (GameStates.IsInTask && player.IsAlive() && Options.LadderDeath.GetBool()) FallFromLadder.FixedUpdate(player);

                if (GameStates.IsInGame) LoversSuicide();
                if (GameStates.IsInGame) CaptainSuicide();

                #region 傀儡师处理
                if (GameStates.IsInTask && Main.PuppeteerList.ContainsKey(player.PlayerId))
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                    {
                        Main.PuppeteerList.Remove(player.PlayerId);
                    }
                    else
                    {
                        Vector2 puppeteerPos = player.transform.position;//PuppeteerListのKeyの位置
                        Dictionary<byte, float> targetDistance = new();
                        float dis;
                        foreach (var target in Main.AllAlivePlayerControls)
                        {
                            if (player.Is(CustomRoles.Sidekick))
                            {
                                if (target.PlayerId != player.PlayerId && !target.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Jackal))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                            if (!player.Is(CustomRoles.Sidekick))
                            {
                                if (target.PlayerId != player.PlayerId && !target.Is(CustomRoleTypes.Impostor))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                        }
                        if (targetDistance.Count() != 0)
                        {
                            var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();//一番値が小さい
                            PlayerControl target = Utils.GetPlayerById(min.Key);
                            var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                            if (min.Value <= KillRange && player.CanMove && target.CanMove)
                            {
                                if (player.RpcCheckAndMurder(target, true))
                                {
                                    var puppeteerId = Main.PuppeteerList[player.PlayerId];
                                    RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
                                    target.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                    player.RpcMurderPlayerV3(target);
                                    Utils.MarkEveryoneDirtySettings();
                                    Main.PuppeteerList.Remove(player.PlayerId);
                                    Utils.NotifyRoles();
                                }
                            }
                        }
                    }
                }
                if (GameStates.IsInTask && Main.CovenLeaderList.ContainsKey(player.PlayerId))
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                    {
                        Main.CovenLeaderList.Remove(player.PlayerId);
                    }
                    else
                    {
                        Vector2 puppeteerPos = player.transform.position;//PuppeteerListのKeyの位置
                        Dictionary<byte, float> targetDistance = new();
                        float dis;
                        foreach (var target in Main.AllAlivePlayerControls)
                        {
                            
                            {
                                if (target.PlayerId != player.PlayerId && !target.GetCustomRole().IsCoven() && !target.Is(CustomRoles.Glitch) && !target.Is(CustomRoles.Pestilence))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                        }
                        if (targetDistance.Count() != 0)
                        {
                            var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();//一番値が小さい
                            PlayerControl target = Utils.GetPlayerById(min.Key);
                            var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                            if (min.Value <= KillRange && player.CanMove && target.CanMove)
                            {
                                if (player.RpcCheckAndMurder(target, true))
                                {
                                    var puppeteerId = Main.PuppeteerList[player.PlayerId];
                                    RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
                                    target.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                    player.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                    player.RpcMurderPlayerV3(target);
                                    Utils.MarkEveryoneDirtySettings();
                                    Main.CovenLeaderList.Remove(player.PlayerId);
                                    Utils.NotifyRoles();
                                    if (!player.Is(CustomRoles.Pestilence))
                                    {
                                        player.RpcMurderPlayerV3(player);
                                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Drained;
                                    }
                                }
                            }
                        }
                    }
                }
                if (GameStates.IsInTask && Main.TaglockedList.ContainsKey(player.PlayerId))
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                    {
                        Main.TaglockedList.Remove(player.PlayerId);
                    }
                    else
                    {
                        Vector2 puppeteerPos = player.transform.position;//PuppeteerListのKeyの位置
                        Dictionary<byte, float> targetDistance = new();
                        float dis;
                        foreach (var target in Main.AllAlivePlayerControls)
                        {
                            if (player.Is(CustomRoles.Sidekick))
                            {
                                if (target.PlayerId != player.PlayerId && !target.Is(CustomRoles.NWitch) && !target.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Jackal))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                            if (!player.Is(CustomRoles.Sidekick))
                            {
                                if (target.PlayerId != player.PlayerId && !target.Is(CustomRoles.NWitch))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                        }
                        if (targetDistance.Count() != 0)
                        {
                            var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();//一番値が小さい
                            PlayerControl target = Utils.GetPlayerById(min.Key);
                            var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                            if (min.Value <= KillRange && player.CanMove && target.CanMove)
                            {
                                if (player.RpcCheckAndMurder(target, true))
                                {
                                    var puppeteerId = Main.TaglockedList[player.PlayerId];
                                    RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
                                    target.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                    player.RpcMurderPlayerV3(target);
                                    Utils.MarkEveryoneDirtySettings();
                                    Main.CovenLeaderList.Remove(player.PlayerId);
                                    Main.TaglockedList.Remove(player.PlayerId);
                                    Utils.NotifyRoles();
                                }
                            }
                        }
                    }
                }
                #endregion

                if (GameStates.IsInTask && player == PlayerControl.LocalPlayer)
                    DisableDevice.FixedUpdate();
                if (GameStates.IsInTask && player == PlayerControl.LocalPlayer)
                    AntiAdminer.FixedUpdate();

                if (GameStates.IsInGame && Main.RefixCooldownDelay <= 0)
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        if (pc.Is(CustomRoles.Vampire) || pc.Is(CustomRoles.Warlock) || pc.Is(CustomRoles.Assassin))
                            Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown * 2;
                        if (pc.Is(CustomRoles.Poisoner))
                            Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown * 2;
                    }

                if (!Main.DoBlockNameChange && AmongUsClient.Instance.AmHost)
                    Utils.ApplySuffix(__instance);
            }
        }

        //LocalPlayer専用
        if (__instance.AmOwner)
        {
            //キルターゲットの上書き処理
            if (GameStates.IsInTask && !__instance.Is(CustomRoleTypes.Impostor) && __instance.CanUseKillButton() && !__instance.Data.IsDead)
            {
                var players = __instance.GetPlayersInAbilityRangeSorted(false);
                PlayerControl closest = players.Count <= 0 ? null : players[0];
                HudManager.Instance.KillButton.SetTarget(closest);
            }
        }

        //役職テキストの表示
        var RoleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
        var RoleText = RoleTextTransform.GetComponent<TMPro.TextMeshPro>();

        if (RoleText != null && __instance != null && !lowLoad)
        {
            if (GameStates.IsLobby)
            {
                if (Main.playerVersion.TryGetValue(__instance.PlayerId, out var ver))
                {
                    if (Main.ForkId != ver.forkId) // フォークIDが違う場合
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>{ver.forkId}</size>\n{__instance?.name}</color>";
                    else if (Main.version.CompareTo(ver.version) == 0)
                        __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#87cefa>{__instance.name}</color>" : $"<color=#ffff00><size=1.2>{ver.tag}</size>\n{__instance?.name}</color>";
                    else __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>v{ver.version}</size>\n{__instance?.name}</color>";
                }
                else __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
            }
            if (GameStates.IsInGame)
            {
                var RoleTextData = Utils.GetRoleText(PlayerControl.LocalPlayer.PlayerId, __instance.PlayerId);

                //if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                //{
                //    var hasRole = main.AllPlayerCustomRoles.TryGetValue(__instance.PlayerId, out var role);
                //    if (hasRole) RoleTextData = Utils.GetRoleTextHideAndSeek(__instance.Data.Role.Role, role);
                //}
                RoleText.text = RoleTextData.Item1;
                if (Options.CurrentGameMode == CustomGameMode.SoloKombat) RoleText.text = "";
                RoleText.color = RoleTextData.Item2;
                if (__instance.AmOwner) RoleText.enabled = true; //自分ならロールを表示
                else if (Options.CurrentGameMode == CustomGameMode.SoloKombat) RoleText.enabled = true;
                else if (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) RoleText.enabled = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) RoleText.enabled = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
                else if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) RoleText.enabled = true;
                else if (__instance.GetCustomRole().IsCoven() && PlayerControl.LocalPlayer.GetCustomRole().IsCoven() && Options.CovenKnowAlliesRole.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Crewpostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Rogue) && PlayerControl.LocalPlayer.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool() && Options.RogueKnowEachOtherRoles.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool() && Options.SidekickKnowOtherSidekickRole.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Jackal) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick)) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Jackal)) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Doctor) && Options.DoctorVisibleToEveryone.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && __instance.AllTasksCompleted()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Snitch) && Snitch.SnitchRevealWhenDoneTasks.GetBool() && __instance.AllTasksCompleted()) RoleText.enabled = true;
                else if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Romantic.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Ritualist.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Succubus.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Captain.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Yandere.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Jackal.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (CursedSoul.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Infectious.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Virus.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (PlayerControl.LocalPlayer.IsRevealedPlayer(__instance)) RoleText.enabled = true;
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.God)) RoleText.enabled = true;
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.GM)) RoleText.enabled = true;
                else if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Ritualist.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                //喵喵队
                //内鬼
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isimp == true && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isimp == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //豺狼
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && PlayerControl.LocalPlayer.Is(CustomRoles.Jackal) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Jackal) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && PlayerControl.LocalPlayer.Is(CustomRoles.Whoops) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Whoops) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isjac == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //西风骑士团(bushi)
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isbk == true && PlayerControl.LocalPlayer.Is(CustomRoles.BloodKnight) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.BloodKnight) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isbk == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //疫情的源头
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.ispg == true && PlayerControl.LocalPlayer.Is(CustomRoles.PlaguesGod) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.PlaguesGod) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.ispg == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //玩家
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isgam == true && PlayerControl.LocalPlayer.Is(CustomRoles.Gamer) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Gamer) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isgam == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //穹P黑客(BUSHI)
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isyl == true && PlayerControl.LocalPlayer.Is(CustomRoles.YinLang) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.YinLang) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isyl == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //黑，真tm黑
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isdh == true && PlayerControl.LocalPlayer.Is(CustomRoles.DarkHide) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.DarkHide) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isdh == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //雇佣
                else if (__instance.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isok == true && PlayerControl.LocalPlayer.Is(CustomRoles.OpportunistKiller) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.OpportunistKiller) && PlayerControl.LocalPlayer.Is(CustomRoles.SchrodingerCat) && SchrodingerCat.isok == true && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Main.GodMode.Value) RoleText.enabled = true;
                else RoleText.enabled = false; //そうでなければロールを非表示
                if (!PlayerControl.LocalPlayer.Data.IsDead && PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) && __instance.Is(CustomRoles.Trickster))
                { 
                    RoleText.text = Farseer.RandomRole[PlayerControl.LocalPlayer.PlayerId];
                    RoleText.text += Farseer.GetTaskState();
                }

                if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                {
                    RoleText.enabled = false; //ゲームが始まっておらずフリープレイでなければロールを非表示
                    if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                }
                if (Main.VisibleTasksCount) //他プレイヤーでVisibleTasksCountは有効なら
                {
                    if ((PlayerControl.LocalPlayer.Is(CustomRoles.TaskManager)) && PlayerControl.LocalPlayer == __instance)
                        RoleText.text += Utils.GetTMtaskCountText(__instance);
                }
                    RoleText.text += Utils.GetProgressText(__instance); //ロールの横にタスクなど進行状況表示


                //変数定義
                var seer = PlayerControl.LocalPlayer;
                var target = __instance;

                string RealName;
                //    string SeerRealName;
                Mark.Clear();
                Suffix.Clear();

                //名前変更
                RealName = target.GetRealName();
                //   SeerRealName = seer.GetRealName();

                //名前色変更処理
                //自分自身の名前の色を変更
                if (target.AmOwner && GameStates.IsInTask)
                { //targetが自分自身
                    if (target.Is(CustomRoles.Arsonist) && target.IsDouseDone())
                        RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist), GetString("EnterVentToWin"));
                    if (target.Is(CustomRoles.Revolutionist) && target.IsDrawDone())
                        RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.TryGetValue(seer.PlayerId, out var x) ? x : 10));
                    if (Pelican.IsEaten(seer.PlayerId))
                        RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));

                    if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                        SoloKombatManager.GetNameNotify(target, ref RealName);
                    if (Deathpact.IsInActiveDeathpact(seer))
                        RealName = Deathpact.GetDeathpactString(seer);
                    if (NameNotifyManager.GetNameNotify(target, out var name))
                        RealName = name;
                }
                    if (target.Is(CustomRoles.RewardOfficer) && RewardOfficer.RewardOfficerShow.Contains(target.PlayerId))
                    {
                        var Rp = target.PlayerId;
                        string roleName = GetString(Enum.GetName(target.GetCustomRole()));
                        if (RewardOfficer.RewardOfficerCanMode.GetInt() == 0)
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                if (RewardOfficer.ForRewardOfficer.Contains(pc.PlayerId))
                                {
                                    roleName = GetString(Enum.GetName(pc.GetCustomRole()));
                                }
                            }
                            target.Notify(string.Format(GetString("RewardOfficerRoles"), roleName));
                        }
                    }

                //NameColorManager準拠の処理
                RealName = RealName.ApplyNameColorData(seer, target, false);

                if (seer.GetCustomRole().IsImpostor()) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★")); //targetにマーク付与
                }
                if (seer.GetCustomRole().IsCrewmate()) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★")); //targetにマーク付与
                }
                if (seer.Is(CustomRoles.Jackal)) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Sidekick)) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), " ♥")); //targetにマーク付与
                }
          /*      if (seer.Is(CustomRoles.Monarch)) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Knighted)) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Knighted), " 亗")); //targetにマーク付与
                } */

                if (seer.Is(CustomRoles.Sidekick)) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool()) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), " ♥")); //targetにマーク付与
                }
                if (seer.GetCustomRole().IsCrewmate() && seer.Is(CustomRoles.Madmate) && Marshall.MadmateCanFindMarshall) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★")); //targetにマーク付与
                }

                //インポスター/キル可能なニュートラルがタスクが終わりそうなSnitchを確認できる
                Mark.Append(Snitch.GetWarningMark(seer, target));
                Mark.Append(Marshall.GetWarningMark(seer, target));

                if (seer.Is(CustomRoles.PlagueBearer))
                {
                    if (PlagueBearer.isPlagued(seer.PlayerId, target.PlayerId))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                        PlagueBearer.SendRPC(seer, target);
                    }
                }

                if (seer.Is(CustomRoles.Arsonist))
                {
                    if (seer.IsDousedPlayer(target))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                    }
                    else if (
                        Main.currentDousingTarget != byte.MaxValue &&
                        Main.currentDousingTarget == target.PlayerId
                    )
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                    }
                }
                if (seer.Is(CustomRoles.Revolutionist))
                {
                    if (seer.IsDrawPlayer(target))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                    }
                    else if (
                        Main.currentDrawTarget != byte.MaxValue &&
                        Main.currentDrawTarget == target.PlayerId
                    )
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                    }
                }
                if (seer.Is(CustomRoles.Farseer))
                {
                    if (
                        Main.currentDrawTarget != byte.MaxValue &&
                        Main.currentDrawTarget == target.PlayerId
                    )
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                    }
                }

                Mark.Append(Executioner.TargetMark(seer, target));

            //    Mark.Append(Lawyer.TargetMark(seer, target));

                Mark.Append(Gamer.TargetMark(seer, target));

                Mark.Append(PlagueDoctor.TargetMark(seer, target));

                Mark.Append(Yandere.TargetMark(seer, target));

                if (seer.PlayerId == target.PlayerId && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 2))
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>");

                if (seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 1))
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>");

                if (seer.Data.IsDead && Medic.InProtect(target.PlayerId) && !seer.Is(CustomRoles.Medic))
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>");

                Mark.Append(Gamer.TargetMark(seer, target));

                Mark.Append(Totocalcio.TargetMark(seer, target));
                Mark.Append(Lawyer.LawyerMark(seer, target));
                Mark.Append(Romantic.TargetMark(seer, target));

                if (seer.Is(CustomRoles.Puppeteer))
                {
                    if (seer.Is(CustomRoles.Puppeteer) &&
                    Main.PuppeteerList.ContainsValue(seer.PlayerId) &&
                    Main.PuppeteerList.ContainsKey(target.PlayerId))
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                }
                if (seer.Is(CustomRoles.CovenLeader))
                {
                    if (seer.Is(CustomRoles.CovenLeader) &&
                    Main.CovenLeaderList.ContainsValue(seer.PlayerId) &&
                    Main.CovenLeaderList.ContainsKey(target.PlayerId))
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.CovenLeader)}>◆</color>");
                }
                if (seer.Is(CustomRoles.NWitch))
                {
                    if (seer.Is(CustomRoles.NWitch) &&
                    Main.TaglockedList.ContainsValue(seer.PlayerId) &&
                    Main.TaglockedList.ContainsKey(target.PlayerId))
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.NWitch)}>◆</color>");
                }
                if (Sniper.IsEnable && target.AmOwner)
                {
                    //銃声が聞こえるかチェック
                    Mark.Append(Sniper.GetShotNotify(target.PlayerId));

                }
                if (seer.Is(CustomRoles.EvilTracker)) Mark.Append(EvilTracker.GetTargetMark(seer, target));
                if (seer.Is(CustomRoles.Tracker)) Mark.Append(Tracker.GetTargetMark(seer, target));
                //タスクが終わりそうなSnitchがいるとき、インポスター/キル可能なニュートラルに警告が表示される
                Mark.Append(Snitch.GetWarningArrow(seer, target));

                if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SuperStar), "★"));

                if (target.Is(CustomRoles.QL) && Options.EveryOneKnowQL.GetBool())
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.QL), "♛"));

                if (target.Is(CustomRoles.Captain))
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Captain), " ★Cap★ "));

                if (target.Is(CustomRoles.Hotpotato))
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Hotpotato), "●"));

                if (BallLightning.IsGhost(target))
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■"));

                //ハートマークを付ける(会議中MOD視点)
                if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers))
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                }
                else if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Data.IsDead)
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                }
                else if (__instance.Is(CustomRoles.Ntr) || PlayerControl.LocalPlayer.Is(CustomRoles.Ntr))
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                }
                else if (__instance == PlayerControl.LocalPlayer && CustomRolesHelper.RoleExist(CustomRoles.Ntr))
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                }


                //矢印オプションありならタスクが終わったスニッチはインポスター/キル可能なニュートラルの方角がわかる
                Suffix.Append(Snitch.GetSnitchArrow(seer, target));

                Suffix.Append(BountyHunter.GetTargetArrow(seer, target));

                Suffix.Append(Mortician.GetTargetArrow(seer, target));

                Suffix.Append(EvilTracker.GetTargetArrow(seer, target));

                Suffix.Append(Bloodhound.GetTargetArrow(seer, target));

                Suffix.Append(Tracker.GetTrackerArrow(seer, target));

                Suffix.Append(Yandere.GetTargetArrow(seer, target));

                Suffix.Append(Deathpact.GetDeathpactPlayerArrow(seer));
                Suffix.Append(Deathpact.GetDeathpactMark(seer, target));
                Suffix.Append(Spiritualist.GetSpiritualistArrow(seer, target));

                if (Vulture.ArrowsPointingToDeadBody.GetBool())
                    Suffix.Append(Vulture.GetTargetArrow(seer, target));

                Suffix.Append(Tracefinder.GetTargetArrow(seer, target));

                if (GameStates.IsInTask && seer.Is(CustomRoles.AntiAdminer))
                {
                    AntiAdminer.FixedUpdate();
                    if (target.AmOwner)
                    {
                        if (AntiAdminer.IsAdminWatch) Suffix.Append("★" + GetString("AntiAdminerAD"));
                        if (AntiAdminer.IsVitalWatch) Suffix.Append("★" + GetString("AntiAdminerVI"));
                        if (AntiAdminer.IsDoorLogWatch) Suffix.Append("★" + GetString("AntiAdminerDL"));
                        if (AntiAdminer.IsCameraWatch) Suffix.Append("★" + GetString("AntiAdminerCA"));
                    }
                }

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                    Suffix.Append(SoloKombatManager.GetDisplayHealth(target));
                    
                /*if(main.AmDebugger.Value && main.BlockKilling.TryGetValue(target.PlayerId, out var isBlocked)) {
                    Mark = isBlocked ? "(true)" : "(false)";
                }*/
                
                if ((Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive)
                    RealName = $"<size=0>{RealName}</size> ";

                bool targetDevoured = Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(target.PlayerId));
                if (targetDevoured)
                    RealName = GetString("DevouredName");

                string DeathReason = seer.Data.IsDead && seer.KnowDeathReason(target) ? $"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(target.PlayerId))})" : "";
                //Mark・Suffixの適用
                target.cosmetics.nameText.text = $"{RealName}{DeathReason}{Mark}";

                if (Suffix.ToString() != "")
                {
                    //名前が2行になると役職テキストを上にずらす必要がある
                    RoleText.transform.SetLocalY(0.35f);
                    target.cosmetics.nameText.text += "\r\n" + Suffix.ToString();

                }
                else
                {
                    //役職テキストの座標を初期値に戻す
                    RoleText.transform.SetLocalY(0.2f);
                }
            }
            else
            {
                //役職テキストの座標を初期値に戻す
                RoleText.transform.SetLocalY(0.2f);
            }
        }
    }
    public static void CaptainSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (CustomRoles.Captain.IsEnable() && Main.IsSeniormanagementDead == false)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (!player.Data.IsDead && player.PlayerId != deathId && !player.Is(CustomRoles.Captain)) continue;
                if (!player.Is(CustomRoles.Captain)) continue;

                    Main.IsSeniormanagementDead = true;
                foreach (var partnerPlayer in Main.AllPlayerControls)
                {
                    if (partnerPlayer.PlayerId != deathId && partnerPlayer.Is(CustomRoles.Seniormanagement) && !partnerPlayer.Data.IsDead)
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.Ownerless;
                        if (isExiled)
                        {
                            if (now) partnerPlayer?.RpcExileV2();
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Ownerless, partnerPlayer.PlayerId);
                        }
                        else
                        {
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer);
                        }
                    }
                }
            }
        }
    }
    //FIXME: 役職クラス化のタイミングで、このメソッドは移動予定
    public static void LoversSuicide(byte deathId = 0x7f, bool isExiled = false)
    {
        if ((CustomRoles.Lovers.IsEnable()||CustomRoles.PlatonicLover.IsEnable()) && Main.isLoversDead == false)
        {
            foreach (var loversPlayer in Main.LoversPlayers)
            {
                //生きていて死ぬ予定でなければスキップ
                if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;

                Main.isLoversDead = true;
                foreach (var partnerPlayer in Main.LoversPlayers)
                {
                    //本人ならスキップ
                    if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;

                    //残った恋人を全て殺す(2人以上可)
                    //生きていて死ぬ予定もない場合は心中
                    if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead)
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide;
                        if (isExiled)
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId);
                        else
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer);
                    }
                }
            }
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
class PlayerStartPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var roleText = UnityEngine.Object.Instantiate(__instance.cosmetics.nameText);
        roleText.transform.SetParent(__instance.cosmetics.nameText.transform);
        roleText.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        roleText.fontSize -= 1.2f;
        roleText.text = "RoleText";
        roleText.gameObject.name = "RoleText";
        roleText.enabled = false;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
class SetColorPatch
{
    public static bool IsAntiGlitchDisabled = false;
    public static bool Prefix(PlayerControl __instance, int bodyColor)
    {
        //色変更バグ対策
        if (!AmongUsClient.Instance.AmHost || __instance.CurrentOutfit.ColorId == bodyColor || IsAntiGlitchDisabled) return true;
        return true;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {

        Witch.OnEnterVent(pc);
        HexMaster.OnEnterVent(pc);
        GrudgeSheriff.OnEnterVent(pc);

        if (pc.Is(CustomRoles.Mayor))
        {
            if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt())
            {
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.ReportDeadBody(null);
            }
        }

        if (pc.Is(CustomRoles.Chairman))
        {
            if (Main.ChairmanUsedButtonCount.TryGetValue(pc.PlayerId, out var Ccount) && Ccount < Options.ChairmanNumOfUseButton.GetInt())
            {
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.ReportDeadBody(null);
            }
        }

        if (pc.Is(CustomRoles.Snitch))
        {
            if (Main.SnitchUsedButtonCount.TryGetValue(pc.PlayerId, out var count1) && count1 < Snitch.SnitchNumOfUseButton.GetInt())
            {
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.ReportDeadBody(null);
            }
        }

        if (pc.Is(CustomRoles.Paranoia))
        {
            if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.ParanoiaNumOfUseButton.GetInt())
            {
                Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                if (AmongUsClient.Instance.AmHost)
                {
                    new LateTask(() =>
                    {
                        Utils.SendMessage(GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]).ToString(), pc.PlayerId);
                    }, 4.0f, "Skill Remain Message");
                }
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.NoCheckStartMeeting(pc?.Data);
            }
        }

        if (pc.Is(CustomRoles.Mario))
        {
            Main.MarioVentCount.TryAdd(pc.PlayerId, 0);
            Main.MarioVentCount[pc.PlayerId]++;
            Utils.NotifyRoles(SpecifySeer: pc);
            if (pc.AmOwner)
            {
           //     if (Main.MarioVentCount[pc.PlayerId] % 5 == 0) CustomSoundsManager.Play("MarioCoin");
           //     else CustomSoundsManager.Play("MarioJump");
            }
            if (AmongUsClient.Instance.AmHost && Main.MarioVentCount[pc.PlayerId] >= Options.MarioVentNumWin.GetInt())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            }
        }

        if (!AmongUsClient.Instance.AmHost) return;

        Main.LastEnteredVent.Remove(pc.PlayerId);
        Main.LastEnteredVent.Add(pc.PlayerId, __instance);
        Main.LastEnteredVentLocation.Remove(pc.PlayerId);
        Main.LastEnteredVentLocation.Add(pc.PlayerId, pc.GetTruePosition());

        Swooper.OnEnterVent(pc, __instance);
        Wraith.OnEnterVent(pc, __instance);
        Addict.OnEnterVent(pc, __instance);
        Chameleon.OnEnterVent(pc, __instance);

        if (pc.Is(CustomRoles.Veteran))
        {
            Main.VeteranInProtect.Remove(pc.PlayerId);
            Main.VeteranInProtect.Add(pc.PlayerId, Utils.GetTimeStamp(DateTime.Now));
            Main.VeteranNumOfUsed[pc.PlayerId]--;
            pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("Gunload");
            pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
        }
        if (pc.Is(CustomRoles.Grenadier))
        {
            if (pc.Is(CustomRoles.Madmate))
            {
                Main.MadGrenadierBlinding.Remove(pc.PlayerId);
                Main.MadGrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }
            else
            {
                Main.GrenadierBlinding.Remove(pc.PlayerId);
                Main.GrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }
            pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("FlashBang");
            pc.Notify(GetString("GrenadierSkillInUse"), Options.GrenadierSkillDuration.GetFloat());
            Utils.MarkEveryoneDirtySettings();
        }
        if (pc.Is(CustomRoles.TimeMaster))
        {
            Main.TimeMasterInProtect.Remove(pc.PlayerId);
            Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.Notify(GetString("TimeMasterOnGuard"), Options.TimeMasterSkillDuration.GetFloat());
            foreach (var player in Main.AllPlayerControls)
            {
                if (Main.TimeMasterbacktrack.ContainsKey(player.PlayerId))
                {
                    var position = Main.TimeMasterbacktrack[player.PlayerId];
                Utils.TP(player.NetTransform, position);
                Main.TimeMasterbacktrack.Remove(player.PlayerId);
                }
                else
                {
                    Main.TimeMasterbacktrack.Add(player.PlayerId, player.GetTruePosition());
                }
            }
        }
        if (pc.Is(CustomRoles.TimeStops))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("THEWORLD");
            Main.TimeStopsInProtect.Remove(pc.PlayerId);
            Main.TimeStopsInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("THEWORLD");
            pc.Notify(GetString("TimeStopsOnGuard"), Options.TimeStopsSkillDuration.GetFloat());
            foreach (var player in Main.AllPlayerControls)
            {
                if (pc == player) continue;
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                NameNotifyManager.Notify(player, Utils.ColorString(Utils.GetRoleColor(CustomRoles.TimeStops), GetString("ForTimeStops")));
                var tmpSpeed1 = Main.AllPlayerSpeed[player.PlayerId];
                Main.TimeStopsstop.Add(player.PlayerId);
                Main.AllPlayerSpeed[player.PlayerId] = Main.MinSpeed;
                player.MarkDirtySettings();
                new LateTask(() =>
                {
                    Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Main.MinSpeed + tmpSpeed1;
                    player.MarkDirtySettings();
                    Main.TimeStopsstop.Remove(player.PlayerId);
                    RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
                }, Options.TimeStopsSkillDuration.GetFloat(), "Trapper BlockMove");
            }
        }
        if (pc.Is(CustomRoles.Rudepeople))
        {
            Main.RudepeopleInProtect.Remove(pc.PlayerId);
            Main.RudepeopleInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            Main.RudepeopleNumOfUsed[pc.PlayerId]--;
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("RNM");
            pc.Notify(GetString("RudepeopleOnGuard"), Options.RudepeopleSkillDuration.GetFloat());
        }
        if (pc.Is(CustomRoles.GlennQuagmire))
        {
            List<PlayerControl> list = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId).ToList();
            if (list.Count < 1)
            {
                Logger.Info($"Q哥没有目标", "GlennQuagmire");
            }
            else
            {
                list = list.OrderBy(x => Vector2.Distance(pc.GetTruePosition(), x.GetTruePosition())).ToList();
                var target = list[0];
                if (target.GetCustomRole().IsImpostor())
                {
                    pc.RPCPlayCustomSound("giggity");
                    target.SetRealKiller(pc);
                    target.RpcCheckAndMurder(target);
                    pc.RpcGuardAndKill();
                        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Creation;
                }
                else
                {
                    if (Main.ForSourcePlague.Contains(pc.PlayerId))
                    {
                        new LateTask(() =>
                        {
                            Utils.TP(pc.NetTransform, target.GetTruePosition());
                            Main.ForSourcePlague.Add(target.PlayerId);
                            Utils.NotifyRoles();
                        }, 2.5f, ("NotRight!"));
                    }
                    new LateTask(() =>
                    {
                        Utils.TP(pc.NetTransform, target.GetTruePosition());
                        Utils.NotifyRoles();
                    }, 2.5f, ("NotRight!"));

                }
            }
        }
        if (pc.Is(CustomRoles.King) && Main.KingCanpc.Contains(pc.PlayerId))
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King); 
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            foreach (var player in Main.AllPlayerControls)
            {
                if (Main.ForKing.Contains(player.PlayerId))
                {
                    CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                }
            }
        }
        if (pc.Is(CustomRoles.DemolitionManiac))
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (Main.DemolitionManiacKill.Contains(player.PlayerId))
                {
                    Main.DemolitionManiacKill.Remove(player.PlayerId);
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    player.SetRealKiller(pc);
                    player.RpcMurderPlayerV3(player);
                }
            }
        }
        if (pc.Is(CustomRoles.DovesOfNeace))
        {
            if (Main.DovesOfNeaceNumOfUsed[pc.PlayerId] < 1)
            {
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc.Notify(GetString("DovesOfNeaceMaxUsage"));
            }
            else
            {
                Main.DovesOfNeaceNumOfUsed[pc.PlayerId]--;
                pc.RpcGuardAndKill(pc);
                Main.AllAlivePlayerControls.Where(x => 
                pc.Is(CustomRoles.Madmate) ?
                (x.CanUseKillButton() && x.GetCustomRole().IsCrewmate()) :
                (x.CanUseKillButton())
                ).Do(x =>
                {
                     x.RPCPlayCustomSound("Dove");
                     x.ResetKillCooldown();
                     x.SetKillCooldown();
                     if (x.Is(CustomRoles.SerialKiller))
                        { SerialKiller.OnReportDeadBody(); }
                    x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), GetString("DovesOfNeaceSkillNotify")));
                });
                pc.RPCPlayCustomSound("Dove");
                pc.Notify(string.Format(GetString("DovesOfNeaceOnGuard"), Main.DovesOfNeaceNumOfUsed[pc.PlayerId]));
            }
        }
    }
}
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
class CoEnterVentPatch
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (AmongUsClient.Instance.IsGameStarted &&
            __instance.myPlayer.IsDouseDone())
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (pc != __instance.myPlayer)
                {
                    //生存者は焼殺
                    pc.SetRealKiller(__instance.myPlayer);
                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Torched;
                    pc.RpcMurderPlayerV3(pc);
                    Main.PlayerStates[pc.PlayerId].SetDead();
                }
            }
            foreach (var pc in Main.AllPlayerControls) pc.KillFlash();
            CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist); //焼殺で勝利した人も勝利させる
            CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
            return true;
        }

        if (AmongUsClient.Instance.IsGameStarted && __instance.myPlayer.IsDrawDone())//完成拉拢任务的玩家跳管后
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Revolutionist);//革命者胜利
            Utils.GetDrawPlayerCount(__instance.myPlayer.PlayerId, out var x);
            CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
            foreach (var apc in x) CustomWinnerHolder.WinnerIds.Add(apc.PlayerId);//胜利玩家
            return true;
        }

        //处理弹出管道的阻塞
        if ((__instance.myPlayer.Data.Role.Role != RoleTypes.Engineer && //不是工程师
        !__instance.myPlayer.CanUseImpostorVentButton()) || //不能使用内鬼的跳管按钮
        (__instance.myPlayer.Is(CustomRoles.Mayor) && Main.MayorUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count) && count >= Options.MayorNumOfUseButton.GetInt()) ||
        (__instance.myPlayer.Is(CustomRoles.Chairman) && Main.ChairmanUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count1) && count1 >= Options.ChairmanNumOfUseButton.GetInt()) ||
        (__instance.myPlayer.Is(CustomRoles.Snitch) && Main.SnitchUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count2) && count2 >= Snitch.SnitchNumOfUseButton.GetInt()) ||
        (__instance.myPlayer.Is(CustomRoles.Paranoia) && Main.ParaUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count3) && count3 >= Options.ParanoiaNumOfUseButton.GetInt()) ||
        (__instance.myPlayer.Is(CustomRoles.Veteran) && Main.VeteranNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count4) && count4 < 1) ||
        (__instance.myPlayer.Is(CustomRoles.DovesOfNeace) && Main.DovesOfNeaceNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count5) && count5 < 1) ||
        (__instance.myPlayer.Is(CustomRoles.Rudepeople) && Main.RudepeopleNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count6) && count6 < 1) ||
        (__instance.myPlayer.Is(CustomRoles.GrudgeSheriff) && (!GrudgeSheriff.CanUseKillButton(__instance.myPlayer.PlayerId) || GrudgeSheriff.KillWaitPlayer.ContainsKey(__instance.myPlayer.PlayerId)))
        )
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
            writer.WritePacked(127);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            new LateTask(() =>
            {
                int clientId = __instance.myPlayer.GetClientId();
                MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                writer2.Write(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer2);
            }, 0.5f, "Fix DesyncImpostor Stuck");
            return false;
        }

        if (__instance.myPlayer.Is(CustomRoles.Swooper))
            Swooper.OnCoEnterVent(__instance, id);

        if (__instance.myPlayer.Is(CustomRoles.Wraith))
            Wraith.OnCoEnterVent(__instance, id);

        if (__instance.myPlayer.Is(CustomRoles.Chameleon))
            Chameleon.OnCoEnterVent(__instance, id);

        if (__instance.myPlayer.Is(CustomRoles.DovesOfNeace)) __instance.myPlayer.Notify(GetString("DovesOfNeaceMaxUsage"));
        if (__instance.myPlayer.Is(CustomRoles.Veteran)) __instance.myPlayer.Notify(GetString("VeteranMaxUsage"));
        if (__instance.myPlayer.Is(CustomRoles.Rudepeople)) __instance.myPlayer.Notify(GetString("RudepeopleMaxUsage"));

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
class SetNamePatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] string name)
    {
    }
}
[HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
class GameDataCompleteTaskPatch
{
    public static void Postfix(PlayerControl pc)
    {
        Logger.Info($"TaskComplete:{pc.GetNameWithRole()}", "CompleteTask");
        Main.PlayerStates[pc.PlayerId].UpdateTask(pc);
        Utils.NotifyRoles();
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
class PlayerControlCompleteTaskPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        var player = __instance;

        if (Workhorse.OnCompleteTask(player)) //タスク勝利をキャンセル
            return false;

        //来自资本主义的任务
        if (Main.CapitalismAddTask.ContainsKey(player.PlayerId))
        {
            var taskState = player.GetPlayerTaskState();
            taskState.AllTasksCount += Main.CapitalismAddTask[player.PlayerId];
            Main.CapitalismAddTask.Remove(player.PlayerId);
            taskState.CompletedTasksCount++;
            GameData.Instance.RpcSetTasks(player.PlayerId, new byte[0]); //タスクを再配布
            player.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: player);
            return false;
        }

        return true;
    }
    public static void Postfix(PlayerControl __instance)
    {
        var pc = __instance;
        Snitch.OnCompleteTask(pc);

        var isTaskFinish = pc.GetPlayerTaskState().IsTaskFinished;
        if (isTaskFinish && pc.Is(CustomRoles.Snitch) && pc.Is(CustomRoles.Madmate))
        {
            foreach (var impostor in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)))
                NameColorManager.Add(impostor.PlayerId, pc.PlayerId, "#ff1919");
            Utils.NotifyRoles(SpecifySeer: pc);
        }
        if ((isTaskFinish &&
            pc.GetCustomRole() is CustomRoles.Doctor or CustomRoles.Sunnyboy) ||
            pc.GetCustomRole() is CustomRoles.SpeedBooster)
        {
            //ライターもしくはスピードブースターもしくはドクターがいる試合のみタスク終了時にCustomSyncAllSettingsを実行する
            Utils.MarkEveryoneDirtySettings();
        }
        if (isTaskFinish && pc.Is(CustomRoles.LoveCutter) && Options.LoveCutterKnow.GetBool())
        {
            foreach (var killer in Main.AllAlivePlayerControls
                .Where(pc => (pc.Is(CustomRoleTypes.Impostor) || pc.IsNeutralKiller())
                && !(pc.Is(CustomRoles.Arsonist) || pc.Is(CustomRoles.PlatonicLover) || pc.Is(CustomRoles.Totocalcio))))
            {
                NameColorManager.Add(pc.PlayerId, killer.PlayerId);
            }
            Utils.NotifyRoles(SpecifySeer: pc);
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
class PlayerControlProtectPlayerPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}", "ProtectPlayer");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
class PlayerControlRemoveProtectionPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        Logger.Info($"{__instance.GetNameWithRole()}", "RemoveProtection");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
class PlayerControlSetRolePatch
{
    public static bool Prefix(PlayerControl __instance, ref RoleTypes roleType)
    {
        var target = __instance;
        var targetName = __instance.GetNameWithRole();
        Logger.Info($"{targetName} =>{roleType}", "PlayerControl.RpcSetRole");
        if (!ShipStatus.Instance.enabled) return true;
        if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
        {
            var targetIsKiller = target.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(target.PlayerId);
            var ghostRoles = new Dictionary<PlayerControl, RoleTypes>();
            foreach (var seer in Main.AllPlayerControls)
            {
                var self = seer.PlayerId == target.PlayerId;
                var seerIsKiller = seer.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(seer.PlayerId);
                if (target.Is(CustomRoles.EvilSpirit))
                {
                    ghostRoles[seer] = RoleTypes.GuardianAngel;
                }
                else if((self && targetIsKiller) || (!seerIsKiller && (target.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(target.PlayerId)) || (target.Is(CustomRoles.Refugee))))
                {
                    ghostRoles[seer] = RoleTypes.ImpostorGhost;
                }
                else
                {
                    ghostRoles[seer] = RoleTypes.CrewmateGhost;
                }
            }
            if (target.Is(CustomRoles.EvilSpirit))
            {
                roleType = RoleTypes.GuardianAngel;
            }
            if (ghostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
            {
                roleType = RoleTypes.CrewmateGhost;
            }
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
            {
                roleType = RoleTypes.ImpostorGhost;
            }
            else
            {
                foreach ((var seer, var role) in ghostRoles)
                {
                    Logger.Info($"Desync {targetName} =>{role} for{seer.GetNameWithRole()}", "PlayerControl.RpcSetRole");
                    target.RpcSetRoleDesync(role, seer.GetClientId());
                }
                return false;
            }
        }
        return true;
    }
}