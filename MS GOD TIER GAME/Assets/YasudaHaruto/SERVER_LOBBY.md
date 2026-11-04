# Server待機画面とゲーム開始（2026-11-02）

## 起動方法
1. PCのUnityで `Assets/YasudaHaruto/Scenes/BookServerLobby.unity` を開いてPlay。Player 1／2の登録状態が表示される。
2. タブレットは従来の `TransportClientTest` から接続する。登録成功後も接続画面で「waiting for PC」と表示して待機する。
3. 1台以上が登録済みならPCの「ゲームを開始」を押せる。PCが `BookServerGame` をロードした後、接続中タブレットへ開始通知を送る。タブレットは通知を受けて `BookScene` へ移動する。
4. 開始後に接続／再接続したタブレットにも登録承認に続いて開始通知を送る。既にBookSceneにいるタブレットは再ロードせず、従来の再接続で復帰する。

PCのビルドではBuild ProfileのScene ListにBookServerLobby（先頭）とBookServerGameを含める。タブレットのビルドではTransportClientTest（先頭）とBookSceneを含める。共有のEditorBuildSettingsは今回変更していない。Editorの待機画面からは、ビルド一覧に未登録でも保存済みのゲームSceneをロードできる。

ClientとServerを両方更新して使用する。旧版には登録応答／開始通知がないため、新旧混在の接続には対応しない。今後のゲームScene切替は、待機SceneのPersistent Book Server → BookServerGameFlow → Game Scene Pathで指定する。

## Sceneのコピー
- コピー元：`Assets/Main/Main_Game_Scene.unity`。
- コピー先：`Assets/YasudaHaruto/Scenes/BookServerGame.unity`。新しいGUIDを付与した、Scene本体の完全コピー。元Sceneは変更していない。
- コピー時の両SceneのSHA256：`0863D87D4E24E353E765ECFCC536AEC9BCCFF07B19256CD4512FFA2EA721E7E7`。
- ユーザー指示でGit操作は実施していないため、コピー時点のGit状態は未確認。
- Scene内のPrefab・ScriptableObject等の参照は元と共通。共有資産の複製・書換えは行っていない。
- BookServerLobbyは新規Scene。通信本体＋GameFlowのルートと、Sceneローカルの待機UIルートを分離する。UIは実行時生成する。

## 変更した責務
- BookNetworkServer：最初の1つだけをDontDestroyOnLoadで保持する。Driver、接続一覧、Player登録、ゲーム開始状態をScene間で維持。後からゲームSceneに生成されるServerはDriverを作らず、既存のInspector参照からのイベント購読／ページ・お邪魔送信／カーソル取得等を永続Serverへ転送する。元のBookSystemやPlayerControllerの参照を差し替える必要はない。
- BookServerGameFlow：1台以上の登録を開始条件とし、二重開始を防止。非同期Sceneロード後、Startメソッドの実行を待ってからServer.BeginGameを呼ぶ。ロード不可／開始失敗は待機画面へ表示する。
- BookServerLobbyUI：Player1／2の登録状態と開始可否を表示。切断の反映時刻はTransportが切断を検出した時点。ケーブル抜去と同時の反映を保証するものではない。
- BookNetworkClient：Transport接続後はCONNECTINGのまま登録承認を待つ。承認受信後にCONNECTED。登録拒否時は再試行を停止してSlot重複の案内を表示する。接続断で開始フラグを破棄し、新しい接続の開始通知を待つ。イベント処理→タイムアウト判定の順序は維持。
- BookConnectionUI：CONNECTEDでの自動遷移を廃止し、GameStarted受信後にLateUpdateでBookSceneをロードする。探索・Manual IP接続は維持。
- 既存メッセージ値0〜6は維持。7=REGISTRATION_ACCEPTED、8=REGISTRATION_REJECTED、9=GAME_STARTED。いずれも1byteでReliableSequenced送信。キュー不足時はServerが次のUpdateで再試行する。開始通知は接続ごとに1回キュー投入し、配送はTransportのReliable処理に委ねる。
- Serverはゲーム開始前のCAST_MAGICを無視する。TransportServerTestのような旧テストSceneだけでは新しい開始操作がないため、今回の一連の確認にはBookServerLobbyを使う。

## 確認手順
- 0台では開始不可、Player1のみ／Player2のみでは開始可能、2台なら両方が接続済み表示となること。
- 同じPlayerを2台で選ぶと2台目が登録拒否となり、Playerを変更して再接続できること。
- タブレット接続直後は接続画面に留まり、PCの開始後にだけBookSceneへ移ること。
- PC側ロード後も接続が維持され、二重Bindのエラーがなく、魔法受信・ページ追加・お邪魔送信が動くこと。
- ゲーム中に2台目を接続するとBookSceneへ進むこと。切断・再接続でも通信と魔法操作が復帰すること。
- PCとAndroidは別プロセスで試す。同一Editorで実際の接続UIを起動すると、そのSceneロードがPCゲームSceneも置き換えるため、端末2台の遷移確認にはならない。

専用自動検証はBookServerLobbyを開いた状態で `Tools > YasudaHaruto > Verify Server Lobby In Play Mode`。Play Modeへ入り、loopbackのClientと追加のTransport接続を用いて、0台／1台の開始条件、登録拒否、実ゲームSceneへのロード、同じServer・接続の維持、Driver1つ、魔法受信、途中参加、明示切断後の再参加を検証する。終了時にPlay Modeを停止する。結果はConsoleと `Temp/ServerLobbyValidation/results.txt`。自動検証の再参加は明示Disconnect→Connectであり、実ケーブル抜去や接続UIの画面遷移は上記の実機手順で確認する。

## 今回の検証結果
- Unity 6000.3.23f1のRoslynと既存Bee参照設定で、Runtime／Editor両方の別出力コンパイルに成功。Runtimeは既存の未使用メンバー警告3件。追加検証コードは初回のUnity.Collections using不足を修正してコンパイル成功。
- Unity専用バッチの起動を試したが、検証メソッド実行前に終了コード1で停止。ログはプロジェクトパス設定後の終了までしか出ておらず、原因を特定できていない。Play Mode自動検証・UI描画・実ネットワーク動作は未確認。COMPLETEの結果はまだ生成されていない。
- 既存NetworkRecoveryValidationの仮Serverも新しい登録承認応答に追従。今回そのPlay Mode実行は未確認。
- ゲーム終了・ロビー復帰・Server再起動後のセッションリセットは今回の範囲外。PCで新しいゲームを始め直す場合はPlay Mode／アプリを再起動する。
