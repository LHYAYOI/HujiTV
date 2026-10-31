# Project Status

## 現在の状態
新しい本モデルへの統合完了。
ルーン入力・ページめくり・魔法通信は動作確認済み。
※動作確認の根拠は下記記録を参照。魔法通信はユーザーがテスト済みで問題なしと再確認しており、追加の再テストは不要。

$12026-10-29：お邪魔4枠の初期実装。左右擦り・スクラッチ、枠保持、4番枠の操作制限、付与通信と予約を追加。Editor／現行SceneのPlay Mode検証と既存RuneFeedbackは通過。完成素材、実機通信、物理操作は未確認。詳細は末尾とBOOK_OBSTACLE_IMPLEMENTATION.mdを参照。

## 次のタスク
- ページ追加・削除の統合
- 不要コードの調査・削除
- ルーン描画中エフェクト
- 射出方向選択演出

## 注意点
- hatake配下は原則変更禁止
- チーム共通Scene・Prefabの変更は相談
- 他メンバーのコード変更は事前相談
- Commit / Pushはユーザーが実施
- Pull / Merge / Rebase / Stash / Reset / ブランチ操作もCodexは実行しない
- 新しい会話でも最初にこのファイルと同階層のAGENTS.mdを読む。新規会話へ両パスの読込指示を渡す（自動読込に依存しない）。

## 調査結果（2026-10-21）

### 開発環境とGit
- Unity 6000.3.23f1を指定。同版Editorのローカル実行ファイルも確認した。
- URP 17.3.0 / Input System 1.20.0 / Transport 2.7.4 / Test Framework 1.6.0をmanifestで確認。
- Build SettingsはTransportClientTest、BookSceneの順で有効。TransportServerTestも存在するがビルド一覧には含まれない。
- Gitの管理ディレクトリは親のHujiTV/.git。通常実行は親へのアクセスで失敗したため、承認付き実行でstatus・fetch・リモート差分を確認した。
- ブランチ：MS-63/Feature/BookViewSystem。HEAD：0599d35。fetch後の追跡先origin/MS-63/Feature/BookViewSystemとコミット差分なし。
- 調査開始時点で既存変更あり：.vs配下、Assets/Settingsの3アセット、TestPage.asset、BookScene.unity、BookPageAreaDetector.cs、hatakeのBookFlipView.cs・BookPageView.cs・number.png.meta。
- BookGameBootstrap、BookGameController、BookFlipCoordinateMapper、BookFlipCoordinateTest、BookGameDragInputと各.metaは開始時点で未追跡。Codexの作成物ではない。既存変更は保持した。

### 構成と修正候補
1. ページ追加・削除の統合：BookGameControllerは初期配列からPageInstanceを生成し、見開き番号から配列位置を求める固定構成。BookNetworkClient.AddPageReceivedの購読は旧BookTestBootstrapにあり、新BookGameBootstrapにはない。BookPageServiceも旧BookModel依存。表示と論理状態を同時に更新する連携の設計が必要。
2. ページの単位確認：hatake.BookControllerにはAddPage / RemovePage、InsertLeaf、InsertSpreadPages、BurnSpread等がある。新構成は固定ページと通常見開きの左右を前提とするため、単ページAPIをそのまま呼ぶと整合性を崩す懸念がある。既存公開APIで接続可能か先に検討し、hatake変更が必要なら相談する。
3. 通信初期化：BookGameBootstrap.StartでBookNetworkClient.Instanceを一度取得する。未生成時はnetworkServiceがnullのままとなる。CastMagicCommandは通信結果を使わないため、起動順・未接続時の表示・再接続時の挙動を確認する。
4. 初期化失敗後の入力：BookGameBootstrapはGameControllerの初期化成功より先に入力を初期化する。設定不備時の操作可否を確認する。
5. 入力と終了処理：BookGameDragInputの無効化、ページ変更、ルーンチャージ中の操作について、状態の復帰と入力競合をPlay Modeで確認する。現時点では不具合断定ではない。

### 削除候補（削除未承認・未実施）
- MagicSystem.cs：クラス本体がコメントアウトされている。ただしGUIDがTransportServerTestに残るため、Scene上の状態を確認してから判断する。
- OldBook.prefab：Assets内のunity / prefab / assetのGUID検索では参照を検出しなかった。文字列・実行時ロードや利用予定は未確認。
- 旧BookModel / BookController / BookDisplayController / BookTestBootstrap一式：新構成への移行完了後の候補。旧BootstrapとDisplayはBookSceneとHamada/Scene/BookSceneHamadaから参照されており、現時点で削除しない。通常C#クラスはGUID参照がなくても使用される。
- BookRaycastTest、BookFlipCoordinateTest、RuneTestBootstrap、RuneRealtimeEvaluationTest、DebugRuneTraceView等の検証系：BookSceneに参照が残る。一部はHamada側Sceneにも参照あり。参照の有無と有効状態・検証用途を分けて確認する。
- GUID検索は静的な一次調査であり、未使用の証明ではない。他メンバーのScene変更が必要な場合は事前相談する。

### 推奨作業順序
1. ユーザー自身が既存未コミット変更を確認・整理し、現在の動作基準を確定する。CodexはGit履歴操作をしない。
2. BookSceneのInspector参照・旧新コンポーネントの有効状態を確認し、接続→本を開く→ルーン入力→方向選択→魔法送信→ページめくりを再確認する。
3. ページの追加・削除仕様（対象、挿入位置、上限、固定ページ保護、表示中ページ削除時、通信形式）を決め、新表示と論理状態の連携を設計する。
4. 承認された範囲でページ追加・削除を統合し、ページ操作中・切断時・再接続時を検証する。
5. 新旧参照を再調査し、チームへ影響を確認してから不要コードの削除を判断する。
6. ルーン描画中エフェクト、射出方向選択演出を追加する。

## 最終作業
日付：2026-10-28
変更内容：MagicDataへ[SerializeField] private Texture2D m_impactRuneTextureと読取専用ImpactRuneTextureを追加。InspectorのImpact Rune Textureへ魔法ごとの固定画像を1枚設定できる。
確認結果：フィールド・APIと利用箇所を静的確認。既存のID・型・狙い方・通信形式は維持。Git fetch後、追跡先とのコミット・ファイル差分なし。Unityコンパイル・Inspector・Play Mode検証は未実施。
未完了事項：各MagicDataへの画像割り当て。着弾表示側での参照・未設定時の演出省略と、弾・命中コードへの接続。既存画像の用途を決め打ちせず、今回assetへの自動割り当てはしていない。

### 前回作業（クールダウン）
変更内容：RuneCooldownImageCoordinatesを追加し、バッチ化前の画像内の高さ0〜1をUV1へ渡す方式へ変更。シェーダーから画像ローカルRectとCanvas頂点座標の混在計算を除去。
確認結果：Verify Rune FeedbackがCOMPLETE。専用PreviewSceneに移動・拡大・左上pivotのWorld Space Canvas/Imageを作り、Camera.RenderのGPU読み戻しで回復高さ25%・50%・75%がそれぞれ0.25・0.5・0.75になることを確認。既存回帰検証も通過。Scene保存・再読み込みなし。
未完了事項：ユーザーの実操作とAndroidでの再確認。透明余白による見え始めの差は現仕様として残す。領域全体の暗幕方式は未実装。

## 動作確認記録（2026-10-21）
- 起動中のUnity 6000.3.23f1のBookSceneでPlay Modeへ進め、本の見開き表示を確認。新規バッチコンパイルや実機ビルドは未実施。
- 今回の実行に対応するConsole / Editor.logにBookGameBootstrapの「初期化完了」を確認。
- 実行中のログにSpread=1（Test01）→2（Fixed）→1、後に0（Fixed）を確認。画面でも2/3の通常見開きと、表紙内側/1の固定見開きを確認。
- ルーンはCHARGING→Cast Confirmed / Direction=CENTER→Cooldown Started / 1.0s→Cooldown Finishedのログを確認。ただし手動入力が検出されたため、自動操作単独での再現成功とは区別する。左右上下の方向入力・失敗からの復帰は今回未確認。
- 保存済みScene上で旧BookTestBootstrapのGameObjectは非アクティブ、新BookGameBootstrap / BookGameControllerはアクティブ。Hierarchy表示とも整合する。
- BookScene単独起動ではBookNetworkClientの直接配置は確認されず、通信接続を通っていない。発動確定ログは通信成功の証拠ではない。通常はTransportClientTestの接続UIからBookSceneへ遷移する構成。
- ConsoleにSerializedObjectNotCreatableException（Object at index 0 is null）とMissingReferenceException（GameObjectInspector.m_Targets）の2件を確認。表示された箇所はUnityEditor側。原因・ゲーム処理への影響は未確定。
- urp_ReflProbes_*、_AdditionalShadowParamsの配列サイズ超過警告が大量に発生。ログにはUnity再起動による配列再生成の案内がある。設定変更や再起動は行っていない。
- 画面操作はcomputer-useスキルで実施。マウスドラッグの期待動作を自動操作だけで確実には再現できておらず、ゲームの不具合とは断定していない。
- 後続のユーザー回答：通信は同一PCのUnity Editorサーバー＋クライアントexeでテスト済み、問題なし。通信の再テストは行わない。

## ページ追加・削除の統合案（2026-10-21、未実装）

### 管理する単位と責務
- PageData / PageInstanceの1件を「ゲーム用の1見開き」とする。表示はLeftVisualDataとRightVisualDataの2件で1組。追加・削除は必ずこの組単位で行う。
- 先頭の表紙内側枠・最初の固定ページ・最後の固定ページを保護する。ゲーム用見開き数Nに対し表示ページ数は3+2N、ゲーム見開きiの左表示indexは2+2i、Spreadはi+1という対応を維持する。
- BookGameControllerが可変長のPageInstance一覧とCurrentPageを管理し、表示位置はhatake.BookControllerのCurrentSpreadを参照する。旧BookModelの現在位置を並行して進める構成にはしない。
- PageDataは種類の定義。追加された個体はPageInstanceで識別する。同一PageIdの複数所持を許可する場合、削除対象をPageIdだけで特定しない。
- PageDatabase / PageFactory / 既存のADD_PAGE受信は再利用する。旧BookPageServiceは旧BookModelと他Sceneの利用を保持し、新構成用のサービス（仮称BookGamePageService）を別に用意する案を推奨する。

### 追加処理
1. BookGameBootstrapでAddPageReceivedを購読し、新サービスへ渡す。OnDestroyで購読解除する。初期化前に受けた要求の扱いも定める。
2. PageDatabaseでIDを解決し、左右表示データ・挿入位置を検証する。0・不明ID・Database内の重複IDは拒否する。所持ページの同種重複は許可する。上限到達時は即拒否せず、下記の入れ替え選択へ進む。
3. 本の開閉・めくり・描画・チャージ・ページ編集状態を確認する。操作中や本が閉じている場合は有界FIFOで保留し、通常状態かつ本が開いた後に順に処理する案。要求は「受付・保留・完了・拒否」を区別し、既存APIのboolを完了扱いしない。上限・対象は実行時にも再検証する。
4. 挿入先を確定してPageInstanceを生成し、変更予定を保持する。表示APIが拒否した場合は一覧を変更しない。
5. 演出中は現在のInteractionを終了し、入力を止める。PageInsertedは表示ページごとに2回通知されるため、通知1回を1見開きの完了にしない。
6. 表示データ更新後のSpreadShownを完了境界とし、左右ペア・件数・予定位置を照合して論理一覧を1回だけ確定し、その後にCurrentPageを再計算・Interaction開始する。既存OnSpreadShownへ先に流さず、この順序をController内で統一する。

### 挿入位置の制約と選択肢
- ユーザー方針：RANDOMは廃止し、END / AFTER_CURRENTを残す。ENDは最後の固定ページの直前、AFTER_CURRENTは現在のゲーム見開きの直後とする案。固定見開き表示時のAFTER_CURRENTは先頭側なら0、末尾側ならNとする案だが要確定。
- RANDOM廃止の実装時はenumの既存数値（END=0 / AFTER_CURRENT=1）を維持する。既存Guide / Obstacle等のm_insertType=2を列挙して移行先を決め、勝手にENDへ読み替えない。今回はコードやアセットを変更しない。
- 現在のhatake.InsertSpreadPagesは「表示中の見開きの前」にしか挿入できず、末尾・現在の次・ランダムの任意位置指定はできない。InsertLeafは左右ペアを分割するため、この統合には用いない。
- 推奨：既存の挿入位置を維持するなら、hatake担当へ「指定したゲーム見開き境界への2ページ一括挿入」「現在の見開きをどう維持するか」「完了・中断通知」を相談する。APIの追加・変更はまだ行わない。
- hatake変更なしで試作する案：現在位置への挿入に仕様を限定すれば公開APIを利用できる。ただし既存InsertTypeの意味が変わるため、ユーザーの仕様判断が必要。
- 別案として挿入先へ自動移動してから既存APIを呼ぶことは検討可能。ただし閉じた本が開く、閲覧位置が変わる等の見える挙動が増えるため、黙って採用しない。単ページの削除・再追加による全体再構築やprivateフィールド操作も採用しない。

