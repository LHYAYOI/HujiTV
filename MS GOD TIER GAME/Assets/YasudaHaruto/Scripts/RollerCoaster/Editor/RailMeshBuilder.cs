using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YasudaHaruto.RollerCoaster.Editor
{
    // 各三角形を共通のZ平面で切ることで隣り合う面の分割位置を揃える。
    /// <summary>レール素材を距離サンプルへ変形し、つながったメッシュへ組み立てる。</summary>
    internal static class RailMeshBuilder
    {
        internal const int VertexLimit = 600000;
        internal struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Color Color;
            public Vector4[] UV;

            public static Vertex Lerp(Vertex a, Vertex b, float t)
            {
                var result = new Vertex
                {
                    Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                    Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t),
                    Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t),
                    Color = UnityEngine.Color.LerpUnclamped(a.Color, b.Color, t),
                    UV = new Vector4[8]
                };
                for (int i = 0; i < 8; i++)
                    result.UV[i] = Vector4.LerpUnclamped(a.UV[i], b.UV[i], t);
                return result;
            }
        }

        internal sealed class Source
        {
            public Vertex[] Vertices;
            public int[][] Indices;
            public bool HasNormals, HasTangents, HasColors;
            public bool[] HasUV = new bool[8];
            public Bounds Bounds;
        }


        internal static Source Read(Mesh mesh, Matrix4x4 matrix)
        {
            if (mesh == null)
                throw new ArgumentException("線路素材を指定してください。");
            if (matrix.determinant <= 0f)
                throw new ArgumentException("素材のScaleには正の値を指定してください。");
            var source = new Source();
            using (var dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = dataArray[0];
                int count = data.vertexCount;
                if (count == 0)
                    throw new ArgumentException("素材に頂点がありません。");
                source.HasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
                source.HasTangents = data.HasVertexAttribute(VertexAttribute.Tangent);
                source.HasColors = data.HasVertexAttribute(VertexAttribute.Color);
                source.Vertices = new Vertex[count];
                using (var values = new NativeArray<Vector3>(count, Allocator.Temp))
                {
                    data.GetVertices(values);
                    for (int i = 0; i < count; i++)
                    {
                        source.Vertices[i] = new Vertex { Position = matrix.MultiplyPoint3x4(values[i]), UV = new Vector4[8], Color = Color.white };
                    }
                    if (source.HasNormals)
                    {
                        data.GetNormals(values);
                        var normalMatrix = matrix.inverse.transpose;
                        for (int i = 0; i < count; i++)
                            source.Vertices[i].Normal = normalMatrix.MultiplyVector(values[i]).normalized;
                    }
                }
                using (var values = new NativeArray<Vector4>(count, Allocator.Temp))
                {
                    if (source.HasTangents)
                    {
                        data.GetTangents(values);
                        for (int i = 0; i < count; i++)
                        {
                            Vector3 direction = matrix.MultiplyVector((Vector3)values[i]).normalized;
                            source.Vertices[i].Tangent = new Vector4(direction.x, direction.y, direction.z, values[i].w);
                        }
                    }
                    for (int channel = 0; channel < 8; channel++)
                    {
                        source.HasUV[channel] = data.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel));
                        if (!source.HasUV[channel])
                            continue;
                        data.GetUVs(channel, values);
                        for (int i = 0; i < count; i++)
                            source.Vertices[i].UV[channel] = values[i];
                    }
                }
                if (source.HasColors)
                {
                    using (var values = new NativeArray<Color>(count, Allocator.Temp))
                    {
                        data.GetColors(values);
                        for (int i = 0; i < count; i++)
                            source.Vertices[i].Color = values[i];
                    }
                }
                source.Indices = new int[data.subMeshCount][];
                for (int sub = 0; sub < data.subMeshCount; sub++)
                {
                    var descriptor = data.GetSubMesh(sub);
                    if (descriptor.topology != MeshTopology.Triangles)
                        throw new ArgumentException("三角形の線路メッシュを指定してください。");
                    using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, sub, true);
                        source.Indices[sub] = indices.ToArray();
                    }
                }
            }
            source.Bounds = new Bounds(source.Vertices[0].Position, Vector3.zero);
            foreach (var vertex in source.Vertices)
                source.Bounds.Encapsulate(vertex.Position);
            return source;
        }


        internal static List<Vertex>[] Subdivide(Source source, float maxStep)
        {
            float length = source.Bounds.size.z;
            if (length < 0.001f)
                throw new ArgumentException("素材の長手方向を+Zへ合わせてください。");
            int slices = Mathf.CeilToInt(length / maxStep);
            if (slices > 4096)
                throw new ArgumentException("素材の長さまたは分割間隔を見直してください（最大4096分割）。");
            float step = length / slices;
            var result = new List<Vertex>[source.Indices.Length];
            int total = 0;
            for (int sub = 0; sub < result.Length; sub++)
            {
                result[sub] = new List<Vertex>();
                var indices = source.Indices[sub];
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var triangle = new List<Vertex> { source.Vertices[indices[i]], source.Vertices[indices[i + 1]], source.Vertices[indices[i + 2]] };
                    float min = Mathf.Min(triangle[0].Position.z, Mathf.Min(triangle[1].Position.z, triangle[2].Position.z));
                    float max = Mathf.Max(triangle[0].Position.z, Mathf.Max(triangle[1].Position.z, triangle[2].Position.z));
                    int first = Mathf.Clamp(Mathf.FloorToInt((min - source.Bounds.min.z) / step), 0, slices - 1);
                    int last = Mathf.Clamp(Mathf.CeilToInt((max - source.Bounds.min.z) / step) - 1, first, slices - 1);
                    for (int slice = first; slice <= last; slice++)
                    {
                        var polygon = Clip(triangle, source.Bounds.min.z + slice * step, true);
                        polygon = Clip(polygon, source.Bounds.min.z + (slice + 1) * step, false);
                        for (int p = 1; p + 1 < polygon.Count; p++)
                        {
                            if (Vector3.Cross(polygon[p].Position - polygon[0].Position, polygon[p + 1].Position - polygon[0].Position).sqrMagnitude < 1e-16f)
                                continue;
                            result[sub].Add(polygon[0]);
                            result[sub].Add(polygon[p]);
                            result[sub].Add(polygon[p + 1]);
                            total += 3;
                            if (total > VertexLimit)
                                throw new ArgumentException("分割後の素材が大きすぎます。分割間隔を広げてください。");
                        }
                    }
                }
            }
            return result;
        }


        internal static List<Vertex> Clip(List<Vertex> input, float z, bool keepAbove)
        {
            var output = new List<Vertex>();
            if (input.Count == 0)
                return output;
            Vertex previous = input[input.Count - 1];
            bool previousInside = keepAbove ? previous.Position.z >= z : previous.Position.z <= z;
            foreach (var current in input)
            {
                bool inside = keepAbove ? current.Position.z >= z : current.Position.z <= z;
                if (inside != previousInside)
                {
                    var intersection = Vertex.Lerp(previous, current, (z - previous.Position.z) / (current.Position.z - previous.Position.z));
                    intersection.Position.z = z;
                    output.Add(intersection);
                }
                if (inside)
                    output.Add(current);
                previous = current;
                previousInside = inside;
            }
            return output;
        }


        internal static Mesh MakeMesh(Source source, List<Vertex>[] triangles, Func<Vertex, Vertex> deform)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var colors = new List<Color>();
            var uv = new List<Vector4>[8];
            for (int channel = 0; channel < 8; channel++)
                uv[channel] = new List<Vector4>();
            var subIndices = new List<int>[triangles.Length];
            for (int sub = 0; sub < triangles.Length; sub++)
            {
                subIndices[sub] = new List<int>();
                foreach (var vertex in triangles[sub])
                {
                    if (positions.Count >= VertexLimit)
                        throw new ArgumentException("生成頂点数の上限を超えました。生成区間を分けるか分割間隔を広げてください。");
                    Vertex result = deform(vertex);
                    subIndices[sub].Add(positions.Count);
                    positions.Add(result.Position);
                    normals.Add(result.Normal);
                    tangents.Add(result.Tangent);
                    colors.Add(result.Color);
                    for (int channel = 0; channel < 8; channel++)
                        if (source.HasUV[channel])
                            uv[channel].Add(result.UV[channel]);
                }
            }
            if (positions.Count == 0)
                throw new ArgumentException("生成区間内に面がありません。");
            var mesh = new Mesh { name = "RailSplineMesh", indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            if (source.HasNormals)
                mesh.SetNormals(normals);
            if (source.HasColors)
                mesh.SetColors(colors);
            for (int channel = 0; channel < 8; channel++)
                if (source.HasUV[channel])
                    mesh.SetUVs(channel, uv[channel]);
            mesh.subMeshCount = triangles.Length;
            for (int sub = 0; sub < triangles.Length; sub++)
                mesh.SetTriangles(subIndices[sub], sub, false);
            if (!source.HasNormals)
                mesh.RecalculateNormals();
            if (source.HasTangents)
                mesh.SetTangents(tangents);
            else if (source.HasUV[0])
                mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
