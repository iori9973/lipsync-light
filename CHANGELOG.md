# Changelog

## [2.0.16] - 2026-03-04

### Fixed
- `AdditiveBlending=true` かつ Off カラーが黒のとき、Off 状態で元マテリアルの発光・点滅が消える問題を修正
  - v2.0.15 では Off カラー=黒のとき常に黒バリアントを生成していたため、`AdditiveBlending=true` でも Off 状態の `_EmissionBlink` 等が黒バリアントの `_EmissionColor=(0,0,0)` で無効化されていた
  - `AdditiveBlending=true` かつ Off カラー=黒の場合は元マテリアルをそのまま Off 状態として参照するよう変更（「加算モード」の本来の意図：Off 状態は元の発光・点滅を変えない）
  - `AdditiveBlending=false` かつ Off カラー=黒の場合は引き続き黒バリアントを生成し、Off 状態を確実に消灯する

## [2.0.15] - 2026-03-03

### Fixed
- Off カラーを黒に設定した場合に Off 状態で元マテリアルの発光が見えてしまう問題を修正
  - v2.0.14 では Off カラー=黒のとき元マテリアルをそのまま PPtrCurve 参照していたため、元マテリアルに発光設定がある場合に非発声時も光り続けていた
  - 黒を含むすべての Off カラーに対して常にバリアントマテリアルを生成するよう変更
  - 黒バリアントは `_EmissionColor=(0,0,0)` を持つため Off 状態で確実に消灯する
  - `_EmissionBlink` は黒バリアントにも存在するが土台色がゼロのため視覚的に無効（シェーダー内部での点滅計算結果がゼロ）

## [2.0.14] - 2026-03-03

### Fixed
- `materialIndex=0` のターゲットも PPtrCurve（マテリアルスワップ）方式に統一し、lilToon の `_EmissionBlink` / `_Emission2ndBlink` 点滅効果との確実な共存を実現
  - 従来（v2.0.13 まで）は `materialIndex=0` で float curve による `_EmissionColor` 書き込みを行っていたため、アニメーターレイヤーが `_EmissionColor` を常時上書きし、他レイヤーや lilToon シェーダー内部の点滅処理に干渉していた
  - Off 状態では元マテリアルをそのまま参照（PPtrCurve）するため、マテリアルの `_EmissionBlink` 設定が完全に保持され自然な点滅が継続する
  - On 状態では OnColor を設定したバリアントマテリアルを参照（PPtrCurve）するため、発光色が正確に反映される
  - `materialIndex=0` でも `Assets/LipSyncLight/{アバター名}/Materials/` にバリアントマテリアルが生成されるようになった

## [2.0.13] - 2026-03-03

### Fixed
- Off 状態でも lilToon の `_EmissionBlink` / `_Emission2ndBlink` 点滅効果が維持されるよう修正
  - 従来は Off カラーのデフォルト（黒）が `_EmissionColor=(0,0,0,0)` として書き込まれていたため、シェーダー内部で色を変調する `_EmissionBlink` の土台がゼロになり点滅が消えていた
  - materialIndex=0（float curve）: Off カラーが黒の場合、各プロパティの元マテリアル設定色を Off ベースとして自動使用するよう変更
  - materialIndex≥1（PPtrCurve）: Off カラーが黒の場合、黒バリアントを生成せず元マテリアルをそのまま Off 状態として参照するよう変更
  - ユーザーが Off カラーを明示的に非黒に設定している場合は従来通り動作する

## [2.0.12] - 2026-03-03

### Fixed
- Hierarchy から「LipSync Light」を手動削除したとき EditorWindow が自動再生成する問題を修正
  - 従来は `_setup == null` になるたびに毎フレーム `LoadOrCreateSetup()` を呼んでいたため、削除直後に再生成されていた
  - domain reload 後の再取得は `TryLoadSetup()`（検索のみ、新規作成なし）に変更
  - 手動削除後は「LipSync Light セットアップが見つかりません」メッセージと「初期化」ボタンを表示するようにした
- 「生成物を削除」はターゲット・カラー等の設定内容を保持したまま生成物のみ削除するよう修正
  - v2.0.11 では誤って GameObject ごと削除していたため元の挙動に戻した
  - 確認ダイアログに「設定内容は保持されます」の説明を追加

## [2.0.11] - 2026-03-03

### Fixed
- 「生成物を削除」実行後に「LipSync Light」オブジェクトが自動再生成される問題を修正
  - 削除後に「LipSync Light」GameObjectとLipsyncLightSetupコンポーネントも合わせて削除するようにした
  - Avatar Root もクリアすることで EditorWindow が自動再生成しなくなる
  - 削除はUndoに対応している