### 削除処理
- 削除契機を2系統に分ける。上限を超える追加時には不要な所持ページを選択して削除する。お邪魔ページのアクションによる削除は候補であり、条件は未定。現段階でアクション成功と自動削除を結び付けない。
- BurnSpreadは表示中の見開きが対象。選択UIで非表示の所持ページを選べるようにするには、選択先を表示して削除する流れか、非表示位置の削除APIが必要。上限時削除は第二段階へ先送りせず初期統合の対象とする。先頭・末尾の固定見開きは選択・削除対象から除外する。
- 削除開始前にInteractionを終了し、対象PageInstanceと表示位置を保存する。PageRemovedは通常2回（右index→左index）通知される。完了SpreadShownで一覧を1件削除してから、新しい表示ページのInteractionを開始する。
- 現行BurnSpreadは通常のゲーム見開き削除後に「同じSpread位置の次の見開き」を表示する。最後のゲーム見開きを消すと末尾固定ページが表示される。旧BookModelの「前のゲームページを優先」と挙動が異なるため、仕様として確認する。
- 現行BOOK_MESSAGE_TYPEには削除メッセージがない。今回の選択削除のためだけにサーバー発の削除メッセージは増やさない。サーバー側も所持一覧を管理する仕様が必要になった場合は別途同期方法を決める。

### 上限・同種所持・入れ替え（ユーザー回答反映）
- 上限値は後から変更できるよう一か所で管理する。推奨はYasudaHaruto配下のBookInventorySettings（仮称ScriptableObject）にMaxGamePageCountを置き、Bootstrapから管理側へ渡す構成。初期数値は未指定であり、10に固定しない。
- 上限の単位はゲーム用見開き数とし、固定ページを含めない案。同種ページも1個体につき1件として数える。定義IDが同じでも異なるPageInstanceを生成し、削除選択は個体を対象にする。
- 上限を超える追加要求は新規分を保留し、所持ページの削除選択を開始する。選択中の新しい要求は順番待ちとし、複数の選択画面を同時に開かない。
- 推奨フロー：追加要求→容量判定→満杯なら所持ページを選択→対象と追加予定を事前検証→削除完了→追加完了。単に1件削除できた時点で入れ替え成功とは扱わない。
- 削除後に追加が失敗した場合、削除だけが残らないよう復元または一括置換APIでの処理を設計する。現行表示APIは複数操作をまとめて成功・失敗させる契約を持たないため、hatakeへの相談項目に含める。
- 選択キャンセル時は既存ページを維持し、新規ページの入手を辞退する案。キャンセル可能か・新規分も選択対象にするかは未確定。
- 上限設定はまず開始時に読み込む案。プレイ中の引き下げ・既存所持数が設定超過の場合に自動削除はしない。実行中変更が必要なら不足枠数に応じた整理フローを別途決める。

### 操作競合・中断への対応
- BookGameDragInputにも共通の編集ガードを設け、ページドラッグ・ルーン入力・発動を演出中に開始できないようにする。終了イベントでのみ復帰する。
- hatake.BookFlipKeyboardActionsのL/Iキーはゲーム側一覧を通らず直接編集する。通常操作では同じ窓口へ接続するか、ページ編集キーだけを無効化する必要がある。該当Scene / Prefab設定の変更も実装前に相談する。
- ページ変更通知が予定外の場合、左右データから種類を推測して同種個体を自動再生成しない。不整合を報告して入力を停止する。
- Scene終了・無効化・演出中断で完了通知が来ない場合を考慮する。表示データが変わっていないなら未確定要求を破棄し、変わっているなら実データと照合して回復する。完了確認なしに入力だけ復帰させない。公開APIには専用の編集中断通知がないため、ライフサイクル確認またはAPI相談が必要。
- 保留要求の最大件数・待ち時間・切断時破棄の扱いは未確定。無制限にためない。同じPageIdの連続受信を無条件に重複除去しない。

### 変更候補と影響範囲
- Assets/YasudaHaruto/Scripts/Book/BookGameController.cs：可変長一覧、編集受付と完了確定、固定ページ保護、CurrentPageの更新。
- Assets/YasudaHaruto/Scripts/Book/BookGameBootstrap.cs：Database等の組立、受信購読・解除、終了時の保留処理。
- Assets/YasudaHaruto/Scripts/Book/Input/BookGameDragInput.cs：編集ガードとの連携。
- Assets/YasudaHaruto/Scripts/Pageに新構成用サービス：ID解決、挿入方針、保留管理。具体的なクラス分割は実装時に最小化する。
- BookScene / 関連Prefab：Database・上限参照、L/Iデバッグ編集設定。変更前に相談する。
- hatake.BookController：既存挿入方針を保持する場合のAPI拡張相談先。他メンバー領域のため変更未承認。
- 旧BookModel / BookPageService / BookTestBootstrapは今回の統合段階では削除・置換しない。

### データ上の事前確認
- TestPageDatabaseは現在TestPage、TestPage 1、TestPage 2、TestPage 3の4件を登録している。Guide / Obstacle用アセットは含まれていないため、利用するなら登録と左右表示参照の確認が必要。
- TestPage 3とTestGuidePageはどちらもPageId=5。ただし現在のDatabaseには後者が未登録で、Database内の重複ではない。両方登録する場合はID整理が必要。今回はアセットを変更していない。

### 実装前に確定する仕様
1. 確定方針：ランダム追加は廃止。上限値は変更可能な設定で管理。同じページの複数所持を許可。上限超過時は不要な所持ページを選択して削除する。
2. 未確定：追加後・削除後の閲覧位置、満杯時の選択UI・キャンセル可否。上限の初期値は後から設定できるため、設計を進めるための必須回答にはしない。
3. 未確定：お邪魔ページのアクション削除の成立条件。通常魔法は使用で消えると仮定しない。未定の条件と独立した削除要求の入口を設ける案。

### 推奨実装・検証順
1. 上記仕様を確定し、必要ならhatake担当へAPI相談。変更するScene / Prefabも提示する。
2. 上限設定、可変長一覧と固定ページ保護、編集状態を整え、まず1件の追加・削除を表示と論理の両方で一致させる。
3. ADD_PAGE受信を新構成へ接続し、保留・拒否・上限・同種複数を確認する。既存の魔法通信自体の再検証は求めない。
4. 上限時の選択削除と新規追加、キャンセル、同種ページの個体選択、削除後の追加失敗を確認する。さらに0→1件、1→0件、先頭・中間・末尾、固定ページ、本が閉じた状態、描画・チャージ・めくり中の受信、連続受信、無効化中断を確認する。各操作後に3+2Nと左右のGUID対応、CurrentPage、イベント購読数を確認する。
5. 新構成の安定後に旧コードの削除可否を別途判断する。

## 専用Controllerと選択画面の具体案（2026-10-21、未実装）

本節は前節の「hatakeへAPI拡張を相談する」案を具体化した最新案。ユーザーはhatake原本の変更前に相談すること、変更範囲が大きく設計上有利なら担当フォルダへの複製・調整を検討することを指定。今回は管理資料だけを更新し、複製や実装は行っていない。

### 推奨する複製範囲
- hatake.BookController（調査時820行）を基に、Assets/YasudaHaruto/Scripts/Book/Presentation/YasudaBookViewController.cs（仮称）を作る。既存BookGameControllerと名前・責務を区別する。
- 元Controllerはsealedで、実行時ページ一覧や編集処理はprivate。継承や外側からのラッパーだけでは任意位置編集を追加できない。反射でprivate状態を書き換える方式は使わない。
- hatake.BookFlipView / BookPageView / BookModelView、BookPageData / BookSpreadData / BookData、既存Material・Shaderは参照して再利用する。調査した表示側にはController型への実装上の直接依存が見当たらず、Controllerだけを分けられる見込み。ただし複製後のコンパイルと表示確認は必要。
- 担当コードのBookGameController、BookGameDragInput、BookFlipCoordinateMapperのController参照を専用型へ接続する。依存を減らすためだけの大規模なインターフェース化は初回に行わない。
- hatake.BookFlipDragInput / BookFlipKeyboardActionsは元Controller型を参照するため、専用構成ではそのまま接続できない。ドラッグは既存BookGameDragInputを使い、L/I編集キーは接続しない。開閉等で必要な確認操作だけ担当側に用意する。
- 元Prefabを直接変更せず、担当用の独立Prefabと検証Sceneで配線する案。Prefab Variantは基底のController型が異なるため、初期移行では独立コピーを推奨。コピー時には新GUIDを用い、内部の旧Controller参照・実行時Live Rig複製への参照も点検する。本番BookSceneへの切替は変更内容を提示して相談する。
- 複製元は未コミット変更も含む現在の作業ツリーを基準にする。HEADだけを出自として記録せず、複製時点の元ファイルのハッシュと取り込んだ差分も記録する。元の修正を自動反映せず、必要な修正だけ比較して取り込む。複製にはこの維持コストがある。

### 専用版で必要な操作契約（仮称）
| 操作 | 入力 | 保証する内容 |
| --- | --- | --- |
| InsertGameSpread | ゲーム一覧の挿入境界、左右表示データ、編集ID | 2ページを一括挿入。先頭・末尾固定ページを保護。任意の末尾／現在の次に対応 |
| RemoveGameSpread | ゲーム一覧の対象位置、編集ID | 非表示位置も対象にできる。左右を一括削除 |
| ReplaceGameSpread | 削除対象位置、削除後一覧での挿入境界、新しい左右表示データ、編集ID | 満杯時の入れ替え全体を一操作として扱い、途中で容量を超えない |
| EditCompleted / EditCancelled / EditFailed | 編集ID、結果、確定後の表示位置 | 受付と完了を区別し、論理一覧を一度だけ更新できる |

- APIは表示を担当し、PageId解決・上限・所持個体の判断はゲーム管理側が担当する。原本のAddPage / RemovePageを外部から自由に呼ぶ構成にはしない。
- 全入力・参照・編集可否を先に検証し、変更予定の表示一覧と描画を準備する。演出成功後、ページ一覧と現在位置をyieldを挟まず確定し、EditCompleted→SpreadShownの順で通知する。ゲーム側はEditCompletedで一覧を確定してからSpreadShownで操作を再開する。
- 無効化などで確定前に中断したら旧一覧・旧表示を保持／復元し、取消結果を残す。通知先も無効化される場合に備え、再有効化時に最終編集結果を照会できるようにする。不可逆な副作用を演出開始時に実行しない。
- 非表示位置の編集は閲覧中のページ個体を維持する案。表示中の見開きが入れ替わる場合だけ入手演出を見せる。遠くのページを編集するために勝手にめくる処理は入れない。
- 原本の編集処理だけを抜き出すと撮影・表紙・Live Rig等の依存が残る。初回はControllerの動作を維持して複製し、任意位置編集と結果通知を加える。その後、必要性が明らかな部分だけ分割する。

### 上限時のページ選択画面
画面案（既存ゲーム画面に重ねるuGUI。実装技術を画面に表示しない）：

```text
ページがいっぱいです                        所持 8 / 8
新しく手に入れたページ：[画像] 名前・種類
手放すページを1つ選んでください

[所持1：画像・名前] [所持2：画像・名前] [所持3：画像・名前]
[所持4：画像・名前] [所持5：画像・名前] [所持6：画像・名前]
                         （件数に応じてスクロール）

選択中：[画像] 名前・種類・所持位置
[このページと入れ替える]              [今回は受け取らない]
```

- 同種ページは個別に表示し、所持順と選択枠で区別する。内部のPageIdや編集IDは画面に出さない。固定ページは一覧に含めない。
- 最初は未選択。選択するとプレビューと強調枠を表示し、確定ボタンを有効にする。カード選択だけでは削除しない。
- 「今回は受け取らない」は保留中の新規1件を辞退し、所持内容を維持する推奨案。逃げ道のない強制削除を避けるため採用を提案しているが、ユーザー確定仕様ではない。
- 選択中は本の通常入力・ルーン・発動を止める。演出中は二重確定を止め、完了後に画面を閉じる。新しい要求がある場合は次の要求を順に評価する。
- 確定時に対象PageInstanceがまだ存在することを再確認する。一覧のindexだけを保存して削除しない。選択中に上限や所持数が変わった場合は再評価し、必要のなくなった削除は実行しない。
- 画面は専用Canvasに置き、本の撮影用CanvasやページのLive Rigには入れない。撮影用RenderTextureへ選択UIが映り込まない構成にする。

### 閲覧位置と入れ替え位置の推奨ルール
- 通常追加：現在閲覧中のページを維持する。入手した名前を短く表示する。
- 満杯時：選択開始時の閲覧ページ個体をAFTER_CURRENTの基準として保存する。選択プレビュー操作で挿入先が変わらないようにする。
- 削除対象が基準ページ自身の場合は、元の基準位置へ新規ページを置く。基準より前を削除した場合は削除後の位置に補正する。ENDなら常に末尾固定ページの直前。
- 閲覧ページが削除対象なら新規ページを表示し、それ以外は元の閲覧ページを維持する。単独削除は次のゲーム見開き、なければ前、ゲーム見開きが0なら先頭固定見開きへ移る案。
- 上記は実装を具体化するための推奨値。お邪魔ページのアクション条件は未定のまま、将来RemoveGameSpreadを呼べる入口だけ分離しておく。

### 直近の実装順
1. 専用Controllerの複製と担当専用の検証Scene / Prefabを用意し、既存の開閉・めくり・ルーン連携を維持する。
2. 設定・所持一覧・任意位置編集・完了／中断通知を実装する。
3. 上限時の選択UIと一括入れ替え、受取辞退を接続する。
4. 通常追加、満杯、同種複数、連続入手、入れ替え中断を確認する。ゲームコード実装・Scene作成は今回未実施。
5. 結果と移行差分を提示し、本番Sceneへの配線変更を相談する。hatake原本が必要になった場合も変更前に相談する。

