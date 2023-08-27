using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using TOHE.CustomCosmetics.CustomCosmeticsData;
using UnityEngine;
using static TOHE.CustomCosmetics.CustomCosmeticsData.CustomPlateData;

namespace TOHE.CustomCosmetics;

public class CustomPlate
{
    public static bool isAdded = false;
    static readonly List<NamePlateData> namePlateData = new();
    public static readonly List<CustomPlateData> customPlateData = new();
    [HarmonyPatch(typeof(HatManager), nameof(HatManager.GetNamePlateById))]
    class UnlockedNamePlatesPatch
    {
        public static void Postfix(HatManager __instance)
        {
            if (isAdded || !DownLoadClass.IsEndDownload) return;
            isAdded = true;
            Main.Logger.LogInfo("[CustomPlate] プレート読み込み処理開始");
            var AllPlates = __instance.allNamePlates.ToList();

            var plateDir = new DirectoryInfo("TOHEK\\CustomPlatesChache");
            if (!plateDir.Exists) plateDir.Create();
            var Files = plateDir.GetFiles("*.png").ToList();
            Files.AddRange(plateDir.GetFiles("*.jpg"));
            var CustomPlates = new List<NamePlateData>();
            foreach (var file in Files)
            {
                try
                {
                    var FileName = file.Name[0..^4];
                    var Data = DownLoadClass.platedetails.FirstOrDefault(data => data.resource.Replace(".png", "") == FileName);
                    TempPlateViewData tpvd = new()
                    {
                        Image = LoadTex.loadSprite("TOHEK\\CustomPlatesChache\\" + Data.resource)
                    };
                    var plate = new CustomPlateData();
                    plate.tpvd = tpvd;
                    plate.name = Data.name + "\nby " + Data.author;
                    plate.ProductId = "CustomNamePlates_" + Data.resource.Replace(".png", "").Replace(".jpg", "");
                    plate.BundleId = "CustomNamePlates_" + Data.resource.Replace(".png", "").Replace(".jpg", "");
                    plate.displayOrder = 99;
                    plate.ChipOffset = new Vector2(0f, 0.2f);
                    plate.Free = true;
                    plate.SpritePreview = tpvd.Image;
                    //CustomPlates.Add(plate);
                    //AllPlates.Add(plate);
                    namePlateData.Add(plate);
                    customPlateData.Add(plate);
                    //Main.Logger.LogInfo("[CustomPlate] プレート読み込み完了:" + file.Name);
                }
                catch (Exception e)
                {
                    Main.Logger.LogError("[CustomPlate:Error] エラー:CustomNamePlateの読み込みに失敗しました:" + file.FullName);
                    Main.Logger.LogError(file.FullName + "のエラー内容:" + e);
                }
            }
            Main.Logger.LogInfo("[CustomPlate] プレート読み込み処理終了");
            AllPlates.AddRange(namePlateData);
            __instance.allNamePlates = AllPlates.ToArray();
        }
    }
}