using UnityEngine;

namespace Hatake.Painterly
{
    // カメラに付けると、ポストプロセスをかける、Rendererの設定を確認する
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class PainterlyCamera : MonoBehaviour
    {
        public bool renderPainterly = true;
        [Tooltip("元の画面との合成率。1で完全に絵画風")]
        [Range(0, 1)] public float strength = 1;

        [Header("Brush (anisotropic Kuwahara)")]
        public bool kuwahara = true;
        [Tooltip("筆のタッチの大きさ。1080p基準の画素")]
        [Range(1, 8)] public float brushRadius = 4;
        [Tooltip("大きいほど境界がくっきりする")]
        [Range(1, 18)] public float brushSharpness = 8;
        [Tooltip("形の流れに沿って筆を細長くする強さ")]
        [Range(0, 1)] public float brushAnisotropy = .85f;

        [Header("Bristle strokes")]
        public bool strokes = true;
        [Range(.2f, 4)] public float strokeScale = 1;
        [Range(0, 1)] public float strokeStrength = .3f;

        [Header("Canvas")]
        public bool canvas = true;
        [Tooltip("画面の高さあたりの織り目の数")]
        [Range(50, 800)] public float canvasScale = 280;
        [Range(0, 1)] public float canvasStrength = .3f;
        [Tooltip("絵具とキャンバスの凹凸の陰影")]
        [Range(0, 2)] public float impasto = .7f;

        [Header("Color")]
        [Range(0, 2)] public float contrast = 1.05f;
        [Range(-1, 1)] public float brightness;
        [Range(0, 2)] public float saturation = 1.15f;
        [Range(-1, 1)] public float warmth = .1f;
        [Range(0, 1)] public float vignette = .2f;

        public void ResetToDefaults()
        {
            renderPainterly = true; strength = 1;
            kuwahara = true; brushRadius = 4; brushSharpness = 8; brushAnisotropy = .85f;
            strokes = true; strokeScale = 1; strokeStrength = .3f;
            canvas = true; canvasScale = 280; canvasStrength = .3f; impasto = .7f;
            contrast = 1.05f; brightness = 0; saturation = 1.15f; warmth = .1f; vignette = .2f;
        }
    }
}
