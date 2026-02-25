# Changelog

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
