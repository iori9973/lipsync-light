using System;
using System.Collections.Generic;
using UnityEngine;

namespace LipsyncLight
{
    [Serializable]
    internal class EmissionTarget
    {
        public Renderer Renderer = null!;
        public int MaterialIndex;
        public string PropertyName = "_EmissionColor";

        // 独自設定（ColorGroupIndex < 0 の場合に使用）
        public Color OffColor = Color.black;
        public Color OnColor = Color.white;
        public Color[] VisemeColors = new Color[15];

        // カラーグループ参照（-1 = 独自設定、>= 0 = グループインデックス）
        public int VoiceColorGroupIndex  = -1;
        public int VisemeColorGroupIndex = -1;

        // 独自設定時の切り替え時間（VisemeColorGroupIndex < 0 の場合に使用）
        public float TransitionDuration = 0f;

        public EmissionTarget()
        {
            for (int i = 0; i < VisemeColors.Length; i++)
                VisemeColors[i] = Color.black;
        }

        /// <summary>
        /// グループ設定を考慮した消灯カラーを返す。
        /// </summary>
        public Color GetOffColor(List<ColorGroup> groups)
        {
            if (VoiceColorGroupIndex >= 0 && VoiceColorGroupIndex < groups.Count)
                return groups[VoiceColorGroupIndex].OffColor;
            return OffColor;
        }

        /// <summary>
        /// グループ設定を考慮した点灯カラーを返す。
        /// </summary>
        public Color GetOnColor(List<ColorGroup> groups)
        {
            if (VoiceColorGroupIndex >= 0 && VoiceColorGroupIndex < groups.Count)
                return groups[VoiceColorGroupIndex].OnColor;
            return OnColor;
        }

        /// <summary>
        /// グループ設定を考慮した Viseme カラーを返す。
        /// </summary>
        public Color GetVisemeColor(List<ColorGroup> groups, int visemeIndex)
        {
            if (VisemeColorGroupIndex >= 0 && VisemeColorGroupIndex < groups.Count)
                return groups[VisemeColorGroupIndex].VisemeColors[visemeIndex];
            return VisemeColors[visemeIndex];
        }
    }

    internal static class ShaderPropertyDetector
    {
        private static readonly string[] s_knownEmissionProperties =
        {
            "_EmissionColor",       // Standard, URP Lit, lilToon, Poiyomi
            "_EmissionColor2",      // lilToon 2nd emission
            "_2nd_EmissionColor",   // Poiyomi alternate
        };

        public static string? DetectEmissionProperty(Renderer renderer, int materialIndex)
        {
            if (renderer == null) return null;
            var mats = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= mats.Length) return null;
            var mat = mats[materialIndex];
            if (mat == null) return null;
            return DetectFromMaterial(mat);
        }

        internal static string? DetectFromMaterial(Material mat)
        {
            if (mat == null) return null;
            foreach (var prop in s_knownEmissionProperties)
                if (mat.HasProperty(prop))
                    return prop;
            return null;
        }

        internal static string? DetectFromPropertyNames(IEnumerable<string> propertyNames)
        {
            var nameSet = new HashSet<string>(propertyNames);
            foreach (var prop in s_knownEmissionProperties)
                if (nameSet.Contains(prop))
                    return prop;
            return null;
        }
    }
}
