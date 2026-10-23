using System;
using System.Collections;
using UnityEngine;

namespace WordRPG.UI
{
    // 숫자가 굴러 올라가는 글자 · 톡 커졌다 돌아오는 연출 (#127 사용자: 능력치·골드가 오를 때 반짝이며 숫자가 오르는 게 보이게).
    // 화면(MonoBehaviour)이 StartCoroutine으로 돌린다. 대상이 사라지면 그냥 끝
    public static class UiMotion
    {
        // show(값): from → to로 seconds 동안 (처음엔 빠르게, 끝에서 천천히)
        public static IEnumerator Count(Action<int> show, int from, int to, float seconds)
        {
            show(from);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / seconds) * (1f - t / seconds);
                show(Mathf.RoundToInt(Mathf.Lerp(from, to, k)));
                yield return null;
            }
            show(to);
        }

        public static IEnumerator Pop(Transform target, float seconds, float peak = 1.15f)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t / seconds);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        // 위로 떠오르며 사라지는 글자 (필드 HUD '+18G')
        public static IEnumerator FloatUp(RectTransform target, float distance, float seconds)
        {
            if (target == null) yield break;
            var start = target.anchoredPosition;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                float k = t / seconds;
                target.anchoredPosition = start + new Vector2(0f, distance * k);
                group.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                yield return null;
            }
            if (target != null) UnityEngine.Object.Destroy(target.gameObject);
        }
    }
}
