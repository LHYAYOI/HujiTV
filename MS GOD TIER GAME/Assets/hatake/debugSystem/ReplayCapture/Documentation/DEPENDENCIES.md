# MP4循環録画：使用技術と権利情報

追加の録画ライブラリ・Unity Recorderパッケージ・FFmpeg・外部コーデックDLLは使用していません。録画／循環管理／結合コードは本プロジェクトで作成しました。

| 使用物 | 用途・配布状況 | 参照先 |
|---|---|---|
| Windows Media Foundation | Windows標準のH.264/AACエンコード、MP4多重化、圧縮サンプルの読み出し。OSに含まれるAPIを呼び出す | [Microsoft概要](https://learn.microsoft.com/en-us/windows/win32/medfound/microsoft-media-foundation-sdk) |
| Windows SDK | C++ビルド時のヘッダーとインポートライブラリ。SDK自体は同梱しない | [Windows SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) |
| Visual C++ツールセット | 自作DLLをx64でビルド。Visual Studio 2022の開発環境を使用 | [Microsoft C++](https://learn.microsoft.com/en-us/cpp/build/vscpp-step-0-installation?view=msvc-170) |
| Unity標準API | 画面全域取得、非同期GPU読み戻し、AudioListenerのUnity音声取得 | [ScreenCapture](https://docs.unity3d.com/ScriptReference/ScreenCapture.CaptureScreenshotIntoRenderTexture.html)、[AsyncGPUReadback](https://docs.unity3d.com/ScriptReference/Rendering.AsyncGPUReadback.html)、[OnAudioFilterRead](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) |
| Unity Input System（導入済み） | F9キー検出。今回パッケージ追加なし | [公式マニュアル](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/index.html)、[Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) |

API仕様の参照：

- [Sink Writerの手順](https://learn.microsoft.com/en-us/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video)
- [ハードウェア変換の有効化](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-readwrite-enable-hardware-transforms)
- [Sink Writerの入力抑制](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-sink-writer-disable-throttling)
- [MPEG-4 File Sink](https://learn.microsoft.com/en-us/windows/win32/medfound/mpeg-4-file-sink)

公式サンプルやOSSのコードファイルをコピーして同梱したものではありません。上記API仕様を参照して実装しています。Unity `MediaEncoder` も調査しましたが、採用していません。Unity参照ソースのコードは組み込んでいません。

自作ネイティブソースは `Tools/ReplayCapture/Native/ReplayMediaFoundation.cpp`、ビルド手順は `Tools/ReplayCapture/build-native.ps1`。生成DLLのUnityインポート設定はWindows x64 Editor専用です。開発環境のVisual C++ランタイムに依存します。

配布unitypackageでは、同じソースとビルド手順を `Assets/ReplayCapture/NativeSource/` に同梱します。C++ソースはUnityに不要なネイティブソースとして処理されないよう `.cpp.txt` とし、ビルドスクリプトが `/TP` 指定でC++としてコンパイルします。NV12変換用ComputeShaderと操作図SVGも本プロジェクト向けの独自実装です。

検証用の `check_native.py` はPython標準ライブラリとこの自作DLLを利用します。画像確認のBMP→PNG変換には導入済みの [Pillow](https://pillow.readthedocs.io/en/stable/about.html) を検証時のみ使用しています。ゲームや録画機能への同梱はありません。音声テストの440 Hz正弦波は計算で生成し、外部音源は使用していません。

Windows／Unity／開発ツールの使用条件、およびコンテスト固有の「標準API・エンコーダー利用」の規定は別途適用されます。H.264/AACのコーデック権利やコンテストへの適合を無条件に保証するものではありません。
