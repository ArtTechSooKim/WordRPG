using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 필드에 보이는 몬스터 그림 (#73, GDD 9-5): 한 무리 = 맨 앞 몬스터 그림 한 장. 칸 사이를 부드럽게 움직이고,
    // 발견하면 머리 위에 "!" 풍선(팩 Emote — 사전 말풍선과 같은 그림). 규칙은 RoamingMonsters, 여기서는 그리기만
    public class FieldRoamerViews
    {
        private const float MarkSeconds = 1.1f;
        private const float WanderMoveSeconds = 0.4f;

        private class View
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Mark;
            public SpriteRenderer Eyes;     // 어둠 속 빨간 눈 (#112)
            public float Blink;
            public SpriteRenderer[] Bubbles; // 웅덩이에서 오르는 먹물 거품 (#113, 먹물도롱뇽)
            public float BubbleTime;
            public Vector3 From, To;
            public float T = 1f, Duration = 0.1f;
            public float MarkLeft;
            public float Phase;
        }

        private readonly Transform parent;
        private readonly Func<Vector2Int, Vector3> cellCenter;
        private readonly Dictionary<Roamer, View> views = new Dictionary<Roamer, View>();
        private Sprite exclaim;
        private Sprite eyes, bubble;
        private const float BlinkEvery = 3.6f, BlinkSeconds = 0.13f;

        public int Count => views.Count;
        public int EyesShown { get; private set; } // 지금 빨간 눈만 보이는 몬스터 수 (테스트용)
        public int BubblesShown { get; private set; } // 지금 거품이 오르는 웅덩이 수 (테스트용)
        public bool IsBodyVisible(Roamer roamer) => views.TryGetValue(roamer, out var view) && view.Body.enabled; // 테스트용

        public FieldRoamerViews(Transform parent, Func<Vector2Int, Vector3> cellCenter)
        {
            this.parent = parent;
            this.cellCenter = cellCenter;
        }

        public bool IsMarked(Roamer roamer) => views.TryGetValue(roamer, out var view) && view.Mark.gameObject.activeSelf;

        public Vector3? WorldPosition(Roamer roamer) => views.TryGetValue(roamer, out var view) ? view.Root.position : (Vector3?)null;

        public void Clear()
        {
            foreach (var view in views.Values)
                if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
            views.Clear();
        }

        // 지역에 들어올 때: 지금 있는 몬스터를 모두 그림
        public void Sync(RoamingMonsters roaming)
        {
            Clear();
            if (roaming == null) return;
            foreach (var roamer in roaming.Roamers) Create(roamer);
        }

        public void Remove(Roamer roamer)
        {
            if (!views.TryGetValue(roamer, out var view)) return;
            if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
            views.Remove(roamer);
        }

        public void Apply(IEnumerable<RoamerEvent> events, RoamingRules rules, float slow)
        {
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case RoamerEventKind.Spawned:
                        Create(e.Roamer);
                        break;
                    case RoamerEventKind.Moved:
                        if (!views.TryGetValue(e.Roamer, out var view)) view = Create(e.Roamer);
                        view.From = view.Root.position;
                        view.To = cellCenter(e.Roamer.Position);
                        view.T = 0f;
                        float seconds = e.Roamer.State == RoamerState.Chasing ? rules.ChaseStepSeconds : WanderMoveSeconds;
                        view.Duration = seconds / Mathf.Max(0.05f, slow);
                        Face(view, e.Roamer.Facing);
                        break;
                    case RoamerEventKind.Noticed:
                        if (views.TryGetValue(e.Roamer, out var noticed))
                        {
                            noticed.MarkLeft = MarkSeconds;
                            noticed.Mark.gameObject.SetActive(true);
                            Face(noticed, e.Roamer.Facing);
                        }
                        break;
                }
            }
        }

        // 실제 시간으로: 이동 보간 · 살짝 통통 · "!" 사라지기 · 웅덩이에 숨은 몬스터는 몸을 숨기고 거품
        public void Tick(float dt)
        {
            int bubbling = 0;
            foreach (var pair in views)
            {
                var view = pair.Value;
                bool hidden = pair.Key.Hidden;
                if (view.Body.enabled == hidden) view.Body.enabled = !hidden;
                bool surfacing = pair.Key.State == RoamerState.Surfacing;
                if (surfacing) bubbling++;
                AnimateBubbles(view, surfacing, dt);
                if (view.T < 1f)
                {
                    view.T = Mathf.Min(1f, view.T + dt / Mathf.Max(0.01f, view.Duration));
                    view.Root.position = Vector3.Lerp(view.From, view.To, view.T);
                }
                view.Phase += dt * (pair.Key.State == RoamerState.Chasing ? 14f : 5f);
                view.Body.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(view.Phase)) * 0.06f, 0f);
                if (view.MarkLeft > 0f)
                {
                    view.MarkLeft -= dt;
                    if (view.MarkLeft <= 0f) view.Mark.gameObject.SetActive(false);
                }
            }
            BubblesShown = bubbling;
        }

        private View Create(Roamer roamer)
        {
            var root = new GameObject($"Roamer_{roamer.Id}").transform;
            root.SetParent(parent, false);
            root.position = cellCenter(roamer.Position);

            var body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            body.transform.SetParent(root, false);
            var species = roamer.Leader.Species;
            body.sprite = species.Sprite != null ? species.Sprite : UiKit.GlowSprite();
            if (species.Sprite == null)
            {
                body.color = species.PlaceholderColor;
                body.transform.localScale = Vector3.one * 0.8f;
            }
            body.sortingOrder = 9; // 주인공(10) 바로 아래

            var mark = new GameObject("Exclaim", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            mark.transform.SetParent(root, false);
            mark.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            mark.sprite = ExclaimSprite();
            mark.sortingOrder = 16; // 동굴 어둠(14) 위 — 어둠 속에서도 '!'는 보이게 (#112)
            mark.gameObject.SetActive(false);

            var eyesRenderer = new GameObject("Eyes", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            eyesRenderer.transform.SetParent(root, false);
            eyesRenderer.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            eyesRenderer.sprite = EyesSprite();
            eyesRenderer.sortingOrder = 15; // 어둠(14) 바로 위
            eyesRenderer.gameObject.SetActive(false);

            var bubbles = new SpriteRenderer[3];
            for (int i = 0; i < bubbles.Length; i++)
            {
                bubbles[i] = new GameObject($"Bubble_{i}", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                bubbles[i].transform.SetParent(root, false);
                bubbles[i].sprite = BubbleSprite();
                bubbles[i].sortingOrder = 8; // 물 위, 주인공 아래
                bubbles[i].gameObject.SetActive(false);
            }

            var view = new View { Root = root, Body = body, Mark = mark, Eyes = eyesRenderer, Bubbles = bubbles, From = root.position, To = root.position,
                                  Phase = roamer.Id, Blink = roamer.Id * 1.37f % BlinkEvery };
            body.enabled = !roamer.Hidden;
            Face(view, roamer.Facing);
            views[roamer] = view;
            return view;
        }

        private static void Face(View view, Direction facing)
        {
            if (facing == Direction.Left) view.Body.flipX = true;
            else if (facing == Direction.Right) view.Body.flipX = false;
        }

        // 거품 고리 3개가 차례로 오르며 커졌다 사라짐 (웅덩이 색과 어울리게 옅은 남보라 — #95 사용자: 효과는 은은하게)
        private static void AnimateBubbles(View view, bool on, float dt)
        {
            if (!on)
            {
                if (view.Bubbles[0].gameObject.activeSelf)
                    foreach (var b in view.Bubbles) b.gameObject.SetActive(false);
                view.BubbleTime = 0f;
                return;
            }
            view.BubbleTime += dt;
            for (int i = 0; i < view.Bubbles.Length; i++)
            {
                var b = view.Bubbles[i];
                if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
                float t = (view.BubbleTime * 1.6f + i / 3f) % 1f;
                b.transform.localPosition = new Vector3((i - 1) * 0.22f + Mathf.Sin(t * 6f + i) * 0.03f, -0.2f + t * 0.45f, 0f);
                b.transform.localScale = Vector3.one * (0.5f + t * 0.6f);
                b.color = new Color(0.78f, 0.7f, 0.95f, (1f - t) * 0.9f);
            }
        }

        // 동굴 어둠 (#112): inDark(몬스터 자리)가 참이면 몸은 어둠에 가려지고 빨간 눈 두 개만 (가끔 깜빡). null이면 눈을 모두 끔.
        // 웅덩이에 숨은 몬스터는 눈도 안 보임
        public void UpdateEyes(Func<Vector3, bool> inDark, float dt)
        {
            if (inDark == null && EyesShown == 0) return;
            int shown = 0;
            foreach (var pair in views)
            {
                var view = pair.Value;
                bool show = inDark != null && !pair.Key.Hidden && inDark(view.Root.position);
                if (show)
                {
                    view.Blink = (view.Blink + dt) % BlinkEvery;
                    show = view.Blink > BlinkSeconds;
                    shown++;
                }
                if (view.Eyes.gameObject.activeSelf != show) view.Eyes.gameObject.SetActive(show);
            }
            EyesShown = shown;
        }

        // 거품 고리 하나 (6×6픽셀 동그라미 테두리 + 위쪽 반짝 한 점)
        private Sprite BubbleSprite()
        {
            if (bubble != null) return bubble;
            var texture = new Texture2D(6, 6, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "ink_bubble" };
            var pixels = new Color32[36];
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                float d = Mathf.Sqrt((x - 2.5f) * (x - 2.5f) + (y - 2.5f) * (y - 2.5f));
                pixels[y * 6 + x] = d > 1.6f && d < 2.9f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            pixels[4 * 6 + 2] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            bubble = Sprite.Create(texture, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            return bubble;
        }

        // 빨간 눈 두 개 (6×2픽셀 + 옅은 번짐, 16px = 1칸)
        private Sprite EyesSprite()
        {
            if (eyes != null) return eyes;
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "roamer_eyes" };
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[8 * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            foreach (int x0 in new[] { 1, 5 })
            {
                for (int y = 1; y <= 2; y++)
                for (int x = x0; x <= x0 + 1; x++)
                    pixels[y * 8 + x] = new Color32(255, 64, 52, 255);
                pixels[3 * 8 + x0] = new Color32(255, 64, 52, 70);     // 위로 옅은 번짐
                pixels[0 * 8 + x0 + 1] = new Color32(255, 64, 52, 70);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            eyes = Sprite.Create(texture, new Rect(0, 0, 8, 4), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            return eyes;
        }

        // 사전 말풍선과 같은 팩 그림 (Ui/bubble_exclaim, 아래 가운데 기준)
        private Sprite ExclaimSprite()
        {
            if (exclaim != null) return exclaim;
            var texture = Resources.Load<Texture2D>("Art/NinjaAdventure/Ui/bubble_exclaim");
            if (texture == null) return null;
            exclaim = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect);
            return exclaim;
        }
    }
}
