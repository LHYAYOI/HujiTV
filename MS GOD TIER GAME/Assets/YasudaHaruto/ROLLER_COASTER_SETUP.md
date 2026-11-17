# 線路スプラインメッシュ試作の使い方（2026-11-16）

## 最新調整：右の奥を隠し、対向車を至近距離へ（2026-11-17）
- 1回目の右回避ルートだけ、囲いの開始を分岐から実距離6m先へ手前移動。分岐後の幅を局所的に4.8→3.6mへ滑らかに絞り、18〜24m先で元へ戻す。左の枝のSpline・囲い接続位置12m・幅4.8mは保持。自車のSpline／速度は変更しない。
- 分岐手前5mの視点から、右レールの12m先が壁で隠れることをrayで確認。線路自体は常時表示し、壁を通過して曲がると自然に先が見える。
- 2か所とも対向車の開始距離12→9m、停止距離6→2.5mへ詰めた。速度3.2m/sは維持。車体後部の重なりを許容するユーザー指定に合わせ、実走でのカメラ位置と対向車の箱領域（幅2／高さ2.4／長さ2.4m）との距離を検証する。旧来の中心間2.3m制限は使用しない。

## 最新調整：図に合わせた滑らかな分岐と奥の遮蔽（2026-11-17）
- 左右の線路が同じ位置・同じ接線から分かれるY字へ修正。対向車側のSpline先頭の回転を明示し、接線の座標系の不一致で線路が交差気味に折れる問題を解消。枝は緩やかに90度曲がった先へ続く。
- 図の赤／青は色指定ではなく「囲いの奥に隠れる区間」として実装。左右の曲がり角から先に囲いをつなぎ、分岐手前5mから両枝の奥が壁に隠れることを検証。線路は常時存在し、表示切替はしない。
- 曲がった進入路でも3開口を保持する囲いへ変更。分岐内側のくぼみを保持し、一方の開口を外周の計算で塞がないようにした。床／天井の接続と落下通路は維持。
- 現在の下り・らせん・自車の走行Splineは変更せず接続できたため、追加の直線は入れていない。対向車は新しい滑らかな枝での安全距離を保つため、開始距離12m／停止距離6mへ変更。速度3.2m/s、2か所の位置と左右は維持。

## 最新仕様：対向トロッコは2か所（2026-11-17）
- 現在の仕様は以下。過去の「落下後に1か所」「直前に右線路を表示／遮蔽」は廃止。
- 1回目は最初のらせんへ入る曲がり角の直前。左へ続く線路から対向車が来て、自車は右へ回避してらせんへ入る。
- 2回目はらせん終端、線路なし落下の直前。右へ続く線路から対向車が来て、自車は左へ回避して落下する。
- 左右両方の線路は最初から接続・常時表示。線路の表示切替は行わない。対向車側の道は別方向へ伸び、再合流しない。
- Sceneの `RollerCoaster Course/Two Oncoming Encounters` が2演出と基準Splineを保持。各 Encounter の Inspector で対向車の Speed（3.2m/s）、Trigger Lead Meters（1回目35m／2回目20m）、Start Distance（9m）／Stop Distance（2.5m）を調整できる。対向車は仮モデル。
- 自車は1本の走行Splineを維持。2回目の左回避のため落下開始以降を水平左へ4m移動し、落差・帰路の形・アップダウンは保持。車体はSplineに沿い、カメラは進もうとする側へ最大15度だけ寄せ、回避中に正面へ戻す（Camera Motion / Encounter Look Degrees、0で無効）。
- 各分岐は3開口の囲いで接続。床をレールの下へ下げ、天井を上へ逃がして線路を覆わないようにする。コンパクトな落下通路、UVスクロール、まばらな配色、縦揺れと着地揺れは維持。
- 演出Inspectorの「2か所の経路と囲いを更新」で基準Splineから両演出・線路・囲いを再生成する。基準は Two Oncoming Encounters/Course Baseline。再生成はその基準から行うため、手作業で変えた現走行Splineを自動採用しない。コース全体の再生成後は基準も含め再設定が必要。
- 本番ステージや既存CameraManagerへの統合は未実施。既存Arai CartControllerとHamadaのカメラ揺れを使用した試走構成。

