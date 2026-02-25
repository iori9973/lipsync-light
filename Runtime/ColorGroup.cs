using System;
using UnityEngine;

namespace LipsyncLight
{
    [Serializable]
    public class ColorGroup
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
}
