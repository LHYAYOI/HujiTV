using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace FieldGhost
{
    /// <summary>
    /// カーテン越しの形。窓とカーテンの間を何かが横切り、途中で止まって顔と両手を部屋側へ押し付け、
    /// 離すとカーテンが揺れる。お化けの本体は描かず、布のふくらみ・影・ほのかな発光だけで見せる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Field Ghost/カーテン越しの形")]
    public sealed class FieldGhostCurtainPress : FieldGhostEvent
    {
        [Header("カーテン")]
        [Tooltip("押すカーテン。未指定なら同じオブジェクトから探す。")]
        public FieldGhostCurtain curtain;

        [Header("顔")]
        [Tooltip("顔の高さ（裾から m）。")] [Min(0)] public float headFromHem = 1.35f;
        [Tooltip("顔の半径（m）。")] [Min(0.01f)] public float headRadius = 0.17f;
        [Tooltip("顔の縦長さ（1 で丸）。")] [Range(0.5f, 2)] public float headAspect = 1.25f;
        [Tooltip("目と口のへこみ。")] [Range(0, 1)] public float faceDetail = 0.8f;

        [Header("横切る")]
        [Tooltip("出てくる位置（中央から右へ m。負なら左）。")] public float fromX = -0.55f;
        [Tooltip("止まる位置（中央から右へ m）。")] public float stopX = 0.05f;
        [Tooltip("ONなら止まらずに通り過ぎるだけ（toX まで）。")] public bool passByOnly = false;
        [Tooltip("通り過ぎるときの行き先（中央から右へ m）。")] public float toX = 0.6f;
        [Tooltip("横切る時間（秒）。")] [Min(0.05f)] public float moveDuration = 1.8f;
        [Tooltip("横切る間に布に触れる深さ（m）。")] [Min(0)] public float touchDepth = 0.06f;

        [Header("押し付ける")]
        [Tooltip("顔を押し付ける深さ（m）。")] [Min(0)] public float pressDepth = 0.2f;
        [Tooltip("押し付けるまでの時間（秒）。")] [Min(0.05f)] public float pressDuration = 0.6f;
        [Tooltip("押し付けたまま止まる時間（秒）。")] [Min(0)] public float holdDuration = 1.0f;
        [Tooltip("離す時間（秒）。短いほど『パッ』と消える。")] [Min(0.05f)] public float releaseDuration = 0.3f;

        [Header("手")]
        [Tooltip("ONなら両手も押し付ける。")] public bool useHands = true;
        [Tooltip("顔から見た手の位置（右手。左手は左右反転）（m）。")] public Vector2 handOffset = new Vector2(0.26f, -0.04f);
        [Tooltip("手の半径（m）。")] [Min(0.01f)] public float handRadius = 0.075f;
        [Tooltip("手の縦長さ。")] [Range(0.5f, 2)] public float handAspect = 1.3f;
        [Tooltip("手を押し付ける深さ（m）。")] [Min(0)] public float handDepth = 0.13f;
        [Tooltip("顔を押し付け始めてから手が出るまで（秒）。右手はさらに少し遅れる。")] [Min(0)] public float handDelay = 0.2f;
        [Tooltip("押し付けている間に手がずり下がる量（m）。")] [Min(0)] public float handSlide = 0.12f;

        [Header("風")]
        [Tooltip("押し付けている間の風の強さ（1 でそのまま）。弱めると顔と手の形が見やすい。")]
        [Range(0, 1)] public float windDuringPress = 0.3f;
        [Tooltip("離したときに起こす突風（0〜1）。")]
        [FormerlySerializedAs("releaseKick")] [Range(0, 1)] public float releaseGust = 0.7f;

        float HeadY => curtain ? -curtain.height + headFromHem : -1;

        protected override Vector3 FocusPoint
        {
            get
            {
                FindCurtain();
                return curtain ? curtain.LocalToWorld(new Vector2(passByOnly ? (fromX + toX) * 0.5f : stopX, HeadY)) : transform.position;
            }
        }

        void FindCurtain()
        {
            if (!curtain)
            {
                curtain = GetComponent<FieldGhostCurtain>();
            }
        }

        protected override void ResetVisual()
        {
            FindCurtain();
            if (curtain)
            {
                curtain.ClearBulges();
                curtain.WindScale = 1;
            }
        }

        protected override IEnumerator Sequence()
        {
            FindCurtain();
            if (!curtain)
            {
                Debug.LogWarning("FieldGhostCurtainPress: カーテン（FieldGhostCurtain）が見つかりません。", this);
                yield break;
            }

            // 1. 窓とカーテンの間を横切る（布に軽く触れながら）。
            float endX = passByOnly ? toX : stopX;
            float headX = fromX;
            float headY = HeadY;
            for (float time = 0; time < moveDuration; time += Time.deltaTime)
            {
                float t = time / moveDuration;
                headX = Mathf.Lerp(fromX, endX, Smooth(t));
                float bob = Mathf.Sin(t * Mathf.PI * 3) * 0.02f;
                // 出てくるときと（通り過ぎるなら）去るときは浅く。
                float fade = passByOnly ? Mathf.Min(EaseOut(t * 4), EaseOut((1 - t) * 4)) : EaseOut(t * 3);
                curtain.SetBulge(0, new Vector2(headX, headY + bob), headRadius, touchDepth * fade, headAspect);
                yield return null;
            }
            if (passByOnly)
            {
                curtain.ClearBulges();
                curtain.Kick(releaseGust * 0.5f);
                yield break;
            }

            // 2. 止まって顔を押し付け、少し遅れて両手。
            Vector2 head = new Vector2(headX, headY);
            Vector2 rightHand = head + handOffset;
            Vector2 leftHand = head + new Vector2(-handOffset.x, handOffset.y);
            float pressTotal = pressDuration + holdDuration;
            for (float time = 0; time < pressTotal; time += Time.deltaTime)
            {
                float press = EaseOut(time / pressDuration);
                float hold = holdDuration > 0 ? Mathf.Clamp01((time - pressDuration) / holdDuration) : 1;
                // 押し付けたまま、顔がわずかに傾くように左右へずれる。
                Vector2 headNow = head + new Vector2(Mathf.Sin(time * 2.1f) * 0.012f, 0);
                curtain.SetBulge(0, headNow, headRadius, Mathf.Lerp(touchDepth, pressDepth, press), headAspect);
                curtain.SetFaceDetail(faceDetail * press);
                curtain.WindScale = Mathf.Lerp(1, windDuringPress, press);
                if (useHands)
                {
                    float slide = handSlide * Smooth(hold);
                    float left = EaseOut((time - handDelay) / (pressDuration * 0.6f));
                    float right = EaseOut((time - handDelay * 1.6f) / (pressDuration * 0.6f));
                    curtain.SetBulge(1, leftHand - new Vector2(0, slide), handRadius, handDepth * left, handAspect);
                    curtain.SetBulge(2, rightHand - new Vector2(0, slide * 0.8f), handRadius, handDepth * right, handAspect);
                }
                yield return null;
            }

            // 3. ぱっと離す。突風が吹き、布があおられる。
            curtain.WindScale = 1;
            curtain.Kick(releaseGust);
            Vector2 leftEnd = leftHand - new Vector2(0, handSlide);
            Vector2 rightEnd = rightHand - new Vector2(0, handSlide * 0.8f);
            for (float time = 0; time < releaseDuration; time += Time.deltaTime)
            {
                float remain = 1 - EaseIn(time / releaseDuration);
                curtain.SetBulge(0, head, headRadius, pressDepth * remain, headAspect);
                curtain.SetFaceDetail(faceDetail * remain);
                if (useHands)
                {
                    curtain.SetBulge(1, leftEnd, handRadius, handDepth * remain, handAspect);
                    curtain.SetBulge(2, rightEnd, handRadius, handDepth * remain, handAspect);
                }
                yield return null;
            }
            curtain.ClearBulges();
        }
    }
}
