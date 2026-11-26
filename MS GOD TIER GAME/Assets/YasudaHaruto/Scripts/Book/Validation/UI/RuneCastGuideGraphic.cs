using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    // Full-screen shade also captures pointer events for the aiming gesture.
    /// <summary>ルーンの発動方向を示すガイドをUIメッシュで描画する。</summary>
    public sealed class RuneCastGuideGraphic : MaskableGraphic
    {

        public int SelectedDirection { get; set; } = -1;


        public void SetShade(float opacity) => color = new Color(0f, 0f, 0f, opacity);
    }
}