# Controller複製の出自（2026-10-21）

複製時HEADはc9ffca30221316fe8de7a36a84ea500346cda915、作業開始時はクリーン。以前の調査時に存在した変更はユーザー側で確定済みの状態だった。UTF-8以外の元コードはCP932で読み、新規コピーだけUTF-8へ変換。
- Assets/hatake/book/scripts/BookController.cs → Assets/YasudaHaruto/Scripts/Book/Validation/YasudaBookViewController.cs / 元SHA256: 6F10FB222E2CCAF422C2C71265927544FDA5DD6F8117DD1338180779E5C58FC7 / 元文字コード: UTF-8
- Assets/YasudaHaruto/Scripts/Book/BookGameController.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookGameController.cs / 元SHA256: 84D4EF19332F20E9B8331418988787170963A30EB4CD0FEA20B8D84F45C83815 / 元文字コード: CP932
- Assets/YasudaHaruto/Scripts/Book/BookGameBootstrap.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookGameBootstrap.cs / 元SHA256: 51E4D21701AEA505823ECDD401912BD49A1B3FDE1C2AB1ECF5CE17D15587620A / 元文字コード: CP932
- Assets/YasudaHaruto/Scripts/Book/Input/BookGameDragInput.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookGameDragInput.cs / 元SHA256: ABE1B7344410CEC9F52712B00B98018F444F1BEFE96353E32ADA4300962CAC38 / 元文字コード: CP932
- Assets/YasudaHaruto/Scripts/Book/Input/BookFlipCoordinateMapper.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookFlipCoordinateMapper.cs / 元SHA256: 6030D72C00E531DCBAD0511DF537980DD9A6C220B318B54022872B20C0FA93BA / 元文字コード: CP932
- Assets/YasudaHaruto/Scripts/Book/Input/BookPageAreaDetector.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookPageAreaDetector.cs / 元SHA256: 16580CBB6FC557F5314127508D90035CB464FD503D8903F4F511E66B4BBB5F27 / 元文字コード: CP932
- Assets/YasudaHaruto/Scripts/Book/Input/BookFlipCoordinateTest.cs → Assets/YasudaHaruto/Scripts/Book/Validation/BookFlipCoordinateTest.cs / 元SHA256: 3222691F8AE8F280A3F0358714664CF7CBC9557C90304A239F086161D18343D9 / 元文字コード: UTF-8

## 専用構成の作成結果（2026-10-21）
- 実体はAssets/YasudaHaruto/Scripts/Book/Validation。名前空間はYasudaHaruto.BookValidation。YasudaBookViewControllerはhatake版を複製し、型名・名前空間・usingだけを調整した。表示アルゴリズムは維持。
- BookGameController / BookGameBootstrap / BookGameDragInput / BookFlipCoordinateMapper / BookPageAreaDetector / BookFlipCoordinateTestも同じ名前空間へ複製。元Sceneの型参照を壊さないため、今回は担当側の元スクリプトも変更していない。恒久的な全コード二重管理を目的とせず、本番移行時に必要な整理範囲を判断する。
- BookValidation.prefabは検証に必要なカメラ・EventSystem・本のCanvas・起動処理を含む、Scene全体相当の検証用セット。本単体の汎用Prefabではない。別Sceneへ入れる場合はカメラ・EventSystemの重複に注意する。
- hatake.BookFlipView / BookPageView / BookModelView / データ・Material・Shaderを共有参照。元のhatake.BookFlipKeyboardActionsとBookFlipDragInputは専用コピー内で無効化し、旧Controller参照を解除。C/L/I等の旧キーボード操作は専用版では未接続。
- Unityメニュー Tools > YasudaHaruto > Verify Existing Book Validation で再確認可能。Sceneを再生成せず専用Sceneを開いてPlay Mode検証し、終了する。未保存Sceneがあれば処理を拒否する。Prepare and Verify Book Validationは初回専用で、既存Prefabを上書きしない。
- 検証PASS：専用Controller生成、初期RenderTexture、開く要求、通常見開きの選択、ルーンCanBeginStroke、末尾固定ページでCurrentPageなし、前へ戻ると操作復帰、閉じる要求、閉じると操作終了、再度開く要求、再開時に操作復帰（計11項目）。
- 実際のマウスなぞり・ドラッグや方向発動をこの検証で再現したわけではない。通信はユーザー確認済みのため再テストしていない。Build Settings・Packages・ProjectSettingsは変更していない。
- 初回コンパイル時のnamespaceとRequireComponent属性の配置誤りは修正済み。その後Unityが専用コードをコンパイルし、検証完了まで実行した。

## 所持一覧・追加削除の実装（2026-10-21）
- 対象はScripts/Book/Validationと専用Prefabのみ。hatake原本・既存BookScene・元のBookGame系は変更していない。
- BookInventorySettings.assetでMax Game Page CountとMax Pending Requestsを管理する。初期値は仮に8見開き／待機16件。Inspectorで変更し、次回開始時に反映する。固定ページは上限に含めない。設定未接続・初期所持数の上限超過は初期化を拒否する。
- BookGameController.Pagesは読取専用の個体一覧。RequestAdd(PageData)、Remove(PageInstance)、ReplacePending(PageInstance)、DeclinePending()が操作入口。満杯時はPAGE_ADD_RESULT.NEEDS_SELECTIONを返し、PendingPageを保持してReplacementRequestedを通知する。
- ENDとAFTER_CURRENTだけを受理する。共有PageDataのenumにあるRANDOMは既存Scene互換のため残したが、専用構成ではInvalidとして拒否する。旧アセットの指定変更は行っていない。
- 同じPageDataでもPageInstanceを都度生成する。所持していない古い個体による削除は拒否する。追加時は閲覧中の個体を維持し、その個体を入れ替えた場合だけ新しい個体を表示する。現在の次への入れ替えでは削除によるindex移動を補正する。
- 表示側TryApplyInventoryが変更後の一覧をコピーして別RenderTextureへ撮影し、その後に表示一覧と論理一覧をyieldなしで確定する。入れ替えは削除→追加の2操作ではなく、所持件数を保った一操作。演出を待つ処理は現段階では追加しておらず、表示は撮影完了時に切り替わる。
- 表示側を確定前に無効化した場合は未確定変更を取り消す。再有効化時に旧一覧を再撮影する。InventoryEditFinished(bool)は成功・取消を通知する。ゲーム側を無効化している間はInteractionを開始しない。
- 通常操作中・閉じた本・描画やチャージ中の受信はBootstrapの有界FIFOで待機する。満杯の保留1件を解決するまで次の要求は進めない。上限を超える待機要求は警告付きで拒否する。通信プロトコル・送受信パケットは変更せず、既存ADD_PAGEイベントの購読・解除を接続した。通信実行の再検証は行っていない。
- 選択中・編集処理中は専用BookGameDragInputからの新しい操作を止める。現在の選択入口はAPI／Editor検証から利用できるが、実ユーザー向けの選択画面はまだない。現段階の専用Sceneでは満杯になった場合、APIで入れ替えか辞退を呼ぶ必要がある。
- 次は専用Canvasへ所持ページ選択・確定・受取辞退のUIを接続する。お邪魔ページのアクション条件は仮定せず、将来Removeを呼べる構造だけ用意した。
- 最終Play Mode検証はCOMPLETEで終了。通常操作に加え、重複追加、上限到達、辞退、表示中／非表示個体の入れ替え、古い個体の拒否、中断取消し、全削除、空からの追加、END追加、RANDOM拒否を検証。各段階で件数・左右表示・固定ページを照合し、合計181回のassertionがPASSした（181種類の独立テストという意味ではない）。結果はBookValidationResults.txt。git diff --checkも問題なし。

## 簡易ページ選択UI（2026-10-21）
- ユーザー指定：画面案が未確定なので軽く分かりやすく、後から変更しやすくする。独立したBookPageReplacementUI.prefabを専用BookValidation.prefabへ接続した。既存BookSceneとhatake原本は変更していない。
- 構成：入手ページ名・種類、所持数／上限、スクロール可能な所持一覧、選択内容、「このページと入れ替える」「今回は受け取らない」。画像・演出・追加パッケージは使っていない。
- 所持一覧の行は同種でも1個体ずつ表示し、所持順の番号を付ける。未選択では確定不可。行選択だけでは削除せず、確定後は二重操作を止め、成功後に画面を閉じる。辞退では既存所持を維持する。
- UI/BookPageReplacementPanel.csは画面の状態とController呼出し、UI/BookPageChoiceRow.csは行表示のみを担当。実際の入れ替えは既存のBookGameControllerに集約。UIを差し替えても所持管理の変更は不要。
- レイアウト・文言・文字サイズ・色・余白はBookPageReplacementUI.prefab上の通常uGUIで編集できる。RowTemplateの色・Text・LayoutElementで一覧の見た目を変更する。コードでのUI生成はEditor初回作成だけで、既存Prefabがある場合は再生成しない。
- 選択UIは撮影用Canvasの外に独立したScreen Space Overlay Canvasを置き、背景を暗くして操作を遮る。ページのRenderTextureやLive Rigには含めない。
- 仮フォントはOSの日本語フォントを起動時に使用し、Windows Editorで日本語表示を確認。配布先のOSに依存しない表示にする場合は、PanelコンポーネントのFontへプロジェクトの日本語Fontを指定する。OSフォント自体はリポジトリへコピーしていない。
- Unityメニュー Tools > YasudaHaruto > Preview Page Selection で専用Sceneを起動し、所持を満杯にして選択画面を表示できる。プレビューはPlay Modeのまま停止するため、確認後はPlayを止める。実データや設定値は変更しない。
- 再有効化時は保留があれば表示し直す。Controllerから直接辞退した場合もReplacementDismissed通知で閉じる。イベントとボタンの購読は有効化／無効化に合わせて解除する。
- 最終UI検証はCOMPLETE。表示前の未描画フレームにRaycastを検証していた初回の失敗は、描画を待ってから検証するよう修正して解消した。実際のEventSystem.RaycastAllで最前面が所持行であること、行と確定／辞退ボタンのイベントから所持管理までつながることを確認した。OSの物理マウス入力の独立再現とは区別する。Play Modeは終了済み。

## 既存BookSceneへの組み込み準備（2026-10-21）
- ユーザーが選択UIの確認完了を報告し、次段階へ進むよう指示。UI確認は完了扱いとする。
- 元BookSceneを別名のBookIntegrationCandidate.unityとして複製し、7スクリプトを専用名前空間版へ差し替え、上限設定・PageDatabase・独立UI Prefabを接続した。元のカメラ・本・Canvas・オブジェクト配置をそのまま基準にし、検証Scene全体のPrefabで置き換える構成にはしない。
- 候補Sceneでは旧hatake.BookFlipDragInput / BookFlipKeyboardActionsを無効化して型の異なるController参照を解除。座標確認用BookFlipCoordinateTestも無効化。非アクティブの旧検証オブジェクトと既存コードファイルは削除していない。
- 生成用メニュー：Tools > YasudaHaruto > Prepare Integration Candidate。既存候補の上書きや未保存Sceneの破棄はしない。検証用メニュー：Verify Integration Candidate。
- 元BookSceneの生成前後SHA256は889C979BFEB26C0498336CDA0EAB28FAAE71426E514176A7D0704D0C21219E46で同一。hatake原本・TransportClientTest・Build Settings・Packagesは未変更。
- 本反映の予定範囲はAssets/YasudaHaruto/Scenes/BookScene.unityのみ。候補と同じ参照変更・UI追加・検証入力無効化を、元SceneのGUIDを維持して反映する。Assets/YasudaHaruto/Scenes/BookScene.unity.metaは変更しない。
- TransportClientTestは現在BookSceneを名前指定でロードするため、Scene名とGUIDを維持すれば接続コードやBuild Settingsの変更は不要。候補Scene自体はビルド一覧へ登録していない。既存の通信テストはユーザー確認済みで再要求しない。
- 既存Scene変更はAGENTS.mdの事前相談ルールに従い、検証結果と変更範囲を示してユーザーへ確認してから実施する。まだ反映していない。

## BookSceneへの反映（2026-10-21）
- ユーザーが「命名規則が元のコードと一致してれば反映して大丈夫」と承認。クラス・メソッド・プロパティのPascalCase、インスタンスフィールドm_、staticフィールドs_を照合した。
- 専用版の追加結果enumは既存のPAGE_TYPE等へ合わせ、PageAddResult.csにPAGE_ADD_RESULT（ACCEPTED / NEEDS_SELECTION / BUSY / INVALID）を定義。Bootstrapと検証コードの参照も統一した。複製版を元コードから区別する名前空間とYasudaBookViewControllerのクラス名は維持し、既存のSerializeField名は変更していない。
- 候補Sceneで確認した構成をBookScene.unityへ反映。専用Controller・入力・座標変換・起動処理へ参照を切り替え、上限設定とPageDatabase、独立選択UIを接続した。
- 旧hatakeのキーボード編集・ドラッグ入力はScene内で無効のまま保持し、型が異なるControllerへの参照を解除。座標ログ用の確認コンポーネントも無効化。非アクティブの旧BookTestBootstrapと旧AreaDetectorの接続は元の型・参照を保持した。既存コードやオブジェクトの削除は行っていない。
- BookScene.unity.metaは未変更。GUIDはc3904b8ed0c72474b9dc8ae4925a86c9。接続Sceneは引き続きBookSceneをロードする。TransportClientTest・EditorBuildSettings・元7スクリプトのハッシュも変わっていない。
- Tools > YasudaHaruto > Verify Integrated Book Sceneで反映後のBookSceneを検証できる。Apply Reviewed Book Integrationは元Sceneの既知ハッシュを確認する一回限りの移行補助で、反映済みSceneには再適用できない。
- Validationフォルダ／BookValidation名前空間は移行元の名称として残っているが、本番BookSceneはそこにある専用版を使用する。元のグローバル名前空間版は他Sceneとの互換性のため保持する。
- 最終版のBookSceneでも検証はCOMPLETE、Play Mode終了済み。既存の開閉・ページ移動・ルーン待ち状態、追加・個体指定削除・重複所持・上限・選択UI・入れ替え・辞退・中断取消し・全削除・再追加を確認。通信再テストと物理ポインターなぞりは含めていない。Unityが追加したUI参照の空value行の末尾空白のみ整形した（意味上の変更なし）。

