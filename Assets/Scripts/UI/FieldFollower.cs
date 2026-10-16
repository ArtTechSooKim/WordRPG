using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;

namespace WordRPG.UI
{
    // 필드에서 주인공을 따라다니는 동료 그림 (#78, 사용자 요청): 주인공이 지나간 길(자취)을 한 칸 뒤에서 그대로 따라온다.
    // 보이기만 하는 그림 — 땅을 밟는 개념이 없어서 풀숲 조우·출입구·몬스터·[확인] 같은 칸의 영향을 받지 않고,
    // 주인공 자취만 따라가므로 맵 밖으로 나갈 일도 없다. 순간이동(지역 이동·시작 지점 귀환)하면 주인공 뒤에 다시 선다
    public class FieldFollower
    {
        public const float Gap = 1f;              // 주인공 뒤 한 칸
        private const float TeleportJump = 1.6f;  // 한 프레임에 이만큼 넘게 움직이면 순간이동으로 보고 다시 세운다
        private const float FramesPerTile = 2f;   // 한 칸 걸을 때 발 그림 두 장 (달리면 저절로 빨라짐)

        private readonly Transform root;
        private readonly SpriteRenderer body;
        private readonly List<Vector3> trail = new List<Vector3>(); // 주인공이 지나간 점들 (끝 = 지금 주인공)
        private CompanionData companion;
        private CharacterSheet sheet;
        private Direction facing = Direction.Down;
        private float walked;
        private Vector3 lastHero;

        public bool Visible => root.gameObject.activeSelf;
        public Vector3 Position => root.position;
        public Direction Facing => facing;
        public CompanionData Companion => companion;

        public FieldFollower(Transform parent)
        {
            var go = new GameObject("Follower", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            root = go.transform;
            body = go.GetComponent<SpriteRenderer>();
            body.sortingOrder = 9;
            go.SetActive(false);
        }

        // 함께 다니는 동료 (null = 숨김). 새로 보이게 되면 주인공 뒤에 세운다
        public void SetCompanion(CompanionData data, Vector3 hero, Direction heroFacing, Func<Vector3, bool> canStand)
        {
            if (data == companion && Visible == (data != null)) return;
            companion = data;
            root.gameObject.SetActive(data != null);
            if (data == null) return;
            sheet = new CharacterSheet($"Art/NinjaAdventure/Companions/{data.CompanionId}_sheet");
            Place(hero, heroFacing, canStand);
        }

        // 주인공 바로 뒤(바라보는 반대쪽 한 칸)에 세운다. 거기가 서기 어색한 칸(벽 등)이면 주인공과 같은 자리에서 나온다
        public void Place(Vector3 hero, Direction heroFacing, Func<Vector3, bool> canStand)
        {
            var behind = hero - (Vector3)(Vector2)heroFacing.ToOffset() * Gap;
            if (canStand != null && !canStand(behind)) behind = hero;
            trail.Clear();
            trail.Add(behind);
            trail.Add(hero);
            root.position = behind;
            lastHero = hero;
            facing = heroFacing;
            walked = 0f;
            Show(false);
        }

        // 뗏목 (#81, 사용자: 아라도 같은 뗏목에 앉은 모습): 뗏목 뒤쪽 자리에 같은 쪽을 보고 선다. 자취는 지금 자리에서 다시 시작
        public void Ride(Vector3 seat, Vector3 hero, Direction heroFacing)
        {
            if (!Visible) return;
            trail.Clear();
            trail.Add(seat);
            trail.Add(hero);
            root.position = seat;
            lastHero = hero;
            facing = heroFacing;
            walked = 0f;
            Show(false);
            body.sortingOrder = seat.y < hero.y - 0.01f ? 11 : 9;
        }

        // 매 프레임: 주인공 위치를 자취에 더하고, 자취를 따라 주인공에게서 한 칸 떨어진 곳으로
        public void Tick(Vector3 hero, Direction heroFacing, Func<Vector3, bool> canStand)
        {
            if (!Visible) return;
            if ((hero - lastHero).magnitude > TeleportJump)
            {
                Place(hero, heroFacing, canStand);
                return;
            }
            lastHero = hero;
            if ((hero - trail[trail.Count - 1]).sqrMagnitude > 1e-6f) trail.Add(hero);

            var target = PointBehind(Gap);
            var delta = target - root.position;
            bool moved = delta.sqrMagnitude > 1e-6f;
            if (moved)
            {
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0 ? Direction.Right : Direction.Left)
                    : (delta.y > 0 ? Direction.Up : Direction.Down);
                walked += delta.magnitude;
            }
            root.position = target;
            Show(moved);
            // 위에서 내려다보는 화면: 주인공보다 아래(화면 앞쪽)에 있으면 주인공 위에 그린다
            body.sortingOrder = root.position.y < hero.y - 0.01f ? 11 : 9;
            Trim();
        }

        private void Show(bool moving)
        {
            int frame = moving ? Mathf.FloorToInt(walked * FramesPerTile) : 0;
            var sprite = sheet != null ? sheet.Get(facing, frame) : null;
            body.sprite = sprite != null ? sprite : PlaceholderArt.Player(facing);
            if (!moving) walked = 0f;
        }

        // 자취를 끝(주인공)에서부터 거꾸로 distance만큼 간 점. 자취가 짧으면 맨 처음 점
        private Vector3 PointBehind(float distance)
        {
            float left = distance;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                var a = trail[i];
                var b = trail[i - 1];
                float length = Vector3.Distance(a, b);
                if (length >= left) return Vector3.Lerp(a, b, left / Mathf.Max(1e-6f, length));
                left -= length;
            }
            return trail[0];
        }

        // 따라가는 데 필요 없는 오래된 점은 버린다 (한 칸 + 여유)
        private void Trim()
        {
            float kept = 0f;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                kept += Vector3.Distance(trail[i], trail[i - 1]);
                if (kept > Gap + 1f)
                {
                    trail.RemoveRange(0, i - 1);
                    return;
                }
            }
        }
    }
}
