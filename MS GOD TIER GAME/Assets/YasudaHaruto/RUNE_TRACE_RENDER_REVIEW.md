# なぞり中の本再描画・分離案

2026-11-11。ユーザー依頼は現状調査と実装案の検討。ゲームコード・Scene・Prefab・設定の変更、実機プロファイルは行っていない。

## 結論

なぞり判定そのものに本の再合成は不要。しかし現行は手描き線も撮影用Canvasに入れて本の合成を通しており、線の表示更新には本の再合成が必要な構成。BookRuneCaptureRefreshの通知だけを止めると、以前の「線が撮影RTにあるのに本に出ない」問題へ戻る。

最初の変更は、動く手描き線のみ画面側の独立Canvasへ分離し、判定・お手本・ページ・汚れは維持する案を推奨。Camera描画と本合成の削減を別々に評価する。

## 確認した現行経路

1. BookGameDragInputが画面入力をページ座標へ変換し、RuneTraceController.AddPointへ渡す。
2. AddPointは正規化距離0.005未満を無視し、有効な点では判定・逸脱色・表示データを更新。
3. RunePageView.UpdateCurrentStrokeが点列をコピーし、RuneStrokeDataを新規作成して完成済み線と合体。RuneLineGraphic.SetStrokesが頂点更新を要求。
4. BookRuneCaptureRefreshは入力先RunePageView配下のRuneLineGraphic（お手本と手描き線）の頂点／Material変更を購読し、更新待ちフラグを立てる。
5. 撮影CameraがCurrentTextureへ描画し終えた後、BookRuneCaptureRefreshがFlipView.MarkDirtyを呼ぶ。ポイントごとにその場でGPU合成するのではなく、撮影単位へまとめて通知する。
6. MarkDirtyはcontentVersionを増加させ、BookFlipView.NeedsRedrawが変更を検出。LateUpdateで本の再合成・仕上げ・後処理へ進む。
7. モバイルは分割パスと静止画の分割描画も使用する。描き終わる前に内容が変わると次の描画を予約する。連続入力が再合成を継続させ得る。

11/5のなぞり線表示復旧でこの更新通知を導入した記録がある。今回新たに無条件更新が加わったわけではない。

## 描画負荷を分ける

- 手描き線のメッシュ更新：点数に応じたコピー、新規List生成、線全体の頂点再構築。CPU／GCの候補。
- ページ撮影：YasudaBookViewControllerは撮影後もCameraを有効のまま維持。保存済み撮影RTは1600×900。線更新がなくてもCamera描画は別に発生し得る。
- 本合成：BookFlipViewは変更時のみ再描画、保存SceneのIdle Refresh Rateは0。線変更通知はそのキャッシュを更新する入口。
- モバイルの出力上限720、分割パス有効、静止画分割6段、Bloomのモバイル反復3という保存設定。これらは描画量・遅延に関係するが、実際の端末設定・実行FPSを今回測定したものではない。
- 暗転／魔法スタイル等にも別の更新条件があるため、線を外せばすべての本描画が必ず0になるとは断定しない。
- 汚れの更新通知は汚れの表示変更に対応する別経路。通常のなぞり点更新とは独立。

Androidの重さの主原因が本合成、Camera、CPU、GCのどれかは未測定。連続再合成を要求する経路が存在することと、性能の支配要因が確定したことは区別する。

## 案の比較

### A：手描き線を画面の別Canvasへ表示（第一候補）

- 本のRenderTextureにはお手本、紙面、汚れを維持。動く手描き線は入れない。
- RunePageViewの点列・逸脱色・クリア通知を独立した線表示へ送る。既存のRuneLineGraphicのメッシュ生成を再利用できる。
- 元のRuneArea／GraphicのRectは座標変換用として残す。元Graphic自体をそのまま画面Canvasへ移すと既存の投影／完成演出の計算が壊れるため、基準Rectと描画先を分ける。
- 画面側の線は既存BookFlipCoordinateMapperによる投影位置へ合わせる。解像度・Canvas倍率・本の位置変更に追従。ページ領域からのはみ出し対策としてMaskableGraphic化または同等のクリップ処理を検討。
- 色・線幅・完成済みストローク・失敗時消去・押下中の新規点を維持。
- 本の紙質、にじみ、陰影、Bloomが手描き線へ自動では適用されなくなる。線の見た目の一致が必要なら、線専用の軽量な表現を追加する。
- 完成／失敗演出は既存のScreen Space Overlay方式へ接続する。完成時に線が二重に残らないよう、表示と演出の引き渡しを明示する。
- 4番枠付与、切断、ページ離脱、フォーカス喪失時に線を消す。お邪魔のRenderTexture描画と独立させる。
- めくり開始時の線をどう見せるかは決める。隠す／試行取消が最小構成。線も紙と一緒にめくる必要があるなら開始時だけ撮影側へ焼き込み、めくり取消時には二重表示しないよう復元する。
- なぞり中に元の撮影用Graphicへも毎回書く方式は採らない。単に通知購読を外すだけでは、別イベントで再撮影された際に古い線が混ざる。

