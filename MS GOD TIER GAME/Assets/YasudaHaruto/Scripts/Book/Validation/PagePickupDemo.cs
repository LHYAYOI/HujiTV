using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YasudaHaruto
{
    [DefaultExecutionOrder(200)]
    public sealed class PagePickupDemo : MonoBehaviour
    {
        [SerializeField] private RoutePageRewards m_rewards;
        [SerializeField] private CartController m_cart;
        [SerializeField] private Transform m_pickupTarget;
        [SerializeField] private SplineController2 m_bedroomRail;
        [SerializeField] private ActiveEnemyManager m_enemies;
        [SerializeField] private MagicBulletManager m_bullets;
        [SerializeField] private MagicData m_practiceMagic;

        private readonly List<byte> m_received = new();
        private float m_nextShot;
        private bool m_skippedToExit;
        private GUIStyle m_text;

        private void OnEnable() => m_rewards.PageArrived += OnPageArrived;

        private void OnDisable() => m_rewards.PageArrived -= OnPageArrived;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.spaceKey.wasPressedThisFrame)
                Shoot();
            if (keyboard.fKey.wasPressedThisFrame)
                GoToBedroomExit();
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null)
                m_pickupTarget.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.43f, 1.65f));
        }

        private void Shoot()
        {
            if (m_cart.Speed > 0f || Time.time < m_nextShot)
                return;

            TargetableObject target = null;
            float nearest = float.PositiveInfinity;
            foreach (var enemy in m_enemies.GetActiveEnemies())
            {
                if (enemy is not MiniGhostEnemy mini || mini == null || mini.GetIsDying())
                    continue;
                var candidate = mini.GetTargetableObject();
                if (candidate == null || !candidate.IsTargetable())
                    continue;
                float distance = (candidate.transform.position - m_cart.transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    target = candidate;
                    nearest = distance;
                }
            }
            if (target == null)
                return;

            m_nextShot = Time.time + 0.7f;
            m_bullets.FireBullet(m_practiceMagic, MAGIC_CAST_DIRECTION.CENTER, target.gameObject);
        }

        private void GoToBedroomExit()
        {
            if (m_skippedToExit || m_received.Contains(1) || m_cart.Speed <= 0f)
                return;
            m_skippedToExit = true;
            m_cart.SetSpline(m_bedroomRail.Spline, m_bedroomRail.Spline.CalculateLength() - 3f,
                m_bedroomRail.LoopFlag, true);
        }

        private void OnPageArrived(byte pageId) => m_received.Add(pageId);

        private void OnGUI()
        {
            m_text ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            GUILayout.BeginArea(new Rect(16f, 16f, Mathf.Min(Screen.width - 32f, 340f), 158f), GUI.skin.box);
            GUILayout.Label("PAGE PICKUP  /  ALPHA", m_text);
            GUILayout.Label(m_received.Count == 0 ? "Watch for the page after a defeat." :
                $"Acquired page IDs   {string.Join("  /  ", m_received)}", m_text);
            using (new GUIEnabledScope(m_cart.Speed <= 0f))
            {
                if (GUILayout.Button("Cast magic at a mini ghost  [Space]", GUILayout.Height(27f)))
                    Shoot();
            }
            if (!m_skippedToExit && !m_received.Contains(1) && m_cart.Speed > 0f &&
                GUILayout.Button("Try the bedroom exit  [F]", GUILayout.Height(27f)))
                GoToBedroomExit();
            GUILayout.EndArea();
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool m_previous;

            public GUIEnabledScope(bool enabled)
            {
                m_previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose() => GUI.enabled = m_previous;
        }
    }
}
