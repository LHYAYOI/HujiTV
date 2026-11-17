using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterEnclosureBuilder
    {
        private const string Folder = "Assets/YasudaHaruto/Data/RollerCoaster/Enclosures";

        internal static RollerCoasterEnclosures Create(RollerCoasterCourse course)
        {
            var existing = course.GetComponentInChildren<RollerCoasterEnclosures>(true);
            if (existing != null) { Generate(existing); return existing; }
            RailSplineMeshGenerator.EnsureFolder(Folder);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RailSplineMeshSetup.ModelPath);
            var filter = model.GetComponentsInChildren<MeshFilter>(true).Single(x => x.name == "duct_straight");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/DuctStraight.asset");
            if (mesh == null)
            {
                var matrix = filter.transform.localToWorldMatrix;
                matrix.m03 = matrix.m13 = matrix.m23 = 0f;
                var source = RailMeshBuilder.Read(filter.sharedMesh, matrix);
                var triangles = source.Indices.Select(indices => indices.Select(i => source.Vertices[i]).ToList()).ToArray();
                mesh = RailMeshBuilder.MakeMesh(source, triangles, vertex => vertex);
                mesh.name = "DuctStraight";
                AssetDatabase.CreateAsset(mesh, Folder + "/DuctStraight.asset");
            }
            // 素材のTexture/Normal等は元Materialから引き継ぐ。
            var palette = new Material[5];
            for (int i = 0; i < palette.Length; i++)
            {
                string path = Folder + $"/DuctColor{i+1}.mat";
                palette[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (palette[i] != null) continue;
                palette[i] = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/map/bunki/odoriba_duct_rail.mat")) { name = $"DuctColor{i+1}" };
                if (palette[i].HasProperty("_Cull")) palette[i].SetFloat("_Cull", 0f);
                palette[i].doubleSidedGI = true;
                palette[i].enableInstancing = true;
                AssetDatabase.CreateAsset(palette[i], path);
            }
            var root = new GameObject("Track Enclosures");
            root.transform.SetParent(course.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Add track enclosures");
            var settings = Undo.AddComponent<RollerCoasterEnclosures>(root);
            settings.Configure(course, mesh, palette);
            ApplyAtlasColors(settings);
            Generate(settings);
            return settings;
        }

        internal static void ApplyAtlasColors(RollerCoasterEnclosures settings)
        {
            // 元Atlasにある緑・青・桃色の模様を使い分ける。原TextureやUVは変更しない。
            var uv = settings.SourceMesh.uv;
            Vector2 min = new Vector2(uv.Min(x=>x.x), uv.Min(x=>x.y));
            Vector2 size = new Vector2(uv.Max(x=>x.x)-min.x, uv.Max(x=>x.y)-min.y);
            Rect[] patches = { new Rect(.84f,.4f,.06f,.35f), new Rect(.35f,.53f,.28f,.15f),
                new Rect(.2f,.08f,.5f,.13f), new Rect(.84f,.4f,.06f,.35f), new Rect(.35f,.53f,.28f,.15f) };
            Color[] tints = { Color.white, Color.white, Color.white, new Color(.85f,.9f,1f), new Color(.85f,1f,.9f) };
            var shader=Shader.Find("YasudaHaruto/RollerCoaster Enclosure");
            if(shader==null)throw new InvalidOperationException("囲い用Shaderが読み込まれていません。");
            for (int i = 0; i < settings.Palette.Length; i++)
            {
                var material = settings.Palette[i];
                var patch = patches[i%patches.Length];
                var texture=material.GetTexture("_BaseMap");
                material.shader=shader;
                material.SetTexture("_BaseMap",texture);
                material.SetVector("_AtlasRect",new Vector4(patch.x,patch.y,patch.width,patch.height));
                material.SetFloat("_TileMeters",3f);
                material.SetVector("_ScrollVelocity",new Vector4(.012f,.004f,0f,0f));
                Vector2 scale = new Vector2(patch.width/size.x,patch.height/size.y);
                Vector2 offset = patch.min-Vector2.Scale(min,scale);
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_ParallaxMap" })
                {
                    if (!material.HasProperty(property)) continue;
                    material.SetTextureScale(property,scale); material.SetTextureOffset(property,offset);
                }
                material.SetColor("_BaseColor",tints[i%tints.Length]);
                material.shaderKeywords=new string[0];
                EditorUtility.SetDirty(material);
            }
        }

        internal static void Generate(RollerCoasterEnclosures settings)
        {
            if (settings.Smooth) { RollerCoasterSmoothEnclosureBuilder.Generate(settings); return; }
            GenerateStraight(settings);
        }

        private static void GenerateStraight(RollerCoasterEnclosures settings)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("編集モードで配置してください。");
            if (settings.Course == null || settings.SourceMesh == null || settings.Palette == null ||
                settings.Palette.Length < 1 || settings.Palette.Any(x => x == null)) throw new ArgumentException("コース・素材・色を設定してください。");
            if (settings.Width < 3f || settings.Height < 3f || settings.Spacing < .5f || settings.Spacing > 3f || settings.Overlap < 0f)
                throw new ArgumentException("囲い寸法・間隔を設定範囲内にしてください。");
            if (settings.GeneratedRoot != null && settings.GeneratedRoot.transform.parent != settings.transform)
                throw new InvalidOperationException("生成rootが移動されています。元の親へ戻してください。");
            var table = new RailDistanceTable(settings.Course.Spline);
            float gapStart = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(settings.Course.Spline.Spline, settings.Course.FallStartKnot));
            float gapEnd = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(settings.Course.Spline.Spline, settings.Course.FallEndKnot));
            if (Mathf.CeilToInt(table.Length/settings.Spacing) > 1500) throw new ArgumentException("配置数が多すぎます。");
            var root = new GameObject("Generated Enclosures");
            root.transform.SetParent(settings.transform, false);
            var random = new System.Random(settings.ColorSeed);
            int previous = -1, run = 0, color = 0, number = 0;
            var bag = new List<int>();
            int NextColor()
            {
                if (bag.Count == 0)
                {
                    bag.AddRange(Enumerable.Range(0, settings.Palette.Length));
                    for (int j = bag.Count-1; j > 0; j--) { int k = random.Next(j+1); (bag[j],bag[k]) = (bag[k],bag[j]); }
                }
                int pick = bag.FindIndex(x => x != previous);
                if (pick < 0) pick = 0;
                int value = bag[pick]; bag.RemoveAt(pick); previous = value; return value;
            }
            try
            {
                foreach (var range in new[] { new Vector2(0f, gapStart), new Vector2(gapEnd, table.Length) })
                {
                    int count = Mathf.CeilToInt((range.y-range.x)/settings.Spacing);
                    float step = (range.y-range.x)/count;
                    for (int i = 0; i < count; i++)
                    {
                        float a = Mathf.Max(range.x, range.x+i*step-settings.Overlap*.5f);
                        float b = Mathf.Min(range.y, range.x+(i+1)*step+settings.Overlap*.5f);
                        table.Frame((a+b)*.5f, out var position, out var rotation);
                        // 開口部は境界の接線に合わせ、落下区間へ伸ばさずレール幅を収める。
                        if (i == 0)
                        {
                            table.Frame(a, out position, out rotation);
                            position += rotation*Vector3.forward*((b-a)*.5f);
                        }
                        else if (i == count-1)
                        {
                            table.Frame(b, out position, out rotation);
                            position -= rotation*Vector3.forward*((b-a)*.5f);
                        }
                        if (run-- <= 0) { color = NextColor(); run = random.Next(1, 4)-1; }
                        var piece = new GameObject($"Enclosure {++number:000} Color {color+1}");
                        piece.transform.SetParent(root.transform, false);
                        Vector3 scale = new Vector3(settings.Width/settings.SourceMesh.bounds.size.x,
                            settings.Height/settings.SourceMesh.bounds.size.y, (b-a)/settings.SourceMesh.bounds.size.z);
                        Vector3 center = position + rotation*Vector3.up*settings.CenterHeight;
                        piece.transform.SetPositionAndRotation(center-rotation*Vector3.Scale(settings.SourceMesh.bounds.center, scale), rotation);
                        piece.transform.localScale = scale;
                        piece.AddComponent<MeshFilter>().sharedMesh = settings.SourceMesh;
                        var renderer = piece.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = Enumerable.Repeat(settings.Palette[color], settings.SourceMesh.subMeshCount).ToArray();
                    }
                }
                Undo.RegisterCreatedObjectUndo(root, "Place track enclosures");
                Undo.RecordObject(settings, "Update track enclosures");
                if (settings.GeneratedRoot != null) Undo.DestroyObjectImmediate(settings.GeneratedRoot);
                settings.SetGeneratedRoot(root);
                EditorUtility.SetDirty(settings);
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }

    [CustomEditor(typeof(RollerCoasterEnclosures))]
    internal sealed class RollerCoasterEnclosuresEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Smoothで連続した囲いを生成します。落下部分は閉じた縦長空間に接続。色はSeedで固定。生成root内は再生成で置き換えるため、手置き物は外へ置いてください。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("囲いを再配置"))
                    try { RollerCoasterEnclosureBuilder.Generate((RollerCoasterEnclosures)target); }
                    catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