## 不要コードの整理調査（2026-10-21）

今回は静的調査と記録のみ。削除、ゲームロジック変更、Scene・Prefabの再保存は行っていない。以下のパスはAssets/YasudaHaruto/基準。

### 最初の削除候補（Scene変更を伴わない3ファイル）
| 対象 | 根拠・注意 |
| --- | --- |
| Scripts/Page/DebugPageInteraction.cs | 型名の呼出し・GUID参照が見つからない旧デバッグ用IPageInteraction実装。 |
| Scripts/Network/TransportClientTest.cs | 型名の呼出し・GUID参照が見つからない旧通信サンプル。実際の接続はBookNetworkClientを使用。 |
| Scripts/Network/TransportServerTest.cs | 型名の呼出し・GUID参照が見つからない旧通信サンプル。実際のサーバーはBookNetworkServerを使用。 |

同名のScenes/TransportClientTest.unity、Scenes/TransportServerTest.unityは削除対象ではない。前者はBuild Settingsの起点、後者はユーザーのEditorサーバーテストに利用する。削除時は各.csに対応する.metaも一緒に扱う。

### 次の整理候補（依存する移行補助もまとめて調整）
- 旧グローバル版のScripts/Book/BookGameBootstrap.cs、BookGameController.cs、Input/BookGameDragInput.cs、Input/BookFlipCoordinateTest.csはScene・PrefabからのGUID参照なし。旧Bootstrapから旧Controller・旧Dragへの参照はあるため、一群として扱う。Validation配下の同名専用版は本番で使用中のため残す。
- Validation/Editor/BookIntegrationPreparation.csには上記旧ファイルのパスと候補生成・一回限りの反映処理が残る。旧4ファイルを削除する際は、この移行補助とBookValidationSetupの候補専用メニューも併せて整理する。
- Scenes/BookIntegrationCandidate.unityは本反映前の候補。AssetsのGUID参照はないが、Editorメニューに明示パス参照があるため、その処理と一緒に整理する。
- Prefabs/OldBook.prefabはGUID参照が見つからない。手動配置用の保管物かどうかは静的参照だけでは確定しないため、旧モデルを保存する意図がないことを確認してから削除する候補。

### 残すもの・事前相談が必要なもの
- Scripts/Book/Validation配下の専用Controller・入力・設定・選択UIは現在のBookSceneが使用する。フォルダ名だけで削除しない。
- Scenes/BookValidation.unity、Prefabs/BookValidation.prefab、Editorの統合済みScene検証・UI生成処理は回帰確認用として当面残す。検証結果txtも直近の確認証跡として残す。
- 旧BookTestBootstrap、BookDisplayController、BookInputRouter、BookNetworkTestUI、RuneTraceTestBootstrap（ファイル名RuneTestBootstrap.cs）、DebugRuneTraceViewはAssets/Hamada/Scene/BookSceneHamada.unityから参照される。担当フォルダ内でも無断削除しない。関連するBookController、BookModel等も依存関係があるため保留。
- 旧Input/BookFlipCoordinateMapper.csも旧BookPageAreaDetectorから型参照されるため、単独削除しない。BookSceneに残した非アクティブな旧構成は別段階で整理する。
- Input/BookInputState.csはBOOK_INPUT_STATE enumを定義し、専用版でも使用中。MonoBehaviourのGUID参照だけで不要と判断しない。
- Validation/BookFlipCoordinateTest.csは無効化済みでもScene・Prefabにコンポーネント参照がある。削除する場合は参照を先に除く必要がある。
- Magic/MagicSystem.csは全体がコメント化されているが、TransportServerTest.unityにGUIDが残る。サーバーSceneの古いコンポーネント整理と併せて別途確認する。
- hatake原本は整理対象外。共有PAGE_INSERT_TYPE.RANDOMも既存アセット互換のため残し、専用版で拒否する現在の実装を維持する。

### 推奨順序と検証
1. 上記3ファイルと各.metaを最初の小さな削除単位にする（ユーザー承認後）。
2. UnityコンパイルとVerify Integrated Book Sceneを確認する。通信コードの変更はなく、通信再テストは現時点では要求しない。
3. 旧4ファイル・候補Scene・一回限りの移行メニューをまとめて整理し、統合済みSceneの検証機能は残す。
4. OldBookの保管意図を確認する。他メンバーの参照を含む旧Book系は、影響範囲を相談してから整理する。

調査の限界：テキストとして保存された参照とC#記述を調べた結果であり、未保存のEditor状態・外部ツールからの利用までは保証しない。今回の調査だけを新たな実行確認とは扱わない。

## 承認済み3ファイルの削除（2026-10-21）
- ユーザー承認に基づきScripts/Page/DebugPageInteraction.cs、Scripts/Network/TransportClientTest.cs、Scripts/Network/TransportServerTest.csと各.metaを削除した。同名の通信Sceneは維持。
- 削除直前のGUID検索と削除後の型名・GUID検索で、AssetsのC#・Scene・Prefab・assetに残存参照がないことを確認。
- 旧Book系の削除も新構成への切り替え完了を条件に了承された。ただしAssets/Hamada/Scene/BookSceneHamada.unityでは旧BookTestBootstrapがm_IsActive: 1で、旧BookDisplayController等への接続も残る。Yasuda側の新BookSceneの動作確認はHamada側の移行完了を意味しないため、参照中の旧Book系とHamada Sceneは今回変更していない。
- 次の順序：Hamada側を新構成へ切り替え、動作確認後、Yasuda側の非アクティブな旧構成と検証Prefabの参照を整理してから旧コードを削除する。BOOK_INPUT_STATE等の共用型は残す。
- Unity 6000.3.23f1のバッチコンパイルを試みたが、プロジェクトパス確認直後にreturn code 1で終了し、コンパイル完了を確認できなかった。ログに具体的な原因の記載なし。Play Mode再検証は未実施。次回Editorでのコンパイル確認が必要。
- Gitのstatus・fetch・追跡先差分を確認し、コミット差分なし。禁止されたGit操作は行っていない。

## 旧Book構成の削除（2026-10-21）
- 現在のブランチはMS-73/Fix/DeleteOldBook。開始時クリーン、fetch後の追跡先とのコミット差分なし。前節のHamada側参照は過去の状態で、今回Assets全体を再調査するとBookSceneHamadaは存在しなかった。
- 削除した16スクリプト（すべてScripts/基準、各.metaも削除）：Book/BookTestBootstrap.cs、Book/BookController.cs、Book/BookModel.cs、Book/BookDisplayController.cs、Book/BookGameBootstrap.cs、Book/BookGameController.cs、Book/Input/BookInputRouter.cs、Book/Input/MouseBookPointerInput.cs、Book/Input/IBookPointerInput.cs、Book/Input/BookPointerData.cs、Book/Input/BookGameDragInput.cs、Book/Input/BookFlipCoordinateTest.cs、Book/Input/BookFlipCoordinateMapper.cs、Book/Input/BookPageAreaDetector.cs、Page/BookPageService.cs、Rune/Runtime/RuneTestBootstrap.cs。
- これらを参照するコンポーネントはBookScene内の6件、BookValidation.prefab内の5件を除去。階層を辿って非アクティブであることを確認した。コンポーネント一覧からも除き、残存するローカル参照はnull化。ゲームオブジェクトやTransformは配置維持のため残した。
- 移行済みのScenes/BookIntegrationCandidate.unityとValidation/Editor/BookIntegrationPreparation.csも各.metaと共に削除。BookValidationSetupのVerifyCandidateメニューを除去。統合済みBookSceneの検証・専用検証Scene・選択UIプレビューは維持。
- 現在使われるValidation配下の専用ランタイム、共用BookInputController・BOOK_INPUT_STATE・CastSwipeDetector・PageFactory等は維持。hatake・HamadaのコードとBuild Settingsは変更していない。
- Unityバッチコンパイル成功（終了コード0）。今回Play Modeは実施していない。前回のバッチ起動失敗とは別の最新結果。

## MagicDataの着弾ルーン画像設計（2026-10-21）

### 確定した要件と現在の構成
- ユーザー確定：着弾形状は「魔法ごとに決まったルーン画像、MagicDataに1枚」。実際になぞった軌跡を再現する仕様ではない。
- 現在PageDataはInteraction.RuneDataとAction.MagicDataを別に参照する。同一MagicDataに異なるRuneDataを使う既存ページがある。この構成は維持し、MagicDataから特定RuneDataへの参照を強制しない。
- RuneDataは編集・判定用の線とルールを管理する。本のRunePageViewもTraceDataの線を描画する。RuneDataには画像参照はない。RuneEditorWindowの取り込み画像はEditor側の作業用フィールド。
- HamadaのMagicBulletControllerはMagicData参照を既に保持する。HitSenderControllerも弾から同じMagicDataを取得するが、現在のHitDataへはMagicType等しか渡していない。着弾エフェクト本体はコメント段階。

### 推奨する保存場所とAPI（2026-10-28にフィールド・プロパティのみ実装済み）
- Assets/YasudaHaruto/Scripts/Magic/MagicData.csへ[SerializeField] private Texture2D m_impactRuneTextureと読取専用ImpactRuneTextureプロパティを追加する案。
- 魔法ごとのMagicDataアセット上で1回だけ画像を指定する。PNG等の実体はAssets/YasudaHaruto/Data/Runes/Images等に置き、参照として設定する。画像の実ファイルは複製しない。
- Texture2Dを推奨するのは、着弾用のMaterial・Particle・Decalへテクスチャとして渡しやすく、Sprite AtlasのUV切り出しに依存しないため。白い形状＋透明背景を基本にし、色や発光・拡大・フェードはエフェクト側で扱う。
- PageData、RuneData、弾Prefab、個別Materialへ同じ画像のInspector設定を追加しない。他の画面でも必要ならMagicData.ImpactRuneTextureを読み、RawImage等へ一時的に反映する。画像の唯一の設定元はMagicData。
- ルーンの判定用線データと着弾画像は用途の異なる情報。今の要件では別々に編集できる。ただし線を変えると画像も自動で変わる仕組みではない。将来一致を自動保証したい場合は、指定したRuneDataからEditorで画像を生成する機能を別途検討する。

### 着弾時の利用経路
1. MagicId受信 → 既存MagicDatabase.Find → MagicData。
2. 発射時に、そのMagicDataを弾へ参照として渡す。
3. 衝突・命中成立時に、MagicDataと命中位置・面の向きを汎用エフェクト表示処理へ渡す。
4. 表示側がImpactRuneTextureを読み、表示インスタンスのMaterialPropertyBlock等で設定して再生する。共有Materialのテクスチャを直接上書きしない。プール利用時は画像・表示状態を毎回更新する。
- 現在MagicBulletManagerはDisplayNameでPrefabを探し、弾はPrefab側のMagicDataを読む。接続時はMagicIdまたはアセット参照で対応を確認し、発射要求のMagicDataを確実に渡す案を相談する。現状の文字列照合のまま画像だけ追加すると取り違えの余地がある。
- 画像そのものを通信しない。クライアント／サーバーが同じMagicId→MagicData対応を持ち、既存MagicIdで解決する。受信側にも該当画像アセットが含まれる必要がある。
- 未設定時はルーン画像の演出だけを省略し、通常の命中処理は継続。設定漏れはEditor検証で検出する案。連続OnStayで何度も生成しないよう、命中イベントで1回再生する。
- Hamada側の弾・命中コードは今回未変更。実装時は他メンバー変更のルールに従い、上記の変更範囲を相談する。

### 実装の推奨順序
1. MagicDataに画像フィールドを追加し、対象MagicDataへ1枚ずつ設定。既存の魔法ID・ルーン判定・通信形式は維持する。
2. 担当フォルダ内に、MagicData・位置・向きを受け取る汎用ルーン着弾表示を作る。
3. Hamada側との接続範囲を相談し、発射時のMagicDataの引き渡しと命中イベントへ接続する。
4. 魔法A/Bの切り替え、同じ魔法を異なるルーンページから発動、画像未設定、連続命中・プール再利用を確認する。

追加確認：Assets・Packages・Library/PackageCacheのGUIDを照合し、BookSceneの73件、BookValidation.prefabの74件のスクリプト参照がすべて解決することを確認。git diff --checkも問題なし。

