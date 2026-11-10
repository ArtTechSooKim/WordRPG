using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 인물 컷씬 (#234 사용자 ⑲ — 숲 나무꾼). 대사는 NpcData, 한 번만 = WorldState 이벤트 (세이브 칸 추가 없음)
    //  ① 그 지역에 처음 들어오면 (보스를 물리치기 전): 입력을 막고 주인공이 다섯 칸 걸어 들어감 → 인물이 달려와 상황을 알려 줌 → 제자리로 걸어감
    //  ② 그 지역 보스를 물리친 뒤 (기술 배우기·돌아가기 묻기 전): 인물이 화면 밖에서 달려와 고맙다 → 보스가 연 길 옆으로 → 주인공도 옆으로 →
    //     이야기 → 화면 밖으로 걸어가 제자리로
    public partial class FieldScreen
    {
        private const int EntryWalk = 5;           // 처음 들어와 걸어 들어가는 칸 수 (사용자)
        private const float HeroStep = 0.24f;      // 컷씬에서 주인공이 한 칸 걷는 시간
        private const float NpcWalkStep = 0.26f;   // 인물이 걸을 때
        private const float NpcRunStep = 0.13f;    // 인물이 달려올 때
        private const int RunInFrom = 9;           // 보스 뒤: 주인공에게서 걸어서 이만큼 떨어진 곳(화면 밖)에서 달려옴
        private const float MinLineSeconds = 0.25f; // 막 뜬 말은 이만큼 지나야 [확인]으로 넘어감 (연타에 못 읽고 지나가지 않게)
        private const float AfterLineCooldown = 0.4f; // 말이 끝나고 조작이 돌아온 뒤 이만큼은 [확인]이 옆 물건을 누르지 않음 (#251 사용자: 막 누르면 이상하게 넘어감)
        private bool cutsceneRunning;
        private bool lineSkip, lineSkipped;        // [확인]을 눌렀다 · 방금 말을 [확인]으로 넘겼다 (#245)

        public int CutscenesPlayed { get; private set; } // 테스트용
        public bool IsCutsceneRunning => cutsceneRunning;

        public static string EntryEventId(NpcData npc) => "npc_entry:" + npc.NpcId;
        public static string GiftEventId(NpcData npc) => "npc_gift:" + npc.NpcId;
        private const int GiftNoticeRange = 2; // 이만큼 가까이 오면 말을 걺

        // 선물 (#236 사서): 가까이 오면 한 번 — 말하고 아이템을 줌 (그 지역 보스를 물리치기 전에만)
        private void MaybeStartGiftScene()
        {
            if (cutsceneRunning || springJumping || fieldTutorialRunning || NeedsPrologue || heroSetupRunning || moving) return;
            foreach (var view in npcs.Views)
            {
                var data = view.Data;
                if (data.GiftItem == null || session.World.HasEvent(GiftEventId(data))) continue;
                if (data.GiftBeforeBossOf != null && data.GiftBeforeBossOf.Boss != null && session.World.IsBossDefeated(data.GiftBeforeBossOf.BossId)) continue;
                var offset = view.Motion.Cell - walker.Position;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > GiftNoticeRange) continue;
                StartCoroutine(GiftScene(view));
                return;
            }
        }

        private IEnumerator GiftScene(NpcView npc)
        {
            BeginCutscene();
            session.World.MarkEvent(GiftEventId(npc.Data));
            FaceEachOther(npc);
            yield return SayLines(npc, npc.Data.GiftLines);
            session.Inventory.Add(npc.Data.GiftItem, npc.Data.GiftCount);
            Sound.Play(Sfx.Coin);
            string gift = npc.Data.GiftCount > 1 ? $"{npc.Data.GiftItem.DisplayName} {npc.Data.GiftCount}개를" : UiKit.WithJosa(npc.Data.GiftItem.DisplayName, "을", "를");
            ShowToast($"{gift} 받았다!", 2.5f);
            RefreshHud();
            EndCutscene();
        }
        public static string BossEventId(NpcData npc) => "npc_boss:" + npc.NpcId;

        // UpdateNpcs에서 (서 있을 때만): 처음 들어온 지역에 알려 줄 인물이 있으면
        private void MaybeStartEntryCutscene()
        {
            if (cutsceneRunning || springJumping || fieldTutorialRunning || NeedsPrologue || heroSetupRunning) return;
            if (area.Boss != null && session.World.IsBossDefeated(area.BossId)) return;
            foreach (var view in npcs.Views)
            {
                if (!view.Data.BriefsOnEntry || session.World.HasEvent(EntryEventId(view.Data))) continue;
                StartCoroutine(EntryCutscene(view));
                return;
            }
        }

        private IEnumerator EntryCutscene(NpcView npc)
        {
            BeginCutscene();
            session.World.MarkEvent(EntryEventId(npc.Data));
            SaveNow();
            yield return WalkHero(StraightPath(walker.Position, Direction.Right, EntryWalk));
            var beside = BesideHero(npc.Motion.Cell);
            yield return WalkNpc(npc, FieldReach.PathTo(walker.Map, npc.Motion.Cell, beside, CutsceneCanStand), NpcRunStep);
            FaceEachOther(npc);
            yield return SayLines(npc, npc.Data.EntryLines);
            yield return WalkNpc(npc, FieldReach.PathTo(walker.Map, npc.Motion.Cell, npc.Motion.Home, CutsceneCanStand), NpcWalkStep);
            EndCutscene();
        }

        // 보스를 이긴 뒤 배웅할 인물 (없으면 null — 부르는 쪽이 한 프레임도 기다리지 않게 먼저 확인)
        private NpcView BossSeer()
        {
            foreach (var view in npcs.Views)
                if (view.Data.SeesOffAfterBoss && !session.World.HasEvent(BossEventId(view.Data))) return view;
            return null;
        }

        // 보스를 이긴 뒤 (AfterBossVictory에서): 배웅 컷씬을 끝까지
        private IEnumerator BossCutscene(NpcView npc, List<AreaGate> gates)
        {
            BeginCutscene();
            session.World.MarkEvent(BossEventId(npc.Data));
            SaveNow();

            // 화면 밖에서 달려옴: 제자리 → 주인공 길에서 주인공 앞 RunInFrom칸 자리로 옮겨 놓고 그다음부터 달림
            var beside = BesideHero(npc.Motion.Cell);
            var road = FieldReach.PathTo(walker.Map, npc.Motion.Cell, beside, CutsceneCanStand);
            if (road.Count > RunInFrom)
            {
                var from = road[road.Count - RunInFrom - 1];
                npc.Motion.Teleport(from, Direction.Down);
                npcs.Redraw(npc);
                road = road.GetRange(road.Count - RunInFrom, RunInFrom);
            }
            yield return WalkNpc(npc, road, NpcRunStep);
            FaceEachOther(npc);
            yield return SayLines(npc, npc.Data.BossThanksLines);

            // 보스가 연 길 옆으로 — 부르는 말은 옆에서 하고, 주인공이 바로 뒤따라 같이 감 (#245 사용자: 인물 혼자 멀리 가면 말하다가 화면 밖으로 사라짐)
            var gate = gates.Find(g => g.Area == area);
            if (gate.Area != null && TrySpotsBeside(gate.Cell, out var npcSpot, out var heroSpot))
            {
                yield return SayLines(npc, npc.Data.ExitCallLines);
                yield return WalkTogether(npc, npcSpot, heroSpot);
                FaceEachOther(npc);
            }
            yield return SayLines(npc, npc.Data.ExitStoryLines);

            // 화면 밖으로 걸어가 제자리로
            var away = FieldReach.PathTo(walker.Map, npc.Motion.Cell, npc.Motion.Home, CutsceneCanStand);
            if (away.Count > RunInFrom) away = away.GetRange(0, RunInFrom);
            yield return WalkNpc(npc, away, NpcWalkStep);
            npc.Motion.Teleport(npc.Motion.Home, Direction.Down);
            npc.Motion.Release();
            npcs.Redraw(npc);
            EndCutscene();
        }

        // 인물이 앞서고 주인공이 한 칸 뒤를 따라 같이 걷는다 (카메라는 주인공을 따라가므로 인물이 화면 안에 남음).
        // 인물이 먼 쪽 자리로 가고 주인공은 가는 길에 있는 가까운 쪽에 서며, 주인공이 서 있는 칸으로는 인물이 오지 않음 (#245 사용자: 겹침)
        private IEnumerator WalkTogether(NpcView npc, Vector2Int npcSpot, Vector2Int heroSpot)
        {
            var start = npc.Motion.Cell;
            if ((npcSpot - start).sqrMagnitude < (heroSpot - start).sqrMagnitude) (npcSpot, heroSpot) = (heroSpot, npcSpot);
            if (npcSpot == walker.Position) (npcSpot, heroSpot) = (heroSpot, npcSpot);

            var npcRoad = FieldReach.PathTo(walker.Map, start, npcSpot, CutsceneCanStand);
            var heroRoad = new List<Vector2Int>();
            if (walker.Position != heroSpot)
            {
                if ((start - walker.Position).sqrMagnitude == 1) // 인물이 지나간 칸을 그대로 (도착 칸 앞까지, 내 자리를 지나면 거기서 멈춤)
                {
                    heroRoad.Add(start);
                    for (int i = 0; i < npcRoad.Count - 1 && heroRoad[heroRoad.Count - 1] != heroSpot; i++) heroRoad.Add(npcRoad[i]);
                }
                var last = heroRoad.Count > 0 ? heroRoad[heroRoad.Count - 1] : walker.Position;
                heroRoad.AddRange(FieldReach.PathTo(walker.Map, last, heroSpot, cell => walker.CanStand(cell) && cell != npcSpot));
            }

            bool heroArrived = false;
            IEnumerator HeroWalk()
            {
                yield return WalkHero(heroRoad, NpcWalkStep); // 같은 빠르기 — 앞사람을 따라잡아 겹치지 않게
                heroArrived = true;
            }
            StartCoroutine(HeroWalk());
            yield return WalkNpc(npc, npcRoad, NpcWalkStep);
            while (!heroArrived) yield return null;
        }

        private void BeginCutscene()
        {
            cutsceneRunning = true;
            transitioning = true;
            CutscenesPlayed++;
            if (moving) SnapPlayer();
            guide.Hide();
            bubble.Hide();
        }

        private void EndCutscene()
        {
            bubble.Hide();
            session.World.SetPosition(area.AreaId, walker.Position);
            SaveNow();
            RefreshNameTags();
            transitioning = false;
            cutsceneRunning = false;
        }

        // 걸을 수 있고 다른 인물이 없는 칸 (주인공 칸은 부르는 쪽이 정함)
        private bool CutsceneCanStand(Vector2Int cell) => walker.CanStand(cell) && cell != walker.Position;

        // 그 방향으로 곧게 갈 수 있는 데까지 (최대 steps칸)
        private List<Vector2Int> StraightPath(Vector2Int from, Direction direction, int steps)
        {
            var path = new List<Vector2Int>();
            var cell = from;
            for (int i = 0; i < steps; i++)
            {
                var next = cell + direction.ToOffset();
                if (!walker.CanStand(next) || walker.Map.Get(next) == FieldTile.Door) break;
                path.Add(next);
                cell = next;
            }
            return path;
        }

        // 주인공 옆 칸 중 인물이 오는 쪽에 가까운 곳
        private Vector2Int BesideHero(Vector2Int npcCell)
        {
            Vector2Int best = walker.Position;
            float bestDistance = float.MaxValue;
            foreach (var direction in new[] { Direction.Right, Direction.Left, Direction.Down, Direction.Up })
            {
                var cell = walker.Position + direction.ToOffset();
                if (!CutsceneCanStand(cell) && cell != npcCell) continue;
                float distance = (cell - npcCell).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // 출입구 바로 앞 칸의 양옆 (인물·주인공이 나란히 설 자리). 둘 다 서 있을 수 있어야 함
        private bool TrySpotsBeside(Vector2Int door, out Vector2Int npcSpot, out Vector2Int heroSpot)
        {
            npcSpot = heroSpot = default;
            foreach (var inward in new[] { Direction.Down, Direction.Up, Direction.Left, Direction.Right })
            {
                var front = door + inward.ToOffset();
                if (!walker.CanStand(front)) continue;
                var side = inward == Direction.Down || inward == Direction.Up ? Vector2Int.right : Vector2Int.up;
                if (walker.CanStand(front - side) && walker.CanStand(front + side))
                {
                    npcSpot = front - side;
                    heroSpot = front + side;
                    return true;
                }
                if (walker.CanStand(front + side))
                {
                    npcSpot = front;
                    heroSpot = front + side;
                    return true;
                }
            }
            return false;
        }

        private void FaceEachOther(NpcView npc)
        {
            walker.Face(FacingToward(npc.Motion.Cell - walker.Position));
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            npc.Motion.Hold(FacingToward(walker.Position - npc.Motion.Cell));
            npcs.Redraw(npc);
        }

        private IEnumerator SayLines(NpcView npc, IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                string text = NpcLines.Render(line, session.Hero.DisplayName);
                float seconds = Mathf.Clamp(0.9f + text.Length * 0.075f, 1.4f, 4f);
                bubble.Speak(npc.Body, npc.Data.DisplayName, text, seconds + 0.4f);
                yield return WaitLine(seconds);
            }
            bubble.Hide();
        }

        // 말 한 줄을 기다림: 시간이 다 되거나 [확인]을 누르면 끝 (#245 사용자: 성격 급한 사람은 한 줄씩 넘기게). 그동안 [확인] 버튼이 켜져 있음
        private IEnumerator WaitLine(float seconds)
        {
            lineSkip = lineSkipped = false;
            SetConfirmReady(true);
            float start = Time.unscaledTime;
            float end = start + seconds * animationScale, earliest = start + MinLineSeconds * animationScale;
            while (Time.unscaledTime < end)
            {
                bool pressed = lineSkip || ConfirmKeyPressed();
                lineSkip = false;
                if (pressed && Time.unscaledTime >= earliest)
                {
                    lineSkipped = true;
                    break;
                }
                yield return null;
            }
            SetConfirmReady(false);
            interactCooldown = Mathf.Max(interactCooldown, AfterLineCooldown * animationScale);
        }

        // 주인공을 길대로 한 칸씩 (걷는 그림, 탐험 기록·미니맵도). 칸 조우·출입구는 없음
        private IEnumerator WalkHero(List<Vector2Int> path, float step = HeroStep)
        {
            int frame = 0;
            foreach (var cell in path)
            {
                var direction = FacingToward(cell - walker.Position);
                var from = CellCenter(walker.Position);
                var to = CellCenter(cell);
                walker.Face(direction);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / (step * animationScale))
                {
                    player.position = Vector3.Lerp(from, to, t);
                    playerRenderer.sprite = PlayerArt.Get(direction, frame + (t < 0.5f ? 0 : 1));
                    yield return null;
                }
                frame += 2;
                walker.WarpTo(cell);
                walker.Face(direction);
                player.position = to;
                if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, cell) > 0)
                {
                    minimap.Redraw();
                    RefreshDarkness();
                }
                minimap.SetPlayer(cell);
            }
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
        }

        // 인물을 길대로 한 칸씩 (컷씬 동안 인물의 움직임은 멈춰 있음 — UpdateNpcs가 쉼)
        private IEnumerator WalkNpc(NpcView npc, List<Vector2Int> path, float step)
        {
            int frame = 0;
            var cell = npc.Motion.Cell;
            foreach (var next in path)
            {
                var direction = FacingToward(next - cell);
                var from = CellCenter(cell);
                var to = CellCenter(next);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / (step * animationScale))
                {
                    npc.Body.position = Vector3.Lerp(from, to, t);
                    npc.PoseWalking(direction, frame + (t < 0.5f ? 0 : 1));
                    yield return null;
                }
                frame += 2;
                cell = next;
                npc.Motion.Teleport(cell, direction);
                npc.Body.position = to;
            }
            npcs.Redraw(npc);
        }
    }
}
