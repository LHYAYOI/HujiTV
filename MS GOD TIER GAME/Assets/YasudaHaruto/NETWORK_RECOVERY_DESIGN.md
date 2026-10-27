# 通信復旧・自動接続の調査と設計案

2026-10-27。第1段階を実装し、既存Unity EditorのPlay ModeでローカルUDP切断・再接続を検証済み。Android実機検証と第2・第3段階は未実施。下記「前提」は変更前の調査記録。

## 確定仕様と第1段階の実装結果

- ユーザー承認：切断中はページめくりだけ許可。描画途中・完成後の発動待ち・方向スワイプは取り消し、復帰後は最初からなぞる。所持と閲覧位置は維持する。
- 最後の有効なIP／PLAYER_SLOTを同じアプリ起動中だけ保持。接続失敗・通信断後に1→2→4→8秒、以後8秒の待機で成功まで継続する。待機と接続タイムアウトはunscaled time。1試行の接続タイムアウトは既存Inspector設定（初期5秒）を使用するため、待機間隔とは別にかかる。
- 手動Disconnectと破棄は再試行を停止。初期化前Connectにも対応。無効なIP／プレイヤー枠は新しい接続要求として受け付けない。
- 切断検出は既存Transportの設定を維持。Wi-Fiを切った瞬間ではなく、Transportが切断を検出した時点でルーンを停止する。検出を早めるタイムアウト調整は今回しない。
- 入力制限はRuneTraceControllerでも行い、別ページのBeginTraceや方向Overlayからルーン入力・発動が復活しないようにした。切断時に保持中のルーンポインターとBookInputControllerの入力占有を解除する。進行中のページドラッグは維持する。
- 開始済みクールダウンは切断でリセットしない。自動発動直後に指離しを失ってもタイマーが止まらない。RuneTraceInteractionが残り時間の終了時刻を保持し、ページを離れて戻っても経過分を引いて復元する。通常のページ移動にも適用される。時間基準は従来どおりゲーム内時間。
- BookConnectionStatusViewをBootstrapが実行時生成。切断中は「再接続中… ページめくりのみ操作できます」、明示切断時は未接続と表示。接続後は非表示。背景と文字はraycastTarget=false、GraphicRaycasterなし。Scene・Prefabの配線変更なし。日本語フォントは既存の選択UIと同様にOSフォントを使用し、Androidでの表示は未確認。
- 通信Clientが存在しないBookScene単独起動は、従来のオフライン検証用として入力を維持する。通常の接続画面経由ではClientを引き継いで制御する。
- Verify Network Recoveryを追加。ローカルの簡易UDPサーバーを遅れて起動し、初回失敗→接続→サーバーから切断→再接続→同じ枠の再登録→明示停止を確認。timeScale=0、入力キャンセル、クールダウン、表示の非干渉も検証した。バックオフの2/4/8秒は切断ハンドラーの時計設定検証であり、すべて実時間待機した試験ではない。
- 最終結果はNetworkRecoveryResults.txt。既存RuneFeedback検証も同じ実行でCOMPLETE。Play Mode終了済み。保存済みScene・Prefab・共有設定・サーバー・メッセージ形式は未変更。
- 制約：今回のCONNECTEDは従来どおりTransport接続成立を意味する。サーバー登録承認・古い枠の即時引き継ぎは第2段階で実装する。Android Wi-Fi断／復帰、サーバーアプリ再起動、IP変更、実際の本のドラッグによる操作と新しい表示の見た目は未確認。

## 前提と確認済み事項

- ユーザー回答：WindowsゲームPCとAndroidタブレット。Unity Transportで通信し、現在の通信経路はWi-Fiと思われる（経路自体は未確認）。
- Transport 2.7.4。BookNetworkClient / ServerはNetworkDriver.Create()の標準UDP経路、7777番、IPv4を使用。
- ClientはDontDestroyOnLoadで本のSceneへ引き継がれる。切断後もConnectを再度呼ぶ構造はあるが、自動再試行・接続先保持がない。接続画面はBookSceneへ遷移して消えるため、ゲーム中の手動再接続入口もない。再起動必須という報告と整合するが、切断原因を実機で再現した証拠ではない。
- 接続開始の5秒タイマーはTime.deltaTime。timeScaleが0なら進まない。切断検出タイムアウトとは別物。
- ClientはTransport接続直後にCONNECTEDと通知し、その後REGISTER_CONTROLLERを送る。Serverによる登録承認メッセージはない。
- Serverは同一PLAYER_SLOTの別接続を拒否する。古い接続がタイムアウトする前に再接続すると登録拒否される可能性がある。PLAYER_SLOTだけで古い接続を無条件に乗っ取らせない。
- 導入済みTransportのRuntime/NetworkParams.csでは、reconnectionTimeoutMSはDTLS向けの経路復旧設定。切断イベント後の新規接続・プレイヤー再登録・ゲーム状態復元を代替しない。

## 方式比較

