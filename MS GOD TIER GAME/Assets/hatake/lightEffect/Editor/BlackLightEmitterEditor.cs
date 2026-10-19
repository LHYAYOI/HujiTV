using UnityEditor;
using UnityEngine;

namespace LightShaftLab.Editor
{
    /// <summary>ブラックライトのインスペクター。スペクトルから求めた「目に見える量」を表示する。</summary>
    [CustomEditor(typeof(BlackLightEmitter)), CanEditMultipleObjects]
    public sealed class BlackLightEmitterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;
            var emitter = (BlackLightEmitter)target;
            var spectrum = emitter.Spectrum;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(new GUIContent("目に見える割合 (%)", "出力のうち可視光として見える明るさ。Volume（Black Light → Dark Adaptation）の暗順応を反映した値です。"), emitter.VisibleFraction * 100);
                EditorGUILayout.FloatField(new GUIContent("　明所視 / カメラ (%)", "明るい場所の目・カメラの感度（CIE 1931、555nmを100%）。"), spectrum.m_luminousFraction * 100);
                EditorGUILayout.FloatField(new GUIContent("　暗所視 / 暗順応した目 (%)", "暗い部屋に慣れた目の感度（CIE 1951、507nmを100%）。"), spectrum.m_scotopicFraction * 100);
                EditorGUILayout.FloatField(new GUIContent("蛍光体へ届く割合 (%)", "フィルターを通過する紫外線の割合。"), spectrum.m_transmittedFraction * 100);
                Color visible = emitter.VisibleLinearColor;
                float maxComponent = visible.maxColorComponent;
                EditorGUILayout.ColorField(new GUIContent("見える色", "Lightに設定される色（明るさは正規化して表示）。"), maxComponent > 0 ? (visible / maxComponent).gamma : Color.black, false, false, false);
                EditorGUILayout.FloatField(new GUIContent("Light Intensity", "Drive Light がオンのときにLightへ設定される値。"), maxComponent);
            }
        }
    }
}
