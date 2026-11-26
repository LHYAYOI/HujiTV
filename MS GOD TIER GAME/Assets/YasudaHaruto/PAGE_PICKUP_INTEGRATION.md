# ページ取得デモと本編への組み込み

## 分岐シーンで報酬を調整する

`Assets/YasudaHaruto/Scenes/Yasuda_GameScene.unity` を開きます。戦闘報酬は戦闘開始側の **Page Reward Battle → Reward Page** で設定します。ミニお化け1戦目は ID 3、2戦目は3体全滅で ID 6、マエストロ撃破は ID 4 です。鍵お化けと群れお化けは従来どおりです。

`BattleManager` の **Route Page Rewards → Route Page Data** に道中の ID 1 が割り当てられています。置き場所は **Route Page** が参照する `Bedroom Exit - Close Page` を移動して調整します。初期配置は寝室出口から廊下へ約5mです。**Pickup Distance** は4mで、トロッコが範囲内に入ると飛行が始まります。

同じコンポーネントの **Open Playback Speed** はページを開く速さ（0.6倍）、**Minimum Flight Duration** は飛行時間の下限（3.2秒）、**Camera Arrival Distance** はカメラから到着点までの距離（1.65m）です。

`Scenes/PagePickupDemo.unity` は `Main_Game_Scene_Alpha_PageRewards` の複製です。Alphaの寝室と廊下、トロッコ、一人称カメラ、敵、魔法弾を使って、2種類のページ取得を1台のUnityエディターで確認できます。

## ノートPCで作業を再開する

- 2026-11-26時点の作業ブランチは `MS-166/Feature/GetPage`。今回の成果物は未コミットなので、ノートPCでブランチを取得する前に、このPC側で必要な差分を確認してコミット・プッシュするか、ファイル一式を直接移します。
- シーン、スクリプト、マテリアル、専用Prefab Variant、`BookScene.unity` と `Data/PageDatabase/BookPageDatabase.asset`、`Assets/page/` のFBX・テクスチャを含めて移します。ファイルとフォルダの **`.meta` をセットで移し、UnityのGUID参照を保持**します。Git上の既存の他担当差分は内容を確認した上で扱います。
- `ProjectSettings/ProjectVersion.txt` に記載のUnityで開き、まず `Assets/YasudaHaruto/Scenes/PagePickupDemo.unity` をPlayします。Spaceで2戦をクリアしてID **3 → 6 → 1**、別の試行で走行中にFを押してID **1** のみが届くことを確認できます。

## 本編シーンへ反映するときの確認順

1. 組み込み先のシーンを複製して試します。`PagePickupDemo` は見え方の参考、`Main_Game_Scene_Alpha_PageRewards` は戦闘・配信の配線の参考です。対象シーンのトロッコ、戦闘イベント、置きページの参照をInspectorで付け直します。確認シーンのSpace・F・取得ID表示は `PagePickupDemo` コンポーネントにまとまっています。
2. 本編の速度と一人称カメラで、敵の撃破位置と道中の待機位置が回収前に見えるか確認します。**Proximity Target** に車両の基準点、**Cart Target** にカメラ前方の見える到着点を設定します。カメラ切り替え中にも紙面が壁や床、近クリップ面に隠れないか確認します。
3. 実際の魔法弾で撃破し、単体・群れ全滅で各1枚、部分撃破・時間切れでは報酬が出ないことを確認します。B・Cの命中受信と死亡パーティクル位置は、使用するPrefabの両方の参照を確認します。マエストロは撃破後にトロッコが再発進するところまで確認します。
4. **Local Preview Only** をオフにして、PCのゲーム開始後にタブレット2台を別々のプレイヤー枠で接続します。初期ID **2・5**、追加ID **1・3・4・6** を照合し、各報酬が両方の本にID順で一度だけ入り、対象見開きで獲得演出が再生されるところまで確認します。タブレット実機2台での通し確認は引き継ぎ時点で未実施です。

## デモの操作

