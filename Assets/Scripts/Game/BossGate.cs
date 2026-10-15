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
        Ready,     // 둘 다 갖춤 → [확인]하면 열림
        Open       // 열었거나, 이미 보스를 물리침 (예전 세이브 — 다시 막지 않는다)
    }

    // 보스 방 문 (#76, 2026-10-16 사용자 결정): 맵에 숨겨 둔 열쇠(K)를 찾고 + 그 지역 단어를 40개 발견해야 보스 방(G)이 열린다.
    // 플레이 시간을 늘리고 지역마다 단어를 충분히 만나게 하려는 것. 연 문은 세이브(WorldState.openedGates)에 남는다
    public static class BossGate
    {
        public static bool HasGate(FieldArea area) => area != null && area.Map.Find(FieldTile.Gate).HasValue;

        public static List<Vector2Int> Cells(FieldArea area) => area.Map.FindAll(FieldTile.Gate);

        // 이 지역 단어장에서 발견한 단어 수 (도감 기준)
        public static int WordsFound(FieldArea area, GameSession session) =>
            area.Words != null ? Dex.GetProgress(area.Words, session.Vocabulary).Discovered : 0;

        public static bool HasKey(FieldArea area, GameSession session) =>
            area.BossKey != null && session.Inventory.GetCount(area.BossKey) > 0;

        public static BossGateState State(FieldArea area, GameSession session)
        {
            if (!HasGate(area)) return BossGateState.None;
            if (session.World.IsGateOpened(area.AreaId) || (area.Boss != null && session.World.IsBossDefeated(area.BossId)))
                return BossGateState.Open;
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
            return true;
        }

        // 열쇠 받침대 'K': 아직 없으면 열쇠를 얻는다. 반환값: 이번에 얻었는지
        public static bool TakeKey(FieldArea area, GameSession session)
        {
            if (area.BossKey == null || HasKey(area, session)) return false;
            session.Inventory.Add(area.BossKey);
            return true;
        }
    }
}
