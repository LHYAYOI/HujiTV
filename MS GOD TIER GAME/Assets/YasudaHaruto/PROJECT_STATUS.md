# Project Status

## 現在の状態
新しい本モデルへの統合完了。
ルーン入力・ページめくり・魔法通信は動作確認済み。
※動作確認の根拠は下記記録を参照。魔法通信はユーザーがテスト済みで問題なしと再確認しており、追加の再テストは不要。

## 現在の作業
旧Book構成の整理を実施。Hamada側BookSceneが無いことを確認し、不要な旧16スクリプト・移行補助・候補Sceneを削除。
MagicDataの着弾ルーン画像について、魔法ごとの固定画像を一か所で設定する設計を整理。画像機能はまだ実装していない。

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
日付：2026-10-21
変更内容：旧Book構成16スクリプトと各.meta、候補Scene、一回限りの移行補助を削除。BookScene・検証Prefabの旧コンポーネント参照と候補専用メニューを除去。着弾画像の設計を記録。
確認結果：Hamada側BookSceneが無く、削除対象への外部GUID参照がないことを確認。除去コンポーネントはすべて非アクティブな階層内。Unity 6000.3.23f1のバッチコンパイルは終了コード0、C#コンパイルエラーなし。git status・fetch・追跡先のコミット差分なし。今回Play Mode再検証は未実施。
未完了事項：BookSceneのPlay Mode回帰確認。MagicDataへの画像フィールド追加とアセット設定、担当間相談後の着弾エフェクト接続。入手・削除演出、日本語フォント指定、お邪魔ページの削除条件。

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

### 推奨する保存場所とAPI（未実装）
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
