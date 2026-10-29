# ルーン完成から方向指定・発射までの演出

## 動作

自分でなぞった線を粒子に置き換え、少し浮上させて中央下へ集める。ページの色付きルーン画像は残す。
集合後は中心の弾と周囲を漂う粒を表示する。集合前には方向指定を開始できない。
ルーンを描いた指を一度離し、弾のある明るい円を触って上・左上・右上へスワイプする。弾は指へ少し追従し、指を離した瞬間に既存の魔法送信とクールダウンを開始する。粒子の弾は選択方向へ尾を引いて画面外へ飛ぶ。

方向指定中だけ魔法風の矢印画像を3方向に表示し、選んだ方向を明るくする。照明領域・3方向の判定・短い／無効なスワイプの失敗規則は従来方式を使用する。
方向指定のない魔法は従来どおり完成時に即発動し、集合待ちを追加しない。

## Inspectorでの調整

BookGameDragInputの **Spell Particle Presentation → Spell Settings** を展開する。

| 項目 | 初期値／用途 |
| --- | --- |
| Float Duration | 0.35秒：粒子が浮く時間 |
| Gather Duration | 0.55秒：中央下へ集まる時間 |
| Float Height | 本の高さの0.12：浮上距離 |
| Trace Particle Count / Size | なぞり線から作る粒子数／大きさ |
| Core Size | 中心の弾の大きさ |
| Orbit Radius / Particle Count / Speed / Particle Size | 周囲の粒の範囲・数・速さ・大きさ |
| Drag Follow / Limit | 指への追従率／明るい円の半径に対する最大移動量 |
| Flight Duration / Trail Particle Count | 発射演出の時間／尾の粒子数 |
| Arrow Sprite | 任意の差し替え画像。空なら同梱のMagicAimArrowを使用 |
| Arrow Length / Idle Alpha | 矢印画像の大きさ／非選択方向の透明度 |
| Fallback Color | 画像がない、または有彩色がない場合の粒子色 |

時間はunscaled time、大きさは原則として画面上の本の高さに対する比率。Cast GuideにあるCast Origin／Radius／Shadeで集合位置・明るい領域・暗さを調整する。

## 色と表示の構成

- 既存のRune Image Indexで取得した色付きルーン画像を32×32へGPU読み戻しし、不透明な有彩色を重視して色を取得する。白い紙・黒い線を除外する。元TextureのRead/Write設定やSpriteは変更しない。
- RuneSpellEffectはDissolving Rune／Spell Core／Orbiting Motes／Spell Trailの4つのParticleSystemを重ねる。なぞり線は各線分の長さで均等にサンプリングするため、入力頻度が低くても線の途中から粒子が生まれる。
- ParticleSystemをPauseして外部制御し、RuneParticleGraphicが粒子をScreen Space OverlayのuGUIへ描画する。通常のParticleSystemRendererは無効。本の紙質・曲面へ焼き込まず、画面上の演出として表示する。
- 接続切断・4番枠による禁止・失敗・ページ終了・無効化で未発射の演出を消す。フォーカス喪失は方向指定だけを取り消し、集合済みの弾を残す。
- 発射演出はクールダウンの時計を進めず、既存のCastConfirmed経路をそのまま使う。描画線色やMagicDataへの色の二重登録は追加しない。

## 矢印素材

保存先：Assets/YasudaHaruto/Resources/RuneSpell/MagicAimArrow.png

組み込みimage_genで生成した透過PNG。RuneSpellAssetImporterはこの新規PNGにだけSprite設定を適用する。既存画像・hatake原本・Scene／Prefabへの保存変更は行わない。

生成プロンプト：

> Create one production game UI sprite: a single narrow vertical spell aiming arrow pointing STRAIGHT UP, centered on a truly transparent background. Fantasy grimoire / arcane magic world, delicate pale ivory and warm gold luminous filigree, elegant long tapered shaft with a clear spear-shaped arrowhead, two restrained curved runic flourishes at its base, tiny floating motes only close to shaft. No text, no letters, no background, no panel, no circle, no border, no shadows outside the sprite. Clear readable directional silhouette at small mobile UI size. Mostly white luminous body so Unity can tint it by spell color, subtle gold accents. Entire arrow contained within middle 40% width and 85% height of a square transparent image, tip near top. One arrow only, perfectly vertical, polished hand-painted fantasy game HUD asset. Save output image.

## 検証メニュー

Tools → YasudaHaruto → Verify Rune Spell In Current Scene。
現在のSceneを保存・差し替えずPlay Modeで、色取得、粒子化、集合待ち、直接ドラッグ、3方向の発射、クールダウン開始、取消、方向指定なしの動作を確認する。入力は合成ポインターイベントであり、物理操作・実機の確認とは区別する。
結果と段階ごとのゲーム画面はTemp/RuneSpellValidationへ出力する。

## 検証結果（2026-10-29）

- 現在のBookScene、Windows対象の既存UnityでRuneSpell COMPLETE。非Readable画像からの色取得、線の消去、4 ParticleSystem、集合前の入力拒否、弾の追従、Imageによる矢印、UIレイキャスト、左／中央／右の1回ずつの発射、即時クールダウン、尾の描画、取消、方向指定なしの即発動を確認。
- 粒子が次フレームにも保持されること、Coreの48頂点と非カリングを確認。01-dissolve／02-float／03-ready／04-aim／05-flightの実画面PNGを生成し、浮上・集合済み弾・画像矢印・発射を目視確認した。
- 既存Verify Rune FeedbackはRuneFeedback COMPLETE。Verify Book Obstacles In Current SceneはBookObstacleRuntime COMPLETE。手によるドラッグの体感・Android描画／実機入力・実PC間通信は今回未確認。
- 初期版はCanvasRenderer不足で粒子が実画面へ出なかった。RequireComponentを追加して修正。検証コードのリフレクションで同名オーバーロードの曖昧一致があり、DeclaredOnlyを指定して修正。修正後の最終検証は通過。
- Sceneを保存・差し替えず、Play Modeを終了。既存のScene・ページ・MagicData等の変更を維持した。
