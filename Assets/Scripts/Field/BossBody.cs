using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Field
{
    // 크게 선 보스(#233 BossEncounter.fieldSize)의 몸이 차지하는 칸 (#267 사용자: 빈칸두꺼비 그림은 6칸인데 [확인]은 B 한 칸이라
    // 어떻게 도전하는지 묻게 됨). 몸 = 그림에서 보이는 부분(투명한 테두리 뺌) 안에 칸 가운데가 들어가는 '걸을 수 있는 칸' + B 칸.
    // 몸은 걸어 들어갈 수 없고, 어느 칸이든 옆에서 [확인] = 보스. 쓰러뜨리면 몸은 없어지고 B 자리 보석만 남는다.
    // 벽·출입구는 몸에 넣지 않는다 — 얇은 벽 너머(보스 방 문 밖)에서 [확인]으로 보스와 싸우지 않게
    public class BossBody
    {
        public static readonly BossBody None = new BossBody(null, new HashSet<Vector2Int>());

        private readonly HashSet<Vector2Int> cells;

        public Vector2Int? Boss { get; }
        public IReadOnlyCollection<Vector2Int> Cells => cells;

        private BossBody(Vector2Int? boss, HashSet<Vector2Int> cells)
        {
            Boss = boss;
            this.cells = cells;
        }

        // boss: B 칸. drawSize: 그림 크기(칸) — 가로는 B 칸 가운데에 맞추고, 세로는 B 칸 아래 변부터 위로 (FieldScreen.DrawBigBoss와 같은 자리).
        // visible: 그림 안에서 보이는 부분 (0~1, 왼쪽 아래가 0 — 비우면 그림 전체)
        public static BossBody Of(FieldMap map, Vector2Int boss, Vector2 drawSize, Rect? visible = null)
        {
            var v = visible ?? new Rect(0f, 0f, 1f, 1f);
            float left = boss.x + 0.5f - drawSize.x / 2f;
            float xMin = left + v.xMin * drawSize.x, xMax = left + v.xMax * drawSize.x;
            float yMin = boss.y + v.yMin * drawSize.y, yMax = boss.y + v.yMax * drawSize.y;
            var cells = new HashSet<Vector2Int> { boss };
            for (int x = Mathf.FloorToInt(xMin); x <= Mathf.CeilToInt(xMax); x++)
            for (int y = Mathf.FloorToInt(yMin); y <= Mathf.CeilToInt(yMax); y++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                if (cx <= xMin || cx >= xMax || cy <= yMin || cy >= yMax) continue;
                var cell = new Vector2Int(x, y);
                if (map != null && map.IsWalkable(cell) && map.Get(cell) != FieldTile.Door) cells.Add(cell);
            }
            return new BossBody(boss, cells);
        }

        // 그림에서 보이는 부분 (0~1, 왼쪽 아래가 0): 스프라이트 메시가 Tight(몬스터 그림의 가져오기 설정)면 꼭짓점이
        // 투명한 테두리를 뺀 몸을 감쌈. 네모 메시거나 그림이 없으면 전체
        public static Rect VisiblePart(Sprite sprite)
        {
            var whole = new Rect(0f, 0f, 1f, 1f);
            if (sprite == null) return whole;
            var bounds = sprite.bounds;
            var vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0 || bounds.size.x <= 0f || bounds.size.y <= 0f) return whole;
            Vector2 min = vertices[0], max = vertices[0];
            foreach (var v in vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return Rect.MinMaxRect((min.x - bounds.min.x) / bounds.size.x, (min.y - bounds.min.y) / bounds.size.y,
                (max.x - bounds.min.x) / bounds.size.x, (max.y - bounds.min.y) / bounds.size.y);
        }

        public bool Contains(Vector2Int cell) => cells.Contains(cell);

        // from → to 한 걸음이 몸에 막히는지. 몸 안에 서 있으면 막지 않는다 — 1.3까지는 그림 위를 걸을 수 있어서 거기서 저장된 세이브도 나올 수 있게
        public bool Blocks(Vector2Int from, Vector2Int to) => cells.Contains(to) && !cells.Contains(from);

        // [확인]으로 쓸 칸: 몸이면 B 칸으로 바꿈
        public Vector2Int Resolve(Vector2Int cell) => Boss.HasValue && cells.Contains(cell) ? Boss.Value : cell;
    }
}