## [2.0.10] - 2026-03-03

### Fixed
- 同一アバター内に LipsyncLightSetup が複数存在する場合（重複セットアップ）を検出してエラーを表示するようにした
  - 重複があると MA MergeAnimator が同一 FX Controller を二重にマージし、レイヤーが2セット追加されて正常に動作しない
  - `Build()` 実行時に重複を検出した場合は `InvalidOperationException` を throw してビルドを中断する
  - EditorWindow の `Validate()` で重複を検出した場合は「セットアップ実行」ボタンを無効化する
  - EditorWindow の上部に Error HelpBox で重複オブジェクト名を表示する
- Avatar Root 切り替え時に既存の LipsyncLightSetup を GameObject 名によらず検出するようにした
  - 従来は "LipSync Light" という名前の子 GameObject のみを探していたため、別名の既存セットアップがある場合に重複作成されることがあった
  - `GetComponentsInChildren<LipsyncLightSetup>()` で名前によらず検索するよう変更

## [2.0.9] - 2026-03-01

### Fixed
- CS8632 / CS0200 警告・エラーを修正し Unity コンパイル互換性を改善した

## [2.0.8] - 2026-02-26

### Fixed
- lilToon の `_UseEmission` / `_UseEmission2nd` などの有効化フラグを自動的に ON にするようにした
  - エミッションカラーを設定しても有効化フラグが 0 のままだと lilToon では発光が表示されない
  - 非黒色カラーを設定する際、対応する `_Use{Stem}` プロパティが存在すれば自動的に 1f に設定する
  - materialIndex ≥ 1 のバリアントマテリアルにも同様の処理を適用

## [2.0.7] - 2026-02-26

### Fixed
- materialIndex ≥ 1 のターゲットに PPtrCurve（マテリアルスワップ）方式を採用し、発光アニメーションが正しく適用されるようにした
  - Unity の animation binding では `materials[N].PropertyName`（N≥1）は MeshRenderer・SkinnedMeshRenderer を問わず `customType:0` となりランタイムで適用されない根本的制限があった
  - セットアップ実行時に各ターゲット・各クリップ用のマテリアルバリアントを `Assets/LipSyncLight/{アバター名}/Materials/` に自動生成
  - バリアントは元マテリアルのコピーに対象プロパティの色だけを上書きしたもの（`_EmissionBlink` などの既存アニメーションはそのまま保持）
  - `.anim` ファイル内で `m_Materials.Array.data[N]` バインディング（PPtrCurve）により該当スロットのマテリアルをクリップごとに切り替える
  - `生成物を削除` でマテリアルバリアントも一括削除される
- MeshRenderer → SkinnedMeshRenderer への自動変換（v2.0.6）を廃止
  - SkinnedMeshRenderer でも同じ animation binding 制限があり根本解決になっていなかったため

## [2.0.6] - 2026-02-26

### Fixed
- MeshRenderer + materialIndex≥1 のターゲットを自動的に SkinnedMeshRenderer へ変換するようにした
  - Unity のアニメーション binding 制限により、MeshRenderer の `materials[N].` プロパティは
    どのアプローチでも `customType:22` が付かずランタイムで適用されない
  - SkinnedMeshRenderer はこの制限がないため、セットアップ実行時に自動変換する
  - 静的メッシュでは見た目・動作は MeshRenderer と完全に同一
  - 変換は MeshRenderer かつ materialIndex≥1 の場合のみ実施（不要な変換は行わない）
  - Undo 対応（変換を元に戻せる）

## [2.0.5] - 2026-02-26

### Fixed
- MeshRenderer の material index ≥ 1 に対するアニメーションが依然として適用されない問題を修正
  - `GetAnimatableBindings()` も MeshRenderer の `materials[N].` バインディングに `customType:0` を返すことが判明
  - `.anim` ファイル保存後に `SerializedObject` 経由で `customType:0 → 22` (MaterialProperty) に直接書き換えることで根本的に解決

## [2.0.4] - 2026-02-26

### Fixed
- MeshRenderer の material index ≥ 1 を対象にしたアニメーションバインディングが正しく適用されない問題を修正
  - `EditorCurveBinding.FloatCurve()` で手動構築したバインディングが MeshRenderer + `materials[N].` の組み合わせで `customType: 0` になり、ランタイムでアニメーションがサイレント失敗していた
  - `AnimationUtility.GetAnimatableBindings()` を使ってバインディングを構築することで正しい `customType` が設定され、アニメーションが適用されるよう修正