## 最新の体感調整（2026-11-17）
- 着地後はその場で1.2秒停止し、速度2.5から3秒かけて平坦区間の速度10へ発進する。落下速度24を平坦へ持ち込まない。Pause中は停止／発進の時計も止まり、Restartでリセットする。
- 最初の急降下は水平長48m／落差36m／目標速度28。実経路長は約60.434m（以前43.563m）。
- らせんは2周を維持し、半径11.5→8.5mへ縮小。新しい外側は以前の内側半径12m以下。らせん全体の実経路長は約128.785m（以前190.665m）。
- 図の俯瞰形状は維持し、First Hill Heightを7.5m、Second Hill Heightを5.5m、Exit Dropを7.5mへ変更。最後の下降を含めて高低差を強めた。
- 現在の全長403.456m／73ノット。落下区間211.651〜248.665mには線路を生成しない。前半Mesh126,582／後半92,520頂点。

### 着地停止と発進の設定場所
`Trial Ride > Roller Coaster Speed Profile` の「着地後の停止とゆっくりした発進」で調整する。

| 項目 | 現在値 | 内容 |
| --- | --- | --- |
| Landing Stop Seconds | 1.2秒 | 着地位置での停止時間。0で停止なし |
| Landing Start Speed | 2.5 | 発進直後の低速値 |
| Landing Acceleration Seconds | 3秒 | Flat Speedへ上げる時間 |

着地より先からStart Progressで開始した場合は、着地停止をスキップする。区間速度を無効にした場合は従来どおり一定速度となる。

## 拡大図に合わせた現在の帰路（2026-11-17）
- 落下後は、右下の黒い平坦 → 大きなカーブの赤い上り → 同じ大きなカーブの青い下り → 左側の小さな曲がりの赤い上り → 最後の青い下り。以前の「平坦な折り返しの後に山を2つ」は置き換えた。
- Courseを選ぶとSceneビューに黒／赤／青／赤／青の中心線と区間名を表示する。上からの形と高低差を同じ5区間で生成する。
- `Return Width`（85m）と`Return Depth`（58m）で俯瞰の大きさ、`First Hill Height`（現在7.5m）／`Second Hill Height`（現在5.5m）で2つの上りの高さ、`Exit Drop`（現在7.5m）で最後に着地高さより下がる量を調整する。
- 旧Flat Length／Hill Half Length／Return Radius／Return Angle／Return Sway／Exit Lengthは既存の保存値を維持しているが、現在の帰路生成では使用せずInspectorから非表示にしている。
- 拡大図へ初めて合わせた時点は73ノット／約443.815m。現在の長さ・生成Meshは冒頭の最新調整を参照。初回は入口〜落下終端を保持したが、最新調整では急降下とらせんも変更済み。

## 区間速度と短い停止の調整
`Trial Ride`の **Roller Coaster Speed Profile** コンポーネントで変更する。現在は区間速度が有効なので、上のTrial Rideコンポーネントの`Speed=12`は一定速度へ戻したときの値。

| 調整項目 | 初期値 | 意味 |
| --- | --- | --- |
| Entry Speed | 6 | 入口の上り |
| Descent Speed | 28 | 頂点を越えた直線下降 |
| Spiral Speed | 16 | らせん下降 |
| Fall Speed | 24 | 線路なし落下 |
| Flat Speed | 10 | 図の黒区間の終わり |
| First Climb Speed | 7 | 大きな赤カーブの頂点 |
| First Descent Speed | 16 | 大きな青カーブの終わり |
| Second Climb Speed | 8 | 左の小さな赤カーブの頂点 |
| Final Descent Speed | 18 | 最後の青区間の終わり |
| Crest Stop Seconds | 0.7秒 | 最初の下りに入る前の停止。0で停止なし |

