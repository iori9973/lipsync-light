# LipSync Light

VRChat アバターのリップシンクパラメーター（`Voice` / `Viseme`）に連動して、
マテリアルのエミッション（色・強度）を制御する Animator 設定を自動生成する Unity Editor 拡張です。

## 機能

- **Voice 連動**: `Voice` パラメーター (0〜1) に応じてエミッション強度をブレンド
- **Viseme カラー**: `Viseme` パラメーター (0〜14) に応じてエミッションカラーを切り替え
- 複数の Renderer / マテリアルを同時にターゲット指定可能
- シェーダーのエミッションプロパティを自動検出

## 動作環境

- Unity 2022.3 以上
- VRChat SDK（VRCSDK3-AVATAR）3.5.0 以上

## インストール

VCC（VRChat Creator Companion）を使用してインストールしてください。

## 使い方

1. `Tools → LipSync Light` でウィンドウを開く
2. **Avatar Root** にアバターの GameObject をドラッグ&ドロップ
3. **[+ ターゲットを追加]** でターゲット Renderer を追加
   - Renderer・マテリアルインデックス・消灯色・点灯色を設定
4. **モード** を選択（Voice 連動 / Viseme カラー / 両方）
5. **[セットアップ実行]** をクリック

`Assets/LipSyncLight/Animations/` に AnimationClip が生成され、
アバターの FX Controller に `LipSyncLight_Voice` / `LipSyncLight_Viseme` レイヤーが追加されます。

## 生成物の削除

**[生成物を削除]** ボタンで生成した AnimationClip とレイヤーをまとめて削除できます。

## ライセンス

MIT License