## [2.0.3] - 2026-02-25

### Changed
- Both モードのターゲット UI から消灯/点灯カラーを削除し、Viseme カラー設定に一本化
  - Both モードでは Viseme レイヤーが色を決定し、Voice レイヤーは閾値・フェードによる on/off のみを担うため
- プロパティ追加ドロップダウンを「選択してから追加ボタンで確定」方式に変更
  - 選択中でないときは追加ボタンを無効化

## [2.0.2] - 2026-02-25

### Fixed
- `LipsyncLightSetup` コンポーネントに対して VRChat SDK が「will be removed by the client」警告を出す問題を修正
  - `IEditorOnly` インターフェースを実装し、エディター専用コンポーネントとして正しく認識させた

## [2.0.1] - 2026-02-25

### Added
- セットアップ完了時に完了ポップアップを表示

### Changed
- 出力先設定を廃止: アバター名から `Assets/LipSyncLight/{アバター名}/` へ自動決定（複数アバターでも干渉しない）
- プロパティ選択 UI をシェーダーの表示名（`GetPropertyDescription`）に対応。lilToon 等の日本語ラベルが自動表示される
- プロパティ手動追加のテキストフィールドをドロップダウンに変更
- ターゲットセクションをフォールドアウト化（デフォルト: 開く）
- カラーグループセクションをフォールドアウト化（デフォルト: 閉じる）
- アバター設定時にターゲットを 1 件自動追加し、カラーグループ 1 を割り当て
- グループを使用中のターゲットでもカラーを直接編集可能に（変更はグループに反映）

## [2.0.0] - 2026-02-25

### Added
- **Modular Avatar 対応**（破壊的変更）: FX Controller を直接編集する方式から、Modular Avatar 経由の非破壊ワークフローに移行
  - セットアップ実行時にアバターの Hierarchy 配下に `LipSync Light` GameObject を自動生成
  - `MA Merge Animator` コンポーネントで専用の AnimatorController をビルド時にマージ
  - `LipSync Light` GameObject を無効にすることで効果を一時的にオフにできる
  - VCC でインストールする際に Modular Avatar も自動でインストールされる
- **1つのマテリアルで複数の発光プロパティを同時制御**: チェックボックス式 UI でプロパティを複数選択可能に（例：`_EmissionColor` と `_EmissionColor2` を同時に光らせる）
- **Voice モードにフェード制御を追加**: 「点灯閾値」と「フェード時間」で点灯・消灯のなめらかさを設定可能に（Voice が閾値を超えたらフェードイン、下回ったらフェードアウト）
- **加算ブレンドモードに対応**: 「既存の発光に加算する」オプションを追加。他の発光アニメーションと競合せず共存できる
- `LipSync Light` GameObject の Inspector に「ウィンドウを開く」ボタンを追加

### Changed
- 設定の保存先を `Assets/LipSyncLight/LipsyncLightConfig.asset` からアバター配下の `LipsyncLightSetup` MonoBehaviour コンポーネントに変更
- `vpmDependencies` に `nadena.dev.modular-avatar >= 1.10.0` を追加（必須依存）

### Migration（v1.x → v2.0）
- v1.x で生成した FX レイヤーは自動移行されません。v1.x の「生成物を削除」を実行してから v2.0 でセットアップをやり直してください

## [1.0.2] - 2026-02-25

### Changed
- Property ドロップダウンを改善：発光関連プロパティ（名前に "emission" を含むもの）のみを表示し、lilToon など多数のプロパティを持つシェーダーでも選択しやすくなりました
- 発光関連以外のプロパティが必要な場合は「手動入力」を選択してプロパティ名を直接入力できます

## [1.0.1] - 2026-02-25

### Changed
- Property ドロップダウンを改善：発光関連プロパティを先頭に日本語付きで表示し、その他のカラープロパティを区切り線の後に表示するよう変更

### Fixed
- ウィンドウを閉じる際に設定が保存されないことがある問題を修正

## [1.0.0] - 2026-02-25

### Added
- リップシンクパラメーター（`Voice` / `Viseme`）に連動したエミッション制御
- Voice モード: Blend Tree による滑らかなエミッション強度制御
- Viseme モード: 15 段階の Viseme ごとに異なるエミッションカラーを設定可能
- 複数の Renderer/マテリアルを同時ターゲット指定
- シェーダーからエミッションプロパティ名を自動検出（Standard / lilToon / Poiyomi 対応）
- VCC（VRChat Creator Companion）によるインストールに対応