### B：透明な線専用RenderTextureを最後に重ねる

本と別の透明RTへ線を描き、本合成の後で重ねる。本の重い再合成からは切り離せるが、追加Camera／RT／透明合成が必要になる。専用のフィルターや複雑な描画が必要な場合の候補。現在の線表示ならAより構成が増える。

### C：現在の方式で本更新の頻度だけ下げる

実装量は小さいが、線の表示遅延が増え、本の再合成は残る。負荷測定用の比較または短期的な妥協案。追従性が大切ななぞりの本命にはしない。

## 推奨する実装段階

1. 線の表示先だけ分離。判定の点数や許容値、通信、汚れのRT化を変えず、線由来の本更新を止める。
2. 実機で停止中／なぞり中のCPU・GPUフレーム時間、GC、撮影Camera、本合成を比較。
3. Cameraがなお重ければ、ページ撮影を必要時だけにする第2段階へ進む。ページ変更、汚れ、クールダウン、動くMaterial、Live Turnなどの撮影要求を扱う。
4. CPU／GCが残る場合に、RunePageViewの点列コピーと一時RuneStrokeData生成を削減する。判定精度を変える間引きは最初から行わない。

Cameraの必要時描画では、Material.SetFloatによるクールダウン変化がGraphicの頂点変更通知とは別である点に注意する。頂点Dirtyだけに依存してCameraを止めると演出が止まる。動的な紙面要素から明示的に撮影要求を出す構成が必要。

## 検証の基準

- 先行するページ初期描画・分割描画が完了してから、停止中と連続なぞりを比較。
- なぞり線はフレームごとに追従し、その更新を理由にFlipView.RenderCountが増えないこと。ただしRenderCountは本の描き直し回数の指標であり、分割描画の全パスやCameraの時間まで表す数値ではない。
- 完成済み線を含む色、線幅、画面位置、クリア、失敗、完成演出、方向選択、4番枠到達、解除、ページめくり取消を確認。
- Androidの同一画面／解像度／Graphics APIでGPU・CPU時間とGCを比較。FPS改善量は実機結果で判断する。
- BookFlipView等の他担当ファイルを変えずにAを担当側で試作できる。共有Shader側の合成変更が必要になるB等は変更範囲を改めて検討する。

## 主な参照

- Scripts/Book/Validation/BookRuneCaptureRefresh.cs
- Scripts/Book/Validation/BookGameBootstrap.cs
- Scripts/Rune/Runtime/RuneTraceController.cs
- Scripts/Rune/Runtime/RunePageView.cs
- Scripts/Rune/Runtime/RuneLineGraphic.cs
- Scripts/Rune/Runtime/RuneSpellEffect.cs
- Scripts/Book/Validation/YasudaBookViewController.cs
- Assets/hatake/book/scripts/BookFlipView.cs（参照のみ）

## 実装済みの範囲（2026-11-11）
上記の手描き線分離案を実装。RunePageView.ConfigureTraceOverlayをBootstrapから設定し、元のPlayerGraphicは座標基準として残す。手描き点列と逸脱色は独立Canvasだけへ反映し、失敗・完成・取消で消去する。めくり等で本が利用できない間は非表示にする。
現在のBookSceneでRuneSpellValidationを実行し、手描き中の元Graphic Dirty通知なし・本RenderCount増加なし・完成／発射までの回帰を確認（RuneSpell COMPLETE）。Android実測と撮影Cameraの必要時更新は未実施。
