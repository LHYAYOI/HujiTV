using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace hatake
{
    /// <summary>
    /// シャンデリアが床に当たった瞬間の演出。Particle System をコードで組み立てる（アセットの手作業が要らない）。
    ///   ・粉じん：床に沿って輪に広がる煙と、中央から立ちのぼる煙
    ///   ・火花：上へ跳ね、重力で落ちて床で跳ね返る（伸びる板）
    ///   ・結晶片：灯りのガラス・飾りの破片がきらめきながら散る
    ///   ・閃光：点光源を一瞬強く光らせる
    ///   ・揺れ：Cinemachine Impulse Source があればそれで、無ければ Main Camera を少し揺らす
    ///   ・音：Audio Source と Impact Clip があれば鳴らす
    /// 大きさは SetSize（ChandelierBreakable が自分の大きさで呼ぶ）に合わせる。強さは Play の strength（1 = 割れる最小の速さ）
    /// </summary>
    public sealed class ChandelierImpactEffect : MonoBehaviour
    {
        [Header("マテリアル（hatake/chandelier/materials）")]
        [SerializeField] private Material m_dustMaterial;
        [SerializeField] private Material m_sparkMaterial;
        [SerializeField] private Material m_shardMaterial;

        [Header("量と大きさ")]
        [Tooltip("全体の大きさの倍率（1 = 対角線 10 m のシャンデリアに合わせた控えめな大きさ）。粒の大きさ・広がる範囲・閃光の届く範囲が変わる")]
        [SerializeField, Min(0.01f)] private float m_scale = 2.2f;
        [SerializeField, Min(0)] private int m_dustCount = 130;
        [SerializeField, Min(0)] private int m_sparkCount = 170;
        [SerializeField, Min(0)] private int m_shardCount = 90;
        [SerializeField] private Color m_dustColor = new Color(0.55f, 0.5f, 0.45f, 0.55f);
        [SerializeField] private Color m_shardColor = new Color(0.85f, 0.95f, 1f, 1f);

        [Header("閃光")]
        [SerializeField, Min(0)] private float m_flashIntensity = 40f;
        [SerializeField] private Color m_flashColor = new Color(1f, 0.8f, 0.55f);
        [SerializeField, Min(0.01f)] private float m_flashDuration = 0.25f;

        [Header("音")]
        [SerializeField] private AudioSource m_audio;
        [SerializeField] private AudioClip m_impactClip;

        private ParticleSystem m_dustRing;
        private ParticleSystem m_dustPuff;
        private ParticleSystem m_sparks;
        private ParticleSystem m_shards;
        private Light m_flash;
        private float m_builtScale = -1f;
        private float m_size = 10f;
        private Coroutine m_flashRoutine;
        private Coroutine m_shakeRoutine;
        private Transform m_shakenCamera;
        private Vector3 m_shakeOrigin;

        /// <summary>演出の大きさを、対象の大きさ（外形の対角線、m）に合わせる</summary>
        public void SetSize(float size)
        {
            m_size = Mathf.Max(0.1f, size);
        }

        /// <summary>point で、normal の向きの面に当たった演出を出す。strength：1 = 割れる最小の強さ</summary>
        public void Play(Vector3 point, Vector3 normal, float strength)
        {
            float s = m_scale * m_size / 10f;
            if (!Mathf.Approximately(s, m_builtScale)) Build(s);
            transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0f ? normal : Vector3.up));
            float amount = Mathf.Clamp(strength, 0.6f, 2.5f);
            Burst(m_dustRing, m_dustCount * amount);
            Burst(m_dustPuff, m_dustCount * 0.4f * amount);
            Burst(m_sparks, m_sparkCount * amount);
            Burst(m_shards, m_shardCount * amount);

            if (m_flash != null)
            {
                if (m_flashRoutine != null) StopCoroutine(m_flashRoutine);
                m_flashRoutine = StartCoroutine(Flash(amount));
            }


            //=========================================================================================================
            // カメラシェイク入れるならココ！！

            //=========================================================================================================

            CameraManager.Instance.ShakeCameraInVHF(2.0f, 3.0f, 0.0f, 3.0f);

            CameraManager.Instance.ChangeCameraFOV010(3.0f, 70);

            if (m_audio != null && m_impactClip != null) m_audio.PlayOneShot(m_impactClip, Mathf.Clamp01(0.5f + 0.25f * amount));
        }

        // 一度に出す。前の粒がまだ残っていても（再生中でも）その場で出す
        private static void Burst(ParticleSystem ps, float count)
        {
            if (ps == null) return;
            if (!ps.isPlaying) ps.Play(false);
            ps.Emit(Mathf.Max(0, Mathf.RoundToInt(count)));
        }

        // ------------------------------------------------------------------
        // 組み立て（大きさ s が変わったら作り直す）
        // ------------------------------------------------------------------
        private void Build(float s)
        {
            m_builtScale = s;
            if (m_dustRing != null) Destroy(m_dustRing.gameObject);
            if (m_dustPuff != null) Destroy(m_dustPuff.gameObject);
            if (m_sparks != null) Destroy(m_sparks.gameObject);
            if (m_shards != null) Destroy(m_shards.gameObject);
            float r = Mathf.Sqrt(s);
            m_dustRing = BuildDustRing(s);
            m_dustPuff = BuildDustPuff(s);
            m_sparks = BuildSparks(s, r);
            m_shards = BuildShards(s, r);
            if (m_flash == null)
            {
                var go = new GameObject("Flash");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.5f;
                m_flash = go.AddComponent<Light>();
                m_flash.type = LightType.Point;
                m_flash.shadows = LightShadows.None;
                m_flash.enabled = false;
            }
            m_flash.color = m_flashColor;
            m_flash.range = 8f * s;
            m_flash.transform.localPosition = Vector3.up * 0.5f * s;
        }

        private ParticleSystem CreateSystem(string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = maxParticles;
            // 出すのは Burst（Emit）だけ。時間ごとには出さない
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            else Debug.LogWarning("ChandelierImpactEffect : " + name + " のマテリアルが未設定です", this);
            return ps;
        }

        // 床に沿って輪に広がる粉じん
        private ParticleSystem BuildDustRing(float s)
        {
            ParticleSystem ps = CreateSystem("DustRing", m_dustMaterial, 1000);
            // 板の下半分が床に埋まって直線で切れないよう、少し浮かせる
            ps.transform.localPosition = Vector3.up * 0.35f * s;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * s, 5f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * s, 1.6f * s);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(m_dustColor, m_dustColor * new Color(0.75f, 0.75f, 0.75f, 0.8f));
            main.gravityModifier = -0.02f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f * s;
            shape.radiusThickness = 0f;
            // 円は XY 平面に出るので、床（XZ）へ倒す
            shape.rotation = new Vector3(90f, 0f, 0f);
            DustCommon(ps, 2.5f);
            return ps;
        }

        // 中央から立ちのぼる粉じん
        private ParticleSystem BuildDustPuff(float s)
        {
            ParticleSystem ps = CreateSystem("DustPuff", m_dustMaterial, 500);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * s, 1.8f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f * s, 2.6f * s);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(m_dustColor, m_dustColor * new Color(0.8f, 0.8f, 0.8f, 0.7f));
            main.gravityModifier = -0.05f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1f * s;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            DustCommon(ps, 1.2f);
            return ps;
        }

        private static void DustCommon(ParticleSystem ps, float drag)
        {
            // 空気の抵抗で止まりながら膨らみ、薄れて消える
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.dampen = 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white, 0.12f, 0.4f));
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 火花：上へ跳ねて落ち、床で跳ね返る
        private ParticleSystem BuildSparks(float s, float r)
        {
            ParticleSystem ps = CreateSystem("Sparks", m_sparkMaterial, 1000);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * r, 11f * r);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f * r, 0.07f * r);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.6f, 0.25f));
            main.gravityModifier = 1.2f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f * s;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.5f), new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            Bounce(ps, 0.45f, 0.35f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // 結晶片：くるくる回りながら散り、床で跳ねて止まる
        private ParticleSystem BuildShards(float s, float r)
        {
            ParticleSystem ps = CreateSystem("Shards", m_shardMaterial, 600);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * r, 6.5f * r);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * r, 0.16f * r);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(m_shardColor, m_shardColor * new Color(0.7f, 0.8f, 1f, 1f));
            main.gravityModifier = 1.4f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.8f * s;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white, 0.01f, 0.75f));
            Bounce(ps, 0.35f, 0.4f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // 床・壁で跳ね返る
        private static void Bounce(ParticleSystem ps, float bounce, float dampen)
        {
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = bounce;
            collision.dampen = dampen;
            collision.lifetimeLoss = 0.1f;
            collision.radiusScale = 0.5f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
        }

        // 現れて（fadeIn まで）、fadeOut から消える不透明度の変化
        private static Gradient Fade(Color start, Color end, float fadeIn, float fadeOut)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOut), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------
        // 閃光と揺れ
        // ------------------------------------------------------------------
        private IEnumerator Flash(float amount)
        {
            m_flash.enabled = true;
            for (float t = 0f; t < m_flashDuration; t += Time.deltaTime)
            {
                float k = 1f - t / m_flashDuration;
                m_flash.intensity = m_flashIntensity * amount * k * k;
                yield return null;
            }
            m_flash.enabled = false;
            m_flashRoutine = null;
        }


        private void OnDisable()
        {
            if (m_shakenCamera != null) m_shakenCamera.localPosition = m_shakeOrigin;
            m_shakenCamera = null;
            m_shakeRoutine = null;
            m_flashRoutine = null;
            if (m_flash != null) m_flash.enabled = false;
        }
    }
}
