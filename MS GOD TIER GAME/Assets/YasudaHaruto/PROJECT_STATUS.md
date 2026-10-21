# Project Status

## 現在の状態
新しい本モデルへの統合完了。
ルーン入力・ページめくり・魔法通信は動作確認済み。
※動作確認の根拠は下記記録を参照。魔法通信はユーザーがテスト済みで問題なしと再確認しており、追加の再テストは不要。

## 現在の作業
ページ追加・削除の統合方針を整理。
実装前の設計案を下記に記録。ゲームロジックの変更・削除は未実施。

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
変更内容：hatake原本を保持しControllerを担当側へ複製する構成案、必要API、満杯時のページ選択画面と入れ替え処理を具体化。ユーザーの複製・変更相談方針をAGENTS.mdにも記録。
確認結果：作業開始時のstatus・fetch・追跡先差分確認を実施し、コミット差分なし。今回は管理資料のみ更新し、コード・Scene・Prefab・データは未変更。
未完了事項：下記の具体案の実装と専用Scene / Prefabへの配線。共有Scene / Prefabやhatake原本を変更する場合は事前相談。お邪魔ページの削除条件は引き続き未定。閲覧位置・キャンセル挙動は下記推奨案で未確定。

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