## ルーン描画フィードバックと失敗演出（2026-10-22）
- ユーザー要件：正確な線は緑、見本から離れるほど赤。失敗時は全画面に軽い黒幕を置き、ルーン領域へ少し大きな赤い×を表示。
- RuneTraceRealtimeEvaluatorにNormalizedDeviationを追加。従来の投影処理が求める見本からの距離をDistanceToleranceで正規化し、区間内の最大値を色へ渡す。許容距離・開始点・終了点・進行判定は変更していない。正確な位置は0、失敗距離は1。
- 任意のIRuneTraceFeedbackViewを経由してRunePageViewへ逸脱量を通知。RuneLineGraphicは点ごとの頂点色に対応し、既存の色なし呼出しも維持。描画済みの位置の色は残し、見本へ戻ると新しく描く部分は緑へ戻る。完成済みストロークの色も保持。
- RuneTraceControllerは無効な開始位置・描画中の逸脱を検出した時点で失敗演出を呼ぶ。途中で指を離した場合・発動方向入力失敗も対象。指を離す際の二重通知を抑制し、既存の再試行へ戻る。TraceFailedの通知も同じ失敗につき1回に統一。
- RuneFailureOverlayを実行時に独立したScreen Space Overlay Canvasとして作成。全画面の黒Imageと、その上の赤い×を表示する。黒幕は透明度0.35、表示0.7秒、末尾0.15秒でフェード。Time.unscaledDeltaTimeで進行し、表示中は新規UI入力を遮る。画面の解像度・ルーンの画面位置は表示中も再計算する。
- ×は初期状態では2本のuGUI Imageで構成するため外部画像アセット不要。RunePageViewのFailure Cross Spriteへ任意の画像を指定すれば差し替え可能。サイズはルーン領域の短辺を基準に1.15倍。黒幕濃度・色・×の色・大きさ・時間・線色・色変化カーブはRunePageViewのInspectorで調整できる。
- BookFlipCoordinateMapperにキャプチャ領域→左ページCrop→本の表示UV→画面の逆変換を追加。BootstrapからDragInput経由で投影関数を渡すため、既存Scene・Prefabの参照追加や再保存は不要。失敗表示をRenderTextureの中へ入れない。
- 再試行のBeginTraceでは直前の失敗演出を消さず、キャンセル・ページの終了・View無効化時には消す。View破棄時は生成したOverlayも破棄する。

### 今回の確認と既存変更の保持
- 開始時点で.vs、3つのTestPageアセット、BookScene、URPProjectSettingsに既存変更あり。Book_Test・PageView/Images・複数のPageView・ClearnRune/FireRune/WaterRune等も未追跡だった。これらを今回の実装で編集・破棄していない。
- git status・fetch・追跡先コミット差分確認を実施。ブランチはMS-73/Fix/DeleteOldBook、コミット差分なし。禁止されたGit操作は行っていない。
- 制限内のGet-Processでは既存Unityが検出できず、バッチ起動が終了コード1で失敗。標準エラーで同一プロジェクトのEditorが起動済みと判明後は追加起動を止め、computer-useスキルで既存Editorを使用した。バッチ起動失敗をコンパイルエラーとは扱わない。
- Tools > YasudaHaruto > Verify Rune Feedbackは最終版でCOMPLETE。距離0・許容距離の半分・許容超過、失敗即時通知、指を離した際の重複抑止、無効開始、途中終了、正確な完走による発動クールダウン、キャンセル、緑と赤の頂点色の保持、全画面の暗幕アンカー、再試行中の演出維持を検証した。初回の検証用リフレクションのオーバーロード指定不足は修正し、再実行で解消済み。
- BookSceneをPlay Modeにし、Preview Rune Failure (Play Mode)で開いた本に失敗演出を表示。黒幕がゲーム画面全体を覆い、赤い×が左ページのルーン領域に少し大きく重なることを目視確認。解除すると元の明るさへ戻った。Play Modeは終了済み。物理マウスで全ストロークをなぞる体感テストとは区別する。
- 上記PreviewはPlay Mode中に実行し、本を開いて演出表示を一時停止する確認用メニュー。表示直後に描画が未更新の場合はStepを1回進める。確認後はPlayを停止する。Sceneやアセットを保存する処理はない。
- BookSceneの作業前後SHA256は3F8690673527FE31ED5E7582A53D1886EA11409109A22A371F30569723DB6F44で同一。URPProjectSettingsもC317FE22B9488785C0611F0642429922E5D324430D929BFFB41648130F30C218で同一。
- 全体のgit diff --checkは既存BookScene.unityの2362行目の末尾空白を検出。ユーザーの既存Scene変更を保持するため触っていない。今回のスクリプトと管理資料だけを対象にしたチェックは別途実施。

## なぞり線全体の色同期へ変更（2026-10-22）
- ユーザーの指定により、描いた位置ごとに色を保存する方式を変更。現在の逸脱量で、完成済みの画を含むなぞり線全体を同じ色へ更新する。
- RunePageViewの点別色リストを取り除き、RuneLineGraphic.colorを更新する。危険になると全体が赤へ寄り、正確な位置へ戻ると描画済みの線も全体が緑へ戻る。画を終えても色を固定せず、次の画の逸脱量で全体を更新する。
- 見本線と失敗時の暗幕・×、既存の判定条件は維持する。
- Verify Rune Feedbackの描画検証は、全頂点が同じ色であること、完成済みの画と現在の画が一緒に赤くなること、追加描画なしでも全体が緑へ戻ることを確認する内容へ変更。
- 最新のVerify Rune FeedbackはCOMPLETE。完成済みと描画中の全頂点が同じ色になること、赤から緑へ戻ることを確認。今回Play Modeは起動せず、Sceneは未保存。既存の失敗演出の検証もPASS。

## 指離し後の中央下スワイプ発動（2026-10-22）
- ユーザー要件：ルーン完成後は一度指を離す。その後、本の中央より下の円だけ明るくして他を暗くし、円から3方向へスワイプする。円外で触っても反応させず、円を緩やかに拡縮して誘導する。
- RuneTraceControllerにWAITING_CAST_RELEASEとWAITING_CAST_STARTを追加（既存enum値を維持するため末尾に追加）。方向指定ありのルーン完了→指離し待ち→EndStrokeで開始待ち→TryBeginCastでCHARGING→既存ConfirmCast→クールダウンへ遷移。完了した指を離さず動かしても発動できない。方向指定なしのMAGIC_AIM_TYPE.NONEは従来通り自動発動。
- BookGameDragInputは方向待機中の通常PointerDown・ページめくり開始を拒否する。既存のルーン終端からのスワイプ判定を除き、最後のPointerUpはEndStrokeだけへ渡す。専用Overlayの円内PointerDownだけがTryBeginCastを呼ぶ。
- RuneCastOverlayはScreen Space Overlay Canvasで全画面入力を捕捉する。円外で押してから円内へドラッグしても開始しない。開始したポインターIDだけを受け付け、他の指や二重PointerUpを無視。開始後の指離し時に既存CastSwipeDetectorで距離・上方向・角度を判定する。短すぎる／方向範囲外なら従来の失敗表示とルーン再試行へ戻す。待機タイムアウトは設けない。
- RuneCastGuideGraphicは透明な円形部分を残して周囲を暗くするuGUIメッシュと、上／左上／右上の矢印を描く。円の縁を金色にし、ゆっくり拡縮させる。スワイプ中に方向が有効になった矢印は緑になる。判定角度はCastSwipeDetectorの設定から取得し、ガイドと判定がずれないようにした。
- 初期設定はBookGameDragInput InspectorのCast Guide：Cast Origin=(0.5,0.25)、Cast Radius=本の高さの0.08、Cast Shade=0.55、Cast Pulse Speed=1Hz、Cast Pulse Amount=0.08。Originは本の左下を(0,0)、右上を(1,1)とした値。表示出力UVと本の座標の変換を使い、実際の本の位置・サイズへ追従する。円の入力半径は一定で、脈動で判定が揺れないようにした。
- ページ操作・入手ページ処理は既存の入力可能条件でも方向待ち状態を受理しないため、待機中の所持変更を進めない。外部からページ終了・キャンセルが起きた場合や入力コンポーネントの無効化時はガイドを消す。フォーカスを失った場合は保持中のスワイプだけ解除し、円内でやり直せる状態へ戻す。生成したOverlayは入力コンポーネント破棄時に破棄する。
- 担当側の専用入力、RuneTraceController、CastSwipeDetector、検証メニューと新規2スクリプトのみ変更。hatake・他メンバーのコード、Scene・Prefab・既存アセット・通信形式は変更していない。BookSceneのSHA256は3F8690673527FE31ED5E7582A53D1886EA11409109A22A371F30569723DB6F44を維持。
- Verify Rune FeedbackはCOMPLETE。元の線色・失敗表示の検証に加えて、ルーン完了後の指離し要求、指離し前の発動拒否、円外PointerDownからの侵入拒否、LEFT/CENTER/RIGHTの発動、二重PointerUp防止、短いスワイプ失敗、フォーカス相当の復帰、キャンセルを検証した。
- BookSceneのPlay Modeでは、確認メニューで完了状態を模擬して円・3方向矢印・暗転・円の拡縮を目視確認。円外のクリックでは待機を維持、円内タップでは既存の短いスワイプ失敗処理へ届くことを確認。OS操作によるドラッグ発動は再現できておらず、3方向発動はイベント／Controllerの自動検証による確認。実際のルーンをなぞるところからの一連の操作確認とは区別する。Play Modeは終了済み。
- Tools > YasudaHaruto > Preview Cast Guide (Play Mode)は本を開いたPlay Modeで実行する確認用。実行時のControllerを完成後の状態へ変更するが、Scene・アセットへ保存しない。確認後はPlayを停止する。
- Git status・fetch・追跡先コミット差分を確認。HEADと追跡先の差分なし。fetchで別メンバーの新リモートブランチを取得したが、ローカルブランチ操作や同期はしていない。

## ルーン完成時の拡大フェードと矢印の太さ（2026-10-22）
- ユーザー確認済み：画像は「今なぞったルーンの形」をそのまま使う。拡大しながら薄くなって消える演出とする。
- 最後のストロークがリアルタイム判定で完了し、View.EndStrokeで全線が揃った時点にIRuneTraceFeedbackView.PlayCompletionを一度だけ通知する。指を離す前に再生するが、既存の指離し待ち・中央下スワイプ・自動発動の状態遷移や成功条件は変更しない。ルーン途中の1画完了や、完成後のPointerUpでは再生し直さない。
- RunePageViewにCompletion Duration（初期0.65秒）とCompletion End Scale（初期1.4倍）を追加。完成済みプレイヤーストロークをコピーし、現在の線色で独立したScreen Space Overlayへ重ねる。PNG・Sprite・MagicDataへの画像登録は不要で、元の線は残る。
- RuneCompletionOverlayはコピーした線の外接範囲の中心を軸にSmoothStepで拡大し、同時にalphaを0へ下げる。実時間で進行する。入力Raycastを無効とし、GraphicRaycasterも付けないため、完成後の指離しと発動ガイドを遮らない。ガイドより手前、失敗の暗幕・×より奥に表示する。
- 演出の開始時に入力点をコピーするので、元のストローク一覧の更新に影響されない。元のRuneLineGraphicの実際のRectを既存のMapperで画面へ投影し、AspectRatioFitterによる領域変更に対応。線幅も元の表示サイズに合わせる。画面位置は演出中も追従する。
- 再試行・失敗・キャンセル・View無効化で演出を消し、View破棄時は生成Overlayも破棄する。専用画像や新しいMaterialを持たせる二重管理は追加していない。
- 3方向の矢印の幅は円半径に対して0.065から0.1へ変更（約1.54倍）。BookGameDragInputのCast Arrow Widthで0.02〜0.2へ調整可能。矢印の角度・長さ・判定条件は維持。
- Verify Rune Feedbackは最終版でCOMPLETE。ルーン途中では再生しない、最後の画で一度だけ再生、指離しで再生し直さない、拡大途中でscale>1かつalpha低下、最後にalpha=0、入力を遮らない、失敗時に残らないことを検証。従来の線全体の色変化・失敗・3方向発動の検証も通過。
- Tools > YasudaHaruto > Preview Rune Completion (Play Mode)は、開いているルーンの見本線を入力済み線として模擬し、演出の途中を確認するメニュー。表示を一時停止するため、Stepを1回進めて確認後はPlayを止める。実際のなぞり操作の代替証拠とはしない。
- 今回のBookScene上の目視確認は上記プレビューによるもの。重なったルーンが拡大し、薄くなって消え、元の線が残ることを確認した。Play Modeは終了済み。
- 開始時からユーザーによる未保存のScene変更があったため、Sceneの再保存・再読み込みはせずに検証した。既存のReferenceRuneGraphic等のInspector設定は触っていない。BookSceneの保存済みファイルSHA256は3F8690673527FE31ED5E7582A53D1886EA11409109A22A371F30569723DB6F44を維持。
- Git status・fetch・追跡先とのコミット差分を確認、差分なし。禁止Git操作なし。今回のスクリプトを対象にgit diff --checkを実施し、問題なし。