目標値の間を滑らかに補間する設定速度で、坂の重力による物理計算ではない。既存CartControllerのSetSpeedへ渡す。着地後は専用の停止・発進を優先し、その後の平坦は速度10とする。停止時間はPause中に進めず、Restartで最初から有効になる。Use Section Speedsをオフにすると、Trial RideのSpeedによる一定速度へ戻る。

## トロッコを試走する（2026-11-17）
1. `RollerCoaster`シーンを開いてUnityのPlayを押す。乗車視点で自動的に走り出す。
2. **Space**または画面左上のPause／Resumeで一時停止・再開。**R**またはRestartで開始位置へ戻る。
3. 出口では止まる。もう一度走る場合はR。終了は通常のUnityのPlay停止。

`Trial Ride`を選ぶと、区間速度に加えてStart Progress（初期0）、Fall Pitch（初期15度）を調整できる。Start Progressを変えた場合、Restartもその位置へ戻る。頂点より先から始めた場合は頂点の停止を行わない。値を保存したい場合は編集モードで変更する。

走行は既存のCartControllerを利用。確認用アダプターがこの車両の位置・姿勢を同じSplineへ合わせ、落下中は若干の前傾にする。落下時はほぼ鉛直になる接線の水平成分を視線へ使わず、離陸方位を保持し、離陸姿勢→前傾→着地姿勢を滑らかにつなぐ。既存の回転Lerpによる遅れはこの車両だけ補正する。表示は既存Prefabの箱モデルで、実モデルの車輪合わせは未実施。

乗車カメラと確認用ライトはPlay中だけ使用し、元のSceneカメラ／AudioListenerの有効状態を終了時に戻す。元のPrefabや他担当コードは変更しない。現在は区間速度と頂点の短い停止を追加済み。カメラは引き続き確認用で、本番のCinemachine構成への接続は未実施。

### 現在の試走と既存機能の関係
- 経路と走行：AraiのSplineContainer／CartControllerを使用済み。
- 速度：担当側Speed Profileから区間に応じた値を既存SetSpeedへ渡す。既存GimmickSpeedChanger／GimmickSplineRange／GimmickSplineStopの衝突イベントは本コースへ未接続。将来そちらへ移す際には速度制御を一つにし、Collider／CartHitter／Range設定も揃える（試走車は現在Kinematic）。
- カメラ：現在は確認用の通常Camera。HamadaのCinemachine／CameraManagerによる切替・視線変更・揺れ等は利用可能だが本コースには未接続。既存PrefabのFPSCameraは試走インスタンスで非アクティブにしている。
- 本番構成へ移す際はカメラ参照・有効化の管理と、試走側の姿勢補正をどう残すかを整理し、同じカメラを複数の仕組みから操作しないよう接続する。今回の不具合修正で他担当コードを変更したりカメラ構成を切り替えたりはしていない。

