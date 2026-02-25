using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace LipsyncLight
{
    internal static class LipsyncLightBuilder
    {
        private const string VoiceLayerName   = "LipSyncLight_Voice";
        private const string VisemeLayerName  = "LipSyncLight_Viseme";
        private const string VoiceParamName   = "Voice";
        private const string VisemeParamName  = "Viseme";

        // ---------------------------------------------------------------
        // Public entry point
        // ---------------------------------------------------------------

        public static void Build(LipsyncLightConfig config)
        {
            if (config.AvatarRoot == null)
                throw new InvalidOperationException("Avatar Root が設定されていません。");
            if (config.Targets == null || config.Targets.Count == 0)
                throw new InvalidOperationException("ターゲットが1つも設定されていません。");

            var fx = GetFxController(config.AvatarRoot);
            if (fx == null)
                throw new InvalidOperationException(
                    "FX Controller が見つかりません。アバターの FX レイヤーに AnimatorController を設定してください。");

            // 既存レイヤーの重複チェック
            bool hasVoice  = config.Mode != LipsyncMode.Viseme && LayerExists(fx, VoiceLayerName);
            bool hasViseme = config.Mode != LipsyncMode.Voice  && LayerExists(fx, VisemeLayerName);
            if ((hasVoice || hasViseme) &&
                !EditorUtility.DisplayDialog(
                    "LipSync Light",
                    "既存の LipSync Light レイヤーが見つかりました。上書きしますか？",
                    "上書き", "キャンセル"))
                return;

            // 出力フォルダを確保
            EnsureFolder(config.OutputPath);
            EnsureFolder(config.OutputPath + "/Animations");

            if (config.Mode == LipsyncMode.Voice || config.Mode == LipsyncMode.Both)
            {
                var offClip = CreateEmissionClip(
                    config.AvatarRoot, config.Targets,
                    t => t.GetOffColor(config.ColorGroups), "LipSyncLight_Off");
                var onClip = CreateEmissionClip(
                    config.AvatarRoot, config.Targets,
                    t => t.GetOnColor(config.ColorGroups) * config.IntensityMultiplier, "LipSyncLight_On");

                SaveClip(offClip, config.OutputPath + "/Animations/LipSyncLight_Off.anim");
                SaveClip(onClip,  config.OutputPath + "/Animations/LipSyncLight_On.anim");
                BuildVoiceLayer(config, fx, offClip, onClip);
            }

            if (config.Mode == LipsyncMode.Viseme || config.Mode == LipsyncMode.Both)
            {
                var clips = new AnimationClip[15];
                for (int i = 0; i < 15; i++)
                {
                    int idx = i;
                    clips[i] = CreateEmissionClip(
                        config.AvatarRoot, config.Targets,
                        t => t.GetVisemeColor(config.ColorGroups, idx),
                        $"LipSyncLight_Viseme_{i}");
                    SaveClip(clips[i], $"{config.OutputPath}/Animations/LipSyncLight_Viseme_{i}.anim");
                }
                BuildVisemeLayer(config, fx, clips);
            }

            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LipSync Light] セットアップが完了しました。");
        }

        // ---------------------------------------------------------------
        // Layer builders
        // ---------------------------------------------------------------

        private static void BuildVoiceLayer(
            LipsyncLightConfig config,
            AnimatorController fx,
            AnimationClip offClip,
            AnimationClip onClip)
        {
            EnsureParameter(fx, VoiceParamName, AnimatorControllerParameterType.Float);
            RemoveLayerIfExists(fx, VoiceLayerName);

            // AnimatorStateMachine は FX アセットのサブアセットとして登録する必要がある
            var sm = new AnimatorStateMachine();
            sm.name      = VoiceLayerName;
            sm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(sm, fx);

            // BlendTree も同様にサブアセット登録が必要
            var blendTree = new BlendTree();
            blendTree.name           = "LipSync Light Voice";
            blendTree.hideFlags      = HideFlags.HideInHierarchy;
            blendTree.blendParameter = VoiceParamName;
            blendTree.blendType      = BlendTreeType.Simple1D;
            AssetDatabase.AddObjectToAsset(blendTree, fx);
            blendTree.AddChild(offClip, 0f);
            blendTree.AddChild(onClip,  1f);

            var state = sm.AddState("Blend");
            state.motion             = blendTree;
            state.writeDefaultValues = false;
            sm.defaultState          = state;

            var layer = new AnimatorControllerLayer
            {
                name          = VoiceLayerName,
                defaultWeight = 1f,
                blendingMode  = AnimatorLayerBlendingMode.Override,
                stateMachine  = sm,
            };
            fx.AddLayer(layer);
            EditorUtility.SetDirty(sm);
        }

        /// <summary>
        /// Viseme レイヤーのトランジション時間を算出する。
        /// 使用中のグループの中で最大の TransitionDuration を採用する。
        /// </summary>
        private static float ResolveVisemeTransitionDuration(LipsyncLightConfig config)
        {
            float max = 0f;
            foreach (var t in config.Targets)
            {
                int g = t.VisemeColorGroupIndex;
                float d = (g >= 0 && g < config.ColorGroups.Count)
                    ? config.ColorGroups[g].TransitionDuration
                    : t.TransitionDuration;
                if (d > max) max = d;
            }
            return max;
        }

        private static void BuildVisemeLayer(
            LipsyncLightConfig config,
            AnimatorController fx,
            AnimationClip[] visemeClips)
        {
            EnsureParameter(fx, VisemeParamName, AnimatorControllerParameterType.Int);
            RemoveLayerIfExists(fx, VisemeLayerName);

            var sm = new AnimatorStateMachine();
            sm.name      = VisemeLayerName;
            sm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(sm, fx);

            for (int i = 0; i < 15; i++)
            {
                var state = sm.AddState($"Viseme_{i}");
                state.motion             = visemeClips[i];
                state.writeDefaultValues = false;

                float transitionDuration = ResolveVisemeTransitionDuration(config);
                var tr = sm.AddAnyStateTransition(state);
                tr.AddCondition(AnimatorConditionMode.Equals, i, VisemeParamName);
                tr.hasExitTime      = false;
                tr.duration         = transitionDuration;
                tr.hasFixedDuration = true;
            }

            var layer = new AnimatorControllerLayer
            {
                name          = VisemeLayerName,
                defaultWeight = 1f,
                blendingMode  = AnimatorLayerBlendingMode.Override,
                stateMachine  = sm,
            };
            fx.AddLayer(layer);
            EditorUtility.SetDirty(sm);
        }

        // ---------------------------------------------------------------
        // AnimationClip generation
        // ---------------------------------------------------------------

        /// <summary>
        /// 複数ターゲットのエミッションカラーを1枚の AnimationClip にまとめる。
        /// </summary>
        internal static AnimationClip CreateEmissionClip(
            GameObject avatarRoot,
            List<EmissionTarget> targets,
            Func<EmissionTarget, Color> colorSelector,
            string clipName)
        {
            var clip = new AnimationClip { name = clipName };

            foreach (var target in targets)
            {
                if (target?.Renderer == null) continue;

                string relativePath = GetRelativePath(avatarRoot.transform, target.Renderer.transform);
                Type   rendererType = target.Renderer.GetType();
                Color  color        = colorSelector(target);
                string propBase     = BuildPropertyPath(target.MaterialIndex, target.PropertyName);

                var channels = new (string suffix, float value)[]
                {
                    (".r", color.r),
                    (".g", color.g),
                    (".b", color.b),
                    (".a", color.a),
                };

                foreach (var (suffix, value) in channels)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(relativePath, rendererType, propBase + suffix),
                        AnimationCurve.Constant(0f, 0f, value));
                }
            }

            return clip;
        }

        /// <summary>
        /// マテリアルインデックスとプロパティ名からアニメーションカーブのプロパティパスを生成する。
        /// index == 0: "material.{propertyName}"
        /// index >= 1: "materials[N].{propertyName}"
        /// </summary>
        internal static string BuildPropertyPath(int materialIndex, string propertyName)
        {
            return materialIndex == 0
                ? $"material.{propertyName}"
                : $"materials[{materialIndex}].{propertyName}";
        }

        /// <summary>
        /// アバタールート Transform からターゲット Transform への相対パスを返す。
        /// </summary>
        internal static string GetRelativePath(Transform root, Transform target)
        {
            var parts   = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static AnimatorController? GetFxController(GameObject avatarRoot)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return null;

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                    return layer.animatorController as AnimatorController;
            }
            return null;
        }

        private static bool LayerExists(AnimatorController fx, string layerName)
            => fx.layers.Any(l => l.name == layerName);

        private static void RemoveLayerIfExists(AnimatorController fx, string layerName)
        {
            var layers = fx.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                {
                    fx.RemoveLayer(i);
                    return;
                }
            }
        }

        private static void EnsureParameter(
            AnimatorController fx,
            string paramName,
            AnimatorControllerParameterType type)
        {
            if (!fx.parameters.Any(p => p.name == paramName && p.type == type))
                fx.AddParameter(paramName, type);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int    slash  = path.LastIndexOf('/');
            string parent = path[..slash];
            string name   = path[(slash + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SaveClip(AnimationClip clip, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
        }

        /// <summary>
        /// 指定モードで生成されたレイヤーと AnimationClip を削除する。
        /// </summary>
        public static void DeleteGenerated(LipsyncLightConfig config)
        {
            if (config?.AvatarRoot == null) return;

            var fx = GetFxController(config.AvatarRoot);
            if (fx != null)
            {
                RemoveLayerIfExists(fx, VoiceLayerName);
                RemoveLayerIfExists(fx, VisemeLayerName);
                EditorUtility.SetDirty(fx);
            }

            string animDir = config.OutputPath + "/Animations";
            AssetDatabase.DeleteAsset(animDir + "/LipSyncLight_Off.anim");
            AssetDatabase.DeleteAsset(animDir + "/LipSyncLight_On.anim");
            for (int i = 0; i < 15; i++)
                AssetDatabase.DeleteAsset($"{animDir}/LipSyncLight_Viseme_{i}.anim");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LipSync Light] 生成物を削除しました。");
        }
    }
}
