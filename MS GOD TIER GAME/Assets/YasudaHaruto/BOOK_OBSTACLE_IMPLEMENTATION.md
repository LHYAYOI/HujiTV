# インク汚れ1種類・4枠の実装

2026-11-11更新。解除方式は方向を問わない擦り距離による全体薄化に統一。旧Rub／Scratch切替の説明より本記録を優先する。

## 設定と表示

- BookGameBootstrapのObstacle Settingsへ単一のObstacleDataを設定する。未設定ならResources/BookObstacles/Rubを使用する。旧アセットのGUIDとパスを維持したため名前はRubのまま。
- 画像はResources/BookObstacles/InkSplat.png。黒いインク飛沫の透過PNGを新規生成して設定済み。Colorは白（画像の色をそのまま使う）。
- Required Distanceは汚れ表示の短辺を1とした清掃距離。初期値4。Minimum Movementは手ぶれを無視する距離で、初期値0.003。
- 縦・横・斜めの同じ物理距離は、同じ清掃量になる。押下と押し直しの座標差、押しっぱなしの時間、キャンセル後の移動は加算しない。
- 透明度は「設定色のalpha × (1 - 清掃進捗)」。画像のalpha形状はそのまま残し、画像全体が滑らかに薄くなる。部分的な削り取りは行わない。
- 表示は1枠1枚・4頂点。64×64マスクと細分メッシュは撤去。画像のRead/Write Enabledは不要（OFF）。
- 画像の縦横比を保ち、既存の枠内に収める。1〜3番枠は右ページ、4番枠は左ページ。Right Obstacle Areaは右ページ内の左下原点の正規化領域。
- 汚れは撮影用BookPageViewの子へ置き、ページと同じRenderTextureへ描画する。めくり元のLive Turn複製にも元ページの状態を渡すため、めくり中も紙面と一緒に表示する。移動先・めくり取消時・閉じた状態での撮影にも対応する。
- Ink.assetは旧GUID保護のため残すが、種類として検索・ロードしない。ランタイムが使う設定は上記1か所。種類enum・辞書・解除方式の基底／派生クラスは撤去済み。

## PC側の付与

    bool accepted = server.SendDirt(playerSlot, slotCount: 2);

- 一度に1〜4枠を埋め、同じ汚れを各枠へ個別に生成する。
- APPLY_DIRT=10、uintのイベントID＋byteの枠数。枠数は1〜4。戻り値は送信受付であり付与成立の承認ではない。
- 旧APPLY_OBSTACLE=6の種類IDを枠数へ黙って読み替えていない。新しい要求はメッセージ値10へ分離する。PCと本は更新したビルドを使う。
- 旧形式のID1は1枠、ID2は2枠の同じ汚れとして受信できる。旧SendObstacle呼出しもID1／2のみ新SendDirtへ転送する互換窓口として残す。
- ServerのSpaceキー確認用入力はSendDirt(PLAYER1,1)へ変更。敵スクリプトから呼び出す接続自体は未追加。
- 種類設定を通信せず、付与枠数は攻撃の要求として渡す。イベントの重複・古い順序は無視する。
- めくり中は最大32件を受信順に予約して完了後の見開きへ適用。通常見開き以外では破棄する。
- 接続状態変更で未適用予約とイベント比較基準をクリア。付与済みの枠は保持する。送信保存・再送・解除通知・PCとの状態同期は未実装。

## 確認

- Unity 6000.3.23f1の別出力コンパイル、および同版UnityのバッチでBookObstacle COMPLETE／BookObstacleRuntime COMPLETEを確認。初回のメッシュ検証で反射の同名メソッドを誤選択し、DeclaredOnly指定へ修正して再実行した。
- 検証記録はTemp/ObstacleValidation/result.txt、runtime-result.txt、Temp/DirtValidation/unity-final.log。入力は合成ポインターによる検証であり、物理タッチ・Android実機・実PC間通信とは区別する。
- バッチのScreenCaptureではゲーム画面PNGを取得できず、最終見開きの目視確認は未実施。元PNGの見た目と実ファイルのalpha、Unity設定への接続は確認済み。

