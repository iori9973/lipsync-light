using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LipsyncLight
{
    internal enum LipsyncMode
    {
        Voice,
        Viseme,
        Both,
    }

    [Serializable]
    internal class ColorGroup
    {
        public string Name = "";
        public Color OffColor = Color.black;
        public Color OnColor = Color.white;
        public Color[] VisemeColors = new Color[15];
        public float TransitionDuration = 0f;

        public ColorGroup()
        {
            for (int i = 0; i < VisemeColors.Length; i++)
                VisemeColors[i] = Color.black;
        }
    }

    [CreateAssetMenu(fileName = "LipsyncLightConfig", menuName = "LipSync Light/Config")]
    internal class LipsyncLightConfig : ScriptableObject
    {
        public GameObject AvatarRoot = null!;
        public List<ColorGroup> ColorGroups = new List<ColorGroup>();
        public List<EmissionTarget> Targets = new List<EmissionTarget>();
        public LipsyncMode Mode = LipsyncMode.Voice;
        public float IntensityMultiplier = 1.0f;
        public string OutputPath = "Assets/LipSyncLight";
    }
}
