# Book管理の現行構成

更新：2026-10-29。専用BookScene／BookValidation構成向け。

## 責務

| クラス | 担当 |
| --- | --- |
| BookInventory | ゲーム用見開き一覧、追加・削除・入れ替え候補、表示用ページ一覧の生成 |
| BookGameController | 現在のPageInstance、Interactionの開始終了、編集受付、入れ替え選択、表示完了後の確定、操作可否 |
| YasudaBookViewController | ページ撮影、RenderTexture、表紙、めくり、ドラッグ表示、表示完了・中断通知 |
| BookGameDragInput | ポインターの占有とルーン／ページ／お邪魔への振分け、座標変換 |
| BookPageRequestQueue | 通信のPageId解決、有界FIFO、実行可能になるまでの保留 |
| BookGameBootstrap | 各処理の生成・接続、通信購読と解除、更新呼出し |
| BookObstacleController | お邪魔の付与、解除操作、画面表示、接続状態によるルーン制限 |

## ページを設定する場所

BookGameControllerのInitial Pages、First Fixed Page、Last Fixed Page、Inventory Settingsを設定する。
Initial Pagesの各PageDataからLeftVisualData／RightVisualDataを参照する。

専用表示ControllerにBookDataを設定する必要はない。初期化も編集時もBookInventory.BuildVisualPagesで同じ順序の表示一覧を生成する。
Scene／PrefabのYAMLに過去のm_bookData値が残っていても、現行専用コードでは読み込まない。元のhatake.BookControllerを使う別構成では従来どおりBookDataを使用するため、既存BookDataアセットは削除しない。

管理単位はPageInstance 1件＝ゲーム用1見開き。表示はBookPageData 1件＝片側1枚。
先頭に表紙内側null枠と最初の固定ページ、末尾に最後の固定ページを付ける。ゲーム見開き数Nに対し表示枚数は3+2N、ゲーム位置iの表示Spreadはi+1。

## 編集の流れ

1. 外部からRequestAdd／Remove／ReplacePendingを呼ぶ。
2. BookInventoryが既存のPageInstanceを保持した候補一覧を作る。この段階で所持一覧は変えない。
3. 表示側が候補見開きを撮影する。編集開始時に現在のInteractionを終了する。
4. 撮影成功後、表示側と管理側の一覧を同じフレームで確定する。
5. SpreadShownで新しい現在ページのInteractionを開始する。

撮影前の表示側無効化は未確定候補を破棄する。上限時は選択中のページと挿入基準を保存し、個体を指定して入れ替える。同じPageDataを持つ複数個体も別々に扱う。新規分の辞退では元の所持・閲覧個体を復帰する。

表示側のAddPage／RemovePage／BurnLeaf／BurnSpread／InsertLeaf／InsertSpread等の独立編集APIと旧Noise除去処理は専用版から除去した。ページ一覧の変更はBookGameControllerへ集約する。

## 操作と表示部品

管理側のCanNavigateとCanRemoveObstaclesを入力側・お邪魔側で利用する。表紙は移動操作で開けるが、表紙上でお邪魔解除は行わない。編集・選択中は通常入力とお邪魔解除を止める。4番枠のお邪魔は通常操作を止めても解除操作を許可する。

ページドラッグのBegin／Update／Endは専用表示ControllerがBookFlipViewへ渡す。ルーンの描画・方向入力・クールダウンの判定は既存RuneTraceController等に残す。

hatake.BookFlipView、BookPageView、BookPageData、Material／Shader／cgincは参照して利用する。今回新しく原本全体を複製せず、既存の専用Controllerから必要な表示連携を残して再構成した。hatake原本は変更していない。
表示画像の取得は既存のSprite・位置・サイズ照合を維持しており、画像を安定したIDで返す専用PageViewは未導入。通信ClientをStartで取得する起動順の前提も維持している。

## 検証

- Tools/YasudaHaruto/Verify Book Management In Current Scene：現在のSceneを保存・差し替えずPlay Modeで初期化、一覧整合、追加、上限選択、辞退、表示中個体の入れ替え、削除、撮影中断と復帰、開閉を確認。結果はTemp/BookRefactorValidation/runtime-result.txt。
- Verify Book Obstacles In Current Scene：お邪魔の付与、めくり中予約、4番枠制限、描画取消、クールダウン保持の回帰確認。
- Verify Rune Feedback：ルーンの入力・完成・発動ガイド・画像クールダウンの回帰確認。

2026-10-29に上記3検証がCOMPLETE。ランタイム／Editorの別出力コンパイルも成功。手によるドラッグ操作、Android実機、実PC間通信は今回未検証。
