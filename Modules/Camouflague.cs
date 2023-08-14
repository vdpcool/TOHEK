using AmongUs.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Impostor;

namespace TOHE;

static class PlayerOutfitExtension
{
    public static GameData.PlayerOutfit Set(this GameData.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId, string nameplateId)
    {
        instance.PlayerName = playerName;
        instance.ColorId = colorId;
        instance.HatId = hatId;
        instance.SkinId = skinId;
        instance.VisorId = visorId;
        instance.PetId = petId;
        instance.NamePlateId = nameplateId;
        return instance;
    }
    public static bool Compare(this GameData.PlayerOutfit instance, GameData.PlayerOutfit targetOutfit)
    {
        return  instance.PlayerName == targetOutfit.PlayerName &&
                instance.ColorId == targetOutfit.ColorId &&
                instance.HatId == targetOutfit.HatId &&
                instance.SkinId == targetOutfit.SkinId &&
                instance.VisorId == targetOutfit.VisorId &&
                instance.PetId == targetOutfit.PetId &&
                instance.NamePlateId == targetOutfit.NamePlateId;

    }
    public static string GetString(this GameData.PlayerOutfit instance)
    {
        return $"{instance.PlayerName} Color:{instance.ColorId} {instance.HatId} {instance.SkinId} {instance.VisorId} {instance.PetId} {instance.NamePlateId}";
    }
}
public static class Camouflage
{
    static GameData.PlayerOutfit CamouflageOutfit = new GameData.PlayerOutfit().Set("", 15, "", "", "", "", ""); // Default

    public static bool IsCamouflage;
    public static Dictionary<byte, GameData.PlayerOutfit> PlayerSkins = new();

    public static void Init()
    {
        IsCamouflage = false;
        PlayerSkins.Clear();

        switch (Options.KCamouflageMode.GetValue())
        { 
            case 0: // Default
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 15, "", "", "", "", "");
                break;

            case 1: // Host's outfit
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", DataManager.Player.Customization.Color, DataManager.Player.Customization.Hat, DataManager.Player.Customization.Skin, DataManager.Player.Customization.Visor, DataManager.Player.Customization.Pet, DataManager.Player.Customization.NamePlate);
                break;

            case 2: // K
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 7, "hat_whitetophat", "", "visor_BubbleBumVisor", "", "");
                break;

            case 3: // Karped1em
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_pk05_Plant", "", "visor_BubbleBumVisor", "", "");
                break;

