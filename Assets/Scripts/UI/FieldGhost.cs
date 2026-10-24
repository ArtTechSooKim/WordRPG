using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 처음 하는 사람에게 풀숲 가는 길을 보여 주는 주인공의 반투명 분신 (#133 사용자: 처음 온 사람은 풀숲이 뭔지 모름 →
    // 카메라가 풀숲으로 가며 분신이 잔상을 남기고 걸어 들어감 → 다시 주인공으로). 보이기만 하는 그림 — 칸 영향 없음.
    // 그림은 고른 모습의 달리기(PlayerArt.GetRun)를 푸르스름하게, 지나간 자리에 옅어지는 잔상을 남긴다
    public class FieldGhost
    {
        private static readonly Color Tint = new Color(0.75f, 0.9f, 1f, 0.6f);
        private const float TrailAlpha = 0.35f;
        private const float TrailEvery = 0.06f; // 잔상을 남기는 간격(초)
        private const float TrailLife = 0.35f;  // 잔상이 사라지는 시간(초)

        private readonly Transform parent;
        private readonly Transform root;
        private readonly SpriteRenderer body;
        private readonly List<(SpriteRenderer image, float bornAt)> trail = new List<(SpriteRenderer, float)>();
        private float lastTrail;

        public Vector3 Position => root.position;
        public int TrailsLeft { get; private set; } // 지금까지 남긴 잔상 수

        public FieldGhost(Transform parent, Vector3 start, int sortingOrder)
        {
            this.parent = parent;
            var go = new GameObject("WayGhost", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            root = go.transform;
            root.position = start;
            body = go.GetComponent<SpriteRenderer>();
            body.sortingOrder = sortingOrder;
            body.color = Tint;
            body.sprite = PlayerArt.Get(Direction.Down, 0);
        }

        // points를 차례로 지나간다. 한 칸에 secondsPerTile, 움직일 때마다 onMove(지금 위치) — 카메라가 따라가게
        public IEnumerator Walk(IReadOnlyList<Vector3> points, float secondsPerTile, Action<Vector3> onMove)
        {
            var facing = Direction.Down;
            float walked = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                var from = points[i - 1];
                var to = points[i];
                var delta = to - from;
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0 ? Direction.Right : Direction.Left)
                    : (delta.y > 0 ? Direction.Up : Direction.Down);
                float duration = Mathf.Max(0.0001f, secondsPerTile * delta.magnitude);
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    walked += Time.unscaledDeltaTime;
                    Move(Vector3.Lerp(from, to, t / duration), facing, walked, onMove);
                    yield return null;
                }
            }
            if (points.Count > 0) Move(points[points.Count - 1], facing, walked, onMove);
            body.sprite = PlayerArt.Get(facing, 0);
        }

        // 남은 잔상이 사라질 때까지 기다리며 분신도 옅어진다
        public IEnumerator FadeOut(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                var c = Tint;
                c.a = Tint.a * (1f - t / seconds);
                body.color = c;
                FadeTrail();
                yield return null;
            }
            body.color = Color.clear;
            ClearTrail();
        }

        public void Destroy()
        {
            ClearTrail();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        private void Move(Vector3 position, Direction facing, float walked, Action<Vector3> onMove)
        {
            root.position = position;
            body.sprite = PlayerArt.GetRun(facing, Mathf.FloorToInt(walked * PlayerArt.FramesPerSecond * 1.8f));
            if (Time.unscaledTime - lastTrail >= TrailEvery) LeaveTrail();
            FadeTrail();
            onMove?.Invoke(position);
        }

        private void LeaveTrail()
        {
            lastTrail = Time.unscaledTime;
            var go = new GameObject("WayGhostTrail", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = root.position;
            var image = go.GetComponent<SpriteRenderer>();
            image.sprite = body.sprite;
            image.sortingOrder = body.sortingOrder - 1;
            image.color = new Color(Tint.r, Tint.g, Tint.b, TrailAlpha);
            trail.Add((image, Time.unscaledTime));
            TrailsLeft++;
        }

        private void FadeTrail()
        {
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var (image, bornAt) = trail[i];
                float k = (Time.unscaledTime - bornAt) / TrailLife;
                if (image == null || k >= 1f)
                {
                    if (image != null) UnityEngine.Object.Destroy(image.gameObject);
                    trail.RemoveAt(i);
                    continue;
                }
                image.color = new Color(Tint.r, Tint.g, Tint.b, TrailAlpha * (1f - k));
            }
        }

        private void ClearTrail()
        {
            foreach (var (image, _) in trail)
                if (image != null) UnityEngine.Object.Destroy(image.gameObject);
            trail.Clear();
        }
    }
}
