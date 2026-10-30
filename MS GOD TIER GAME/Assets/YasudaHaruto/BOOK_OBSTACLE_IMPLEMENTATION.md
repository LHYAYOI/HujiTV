# お邪魔4枠の実装と使い方

2026-10-29。合意仕様はBOOK_INTERACTION_DESIGN.md末尾を参照。

## 設定

- BookGameBootstrapのObstaclesへObstacleDataを登録する。空の場合はResources/BookObstaclesを読み込む。重複IDはエラーとして後続を登録しない。
- 仮データ：RubはID=1、1枠、左右擦り。InkはID=2、2枠、スクラッチ。Texture未設定時は単色の四角形を表示する。完成素材ではない。
- Create > Book > Obstacle Dataで種類を追加する。使用枠数、Texture、Color、解除方式と閾値を設定する。スクラッチ素材はRead/Write Enabledが必要。透明部分は解除必要面積に含めない。
- Right Obstacle Areaは右ページ内の正規化座標（左下原点）。初期値は(x=0.1,y=0.15,w=0.8,h=0.7)。3つの帯へ分け、各帯内で位置をずらす。4番枠は左ページを広く覆う。
- 今回の表示はScreen Space Overlay。めくり中は非表示とし、完了後に対象見開きの状態から復元する。本の紙質・曲面・めくり中の汚れ合成は未対応。

## 敵側からの呼び出し

敵処理から、対象プレイヤーに接続しているBookNetworkServerへ次を呼ぶ。

```csharp
bool sent = server.SendObstacle(playerSlot, obstacleId);
```

他メンバーの敵コードへの呼び出し追加は今回行っていない。戻り値はTransportへの送信受付であり、付与成立・解除確認ではない。

- メッセージ値：APPLY_OBSTACLE=6。既存の0〜5は維持。
- ペイロード：uintの付与イベントID、byteの種類ID。既存のReliableSequencedパイプラインを使う。
- 使用枠数と素材は本側の種類データから取得。PCと本で種類IDの意味を合わせる。
- 本側は同一接続中のイベントIDを比較して重複・古いイベントを無視する。接続状態変更時に比較基準と未適用予約をクリアする。既に付与済みの枠は維持する。
- 切断中の送信保存、再接続後の再送、PCとの状態同期、解除通知は実装していない。
- めくり中は最大32件を受信順に予約。超過は警告して破棄。表紙／固定ページ／編集選択中には通常見開きへの持ち越しをしない。

## 状態・入力

- PageInstance.Obstaclesが4枠を所有する。空き番号順→既存の付与順で置換先を選び、全置換先を確定後に更新する。
- 各枠のObstacleInstanceが解除処理を所有。枠間で進捗を共有しない。Clearは期待する実体との一致も確認し、古い入力が新しい汚れを消さない。
- BookGameDragInputが押下時に解除入力の所有権を確保。指を離すまでルーン／めくりへ切り替えない。上書きされた枠は古い操作をキャンセルし、押し直しを必要とする。
- 4番枠への付与はルーン入力を停止し、途中のなぞり・発動待ちを取り消す。既存クールダウンは維持する。4番枠解除で接続状態に応じて通常入力を戻す。
- スクラッチ形状は各実体内の64×64グリッドで保持する。高速移動は点間の線分で塗る。除去済みセルの重複加算なし。ページを離れても形状を保持する。
- 左右擦りは必要距離を超えた往復を数える。手ぶれや同じ向きの押し直しは進まない。

## 確認用メニュー

- Tools > YasudaHaruto > Verify Book Obstacles：状態と解除処理のEditor検証。
- Verify Book Obstacles In Current Scene：現在の本SceneでPlay Mode検証。Sceneを保存・差し替えず、終了時にPlay Modeを停止する。
- Preview Obstacle > Rub / Ink / Fill Four Slots (Play Mode)：仮データを付与。表紙／固定ページなら最初の通常見開きを開いてから適用する。Fill Four Slotsは左右擦りで4枠を埋める。通信イベントIDを変更しない。
- 検証結果はTemp/ObstacleValidation/result.txtとruntime-result.txt。

Android実機、PCとタブレット間の実通信、完成素材の見た目は別途確認が必要。
