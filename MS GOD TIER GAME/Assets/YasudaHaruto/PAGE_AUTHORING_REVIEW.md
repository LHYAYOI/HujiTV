# ページ設定・編集・文字・紙質の調査

調査日：2026-10-28。コード・保存済みScene／assetと既存UnityのInspectorを確認。ゲームコード・Scene・Prefab・Materialは変更しない。UnityはAndroid対象、BookSceneにユーザーの未保存変更あり。保存・再読み込み・Play Mode実行はしていない。

## 1. ページ設定が二重になる原因

現在の参照：
- BookGameController.Initial Pages → PageData（ゲーム用1見開き）→ LeftVisualData / RightVisualData（BookPageData各1枚）。
- YasudaBookViewController.Book Data → Data/Book_Test.asset → m_pages（同じ左右ページの並び）。Contentsは空で旧Pagesを使用中。
- BookGameController.First / Last Fixed Pageも、Book_Testの先頭・末尾と同じ参照を設定する必要がある。
- YasudaBookViewController.AwakeがBookDataを実行時一覧へコピーし、表紙内側のnull枠を先頭に挿入。BookGameController.InitializeはValidateBookPagesで件数・固定ページ・左右参照の一致を要求する。起動時に管理側から表示用一覧を生成していない。
- 対して追加・入れ替え・削除時はBookGameController.ApplyがPageInstance一覧から左右ページを生成し、TryApplyInventoryへ渡している。実行時の所持変更は管理側が主導済み。

初期3見開きの対応：
| 管理用定義 | 左表示 | 右表示 |
| --- | --- | --- |
| Data/Pages/TestPage.asset | PageView_07 | PageView_08 |
| Data/Pages/TestPage 1.asset | PageView_09 | PageView_10 |
| Data/Pages/TestPage 2.asset | PageView_11 | PageView_12 |

本の表示とゲーム動作を分ける責務自体は有用だが、初期所持と順番を2回入力する必要はない。推奨はInitial Pagesと固定ページ設定を唯一の入力元とし、担当側Controllerで起動前に表示用配列を組み立てること。hatakeのBookDataをアセットとして自動保存する方式は避ける。Awake / OnEnableで最初の撮影が始まる前に一覧を渡す初期化順の調整が必要で、Start後に上書きするだけでは不十分。

PageData.PageType（MAGIC/GUIDE/OBSTACLE）とBookPageData.Category（Guide/Attack/Assist/Noise）も別々に存在する。通常の追加等の主処理と、表示側のノイズ削除分岐で参照先が異なるため、廃止前には利用経路を整理する。CreateAssetMenuの名前も両方がBook/Page Dataで同じため、作成メニューが紛らわしい。

## 2. PageViewのInspector編集

確認済み：ProjectでData/PageView/PageView_05.assetを選ぶと、Images → Element 0 → Sprite / Material / Position / Sizeが通常の編集欄として表示された。BookImageDataのposition / sizeは今もSerializeFieldで、BookPageData向けの独自Inspectorや編集禁止処理は見つからなかった。値を変更するテストは行っていないので、保存・表示反映まで確認済みとはしない。

編集対象を区別する必要がある：
- PageView_XX.asset：画像の位置・サイズを保存する設定元。Positionはページ左上が原点、X右向き、Y下向き。基準サイズ310×360。
- HierarchyのLeftPage / RightPageに付くBookPageView：表示器。InspectorにはPage Content参照があり、画像レイアウト本体は持たない。
- Play中に生成されたPageContent以下の画像：ShowPageのたびに削除・再生成され、位置・サイズはassetから上書きされる。直接移動を恒久設定にする仕組みではない。
- めくり中のLive TurnはCanvas／Cameraのコピーで、実行時複製が表示される経路もある。

現在はasset変更時にプレビューを再生成するOnValidate／ExecuteAlwaysや専用プレビューEditorがない。assetを編集できることと、Scene上で即座に見ながら配置できることは別。即時反映やドラッグ編集ができない点は現状実装の制約。欄自体が入力不能という症状は今回再現できていないため、正常仕様とは断定しない。

## 3. 文字・フォントを追加できるか

構造上は可能。画像・ルーンをuGUI Canvasで描画 → CameraでRenderTextureへ撮影 → BookFlipViewで合成するため、同じ撮影CanvasにTextMeshProUGUIを置けば文字も撮影対象になる。既存Assembly-CSharp.csprojはUnity.TextMeshProを参照しており、新しいパッケージ追加は不要。

ただし現在のBookPageDataはImagesしか持たず、BookPageViewもImageしか生成しない。ページごとの文章、TMP_FontAsset、文字サイズ、色、配置範囲、揃え方、折返し等を保存・再現する機能は未実装。別の画面用Canvasに文字を置くだけでは本の撮影内容へ入らない。文字も画像と同じレイアウトデータで一元管理し、共通の重なり順で描く案が適する。

