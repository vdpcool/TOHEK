using HarmonyLib;
using UnityEngine;

namespace TOHE;

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
public class LobbyFixedUpdatePatch
{
    private static GameObject Paint;
    public static void Postfix()
    {
        if (Paint == null)
        {
            var LeftBox = GameObject.Find("Leftbox");
            if (LeftBox != null)
            {
                Paint = Object.Instantiate(LeftBox, LeftBox.transform.parent.transform);
                Paint.name = "TOHE Lobby Paint";
                Paint.transform.localPosition = new Vector3(0.042f, 1.39f, -10.5f);
                SpriteRenderer renderer = Paint.GetComponent<SpriteRenderer>();
                renderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.TOHELobbyPaint.png", 290f);
            }
        }
    }
}
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
public class LobbyFixedUpdatePatch1
{
    private static GameObject Paint;
    public static void Postfix()
    {
        if (Paint == null)
        {
            var LeftBox = GameObject.Find("Leftbox");
            if (LeftBox != null)
            {
                Paint = Object.Instantiate(LeftBox, LeftBox.transform.parent.transform);
                Paint.name = "KARPED1EM Lobby Paint";
                Paint.transform.localPosition = new Vector3(0.042f, -2.59f, -10.5f);
                SpriteRenderer renderer = Paint.GetComponent<SpriteRenderer>();
                renderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.KARPED1EMLobbyPaint.png", 290f);
            }
        }
    }
}