using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 아직 [확인]하지 않은 상자·열쇠·레버·석판에 은은한 반짝임 (#233 사용자 ⑯: 처음 하는 사람은 다가가서 [확인]해야 하는지 모름 → 시선이 가게).
    // 은은하게 (사용자: 효과는 주변 색 ± 한두 톤) — 작은 반짝이 하나가 물건마다 2~3초에 한 번, 화면 안 물건만. 끝난 것(연 상자 등)은 안 반짝임
    public partial class FieldScreen
    {
        private static readonly FieldTile[] ShinyTiles = { FieldTile.Chest, FieldTile.Key, FieldTile.Lever, FieldTile.Tablet, FieldTile.Boss };
        private const float SparkleTick = 0.3f;              // 이만큼에 하나씩만 새로 반짝 (한꺼번에 반짝이면 어지러움)
        private const float SparkleEvery = 2.4f;             // 한 물건이 다시 반짝이기까지
        private static readonly Color SparkleTint = new Color(1f, 0.95f, 0.78f, 0.8f);

        private readonly List<Vector2Int> shinyCells = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, float> nextSparkle = new Dictionary<Vector2Int, float>();
        private FieldArea shinyArea;
        private float sparkleClock;
        private readonly System.Random sparkleRng = new System.Random(233); // 게임 규칙의 rng와 따로 — 반짝임이 조우·몬스터 움직임을 바꾸지 않게

        public int SparklesPlayed { get; private set; } // 테스트용
        public Vector2Int? LastSparkleCell { get; private set; } // 테스트용

        private void UpdateSparkles(float dt)
        {
            if (area == null || inBattle || transitioning || IsPanelOpen) return;
            if (shinyArea != area)
            {
                shinyArea = area;
                shinyCells.Clear();
                nextSparkle.Clear();
                foreach (var kind in ShinyTiles) shinyCells.AddRange(area.Map.FindAll(kind));
            }
            sparkleClock += dt;
            if (sparkleClock < SparkleTick || shinyCells.Count == 0) return;
            sparkleClock = 0f;

            float now = Time.unscaledTime;
            float halfWidth = tilesAcross / 2f + 1f, halfHeight = tilesAcross / Mathf.Max(0.1f, cam != null ? cam.aspect : 0.5f) / 2f + 1f;
            var view = cam != null ? (Vector2)cam.transform.position : (Vector2)player.position;
            foreach (var cell in shinyCells)
            {
                var center = CellCenter(cell);
                if (Mathf.Abs(center.x - view.x) > halfWidth || Mathf.Abs(center.y - view.y) > halfHeight) continue;
                if (IsCellDone(cell) != (area.Map.Get(cell) == FieldTile.Boss)) continue; // 보스 칸만 반대로 — 이긴 뒤 남은 모래시계가 반짝임 (#251)
                if (nextSparkle.TryGetValue(cell, out var at) && now < at) continue;
                nextSparkle[cell] = now + SparkleEvery + (float)sparkleRng.NextDouble() * 0.8f;
                var offset = new Vector3((float)sparkleRng.NextDouble() * 0.5f - 0.25f, (float)sparkleRng.NextDouble() * 0.4f - 0.05f, 0f);
                StartCoroutine(FieldFx.Play(transform, center + offset, Fx.Sparkle, 0.7f, animationScale, 11, tint: SparkleTint));
                SparklesPlayed++;
                LastSparkleCell = cell;
                return; // 한 번에 하나
            }
        }
    }
}