## RollerCoasterシーンと編集操作
- `Assets/YasudaHaruto/Scenes/RollerCoaster.unity` に `RollerCoaster Course` を配置済み。既存のMain CameraとCubeは維持している。
- 入口は小さく上って頂点から直線下降し、時計回りに縮小する2周のらせん、線路なし落下へ進む。その後は本資料冒頭の拡大図準拠の5区間。入口・出口の位置、寸法は仮。ワープは未設定。
- 入口14mで2m上り、直線下降の水平長48m／落差36m、らせん半径11.5→8.5m／落差28m。落下はユーザーが変更した前進4m／落差36mを維持。
- 全区間を `Ride Spline` の先頭Spline1本で表す。`SplineController2` も同じContainerを参照。線路は `Rails Before Fall` と `Rails After Fall` がそれぞれ同じSplineの前後区間を生成する。現在の落下区間は約211.651〜248.665mで、線路は存在しない。
- `RollerCoaster Course` を選び、Inspectorの各長さ／高さ／半径を調整して **設計値からコースと線路を再生成** を押す。これはノットの手編集を設計値から置き換える操作。
- ノットの位置やハンドルを手で調整した場合は、同じInspectorの **現在のSplineから線路だけ再生成** を使う。落下前後の距離も再計算する。境界は生成時のノット番号で管理するため、この操作ではノットの追加・削除・並べ替えは対象外。構造を変える場合は設計値から再生成する。
- Course選択時、頂点・らせん始終端・落下始終端・入口／出口のラベルがSceneビューに表示される。オレンジの線が線路なし落下の経路。
- 形の生成と線路更新はUndo／Redo対応。更新後のScene保存は通常のUnity操作で行う。初回の配置は保存済み。
- 形はSceneビュー、走行はPlay中のGameビューで確認する。編集モードの既存カメラは元の位置を維持している。
- 既存のArai/TrokkoController.prefabの表示部分はCubeの仮モデルだったため、実モデルの車輪の高さ合わせは未実施。

### コース生成の検証
- 最新調整後も、図の5区間の平面座標／高低差の向きと全経路frameを検証済み。急降下の延長とらせんの短縮を実距離で比較。Play Modeで着地停止、停止中のPause／Resume、速度2.5の発進、全経路走行、出口停止とRestartを確認。実行時Error／例外なし。記録はTemp/RollerCoasterTrialValidation/landing-tuning-play.txt。
- 最新の拡大図について、6つの区間境界の俯瞰座標と、黒／赤／青／赤／青の高さ変化を各区間200サンプルで確認。全経路3,001点のframe、前半ノット保持、線路なし落下も通過。Sceneビューを拡大して図との対応を確認。
- 区間速度を有効にして実Play Modeで入口から出口まで試走。頂点停止、Pause／Resume、変化する速度、落下カメラ、出口停止、Restartを確認。テレメトリの正の速度は6〜23.994。Error／例外なし。試験用の正速度最小値集計はdomain reloadで初期値が0になる問題を修正し、実テレメトリからも加減速を確認した。記録はTemp/RollerCoasterTrialValidation/diagram-speed-play.txt。
- 2026-11-17追加：帰路半径12→26m／振れ3→1mを反映。入口〜落下終端40番の実ノットを保持。現在の落下後Meshは96,462頂点。落下の視線反転原因として、経路の水平接線が逆方向になる区間を確認。離陸方位を保持する修正後は落下前後2,001サンプルで方位反転なし。
- 修正後も全経路をPlay Mode試走し、カメラの方位維持を実測。落下中の最大観測更新間角度差は約0.2094度。Pause／Resume、出口停止、Restartを再確認し例外／Errorなし。検証後は編集モードへ戻した。
- 2026-11-17、帰路変更前の入口〜落下終端40番ノットを完全に保持したことを確認。帰路は入口側へ戻る配置。全経路3,001点と落下区間の線路欠落を再確認。現在の落下前153,324頂点／落下後70,632頂点。
- 起動済みUnityのPlay Modeで、入口かららせん・落下・帰り道・出口まで実際に走行。Pause／Resume、出口への到達と停止、Restart、乗車画面を確認しTrial Ride COMPLETE。実行中の例外／Errorログなし。検証後は編集モードへ戻した。ログはTemp/RollerCoasterTrialValidation。
- 起動済みのUnity内で生成・保存。1本の開いたSpline、2周の時計回り／縮小／連続下降、入口の上りと直線下降、全接続の接線方向、落下区間の線路欠落、着地後の平坦と2つの山を検証。
- 全経路3,001点で位置・接線・upの有効性を確認。落下前153,324頂点、落下後47,616頂点。試験用複製で手編集維持、2区間一括Undo／Redo、不正値時の出力保持、元メッシュへの非干渉を確認。試験用生成Meshは除去済み。
- 初回の形作成時は既存Sceneの全オブジェクト／Prefab／環境設定のYAMLブロックが作業前と同一（SceneRootsへの追加を除く）。帰路変更・試走追加後も既存Main CameraとCubeの保存データが不変であることを確認。

