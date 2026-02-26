using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;

namespace LipsyncLight
{
    internal static class LipsyncLightBuilder
    {
        private const string VoiceLayerName       = "LipSyncLight_Voice";
        private const string VisemeLayerName      = "LipSyncLight_Viseme";
        private const string VoiceParamName       = "Voice";
        private const string VisemeParamName      = "Viseme";
        private const string FxControllerFileName = "LipSyncLight_FX.controller";

        // ---------------------------------------------------------------
        // Public entry point
        // ---------------------------------------------------------------

        public static void Build(LipsyncLightSetup setup)
        {
            if (setup == null)
                throw new InvalidOperationException("セットアップコンポーネントが見つかりません。");
            if (setup.Targets == null || setup.Targets.Count == 0)
                throw new InvalidOperationException("ターゲットが1つも設定されていません。");

            var avatarRoot = FindAvatarRoot(setup);
            string outputPath = DeriveOutputPath(avatarRoot);

            // MeshRenderer + materialIndex≥1 の組み合わせは Unity のアニメーション
            // バインディング制限により動作しないため、自動的に SkinnedMeshRenderer へ変換する
            ConvertMeshRenderersIfNeeded(setup);

            EnsureFolder(outputPath);
            EnsureFolder(outputPath + "/Animations");

            var controller = CreateOrUpdateController(setup, avatarRoot, outputPath);

            SetupMaMergeAnimator(setup.gameObject, controller);

            EditorUtility.SetDirty(setup);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LipSync Light] セットアップが完了しました。");
        }

        // ---------------------------------------------------------------
        // Controller generation
        // ---------------------------------------------------------------

        private static AnimatorController CreateOrUpdateController(
            LipsyncLightSetup setup, GameObject avatarRoot, string outputPath)
        {
            string controllerPath = outputPath + "/" + FxControllerFileName;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            RemoveLayerIfExists(controller, VoiceLayerName);
            RemoveLayerIfExists(controller, VisemeLayerName);

            if (setup.Mode == LipsyncMode.Voice || setup.Mode == LipsyncMode.Both)
            {
                var offClip = CreateEmissionClip(
                    avatarRoot, setup.Targets,
                    t => t.GetOffColor(setup.ColorGroups), "LipSyncLight_Off");
                var onClip = CreateEmissionClip(
                    avatarRoot, setup.Targets,
                    t => t.GetOnColor(setup.ColorGroups) * setup.IntensityMultiplier, "LipSyncLight_On");

                SaveClip(offClip, outputPath + "/Animations/LipSyncLight_Off.anim");
                SaveClip(onClip,  outputPath + "/Animations/LipSyncLight_On.anim");
                BuildVoiceLayer(controller, offClip, onClip,
                    setup.VoiceThreshold, setup.VoiceFadeTime, setup.AdditiveBlending);
            }

            if (setup.Mode == LipsyncMode.Viseme || setup.Mode == LipsyncMode.Both)
            {
                var clips = new AnimationClip[15];
                for (int i = 0; i < 15; i++)
                {
                    int idx = i;
                    clips[i] = CreateEmissionClip(
                        avatarRoot, setup.Targets,
                        t => t.GetVisemeColor(setup.ColorGroups, idx),
                        $"LipSyncLight_Viseme_{i}");
                    SaveClip(clips[i], $"{outputPath}/Animations/LipSyncLight_Viseme_{i}.anim");
                }
                BuildVisemeLayer(setup, controller, clips, setup.AdditiveBlending);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void SetupMaMergeAnimator(GameObject setupGo, AnimatorController controller)
        {
            var merge = setupGo.GetComponent<ModularAvatarMergeAnimator>()
                ?? setupGo.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator               = controller;
            merge.layerType              = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.deleteAttachedAnimator = false;
            merge.pathMode               = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = true;
            EditorUtility.SetDirty(merge);
        }

        // ---------------------------------------------------------------
        // Layer builders
        // ---------------------------------------------------------------

        private static void BuildVoiceLayer(
            AnimatorController controller,
            AnimationClip offClip,
            AnimationClip onClip,
            float threshold,
            float fadeTime,
            bool additive)
        {
            EnsureParameter(controller, VoiceParamName, AnimatorControllerParameterType.Float);

            var sm = new AnimatorStateMachine();
            sm.name      = VoiceLayerName;
            sm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(sm, controller);

            // Off ステート（消灯）: デフォルト
            var offState = sm.AddState("Off");
            offState.motion             = offClip;
            offState.writeDefaultValues = false;
            sm.defaultState             = offState;

            // On ステート（BlendTree: Voice の強さに応じて発光強度をブレンド）
            var blendTree = new BlendTree();
            blendTree.name           = "LipSync Light Voice";
            blendTree.hideFlags      = HideFlags.HideInHierarchy;
            blendTree.blendParameter = VoiceParamName;
            blendTree.blendType      = BlendTreeType.Simple1D;
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(offClip, 0f);
            blendTree.AddChild(onClip,  1f);

            var onState = sm.AddState("On");
            onState.motion             = blendTree;
            onState.writeDefaultValues = false;

            // Off → On: Voice > threshold でフェードイン
            var toOn = offState.AddTransition(onState);
            toOn.AddCondition(AnimatorConditionMode.Greater, threshold, VoiceParamName);
            toOn.hasExitTime      = false;
            toOn.duration         = fadeTime;
            toOn.hasFixedDuration = true;

            // On → Off: Voice < threshold でフェードアウト
            var toOff = onState.AddTransition(offState);
            toOff.AddCondition(AnimatorConditionMode.Less, threshold, VoiceParamName);
            toOff.hasExitTime      = false;
            toOff.duration         = fadeTime;
            toOff.hasFixedDuration = true;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name          = VoiceLayerName,
                defaultWeight = 1f,
                blendingMode  = additive
                    ? AnimatorLayerBlendingMode.Additive
                    : AnimatorLayerBlendingMode.Override,
                stateMachine  = sm,
            });
            EditorUtility.SetDirty(sm);
        }