| 方式 | 利点 | 条件・制約 |
| --- | --- | --- |
| 現在のWi-Fi＋自動再接続 | 現行のUDP・メッセージを維持し、小さく改善できる | IP変更には別途発見処理が必要 |
| USBテザリング＋同じ自動再接続 | USBをネットワークとして扱い、Transportを維持できる候補 | Android側の有効化、タブレットの対応、PCとの双方向UDP、複数台接続を実機確認。ケーブル挿入だけでテザリングが有効になるとは保証しない |
| adb経由 | 開発PCから端末一覧や接続状態を取得できる | USBデバッグ・PC承認が必要。標準のforward/reverseはUDPを直接転送する方式ではなく、現在のTransportへそのまま適用しない。TCPへの変更や中継は別途必要 |
| USB Accessory等の専用通信 | 専用のUSB通信を設計できる | Android／Windows双方の対応・プロトコル・配布を追加するため、今回の第一候補にはしない |

第一候補はWi-Fiを含むIP通信の復旧。USBの実機試験はテザリングによるIP通信で行い、成功すれば同じ再接続処理へ統合する。

## 第1段階：切断後にゲームを再起動せず復旧

1. Clientに最後に検証済みの接続先とPLAYER_SLOT、接続継続の意図を保持する。手動Disconnectは再試行を止め、通信断・接続試行タイムアウトは再試行待ちへ進める。
2. 1→2→4→8秒を上限とする間隔案で一度に1接続だけを試す。時間はunscaled time。成功時に間隔を戻す。待機中にUpdate冒頭の未接続returnで再試行処理が止まらない構造にする。
3. Driverは原則保持し、切断済みNetworkConnectionだけを作り直す。旧イベントの処理を終了してから接続を差し替える。初期化前Connect、無効なIP・PLAYER_SLOT、破棄・終了、フォーカス復帰も扱う。
4. 最小改修では既存サーバーの枠解放まで再試行を続ける。CONNECTEDが登録承認を意味しない制約を明示する。これを完成形にせず、次段階で登録待ちを導入する。
5. 本のScene・所持ページ・ルーン状態を再ロードしない。切断中の魔法をためて復帰後に一括送信しない。送れなかった操作とクールダウンの扱いはCastMagicCommand側との整合を決める。

## 第2段階：登録承認とセッション復旧

- Transport接続とゲームとしてのREADYを分離。REGISTER_CONTROLLERの承認／拒否、登録タイムアウトを追加し、READYになってから送信と初回Scene遷移を許可する。
- 同じアプリ起動中の再接続用にサーバー発行のセッショントークンを持ち、同じトークンだけ古い接続を引き継ぐ案。他端末の同一PLAYER_SLOT要求は拒否する。アプリ再起動後も引き継ぐか、サーバー再起動時の扱い、認証が必要な運用かを別途決める。
- 新メッセージを追加するためクライアント・サーバーを同時更新し、プロトコル版を照合する。
- ReliableSequencedは新しい接続をまたいだ再送保証ではない。切断中のADD_PAGEを保証するなら要求ID・適用済みID・所持一覧の権威を別途設計する。同じPageIdは複数所持できるためPageIdで重複排除しない。
- サーバー再起動後に同一ゲームを継続できるかはゲーム側の状態保持次第。通信の再接続成功とゲーム状態の完全復元を分けて表示・検証する。

## 第3段階：IP入力の省略とUSB

- 接続先発見をClientの接続管理と分離。まず最後の接続先へ試行し、失敗が続いた場合にローカルネットワーク内の発見要求／応答を使用する案。
- 発見応答にプロジェクト識別子・プロトコル版・サーバー識別子・ポートを持たせる。複数候補なら選択させる。PLAYER1/2をUSB挿入順だけで自動決定しない。
- 複数ネットワーク・VPN・ファイアウォール・Wi-Fiの端末間分離を考慮し、手動IP指定は残す。USBネットワーク出現は探索のきっかけであり、サーバー実在の証明にはしない。
- USB試験：対象タブレットでテザリング対応を確認→有効化→PC側ネットワーク出現と双方IPを確認→UDP7777接続→抜去→再挿入→アドレス変更→2台同時の順。OS設定の完全自動化は確認後に判断する。

## 変更範囲と検証

- 基本はAssets/YasudaHaruto/Scripts/Network内。メッセージenum、BookGameBootstrapとCastMagicCommandの利用箇所を調査して段階ごとに変更する。
- Scene・Prefab・共有設定・他メンバー領域の変更が必要ならAGENTS.mdに従って事前相談する。
- 第1段階は再試行間隔、手動停止、タイムアウト、初回失敗、繰り返し切断、timeScale=0、破棄を検証。実機ではサーバー後起動／再起動、Wi-Fi切断・復帰、本のSceneを維持することを確認する。
- 第2段階は登録拒否、旧接続残存、別端末の枠競合、承認待ちの送信禁止を確認。従来の正常通信確認済み記録とは区別する。

## 一次資料

- Unity Transport FAQ：https://docs.unity.cn/Packages/com.unity.transport%402.7/manual/faq.html
- adb公式マニュアル（転送方式）：https://android.git.googlesource.com/platform/packages/modules/adb/+/HEAD/docs/user/adb.1.md
- Android USBテザリング：https://support.google.com/android/answer/9059108
- 導入済みソース：Library/PackageCache/com.unity.transport@98ef8bfec34d/Runtime/NetworkParams.cs、UDPNetworkInterface.cs（調査のみ、編集しない）。