## 最初に試す
1. Unityの `Tools > Roller Coaster > Create Rail Sample` を選択する。直線寄りの区間、カーブ、坂を含む試作用Prefabが現在のSceneへ配置される。Sceneの保存は自動では行わない。
2. 配置された `RailSplineSample` の `SplineContainer` を選び、Unity標準のSpline編集でノットを動かす。
3. 同じオブジェクトの `RailSplineMesh` で **線路を再生成・保存** を押す。自動更新ではないので、経路や生成設定を変えた後は再生成する。
4. 継ぎ目や曲がり方を確認する。再生成はUndo／Redoに対応。SceneまたはPrefabの変更は通常のUnity操作で保存する。

保存済みサンプルは `Assets/YasudaHaruto/Prefabs/RollerCoaster/RailSplineSample.prefab`。生成済みメッシュを持つため、Prefabを置くだけでも表示できる。走行用Cartや囲いは含まない。

## 既存の走行ルートで使う
- サンプルを配置し、`RailSplineMesh > Spline` を、Arai側の `SplineController2 > Spline` と同じContainerへ変更して再生成する。サンプル自身のSplineContainerは、この指定後は線路生成に使用されない。
- Containerの先頭のSplineを参照する。初期構成は1 Containerに走行用Spline1本。分岐先は別の生成コンポーネントで扱う。
- 素材は担当側の `Data/RollerCoaster/RailStraight.asset`。元FBXの `rail_straight` だけを抽出し、展示用の配置位置を除いて回転・Scaleを反映したもの。元FBX・Importer・マテリアルは変更しない。
- 線路素材の実寸は幅約2.527m、高さ約0.220m、長さ約3.034m。初期のSource Rotationは0、Source Scaleは1。
- `Cross Section Offset` はSplineの右／上方向へのオフセット。サンプルのYは約-0.110222mで、素材上端を経路高さへ合わせている。これは仮の基準で、実際のトロッコとの車輪・高さ合わせは未確認。

## 調整できる項目
| 項目 | 内容 |
| --- | --- |
| Spline | 走行と共有するSplineContainer。先頭のSplineを使用 |
| Source Mesh / Materials | 曲げる素材とサブメッシュ順のマテリアル。サンプルは設定済み |
| Source Rotation / Scale | 素材を進行+Z、上+Yへ合わせる。Scaleは各軸とも正の値 |
| Cross Section Offset | 素材断面のBounds中心を基準に、横／高さをmで調整 |
| Start Distance / End Distance | 曲線に沿ったワールド実距離(m)。End=0はSpline終点 |
| Max Segment Length | 素材を長手方向へ切る最大間隔(m)。初期0.2。小さいほど細かくなる |

既存のStart Offset／Gimmick Point等は「距離相当値÷全長」をSpline評価値にする方式。この生成範囲の実距離とは同じ数値として扱わない。生成機能は既存走行・イベントの解釈を変更しない。

## 保存・制限
- 生成結果は `Data/RollerCoaster/Generated` 配下のMesh資産と、コンポーネント直下の `Generated Rail` に保存する。再生中の毎フレーム生成は行わない。
- 再生成は管理している出力だけを置き換える。手置きオブジェクトをGenerated Railの中へ入れない。生成rootを別の親へ移動すると再生成を止めて通知する。
- 生成失敗時は以前の出力を保持する。長すぎる区間・細かすぎる分割は頂点数上限60万で止める。線路幅に対して急すぎるカーブは内側が潰れるため調整が必要。
- Undoと複製したPrefabの安全のため、再生成ごとに新しいMesh資産を作る。古い資産は自動削除しない。整理はUndo不要かつScene／Prefabから未参照と確認した後に行う。
- 最後の素材は余りの長さで切り詰める。素材全体の引き伸ばしや終端キャップの追加はしない。
- 囲いは曲げない。Collider、分岐形状の自動接続、対向車、落下、カメラ演出は今回の対象外。
- Source Meshの差し替えは、長手軸と始終端断面が一致する静的な三角形メッシュを前提とする。UV各チャンネル、頂点カラー、法線、接線、サブメッシュを引き継ぐ。

