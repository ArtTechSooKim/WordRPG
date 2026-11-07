using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    public enum BossGateState
    {
        None,      // 이 지역엔 보스 방 문이 없음
        NeedKey,   // 열쇠를 아직 못 찾음 → "열쇠가 필요한 것 같다"
        NeedWords, // 열쇠는 있지만 이 지역 단어를 덜 발견함 → "보스를 상대하려면 새로운 단어로 힘을 모아야해"
        Ready,     // 둘 다 갖춤 → [확인]하면 열림 (반딧돌 문은 다 밝힌 순간 저절로)
        Open,      // 열었거나, 이미 보스를 물리침 (예전 세이브 — 다시 막지 않는다)
        NeedStones // 반딧돌 문 (#233, 동굴): 글자 반딧돌을 아직 다 못 밝힘
    }

    // 보스 방 문 (#76, 2026-10-16 사용자 결정): 맵에 숨겨 둔 열쇠(K)를 찾고 + 그 지역 단어를 40개 발견해야 보스 방(G)이 열린다.
    // 플레이 시간을 늘리고 지역마다 단어를 충분히 만나게 하려는 것. 연 문은 세이브(WorldState.openedGates)에 남는다.
    // 문을 열면 열쇠는 더 쓸 데가 없어서 소지품에서 사라진다 (#147)
    public static class BossGate
    {
        public static bool HasGate(FieldArea area) => area != null && area.Map.Find(FieldTile.Gate).HasValue;

        public static List<Vector2Int> Cells(FieldArea area) => area.Map.FindAll(FieldTile.Gate);

        // 반딧돌 문 (#233 사용자 ㉟): 열쇠 받침대 'K'가 없고 글자 반딧돌 'I'가 있는 지역(동굴)은 반딧돌을 모두 밝히면 열림 — 단어 수 조건 없음.
        // 열쇠 아이템(bossKey)은 예전 세이브에 남은 열쇠를 치우려고 그대로 둠 (문이 열리면 DiscardUsedKey)
        public static bool OpensWithStones(FieldArea area) =>
            HasGate(area) && !area.Map.Find(FieldTile.Key).HasValue && GlowStones.Stones(area).Count > 0;

        // 이 지역 단어장에서 발견한 단어 수 (도감 기준)
        public static int WordsFound(FieldArea area, GameSession session) =>
            area.Words != null ? Dex.GetProgress(area.Words, session.Vocabulary).Discovered : 0;

        public static bool HasKey(FieldArea area, GameSession session) =>
            area.BossKey != null && session.Inventory.GetCount(area.BossKey) > 0;

        // 열쇠를 이미 가져갔는지: 갖고 있거나, 문을 열어서 다 썼거나 → 받침대는 빈 모습 그대로
        public static bool KeyTaken(FieldArea area, GameSession session) => HasKey(area, session) || IsOpen(area, session);

        public static BossGateState State(FieldArea area, GameSession session)
        {
            if (!HasGate(area)) return BossGateState.None;
            if (session.World.IsGateOpened(area.AreaId) || (area.Boss != null && session.World.IsBossDefeated(area.BossId)))
                return BossGateState.Open;
            if (OpensWithStones(area)) return GlowStones.AllLit(area, session.World) ? BossGateState.Ready : BossGateState.NeedStones;
            if (!HasKey(area, session)) return BossGateState.NeedKey;
            if (WordsFound(area, session) < area.BossGateWords) return BossGateState.NeedWords;
            return BossGateState.Ready;
        }

        public static bool IsOpen(FieldArea area, GameSession session) => State(area, session) == BossGateState.Open;

        // 다 갖췄으면 문을 연다. 반환값: 이번에 열었는지
        public static bool TryOpen(FieldArea area, GameSession session)
        {
            if (State(area, session) != BossGateState.Ready) return false;
            session.World.MarkGateOpened(area.AreaId);
            DiscardUsedKey(area, session); // 열쇠 문이면 쓴 열쇠, 반딧돌 문이면 예전에 주운 열쇠
            return true;
        }

        // 예전 세이브 (#147 전에 문을 연 사람): 다 쓴 열쇠가 소지품에 남아 있으면 치움. 불러올 때 지역마다 부름
        public static void DiscardUsedKey(FieldArea area, GameSession session)
        {
            if (area.BossKey == null || !IsOpen(area, session)) return;
            int left = session.Inventory.GetCount(area.BossKey);
            if (left > 0) session.Inventory.TryRemove(area.BossKey, left);
        }

        // 열쇠 받침대 'K': 아직 없으면 열쇠를 얻는다. 반환값: 이번에 얻었는지
        public static bool TakeKey(FieldArea area, GameSession session)
        {
            if (area.BossKey == null || KeyTaken(area, session)) return false;
            session.Inventory.Add(area.BossKey);
            return true;
        }
    }
}