## テスト補助コードの整理（2026-10-22）
- ユーザー指示に基づき、Assets/YasudaHaruto内のテスト・デバッグ・Editor補助のC#参照とAssets全体のGUID参照を再確認。
- 削除：BookReplacementUIBuilder.csと.meta。すでにBookPageReplacementUI.prefabが作成・調整済みのため、初回のUI自動生成処理は役目を終えた。
- BookValidationSetupからPrepare and Verify Book Validation、Preview Page Selection、ConfigureInventoryを削除。検証前に設定assetを作成したり、検証Prefabを書き換えて保存する処理を除去。検証は既存の設定・Prefabをそのまま使う。
- RuneFeedbackValidationからPreview Rune Completion / Preview Rune Failure / Preview Cast Guideと、それら専用の期限・EditorApplication.update処理を削除。内部状態のリフレクション書き換えや一時停止を伴う演出確認用の補助は残さない。
- 残すEditorメニューはVerify Existing Book Validation、Verify Integrated Book Scene、Verify Rune Feedbackの3つ。既存動作を確かめる回帰検証コードは削除していない。
- 過去のプレビュー専用分岐・SessionState・別レポート選択も削除。BookValidationPreview.txt等の過去レポートは確認証跡として保持する。管理資料の過去記録にある生成・Previewメニューは現在は利用できない。
- 参照が残るため保持した旧デバッグコード：BookRaycastTest、Validation/BookFlipCoordinateTest、RuneRealtimeEvaluationTest、DebugRuneTraceView、BookNetworkTestUI。いずれもBookScene.unityとBookValidation.prefabにGUID参照がある。コードだけを消すとMissing Scriptになる。BookNetworkTestUIにはボタンから呼ぶ公開メソッドもあり、コンポーネントだけでなくUnityEventも整理する必要がある。
- 現在のUnityにはユーザーの未保存Scene変更があるため、今回そのSceneの再読み込み・保存・ディスク上の配線変更はしていない。旧デバッグ5件は、未保存変更を保持した状態でScene・Prefabの参照を外す工程と一緒に扱う。現行ランタイムを含むValidationフォルダの一括削除もしない。
- 削除したクラス・メニューへのC#残存参照なし。既存Unityでコンパイル後、Verify Rune FeedbackはCOMPLETE。Toolsメニューが検証3項目になったことも確認した。統合済みSceneの検証は未保存Sceneを開き直すため、今回は実行していない。
- BookSceneの保存済みファイルSHA256は3F8690673527FE31ED5E7582A53D1886EA11409109A22A371F30569723DB6F44で同一。実行時コード・Scene・Prefab・他メンバーコードは今回未変更。
- Git status・fetch・追跡先差分確認済み、追跡先とコミット差分なし。禁止Git操作なし。新しいDevelop等のリモート追跡情報だけ取得した。

## 太いなぞり線の描画改善（2026-10-22）
- 原因：入力点間ごとに独立した四角形を描くため、太線で継ぎ目の隙間・重なりと角張った端が目立っていた。
- 隣り合う線分の断面を共有し、角の継ぎ目を接続。鋭い角の突出を制限し、ほぼ180度の折り返しは区切って丸い端でつなぐ。同じ座標の連続点は描画時のみ読み飛ばす。
- 線の両端を半円にし、輪郭に約1出力ピクセル分の透明な帯を追加。RuneLineGraphicのEdge Softness（初期1、0.25〜3）で調整可能。既存Line Widthと参照は維持。共通描画なので見本線と完成時のコピーにも適用される。
- RuneData・入力点・距離の成功失敗判定を変更しない。線色はRGBを全体同期し、輪郭だけalphaを0へ下げる。追加の画像・Materialは不要。
- 既存Verify Rune Feedbackの色検証をRGB同期と透明な輪郭の検証に更新。幅28の線で重複点・急角度・折り返しを含むメッシュの有限性と入力点の保持も確認。Unityのコンパイル後にCOMPLETEを確認。
- 今回はPlay Modeで実際になぞる目視確認は未実施。ユーザーの未保存Scene状態を保持し、他メンバーのコード・Scene・Prefab・ProjectSettingsは編集していない。

## ルーン画像取り込みの改善（2026-10-27）
- ユーザー指定の基準画像：Data/Runes/Images/Runa_Fire.png、Runa_Water.png、Runa_Clean.png。いずれも512×512、alpha範囲0〜255。色付きルーンを主対象とし、交差は直進方向をつないで「＋」を2画にする方針で承認済み。
- RuneImagePreprocessor：透明画素を含む画像はRGBの明るさではなくalphaで二値化。Thresholdを上げるほど薄い部分も含む（alpha > 1 - Threshold）。完全不透明画像は従来の明るさ判定を維持。画像アセットのImport Settingsは変更しない。
- RuneStrokeExtractor：直交接続がある場所の斜めショートカットを除き、実際の接続数で経路を分割。分岐点では短い接線方向が向かい合う経路（dot < -0.85）を優先結合する。離れた線同士は結合しない。端点のない閉じた輪も第二走査で抽出。画順・開始方向は画像から確定できないため、既存の手動編集を残す。
- RuneEditorWindow：同じSkeletonの二重抽出を解消。Min Stroke Lengthを追加（初期0.01、範囲0〜0.05、画像の0〜1座標の経路長）。必要な短線が消える場合は0へ下げられる。Simplify Toleranceは従来通り点数の調整。
- 単独C#検証：Unityの数値型等の代替を使い、実際の変更ソースを実行。「＋」が2画、閉ループが1画、単純化後も輪が残る、透明な黒を除外して不透明な黄色を取り込む検証がPASS。
- 同じ単独環境でThreshold=0.3、Simplify Tolerance=0.002、Min Stroke Length=0.01：Fireは抽出7→採用7画/140点、Waterは9→8画/102点、Cleanは16→11画/110点。処理は各約0.08〜0.12秒だったが、Unity Editorの実測値ではない。形状の正しさ・筆順の保証ではない。
- HEADの変更前ソースも同じ画像・Thresholdで比較：FireとCleanは抽出0画、Waterは133画。旧コードの色判定では入力形状自体が異なるため、この比較から速度向上率は算出しない。旧処理には短線フィルターがない。
- Unity 6000.3.23f1のManaged DLLと既存Assembly-CSharp.dllを参照し、ルーンEditorフォルダの全C#を一時net9.0プロジェクトでコンパイル。警告0・エラー0。Unity自身のインポート・ドメインリロード・Editor操作の確認とは区別する。
- 変更は担当内Editorスクリプト3件と管理資料のみ。各.metaとCP932文字コードを維持。Scene・Prefab・RuneData・hatake原本・通信・プレイ中のなぞり判定は変更していない。EditorでAnalyze Image→プレビュー→必要なら短線除去調整→Apply→Bakeの確認は未実施。

## ルーン編集UIの追加と確認（2026-10-27）
- RuneEditorWindowへDraw / Select・Deleteの切替、キャンバスの線選択と水色強調、選択削除・行ごとのDelete・Deleteキー、元画像の下書き表示と不透明度調整を追加。適用後は解析プレビューを隠し、元画像と編集済み線を見分けられるようにした。Undo/Redo・モード変更・フォーカス喪失時の描画キャンセルも接続。
- 手描き追加と最後の線削除でもTraceDataを無効にし、編集後のBakeを必須にした。既存の結合・分割・反転・並べ替えを維持。
- Verify Rune Editingを追加。一時RuneDataで選択・削除・Undo/Redo・描画追加・座標・キャンセル等14項目がUnityバッチ検証でPASS。既存RuneDataは変更しない。前回のUI操作でUnity全体を閉じてしまった事実と、その後プロジェクトを開き直したことをユーザーへ報告済み。未保存のウィンドウ状態の復元は未確認。
- 後続のユーザー回答：「オッケーめっちゃよくなった」。編集機能のユーザー確認済みとして記録。

## クールダウンの円形ワイプ表示（2026-10-27）
- ユーザー指定：発動後は書いたルーンを消し、時計回りに消えていく円形タイマーへ変更。
- IRuneCooldownViewを任意の表示通知先として追加。RuneTraceControllerのクールダウン開始時に残量1、既存Tickで残り時間の割合を通知する。入力待機・成功条件・発動タイミング・通信・待ち時間・Time.deltaTimeは維持。方向なし魔法の指離し待ち中も従来通り時間を進めない。
- RunePageViewはクールダウン時に見本と描画済み線、完成時のコピー演出を消す。playerGraphicのRect内にRuneCooldownGraphicを実行時生成し、本の撮影Canvas内で表示する。終了後のBeginTraceでリングを隠し、見本を戻す。Cancel/Clear・OnDisableでも隠す。
- RuneCooldownGraphicは画像不要のuGUIリング。12時から時計回りに欠ける。初期色は金色、Cooldown Diameter=0.28（描画領域の短辺に対する直径割合）、内径/外径比0.76。色と直径はRunePageViewのInspectorで調整可能。raycastTarget=falseで入力を遮らない。生成物はViewの子として破棄される。
- 既存Unityを開いたままコンパイルし、Tools > YasudaHaruto > Verify Rune Feedbackを実行。最終結果はRuneFeedback COMPLETE。開始時の全円、指離し待機中の停止、残量0.75への同期、終了時の入力復帰、描画線の消去、1/4経過時の右上1/4消去、残量0の空メッシュ、再開・キャンセル・無効化通知での非表示を追加検証。無効化はEdit Mode検証なのでOnDisableを明示呼出ししており、Play Modeの物理操作による検証とは区別する。
- 初回の検証コードではOnPopulateMeshの反射呼出しが継承先の同名メソッドと衝突し失敗。DeclaredOnly指定で修正。Edit Modeでenabled変更時にランタイムOnDisableが走るとの仮定も修正し、最終検証は従来の線色・失敗・完成演出・3方向発動を含めて通過した。
- Scene・Prefab・既存RuneData・他メンバー領域・共有設定は未変更。開始時からあったClearnRune/FireRune/WaterRune.assetのユーザー変更を保持。既存Unityの再起動・Scene保存・Play Mode起動は今回していない。実際の発動時の見た目・サイズは未確認。git diff --checkは問題なし。

## 通信復旧・USB接続の調査（2026-10-27）
- AGENTS.md、PROJECT_STATUS.md全文、BOOK_INTERACTION_DESIGN.mdと現行通信コードを確認。新しい調査・段階的設計案はNETWORK_RECOVERY_DESIGN.mdを参照する。
- ユーザー回答：WindowsゲームPC＋Androidタブレット、Unity Transportを使用。現在はWi-Fiと思われるが通信経路は未確定。
- 自動再試行なし、本Scene遷移後の再接続UIなし、登録承認なし、古いプレイヤー枠が再登録を拒否し得る点を確認。実機で切断原因を再現したわけではない。
- 推奨順：現行UDPで同じ接続先へ自動再接続→登録承認・セッション復旧→接続先自動発見。USBはテザリングによるIP通信が候補。adbの標準forward/reverseを現行UDPへそのまま使わない。
- 今回は調査と設計資料のみ更新し、ゲームコード・Scene・Prefab・共有設定は未変更。USB対応・抜き差し・複数台動作は実機未確認。次は第1段階の再接続処理を実装し、登録承認と切断中操作の扱いを順に詰める。
- 開始時の既存未コミット・未追跡変更を保持。親Gitアクセスは通常権限で失敗したが、承認付きでstatus・fetch・追跡先比較を完了。HEADと追跡先は0/0、追跡先とのファイル差分なし。禁止されたGit操作は未実施。

## 自動再接続の第1段階（2026-10-27）
- ユーザーは切断中にページめくりだけ可能、接続成功まで継続、途中のルーン／発動待ちは取消、開始済みクールダウン・所持・閲覧位置は維持する方針を承認。
- BookNetworkClientで接続先とプレイヤー枠を保持し、1→2→4→8秒（以後8秒）の待機で再試行。接続タイムアウトは従来の5秒設定。待機とタイムアウトはunscaled time。明示Disconnectと破棄で停止。無効なプレイヤー枠も拒否する。
- BookGameBootstrapから通信状態を入力へ通知。RuneTraceControllerで描画・発動開始・確定を止め、途中のルーンをリセット。入力中のポインターと占有を解除し、ページ操作を残す。開始済みクールダウンは切断で失わず、RuneTraceInteractionがページを離れた間の経過も保持する。
- BookConnectionStatusViewを実行時生成し、再接続中／未接続を表示。入力を遮らず、接続で消える。Scene・Prefabの参照追加は不要。OS日本語フォント方式で、Androidの表示は未確認。
- 既存Unityが別権限のプロセスとして起動していたため、通常権限のプロセス確認では見えなかった。バッチ起動直前の承認付き再確認で検出し、別Unityは起動せず、既存Editorのメニューから検証した。Editorの再起動・Scene保存・再読み込み操作はしていない（Play Mode終了時のUnity自身による復元は発生）。
- Verify Network Recoveryを追加。最終実行でNetworkRecovery COMPLETE、従来のRuneFeedbackもCOMPLETE。ローカルUDPの後起動・切断・再接続・同じPLAYER2登録・Scene維持・明示停止、timeScale=0、描画取消・別ページからの入力再開防止・クールダウン保持、表示とRaycast非干渉を検証。結果抜粋はNetworkRecoveryResults.txt。
- バックオフの最初の1秒は実時間で測定。2/4/8秒と上限は切断ハンドラーを明示呼出しし、次の試行時刻を照合した。実機Wi-Fi断や本の物理ドラッグの実証とは区別する。Play Modeは終了済み。
- サーバー・メッセージ形式は変更せず、登録承認と古い枠の即時引継ぎは次段階。Transportが切断を検出するまでの時間は既存設定のまま。IP変更への自動追従、Android実機確認、USBは未実施。
- 既存変更を保持し、既存CP932/UTF-8文字コードと.metaを維持。Scene・Prefab・hatake・他メンバー領域・共有設定は未変更。Git fetchと追跡先比較は0/0、禁止Git操作なし。

