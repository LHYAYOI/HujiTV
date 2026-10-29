using UnityEngine;
using UnityEngine.Serialization;

namespace FieldGhost
{
    /// <summary>
    /// 白の半透明カーテン。格子メッシュを自分で作り、ひだ・風・裏から押されたふくらみの値をシェーダへ渡す。
    /// このオブジェクトの位置がカーテンレールの中央。青の矢印（+Z）が部屋側、裏（-Z）が窓側。
    /// 風は窓（裏）から部屋側へ吹き、ときどき突風で裾がめくれる。
    /// 色や透け具合・ひだの陰はマテリアル（custom/hatake/FieldGhostCurtain）で調整する。
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Field Ghost/カーテン")]
    public sealed class FieldGhostCurtain : MonoBehaviour
    {
        public const int MaxBulges = 4;
        public const int MaxFolds = 4;

        public enum FoldAxis
        {
            [InspectorName("縦の折り目（そこから外側の左右を曲げる）")] Vertical,
            [InspectorName("横の折り目（そこから下を曲げる）")] Horizontal,
        }

        /// <summary>折り目（支点）。布はここを軸に曲がる。</summary>
        [System.Serializable]
        public struct Fold
        {
            [Tooltip("縦の折り目は中央から見て外側を、横の折り目はレールから見て下を曲げる。")]
            public FoldAxis axis;
            [Tooltip("折り目の位置（m）。縦: 中央から右へ（負で左）。横: レールから下へ負の値。")]
            public float position;
            [Tooltip("曲げる角度（度）。正で奥（窓・箱の側）へ、負で手前へ。0 で無効。")]
            [Range(-180, 180)] public float angle;
            [Tooltip("曲がりの丸み（半径 m）。小さいほど角が立つ。ひだの深さより大きくすると布が食い込まない。")]
            [Min(0.001f)] public float radius;

            public Fold(FoldAxis axis, float position, float angle, float radius)
            {
                this.axis = axis;
                this.position = position;
                this.angle = angle;
                this.radius = radius;
            }
        }

        public enum WrapSides
        {
            [InspectorName("前の 1 面")] Front,
            [InspectorName("前と左右の 3 面")] ThreeSides,
            [InspectorName("ぐるり 4 面")] AllAround,
        }

        public enum BoxFace
        {
            [InspectorName("前（+Z）")] Forward,
            [InspectorName("後ろ（-Z）")] Back,
            [InspectorName("右（+X）")] Right,
            [InspectorName("左（-X）")] Left,
        }

        [Header("大きさ")]
        [Min(0.1f), Tooltip("横幅（m）。")] public float width = 1.2f;
        [Min(0.1f), Tooltip("レールから裾までの長さ（m）。")] public float height = 2.4f;
        [Range(8, 160), Tooltip("横の分割数。細かいひだには『ひだの数 × 6』以上。")] public int columns = 112;
        [Range(8, 128), Tooltip("縦の分割数。")] public int rows = 72;

        [Header("ひだ")]
        [Min(0), Tooltip("ひだの数（横幅あたり）。")]
        [FormerlySerializedAs("foldCount")] public float pleatCount = 12;
        [Min(0), Tooltip("ひだの深さ（m）。")]
        [FormerlySerializedAs("foldDepth")] public float pleatDepth = 0.022f;
        [Range(0, 1), Tooltip("ひだの幅の不揃いさ。")] public float pleatIrregularity = 0.7f;
        [Min(0), Tooltip("裾ほど大きくなる、ゆるい大きな折れ目の数。")] public float largeFoldCount = 3.5f;