## 今回の確認
- Unity 6000.3.23f1内で実FBXを読み取り、素材の両端断面の一致、カーブ／坂での生成、法線・UV・複数マテリアル、始終端の実距離、末尾切り詰め、Transform、失敗時の出力保持、手置き物の保持、Undo／Redoを検証。
- 保存済みサンプルを別のUnity起動で読み直し、Mesh資産の参照を確認。サンプルの生成頂点数33,120。
- 元のマテリアルによる全景とカーブ拡大を描画して目視確認。画像と検証ログは `Temp/RailSplineValidation`（Git共有用ではない）。描画バッチは画像出力後も終了待ちとなったため、その検証専用プロセスを終了した。
- 実トロッコのPlay Mode走行、車輪との最終位置合わせ、実際のステージへの配置は未実施。次は試作用の複製構成で、同じSplineを走行する車体との高さ・姿勢を確認する。

## 走行・着地のカメラ揺れ（2026-11-17）
- Trial RideのRollerCoasterCameraMotionからHamadaのCameraShake / CinemachineRotationExtension、既存6D Wobble Noiseを直接利用。原本は変更しない。
- 速度と坂の向き・傾斜変化で揺れを調整し、停止時に弱め、空中のレール振動を抑える。着地で一度だけ0.5秒の減衰揺れ（縦2.5度、横・ロール0.45度）。
- Trial RideのCamera Motionで強さ・速度基準・坂による補正・着地時間と回転量を調整。Noise基準Amplitude=0.18、Frequency=1.5。
- 独立したVirtualCameraと手動更新Brainを追加。車体姿勢更新後にカメラを更新。試走中の他Camera/Brainは終了時に復元する。
- 実Play Modeで全経路完走、揺れのCinemachine出力、着地一回、停止・発進、Pause、Restart、落下視線を確認。実行時Errorなし。

## 本番ステージへの接続
- 経路・Mesh・速度値・停止時間は再利用可能。既存CartController.SetSpeedを使用している。
- TrialRideには自動開始、他カメラ停止、確認GUI、車体姿勢補正があるため、本番では入口／出口イベントへの接続が必要。
- SpeedProfile.Tickは毎フレーム速度を設定する。既存Stop／Battle／Rangeの停止を上書きしないよう、速度の所有権を切り替える。本番ステージへの統合・検証は未実施。
- 既存CameraManagerで管理する場合はCameraMotionのManage Camera Activation / Manual Brain Updateを外し、既存VirtualCameraとCameraShakeへ参照を設定。BeginRide/EndRideを入口／出口から呼び、車体姿勢との更新順を統合先で確認する。
- 旧節の「カメラ演出は対象外」は線路生成単体の範囲。現在の試走構成には上記カメラ演出を追加済み。