        private static void BuildVisemeLayer(
            LipsyncLightSetup setup,
            AnimatorController controller,
            AnimationClip[] visemeClips,
            bool additive)
        {
            EnsureParameter(controller, VisemeParamName, AnimatorControllerParameterType.Int);

            var sm = new AnimatorStateMachine();
            sm.name      = VisemeLayerName;
            sm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(sm, controller);

            float transitionDuration = ResolveVisemeTransitionDuration(setup);

            for (int i = 0; i < 15; i++)
            {
                var state = sm.AddState($"Viseme_{i}");
                state.motion             = visemeClips[i];
                state.writeDefaultValues = false;

                var tr = sm.AddAnyStateTransition(state);
                tr.AddCondition(AnimatorConditionMode.Equals, i, VisemeParamName);
                tr.hasExitTime      = false;
                tr.duration         = transitionDuration;
                tr.hasFixedDuration = true;
            }

            controller.AddLayer(new AnimatorControllerLayer
            {
                name          = VisemeLayerName,
                defaultWeight = 1f,
                blendingMode  = additive
                    ? AnimatorLayerBlendingMode.Additive
                    : AnimatorLayerBlendingMode.Override,
                stateMachine  = sm,
            });
            EditorUtility.SetDirty(sm);
        }

        private static float ResolveVisemeTransitionDuration(LipsyncLightSetup setup)
        {
            float max = 0f;
            foreach (var t in setup.Targets)
            {
                int g = t.VisemeColorGroupIndex;
                float d = (g >= 0 && g < setup.ColorGroups.Count)
                    ? setup.ColorGroups[g].TransitionDuration
                    : t.TransitionDuration;
                if (d > max) max = d;
            }
            return max;
        }

        // ---------------------------------------------------------------
        // AnimationClip generation
        // ---------------------------------------------------------------

        /// <summary>
        /// 複数ターゲット・複数プロパティのエミッションカラーを1枚の AnimationClip にまとめる。
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
                if (target.PropertyNames == null || target.PropertyNames.Count == 0) continue;

                string relativePath = GetRelativePath(avatarRoot.transform, target.Renderer.transform);
                Type   rendererType = target.Renderer.GetType();
                Color  color        = colorSelector(target);

                // MeshRenderer で materials[N].PropertyName バインディングを手動構築すると
                // customType: 0 になりランタイムでアニメーションが適用されない問題がある。
                // GetAnimatableBindings() が返すバインディングは Unity 内部で customType が
                // 正しく設定されているため、それを優先して使用する。
                var bindingCache = BuildBindingCache(target.Renderer.gameObject, avatarRoot, relativePath);