        [Header("風（窓から部屋側へ）")]
        [Min(0), Tooltip("ふだんのふくらみ（裾で、m）。0 で無風。")] public float windStrength = 0.08f;
        [Min(0), Tooltip("突風で足すふくらみ（裾で、m）。")] public float gustStrength = 0.35f;
        [Min(0.01f), Tooltip("突風の来る頻度（Hz の目安。0.15 で 7 秒に 1 回ほど）。")] public float gustFrequency = 0.15f;
        [Range(0, 1), Tooltip("突風の来やすさ。大きいほど突風の時間が長い。")] public float gustiness = 0.45f;
        [Tooltip("突風で裾がめくれて横へあおられる量（m）。正で右、負で左。")] public float lateralFlap = 0.3f;
        [Min(0), Tooltip("布を渡る細かい波の大きさ（m）。")] public float flutter = 0.03f;
        [Min(0), Tooltip("布を渡る波の速さ（Hz）。")] public float waveSpeed = 0.9f;
        [Min(0), Tooltip("風でひだが斜めにねじれる量。")] public float twist = 0.5f;
        [Min(0.01f), Tooltip("突風の立ち上がり・収まりの速さ（大きいほどきびきび）。")] public float gustResponse = 2.5f;
        [Tooltip("乱数の種。カーテンを複数置くときは変えると動きがそろわない。")] public int seed = 7;

        [Header("折り曲げ（支点）")]
        [Tooltip("最大 4 本。縦の折り目で角に巻き付け、横の折り目で縁から垂らす。")]
        public Fold[] folds = new Fold[0];

        [Header("箱に貼り付ける（インスペクタのボタンで位置・大きさ・折り目を合わせる）")]
        [Tooltip("貼り付ける箱。BoxCollider があればその大きさ、無ければメッシュの大きさを使う。")]
        public Transform wrapTarget;
        [Tooltip("どの面に貼るか。")] public WrapSides wrapSides = WrapSides.ThreeSides;
        [Tooltip("正面にする箱の面（箱から見た向き）。")] public BoxFace wrapFace = BoxFace.Forward;
        [Tooltip("箱の表面から浮かせる距離（m）。")] [Min(0)] public float wrapGap = 0.02f;
        [Tooltip("角の丸み（半径 m）。")] [Min(0.001f)] public float wrapCornerRadius = 0.05f;
        [Tooltip("ONなら高さを箱の高さに合わせる。")] public bool wrapFitHeight = true;

        [Header("影")]
        [Tooltip("ONなら影を落とし、窓からの光（床の光の溜まり）や光の筋（LightShaft）をさえぎる。濃さはマテリアルの『光をさえぎる強さ』。")]
        public bool castShadows = true;
        [Tooltip("ONなら God Ray（LightShaft の、空から放射状に出る光）を遮る。濃さは影と同じ『光をさえぎる強さ』。")]
        public bool blockGodRays = true;

        [Header("ふくらみ")]
        [Tooltip("ふくらみの最大の深さ（m）。描画範囲の広げ幅にも使う。")]
        [Min(0)] public float maxBulgeDepth = 0.35f;

        /// <summary>風の強さの倍率（演出中に弱めるなど）。1 でそのまま。</summary>
        public float WindScale { get; set; } = 1;

        static readonly int SizeId = Shader.PropertyToID("_CurtainSize");
        static readonly int WindId = Shader.PropertyToID("_Wind");
        static readonly int Wind2Id = Shader.PropertyToID("_Wind2");
        static readonly int AspectId = Shader.PropertyToID("_BulgeAspect");
        static readonly int FaceId = Shader.PropertyToID("_FaceDetail");
        static readonly int GodRayBlockId = Shader.PropertyToID("_GodRayBlock");
        static readonly int[] FoldIds =
        {
            Shader.PropertyToID("_Fold0"), Shader.PropertyToID("_Fold1"),
            Shader.PropertyToID("_Fold2"), Shader.PropertyToID("_Fold3"),
        };
        readonly Vector4[] packedFolds = new Vector4[MaxFolds];
        static readonly int[] BulgeIds =
        {
            Shader.PropertyToID("_Bulge0"), Shader.PropertyToID("_Bulge1"),
            Shader.PropertyToID("_Bulge2"), Shader.PropertyToID("_Bulge3"),
        };

        readonly Vector4[] bulges = new Vector4[MaxBulges];
        readonly float[] aspects = { 1, 1, 1, 1 };
        float faceDetail;
        float gust;
        float extraGust;
        float wavePhase;
        float windTime;
        float appliedWindScale = 1;
        Mesh mesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        int builtColumns, builtRows;
        float builtWidth, builtHeight, builtKey;