1. `PagePickupDemo` を開いて Play を押し、Game ビューを表示します。
2. トロッコが戦闘で停止したら、**Space** または **Cast magic at a mini ghost** で既存の魔法弾を撃ちます。1戦目のミニお化けを倒すと「火をつける」（ID 3）が敵の位置から落ちます。2戦目は全滅で「枕を投げる」（ID 6）が出ます。
3. トロッコが寝室を出ると、廊下に置かれた「閉める」（ID 1）が近づいたトロッコに飛んできます。道中の回収を先に試す場合は、走行中に **F** または **Try the bedroom exit** で寝室出口の手前へ移動できます。
4. 再試行するときは Play を停止して再開します。

デモは到着したIDを画面左上に表示します。接続や本のめくりを含めて確認する場合は、本編側の `BookNetworkServer` とタブレット側の `BookScene` を使います。

## 処理の流れ

| 発生元 | 開始条件 | 使用する処理 |
| --- | --- | --- |
| 敵・群れ | 今回の戦闘で出現させた対象がすべて撃破された | `PageRewardBattle` → `RoutePageRewards.Drop` |
| 道中 | トロッコの取得点が置きページの近接範囲に入った | `RoutePageRewards` が前フレームからの移動区間と配置点の距離を判定 |
| 共通 | Pageモデルがトロッコの取得点に到着した | `PageArrived` を通知。本編では `AcquirePageForBoth` に同じIDを渡す |

本では `BookPageDatabase` がIDを `PageData` に変換します。取得した見開きをID順に挿入し、対象までめくって既存の金色の演出を再生します。

## 本編側で設定する項目

- **Route Page Rewards** に、トロッコの取得点 `Cart Target`、道中の配置点 `Route Page`、その `PageData` を割り当てます。初回は配置点が進行経路から `Pickup Distance` 以内にあることを確認します。
- **Page Model** に `Assets/page/page_mdl.fbx`、**Open Clip** に `Assets/page/page_anim.fbx` の `Take 001`、**Page Material** に `Materials/PagePickup.mat` を指定します。画面内で紙が読み取れる大きさは **Page Scale** で調整できます。**Face Camera** を使う場合、飛行中は MainCamera の方向へ紙面を向けます。
- **敵報酬**は撃破が確定した位置と `PageData` を `Drop` に渡します。群れは1体ごとの死亡演出で発行せず、全滅時に一度発行します。戦闘終了イベントには時間切れの経路もあるため、撃破成立を先に判定します。
- ミニお化けB・Cを使う場合は、弾が当たるColliderのオブジェクトに `HitReceiverController` を置き、`On Hit` から `MiniGhostEnemy.OnMiniGhostHit` へつなぎます。確認シーンではこの接続を持つ専用Prefab Variantを使っています。
- **Enemy Fall Duration** を設定すると、`Drop(page, position)` は撃破位置から0.38m下へ落下し、0.45秒置いてから飛びます。デモは落下を0.42秒に設定しています。落下先の座標を指定する場合は `Drop(page, spawnPosition, landingPosition, landingDuration)` を使います。既定値の0秒では、撃破位置からすぐ飛行を始めます。
- 取得時の紙面を一人称視点に収めるため、デモはカメラの手前1.65mを飛行の到着点にしています。**Proximity Target** にはトロッコ本体を指定し、取得開始の距離判定は車両の位置で行います。
- 本編では **Local Preview Only** をオフにします。これで到着済みIDが本編のサーバーへ渡ります。タブレットの `BookPageDatabase` に同じIDがあり、そのデータに左右の表示が設定されていることを確認します。

## Alpha複製シーンで見える位置

`Main_Game_Scene_Alpha_PageRewards` の置きページは、寝室レールから廊下レールへ乗り換えた直後です。敵報酬は撃破と同時に飛行を開始します。見える時間はカメラ角度、配置点、トロッコの進行で変わります。確認シーンでは、一人称カメラをトロッコPrefabの標準の高さに設定し、置きページを廊下に入って約5mの位置に置いています。紙の大きさ・明るさ・到着点と、敵ドロップの落下・待機も調整しています。
