// VRC SDK3 API の最小限スタブ。
// テストプロジェクトは Unity/VRC SDK なしで動作するため、コンパイルに必要な型を定義する。
using System;
using UnityEngine;
using UnityEditor.Animations;

namespace VRC.SDK3.Avatars.Components
{
    public class VRCAvatarDescriptor : Behaviour
    {
        public enum AnimLayerType
        {
            Base = 0, Additive = 1, Gesture = 2, Action = 3, FX = 4,
            Sitting = 5, TPose = 6, IKPose = 7,
        }

        public struct CustomAnimLayer
        {
            public AnimLayerType type;
            public RuntimeAnimatorController? animatorController;
            public bool isDefault;
            public bool isEnabled;
        }

        public CustomAnimLayer[] baseAnimationLayers    = Array.Empty<CustomAnimLayer>();
        public CustomAnimLayer[] specialAnimationLayers = Array.Empty<CustomAnimLayer>();
    }
}
