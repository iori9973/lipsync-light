using System;
using System.Collections.Generic;
using UnityEngine;

namespace LipsyncLight
{
    [Serializable]
    public class EmissionTarget
    {
        public Renderer Renderer = null!;
        public int MaterialIndex;

        // 発光させるプロパティ名のリスト（複数選択可能）
        public List<string> PropertyNames = new List<string> { "_EmissionColor" };

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

        public Color GetOffColor(List<ColorGroup> groups)
        {
            if (VoiceColorGroupIndex >= 0 && VoiceColorGroupIndex < groups.Count)
                return groups[VoiceColorGroupIndex].OffColor;
            return OffColor;
        }

        public Color GetOnColor(List<ColorGroup> groups)
        {
            if (VoiceColorGroupIndex >= 0 && VoiceColorGroupIndex < groups.Count)
                return groups[VoiceColorGroupIndex].OnColor;
            return OnColor;
        }

        public Color GetVisemeColor(List<ColorGroup> groups, int visemeIndex)
        {
            if (VisemeColorGroupIndex >= 0 && VisemeColorGroupIndex < groups.Count)
                return groups[VisemeColorGroupIndex].VisemeColors[visemeIndex];
            return VisemeColors[visemeIndex];
        }
    }
}
