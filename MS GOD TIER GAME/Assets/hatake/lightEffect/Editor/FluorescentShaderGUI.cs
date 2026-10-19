using System;
using UnityEditor;
using UnityEngine;

namespace LightShaftLab.Editor
{
    /// <summary>
    /// Fluorescent Lit / Unlit 用インスペクター。URP標準のLit/Unlit用GUIをそのまま表示し、
    /// その下に蛍光のパラメータを追加する。URPのGUIクラスはinternalなのでリフレクションで生成する。
    /// </summary>
    public sealed class FluorescentShaderGUI : ShaderGUI
    {
        static bool g_foldoutFlag = true;
        ShaderGUI m_inner;
        string m_innerShader;

        /// <summary>シェーダーに対応するURP標準のGUI（Lit/Unlit）を取得する。シェーダーが変わったときだけ作り直す。</summary>
        ShaderGUI GetInnerGUI(Shader shader)
        {
            string shaderName = shader ? shader.name : "";
            if (m_inner != null && m_innerShader == shaderName) return m_inner;
            m_innerShader = shaderName; m_inner = null;
            string typeName = shaderName.Contains("Unlit") ? "UnlitShader" : "LitShader";
            try
            {
                var type = Type.GetType($"UnityEditor.Rendering.Universal.ShaderGUI.{typeName}, Unity.RenderPipelines.Universal.Editor");
                if (type != null) m_inner = Activator.CreateInstance(type, true) as ShaderGUI;
            }
            catch (Exception) { m_inner = null; }
            return m_inner;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            var inner = GetInnerGUI(material ? material.shader : null);
            if (inner == null)
            {
                // URPのGUIが取得できない場合は全プロパティを標準表示する（蛍光の項目も含まれる）。
                base.OnGUI(materialEditor, properties);
                return;
            }
            inner.OnGUI(materialEditor, properties);

            EditorGUILayout.Space();
            g_foldoutFlag = EditorGUILayout.BeginFoldoutHeaderGroup(g_foldoutFlag, "蛍光（ブラックライト）");
            if (g_foldoutFlag)
            {
                var color = FindProperty("_FluorColor", properties, false);
                var map = FindProperty("_FluorMap", properties, false);
                if (map != null && color != null)
                    materialEditor.TexturePropertySingleLine(new GUIContent("蛍光色 / マスク", "紫外線を受けたときに放出する色（HDR）。テクスチャのRGBを乗算し、黒い部分は光りません。UVはBase Mapと共通です。"), map, color);
                DrawProperty(materialEditor, properties, "_FluorEfficiency", "変換効率", "紫外線をどれだけ光に変えるか。0で蛍光なし。");
                DrawProperty(materialEditor, properties, "_FluorAlbedoTint", "ベース色の反映", "0=蛍光色のみ、1=Base Map×Base Colorを蛍光色に乗算。");
                if (material && !material.shader.name.Contains("Unlit"))
                    DrawProperty(materialEditor, properties, "_FluorWrap", "回り込み", "ライトに背を向けた面にも紫外線を回り込ませる量。");
                DrawProperty(materialEditor, properties, "_FluorSaturation", "明るさの上限", "蛍光の輝度をこの値に向けて滑らかに頭打ちにし、Bloomの白飛びを抑えます。0で上限なし。");
                EditorGUILayout.HelpBox("ブラックライト（BlackLightEmitter付きのSpot/Point Light）が当たったときだけ光ります。RendererにBlackLightFeatureが必要です。", MessageType.None);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        static void DrawProperty(MaterialEditor editor, MaterialProperty[] properties, string name, string label, string tooltip)
        {
            var property = FindProperty(name, properties, false);
            if (property != null) editor.ShaderProperty(property, new GUIContent(label, tooltip));
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            var inner = GetInnerGUI(newShader);
            if (inner != null) inner.AssignNewShaderToMaterial(material, oldShader, newShader);
            else base.AssignNewShaderToMaterial(material, oldShader, newShader);
        }

        public override void ValidateMaterial(Material material)
        {
            var inner = GetInnerGUI(material ? material.shader : null);
            if (inner != null) inner.ValidateMaterial(material);
        }
    }
}
