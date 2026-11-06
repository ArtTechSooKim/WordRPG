using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using Random = System.Random;

namespace WordRPG.UI
{
    // 필드 위의 인물들 (#209, GDD 9-10): 그림과 움직임. 규칙은 NpcMotion·NpcLines
    public class NpcView
    {
        private readonly SpriteRenderer body;
        private readonly CharacterSheet sheet;
        private readonly Sprite[] frames;
        private float animalClock;

        public NpcData Data { get; }
        public NpcMotion Motion { get; }
        public NpcStage Stage { get; set; }
        public Vector3 Position => body.transform.position;
        public Transform Body => body.transform; // 컷씬이 직접 움직일 때 (#223 — 어르신이 샘으로 뛰어듦)

        public NpcView(NpcData data, Transform parent, Vector2Int home, Vector2Int cell, NpcMotionKind kind, Random rng, int sortingOrder = 9, float pace = 1f)
        {
            Data = data;
            Motion = new NpcMotion(home, kind, rng, cell, pace);
            var go = new GameObject($"Npc_{data.NpcId}", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            body = go.GetComponent<SpriteRenderer>();
            body.sortingOrder = sortingOrder; // 주인공(10) 바로 아래. 동굴에서는 어둠(14) 아래라 빛 안에서만 보임. 상인은 계산대 뒤 6
            string path = "Art/NinjaAdventure/Npcs/" + data.NpcId;
            if (data.IsAnimal) frames = CutFrames(Resources.Load<Texture2D>(path), data.FrameWidth);
            else sheet = new CharacterSheet(path);
        }

        private static Sprite[] CutFrames(Texture2D texture, int frameWidth)
        {
            if (texture == null) return new Sprite[0];
            var cut = new Sprite[Mathf.Max(1, texture.width / frameWidth)];
            for (int i = 0; i < cut.Length; i++)
                cut[i] = Sprite.Create(texture, new Rect(i * frameWidth, 0, frameWidth, texture.height), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            return cut;
        }

        public void Draw(float dt, Func<Vector2Int, Vector3> cellCenter)
        {
            var from = cellCenter(Motion.Previous);
            var to = cellCenter(Motion.Cell);
            body.transform.position = Vector3.Lerp(from, to, Motion.StepProgress) + Vector3.up * Motion.HopHeight;
            if (Data.IsAnimal) DrawAnimal(dt);
            else DrawPerson();
        }

        private void DrawPerson()
        {
            if (Motion.Pose == NpcPose.Crouch) body.sprite = sheet.Pose(PlayerPose.Crouch);
            else if (Motion.Pose == NpcPose.Item) body.sprite = sheet.Pose(PlayerPose.Item);
            else
            {
                int walkFrame = Motion.Walking ? Motion.StepsTaken * 2 + (int)(Motion.StepProgress * 2f) : 0;
                body.sprite = sheet.Get(Motion.Facing, walkFrame);
            }
        }

        // 동물: 그림이 두 장뿐이라 번갈아 쉬고, 오른쪽을 볼 땐 뒤집는다
        private void DrawAnimal(float dt)
        {
            if (frames.Length == 0) return;
            animalClock += dt;
            body.sprite = frames[(int)(animalClock / 0.8f) % frames.Length];
            body.flipX = Motion.Facing == Direction.Right;
        }

        public void Destroy() => UnityEngine.Object.Destroy(body.gameObject);
    }

    public class FieldNpcs
    {
        private readonly Transform parent;
        private readonly Func<Vector2Int, Vector3> cellCenter;
        private readonly List<NpcView> views = new List<NpcView>();
        private FieldMap map;
        private Vector2Int playerCell;

        public IReadOnlyList<NpcView> Views => views;
        public NpcView Merchant { get; private set; } // 가판대 안의 상인 (#210)

        public FieldNpcs(Transform parent, Func<Vector2Int, Vector3> cellCenter)
        {
            this.parent = parent;
            this.cellCenter = cellCenter;
        }

        // 지역에 들어갈 때. 예전 세이브의 위치가 인물 자리와 겹치면 인물이 옆으로 비켜 선다
        public void Build(FieldArea area, GameSession session, Vector2Int player, Random rng)
        {
            foreach (var view in views) view.Destroy();
            views.Clear();
            Merchant?.Destroy();
            Merchant = null;
            map = area.Map;
            playerCell = player;
            var merchantData = area.Shop != null ? area.Shop.Merchant : null;
            if (merchantData != null && map.ShopBlocks.Count > 0)
            {
                var cell = map.ShopBlocks[0].MerchantCell;
                var first = merchantData.Stages.Count > 0 ? merchantData.Stages[0] : null;
                Merchant = new NpcView(merchantData, parent, cell, cell, first != null ? first.Motion : NpcMotionKind.Stand, rng, 6, first != null ? first.Pace : 1f);
                Merchant.Draw(0f, cellCenter);
            }
            var spots = map.NpcSpots;
            for (int i = 0; i < spots.Count && i < area.Npcs.Count; i++)
            {
                var data = area.Npcs[i];
                if (data == null || SpringLeap.Gone(data, session.World)) continue; // 샘으로 뛰어든 인물은 다시 안 나옴 (#223)
                var stage = StageOf(data, area, session);
                var cell = spots[i];
                if (cell == player) cell = FreeCellBeside(cell);
                var view = new NpcView(data, parent, spots[i], cell, stage != null ? stage.Motion : NpcMotionKind.Stand, rng, 9, stage != null ? stage.Pace : 1f) { Stage = stage };
                views.Add(view);
                view.Draw(0f, cellCenter);
            }
        }

        private Vector2Int FreeCellBeside(Vector2Int cell)
        {
            foreach (var direction in new[] { Direction.Left, Direction.Right, Direction.Down, Direction.Up })
            {
                var next = cell + direction.ToOffset();
                if (CanStand(next)) return next;
            }
            return cell;
        }

        private static NpcStage StageOf(NpcData data, FieldArea area, GameSession session)
        {
            int discovered = area.Words != null ? Dex.GetProgress(area.Words, session.Vocabulary).Discovered : 0;
            return NpcLines.Current(data, boss => session.World.IsBossDefeated(boss.BossId), discovered);
        }

        // 보스를 이겼거나 단어를 찾았을 때: 말과 움직임이 바뀜
        public void Refresh(FieldArea area, GameSession session)
        {
            foreach (var view in views)
            {
                view.Stage = StageOf(view.Data, area, session);
                if (view.Stage != null) view.Motion.SetKind(view.Stage.Motion, view.Stage.Pace);
            }
        }

        // 컷씬이 끝나 사라짐 (#223)
        public void Remove(NpcView view)
        {
            view.Destroy();
            views.Remove(view);
        }

        // 컷씬이 바라보는 쪽을 바꾼 뒤 그림만 다시 (움직임은 멈춘 채)
        public void Redraw(NpcView view) => view.Draw(0f, cellCenter);

        public bool IsOccupied(Vector2Int cell)
        {
            foreach (var view in views)
                if (view.Motion.Occupies(cell)) return true;
            return false;
        }

        private bool CanStand(Vector2Int cell)
        {
            var tile = map.Get(cell);
            return (tile == FieldTile.Floor || tile == FieldTile.Lawn) && cell != playerCell && !IsOccupied(cell);
        }

        public void Tick(float dt, Vector2Int player, NpcView talking)
        {
            playerCell = player;
            if (Merchant != null)
            {
                if (Merchant == talking) Merchant.Motion.Hold(Direction.Down);
                else Merchant.Motion.Release();
                Merchant.Motion.Tick(dt, _ => false);
                Merchant.Draw(dt, cellCenter);
            }
            foreach (var view in views)
            {
                var motion = view.Motion;
                var offset = player - motion.Cell;
                bool beside = Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1;
                if (beside || view == talking) motion.Hold(FacingTo(offset));
                else motion.Release();
                motion.Tick(dt, CanStand);
                view.Draw(dt, cellCenter);
            }
        }

        private static Direction FacingTo(Vector2Int offset)
        {
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)) return offset.x < 0 ? Direction.Left : Direction.Right;
            return offset.y < 0 ? Direction.Down : Direction.Up;
        }

        // 말을 걸 수 있는 인물: 바라보는 칸 먼저, 그다음 옆 칸
        public NpcView FindTalkable(Vector2Int position, Direction facing, bool facingOnly)
        {
            var ahead = At(position + facing.ToOffset());
            if (ahead != null || facingOnly) return ahead;
            foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                var view = At(position + direction.ToOffset());
                if (view != null) return view;
            }
            return null;
        }

        private NpcView At(Vector2Int cell)
        {
            foreach (var view in views)
                if (view.Stage != null && view.Motion.Cell == cell) return view;
            return null;
        }
    }
}
