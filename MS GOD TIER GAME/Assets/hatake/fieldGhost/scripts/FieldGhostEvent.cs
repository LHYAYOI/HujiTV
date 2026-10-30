using System.Collections;
using UnityEngine;

namespace FieldGhost
{
    /// <summary>
    /// フィールドのお化け演出の共通部分。出る条件（近づいたら・確率・1回だけ・画面に入っているときだけ）と、
    /// 演出の再生・停止をまとめる。演出そのものは派生クラスの Sequence に書く。
    /// 当たり判定は持たない（見せるだけ）。
    /// </summary>
    public abstract class FieldGhostEvent : MonoBehaviour
    {
        public enum TriggerMode
        {
            [InspectorName("近づいたら")] Approach,
            [InspectorName("スクリプトから Play() を呼ぶ")] Manual,
        }

        [Header("出る条件")]
        [Tooltip("いつ出すか。")]
        public TriggerMode trigger = TriggerMode.Approach;
        [Tooltip("近づいたかを測る相手。未指定なら Main Camera。")]
        public Transform player;
        [Tooltip("この距離（m）より内側に入ったら出す。")]
        [Min(0.1f)] public float triggerRadius = 4;
        [Tooltip("入るたびに、この確率で出す。")]
        [Range(0, 1)] public float chance = 1;
        [Tooltip("ONなら1回出したら二度と出さない。")]
        public bool playOnce = true;
        [Tooltip("1回出したあと、次に出せるまでの秒数（1回だけのときは使わない）。")]
        [Min(0)] public float cooldown = 30;
        [Tooltip("条件を満たしてから出し始めるまでの秒数。")]
        [Min(0)] public float startDelay = 0.3f;
        [Tooltip("ONなら、演出の場所がカメラに映っているときだけ出す（見ていないところで終わらないように）。")]
        public bool requireInView = true;
        [Tooltip("画面の端からどれだけ内側に入っていれば『映っている』とみなすか（画面の割合）。")]
        [Range(0, 0.4f)] public float viewMargin = 0.1f;

        /// <summary>演出の最中なら true。</summary>
        public bool IsPlaying { get; private set; }

        bool wasInside;
        bool pending;
        bool hasPlayed;
        float nextAllowedTime;
        Coroutine running;

        /// <summary>演出の見どころの位置（画面に映っているかの判定に使う）。</summary>
        protected virtual Vector3 FocusPoint => transform.position;

        /// <summary>演出の中身。終わったら抜ける。</summary>
        protected abstract IEnumerator Sequence();

        /// <summary>出る前・止めたあとの見た目に戻す（お化けを隠すなど）。</summary>
        protected abstract void ResetVisual();

        protected virtual void OnEnable()
        {
            wasInside = false;
            pending = false;
            ResetVisual();
        }

        protected virtual void OnDisable()
        {
            IsDisabling = true;
            Stop();
            IsDisabling = false;
        }

        /// <summary>
        /// このコンポーネントが無効になる最中なら true。お化けがこのオブジェクトの子のとき、
        /// この間に SetActive を呼ぶと Unity のエラーになるので、ResetVisual で避ける。
        /// </summary>
        protected bool IsDisabling { get; private set; }

        /// <summary>お化けを隠す。無効になる最中に子を SetActive しない。</summary>
        protected void HideGhost(Transform ghost)
        {
            if (ghost && !(IsDisabling && ghost.IsChildOf(transform)))
            {
                ghost.gameObject.SetActive(false);
            }
        }

        /// <summary>条件を見ずに今すぐ出す。演出の最中なら何もしない。</summary>
        public void Play()
        {
            if (IsPlaying || !isActiveAndEnabled)
            {
                return;
            }
            running = StartCoroutine(Run());
        }

        /// <summary>演出を途中で止めて、出る前の見た目に戻す。</summary>
        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            IsPlaying = false;
            IsStopping = true;
            ResetVisual();
            IsStopping = false;
        }

        /// <summary>Stop()（途中で止めた）から ResetVisual が呼ばれているなら true。演出が最後まで終わったときは false。</summary>
        protected bool IsStopping { get; private set; }

        protected virtual void Update()
        {
            if (trigger != TriggerMode.Approach || !Application.isPlaying)
            {
                return;
            }
            Transform target = player;
            if (!target)
            {
                Camera main = Camera.main;
                target = main ? main.transform : null;
            }
            if (!target)
            {
                return;
            }
            bool inside = (target.position - transform.position).sqrMagnitude <= triggerRadius * triggerRadius;
            // 入った瞬間に「出す候補」にし、画面に映るなどの条件がそろうまで待つ。出たら候補から外す。
            if (inside && !wasInside)
            {
                pending = true;
            }
            if (!inside)
            {
                pending = false;
            }
            wasInside = inside;
            if (!pending || !CanStart())
            {
                return;
            }
            pending = false;
            if (Random.value > chance)
            {
                // 外れたら、出直すまで抽選しない。
                nextAllowedTime = Time.time + cooldown;
                return;
            }
            Play();
        }

        bool CanStart()
        {
            if (IsPlaying || (playOnce && hasPlayed) || Time.time < nextAllowedTime)
            {
                return false;
            }
            return !requireInView || IsInView(FocusPoint);
        }

        bool IsInView(Vector3 worldPoint)
        {
            Camera main = Camera.main;
            if (!main)
            {
                return true;
            }
            Vector3 viewport = main.WorldToViewportPoint(worldPoint);
            return viewport.z > 0
                && viewport.x > viewMargin && viewport.x < 1 - viewMargin
                && viewport.y > viewMargin && viewport.y < 1 - viewMargin;
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            hasPlayed = true;
            if (startDelay > 0)
            {
                yield return new WaitForSeconds(startDelay);
            }
            yield return Sequence();
            nextAllowedTime = Time.time + cooldown;
            running = null;
            IsPlaying = false;
            ResetVisual();
        }

        // ---- 派生クラス用の小道具 ----

        /// <summary>0→1 をなめらかに（始めと終わりがゆっくり）。</summary>
        protected static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        /// <summary>0→1 を、始めが速く終わりがゆっくりに。</summary>
        protected static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - (1 - t) * (1 - t);
        }

        /// <summary>0→1 を、始めがゆっくり終わりが速く。</summary>
        protected static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (trigger != TriggerMode.Approach)
            {
                return;
            }
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
