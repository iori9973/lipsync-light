using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LipsyncLight
{
    internal class LipsyncLightWindow : EditorWindow
    {
        private const string ConfigAssetPath = "Assets/LipSyncLight/LipsyncLightConfig.asset";

        [SerializeField] private LipsyncLightConfig _config;
        [SerializeField] private List<bool> _visemeFoldouts = new List<bool>();
        [SerializeField] private List<bool> _groupVisemeFoldouts = new List<bool>();
        private Vector2 _scrollPos;

        // コピー&ペースト用クリップボード（セッション中のみ保持）
        private static (Color off, Color on)? s_voiceColorClipboard;
        private static Color[]?              s_visemeColorClipboard;

        private static readonly string[] VisemeNames =
        {
            "0: sil（無音）",
            "1: PP（p, b, m）",
            "2: FF（f, v）",
            "3: TH（th）",
            "4: DD（d, t, n）",
            "5: kk（k, g）",
            "6: CH（ch, j, sh）",
            "7: SS（s, z）",
            "8: nn（n, ng）",
            "9: RR（r）",
            "10: aa（a）",
            "11: E（e）",
            "12: ih（i）",
            "13: oh（o）",
            "14: ou（u）",
        };

        [MenuItem("Tools/LipSync Light")]
        public static void Open()
        {
            var w = GetWindow<LipsyncLightWindow>("LipSync Light");
            w.minSize = new Vector2(420, 540);
        }

        private void OnEnable()
        {
            _config = AssetDatabase.LoadAssetAtPath<LipsyncLightConfig>(ConfigAssetPath);
        }

        private void OnGUI()
        {
            EnsureConfig();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            DrawHeader("LipSync Light Setup");
            EditorGUILayout.Space(4);

            // Avatar Root
            _config.AvatarRoot = (GameObject)EditorGUILayout.ObjectField(
                "Avatar Root", _config.AvatarRoot, typeof(GameObject), true);
            EditorGUILayout.Space(8);

            // Mode
            DrawSectionHeader("モード");
            _config.Mode = (LipsyncMode)GUILayout.SelectionGrid(
                (int)_config.Mode,
                new[] { "Voice 連動", "Viseme カラー", "両方" },
                3);
            EditorGUILayout.Space(8);

            // Voice settings
            if (_config.Mode == LipsyncMode.Voice || _config.Mode == LipsyncMode.Both)
            {
                DrawSectionHeader("Voice 設定");
                _config.IntensityMultiplier = EditorGUILayout.Slider(
                    "強度倍率", _config.IntensityMultiplier, 0f, 2f);
                EditorGUILayout.Space(8);
            }

            // Color Groups
            DrawSectionHeader("カラーグループ");
            DrawColorGroupList();
            if (GUILayout.Button("+ グループを追加"))
                AddColorGroup();
            EditorGUILayout.Space(8);

            // Targets
            DrawSectionHeader("ターゲット");
            DrawTargetList();
            if (GUILayout.Button("+ ターゲットを追加"))
                AddTarget();
            EditorGUILayout.Space(8);

            // Output path
            DrawSectionHeader("出力先");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_config.OutputPath, EditorStyles.textField, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("変更", GUILayout.Width(50)))
                ChooseOutputPath();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);

            // Validation
            string validationError = Validate();
            if (!string.IsNullOrEmpty(validationError))
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(validationError)))
            {
                if (GUILayout.Button("セットアップ実行", GUILayout.Height(32)))
                    RunSetup();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("生成物を削除", GUILayout.Height(24)))
                RunDelete();

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SaveConfig();
        }

        // ---------------------------------------------------------------
        // Color Group list
        // ---------------------------------------------------------------

        private void DrawColorGroupList()
        {
            if (_config.ColorGroups == null) _config.ColorGroups = new System.Collections.Generic.List<ColorGroup>();

            for (int i = 0; i < _config.ColorGroups.Count; i++)
            {
                var group = _config.ColorGroups[i];

                // VisemeColors が未初期化の場合（古いデータ対応）
                if (group.VisemeColors == null || group.VisemeColors.Length != 15)
                {
                    group.VisemeColors = new Color[15];
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                group.Name = EditorGUILayout.TextField(group.Name, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("削除", GUILayout.Width(44)))
                {
                    // このグループを参照しているターゲットのインデックスをリセット
                    foreach (var t in _config.Targets)
                    {
                        if (t.VoiceColorGroupIndex == i)  t.VoiceColorGroupIndex  = -1;
                        if (t.VisemeColorGroupIndex == i) t.VisemeColorGroupIndex = -1;
                        // 削除されたグループより大きいインデックスを1つずらす
                        if (t.VoiceColorGroupIndex  > i) t.VoiceColorGroupIndex--;
                        if (t.VisemeColorGroupIndex > i) t.VisemeColorGroupIndex--;
                    }
                    _config.ColorGroups.RemoveAt(i);
                    SaveConfig();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                // Voice カラー
                if (_config.Mode == LipsyncMode.Voice || _config.Mode == LipsyncMode.Both)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("コピー", GUILayout.Width(52)))
                        s_voiceColorClipboard = (group.OffColor, group.OnColor);
                    using (new EditorGUI.DisabledScope(s_voiceColorClipboard == null))
                    {
                        if (GUILayout.Button("貼り付け", GUILayout.Width(62)) && s_voiceColorClipboard.HasValue)
                        {
                            group.OffColor = s_voiceColorClipboard.Value.off;
                            group.OnColor  = s_voiceColorClipboard.Value.on;
                            SaveConfig();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    group.OffColor = EditorGUILayout.ColorField("消灯", group.OffColor);
                    group.OnColor  = EditorGUILayout.ColorField("点灯", group.OnColor);
                    EditorGUILayout.EndHorizontal();
                }

                // Viseme カラー
                if (_config.Mode == LipsyncMode.Viseme || _config.Mode == LipsyncMode.Both)
                {
                    group.TransitionDuration = DurationField("切り替え時間 (秒)", group.TransitionDuration);

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("コピー", GUILayout.Width(52)))
                    {
                        s_visemeColorClipboard = new Color[15];
                        group.VisemeColors.CopyTo(s_visemeColorClipboard, 0);
                    }
                    using (new EditorGUI.DisabledScope(s_visemeColorClipboard == null))
                    {
                        if (GUILayout.Button("貼り付け", GUILayout.Width(62)) && s_visemeColorClipboard != null)
                        {
                            s_visemeColorClipboard.CopyTo(group.VisemeColors, 0);
                            SaveConfig();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    while (_groupVisemeFoldouts.Count <= i)
                        _groupVisemeFoldouts.Add(false);

                    _groupVisemeFoldouts[i] = EditorGUILayout.Foldout(
                        _groupVisemeFoldouts[i], "Viseme カラー詳細", true);
                    if (_groupVisemeFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        for (int v = 0; v < 15; v++)
                            group.VisemeColors[v] = EditorGUILayout.ColorField(
                                VisemeNames[v], group.VisemeColors[v]);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        // ---------------------------------------------------------------
        // Target list
        // ---------------------------------------------------------------

        private void DrawTargetList()
        {
            if (_config.Targets == null) _config.Targets = new System.Collections.Generic.List<EmissionTarget>();

            for (int i = 0; i < _config.Targets.Count; i++)
            {
                var target = _config.Targets[i];

                // VisemeColors が未初期化の場合（古いデータ対応）
                if (target.VisemeColors == null || target.VisemeColors.Length != 15)
                {
                    target.VisemeColors = new Color[15];
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ターゲット {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("削除", GUILayout.Width(44)))
                {
                    _config.Targets.RemoveAt(i);
                    SaveConfig();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // Renderer
                var newRenderer = (Renderer)EditorGUILayout.ObjectField(
                    "Renderer", target.Renderer, typeof(Renderer), true);
                if (newRenderer != target.Renderer)
                {
                    target.Renderer = newRenderer;
                    if (newRenderer != null)
                        target.PropertyName =
                            ShaderPropertyDetector.DetectEmissionProperty(newRenderer, target.MaterialIndex)
                            ?? "_EmissionColor";
                }

                // Material index popup
                if (target.Renderer != null)
                {
                    var mats = target.Renderer.sharedMaterials;
                    if (mats != null && mats.Length > 0)
                    {
                        var matNames = new string[mats.Length];
                        for (int m = 0; m < mats.Length; m++)
                            matNames[m] = $"{m}: {(mats[m] != null ? mats[m].name : "(None)")}";
                        target.MaterialIndex = EditorGUILayout.Popup("Material", target.MaterialIndex, matNames);
                    }
                }
                else
                {
                    target.MaterialIndex = EditorGUILayout.IntField("Material Index", target.MaterialIndex);
                }

                // Property name
                DrawPropertyField(target);

                // Voice カラー設定
                if (_config.Mode == LipsyncMode.Voice || _config.Mode == LipsyncMode.Both)
                    DrawTargetVoiceColor(target, i);

                // Viseme カラー設定
                if (_config.Mode == LipsyncMode.Viseme || _config.Mode == LipsyncMode.Both)
                    DrawTargetVisemeColor(target, i);

                // グループとして保存ボタン
                bool canSaveVoice  = (_config.Mode != LipsyncMode.Viseme) && target.VoiceColorGroupIndex  < 0;
                bool canSaveViseme = (_config.Mode != LipsyncMode.Voice)  && target.VisemeColorGroupIndex < 0;
                if (canSaveVoice || canSaveViseme)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("この設定でグループを作成"))
                        CreateGroupFromTarget(target, canSaveVoice, canSaveViseme);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        // ---------------------------------------------------------------
        // Target Voice カラー設定
        // ---------------------------------------------------------------

        private void DrawTargetVoiceColor(EmissionTarget target, int targetIndex)
        {
            var groupOptions = BuildGroupOptions();
            int currentPopup = ResolveGroupPopup(groupOptions, target.VoiceColorGroupIndex);
            if (currentPopup < 0) { currentPopup = 0; target.VoiceColorGroupIndex = -1; }

            int newPopup = EditorGUILayout.Popup("消灯/点灯カラー", currentPopup, GetGroupLabels(groupOptions));
            target.VoiceColorGroupIndex = groupOptions[newPopup].groupIdx;

            if (target.VoiceColorGroupIndex < 0)
            {
                // 独自設定: コピー/貼り付け → カラーフィールド
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("コピー", GUILayout.Width(52)))
                    s_voiceColorClipboard = (target.OffColor, target.OnColor);
                using (new EditorGUI.DisabledScope(s_voiceColorClipboard == null))
                {
                    if (GUILayout.Button("貼り付け", GUILayout.Width(62)) && s_voiceColorClipboard.HasValue)
                    {
                        target.OffColor = s_voiceColorClipboard.Value.off;
                        target.OnColor  = s_voiceColorClipboard.Value.on;
                        SaveConfig();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                target.OffColor = EditorGUILayout.ColorField("消灯", target.OffColor);
                target.OnColor  = EditorGUILayout.ColorField("点灯", target.OnColor);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // グループ設定: グループのカラーをグレーアウト表示
                int gIdx = target.VoiceColorGroupIndex;
                if (gIdx < _config.ColorGroups.Count)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ColorField("消灯", _config.ColorGroups[gIdx].OffColor);
                    EditorGUILayout.ColorField("点灯", _config.ColorGroups[gIdx].OnColor);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        // ---------------------------------------------------------------
        // Target Viseme カラー設定
        // ---------------------------------------------------------------

        private void DrawTargetVisemeColor(EmissionTarget target, int targetIndex)
        {
            var groupOptions = BuildGroupOptions();
            int currentPopup = ResolveGroupPopup(groupOptions, target.VisemeColorGroupIndex);
            if (currentPopup < 0) { currentPopup = 0; target.VisemeColorGroupIndex = -1; }

            int newPopup = EditorGUILayout.Popup("Viseme カラー", currentPopup, GetGroupLabels(groupOptions));
            target.VisemeColorGroupIndex = groupOptions[newPopup].groupIdx;

            while (_visemeFoldouts.Count <= targetIndex)
                _visemeFoldouts.Add(false);

            if (target.VisemeColorGroupIndex < 0)
            {
                // 独自設定: フォールドアウト内にコピー/貼り付け＋カラーフィールド
                _visemeFoldouts[targetIndex] = EditorGUILayout.Foldout(
                    _visemeFoldouts[targetIndex], "Viseme カラー詳細", true);
                if (_visemeFoldouts[targetIndex])
                {
                    target.TransitionDuration = DurationField("切り替え時間 (秒)", target.TransitionDuration);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("コピー", GUILayout.Width(52)))
                    {
                        s_visemeColorClipboard = new Color[15];
                        target.VisemeColors.CopyTo(s_visemeColorClipboard, 0);
                    }
                    using (new EditorGUI.DisabledScope(s_visemeColorClipboard == null))
                    {
                        if (GUILayout.Button("貼り付け", GUILayout.Width(62)) && s_visemeColorClipboard != null)
                        {
                            s_visemeColorClipboard.CopyTo(target.VisemeColors, 0);
                            SaveConfig();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;
                    for (int v = 0; v < 15; v++)
                        target.VisemeColors[v] = EditorGUILayout.ColorField(
                            VisemeNames[v], target.VisemeColors[v]);
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                // グループ設定: グループのカラーをグレーアウト表示
                int gIdx = target.VisemeColorGroupIndex;
                _visemeFoldouts[targetIndex] = EditorGUILayout.Foldout(
                    _visemeFoldouts[targetIndex], "Viseme カラー詳細（グループを表示中）", true);
                if (_visemeFoldouts[targetIndex] && gIdx < _config.ColorGroups.Count)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.indentLevel++;
                    var grp = _config.ColorGroups[gIdx];
                    for (int v = 0; v < 15; v++)
                        EditorGUILayout.ColorField(VisemeNames[v], grp.VisemeColors[v]);
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        // ---------------------------------------------------------------
        // グループ選択ヘルパー
        // ---------------------------------------------------------------

        private List<(string label, int groupIdx)> BuildGroupOptions()
        {
            var options = new List<(string label, int groupIdx)>();
            options.Add(("独自設定", -1));
            for (int j = 0; j < _config.ColorGroups.Count; j++)
                options.Add(($"グループ: {_config.ColorGroups[j].Name}", j));
            return options;
        }

        private static string[] GetGroupLabels(List<(string label, int groupIdx)> options)
        {
            var labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
                labels[i] = options[i].label;
            return labels;
        }

        private static int ResolveGroupPopup(List<(string label, int groupIdx)> options, int groupIdx)
            => options.FindIndex(o => o.groupIdx == groupIdx);

        // ---------------------------------------------------------------
        // Actions
        // ---------------------------------------------------------------

        private void CreateGroupFromTarget(EmissionTarget target, bool includeVoice, bool includeViseme)
        {
            if (_config.ColorGroups == null)
                _config.ColorGroups = new System.Collections.Generic.List<ColorGroup>();

            int num   = _config.ColorGroups.Count + 1;
            var group = new ColorGroup { Name = $"グループ {num}" };

            if (includeVoice)
            {
                group.OffColor = target.OffColor;
                group.OnColor  = target.OnColor;
            }
            if (includeViseme)
            {
                target.VisemeColors.CopyTo(group.VisemeColors, 0);
                group.TransitionDuration = target.TransitionDuration;
            }

            _config.ColorGroups.Add(group);
            int newIdx = _config.ColorGroups.Count - 1;

            if (includeVoice)  target.VoiceColorGroupIndex  = newIdx;
            if (includeViseme) target.VisemeColorGroupIndex = newIdx;

            SaveConfig();
        }

        private void AddColorGroup()
        {
            if (_config.ColorGroups == null)
                _config.ColorGroups = new System.Collections.Generic.List<ColorGroup>();
            int num = _config.ColorGroups.Count + 1;
            _config.ColorGroups.Add(new ColorGroup { Name = $"グループ {num}" });
            SaveConfig();
        }

        private void AddTarget()
        {
            if (_config.Targets == null) _config.Targets = new System.Collections.Generic.List<EmissionTarget>();
            _config.Targets.Add(new EmissionTarget()); // デフォルトは独自設定（グループ未割り当て）
            SaveConfig();
        }

        private void RunSetup()
        {
            try
            {
                LipsyncLightBuilder.Build(_config);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("LipSync Light エラー", ex.Message, "OK");
                Debug.LogError("[LipSync Light] " + ex.Message);
            }
        }

        private void RunDelete()
        {
            if (!EditorUtility.DisplayDialog(
                    "LipSync Light",
                    "生成物（AnimationClip・Animator レイヤー）を削除しますか？",
                    "削除", "キャンセル"))
                return;

            try
            {
                LipsyncLightBuilder.DeleteGenerated(_config);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("LipSync Light エラー", ex.Message, "OK");
            }
        }

        private void ChooseOutputPath()
        {
            string current = string.IsNullOrEmpty(_config.OutputPath)
                ? Application.dataPath
                : Path.GetFullPath(_config.OutputPath);

            string chosen = EditorUtility.SaveFolderPanel("出力フォルダの選択", current, "");
            if (string.IsNullOrEmpty(chosen)) return;

            if (chosen.StartsWith(Application.dataPath))
                _config.OutputPath = "Assets" + chosen[Application.dataPath.Length..];
            else
                EditorUtility.DisplayDialog("LipSync Light", "Assets フォルダ内を選択してください。", "OK");
        }

        // ---------------------------------------------------------------
        // Validation
        // ---------------------------------------------------------------

        private string Validate()
        {
            if (_config.AvatarRoot == null)
                return "Avatar Root が設定されていません。";
            if (_config.Targets == null || _config.Targets.Count == 0)
                return "ターゲットが1つも設定されていません。";
            for (int i = 0; i < _config.Targets.Count; i++)
            {
                var t = _config.Targets[i];
                if (t.Renderer == null)
                    return $"ターゲット {i + 1} の Renderer が設定されていません。";
                if (string.IsNullOrEmpty(t.PropertyName))
                    return $"ターゲット {i + 1} の Property Name が空です。";
            }
            return null;
        }

        // ---------------------------------------------------------------
        // Config management
        // ---------------------------------------------------------------

        private void EnsureConfig()
        {
            if (_config != null) return;

            _config = AssetDatabase.LoadAssetAtPath<LipsyncLightConfig>(ConfigAssetPath);
            if (_config != null) return;

            string dir = Path.GetDirectoryName(ConfigAssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder(
                    Path.GetDirectoryName(dir),
                    Path.GetFileName(dir));
            }
            _config = CreateInstance<LipsyncLightConfig>();
            _config.ColorGroups.Add(new ColorGroup { Name = "グループ 1" });
            AssetDatabase.CreateAsset(_config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
        }

        private void SaveConfig()
        {
            if (_config == null) return;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------
        // UI helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Renderer が選択されていればシェーダーの Color プロパティをドロップダウンで表示。
        /// 未選択時は自由入力テキストフィールドにフォールバック。
        /// </summary>
        private static void DrawPropertyField(EmissionTarget target)
        {
            if (target.Renderer != null)
            {
                var props = GetMaterialColorProperties(target.Renderer, target.MaterialIndex);
                if (props.Length > 0)
                {
                    var options = new System.Collections.Generic.List<string>(props);
                    // 現在の値がリストにない場合は先頭に追加
                    if (!string.IsNullOrEmpty(target.PropertyName) && !options.Contains(target.PropertyName))
                        options.Insert(0, target.PropertyName);

                    int idx = Mathf.Max(0, options.IndexOf(target.PropertyName));
                    target.PropertyName = options[EditorGUILayout.Popup("Property", idx, options.ToArray())];
                    return;
                }
            }

            // フォールバック: Renderer 未選択またはシェーダー情報なし
            EditorGUILayout.BeginHorizontal();
            target.PropertyName = EditorGUILayout.TextField("Property", target.PropertyName);
            if (GUILayout.Button("自動検出", GUILayout.Width(60)) && target.Renderer != null)
                target.PropertyName =
                    ShaderPropertyDetector.DetectEmissionProperty(target.Renderer, target.MaterialIndex)
                    ?? target.PropertyName;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 指定 Renderer/マテリアルのシェーダーから Color 型プロパティ名の一覧を返す。
        /// </summary>
        private static string[] GetMaterialColorProperties(Renderer renderer, int materialIndex)
        {
            if (renderer == null) return System.Array.Empty<string>();
            var mats = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= mats.Length) return System.Array.Empty<string>();
            var mat = mats[materialIndex];
            if (mat == null || mat.shader == null) return System.Array.Empty<string>();

            int count = ShaderUtil.GetPropertyCount(mat.shader);
            var props = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
                if (ShaderUtil.GetPropertyType(mat.shader, i) == ShaderUtil.ShaderPropertyType.Color)
                    props.Add(ShaderUtil.GetPropertyName(mat.shader, i));
            return props.ToArray();
        }

        /// <summary>
        /// スライダー（0〜0.3）＋数値フィールド（上限なし）の組み合わせ入力。
        /// </summary>
        private static float DurationField(string label, float value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label);
            float clamped   = Mathf.Clamp(value, 0f, 0.3f);
            float newSlider = GUILayout.HorizontalSlider(clamped, 0f, 0.3f, GUILayout.ExpandWidth(true));
            float newField  = Mathf.Max(0f, EditorGUILayout.FloatField(value, GUILayout.Width(55)));
            EditorGUILayout.EndHorizontal();
            // スライダーが動いた場合はその値を優先、そうでなければフィールドの値を使用
            if (Mathf.Abs(newSlider - clamped) > 0.0001f)
                return newSlider;
            return newField;
        }

        private static void DrawHeader(string title)
            => GUILayout.Label(title, EditorStyles.boldLabel);

        private static void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            EditorGUILayout.Space(2);
        }
    }
}
