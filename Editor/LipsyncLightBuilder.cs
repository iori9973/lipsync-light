#nullable enable
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

            // 重複セットアップの検出（同一アバター内に複数ある場合は二重マージになるためエラー）
            var allSetups = avatarRoot.GetComponentsInChildren<LipsyncLightSetup>();
            if (allSetups.Length > 1)
            {
                var others = string.Join("、", allSetups.Where(s => s != setup).Select(s => s.gameObject.name));
                throw new InvalidOperationException(
                    $"同一アバター内に LipsyncLightSetup が複数存在します（{others}）。\n" +
                    "重複するとアニメーターが二重マージされ、正常に動作しません。\n" +
                    "不要なセットアップを削除してから再実行してください。");
            }

            string outputPath = DeriveOutputPath(avatarRoot);

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

            // materialIndex ≥ 1 のターゲット用マテリアルバリアントを事前生成する
            // （float curve では customType:0 になりランタイムで適用されないため PPtrCurve で対処）
            var variantMap = BuildVariantMap(setup, outputPath);

            if (setup.Mode == LipsyncMode.Voice || setup.Mode == LipsyncMode.Both)
            {
                var offClip = CreateEmissionClip(
                    avatarRoot, setup.Targets,
                    t => t.GetOffColor(setup.ColorGroups), "LipSyncLight_Off", variantMap);
                var onClip = CreateEmissionClip(
                    avatarRoot, setup.Targets,
                    t => t.GetOnColor(setup.ColorGroups) * setup.IntensityMultiplier, "LipSyncLight_On", variantMap);

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
                        $"LipSyncLight_Viseme_{i}", variantMap);
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
        /// materialIndex ≥ 1 のターゲットは PPtrCurve（マテリアルスワップ）方式を使用する。
        /// variantMap が null またはエントリがない場合はフォールバックとして float curve を使用する。
        /// </summary>
        internal static AnimationClip CreateEmissionClip(
            GameObject avatarRoot,
            List<EmissionTarget> targets,
            Func<EmissionTarget, Color> colorSelector,
            string clipName,
            Dictionary<(EmissionTarget, string), Material>? variantMap = null)
        {
            var clip = new AnimationClip { name = clipName };

            foreach (var target in targets)
            {
                if (target?.Renderer == null) continue;
                if (target.PropertyNames == null || target.PropertyNames.Count == 0) continue;

                string relativePath = GetRelativePath(avatarRoot.transform, target.Renderer.transform);
                Type   rendererType = target.Renderer.GetType();

                // materialIndex ≥ 1 かつバリアントマテリアルが用意されている場合は
                // PPtrCurve（m_Materials.Array.data[N]）でマテリアルごとスワップする。
                // Unity の animation binding 制限により materials[N].PropertyName は
                // customType:0 になりランタイムで適用されないためこの方式を採用する。
                if (target.MaterialIndex >= 1
                    && variantMap != null
                    && variantMap.TryGetValue((target, clipName), out var variantMat))
                {
                    AddMaterialSwapCurve(clip, relativePath, rendererType, target.MaterialIndex, variantMat);
                    continue;
                }

                // materialIndex == 0 またはバリアントなしの場合は float curve を使用
                Color  color        = colorSelector(target);
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

                    // 非黒色 + materialIndex==0 + 有効化フラグが存在するとき、フラグも有効化する
                    // （lilToon の _UseEmission 等が 0 だとエミッションが表示されないため）
                    if (target.MaterialIndex == 0 && (color.r > 0f || color.g > 0f || color.b > 0f))
                    {
                        var mats   = target.Renderer.sharedMaterials;
                        var srcMat = mats.Length > 0 ? mats[0] : null;
                        if (srcMat != null)
                        {
                            var enablePropName = FindEnablePropertyName(propName, srcMat);
                            if (enablePropName != null)
                            {
                                string enablePath = $"material.{enablePropName}";
                                if (!bindingCache.TryGetValue(enablePath, out var enableBinding))
                                    enableBinding = EditorCurveBinding.FloatCurve(relativePath, rendererType, enablePath);
                                AnimationUtility.SetEditorCurve(
                                    clip, enableBinding, AnimationCurve.Constant(0f, 0f, 1f));
                            }
                        }
                    }
                }
            }

            return clip;
        }

        /// <summary>
        /// materialIndex ≥ 1 のターゲット全てについて、各クリップ用のマテリアルバリアントを生成し
        /// (target, clipName) をキーとする辞書を返す。
        /// バリアントは元マテリアルのコピーに PropertyNames の色を上書きしたもの。
        /// 元の発光アニメーション（_EmissionBlink 等）はすべてのプロパティがコピーされるため保持される。
        /// </summary>
        private static Dictionary<(EmissionTarget, string), Material> BuildVariantMap(
            LipsyncLightSetup setup, string outputPath)
        {
            var map = new Dictionary<(EmissionTarget, string), Material>();

            bool needsVoice  = setup.Mode == LipsyncMode.Voice  || setup.Mode == LipsyncMode.Both;
            bool needsViseme = setup.Mode == LipsyncMode.Viseme || setup.Mode == LipsyncMode.Both;

            var targets = setup.Targets
                .Where(t => t?.Renderer != null && t.MaterialIndex >= 1
                         && t.PropertyNames != null && t.PropertyNames.Count > 0)
                .ToList();

            if (targets.Count == 0) return map;

            string materialsPath = outputPath + "/Materials";
            EnsureFolder(materialsPath);

            foreach (var target in targets)
            {
                var mats = target.Renderer.sharedMaterials;
                if (target.MaterialIndex >= mats.Length || mats[target.MaterialIndex] == null)
                {
                    Debug.LogWarning(
                        $"[LipSync Light] {target.Renderer.gameObject.name}: " +
                        $"materialIndex {target.MaterialIndex} が sharedMaterials の範囲外です。スキップします。");
                    continue;
                }

                var originalMat = mats[target.MaterialIndex];
                string baseName  = $"{SanitizeName(target.Renderer.gameObject.name)}_mat{target.MaterialIndex}";

                if (needsVoice)
                {
                    var offColor = target.GetOffColor(setup.ColorGroups);
                    var onColor  = target.GetOnColor(setup.ColorGroups) * setup.IntensityMultiplier;

                    map[(target, "LipSyncLight_Off")] = CreateAndSaveMaterialVariant(
                        originalMat, target.PropertyNames, offColor,
                        $"{materialsPath}/{baseName}_Off.mat");
                    map[(target, "LipSyncLight_On")] = CreateAndSaveMaterialVariant(
                        originalMat, target.PropertyNames, onColor,
                        $"{materialsPath}/{baseName}_On.mat");
                }

                if (needsViseme)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        var color = target.GetVisemeColor(setup.ColorGroups, i);
                        map[(target, $"LipSyncLight_Viseme_{i}")] = CreateAndSaveMaterialVariant(
                            originalMat, target.PropertyNames, color,
                            $"{materialsPath}/{baseName}_Viseme_{i}.mat");
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// 元マテリアルをコピーし、指定プロパティに色をセットしてアセットとして保存する。
        /// 非黒色の場合、対応する有効化フラグ（_UseEmission 等）を自動的に有効化する。
        /// </summary>
        private static Material CreateAndSaveMaterialVariant(
            Material original,
            List<string> propertyNames,
            Color color,
            string path)
        {
            AssetDatabase.DeleteAsset(path);
            var mat = new Material(original);
            bool isNonZero = color.r > 0f || color.g > 0f || color.b > 0f;
            foreach (var prop in propertyNames)
            {
                mat.SetColor(prop, color);
                // 非黒色の場合、対応する有効化フラグ（_UseEmission 等）を有効化する
                if (isNonZero)
                {
                    var enableProp = FindEnablePropertyName(prop, original);
                    if (enableProp != null)
                        mat.SetFloat(enableProp, 1f);
                }
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// PPtrCurve（m_Materials.Array.data[N]）バインディングを AnimationClip に追加する。
        /// これにより materialIndex ≥ 1 のスロットへのマテリアルスワップが
        /// MeshRenderer・SkinnedMeshRenderer を問わずランタイムで正しく適用される。
        /// </summary>
        private static void AddMaterialSwapCurve(
            AnimationClip clip,
            string path,
            Type rendererType,
            int materialIndex,
            Material material)
        {
            var binding = EditorCurveBinding.PPtrCurve(
                path,
                rendererType,
                $"m_Materials.Array.data[{materialIndex}]");
            var keyframes = new ObjectReferenceKeyframe[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = material },
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
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
        /// カラープロパティ名（例: "_EmissionColor"）に対応する有効化フラグ名
        /// （例: "_UseEmission"）を返す。シェーダーがそのプロパティを持たない場合は null。
        /// パターン: "_FooColor" → "_UseFoo"（lilToon の _UseEmission 等に対応）
        /// </summary>
        internal static string? FindEnablePropertyName(string colorPropName, Material mat)
        {
            if (colorPropName.StartsWith("_") && colorPropName.EndsWith("Color"))
            {
                var stem = colorPropName[1..^5]; // "Emission", "Emission2nd" など
                if (stem.Length > 0)
                {
                    var candidate = "_Use" + stem;
                    if (mat.HasProperty(candidate))
                        return candidate;
                }
            }
            return null;
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

            // PPtrCurve 用マテリアルバリアントを削除
            AssetDatabase.DeleteAsset(outputPath + "/Materials");

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
            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// ファイル名として使用できない文字をアンダースコアに置換する。
        /// </summary>
        private static string SanitizeName(string name)
            => string.Concat(name.Select(c =>
                (char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_'));
    }
}
