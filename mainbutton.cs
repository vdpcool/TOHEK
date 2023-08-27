using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE;
[HarmonyPatch(typeof(MainMenuManager))]
//参考TO-HOPE（N让我搬过来））））https://gitee.com/xigua_ya/to-hope
public class Mainbutton
{   
    [HarmonyPatch(nameof(MainMenuManager.Start))]
    [HarmonyPrefix]
    static void LoadButtons(MainMenuManager __instance)
    {
        Buttons.Clear();
        var template = __instance.creditsButton;
    
        if (!template) return;
        // 示例，创建一个名为Gitee的按钮，点击后打开https://gitee.com/xigua_ya/tohex
        CreateButton(__instance, template, GameObject.Find("RightPanel")?.transform, new(0.4f, 0.3f), "Github", () => { Application.OpenURL("https://github.com/vdpcool/TOHEK"); }, new Color32(0, 0, 0, byte.MaxValue));
        CreateButton(__instance, template, GameObject.Find("RightPanel")?.transform, new(0.6f, 0.2f), "Discord TOHEX", () => { Application.OpenURL("https://discord.gg/WKYdzxKhNm"); }, new Color32(0, 8, 255, byte.MaxValue));
        CreateButton(__instance, template, GameObject.Find("RightPanel")?.transform, new(0.4f, 0.2f), "Discord TOHER", () => { Application.OpenURL("https://discord.gg/tohe"); }, new Color32(0, 8, 255, byte.MaxValue));
        CreateButton(__instance, template, GameObject.Find("RightPanel")?.transform, new(0.2f, 0.2f), "Discord TOHY", () => { Application.OpenURL("https://discord.gg/MX3QK8aP6Z"); }, new Color32(0, 8, 255, byte.MaxValue));
        CreateButton(__instance, template, GameObject.Find("RightPanel")?.transform, new(0.2f, 0.3f), "QQ", () => { Application.OpenURL("https://jq.qq.com/?_wv=1027&k=2RpigaN6"); }, new Color32(255, 143, 0, byte.MaxValue));
    }
    
    private static readonly List<PassiveButton> Buttons = new();
    /// <summary>
    /// 在主界面创建一个按钮
    /// </summary>
    /// <param name="__instance">MainMenuManager 的实例</param>
    /// <param name="template">按钮模板</param>
    /// <param name="parent">父游戏物体</param>
    /// <param name="anchorPoint">与父游戏物体的相对位置</param>
    /// <param name="text">按钮文本</param>
    /// <param name="action">点击按钮的动作</param>
    /// <returns>返回这个按钮</returns>
    static void CreateButton(MainMenuManager __instance, PassiveButton template, Transform parent, Vector2 anchorPoint, string text, Action action,Color color)
    {
        if (!parent) return;

        var button = UnityEngine.Object.Instantiate(template, parent);
        button.GetComponent<AspectPosition>().anchorPoint = anchorPoint;
        SpriteRenderer buttonSprite = button.transform.FindChild("Inactive").GetComponent<SpriteRenderer>();
        buttonSprite.color = color;
        __instance.StartCoroutine(Effects.Lerp(0.5f, new Action<float>((p) => {
            button.GetComponentInChildren<TMPro.TMP_Text>().SetText(text);
        })));
        button.OnClick = new();
        button.OnClick.AddListener(action);

        Buttons.Add(button);
    }

    [HarmonyPatch(nameof(MainMenuManager.OpenAccountMenu))]
    [HarmonyPatch(nameof(MainMenuManager.OpenCredits))]
    [HarmonyPatch(nameof(MainMenuManager.OpenGameModeMenu))]
    [HarmonyPostfix]
    static void Hide()
    {
        foreach (var btn in Buttons) btn.gameObject.SetActive(false);
    }
    [HarmonyPatch(nameof(MainMenuManager.ResetScreen))]
    [HarmonyPostfix]
    static void Show()
    {
        foreach (var btn in Buttons)
        {
            if (btn == null || btn.gameObject == null) continue;
            btn.gameObject.SetActive(true);
        }
    }
}