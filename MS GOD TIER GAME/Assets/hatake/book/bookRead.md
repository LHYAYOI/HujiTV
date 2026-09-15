## 2. 既存UIの前提

以前の2D版でページとスワイプが動作している状態から進めます。

```text
Main Camera                 有効、画面出力、Audio Listenerはここだけ
BookCaptureCamera           Capture用。Main Cameraとは別
Capture                     Screen Space - Camera / BookCapture Layer
└─ BookRoot                 中央620×360
   ├─ LeftPage              310×360、中央基準X=-155
   └─ RightPage             310×360、中央基準X=+155
Display                     Screen Space - Overlay
└─ BookDisplayImage         RawImage 800×450 + BookSwipeInput
BookManager                 BookController
EventSystem
```

- 両Canvasの基準解像度は800×450。
- A/Bは別々の800×450 Render Texture。表示RawImageは白・不透明、UV Rectは0,0,1,1。
- `BookPageView` の生成処理に `imageObject.layer = gameObject.layer;` が必要。
- 撮影CameraはBookCaptureだけを描画し、ポスト処理を無効にします。
- BookControllerの既存7参照を設定済みにします。新しいModel View欄はまだ空で構いません。
- 設定変更によって見開きの画面内位置を変えるとShaderの切り出し領域も変更が必要です。

## 3. 自動接続

1. Hierarchyで `BookManager`（BookControllerが付いたオブジェクト）を選択します。
2. 上部メニューの **Tools > Book > Connect Provided 3D Model** を実行します。
3. Consoleに「3D本を接続しました」と出ることを確認します。
4. シーンを保存します。
5. Gameビューを表示してPlayします。

メニューは次を作成します。

```text
BookModelStage               BookModelView
├─ book_mdl                  Animator（Playablesから制御）
│  └─ 提供モデルの階層
│     ├─ book                本体・静止ページ
│     ├─ page                動く1枚の紙、SkinnedMeshRenderer
│     └─ joint1 ... joint8   提供モデルのボーン構造を維持
└─ BookModelCamera           追加のAudio Listenerなし
```

実際のFBX階層では `Book_grp` の残り方がImporter設定によって変わる場合があります。メニューは子階層から `book` と `page` を検索します。モデル内の名前は変更しないでください。

さらに `Assets/Book3DGenerated`（同名があれば別の名前）に2つのMaterialと `BookModelTexture` を生成します。

`BookModel` Layerを新設し、選択シーン内の既存CameraのCulling MaskからこのLayerを外します。元FBX・元テクスチャ・元Material・元Animator Controllerは書き換えません。作成したモデルインスタンスには専用Materialを設定します。シーンは自動保存しません。Undoはシーン上の変更に使えますが、生成アセットは残ります。

## 4. 実行時の流れ

1. 既存ページUIをAへ撮影。
2. Aの左右領域を3D本体のページ面へ表示。
3. 3D Cameraの出力を `BookDisplayImage` へ表示。
4. スワイプすると、次の見開きをBへ撮影。
5. 静止面と動く紙の表裏へA/Bを割り当て、提供アニメーションを再生。
6. 終了後、動く紙を隠し、静止面を次の見開きに統一。
7. 次回はA/Bを交換して再利用。

戻る操作では同じAnimationClipの時間を逆向きに評価します。Animator.speedに負数を入れる方法ではありません。撮影開始からアニメーション終了後の描画完了まで追加のスワイプを無視します。

初回表示では動く紙を隠し、静止ページだけを表示します。`page_anim.fbx` はボーンとアニメーション用で、別の本としてHierarchyへ置きません。付属 `book_anim.controller` も自動再生には使用せず、そこから参照されているAnimationClipをPlayablesで評価します。

## 5. 調整

- **本の画面内サイズ**：BookModelCameraのOrthographic Size。小さくすると本が大きく見えます。
- **めくる速度**：BookModelStageのTurn Duration。初期値0.65秒。
- **動きが逆**：Forward FlagをOFF。ただし裏表の誤りは次の項目で調整します。
- **動く紙の表裏が逆**：生成したBookPageMaterialのSwap Front And BackをON。
- **紙の境界でちらつく**：BookPageMaterialのDepth Biasを調整。初期値-1。
- **ページ内容が上下左右にずれる**：まず撮影用UIを上記の寸法・中央位置に戻してください。

モデルのページ縦横比は既存の310×360と完全には一致しないため、内容はモデル面に合わせて伸縮します。必要に応じて後からページ基準サイズと撮影用レイアウトを再設計してください。

モデル用ShaderはUnlitです。元テクスチャの表紙表現は残りますが、PBRライティング・紙の落とす影はまだ実装していません。ページの透明度は不透明な紙として扱います。

## 6. 確認する操作

- 内容が識別できる4ページ以上で確認（例：左上に1、2、3、4のある画像）。
- 左スワイプ：めくる紙の表が旧右ページ、裏が次左ページ。
- 右スワイプ：上記を逆再生し、前の見開きへ戻る。
- 最初で戻る、最後で進む操作は無視。
- 連打してもアニメーション中は次の撮影を始めない。
- BookManagerを無効化→有効化すると現在の見開きを再表示。
- Capture Camera停止後もMain Cameraは有効なまま。
- Audio Listenerは1つだけ。

Gameビューが白い場合、BookControllerのModel View参照、BookModelTexture、Consoleの最初のエラーを確認してください。ページが動かなければ、page_animのImport Animation、Generic設定、およびモデルの階層名を確認します。元パッケージでは両FBXのBook_grp/joint1...joint8の構造が一致しています。