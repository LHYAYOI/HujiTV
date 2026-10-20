using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TeamToon.Editor
{
    public sealed class TeamToonShaderGUI : ShaderGUI
    {
        MaterialEditor editor;
        MaterialProperty[] properties;
        bool appearance, surface, shine, lighting, character, edge, advanced;
        int preset;
        static readonly string[] Presets = { "標準", "肌", "髪", "布", "金属", "瞳" };

        internal void ShowDocumentationSection(int section)
        {
            appearance = section == 1;
            surface = section == 2;
            shine = section == 3;
            lighting = section == 4;
            character = section == 5;
            edge = section == 6;
            advanced = section == 7;
        }
        MaterialProperty P(string name) => FindProperty(name, properties, false);
        bool Any(string name) => editor.targets.Cast<Material>().Any(m => m.GetFloat(name) > 0.5f);
        bool AnyMode(string name, int mode) => editor.targets.Cast<Material>().Any(m => Mathf.RoundToInt(m.GetFloat(name)) == mode);
        bool AllUnlit => editor.targets.Cast<Material>().All(m => Mathf.RoundToInt(m.GetFloat("_ShadeMode")) == 3);

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            editor = materialEditor; properties = props;
            EditorGUILayout.LabelField("Team Toon — マテリアル調整", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("画像なしでも、色と数値だけで使えます。", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            editor.TexturePropertySingleLine(new GUIContent("ベース画像", "地の色の画像です。未指定ならベース色だけで描画します。"), P("_BaseMap"));
            Field("_BaseColor", "ベース色", "画像へ掛ける色。白で元の画像の色。画像なしは、この色になります。");
            using (new EditorGUI.DisabledScope(AllUnlit))
            {
                Field("_ShadowColor", "影の色", "ベース色へ掛ける色です。最終的な影の色を直接指定する項目ではありません。");
                Field("_ShadowThreshold", "影になる範囲", "大きいほど影が広がります。Ramp方式では詳細欄の画像で調整します。");
                Field("_ShadowSoftness", "境界の柔らかさ", "面の明暗の境界幅。ライトの落ち影をぼかす設定とは別です。");
                using (new EditorGUI.DisabledScope(!Any("_SpecularMode")))
                    Field("_SpecularStrength", "ツヤの強さ", "ツヤの方式が『なし』のときは無効です。下のツヤ欄で切り替えます。");
            }
            if (AllUnlit) EditorGUILayout.HelpBox("照明なし：ベース画像・色と自己発光で描画します。Fogは適用します。", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            preset = EditorGUILayout.Popup(new GUIContent("材質の見本", "画像とベース色、切り抜き・裏面・輪郭線の設定を保持して、材質の初期値を適用します。"), preset, Presets);
            if (GUILayout.Button("初期値を適用", GUILayout.Width(98)))
            {
                Undo.RecordObjects(editor.targets, "Team Toon 材質の初期値");
                foreach (Material material in editor.targets)
                {
                    TeamToonMaterial.ApplyPreset(material, (TeamToonMaterial.Preset)preset);
                    EditorUtility.SetDirty(material);
                }
                editor.PropertiesChanged();
            }
            EditorGUILayout.EndHorizontal();

            if (Fold(ref appearance, "陰影の方式・2影"))
            {
                Popup("_ShadeMode", "陰影の方式", new[] { "2色セル", "3色セル", "なめらか", "照明なし", "Ramp画像" });
                if (AnyMode("_ShadeMode", 1))
                {
                    Field("_DeepShadowColor", "2影の色", "最も暗い部分へ掛ける色。");
                    Field("_DeepThreshold", "2影になる範囲", "1影の境界より低い値に制限します。");
                    Field("_DeepSoftness", "2影の柔らかさ", "2影と1影の境界幅。");
                }
                if (AnyMode("_ShadeMode", 4))
                {
                    Texture("_RampMap", "陰影Ramp画像", "横方向：暗→明。中央の行を使用。未指定なら2色セルへ戻ります。");
                    EditorGUILayout.HelpBox("Ramp画像はsRGB ON、WrapはClamp。影色・影範囲の代わりに画像で陰影を作ります。", MessageType.Info);
                }
            }
            using (new EditorGUI.DisabledScope(AllUnlit))
            {
                if (Fold(ref surface, "凹凸・材質・AO"))
                {
                    Toggle("_NormalEnabled", "凹凸の画像を使用", "未指定ならモデルの法線を使います。");
                    if (Any("_NormalEnabled")) { Texture("_NormalMap", "凹凸の画像", "Normal mapとして取り込んでください。"); Field("_NormalScale", "凹凸の強さ", "0で凹凸を弱めます。"); }
                    Field("_Metallic", "金属度", "画像なしはこの値。材質画像ありは画像Rへ掛ける倍率。");
                    Field("_Roughness", "表面の粗さ", "大きいほどツヤ・映り込みが広がります。画像ありは画像Gへ掛ける倍率。");
                    Field("_Occlusion", "素材の明るさ（AO）", "1＝遮蔽なし。画像がなくても使えます。画像ありはAOへ掛ける倍率。");
                    Toggle("_SurfaceEnabled", "材質画像を使用", "未指定なら上の3数値をそのまま使います。");
                    if (Any("_SurfaceEnabled")) { Texture("_SurfaceMap", "材質画像", "R：金属度 / G：粗さ / B：AO。sRGB OFF。"); EditorGUILayout.HelpBox("材質画像 × 上の数値。画像の値をそのまま使うには、倍率を1にします。", MessageType.Info); }
                    Toggle("_ControlEnabled", "Toon制御画像を使用", "未指定ならツヤ・リムは全適用、AO遮蔽なし、影範囲の補正なし。");
                    if (Any("_ControlEnabled"))
                    {
                        Texture("_ControlMap", "Toon制御画像", "R：ツヤ量 / G：AO / B：リム量 / A：影範囲の補正。sRGB OFF。");
                        Field("_ThresholdMaskStrength", "画像による影範囲の補正", "A=0.5が中立。0で画像による影範囲の補正を無効にします。");
                    }
                }
                if (Fold(ref shine, "ツヤ・輪郭の光・映り込み"))
                {
                    Popup("_SpecularMode", "ツヤの方式", new[] { "なし", "通常のツヤ", "髪に沿うツヤ" });
                    if (Any("_SpecularMode"))
                    {
                        Field("_SpecularColor", "ツヤの色", "金属度が高いとベース色も反映します。");
                        Field("_SpecularWidth", "ツヤの幅", "大きいほど広くなります。");
                        Field("_SpecularSoftness", "ツヤの境界幅", "小さいほどくっきりします。");
                    }
                    Toggle("_RimEnabled", "輪郭の光を使用", "面へ光を加える機能。輪郭線とは別です。");
                    if (Any("_RimEnabled")) { Field("_RimColor", "輪郭の光の色", ""); Field("_RimStrength", "輪郭の光の強さ", ""); Field("_RimPower", "輪郭の光の細さ", "大きいほど細い。"); Field("_RimLightAlignment", "ライト方向へ寄せる", "0＝視線基準、1＝ライト方向も反映。"); }
                    Toggle("_ReflectionEnabled", "周囲の映り込みを使用", "シーンのReflection Probe／環境反射を使います。");
                    if (Any("_ReflectionEnabled")) Field("_ReflectionStrength", "映り込みの強さ", "映り込みが出ない場合はシーンの環境反射を確認してください。");
                }
                if (Fold(ref lighting, "ライト・落ち影"))
                {
                    Toggle("_ReceiveShadows", "落ち影を受ける", "別の物体が落とす影。面の向きによるセル陰影とは別です。");
                    if (Any("_ReceiveShadows")) Field("_ReceiveShadowStrength", "落ち影の強さ", "0で無視、1で全適用。");
                    Field("_AmbientStrength", "環境光の強さ", "Light Probe／Lightmapの明るさ。");
                    Field("_AdditionalStrength", "追加ライトの強さ", "Point/Spot等。追加ライトの拡散はなめらかな照明です。");
                    Field("_LightColorInfluence", "ライト色の影響", "0でライトの色味を抑えます。");
                    Field("_LightIntensityInfluence", "強いライトの影響", "0で高強度による白飛びを抑えます。");
                    Field("_SSAOReceive", "SSAOの影響", "RendererでSSAOが有効な場合に適用。");
                    Field("_ColorPreservation", "ベース色を保つ", "1で照明後の色をベース色に戻します。発光とは別です。");
                }
                if (Fold(ref character, "顔・髪・前髪越しの表示"))
                {
                    Popup("_FaceMode", "顔の陰影", new[] { "通常の法線", "顔の法線補正", "顔SDF" });
                    if (Any("_FaceMode"))
                    {
                        Field("_FaceBlend", "顔の陰影を整える", "頭の前方向に照明法線を寄せます。");
                        EditorGUILayout.HelpBox("モデルに『TeamToonCharacter』を追加し、頭とRendererを指定してください。未設定の前方向はワールドZです。", MessageType.Info);
                        if (AnyMode("_FaceMode", 2)) { Toggle("_FaceSDFEnabled", "顔SDF画像を使用", "画像なしは顔の法線補正へ戻ります。"); Texture("_FaceSDFMap", "顔SDF画像", "R＝光が入る角度。sRGB OFF。左右反転して使用します。"); }
                    }
                    if (AnyMode("_SpecularMode", 2))
                    {
                        Field("_HairShift", "髪のツヤの位置", ""); Field("_HairSecondaryStrength", "副ハイライト", ""); Field("_HairBandStrength", "帯状のツヤ", "");
                        Toggle("_HairFlowEnabled", "毛流れの画像を使用", "画像なしはUVの縦方向。接線を持つモデルが必要です。");
                        if (Any("_HairFlowEnabled")) Texture("_HairFlowMap", "毛流れの画像", "RGを-1〜1のUV方向へ変換。sRGB OFF。");
                    }
                    Toggle("_IsHair", "前髪越し表示の遮蔽物（髪）", "透かす相手の髪Materialに設定します。");
                    Toggle("_RevealEnabled", "前髪越しに表示（眉・瞳）", "眉・瞳のMaterialに設定します。髪と同じキャラクター識別番号が必要です。");
                    if (Any("_RevealEnabled")) { Field("_RevealOpacity", "前髪越しの濃さ", ""); Field("_RevealMaxDepth", "透かす最大距離（m）", "髪と眉・瞳の奥行き差。小さくすると透かす範囲を限定できます。"); }
                    if (Any("_IsHair") || Any("_RevealEnabled")) { Field("_CharacterId", "キャラクター識別番号", "同じキャラの髪・眉・瞳は同じ番号。別のキャラには別番号。"); EditorGUILayout.HelpBox("前髪越し表示には、使用中のRendererへTeamToonRendererFeatureを追加してください。", MessageType.Info); }
                }
            }
            if (Fold(ref edge, "輪郭線・切り抜き・裏面"))
            {
                Toggle("_OutlineEnabled", "輪郭線を使用", "追加描画を有効にします。");
                if (Any("_OutlineEnabled"))
                {
                    Field("_OutlineColor", "輪郭線の色", ""); Field("_OutlineWidth", "輪郭線の幅（pixel）", "描画解像度基準。分離した頂点や硬い法線では線が割れる場合があります。");
                    Toggle("_OutlineMaskEnabled", "輪郭線の幅画像を使用", "未指定なら均一な幅。");
                    if (Any("_OutlineMaskEnabled")) Texture("_OutlineMask", "輪郭線の幅画像", "R：0で線なし、1で全幅。sRGB OFF。");
                    EditorGUILayout.HelpBox("輪郭線には、使用中のRendererへTeamToonRendererFeatureを追加してください。", MessageType.Info);
                }
                Toggle("_AlphaClip", "透明部分を切り抜く", "半透明ではなく、画像Aで表示／非表示を切り替えます。");
                if (Any("_AlphaClip")) Field("_Cutoff", "切り抜きの境界値", "大きいほど切り抜く範囲が増えます。");
                Popup("_Cull", "裏面の表示", new[] { "両面", "裏面のみ", "表面のみ（通常）" });
                EditorGUILayout.HelpBox("影を落とす／落とさないは、モデルのRendererにあるCast Shadowsで設定します。", MessageType.None);
            }
            if (Fold(ref advanced, "自己発光・画像の配置・技術設定"))
            {
                Toggle("_EmissionEnabled", "自己発光を使用", "画像なしでもHDR色だけで発光できます。BloomはVolume側で設定します。");
                if (Any("_EmissionEnabled"))
                {
                    Field("_EmissionColor", "自己発光の色", ""); Toggle("_EmissionMapEnabled", "自己発光の画像を使用", "未指定なら自己発光の色だけを使用。");
                    if (Any("_EmissionMapEnabled")) Texture("_EmissionMap", "自己発光の画像", "RGBを発光色へ掛けます。sRGB ON。");
                }
                editor.TextureScaleOffsetProperty(P("_BaseMap"));
                EditorGUILayout.LabelField("画像の配置はBase・Normal・材質・制御画像で共通です。", EditorStyles.wordWrappedMiniLabel);
                editor.EnableInstancingField();
            }
            bool changed = EditorGUI.EndChangeCheck();
            foreach (Material material in editor.targets)
            {
                TeamToonMaterial.Synchronize(material);
                if (changed) EditorUtility.SetDirty(material);
            }
            CheckImports();
        }
        static bool Fold(ref bool value, string title)
        {
            EditorGUILayout.Space(5);
            value = EditorGUILayout.Foldout(value, title, true, EditorStyles.foldoutHeader);
            return value;
        }
        void Field(string name, string title, string tip) => editor.ShaderProperty(P(name), new GUIContent(title, tip));
        void Texture(string name, string title, string tip) => editor.TexturePropertySingleLine(new GUIContent(title, tip), P(name));
        void Toggle(string name, string title, string tip)
        {
            var p = P(name);
            EditorGUI.showMixedValue = p.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(new GUIContent(title, tip), p.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck()) p.floatValue = enabled ? 1 : 0;
            EditorGUI.showMixedValue = false;
        }
        void Popup(string name, string title, string[] options)
        {
            var p = P(name);
            EditorGUI.showMixedValue = p.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(title, Mathf.Clamp(Mathf.RoundToInt(p.floatValue), 0, options.Length - 1), options);
            if (EditorGUI.EndChangeCheck()) p.floatValue = selected;
            EditorGUI.showMixedValue = false;
        }
        void CheckImports()
        {
            foreach (var map in new[] { ("_NormalMap", true, false), ("_SurfaceMap", false, false), ("_ControlMap", false, false),
                ("_FaceSDFMap", false, false), ("_HairFlowMap", false, false), ("_OutlineMask", false, false),
                ("_BaseMap", false, true), ("_RampMap", false, true), ("_EmissionMap", false, true) })
            {
                var textures = editor.targets.Cast<Material>().Select(m => m.GetTexture(map.Item1)).Where(t => t).Distinct();
                foreach (var texture in textures)
                {
                    string path = AssetDatabase.GetAssetPath(texture);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                    bool invalid = map.Item2 ? importer.textureType != TextureImporterType.NormalMap : importer.sRGBTexture != map.Item3;
                    if (invalid) EditorGUILayout.HelpBox(texture.name + "：" + (map.Item2 ? "Texture TypeをNormal mapにしてください。" : "sRGBを" + (map.Item3 ? "ON" : "OFF") + "にしてください。") + "共有画像の設定はここでは自動変更しません。", MessageType.Warning);
                }
            }
        }
        public override void ValidateMaterial(Material material) => TeamToonMaterial.Synchronize(material);
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {

            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            material.SetTexture("_BaseMap", baseMap); material.SetColor("_BaseColor", baseColor);
            material.shaderKeywords = new string[0];
            TeamToonMaterial.Synchronize(material);
        }
    }
}