            case 4: // Loonie
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 12, "hat_pkHW01_Wolf", "skin_scarfskin", "visor_Carrot", "pet_Pusheen", "");
                break;

            case 5: // Lauryn
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_rabbitEars", "skin_Bananaskin", "visor_BubbleBumVisor", "pet_Pusheen", "");
                break;

            case 6: // Moe
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 4, "hat_mira_headset_yellow", "skin_SuitB", "visor_lollipopCrew", "pet_EmptyPet", "");
                break;

            case 7: // Ryuk
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 6, "hat_osiris", "skin_D2Osiris", "", "pet_Snow", "");
                break;

            case 8: // Levi
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 11, "", "", "", "", "");
                break;
        }
    }
    public static void CheckCamouflage()
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || CustomRoles.Camouflager.IsEnable()))) return;

        var oldIsCamouflage = IsCamouflage;

        IsCamouflage = (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive;

        if (oldIsCamouflage != IsCamouflage)
        {
            Main.AllPlayerControls.Do(pc => RpcSetSkin(pc));
            Utils.NotifyRoles();
        }
    }
    public static void ChangeSkin()
    {
        if (!(AmongUsClient.Instance.AmHost && Options.IsSyncColorMode)) return;
        List<PlayerControl> chengePlayers = new();
        foreach (var p in Main.AllPlayerControls)
        {
            chengePlayers.Add(p);
        }
        chengePlayers = chengePlayers.OrderBy(a => Guid.NewGuid()).ToList();

        switch (Options.GetSyncColorMode())
        {
            case SyncColorMode.None:
                break;

            case SyncColorMode.Clone:
                PlayerControl target = chengePlayers[0];
                Logger.Info("選定先:" + target.GetRealName(), "CloneMode");
                Main.AllPlayerControls.Do(pc => RpcSetSkin(pc, target));
                break;

            case SyncColorMode.fif_fif:
                bool tar1 = true;
                PlayerControl target1 = null;
                PlayerControl target2 = null;
                foreach (var p in chengePlayers)
                {
                    if (target1 == null)
                    {
                        target1 = chengePlayers[p.PlayerId];
                        Logger.Info("選定先1:" + target1.GetRealName(), "fif_fifMode");
                    }
                    else if (target2 == null)
                    {
                        target2 = chengePlayers[p.PlayerId];
                        Logger.Info("選定先2:" + target2.GetRealName(), "fif_fifMode");
                    }

                    if (tar1) RpcSetSkin(p, target1);
                    else RpcSetSkin(p, target2);

                    tar1 = !tar1;
                }
                break;

            case SyncColorMode.ThreeCornered:

                int Count = 0;
                PlayerControl Target1 = null;
                PlayerControl Target2 = null;
                PlayerControl Target3 = null;
                foreach (var p in chengePlayers)
                {
                    if (Target1 == null)
                    {
                        Target1 = chengePlayers[p.PlayerId];
                        Logger.Info("選定先1:" + Target1.GetRealName(), "ThreeCornered");
                    }
                    else if (Target2 == null)
                    {
                        Target2 = chengePlayers[p.PlayerId];
                        Logger.Info("選定先2:" + Target2.GetRealName(), "ThreeCornered");
                    }
                    else if (Target3 == null)
                    {
                        Target3 = chengePlayers[p.PlayerId];
                        Logger.Info("選定先3:" + Target3.GetRealName(), "ThreeCornered");
                    }

                    switch(Count % 3)
                    {
                        case 0: RpcSetSkin(p, Target1); break;
                        case 1: RpcSetSkin(p, Target2); break;
                        case 2: RpcSetSkin(p, Target3); break;
                    }
                    Count++;
                }
                break;

            case SyncColorMode.Twin:

                int count = 0;
                PlayerControl targetT = null;
                foreach (var p in chengePlayers)
                {
                    count++;
                    if (count % 2 == 1)
                    {
                        targetT = chengePlayers[p.PlayerId];
                        Logger.Info("選定先:" + targetT.GetRealName(), "TwinMode");
                    }
                    RpcSetSkin(p, targetT);
                }
                break;
        }
    }
    public static void RpcSetSkin(PlayerControl target, bool ForceRevert = false, bool RevertToDefault = false)
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || CustomRoles.Camouflager.IsEnable()))) return;
        if (target == null) return;

        var id = target.PlayerId;

        if (IsCamouflage)
        {
            //コミュサボ中

            //死んでいたら処理しない
            if (Main.PlayerStates[id].IsDead) return;
        }

        var newOutfit = CamouflageOutfit;

        if (!IsCamouflage || ForceRevert)
        {
            //コミュサボ解除または強制解除

            if (Main.CheckShapeshift.TryGetValue(id, out var shapeshifting) && shapeshifting && !RevertToDefault)
            {
                //シェイプシフターなら今の姿のidに変更
                id = Main.ShapeshiftTarget[id];
            }

            newOutfit = PlayerSkins[id];
        }
        Logger.Info($"newOutfit={newOutfit.GetString()}", "RpcSetSkin");

        var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.PlayerName})");

        target.SetName(newOutfit.PlayerName);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetName)
            .Write(newOutfit.PlayerName)
            .EndRpc();

        target.SetColor(newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
            .Write(newOutfit.ColorId)
            .EndRpc();

        target.SetHat(newOutfit.HatId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .EndRpc();

        target.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .EndRpc();

        target.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .EndRpc();

        target.SetPet(newOutfit.PetId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .EndRpc();

        target.SetNamePlate(newOutfit.NamePlateId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetNamePlateStr)
            .Write(newOutfit.NamePlateId)
            .EndRpc();

        sender.SendMessage();
    }
}