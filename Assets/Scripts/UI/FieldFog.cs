using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 바닥 안개 (#102, 사용자 참고 그림): 몬스터 칸(풀숲) 위에 넓고 납작한 안개 띠를 조금씩 어긋나게 겹쳐 놓고 천천히 좌우로 흔든다.
    // 고른 반투명 막은 '유리 같다'고 해서 이렇게 바꿈. 자리는 FieldDecor.FogPuffs(규칙), 그림은 Tiles/{테마}_Fog_Wide_n · _Fog_Small_n —
    // 그림이 없는 테마(초원·숲·정글)는 아무것도 그리지 않는다. 막지 않는 그림일 뿐 (조우는 그대로 풀숲 규칙)
    public class FieldFog
    {
        private const float SwayPixels = 3f;   // 좌우로 흔들리는 폭 (픽셀)
        private const float SwaySpeed = 0.35f; // 라디안/초 — 아주 느리게
        private readonly Transform parent;
        private readonly List<(Transform transform, Vector3 home, float phase)> puffs = new List<(Transform, Vector3, float)>();
        private float time;

        public int Count => puffs.Count; // 테스트용

        public FieldFog(Transform parent) => this.parent = parent;

        public void Build(FieldMap map, FieldTheme theme)
        {
            foreach (var puff in puffs) Object.Destroy(puff.transform.gameObject);
            puffs.Clear();
            var wide = FieldArt.Fog(theme, true);
            var small = FieldArt.Fog(theme, false);
            if (wide.Length == 0 || small.Length == 0) return;
            foreach (var puff in FieldDecor.FogPuffs(map))
            {
                var set = puff.Wide ? wide : small;
                var go = new GameObject("Fog", typeof(SpriteRenderer));
                go.transform.SetParent(parent, false);
                var home = new Vector3(puff.Cell.x + 0.5f + puff.Offset.x / 16f, puff.Cell.y + 0.5f + puff.Offset.y / 16f, 0f);
                go.transform.position = home;
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = set[puff.Variant % set.Length];
                renderer.flipX = puff.Flip;
                renderer.sortingOrder = 3; // 타일·흩뿌림(1) 위, 큰 그림(5)·주인공(10) 아래
                puffs.Add((go.transform, home, puff.Variant % 628 / 100f));
            }
        }

        // 천천히 좌우로 — 한 픽셀 단위로 움직여 도트가 번지지 않게
        public void Tick(float dt)
        {
            if (puffs.Count == 0) return;
            time += dt;
            foreach (var (transform, home, phase) in puffs)
            {
                float px = Mathf.Round(Mathf.Sin(time * SwaySpeed + phase) * SwayPixels);
                transform.position = home + new Vector3(px / 16f, 0f, 0f);
            }
        }
    }
}
