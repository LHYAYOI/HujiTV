using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    /// <summary>
    /// 扉から押し出される煙パーティクルの設定を、エディタのメニューと実行時の生成で共通に使う。
    /// </summary>
    public static class DoorSmokeParticles
    {
        public const string k_shaderName = "custom/hatake/SmokePuff";

        [System.Serializable]
        public struct Settings
        {
            [Min(0.01f), Tooltip("煙が出る隙間の幅（m）。")]
            public float m_emitterWidth;
            [Tooltip("1粒が消えるまでの秒数（最小・最大）。")]
            public Vector2 m_lifetime;
            [Tooltip("押し出す速さ m/s（最小・最大）。")]
            public Vector2 m_speed;
            [Tooltip("粒の大きさ m（最小・最大）。")]
            public Vector2 m_size;
            [Tooltip("粒の色と濃さ。")]
            public Color m_color;
            [Tooltip("重力の倍率。正で床に沈む。")]
            public float m_gravity;
            [Min(0), Tooltip("空気抵抗。大きいほどすぐ減速して床に広がる。")]
            public float m_drag;
            [Min(1)]
            public int m_maxParticles;

            public static Settings Default
            {
                get
                {
                    return new Settings
                    {
                        m_emitterWidth = 1.1f,
                        m_lifetime = new Vector2(4, 6.5f),
                        m_speed = new Vector2(0.8f, 2),
                        m_size = new Vector2(0.5f, 1.1f),
                        m_color = new Color(1, 1, 1, 0.6f),
                        m_gravity = 0.05f,
                        m_drag = 0.7f,
                        m_maxParticles = 220
                    };
                }
            }
        }

        /// <summary>
        /// 煙パーティクルを設定する。再生中に呼んだ場合は、止めずに変えられる項目だけ変える。
        /// 量（Emission）は DoorSmoke が動かすので 0 にしておく。
        /// </summary>
        public static void Configure(ParticleSystem system, Material material, Settings settings, float previewRate)
        {
            bool playingFlag = system.isPlaying;
            var main = system.main;
            if (!playingFlag)
            {
                main.duration = 5;
            }
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.m_lifetime.x, settings.m_lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(settings.m_speed.x, settings.m_speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.m_size.x, settings.m_size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = settings.m_color;
            main.gravityModifier = settings.m_gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = settings.m_maxParticles;

            var emission = system.emission;
            emission.rateOverTime = previewRate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0, 0.15f, 0.1f);
            shape.scale = new Vector3(settings.m_emitterWidth, 0.1f, 0.05f);
            shape.randomDirectionAmount = 0.25f;

            // 押し出された煙は空気抵抗で減速し、床に沿って広がる
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100;
            limit.drag = settings.m_drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.5f, 0, 2.5f), new Keyframe(0.4f, 1.2f), new Keyframe(1, 1.8f)));

            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0, 1) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.15f;
            noise.octaveCount = 2;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // 床と壁に当てて、床を這わせ、壁で止める
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            // Low は当たり判定を格子でキャッシュする。煙の粒には十分で、CPU の負荷が小さい
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.voxelSize = 0.5f;
            collision.dampen = 0.3f;
            collision.bounce = 0;
            collision.lifetimeLoss = 0;
            collision.radiusScale = 0.35f;
            collision.enableDynamicColliders = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            // 画面に対する粒の大きさの上限。カメラ直前の粒が画面を覆って重くなるのを防ぐ
            renderer.maxParticleSize = 0.6f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            // シェーダーが TEXCOORD0.zw で粒ごとの乱数と経過割合を読む
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomX,
                ParticleSystemVertexStream.AgePercent
            });

            if (!playingFlag)
            {
                system.Play(true);
            }
        }
    }
}
