using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 흩뿌림 꾸밈 (#86): 이끼 땅의 풀·꽃, 강의 연잎, 유적 바닥의 돌 부스러기를 칸 위에 그린다 — 막지 않는 그림일 뿐.
    // 어느 칸에 무엇을 그릴지는 FieldDecor.ScatterAt (맵 글자 + 좌표 해시라 늘 같은 자리). 그림 = Tiles/{테마}_Scatter_{바닥}_{n}.png
    public class FieldScatter
    {
        private readonly Transform parent;
        private readonly List<GameObject> sprites = new List<GameObject>();

        public int Count => sprites.Count; // 테스트용

        public FieldScatter(Transform parent) => this.parent = parent;

        public void Build(FieldMap map, FieldTheme theme)
        {
            foreach (var go in sprites) Object.Destroy(go);
            sprites.Clear();
            var sets = new Dictionary<FieldTile, Sprite[]>
            {
                [FieldTile.Lawn] = FieldArt.Scatter(theme, FieldTile.Lawn),
                [FieldTile.Water] = FieldArt.Scatter(theme, FieldTile.Water),
                [FieldTile.RuinFloor] = FieldArt.Scatter(theme, FieldTile.RuinFloor),
            };
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!sets.TryGetValue(map.Get(cell), out var set) || set.Length == 0) continue;
                int index = FieldDecor.ScatterAt(map, cell, set.Length);
                if (index < 0) continue;
                var go = new GameObject("Scatter", typeof(SpriteRenderer));
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = set[index];
                renderer.sortingOrder = 1; // 타일 바로 위 — 주인공·물건·물결보다 아래
                sprites.Add(go);
            }
        }
    }
}