現場の状態：
- 未追跡のData/Font/Sourceに10個のTTF/OTF、Data/Font/SDFにANYAfont SDF.assetがある。ユーザー作成物として未変更。
- 既存Unityの未保存SceneにFontTestCanvas / ANYATextがあり、TextMeshPro - Text (UI)、ANYAfont SDF、文字サイズ20の設定を確認。現時点で本への組み込み・表示成功を示すものではない。
- ANYAfont SDFはAtlasPopulationMode=0（Static）、文字テーブル190件、CJK漢字0文字。ひらがな・カタカナはあるが「炎」「水」「魔法」「攻撃力」の漢字は未収録。現フォントアセットだけで日本語説明文がすべて表示できるとはいえない。元フォントに漢字があるかは今回未検証。
- 必要な文字を含むStatic SDF、または漢字を含む元フォントと適切なDynamic／fallback設定が必要。既存の固定SDFは勝手に変えない。
- 撮影RenderTextureは800×450、合成出力は1600×1000。小さい文字は撮影段階で細部が失われ得る。紙質のにじみと合わせて、実際のタブレットで可読性を評価する必要がある。

今回確認したのはコード経路・依存関係・Inspector・文字テーブル。ページ内へ新しく文字を生成して撮影・めくり・Android表示する実証は未実施。

## 4. 和紙・しわのような処理

保存済みBookSceneのBookFlipViewはAssets/hatake/book/data/BookFlip.matを参照する。BookFlipView.InitializeがそのMaterialを実行時に複製する。

主処理はAssets/hatake/book/shader/BookFlipComposite.shaderとBookFlipLook.cginc。取り込んだ画像ではなく、撮影したページ全体に紙質を合成するため、画像や今後の文字にも効果が乗る。

| Materialの表示名 | 内部名 | 現在値 | 効果 |
| --- | --- | --- | --- |
| Paper grain | _PaperGrain | 0.08 | 明暗の粒子ノイズ |
| Paper fiber relief | _PaperBump | 0.22 | 繊維ノイズから擬似法線を作り凹凸の明暗を付加 |
| Paper fiber scale | _PaperBumpScale | 160 | 凹凸の細かさ |
| Ink bleed | _InkBleed | 0.45 | インクのにじみ |
| Edge aging | _PaperAge | 0.45 | ページ端の古色 |
| Paper tint | _PaperTint | (1, 0.96, 0.88, 1) | 紙色を乗算 |
| Paper texture strength | _PaperTexStrength | 0 | 外部紙テクスチャは現在寄与なし |
| Paper normal map strength | _PaperNormalStrength | 0 | 外部法線テクスチャは現在寄与なし |

BookPaperHeight / BookPaperTiltが繊維の凹凸、BookPaperが紙色・粒子・端の古色、BookInkBleedがにじみを計算。Compositeの157〜159行付近でにじみ→紙質→ノドの陰影、198〜200行付近で凹凸による光の明暗を適用する。したがって外部ノイズ画像が未設定でも質感が出る。

BookFlipPost.shaderはBloom・暗転ぼかし・露出・トーンマップで、紙の粒子生成の主因ではない。画像の縦横比はBookPageViewでpreserveAspect=falseのため指定サイズに引き伸ばされるが、これは紙のしわとは別の現象。PageView_07の一部画像にはGraySprite.matの色乗算もある。

切り分ける場合は担当フォルダにBookFlip.matを複製し、PaperBump / PaperGrain / InkBleedを0にして比較する案が安全。紙色と端の色まで元画像へ近づけるならPaperTint白、PaperAge=0も比較する。共有MaterialやSceneは今回は変更しない。現方式はページ全体に処理するため、背景だけ紙質を残して画像・文字を除外するには描画レイヤー等の設計追加が必要。

## 推奨作業順

1. 初期ページ一覧を管理側へ一元化し、表示側の重複入力をなくす。担当側のController変更で進める。
2. PageDataから左右の表示設定へすぐ移動・編集できるInspector／プレビューを用意する。プレビューは既存Sceneを保存・上書きせず、専用の作業領域で表示する。
3. 画像・文章を共通の配置／重なり順で管理する表示データと描画を設計。hatakeのBookPageData／BookPageViewへ機能追加するなら先にユーザーへ相談。担当側へ専用化する場合も参照型・既存assetの移行範囲を整理し、いきなり表示システム全体を複製しない。
4. 必要な日本語文字を持つフォントで文字の撮影→めくり→切替／追加削除→Android可読性を検証。
5. 担当側Materialで紙質を比較し、全体へかけるか背景だけにするか決める。

Git：status・fetch・追跡先比較を実施。HEADと追跡先のコミット・ファイル差分なし。開始時のnumber.png.meta変更、Fontフォルダとimage.pngの未追跡状態を保持。禁止Git操作なし。