                foreach (var propName in target.PropertyNames)
                {
                    if (string.IsNullOrEmpty(propName)) continue;

                    string propBase = BuildPropertyPath(target.MaterialIndex, propName);
                    var channels = new (string suffix, float value)[]
                    {
                        (".r", color.r),
                        (".g", color.g),
                        (".b", color.b),
                        (".a", color.a),
                    };

                    foreach (var (suffix, value) in channels)
                    {
                        string fullPropName = propBase + suffix;
                        if (!bindingCache.TryGetValue(fullPropName, out var binding))
                            binding = EditorCurveBinding.FloatCurve(relativePath, rendererType, fullPropName);

                        AnimationUtility.SetEditorCurve(
                            clip, binding,
                            AnimationCurve.Constant(0f, 0f, value));
                    }
                }
            }

            return clip;
        }

        /// <summary>
        /// AnimationUtility.GetAnimatableBindings() から propertyName → EditorCurveBinding の辞書を構築する。
        /// シーン内オブジェクトでない場合（テスト等）は空辞書を返す。
        /// </summary>
        private static Dictionary<string, EditorCurveBinding> BuildBindingCache(
            GameObject rendererGo, GameObject avatarRoot, string relativePath)
        {
            if (!rendererGo.scene.IsValid())
                return new Dictionary<string, EditorCurveBinding>();

            return AnimationUtility.GetAnimatableBindings(rendererGo, avatarRoot)
                .Where(b => b.path == relativePath && !b.isPPtrCurve && !b.isDiscreteCurve)
                .GroupBy(b => b.propertyName)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// マテリアルインデックスとプロパティ名からアニメーションカーブのプロパティパスを生成する。
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
        // Delete
        // ---------------------------------------------------------------

        public static void DeleteGenerated(LipsyncLightSetup setup)
        {
            if (setup == null) return;

            var merge = setup.GetComponent<ModularAvatarMergeAnimator>();
            if (merge != null)
                UnityEngine.Object.DestroyImmediate(merge);

            string outputPath;
            try   { outputPath = DeriveOutputPath(FindAvatarRoot(setup)); }
            catch { outputPath = "Assets/LipSyncLight"; }

            string controllerPath = outputPath + "/" + FxControllerFileName;
            AssetDatabase.DeleteAsset(controllerPath);

            string animDir = outputPath + "/Animations";
            AssetDatabase.DeleteAsset(animDir + "/LipSyncLight_Off.anim");
            AssetDatabase.DeleteAsset(animDir + "/LipSyncLight_On.anim");
            for (int i = 0; i < 15; i++)
                AssetDatabase.DeleteAsset($"{animDir}/LipSyncLight_Viseme_{i}.anim");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LipSync Light] 生成物を削除しました。");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// アバタールートの名前から出力先パスを自動決定する。
        /// 複数アバターで使っても干渉しないようにアバター名をサブフォルダにする。
        /// </summary>
        private static string DeriveOutputPath(GameObject avatarRoot)
            => $"Assets/LipSyncLight/{avatarRoot.name}";

        private static GameObject FindAvatarRoot(LipsyncLightSetup setup)
        {
            var descriptor = setup.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null)
                throw new InvalidOperationException(
                    "VRCAvatarDescriptor が見つかりません。" +
                    "アバターのルート GameObject に VRCAvatarDescriptor がついているか確認してください。");
            return descriptor.gameObject;
        }

        private static bool LayerExists(AnimatorController controller, string layerName)
            => controller.layers.Any(l => l.name == layerName);

        private static void RemoveLayerIfExists(AnimatorController controller, string layerName)
        {
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                {
                    controller.RemoveLayer(i);
                    return;
                }
            }
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string paramName,
            AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == paramName && p.type == type))
                controller.AddParameter(paramName, type);
        }

        /// <summary>
        /// MeshRenderer + materialIndex≥1 の組み合わせは Unity の animation binding 制限で
        /// customType:22 が付かず、マテリアルプロパティのアニメーションが適用されない。
        /// SkinnedMeshRenderer は同制限がないため、該当ターゲットを自動変換する。
        /// 静的メッシュでは見た目・動作は MeshRenderer と完全に同一。
        /// </summary>
        private static void ConvertMeshRenderersIfNeeded(LipsyncLightSetup setup)
        {
            foreach (var target in setup.Targets)
            {
                if (target?.Renderer == null) continue;
                if (target.MaterialIndex == 0) continue;               // index 0 は MeshRenderer でも動作する
                if (target.Renderer is SkinnedMeshRenderer) continue;  // すでに SMR なら不要
                if (target.Renderer is not MeshRenderer mr) continue;

                var go = mr.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null) continue;

                var mesh      = mf.sharedMesh;
                var materials = mr.sharedMaterials;

                Undo.DestroyObjectImmediate(mr);
                Undo.DestroyObjectImmediate(mf);

                var smr = Undo.AddComponent<SkinnedMeshRenderer>(go);
                smr.sharedMesh      = mesh;
                smr.sharedMaterials = materials;

                target.Renderer = smr;
                Debug.Log($"[LipSync Light] {go.name}: MeshRenderer → SkinnedMeshRenderer に自動変換しました（material index {target.MaterialIndex} のアニメーションに必要）");
            }
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
            FixMeshRendererCustomType(clip);
            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// MeshRenderer (typeID:23) の材質プロパティバインディングが
        /// customType:0（汎用）になる Unity の内部制限を回避するため、
        /// SerializedObject 経由で customType を 22（MaterialProperty）に直接書き換える。
        /// customType:22 にすると Unity ランタイムが MaterialPropertyBlock 経由で
        /// プロパティを適用し、materials[N].PropertyName 形式が正しく機能する。
        /// </summary>
        private static void FixMeshRendererCustomType(AnimationClip clip)
        {
            var so = new SerializedObject(clip);
            var genericBindings = so.FindProperty("m_ClipBindingConstant.genericBindings");
            if (genericBindings == null || !genericBindings.isArray) return;

            bool modified = false;
            for (int i = 0; i < genericBindings.arraySize; i++)
            {
                var element    = genericBindings.GetArrayElementAtIndex(i);
                if (element == null) continue;
                var typeIDProp = element.FindPropertyRelative("typeID");
                var ctProp     = element.FindPropertyRelative("customType");
                if (typeIDProp == null || ctProp == null) continue;

                // typeID 23 = MeshRenderer
                // customType 0 = 汎用（失敗）→ 22 = MaterialProperty（MaterialPropertyBlock 経由）に修正
                if (typeIDProp.intValue == 23 && ctProp.intValue == 0)
                {
                    ctProp.intValue = 22;
                    modified = true;
                }
            }

            if (modified)
                so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