        /// <summary>
        /// ふくらみを置く。index 0 は顔（faceDetail で目と口のへこみが付く）。
        /// localPosition は布の上の位置（x: 中央から右へ m、y: レールから下へ負の m）。depth は部屋側への押し出し（m）。
        /// </summary>
        public void SetBulge(int index, Vector2 localPosition, float radius, float depth, float aspect = 1)
        {
            if (index < 0 || index >= MaxBulges)
            {
                return;
            }
            bulges[index] = new Vector4(localPosition.x, localPosition.y, Mathf.Max(radius, 0.001f), Mathf.Clamp(depth, 0, maxBulgeDepth));
            aspects[index] = Mathf.Max(aspect, 0.1f);
        }

        /// <summary>顔のへこみの強さ（0〜1）。index 0 のふくらみにだけ効く。</summary>
        public void SetFaceDetail(float amount) => faceDetail = Mathf.Clamp01(amount);

        public void ClearBulges()
        {
            for (int i = 0; i < MaxBulges; i++)
            {
                bulges[i] = new Vector4(0, 0, 0.1f, 0);
                aspects[i] = 1;
            }
            faceDetail = 0;
        }

        /// <summary>突風を起こす（0〜1。1 で最大の突風）。押していた手が離れたときなどに。</summary>
        public void Kick(float amount) => extraGust = Mathf.Clamp01(extraGust + Mathf.Max(amount, 0));

        /// <summary>布の上の位置（SetBulge と同じ座標）をワールド座標にする。</summary>
        public Vector3 LocalToWorld(Vector2 localPosition)
        {
            PackFolds();
            return transform.TransformPoint(FoldPoint(new Vector3(localPosition.x, localPosition.y, 0)));
        }

        void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            ClearBulges();
            gust = 0;
            extraGust = 0;
            Rebuild();
            Apply();
        }