## 囲い配置と減速・発進（2026-11-17）
- RollerCoaster Course > Track Enclosuresに配置設定。既存duct_lane.fbxのduct_straightを担当側DuctStraight.assetへ抽出し、変形せず168個配置。原本FBX・Importer・Texture・Materialは未変更。
- Width/Height=4.8m、Center Height=1.8m、Spacing=2.2m、Overlap=1.4m。囲い床はレール中心から約0.6m下。継ぎ目を重ねて曲線に追従させ、落下区間は空ける。Colliderは追加しない。
- 元Atlas内の緑・青・桃色模様を、専用MaterialのTexture Scale/Offsetで使い分ける。5枠のPaletteを固定Seed=173で並べ、同色は1〜3個まとまる。シャッフル袋で色の偏りを抑え、毎回の再生で色を変えない。MaterialやSeedを変更可能。
- 設定変更後は「囲いを再配置」。Spline変更後も手動で再配置する。Generated Enclosures内だけを置き換え、Undoに対応。手置きした装飾は生成rootの外に置く。Meshは全個体で共有し、Spline Meshのような頂点変形はしない。
- Speed Profile: Entry Speed=6からCrest Approach Speed=0.65へ徐々に減速。頂点で0.7秒停止後、Crest Launch Seconds=1.3秒で発進して下りの加速へつなぐ。
- 着地後は1.2秒停止、Landing Start Speed=0.6からLanding Acceleration Seconds=4.5秒で平坦速度10へ戻す。Pause中は加速時計も停止する。
- 囲い内の最終照明・装飾、厳密に密閉した継ぎ目、当たり判定、本番ステージへの統合は別途調整。囲いは直線モデルを重ねる方式のため、角部分の形は残る。

## 滑らかな囲い・落下の囲い・右回避演出（2026-11-17）
- Track EnclosuresのSmoothを有効化。通常部分はMesh Step=0.25mごとの四角断面を共有位置で接続し、壁・天井・床の継ぎ目をなくす。元の断面寸法とTextureを使い、23,072頂点を自動生成。モデル原本の頂点数を手作業で増やす必要はない。
- 色は既存5枠Paletteと固定Seedを維持。約3mを単位に1〜3単位のまとまりを作る。元の緑・青・桃色模様を使い分ける。
- 急な落下部分は一定断面を無理に曲げず、入口／出口に同一断面の開口を持つ縦長の囲い空間に接続。落下中も外壁あり。線路なしの落下は維持する。入場口とコース出口は開口したまま。
- 着地後の最初の平坦区間へOncoming Cart Encounterを追加。左へ続く見かけの線路と対向車を置き、実走行は右へ3m膨らんで戻る固定経路。分岐切替・プレイヤー入力は使わない。囲いはこの区間だけ広げて両方の線路を収める。
- 対向車の移動は自車の進行に同期するため、停止中は対向車も停止し、Restartで再演出できる。仮の車体・車輪・ヘッドライト・乗員を担当側で用意。完成モデルへ差し替える場合はOncoming Cartの参照先を変更する。
- Oncoming Cart EncounterのApproach Endで対向車の到達位置を調整。Right Offset変更後は「回避経路と囲いを更新」。更新対象は演出区間の41〜44番ノットで、区間外の手編集を維持する。Original Left Routeは左側線路／囲い空間の基準として保持する。
- コース全体を「設計値から再生成」すると右回避経路は元に戻るため、その後で回避経路と囲いを更新する。演出区間のノット構成自体を変更した場合は境界と元ルートを再設定する。
- 仮照明・試走構成で確認。最終モデル・音・演出位置の最終調整、本番ステージへの統合は未実施。囲いColliderは本番には追加せず、幾何検査にだけ一時利用する。

