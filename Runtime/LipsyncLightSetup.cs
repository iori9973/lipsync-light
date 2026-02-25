using System.Collections.Generic;
using UnityEngine;

namespace LipsyncLight
{
    /// <summary>
    /// アバターの Hierarchy 配下に配置されるセットアップコンポーネント。
    /// LipSync Light の設定を保持し、Modular Avatar 経由で FX に適用する。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LipSync Light/LipSync Light Setup")]
    public class LipsyncLightSetup : MonoBehaviour
    {
        public LipsyncMode Mode = LipsyncMode.Voice;

        // true = Additive（既存の発光に加算）/ false = Override（上書き）
        public bool AdditiveBlending = false;

        public float IntensityMultiplier = 1.0f;

        // Voice モード: 点灯する Voice の閾値（0〜1）
        public float VoiceThreshold = 0.1f;
        // Voice モード: 点灯・消灯のフェード時間（秒）
        public float VoiceFadeTime = 0.05f;

        public List<ColorGroup> ColorGroups = new List<ColorGroup>();
        public List<EmissionTarget> Targets = new List<EmissionTarget>();
        public string OutputPath = "Assets/LipSyncLight";
    }
}
