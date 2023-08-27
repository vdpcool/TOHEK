using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using TOHE.CustomCosmetics.CustomCosmeticsData;
using UnityEngine;

namespace TOHE.CustomCosmetics;

public class CustomVisor
{
    public static bool isAdded = false;
    static readonly List<VisorData> visorData = new();
    public static readonly List<CustomVisorData> customVisorData = new();
    [HarmonyPatch(typeof(HatManager), nameof(HatManager.GetVisorById))]
    class UnlockedVisorPatch
    {
        public static void Postfix(HatManager __instance)
        {
            if (isAdded || !DownLoadClassVisor.IsEndDownload) return;
            isAdded = true;
            Main.Logger.LogInfo("[CustomVisor] バイザー読み込み処理開始");
            var AllVisors = __instance.allVisors.ToList();

            var plateDir = new DirectoryInfo("TOHEK\\CustomVisorsChache");
            if (!plateDir.Exists) plateDir.Create();
            var Files = plateDir.GetFiles("*.png").ToList();
            Files.AddRange(plateDir.GetFiles("*.jpg"));
            var CustomPlates = new List<VisorData>();
            foreach (var file in Files)
            {
                try
                {
                    var FileName = file.Name[0..^4];
                    var Data = DownLoadClassVisor.Visordetails.FirstOrDefault(data => data.resource.Replace(".png", "") == FileName);
                    VisorViewData vvd = new VisorViewData
                    {
                        IdleFrame = Data.IsTOP
                        ? Utils.CreateSprite("TOHEK\\CustomVisorsChache\\" + file.Name, true)
                        : LoadTex.loadSprite("TOHEK\\CustomVisorsChache\\" + file.Name)
                    };
                    var plate = new CustomVisorData(vvd)
                    {
                        name = Data.name + "\nby " + Data.author,
                        ProductId = "CustomVisors_" + Data.resource.Replace(".png", "").Replace(".jpg", ""),
                        BundleId = "CustomVisors_" + Data.resource.Replace(".png", "").Replace(".jpg", ""),
                        displayOrder = 99,
                        ChipOffset = new Vector2(0f, 0.2f),
                        Free = true,
                        SpritePreview = vvd.IdleFrame
                    };
                    visorData.Add(plate);
                    customVisorData.Add(plate);
                    //Main.Logger.LogInfo("[CustomVisor] バイザー読み込み完了:" + file.Name);
                }
                catch (Exception e)
                {
                    Main.Logger.LogError("[CustomVisor:Error] エラー:CustomVisorの読み込みに失敗しました:" + file.FullName);
                    Main.Logger.LogError(file.FullName + "のエラー内容:" + e);
                }
            }
            Main.Logger.LogInfo("[CustomVisor] バイザー読み込み処理終了");
            AllVisors.AddRange(visorData);
            __instance.allVisors = AllVisors.ToArray();
        }
    }
}