# トロッコステージ設定ガイド

対象は Arai 担当のトロッコ走行とギミック、および Hamada 担当のカメラ・当たり判定との接続です。2026-11-04 時点の保存済みシーン、Prefab、C# を確認してまとめています。Play Mode での動作は未確認です。

## まず開くもの

| Unity の Project 内の場所 | 確認する内容 |
| --- | --- |
| `Assets/Main/Main_Game_Scene_Alpha.unity` | 走行・戦闘カメラ・敵出現と戦闘開始をまとめて追える既存例。保存値の `Start Offset` に 60 の上書きがあるので、冒頭から確認するときは複製側で開始位置を調整する。 |
| `Assets/Arai/Scene/Stage_MultiSwitchSplineGimmick.unity` | 分岐用の参照シーン。個々の `Switch Point` の切替先が埋まっているか確認して使う。 |
| `Assets/Arai/Prefabs/Stage` | 走行・停止・分岐・視線変更などの設定済み Prefab。シーンに配置したインスタンスで参照を割り当てる。 |

作業を試すときはシーンを複製し、Prefab のフィールドをシーン側で調整します。以下のフィールド名は Inspector 表記に寄せています。必要に応じて括弧内の C# 名で検索できます。

## 1. トロッコを走らせる

1. `StageSpline.prefab` と `TrokkoController.prefab` を置く。 `StageSpline` の `Cart` にトロッコの `CartController` を割り当てる。StageSpline の Prefab 原本ではこの参照は空。
2. `StageSpline` 配下の `SplineController2` を選択し、その `Spline` に登録されている `SplineContainer` を確認する。Scene ビューでスプラインのノットを編集し、先頭から終点までの向きと高さを整える。分岐先は `SplineController.prefab` を使って別の Container に分ける。走行は Container の先頭スプラインを評価する。
3. `StageSpline` の `Start Spline Controller` に開始ルート、`Start Offset` にルート先頭からの開始位置、`Start Speed` に走行開始速度を設定する。最小確認なら開始位置 0、速度 1。Play 開始時にこの位置へ移動して走り出す。
4. 終端で先頭へ戻すなら、そのルートの `SplineController2 > Loop Flag` を有効にする。形状も滑らかにつなぎたい場合はスプラインの閉じ方と始終端を確認する。

**位置指定の読み方**  
`Start Offset`、`Gimmick Point`、`Switch Offset` は、それぞれのルート上での距離相当値です。内部では「値 ÷ スプライン全長」で 0〜1 の評価値に変換します。例えば全長 40 のルートで 10 を指定すると評価値は 0.25 です。厳密な等距離変換ではないため、カーブを編集したら Scene ビューで実位置を見直します。

`CartController > Speed` は `Awake` で復帰用の `BaseSpeed` に保存され、走行開始時には `StageSpline > Start Speed` が現在速度を上書きします。通常速度を統一したい場合は両方を揃えます。分岐時のレール間移動速度は `CartController > Switch Speed` です。

## 2. 通過イベントを置く

**トロッコの Collider → CartHitter → ギミックの HitReceiverController → On Hit → 実行するメソッド**

1. ギミック Prefab を、発火させたい `SplineController2` の子孫に置く。 `GimmickSpline` は親を検索して所属ルートを決める。
2. `Gimmick Point` を設定し、Scene ビューで発火用 Collider の位置を見る。基底クラスがスプライン上へ配置するタイプでは、この欄を調整する。
3. 当たり判定用 GameObject の `HitReceiverController > On Hit` に、実行先のコンポーネントとメソッドを設定する。 `Switch(HitData)` や `StopHit(HitData)` は Dynamic の項目を使う。引数のない `Play()` などは通常の項目を選ぶ。
4. `CartHitter` は `OnCollisionEnter` を使う。既存 Prefab と同じく `Is Trigger` をオフにし、衝突相手の Collider と受信コンポーネントの GameObject、トロッコ側の Rigidbody、レイヤー同士の衝突設定を確認する。トロッコを必要とするメソッドは受信データから `CartController` を取得する。

| やりたいこと | Prefab / コンポーネント | 設定する欄と接続 |
| --- | --- | --- |
| 別ルートへ分岐 | `SwitchPoint` / `GimmickSplineSwitchPoint` | `Target Spline Controller` に切替先、`Switch Offset` に切替先での合流位置、`On Hit → Switch(HitData)`。 `Teleport` オンで即座に位置と向きを移し、オフで合流地点まで直線移動する。 |
| 一時停止・再開 | `Stop` / `GimmickSplineStop` | `On Hit → StopHit(HitData)` で進入速度を覚えて 0 にする。戦闘終了などから同じ Stop の `Move()` を呼ぶと進入速度へ戻る。 |
| 区間で加速・減速 | `SpeedChange` / `GimmickSpeedChanger` | 同じレールの異なる地点に Range のペアを置く。 `Normal To Target` と `Target To Normal` にそれぞれ担当する入口を指定。 `Target Speed` と通常速度を設定する。 `Use Base Speed` オン時はトロッコの `BaseSpeed` を使用。 |
| アニメーション | `PlayAnim` / `GimmickSplineAnimPlayer` | `Animator` と Animator Controller の Trigger 名を指定し、`On Hit → Play()`。 |
| オブジェクト生成 | `GimmickSplineCreateObject` | `Prefab`、`Instantiate Transform`、`Destroy Timer` を指定し、`On Hit → Create(HitData)`。生成位置・向きは指定した Transform を使う。 |
| 敵を単独で出現 | `GimmickSplineEnemySpawn` | `Enemy Manager` と `Enemy Id`・`Spline Id`・`Spawn Id` を設定し、`On Hit → Spawn(HitData)`。ID は `EnemySpawnManager` 側の登録と一致させる。 |

