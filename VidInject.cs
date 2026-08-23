using GameNetcodeStuff;
using HarmonyLib;
using System;
using UnityEngine.Video;

namespace TheEar
{
    [HarmonyPatch(typeof(Terminal))]
    internal class TerminalVideoInjectorPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void InjectVideo(Terminal __instance)
        {
            if (EarContentHandler.Instance.earAssets.TheEar == null) return;
            EarTerminalAssets container = EarContentHandler.Instance.earAssets.TheEar.GetComponent<EarTerminalAssets>();
            if (container == null || container.bestiaryVideo == null) return;
            foreach (TerminalNode node in __instance.enemyFiles)
            {
                if (node != null && node.name == "The EarBestiaryNode")
                {
                    node.displayVideo = container.bestiaryVideo;
                    return;
                }
            }

            Plugin.Logger.LogWarning("Could not find a TerminalNode named 'TheEarBestiaryNode' in the Bestiary.");
        }
    }
}
