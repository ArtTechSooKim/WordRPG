using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 얕은 강의 물살 (#81): 화면 안의 얕은 강 칸에 물결 그림이 생겨 물살 방향으로 흘러가다 사라진다 — 어느 쪽으로 걸으면 빨라지는지 보이게.
    // 그림 = 흰 물결선 4프레임 (Art/NinjaAdventure/Field/ripple.png — import_ninja_art.py flow_streaks). 보이기만 하는 그림
    public class FieldStreams
    {
        private const int PoolSize = 28;
        private const float Life = 1.3f;          // 물결 하나가 사는 시간
        private const float Speed = 2.4f;         // 칸/초 (걷기 한 칸 0.16초 = 6칸/초보다 느리게, 흘러가는 느낌)
        private const int Range = 11;             // 주인공에서 이 칸 안의 얕은 강에만
        private const float SpawnEvery = 0.07f;

        private readonly Transform parent;
        private readonly List<(SpriteRenderer renderer, Vector3 velocity, float age)> ripples = new List<(SpriteRenderer, Vector3, float)>();
        private readonly Sprite[] frames;
        private readonly System.Random rng = new System.Random(81);
        private List<Vector2Int> streamCells = new List<Vector2Int>();
        private FieldMap map;
        private float spawnTimer;

        public int ActiveCount { get; private set; } // 테스트용

        public FieldStreams(Transform parent)
        {
            this.parent = parent;
            var texture = Resources.Load<Texture2D>("Art/NinjaAdventure/Field/ripple");
            if (texture != null)
            {
                int count = Mathf.Max(1, texture.width / Mathf.Max(1, texture.height));
                frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                    frames[i] = Sprite.Create(texture, new Rect(i * texture.height, 0, texture.height, texture.height), new Vector2(0.5f, 0.5f), 16);
            }
        }

        public void SetMap(FieldMap fieldMap)
        {
            map = fieldMap;
            streamCells = fieldMap != null ? fieldMap.FindAll(FieldTile.Stream) : new List<Vector2Int>();
            foreach (var ripple in ripples) ripple.renderer.gameObject.SetActive(false);
            ActiveCount = 0;
        }

        public void Tick(float dt, Vector2Int player)
        {
            if (map == null || streamCells.Count == 0 || frames == null) return;
            ActiveCount = 0;
            for (int i = 0; i < ripples.Count; i++)
            {
                var (renderer, velocity, age) = ripples[i];
                if (!renderer.gameObject.activeSelf) continue;
                age += dt;
                if (age >= Life)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }
                renderer.transform.position += velocity * dt;
                float t = age / Life;
                renderer.color = new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.PI) * 0.75f);
                renderer.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * frames.Length))];
                ripples[i] = (renderer, velocity, age);
                ActiveCount++;
            }
            spawnTimer -= dt;
            while (spawnTimer <= 0f)
            {
                spawnTimer += SpawnEvery;
                Spawn(player);
            }
        }

        private void Spawn(Vector2Int player)
        {
            // 주인공 가까이의 얕은 강 칸 하나 (여러 번 골라 보고 없으면 쉼)
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var cell = streamCells[rng.Next(streamCells.Count)];
                if (Mathf.Abs(cell.x - player.x) > Range || Mathf.Abs(cell.y - player.y) > Range) continue;
                var direction = map.StreamDirection(cell);
                if (!direction.HasValue) continue;
                var slot = FreeSlot();
                if (slot < 0) return;
                var offset = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, 0f) * 0.8f;
                var renderer = ripples[slot].renderer;
                renderer.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f) + offset;
                bool vertical = direction.Value == Direction.Up || direction.Value == Direction.Down;
                renderer.transform.localEulerAngles = new Vector3(0f, 0f, vertical ? 90f : 0f); // 물결선이 물살 방향으로 눕게
                renderer.gameObject.SetActive(true);
                renderer.color = new Color(1f, 1f, 1f, 0f);
                ripples[slot] = (renderer, (Vector3)(Vector2)direction.Value.ToOffset() * Speed, 0f);
                return;
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < ripples.Count; i++)
                if (!ripples[i].renderer.gameObject.activeSelf) return i;
            if (ripples.Count >= PoolSize) return -1;
            var go = new GameObject("Ripple", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 1; // 타일 바로 위, 주인공·물건보다 아래
            go.SetActive(false);
            ripples.Add((renderer, Vector3.zero, 0f));
            return ripples.Count - 1;
        }
    }
}