## ページ編集・文字・紙質の調査（2026-10-28）
- PAGE_AUTHORING_REVIEW.mdを追加。表示用BookDataと管理用Initial Pagesの重複設定が必要な現行コードを確認。実行時の追加削除は管理側から表示一覧を生成済みのため、初期化も同じ方向へ統一する案を推奨。
- 既存UnityのPageView_05.assetではImagesのPosition/Size欄を確認。画像の生成物は表示ごとに破棄・再配置され、asset編集の即時プレビューは未実装。入力不能の再現・保存反映テストはしていない。
- 文字は同じ撮影CanvasにTMPを追加可能な構成だが、現ページデータ／表示器に文字用機能なし。ユーザー追加のANYAfont SDFはStaticで190文字、漢字0文字。実際のページへの組み込み・Android表示は未確認。
- 紙質は共有BookFlip.matとBookFlipComposite.shader / BookFlipLook.cgincのプロシージャル紙粒子・凹凸・にじみ。PaperGrain=0.08、PaperBump=0.22、InkBleed=0.45。画像ごとの設定ではなくページ全体に適用される。
- 今回は資料のみ更新。hatake原本・Scene・Prefab・Material・フォント・既存の未追跡画像を変更しない。Unityは未保存BookSceneのまま、再起動・保存・再読み込み・Play Modeを実行していない。

## 色付きルーンのクールダウン演出（2026-10-28）
- ユーザー確認済み：対象はなぞり線ではなくページの色付きルーン画像。発動時にふわっと光り、残像が少し上へ抜けて消え、元画像が暗くなる。その後、下から元の明るさへ回復し、全体が明るくなると再使用可能。従来の円形タイマーを置換する。
- RuneCooldownImageFeedbackとResources/RuneCooldownImage.shaderを追加。既存Imageへ実行時の専用Materialを適用し、色付き画像のalpha形状を保ってRGBを暗くする。高さはImageのローカルRectから取得するので画像UVやアトラス内の位置に依存しない。回復境界は高さの約3%でなじませる。
- 放出時はSmoothStepで0.4秒かけて暗くし、途中で白寄りの光を加える。同じ既存Spriteの残像を一時生成し、画像高さの0.12倍だけ上昇してフェード。Raycastは無効、同じ撮影Canvas・Layerに載せ、既存の紙質・めくり表示を通す。
- RunePageViewのCooldown FeedbackでCooldown Brightness（初期0.28、暗い部分の明るさ）、Cooldown Release Duration（初期0.4秒）、Cooldown Release Rise（初期0.12、高さ比）を調整可能。
- PageInteractionData.Rune Image Indexは左表示ページのImagesの0始まりindex。初期値-1は末尾画像。現在のPageView_07 / 09 / 11は全て末尾が色付きルーンであることを確認。将来画像を後ろへ追加した場合はindexを明示指定する。Sprite自体はBookPageDataの既存設定だけを使い、RuneDataやMagicDataへ二重登録しない。
- 担当側YasudaBookViewControllerが現在の左ページの画像データと生成ImageのSprite／位置／サイズを照合し、BookGameController→BootstrapのコールバックでRunePageViewへ渡す。hatake原本・共有BookPageView・既存ページassetは変更しない。対象画像がない場合でもクールダウン計時は継続する。
- 新規発動時だけ任意のIRuneCooldownReleaseViewで放出演出を通知。ページ再入場のResumeCooldownは残量に対応した暗さを直接表示し、放出を繰り返さない。元の計時、指離し待機、所持ごとのクールダウン保持、切断中の扱いは維持する。
- 終了・Clear・キャンセル・無効化・破棄で元のMaterial参照へ戻し、残像と専用Materialを解放する。元画像を削除／置換せず、なぞり線と完成コピーは従来のクールダウン開始時と同様に消す。
- 旧RuneCooldownGraphic.csと.metaはC#・Scene・Prefab・assetに参照がないことを確認し削除。円形タイマーは生成しない。
- Verify Rune Feedbackを更新しCOMPLETE。緑赤・失敗・完成・3方向発動等の既存検証に加え、暗転前の連続性、光、残像上昇、Raycast非干渉、回復率同期、元Material復元、復帰時の放出抑止、画像なし、3ページの生成Imageとの結び付けを検証。GPUで白い画像の下半分が明るく上半分が暗いことを読み戻して確認。
- Temp/RuneCooldownPreview.pngは現在のシェーダーとRuna_Clean画像による5段階の比較。左から通常／放出中の光／暗い状態／半分回復／回復完了。画像を目視確認した。本の紙質を含む最終合成や物理スワイプによる一連の操作の実証とは区別する。
- 初回の検証コードは既存echo変数と名前が衝突しコンパイル失敗。releaseEchoへ変更し、最終版でコンパイル・回帰検証を通過。既存Unityを使い、追加Editorの起動・Scene保存・再読み込み・Play Mode実行はしていない。
- 保存済みBookSceneのSHA256は12F754CE7DB72C81ADF1C6DFD37E1C660050049C278AE2573941708EA3D1302Eで作業前後同一。開始時のPageView_10・Scene・number.png.meta変更と未追跡フォント・画像を保持。Git status・fetch・追跡先コミット比較で差分なし、禁止Git操作なし。

## クールダウン回復ペースの調整（2026-10-28）
- ユーザー報告：下側の回復が遅く、終盤の上側が一気に進む。全体が平均して上がるよう調整する。
- 調査：残量から高さへの変換自体は直線だったが、1秒のクールダウンに0.4秒の放出・暗転が重なっていた。前半の回復が光に覆われる要因がある。報告された実機症状を自動操作で再現したものではない。
- IRuneCooldownReleaseViewで全クールダウン秒数を受け取り、暗転・残像時間をmin(Cooldown Release Duration, 全クールダウンの10%)に制限。1秒なら0.1秒。低い秒数にも対応するため描画側の最小放出時間を0.05から0.001秒へ変更。実際の使用可能になる時刻は変えない。
- シェーダーは未回復部分の暗転・光を先に計算し、回復した部分は最後に元色へ戻す。回復済み領域へ放出光が重なって白くなることを抑える。回復高さは1-残量の直線のままで、加速・減速カーブは追加しない。
- Verify Rune Feedbackは最終版でCOMPLETE。32行のGPU描画を読み戻し、25%・50%・75%で明るい行が8・16・24行（許容1行）になることを確認。放出中の半分回復でも、下の元色(R=0.2)と上の放出光(R約0.831)が分離することを確認。既存の終了・復帰・入力・ページ画像参照等も通過。
- 途中の追加検証は光が全領域へ重なる状態を検出して失敗し、シェーダーの合成順修正後に通過。既存Editorで明示的にRefreshして検証した。Scene保存・再読み込み・Play Mode起動・他メンバーコード変更はしていない。
- 保存済みBookSceneのSHA256は12F754CE7DB72C81ADF1C6DFD37E1C660050049C278AE2573941708EA3D1302Eを維持。現在の未保存Scene変更も保持。Git status・fetch・追跡先比較を実施しコミット差分なし。禁止Git操作なし。

## UI座標の混在による回復高さの修正（2026-10-28）
- ユーザー再報告：画像の約7割から上が急に明るくなる。画像ごとの開始差と透明部分の扱い、四角い描画エリア全体を使う案を質問。
- 従来の計算は1-残り時間比を回復率とし、画素の画像内高さと比較して下を元色・上を暗色にする方式。alphaは変更せず、ImageのRect全体（透明な余白込み）を基準にしていた。余白の量による見え始めの差はある。
- 問題点：シェーダー入力v.vertexはuGUIのバッチ化でCanvas側へ変換され得るのに、_RectYは元のImageローカルRectのままだった。異なる座標空間を混ぜたため、配置によって画像内高さの範囲がずれる。前回の単独GL四角形テストでは実際のCanvas配置を再現できず、この問題を検出できていなかった。放出演出だけの調整では未解消とのユーザー報告を優先する。
- RuneCooldownImageCoordinates : BaseMeshEffectで、UIのバッチ化前に各頂点の画像内高さ0〜1をuv1.xへ格納。撮影Canvasに実行時のみTexCoord1チャネルを追加し、シェーダーはその値を補間して使う。画像の配置・拡大・pivotに依存しない。_RectYプロパティと実行時設定を除去。alpha余白を切り詰める処理は追加していない。
- 座標用コンポーネントはクールダウン中の既存Imageに一時追加し、Hide時に無効化・破棄。Scene・Prefabへ保存しない。CanvasのTexCoord1は実行時Canvasに残すが、共有asset・Sceneを書き換えない。残像はRecovery=0として放出光を保持する。
- 新規の実Canvas検証：Editorの専用PreviewSceneでWorld Space Canvas、pivot(0,1)、位置(-40,90)、サイズ(80,120)、scale(1.2,0.8)のImageを撮影。256×256のRenderTextureを読み戻し、回復率0.25/0.5/0.75に対し明るい高さの実測比が0.25/0.5/0.75になることを確認。75%でも上25%は暗いまま。終了後にPreviewSceneと生成物を破棄し、既存Sceneは保存・再読み込みしない。
- 既存のGL単独テストもUV1を明示して更新。Verify Rune Feedbackは最終版でCOMPLETE、画像参照・復元・放出・既存入力等の検証も通過。実際のゲーム操作とAndroid実機での確認とは区別する。
- ユーザー提案の四角い画像方式は、ルーン描画エリア全体へ半透明の暗幕を重ね、下から消す形なら可能。透明画像だけでは下のルーンの明るさは変わらない。背景・余白にも暗幕がかかる別の見た目になるため、今回は座標修正を先に実施しており、その方式への切替はしていない。
- 保存済みBookSceneのSHA256は12F754CE7DB72C81ADF1C6DFD37E1C660050049C278AE2573941708EA3D1302Eを維持。現在の未保存Scene変更と既存ユーザー変更を保持。Git status/fetch/追跡先コミット比較で差分なし。禁止Git操作なし。

## お邪魔アクション設計の現状調査（2026-10-28）
- AGENTS.md、PROJECT_STATUS.md、BOOK_INTERACTION_DESIGN.mdと現行のページ・入力・通信・表示コードを静的確認。
- PageInstanceはPageDataとIPageInteractionを保持するが、お邪魔状態・所持個体の通信IDはない。PageDataは左右の見開き単位、PageIdは種類ID。PAGE_TYPE.OBSTACLEや既存Noise除去は、今回の既存ページへの状態付与と区別する。
- BookGameDragInputはルーン／ページめくりの入力占有を管理。お邪魔解除の入力配送は未実装。BookNetworkServer.SendAddPageからクライアント受信へ至る経路はあるが、お邪魔専用メッセージはない。
- 仮案：種類IDから効果を生成し、PageInstanceに実行中効果を保持。基底クラスで開始・入力・更新・終了を揃え、派生側で解除判定を担当。状態と表示寿命を分離する。本全体の使用済み効果はページ効果と対象範囲を分ける。
- 未確定：付与対象（閲覧中／任意個体、左右片側／見開き）、同時付与、めくり可否、解除通知・再接続同期、べとべとの全体解除／部分除去。ユーザーと相談して確定する。画像生成・コード実装は未着手。
- ゲームコード・Scene・Prefabは変更せず、既存変更を保持。今回は実機・Play Mode検証なし。

## お邪魔4枠方式の仕様合意（2026-10-28）
- BOOK_INTERACTION_DESIGN.md末尾に4枠方式の確定仕様と推奨構成を追記。旧単一効果案より優先する。
- 見開きごとに4枠、複数枠付与・個別解除、空き番号順／不足分は古い枠から上書き、上書き進捗リセット、空きの詰め直しなし、ページ移動後も状態保持を合意。
- 4番枠だけが通常アクション・めくりを禁止する。4番枠解除で再開し、1〜3番枠の残存は再開を妨げないことをユーザー最終確認。
- 今回は設計資料のみ更新。コード・素材生成・Unity検証は未実施。通信復旧・入力中の付与・固定ページの扱い等は未確定として資料へ分離。