`Range` は入口の衝突で `EnterRange(HitData)` を実行し、その時点のトロッコ位置から `Pair` の地点までの進捗を毎フレーム通知します。出口判定は位置の比較です。入口と出口を同じルート上の別地点に置き、ペア参照を埋めてから Play します。停止や速度変更の発火順を分かりやすくするため、最初の確認では発火地点を離して配置します。

## 3. カメラを切り替える

1. 描画用 Camera に `CinemachineBrain` を用意し、`CameraManager > Brain` に登録する。 `Camera List` に通常用と戦闘用の `CinemachineCamera` を登録し、各 Name を **`FPSCamera` / `BattleCamera`** にする。コードはリストの Name で検索する。追従・画角は各 Cinemachine Camera、切替時のブレンドは Brain を確認する。
2. `SetBattleCamera` を対象レール配下に置き、子の Range で入口・出口を決める。 `GimmickSplineSetBattleCamera > Range` が進行方向側の入口を指し、その入口の `Pair` が出口を指すことを確認する。入口から区間内に入ると戦闘カメラ、区間外へ出ると通常カメラへ切り替わる。実際の切替地点は子 Range の値で決まる。
3. 戦闘カメラの視線を演出するなら、戦闘用 `CinemachineCamera` に `CameraLookTargetChanger` があることを確認する。 `LookTarget` の `Target` に見せたい GameObject、`Duration` に視線変更の時間を指定し、`On Hit → SetLookTarget()`。元の向きに戻したい地点には `ResetCameraOrientation` を置き、`On Hit → Reset()`。この Reset は戦闘カメラの向きを戻す操作で、通常カメラへの切替は Range の退出で行う。
4. カメラ登録だけを切り分けたい場合は、`CameraManager` のデバッグ入力 P（戦闘）／O（通常）を Play 中に確認する。切替は対象の Priority を 10、リスト内の他カメラを 0 にする。

現行の `GimmickSplineCameraShake.Shake()` はカメラ呼び出しがコメントアウトされています。 `GimmickSplineSwitchPointTeleport` も内部では即時移動を指定していません。即時分岐の設定箇所は通常の `SwitchPoint > Teleport` です。

## 4. 戦闘で止めて、終わったら発進させる

1. シーンの `BattleManager` に `Active Enemy Manager` を割り当て、`EnemySpawnManager` が存在することを確認する。
2. `BattleData` に制限時間（秒）と敵リストを設定する。各行に `Enemy Id`・`Spawn Id` と、スプライン移動する敵なら `Spline Id` を入れる。 `Spline Id` が空の行はポップアップ敵の生成経路を使う。
3. 手前の発火地点で `GimmickSplineBattle > Battle Data` に上のデータを指定し、`On Hit → SpawnStart()` で敵を出す。後ろの発火地点から `BattleStart()` を呼ぶ。停止用 `StopHit(HitData)` も配線し、停止したせいで戦闘開始地点に届かなくならないよう、戦闘開始と停止を同じ `On Hit` に登録するか、走行順を調整する。
4. **`BattleStart()` を呼ぶ側**の `On Finish Battle` に、停止に使った `GimmickSplineStop.Move()` を登録する。敵が全滅した場合も制限時間が切れた場合も、この終了イベントが呼ばれる。戦闘カメラ区間の出口は、再発進後に抜ける位置へ置く。

## 5. Play Mode で確認する順番

| 症状 | 最初に確認する欄 |
| --- | --- |
| 出発しない | `StageSpline` の `Cart` と `Start Spline Controller`、Container の全長、開始位置・速度。Console に参照エラーが出ていないか。 |
| 通過しても発火しない | ギミックの親ルート、`Gimmick Point`、Collider の位置と Trigger、Rigidbody・衝突レイヤー、`On Hit` の参照先。開始位置が発火地点より先になっていないか。 |
| 分岐先で止まる | `Target Spline Controller`、`Switch Offset` が切替先の全長内か、`Switch Speed` が正か、現在速度が 0 になっていないか。 |
| カメラが戻らない | `Camera List` の名前、Range の入口と Pair、出口が実際に通るレール上にあるか。 |
| 戦闘後に発進しない | `SpawnStart()` が先に呼ばれたか、`BattleStart()` 側の `On Finish Battle` が停止に使った Stop の `Move()` を指すか。 |

順番は「走る → 1個のイベントが発火する → 分岐・カメラ区間を通る → 戦闘後に再発進する」。複数の演出を置くときも、最初の段階から順に通すと止まった箇所を絞れます。

## 実装を確認する場所

- 走行とルートの初期化 [StageSpline.cs](../Arai/Script/Trokko/StageSpline.cs) / [CartController.cs](../Arai/Script/Trokko/CartController.cs)
- 衝突と位置指定 [CartHitter.cs](../Arai/Script/Trokko/CartHitter.cs) / [HitReceiverController.cs](../Hamada/Scripts/Hit/HitReceiverController.cs) / [GimmickSpline.cs](../Arai/Script/Gimmick/GimmickSpline.cs)
- 区間・カメラ [GimmickSplineRange.cs](../Arai/Script/Gimmick/GimmickSplineRange.cs) / [CameraManager.cs](../Hamada/Scripts/Camera/CameraManager.cs)
- 戦闘の開始と終了 [BattleManager.cs](../Arai/Script/Battle/BattleManager.cs)

