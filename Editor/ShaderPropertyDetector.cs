#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace LipsyncLight
{
    /// <summary>
    /// シェーダーから発光関連プロパティを自動検出するユーティリティ。
    /// </summary>
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