        void OnDisable()
        {
            if (mesh)
            {
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh == mesh)
                {
                    filter.sharedMesh = null;
                }
                DestroyImmediate(mesh);
                mesh = null;
            }
        }

        // OnValidate ではメッシュを差し替えない（Unity の警告が出るため）。次の LateUpdate で作り直す。

        void LateUpdate()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0;
            Simulate(dt);
            if (NeedsRebuild())
            {
                Rebuild();
            }
            Apply();
        }

        // 突風はなめらかなノイズで決める。ノイズが閾値を越えた分だけ吹き、なめらかに立ち上がって収まる。
        void Simulate(float dt)
        {
            windTime += dt;
            float noise = Mathf.PerlinNoise(windTime * gustFrequency, seed * 13.37f + 0.5f);
            float threshold = Mathf.Lerp(0.75f, 0.3f, gustiness);
            float target = Mathf.Clamp01((noise - threshold) / Mathf.Max(1 - threshold, 0.01f) * 1.6f);
            target = Mathf.Max(target, extraGust);
            gust = Mathf.Lerp(gust, target, 1 - Mathf.Exp(-gustResponse * dt));
            extraGust *= Mathf.Exp(-0.8f * dt);
            // 風の倍率はなめらかに追う（急に戻して布が跳ねないように）。止まっているときはそのまま。
            appliedWindScale = dt > 0 ? Mathf.Lerp(appliedWindScale, Mathf.Max(WindScale, 0), 1 - Mathf.Exp(-3 * dt)) : Mathf.Max(WindScale, 0);
            // 強く吹いているほど波が速く渡る。
            wavePhase = Mathf.Repeat(wavePhase + dt * Mathf.PI * 2 * waveSpeed * (0.5f + gust), Mathf.PI * 200);
        }

        // シェーダへ渡す形にする: (向き, 位置, 角度 rad, 半径)。支点からの距離の近い順に並べる（シェーダはこの順に曲げる）。
        void PackFolds()
        {
            int count = 0;
            if (folds != null)
            {
                foreach (Fold fold in folds)
                {
                    if (count >= MaxFolds)
                    {
                        break;
                    }
                    if (Mathf.Abs(fold.angle) < 0.01f)
                    {
                        continue;
                    }
                    packedFolds[count++] = new Vector4(fold.axis == FoldAxis.Horizontal ? 1 : 0, fold.position, fold.angle * Mathf.Deg2Rad, Mathf.Max(fold.radius, 0.001f));
                }
            }
            for (int i = count; i < MaxFolds; i++)
            {
                packedFolds[i] = new Vector4(0, 0, 0, 0.03f);
            }
            // 挿入ソート（4 個まで）。
            for (int i = 1; i < count; i++)
            {
                Vector4 key = packedFolds[i];
                int j = i - 1;
                while (j >= 0 && HingeDistance(packedFolds[j]) > HingeDistance(key))
                {
                    packedFolds[j + 1] = packedFolds[j];
                    j--;
                }
                packedFolds[j + 1] = key;
            }
        }

        static float HingeDistance(Vector4 fold) => fold.x > 0.5f ? -fold.y : Mathf.Abs(fold.y);

        // シェーダの FoldPoint と同じ計算（ギズモとワールド座標の計算用）。PackFolds のあとに呼ぶ。
        Vector3 FoldPoint(Vector3 p)
        {
            Vector2 down = FoldStrip(Mathf.Max(-p.y, 0), p.z, 1);
            float y = p.y > 0 ? p.y : -down.x;
            float z = p.y > 0 ? p.z : down.y;
            float side = p.x >= 0 ? 1 : -1;
            Vector2 across = FoldStrip(Mathf.Abs(p.x), z, 0);
            return new Vector3(across.x * side, y, across.y);
        }

        Vector2 FoldStrip(float s, float z, float axis)
        {
            Vector2 origin = Vector2.zero;
            Vector2 tangent = new Vector2(1, 0);
            Vector2 normal = new Vector2(0, 1);
            float start = 0;
            for (int i = 0; i < MaxFolds; i++)
            {
                Vector4 fold = packedFolds[i];
                float hinge = axis > 0.5f ? -fold.y : Mathf.Abs(fold.y);
                bool active = Mathf.Abs(fold.x - axis) < 0.5f && Mathf.Abs(fold.z) > 1e-4f && s > hinge && hinge >= start;
                if (!active)
                {
                    continue;
                }
                float radius = Mathf.Max(fold.w, 1e-3f);
                float bend = fold.z >= 0 ? 1 : -1;
                Vector2 h = origin + tangent * (hinge - start);
                Vector2 center = h - normal * (radius * bend);
                float phi = Mathf.Min(s - hinge, radius * Mathf.Abs(fold.z)) / radius * bend;
                Vector2 t2 = tangent * Mathf.Cos(phi) - normal * Mathf.Sin(phi);
                Vector2 n2 = normal * Mathf.Cos(phi) + tangent * Mathf.Sin(phi);
                origin = center + n2 * (radius * bend);
                start = hinge + Mathf.Abs(phi) * radius;
                tangent = t2;
                normal = n2;
            }
            return origin + tangent * (s - start) + normal * z;
        }

        bool HasFolds()
        {
            if (folds == null)
            {
                return false;
            }
            foreach (Fold fold in folds)
            {
                if (Mathf.Abs(fold.angle) >= 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// wrapTarget の箱の面に合わせて、位置・向き・幅・高さ・折り目を決める。
        /// 箱の上端の、正面の面から wrapGap だけ浮いた所がレールになる。
        /// </summary>
        public bool FitToBox()
        {
            if (!wrapTarget || !TryGetBox(wrapTarget, out Vector3 localCenter, out Vector3 localSize))
            {
                return false;
            }
            Vector3 scale = wrapTarget.lossyScale;
            Vector3 size = new Vector3(Mathf.Abs(localSize.x * scale.x), Mathf.Abs(localSize.y * scale.y), Mathf.Abs(localSize.z * scale.z));
            Vector3 center = wrapTarget.TransformPoint(localCenter);
            Vector3 up = wrapTarget.up;
            Vector3 outward;
            float faceWidth;
            float depth;
            switch (wrapFace)
            {
                case BoxFace.Back:
                    outward = -wrapTarget.forward; faceWidth = size.x; depth = size.z;
                    break;
                case BoxFace.Right:
                    outward = wrapTarget.right; faceWidth = size.z; depth = size.x;
                    break;
                case BoxFace.Left:
                    outward = -wrapTarget.right; faceWidth = size.z; depth = size.x;
                    break;
                default:
                    outward = wrapTarget.forward; faceWidth = size.x; depth = size.z;
                    break;
            }
            float gap = wrapGap;
            float r = Mathf.Min(wrapCornerRadius, Mathf.Min(faceWidth, depth) * 0.5f + gap);
            float frontHalf = faceWidth * 0.5f + gap;
            float sideLength = depth + gap * 2;
            float quarter = r * Mathf.PI * 0.5f;
            // 正面の中央をレールの中央にする。
            transform.SetPositionAndRotation(center + outward * (depth * 0.5f + gap) + up * (size.y * 0.5f), Quaternion.LookRotation(outward, up));
            if (wrapFitHeight)
            {
                height = Mathf.Max(size.y, 0.1f);
            }
            float first = frontHalf - r;
            switch (wrapSides)
            {
                case WrapSides.Front:
                    width = frontHalf * 2;
                    folds = new Fold[0];
                    break;
                case WrapSides.ThreeSides:
                    // 側面は奥の端まで。
                    width = (first + quarter + (sideLength - r)) * 2;
                    folds = new[]
                    {
                        new Fold(FoldAxis.Vertical, first, 90, r),
                        new Fold(FoldAxis.Vertical, -first, 90, r),
                    };
                    break;
                default:
                    // 後ろの面の中央で左右の布が合わさる。
                    float second = first + quarter + (sideLength - 2 * r);
                    width = (second + quarter + (frontHalf - r)) * 2;
                    folds = new[]
                    {
                        new Fold(FoldAxis.Vertical, first, 90, r),
                        new Fold(FoldAxis.Vertical, -first, 90, r),
                        new Fold(FoldAxis.Vertical, second, 90, r),
                        new Fold(FoldAxis.Vertical, -second, 90, r),
                    };
                    break;
            }
            return true;
        }

        static bool TryGetBox(Transform target, out Vector3 center, out Vector3 size)
        {
            var box = target.GetComponent<BoxCollider>();
            if (box)
            {
                center = box.center;
                size = box.size;
                return true;
            }
            var filter = target.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                center = bounds.center;
                size = bounds.size;
                return true;
            }
            center = Vector3.zero;
            size = Vector3.zero;
            return false;
        }

        bool NeedsRebuild()
        {
            return !mesh || builtColumns != columns || builtRows != rows
                || !Mathf.Approximately(builtWidth, width) || !Mathf.Approximately(builtHeight, height)
                || !Mathf.Approximately(builtKey, BuildKey());
        }

        // 描画範囲に効く値をまとめた目印。変わったらメッシュ（の描画範囲）を作り直す。
        float BuildKey() => BoundsDepth() + (HasFolds() ? 1000 : 0);

        float BoundsDepth() => maxBulgeDepth + pleatDepth * 2 + windStrength * 1.4f + gustStrength * 1.4f + flutter + Mathf.Abs(lateralFlap) * 1.2f;

        void Apply()
        {
            if (!meshRenderer)
            {
                return;
            }
            var shadowMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (meshRenderer.shadowCastingMode != shadowMode)
            {
                meshRenderer.shadowCastingMode = shadowMode;
            }
            float scale = appliedWindScale;
            float g = gust * scale;
            block ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetVector(SizeId, new Vector4(width, height, pleatCount, pleatDepth));
            // x: 裾のふくらみ, y: 横へのめくれ, z: 波の位相, w: 細かい波
            block.SetVector(WindId, new Vector4(
                windStrength * scale + gustStrength * g,
                lateralFlap * g * g,
                wavePhase,
                flutter * (0.3f + g) * Mathf.Min(scale, 1)));
            // x: ねじれ, y: ひだの不揃いさ, z: 乱数の種, w: 大きな折れ目の数
            block.SetVector(Wind2Id, new Vector4(twist * (0.25f + g), pleatIrregularity, seed * 0.618f, largeFoldCount));
            for (int i = 0; i < MaxBulges; i++)
            {
                block.SetVector(BulgeIds[i], bulges[i]);
            }
            block.SetVector(AspectId, new Vector4(aspects[0], aspects[1], aspects[2], aspects[3]));
            block.SetFloat(FaceId, faceDetail);
            block.SetFloat(GodRayBlockId, blockGodRays ? 1 : 0);
            PackFolds();
            for (int i = 0; i < MaxFolds; i++)
            {
                block.SetVector(FoldIds[i], packedFolds[i]);
            }
            meshRenderer.SetPropertyBlock(block);
        }

        // z=0 の平らな格子。上端（y=0）がレール、下端（y=-height）が裾。形はシェーダで付ける。
        void Rebuild()
        {
            if (!mesh)
            {
                mesh = new Mesh { name = "FieldGhostCurtain", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            int cx = Mathf.Max(columns, 1);
            int cy = Mathf.Max(rows, 1);
            var vertices = new Vector3[(cx + 1) * (cy + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (int y = 0; y <= cy; y++)
            {
                for (int x = 0; x <= cx; x++)
                {
                    int i = y * (cx + 1) + x;
                    float u = (float)x / cx;
                    float v = (float)y / cy;
                    vertices[i] = new Vector3((u - 0.5f) * width, -v * height, 0);
                    normals[i] = Vector3.forward;
                    uvs[i] = new Vector2(u, 1 - v);
                }
            }
            var triangles = new int[cx * cy * 6];
            int t = 0;
            for (int y = 0; y < cy; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    int i = y * (cx + 1) + x;
                    // 表面が +Z（部屋側）を向く順番。行は下へ進むので、右→下の順だと裏返る。
                    triangles[t++] = i;
                    triangles[t++] = i + cx + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + cx + 1;
                    triangles[t++] = i + cx + 2;
                }
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // シェーダで前後左右に動くので、その分だけ描画範囲を広げておく。
            float depth = BoundsDepth();
            if (HasFolds())
            {
                // 曲げた布は中央・レールから布の長さより遠くへは行かない。
                float reach = Mathf.Max(width * 0.5f, height) + depth;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(reach * 2, reach * 2, reach * 2));
            }
            else
            {
                mesh.bounds = new Bounds(new Vector3(0, -height * 0.5f, depth * 0.5f), new Vector3(width + depth * 2, height + depth * 2, depth * 3));
            }
            GetComponent<MeshFilter>().sharedMesh = mesh;
            builtColumns = columns;
            builtRows = rows;
            builtWidth = width;
            builtHeight = height;
            builtKey = BuildKey();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 1, 0.6f);
            PackFolds();
            // 曲げたあとの上端・下端・左右端を線で描く。
            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(-width * 0.5f, width * 0.5f, (float)i / steps);
                float b = Mathf.Lerp(-width * 0.5f, width * 0.5f, (float)(i + 1) / steps);
                Gizmos.DrawLine(FoldPoint(new Vector3(a, 0, 0)), FoldPoint(new Vector3(b, 0, 0)));
                Gizmos.DrawLine(FoldPoint(new Vector3(a, -height, 0)), FoldPoint(new Vector3(b, -height, 0)));
                float c = Mathf.Lerp(0, -height, (float)i / steps);
                float d = Mathf.Lerp(0, -height, (float)(i + 1) / steps);
                Gizmos.DrawLine(FoldPoint(new Vector3(-width * 0.5f, c, 0)), FoldPoint(new Vector3(-width * 0.5f, d, 0)));
                Gizmos.DrawLine(FoldPoint(new Vector3(width * 0.5f, c, 0)), FoldPoint(new Vector3(width * 0.5f, d, 0)));
            }
            // 折り目（支点）を黄色で。
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            if (folds != null)
            {
                foreach (Fold fold in folds)
                {
                    if (Mathf.Abs(fold.angle) < 0.01f)
                    {
                        continue;
                    }
                    if (fold.axis == FoldAxis.Vertical)
                    {
                        Gizmos.DrawLine(FoldPoint(new Vector3(fold.position, 0, 0)), FoldPoint(new Vector3(fold.position, -height, 0)));
                    }
                    else
                    {
                        Gizmos.DrawLine(FoldPoint(new Vector3(-width * 0.5f, fold.position, 0)), FoldPoint(new Vector3(width * 0.5f, fold.position, 0)));
                    }
                }
            }
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawLine(new Vector3(0, -height * 0.5f, 0), new Vector3(0, -height * 0.5f, 0.4f));
        }
    }
}