## 生成素材の記録

- Built-in image_genを使用。元画像はC:/Users/ths30546/.codex/generated_images/01a18080-f447-7821-93e1-c20650ed9d1e/exec-a0943e5b-549e-4db4-935e-83079dcdb6d2.png。コピーしてプロジェクトへ取り込み、元画像は保持。
- プロジェクト保存先：Assets/YasudaHaruto/Resources/BookObstacles/InkSplat.png。透過alphaを保持し、Unityでは最大1024・mipmap OFF・Read/Write OFF。

生成プロンプト（原文）：

Use case: stylized-concept. Asset type: transparent PNG texture for an ink stain obstacle on a fantasy spellbook game page. Generate ONE isolated irregular black ink splat viewed exactly flat from above. A broad dense opaque central puddle, organic lobed ragged contour, several short radial splashes and a few nearby droplets, subtle natural ink pooling texture. Deep charcoal-black ink, largely matte, high opacity in central area to obscure text beneath. Compact roughly circular silhouette occupying about 85 percent of the square canvas, all droplets inside bounds with transparent margin. Actual transparent alpha background, no paper, no surface, no checkerboard baked in, no backdrop, no drop shadow, no lettering, no border, no objects. The whole stain will gradually fade uniformly as player rubs it. Clean production-ready 2D game texture, readable at small size, avoid thin long spikes and excessive tiny disconnected specks.
## デバッグ画面

Tools > YasudaHaruto > Obstacle Debug Windowを開き、BookSceneをPlay Modeにする。「一度に埋める枠数」を1〜4で指定して「お邪魔を追加」を押す。表紙・固定ページなら最初の通常見開きを開く。通信接続は不要。各枠の清掃進捗と4番枠による制限も表示する。めくり・編集・一時停止中は追加できない。

2026-11-11修正：Bootstrapと本管理が別GameObjectの場合にも、Bootstrapが保持する実際の参照を取得して追加する。通常見開きに対してPreview(2)で2枠追加するPlay Mode回帰検証が通過。追加できない場合は画面に参照・初期化・一時停止の理由を表示する。


2026-11-11配置・メニュー整理：1〜3番枠は右ページ内の右上／中央左／右下へ固定し、従来の横帯より大きく表示する。4番枠も左ページ内で少し拡大。縦横比を維持する。Verify系10項目と重複Preview Obstacleの4項目はTools登録を削除し、日常確認はObstacle Debug Windowへ集約。内部検証メソッドはメニュー登録なしで保持する。

## RenderTextureへの描画（2026-11-11）

- YasudaBookViewControllerが各撮影のShowPage直後に装飾処理を呼ぶ。通常撮影・Live Turn・ページ追加候補・めくり取消時の復元で、それぞれ対応するPageInstanceを使用する。
- BookObstacleControllerが撮影ページ内へPage Stainsを生成し、汚れを配置・薄化する。画面用Overlay Canvasは生成しない。Layer・マスク・Cameraは撮影ページに従う。
- 通常表示の入力矩形は、左右それぞれのCropを使って実際のGraphicから画面へ投影する。外へ出て戻る操作の再開も維持。
- 変更があったCameraだけを記録し、そのCameraの描画完了後にFlipView.MarkDirtyを通知する。変化のないフレームで無条件に再描画を要求しない。
- BookObstacleRuntime COMPLETE。通常撮影RTとLive Turn撮影RTの不透明なインク中心を画素読み戻しで確認。元ページの3汚れ維持、移動先への混入防止、戻った後の入力、薄化、外からの再入場、4枠目解除が通過。合成ポインターと実RenderTextureによる検証。Android実機と物理ドラッグの体感確認は別途必要。