## お邪魔4枠の初期実装（2026-10-29）
- 合意した方針で実装を開始。PageInstanceごとにPageObstacleStateを持ち、各枠に独立したObstacleInstance／解除処理を保持。空き番号順・不足分の古い枠上書き・全枠置換・進捗リセット・解除後の詰め直しなしに対応。
- 左右擦りとスクラッチを実装。スクラッチは64×64の除去形状を保持し、透明余白を必要面積から除外。高速移動は線分で処理し、除去済み領域の再加算なし。スクラッチ素材はRead/Write Enabled必須。
- BookGameDragInputへ解除入力を接続。4番枠中は通常ルーンとめくりを禁止。描画／発動待ちは取消し、開始済みクールダウンは維持。上書きされた枠への古い解除入力は引き継がない。
- APPLY_OBSTACLE=6（uintイベントID＋byte種類ID）、BookNetworkServer.SendObstacleを追加。既存通信値は維持。受信の重複除外、めくり中の順序付き予約、固定ページ／表紙での破棄を実装。再接続時は未適用予約と比較基準を破棄し、付与済み状態は維持。送信保存・再送・解除通知・状態同期は未実装。
- 設定空時にResources/BookObstaclesのRub(ID1/1枠)、Ink(ID2/2枠)を使う。仮表示は色付き四角形。表示はScreen Space Overlayで、めくり中は非表示。本の紙質や曲面・めくりへの汚れ合成と完成素材は未対応。
- BookObstacle COMPLETE。枠割当・独立解除・上書き・古い入力による誤解除防止・4番枠制限・見開き独立・左右反転・スクラッチ形状保持／透明余白をEditorで確認。
- BookObstacleRuntime COMPLETE。現在のBookSceneを保存／差し替えせずPlay Modeで、パケット解析→付与、重複無視、ページ状態保持、めくり中複数予約の完了後適用、入力禁止／復帰、描画取消、クールダウン維持、4つの表示生成を検証。入力状態の一部は反射で設定した検証で、物理操作の実証とは区別する。
- 既存Verify Rune FeedbackもCOMPLETE。初回の追加検証コードでDataStreamWriterをIDisposable扱いしてコンパイル失敗。NativeArrayを所有する形式へ修正後、Unityでコンパイルと上記検証を通過。
- 操作を伴う目視確認では表紙の表示まで確認。自動ドラッグで表紙から進められず、お邪魔を物理的に擦る体感確認は未完了。最終のPlay Modeは終了済み。実PC／Android通信とAndroid描画は未確認。
- 敵側の呼び出し追加は他メンバー領域のため未変更。利用方法・通信・制限・検証メニューはBOOK_OBSTACLE_IMPLEMENTATION.mdを参照。
- 開始時のAGENTS.md・設計資料・PROJECT_STATUS.md・BookScene・MagicData・.vsの既存変更を保持。Scene・Prefab・hatake原本・共有設定は未変更。確認時点の保存済みScene SHA256は479FB93964D78D196200B4EF0C6ADBAFBA08691FA593CFC98BBB59082D4B0009。
- Git status/fetch/追跡先比較を実施し、現在ブランチMS-82/Feature/BookObstacleは追跡先とのコミット差分なし。禁止Git操作は未実施。
- 最終調整：表示メッシュは解除進捗が変わったときだけ再構築し、右指定範囲をページ内へ制限。Previewメニューに最初の通常見開きを開く処理とFill Four Slotsを追加。最終ランタイム／EditorコードはUnityの参照設定による別出力コンパイルも成功。これら最終調整後の物理操作・目視確認は未実施。

## Book管理再設計に向けた現状調査（2026-10-29）
- ユーザー依頼はまず現状確認。ゲームコード・Scene・Prefab・hatake原本は変更せず、静的調査のみ実施。今回のUnityコンパイル・Play Mode・実機確認は未実施。
- 現行BookSceneはBookGameBootstrap → BookGameController → YasudaBookViewController → hatake.BookFlipView / BookPageViewの構成。YasudaBookViewControllerは既にhatake.BookControllerの専用複製。BookSceneのm_modelViewは未設定、Flip View・表紙内側・Live Turning Pagesは有効。BookValidation.prefabにも専用Controllerがあり、Book.prefab / OldBook.prefabとhatakeのBookRenderTestは元Controllerを参照している。資産全体を一括置換する前提にはしない。
- 初期一覧はBookGameController.Initial Pagesと表示用BookDataで二重設定。表示側AwakeがBookDataを読み、管理側Initializeが固定ページ・左右参照・件数を照合する。追加・削除・入れ替え時は逆に管理側Applyが表示用一覧を組み、TryApplyInventoryの完了で論理一覧を確定する。初期化と更新でデータの流れが異なる。
- PageData / PageInstanceはゲーム用1見開き、BookPageDataは片側1枚。現在の対応は表示枚数3+2N、ゲーム見開きi→表示Spread i+1、左index 2+2i。左右や固定ページの計算は管理・表示の境界で扱うべき。
- YasudaBookViewControllerは約900行。撮影、RenderTexture切替、表紙、めくり、ライブ撮影、一覧編集、燃焼／復活、旧Noise除去、Rune画像取得が同居。AddPage / RemovePage / BurnLeaf / InsertLeaf / BurnSpread等、管理側PageInstance一覧を更新しない公開編集経路も残る。現行専用ControllerへのこれらAPIの外部C#呼出しは今回の検索では検出しなかったが、削除前にはUnityEvent・Prefab・実行時参照も確認が必要。
- 操作可否はBookGameController.Ready / IsInputBlocked、BookGameDragInputのGESTURE、BookInputController、RuneTraceController、BookObstacleController.Ready / BlocksActions、表示側IsBusyとFlipView.IsTurningへ分散。入力側もFlipView.BeginDrag等を直接呼ぶ。状態を単一enumへ押し込むより、操作ごとの可否判定の窓口と中断・完了通知を揃える案を推奨。
- Bootstrapは初期化に加えて通信購読、ページ要求キュー、ルーン更新、お邪魔生成・接続状態反映を担う。ネットワークClientはStartで一度取得する構成。起動順に関する実行時不具合は今回未検証。
- Rune画像は専用Controllerが生成ImageをSprite・位置・サイズで照合して取得する。BookPageViewは画像を再生成し、生成Imageをindexで返す公開契約がない。専用PageViewを作るなら画像参照を安定して取得できるようにする価値がある。
- 複製候補：撮影・表紙・めくりの連携部分、必要ならBookPageViewとBookFlipView。BookFlipViewはShader / Material / cgincと併せて依存確認が必要。BookData / BookSpreadDataはゲーム側を一覧の正にすれば不要化できる見込み。BookPageDataは既存asset参照維持のため当初再利用する案。BookModelView、元入力、デバッグ入力、旧Noise管理は現行Sceneの用途と必要性を確認して選別する。以上は提案であり新しい複製・移行は未実施。
- 推奨する次の設計：所持・現在ページを所有する管理、描画とめくりを受け持つ表示、操作可否と入力振分け、通信要求の受付を分離。初期化も編集も同じ管理一覧から表示を生成。既存PageInstance・RuneTraceInteraction・クールダウン・お邪魔状態は移行時に保持する。固定ページ、めくり取消、編集中断、上限入れ替え、切断時の入力制限を回帰確認対象とする。
- Git：通常権限は親HujiTV管理ディレクトリへのアクセスで失敗。承認付きstatus/fetch/追跡先比較は成功。MS-82/Feature/BookObstacleは追跡先とコミット・確定ファイル差分なし。既存の未コミット／未追跡のお邪魔実装、BookScene、管理資料、MagicData、.vs変更を保持。履歴操作なし。

## Book管理の再構成（2026-10-29、実装済み）
- ユーザーから再設計・実装の指示を受け、担当側の専用構成へ適用。詳細と設定手順はBOOK_MANAGEMENT_DESIGN.md。過去の「初期BookDataとInitial Pagesの両方を揃える」という記録は、今回以降の専用版には適用しない。
- BookInventoryを新設。所持見開き一覧、追加・削除・入れ替え候補と表示投影を分離。初期表示も編集も同じBuildVisualPagesで作り、専用表示側のBookData参照と初期照合処理を除去。固定ページとInitial Pagesのみで初期化できる。
- BookGameControllerは現在個体・Interaction・編集受付／確定・選択・操作可否を担当。表示完了通知でInteractionを再開し、撮影途中の再開を防止。初期化完了前の入力を禁止し、CanNavigate / CanRemoveObstaclesを共通化。
- 既存専用複製YasudaBookViewControllerを再構成。管理を迂回する独立ページ編集・見開きセット管理・旧Noise除去を除去。撮影・表紙・めくり・ライブ撮影は維持。ドラッグのBegin / Update / Endは表示側経由に統一。約900行から約560行へ縮小。
- BookPageRequestQueueを新設。Bootstrapに同居していたID解決・有界FIFO・保留処理を分離。Bootstrapの必須参照検証と、管理初期化成功後の入力接続を追加。既存の通信形式と起動時Client取得方式は維持。
- 現行BookSceneを保存・差し替えずVerify Book Management In Current Sceneを実行しBookManagement COMPLETE。初期3見開きの投影、上限8までの追加、現在個体保持、満杯選択と辞退、閲覧個体の入れ替え、古い個体の拒否、削除、編集中断・復帰、開閉を確認。新規検証コードはEditor配下。
- 既存Verify Book Obstacles In Current SceneもBookObstacleRuntime COMPLETE。4番枠制限／解除、途中描画取消、開始済みクールダウン保持、めくり予約、個体状態保持を確認。Verify Rune FeedbackもRuneFeedback COMPLETE。物理ドラッグ操作・Android・実PC間通信の実証とは区別する。
- Unityの参照設定を使い、Temp/BookRefactorValidationへ別出力のランタイム／Editorコンパイルも成功。最初の補助Python実行で既定CP932読込エラー、Editor用rsp追記で改行不足があり、UTF-8明示と改行修正後に通過。ゲームコードの最終コンパイルエラーなし。
- hatake原本・Scene・Prefab・共有設定・既存アセットは変更なし。保存済みBookScene SHA256は479FB93964D78D196200B4EF0C6ADBAFBA08691FA593CFC98BBB59082D4B0009で同一。既存のお邪魔実装等の未コミット変更を維持。Play Mode終了済み。Git履歴操作なし。diff --checkで出たScene末尾空白は今回未変更の既存差分。
- 継続上の前提：低レベルのBookFlipView / BookPageView / BookPageDataとShader等は原本参照で再利用。ルーン画像は既存の生成Image照合方式。旧Book.prefab / OldBook.prefab等の元Controller構成とBookDataアセットは保持。新専用PageViewや全表示部品の複製は行っていない。

## ルーン粒子・直接方向指定演出（2026-10-29、実装済み）
- ユーザー合意：なぞった線のみを粒子化し、少し上昇して中央下へ集合。色は印刷ルーン画像から取得。中心の弾と周囲を漂う粒を重ね、指を離してから弾を触る→少し追従するドラッグ→指を離した瞬間に発射。中央下の明るい領域を維持。集合0.9秒程度を初期値とし各数値をInspector調整可能。方向指定中だけ魔法風の画像矢印を表示。
- RuneSpellEffect / RuneParticleGraphic / RuneSpellSettings / RuneSpellPalette / IRuneCastPresentationを追加。Dissolving Rune、Spell Core、Orbiting Motes、Spell Trailの4 ParticleSystemをuGUIで合成。書いた線を長さ基準で均等サンプリングし、浮上と曲線収束、待機、追従、3方向への発射を実装。Screen Space Overlay表示で、本の紙質・曲面への合成ではない。
- 色は既存のRune Image Indexで指定されたSpriteからGPUで32×32読み戻し、白い紙・黒い線を除いた有彩色を抽出。元アセットのRead/Write設定・画像・MagicDataへ重複設定を追加しない。無彩色／画像なしにはFallback Colorを使用。
- RunePageViewへ専用表示接続を追加。BookGameBootstrap→BookGameDragInput.ConfigureSpellPresentationで既存Sceneへ自動接続。RuneTraceControllerは集合完了までTryBeginCastを拒否し、確定時に粒子発射→既存クールダウン／CastConfirmedへ進む。方向指定なしの魔法は即発動のまま。切断・4番枠による入力禁止・失敗・ページ終了で未発射表示を解除。フォーカス喪失では保持操作だけ取消。
- 調整場所：BookGameDragInputのSpell Particle Presentation → Spell Settings。Float / Gather Duration、Float Height、Trace Particle Count / Size、Core Size、Orbit関連、Drag Follow / Limit、Flight Duration、Trail数、Arrow Sprite / Length / Idle Alpha。既存Cast Guideの位置・半径・暗さ設定は維持。旧Arrow Widthは互換フィールドとして残るが、画像矢印の大きさは新Arrow Lengthで調整。
- 組み込みimage_genで透過の白金の魔法風矢印を生成。Resources/RuneSpell/MagicAimArrow.pngへコピーし、RuneSpellAssetImporterで当該新規PNGをSprite設定。既存メッシュ矢印をImageへ置換。素材パス・生成プロンプト・設定・構成・検証はRUNE_SPELL_PRESENTATION.md。
- Unityの参照設定を使う別出力ランタイム／Editorコンパイルが成功。最終版は既存Unityでコンパイルし、新規Verify Rune Spell In Current SceneがRuneSpell COMPLETE。非Readable画像の色取得、粒子化と線の消去、集合前ガード、Coreと周囲粒、次フレーム保持と48頂点、直接追従、3方向の1回発射、即時クールダウン、尾、フォーカス取消・入力禁止時消去・方向指定なしを確認。入力は合成ポインターイベント、描画は実Game画面を使用。
- Temp/RuneSpellValidationに結果と段階別の実画面PNGを保存。浮上・中央下の弾・方向指定中の画像矢印・発射を目視確認。初期版はCanvasRenderer不足で粒子が描画されず、RequireComponent追加後に表示成功。検証コードの曖昧なGetMethodもDeclaredOnly指定で修正し、最終検証を通過。
- 既存Verify Rune FeedbackはRuneFeedback COMPLETE、Verify Book Obstacles In Current SceneはBookObstacleRuntime COMPLETE。物理ドラッグの体感・Android実機・実PC間通信は今回未確認。最終Play Modeは終了済み。Unityは開始時からWindows対象であり、Codexによるプラットフォーム切替は行っていない。
- Scene・Prefab・hatake原本・共有設定は未変更。開始時からあったFireMagic / IceMagic、PageView_07 / 08、TestPage群、MagicType、Close / Open関連新規データ、number.png.meta等の既存ユーザー変更を保持。BookScene保存済みSHA256は479FB93964D78D196200B4EF0C6ADBAFBA08691FA593CFC98BBB59082D4B0009で同一。Git status/fetch/追跡先比較を実施し追跡先とのコミット差分なし。Git履歴操作なし。
