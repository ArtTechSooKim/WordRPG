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
            public Vector3 From, To;
            public float T = 1f, Duration = 0.1f;
            public float MarkLeft;
            public float Phase;
        }

        private readonly Transform parent;
        private readonly Func<Vector2Int, Vector3> cellCenter;
        private readonly Dictionary<Roamer, View> views = new Dictionary<Roamer, View>();
        private Sprite exclaim;

        public int Count => views.Count;

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

        // 실제 시간으로: 이동 보간 · 살짝 통통 · "!" 사라지기
        public void Tick(float dt)
        {
            foreach (var pair in views)
            {
                var view = pair.Value;
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
            mark.sortingOrder = 12;
            mark.gameObject.SetActive(false);

            var view = new View { Root = root, Body = body, Mark = mark, From = root.position, To = root.position, Phase = roamer.Id };
            Face(view, roamer.Facing);
            views[roamer] = view;
            return view;
        }

        private static void Face(View view, Direction facing)
        {
            if (facing == Direction.Left) view.Body.flipX = true;
            else if (facing == Direction.Right) view.Body.flipX = false;
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
