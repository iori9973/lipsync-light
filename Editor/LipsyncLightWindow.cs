using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LipsyncLight
{
    internal class LipsyncLightWindow : EditorWindow
    {
        // アバタールート（シリアライズして domain reload 後も保持）
        [SerializeField] private GameObject _avatarRoot;

        // セットアップコンポーネント（シリアライズ不要 - アバターから都度取得）
        private LipsyncLightSetup _setup;

        [SerializeField] private List<bool> _visemeFoldouts       = new List<bool>();
        [SerializeField] private List<bool> _groupVisemeFoldouts  = new List<bool>();

        // セクションの開閉状態（domain reload 後も保持）
        [SerializeField] private bool _targetsFoldout      = true;   // 最初から開く
        [SerializeField] private bool _colorGroupsFoldout  = false;  // 最初は閉じる

        private Vector2 _scrollPos;

        // コピー&ペースト用クリップボード（セッション中のみ保持）
        private static (Color off, Color on)? s_voiceColorClipboard;
        private static Color[]?              s_visemeColorClipboard;

        // 既知の発光プロパティ名（表示名はシェーダーの GetPropertyDescription を使用）
        private static readonly string[] s_knownEmissionPropNames =
        {
            "_EmissionColor",
            "_EmissionColor2",
            "_2nd_EmissionColor",
        };

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
            // domain reload 後にアバタールートが残っていればセットアップを再取得
            if (_avatarRoot != null)
                TryLoadSetup();
        }

        private void OnDisable()
        {
            SaveSetup();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            DrawHeader("LipSync Light Setup");
            EditorGUILayout.Space(4);

            // Avatar Root
            var newRoot = (GameObject)EditorGUILayout.ObjectField(
                "Avatar Root", _avatarRoot, typeof(GameObject), true);
            if (newRoot != _avatarRoot)
            {
                _avatarRoot = newRoot;
                _setup = null;
                if (_avatarRoot != null)
                    LoadOrCreateSetup();
            }
            else if (_setup == null && _avatarRoot != null)
            {
                LoadOrCreateSetup();
            }

            EditorGUILayout.Space(8);

            if (_setup == null)
            {
                EditorGUILayout.HelpBox("Avatar Root を設定してください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            // Mode
            DrawSectionHeader("モード");
            _setup.Mode = (LipsyncMode)GUILayout.SelectionGrid(
                (int)_setup.Mode,
                new[] { "Voice 連動", "Viseme カラー", "両方" },
                3);
            EditorGUILayout.Space(4);

            _setup.AdditiveBlending = EditorGUILayout.ToggleLeft(
                "既存の発光に加算する（他のアニメーションと共存）", _setup.AdditiveBlending);
            if (_setup.AdditiveBlending)
                EditorGUILayout.HelpBox(
                    "加算モード：LipSync Light の発光が既存の発光アニメーションに上乗せされます。\n" +
                    "共存させたい場合は消灯カラーを黒（0,0,0,0）にしてください。",
                    MessageType.Info);
            EditorGUILayout.Space(8);

            // Voice settings
            if (_setup.Mode == LipsyncMode.Voice || _setup.Mode == LipsyncMode.Both)
            {
                DrawSectionHeader("Voice 設定");
                _setup.IntensityMultiplier = EditorGUILayout.Slider(
                    "強度倍率", _setup.IntensityMultiplier, 0f, 2f);
                _setup.VoiceThreshold = EditorGUILayout.Slider(
                    "点灯閾値", _setup.VoiceThreshold, 0f, 1f);
                _setup.VoiceFadeTime = DurationField("フェード時間 (秒)", _setup.VoiceFadeTime);
                EditorGUILayout.Space(8);
            }

            // Targets
            _targetsFoldout = EditorGUILayout.Foldout(_targetsFoldout, "ターゲット", true, EditorStyles.boldLabel);
            if (_targetsFoldout)
            {
                DrawTargetList();
                if (GUILayout.Button("+ ターゲットを追加"))
                    AddTarget();
            }
            EditorGUILayout.Space(8);

            // Color Groups
            _colorGroupsFoldout = EditorGUILayout.Foldout(_colorGroupsFoldout, "カラーグループ", true, EditorStyles.boldLabel);
            if (_colorGroupsFoldout)
            {
                DrawColorGroupList();
                if (GUILayout.Button("+ グループを追加"))
                    AddColorGroup();
            }
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
                SaveSetup();
        }

        // ---------------------------------------------------------------
        // Color Group list
        // ---------------------------------------------------------------

        private void DrawColorGroupList()
        {
            if (_setup.ColorGroups == null) _setup.ColorGroups = new List<ColorGroup>();

            for (int i = 0; i < _setup.ColorGroups.Count; i++)
            {
                var group = _setup.ColorGroups[i];

                if (group.VisemeColors == null || group.VisemeColors.Length != 15)
                    group.VisemeColors = new Color[15];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                group.Name = EditorGUILayout.TextField(group.Name, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("削除", GUILayout.Width(44)))
                {
                    foreach (var t in _setup.Targets)
                    {
                        if (t.VoiceColorGroupIndex == i)  t.VoiceColorGroupIndex  = -1;
                        if (t.VisemeColorGroupIndex == i) t.VisemeColorGroupIndex = -1;
                        if (t.VoiceColorGroupIndex  > i) t.VoiceColorGroupIndex--;
                        if (t.VisemeColorGroupIndex > i) t.VisemeColorGroupIndex--;
                    }
                    _setup.ColorGroups.RemoveAt(i);
                    SaveSetup();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (_setup.Mode == LipsyncMode.Voice || _setup.Mode == LipsyncMode.Both)
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
                            SaveSetup();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    group.OffColor = EditorGUILayout.ColorField("消灯", group.OffColor);
                    group.OnColor  = EditorGUILayout.ColorField("点灯", group.OnColor);
                    EditorGUILayout.EndHorizontal();
                }

                if (_setup.Mode == LipsyncMode.Viseme || _setup.Mode == LipsyncMode.Both)
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
                            SaveSetup();
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
            if (_setup.Targets == null) _setup.Targets = new List<EmissionTarget>();

            for (int i = 0; i < _setup.Targets.Count; i++)
            {
                var target = _setup.Targets[i];

                if (target.VisemeColors == null || target.VisemeColors.Length != 15)
                    target.VisemeColors = new Color[15];
                if (target.PropertyNames == null)
                    target.PropertyNames = new List<string> { "_EmissionColor" };

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ターゲット {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("削除", GUILayout.Width(44)))
                {
                    _setup.Targets.RemoveAt(i);
                    SaveSetup();
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
                    {
                        // Renderer が変わったら既知の発光プロパティで初期化
                        target.PropertyNames.Clear();
                        var detected = ShaderPropertyDetector.DetectEmissionProperty(
                            newRenderer, target.MaterialIndex);
                        target.PropertyNames.Add(detected ?? "_EmissionColor");
                    }
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

                // Property name（複数チェックボックス）
                DrawPropertyField(target, i);

                // Voice カラー設定
                if (_setup.Mode == LipsyncMode.Voice || _setup.Mode == LipsyncMode.Both)
                    DrawTargetVoiceColor(target, i);

                // Viseme カラー設定
                if (_setup.Mode == LipsyncMode.Viseme || _setup.Mode == LipsyncMode.Both)
                    DrawTargetVisemeColor(target, i);

                // グループとして保存ボタン
                bool canSaveVoice  = (_setup.Mode != LipsyncMode.Viseme) && target.VoiceColorGroupIndex  < 0;
                bool canSaveViseme = (_setup.Mode != LipsyncMode.Voice)  && target.VisemeColorGroupIndex < 0;
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
        // Property field（複数プロパティ チェックボックス選択）
        // ---------------------------------------------------------------

        private void DrawPropertyField(EmissionTarget target, int targetIndex)
        {
            if (target.PropertyNames == null) target.PropertyNames = new List<string>();

            EditorGUILayout.LabelField("発光プロパティ", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (target.Renderer != null)
            {
                var allProps    = GetMaterialColorPropertiesWithDesc(target.Renderer, target.MaterialIndex);
                var propDescMap = allProps.ToDictionary(x => x.name, x => x.desc);

                // 1. 既知の発光プロパティ（シェーダーの説明文でラベル表示）
                foreach (var propName in s_knownEmissionPropNames)
                {
                    if (!propDescMap.TryGetValue(propName, out var desc)) continue;
                    bool on    = target.PropertyNames.Contains(propName);
                    bool newOn = EditorGUILayout.ToggleLeft(FormatPropLabel(propName, desc), on);
                    if (newOn && !on) target.PropertyNames.Add(propName);
                    else if (!newOn && on) target.PropertyNames.Remove(propName);
                }

                // 2. 名前に "emission" を含むその他のプロパティ
                foreach (var (name, desc) in allProps)
                {
                    if (s_knownEmissionPropNames.Contains(name)) continue;
                    if (name.IndexOf("emission", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool on    = target.PropertyNames.Contains(name);
                    bool newOn = EditorGUILayout.ToggleLeft(FormatPropLabel(name, desc), on);
                    if (newOn && !on) target.PropertyNames.Add(name);
                    else if (!newOn && on) target.PropertyNames.Remove(name);
                }

                // 3. シェーダーに存在しない手動追加プロパティ（× で削除のみ可能）
                foreach (var mp in target.PropertyNames.ToList())
                {
                    if (propDescMap.ContainsKey(mp)) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(mp, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                        target.PropertyNames.Remove(mp);
                    EditorGUILayout.EndHorizontal();
                }

                // 4. 発光以外のプロパティをドロップダウンで追加
                var remaining = allProps
                    .Where(x => !IsEmissionProp(x.name) && !target.PropertyNames.Contains(x.name))
                    .ToArray();

                if (remaining.Length > 0)
                {
                    var labels = new[] { "（追加するプロパティを選択）" }
                        .Concat(remaining.Select(x => FormatPropLabel(x.name, x.desc)))
                        .ToArray();
                    int sel = EditorGUILayout.Popup("その他のプロパティを追加", 0, labels);
                    if (sel > 0)
                    {
                        var toAdd = remaining[sel - 1].name;
                        if (!target.PropertyNames.Contains(toAdd))
                        {
                            target.PropertyNames.Add(toAdd);
                            SaveSetup();
                        }
                    }
                }
            }
            else
            {
                // Renderer 未選択時は PropertyNames を直接表示（削除のみ可能）
                foreach (var mp in target.PropertyNames.ToList())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(mp, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                        target.PropertyNames.Remove(mp);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
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
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("コピー", GUILayout.Width(52)))
                    s_voiceColorClipboard = (target.OffColor, target.OnColor);
                using (new EditorGUI.DisabledScope(s_voiceColorClipboard == null))
                {
                    if (GUILayout.Button("貼り付け", GUILayout.Width(62)) && s_voiceColorClipboard.HasValue)
                    {
                        target.OffColor = s_voiceColorClipboard.Value.off;
                        target.OnColor  = s_voiceColorClipboard.Value.on;
                        SaveSetup();
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
                int gIdx = target.VoiceColorGroupIndex;
                if (gIdx < _setup.ColorGroups.Count)
                {
                    var group = _setup.ColorGroups[gIdx];
                    EditorGUILayout.BeginHorizontal();
                    var newOff = EditorGUILayout.ColorField("消灯", group.OffColor);
                    var newOn  = EditorGUILayout.ColorField("点灯", group.OnColor);
                    EditorGUILayout.EndHorizontal();
                    if (newOff != group.OffColor) { group.OffColor = newOff; SaveSetup(); }
                    if (newOn  != group.OnColor)  { group.OnColor  = newOn;  SaveSetup(); }
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
                            SaveSetup();
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
                int gIdx = target.VisemeColorGroupIndex;
                _visemeFoldouts[targetIndex] = EditorGUILayout.Foldout(
                    _visemeFoldouts[targetIndex], "Viseme カラー詳細（グループ）", true);
                if (_visemeFoldouts[targetIndex] && gIdx < _setup.ColorGroups.Count)
                {
                    var grp = _setup.ColorGroups[gIdx];
                    grp.TransitionDuration = DurationField("切り替え時間 (秒)", grp.TransitionDuration);
                    EditorGUI.indentLevel++;
                    for (int v = 0; v < 15; v++)
                    {
                        var newColor = EditorGUILayout.ColorField(VisemeNames[v], grp.VisemeColors[v]);
                        if (newColor != grp.VisemeColors[v]) { grp.VisemeColors[v] = newColor; SaveSetup(); }
                    }
                    EditorGUI.indentLevel--;
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
            for (int j = 0; j < _setup.ColorGroups.Count; j++)
                options.Add(($"グループ: {_setup.ColorGroups[j].Name}", j));
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
            if (_setup.ColorGroups == null) _setup.ColorGroups = new List<ColorGroup>();

            int num   = _setup.ColorGroups.Count + 1;
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

            _setup.ColorGroups.Add(group);
            int newIdx = _setup.ColorGroups.Count - 1;

            if (includeVoice)  target.VoiceColorGroupIndex  = newIdx;
            if (includeViseme) target.VisemeColorGroupIndex = newIdx;

            SaveSetup();
        }

        private void AddColorGroup()
        {
            if (_setup.ColorGroups == null) _setup.ColorGroups = new List<ColorGroup>();
            int num = _setup.ColorGroups.Count + 1;
            _setup.ColorGroups.Add(new ColorGroup { Name = $"グループ {num}" });
            SaveSetup();
        }

        private void AddTarget()
        {
            if (_setup.Targets == null) _setup.Targets = new List<EmissionTarget>();
            var t = new EmissionTarget();
            // カラーグループが1つ以上あれば、新規ターゲットはグループ 1 をデフォルトで使用
            if (_setup.ColorGroups != null && _setup.ColorGroups.Count > 0)
            {
                t.VoiceColorGroupIndex  = 0;
                t.VisemeColorGroupIndex = 0;
            }
            _setup.Targets.Add(t);
            SaveSetup();
        }

        private void RunSetup()
        {
            try
            {
                LipsyncLightBuilder.Build(_setup);
                EditorUtility.DisplayDialog(
                    "LipSync Light",
                    "セットアップが完了しました！\n\nアバターを VRChat にアップロードすると、" +
                    "リップシンクに連動して発光するようになります。",
                    "OK");
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
                    "生成物（AnimationClip・FX Controller・MA Merge Animator）を削除しますか？",
                    "削除", "キャンセル"))
                return;

            try
            {
                LipsyncLightBuilder.DeleteGenerated(_setup);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("LipSync Light エラー", ex.Message, "OK");
            }
        }

        // ---------------------------------------------------------------
        // Validation
        // ---------------------------------------------------------------

        private string Validate()
        {
            if (_avatarRoot == null)
                return "Avatar Root が設定されていません。";
            if (_avatarRoot.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>() == null)
                return "Avatar Root に VRCAvatarDescriptor が見つかりません。";
            if (_setup == null)
                return "セットアップコンポーネントが見つかりません。";
            if (_setup.Targets == null || _setup.Targets.Count == 0)
                return "ターゲットが1つも設定されていません。";
            for (int i = 0; i < _setup.Targets.Count; i++)
            {
                var t = _setup.Targets[i];
                if (t.Renderer == null)
                    return $"ターゲット {i + 1} の Renderer が設定されていません。";
                if (t.PropertyNames == null || t.PropertyNames.Count == 0)
                    return $"ターゲット {i + 1} の発光プロパティが1つも選択されていません。";
            }
            return null;
        }

        // ---------------------------------------------------------------
        // Setup management
        // ---------------------------------------------------------------

        private void TryLoadSetup()
        {
            if (_avatarRoot == null) { _setup = null; return; }
            var child = _avatarRoot.transform.Find("LipSync Light");
            if (child != null)
                _setup = child.GetComponent<LipsyncLightSetup>();
        }

        private void LoadOrCreateSetup()
        {
            if (_avatarRoot == null) { _setup = null; return; }

            // 既存の "LipSync Light" 子 GameObject を探す
            var child = _avatarRoot.transform.Find("LipSync Light");
            if (child != null)
            {
                _setup = child.GetComponent<LipsyncLightSetup>();
                if (_setup != null) return;
            }

            // 新規作成
            var go = new GameObject("LipSync Light");
            Undo.RegisterCreatedObjectUndo(go, "Create LipSync Light");
            go.transform.SetParent(_avatarRoot.transform, false);
            _setup = Undo.AddComponent<LipsyncLightSetup>(go);
            _setup.ColorGroups.Add(new ColorGroup { Name = "グループ 1" });
            // デフォルトでターゲットを1件追加し、グループ 1 を割り当て
            _setup.Targets.Add(new EmissionTarget
            {
                VoiceColorGroupIndex  = 0,
                VisemeColorGroupIndex = 0,
            });
            EditorUtility.SetDirty(_setup);
        }

        private void SaveSetup()
        {
            if (_setup == null) return;
            EditorUtility.SetDirty(_setup);
        }

        // ---------------------------------------------------------------
        // UI helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// シェーダーの Color プロパティ名と説明文の一覧を返す。
        /// 説明文は ShaderUtil.GetPropertyDescription（シェーダー定義の表示名）を使用するため、
        /// lilToon など日本語対応シェーダーでは日本語ラベルが得られる。
        /// </summary>
        private static (string name, string desc)[] GetMaterialColorPropertiesWithDesc(
            Renderer renderer, int materialIndex)
        {
            if (renderer == null) return System.Array.Empty<(string, string)>();
            var mats = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= mats.Length)
                return System.Array.Empty<(string, string)>();
            var mat = mats[materialIndex];
            if (mat == null || mat.shader == null) return System.Array.Empty<(string, string)>();

            int count = ShaderUtil.GetPropertyCount(mat.shader);
            var props = new List<(string name, string desc)>();
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.Color)
                    continue;
                string name = ShaderUtil.GetPropertyName(mat.shader, i);
                string desc = ShaderUtil.GetPropertyDescription(mat.shader, i);
                props.Add((name, desc));
            }
            return props.ToArray();
        }

        /// <summary>
        /// プロパティ名と説明文から UI 表示ラベルを生成する。
        /// 説明文がプロパティ名と異なる場合：「説明文 (プロパティ名)」
        /// 同じまたは空の場合：「プロパティ名」のみ
        /// </summary>
        private static string FormatPropLabel(string propName, string desc)
        {
            if (!string.IsNullOrEmpty(desc) && desc != propName)
                return $"{desc} ({propName})";
            return propName;
        }

        /// <summary>
        /// 発光関連プロパティかどうかを判定する（既知リスト or 名前に "emission" を含む）。
        /// </summary>
        private static bool IsEmissionProp(string propName)
            => s_knownEmissionPropNames.Contains(propName)
               || propName.IndexOf("emission", System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static float DurationField(string label, float value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label);
            float clamped   = Mathf.Clamp(value, 0f, 0.3f);
            float newSlider = GUILayout.HorizontalSlider(clamped, 0f, 0.3f, GUILayout.ExpandWidth(true));
            float newField  = Mathf.Max(0f, EditorGUILayout.FloatField(value, GUILayout.Width(55)));
            EditorGUILayout.EndHorizontal();
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
