# YasudaHaruto 開発・引き継ぎガイド

## 作業開始時（新しい会話でも必須）
- 最初に本ファイル `Assets/YasudaHaruto/AGENTS.md` と `Assets/YasudaHaruto/PROJECT_STATUS.md` の両方を読む。パスはUnityプロジェクトルート基準。
- サブフォルダのAGENTS.mdは、プロジェクトルートから開始した会話で自動読込される保証がない。新しい会話の最初に「Assets/YasudaHaruto/AGENTS.md と PROJECT_STATUS.md を最初に読み、ルールに従って作業してください」と伝える。
- 管理ファイルはこのフォルダに配置する。プロジェクトルートには作成しない。
- `git status --short --branch`、`git fetch`、追跡先とのコミット差分・ファイル差分を可能な範囲で実行する。失敗時は理由と未確認範囲を報告する。
- 既存の未コミット変更・未追跡ファイルを確認し、上書き・破棄しない。確認済み事実、ユーザーの動作確認報告、未確認事項、提案を区別する。

## プロジェクト概要
- チーム共有のUnityゲームプロジェクト「MS GOD TIER GAME」。担当領域は本の操作、ルーン入力・判定、ページ管理、魔法通信の連携。
- ユーザー報告では新しい本モデルへの統合が完了し、ルーン入力・ページめくり・魔法通信は動作確認済み。
- Unity 6000.3.23f1 / C#。URP 17.3.0、Input System 1.20.0、Unity Transport 2.7.4。正確な構成はProjectSettings/ProjectVersion.txt、Packages/manifest.json、packages-lock.jsonを参照する。
- 新構成はBookGameBootstrap / BookGameController / BookGameDragInputからhatakeの本表示へ連携する。旧BookModel系も残っている。
- 他プロジェクトの同名担当フォルダの仕様・担当・ルールを本プロジェクトへ流用しない。ゲーム全体の詳細仕様・完成条件は未確認。

## 変更権限
- 基本の作業範囲はAssets/YasudaHaruto配下。他メンバーのコードは調査可能だが、変更前にユーザーへ相談する。
- Assets/hatake配下は原則変更禁止。チーム共通Scene・Prefabの変更は事前相談する。
- hatake側を変更する必要が生じたら、変更前にユーザーへ相談する。変更範囲が大きく担当側での管理・設計に利点がある場合は、必要なコードをAssets/YasudaHarutoへ複製し、専用版として調整する案を優先検討する（ユーザー方針）。
- 複製は必要な範囲に限定し、元ファイルのパス・複製時のGit状態・独自変更点を記録する。クラス名または名前空間と.metaのGUIDを分け、既存参照を無断で置き換えない。複製の具体案はPROJECT_STATUS.mdを参照。
- 共有設定（ProjectSettings、Packages等）の変更も事前相談する。担当フォルダ内でも他メンバーから参照される資産は影響を調べる。
- 後続のユーザー指示で専用構成への追加・削除・入れ替え・上限設定と簡単な選択UIの実装が許可された。UIは分かりやすく軽量にし、後から変更しやすくする。
- 2026-10-21、ユーザーは「命名規則が元のコードと一致してれば反映して大丈夫」と承認。命名確認・調整後、提示済みの候補構成をAssets/YasudaHaruto/Scenes/BookScene.unityへ反映してよい。Scene名・GUIDと接続処理は維持する。hatake原本変更・既存ファイル削除は引き続き未許可。

## 実装方針
- 2026-10-21、ユーザー承認により未使用のDebugPageInteraction.cs、TransportClientTest.cs、TransportServerTest.csと各.metaを削除。旧Book系も新構成への切り替え完了を条件に削除可能との方針。ただしHamada側Sceneは旧BookTestBootstrapがアクティブなため、現時点では参照中の旧構成を保持する。
- 既存の命名・責務分離・コーディング形式を維持する。既存C#は主にm_フィールド形式。文字コードは変更前に確認し、一括変換しない。
- 担当側のクラス・メソッド・プロパティはPascalCase、インスタンスフィールドはm_、staticフィールドはs_。担当側のenum型・要素は既存のPAGE_TYPE等に合わせてUPPER_SNAKE_CASEとする。複製元hatakeの公開型名はそのまま利用する。
- 本表示、ページの状態、ルーン判定、通信の責務と参照関係を確認してから変更する。
- ページ追加・削除は表示上の左右ペア、固定ページ、現在の見開き、操作開始・終了、通信との整合性を設計してから統合する。
- 削除候補はC#の呼出しだけでなく、Scene・Prefab・ScriptableObjectのGUID参照と実行時ロードを調べる。.metaとGUIDを維持する。
- Unityで元プロジェクトを開いたまま、同じプロジェクトを別Unityプロセスで検証しない。調査目的でSceneやPrefabを再保存しない。
- 変更後は目的に合った検証を行い、実施内容と未確認範囲を報告する。静的調査を実機・Play Mode確認済みとは記載しない。

## Git運用
- CodexはCommit、Push、Pull、Merge、Rebase、Stash、Resetおよびブランチ操作（作成・切替・削除等）を実行しない。同期・履歴確定はユーザーが行う。
- 状態確認・差分確認とfetchのみ可能。追跡先がない場合は勝手に設定しない。
- 他者の変更を巻き戻したり、自動修正・整理に巻き込んだりしない。
- 作業終了時にPROJECT_STATUS.mdの最終作業・確認結果・未完了事項を更新する。別PCへの資料共有もユーザー自身のGit操作が必要。