## 背景表示・縦揺れ・左通路の修正（2026-11-17）
- ユーザー確認: 自車は直進気味で、左の線路と壁で左へ進むと思わせる。左ルートは再合流せず別方向へ伸び、自車だけ右から既存アップダウンへ進む。
- Ride Cinemachine CameraのNoise Profileを担当側RideVerticalNoise.assetへ変更。既存6D Wobbleを複製し、Position NoiseのY Amplitudeを1.8倍、Orientation NoiseのX Amplitudeを1.25倍。周波数と他軸は維持。Trial Ride/Camera MotionのLanding Pitch（Hamada VHShakeのverticalIntensity）は2.5→3.5度。共通ProfileとHamada原本は変更しない。
- 囲いの法線を同じ壁の断面間で平均化。床・壁・天井の角は維持。UVは各壁の実寸と実際の曲線長から計算し、広い壁へ一枚の画像を引き伸ばす方式をやめた。
- 専用Shader YasudaHaruto/RollerCoaster Enclosureを追加。囲いは簡易陰影で表示し、旧Materialの視差・法線マップ・強い影が生む黒い帯や近接歪みを除く。元Atlasを使い、色ごとの範囲内で反復・反転して継ぎ目を抑える。緑は余白を含まない範囲へ切り直した。原本Textureは変更しない。
- DuctColor1〜5のScroll Velocityを(0.012,0.004) cycles/sec、Tile Meters=3に設定。Shader内のTimeでスクロールするため、共有MaterialのOffsetを書き換える毎フレーム処理は追加しない。色の境界は移動せず模様だけが流れる。停止したければScroll Velocityを0へ。
- 落下室を着地後の分岐まで拡張し、前方／左右の余裕を増やした。落下中間地点で前方12m超・左右5m超を検査。Fall Clearance初期6m。床は着地点の約0.7m下にし、着地後の部分には低い天井を追加。落下経路そのものや落下カメラの方位は維持する。
- 左ルートは分岐点から左へ曲がり、さらに奥の見えない通路へ伸ばす。囲い左壁へ開口を作って専用トンネルと終端壁を接続し、壁越しに対向車が見えないようにした。右回避経路は初め直進気味、分岐直前から右へ膨らんだ後に既存帰路へ接続。左との再合流はしない。
- Approach End=0.17、Arrival Ratio=0.1。自車が大きく右を向く前に対向車が正面左へ来るよう調整。値はOncoming Cart Encounterで調整可能。接近は自車の進行同期なのでPause/Restartに対応。

## 狭めた落下通路・右を隠す演出・縦揺れ再調整（2026-11-17）
- 最新ユーザー指定を優先: 見た目は左だけへ進めそうで、対向車が来た直前に右へ逃げる。目立つ青い箱を置く見せ方は廃止。
- 落下室の内側へ幅6.8mの縦通路・天井・正面の出口壁を追加。下の通路の天井は正面壁の向こうへ接続し、落下中に天井上面を見下ろせない。中間地点の横幅・前方距離、天井上面への遮蔽を検証する。外側の落下室は外壁として残すが、乗車視点は内側の通路で囲う。
- 左の線路は入口から強く左へ曲げ、左通路の壁を分岐から約6m地点まで手前へ延長。内壁は周囲と同じ緑Materialへ揃え、室内で二重になる青い通路天井は生成しない。奥の線路と対向車の出発位置を曲がり角で隠す。
- 右の内側壁を落下通路の出口壁から続け、右の逃げ道を手前から隠す。右回避区間のレールはConcealed Right Trackへ分離し、Begin+0.002の進行で表示する。壁とレールの表示制御を合わせ、接近中に右奥のループが見えることを抑える。走行用Splineと左の見かけの線路は別のまま。通常の分岐操作は使用しない。
- 対向車はApproach Start=0.42、Approach End=0.12、Approach Lead=0.04、Arrival Ratio=0。移動の短い区間から開始して補間時間を延ばし、SmoothStepの急な速度ピークをやめた。自車進行に同期するためPause中は停止し、Restartで再演出する。
- 専用Noise Profileの上下Yは共通元の2.4倍（前回1.8倍）、縦の回転Xは1.5倍（前回1.25倍）。Airborne Strengthは0.025→0.3。着地の縦衝撃3.5度は維持。
- RollerCoasterVerticalNoiseExtensionを追加。Cinemachine Noise出力のカメラ上下方向だけを増幅し、通常の下りはDownhill Vertical Scale=1.5（傾斜に応じて補間）、落下中はFall Vertical Scale=1.25。横方向・車体姿勢は変更しない。Trial RideのCamera Motionで調整できる。
- 回避経路やレールを再生成したら、Oncoming Cart Encounterの「回避経路と囲いを更新」で右レールの分離表示も再設定する。
