using WordRPG.Field;

namespace WordRPG.Game
{
    // 어르신이 샘으로 뛰어듦 (#223 사용자, GDD 9-11): 그 인물의 말이 돌아온 뒤(정한 보스를 물리침) 다른 지역 샘으로 갈 수 있게 되면,
    // 주인공이 가까이 올 때 '샘은 땅 밑으로 다 이어져 있다'고 말하고 샘으로 쓩 뛰어들어 사라진다. 한 번만 보는 컷씬 — 그 뒤 그 지역에 다시 안 나옴.
    // 일어난 일은 세이브 WorldState.events (id = "spring_leap:npcId")
    public static class SpringLeap
    {
        public const string EventPrefix = "spring_leap:";
        public const int NoticeRange = 3; // 주인공이 이만큼(칸, 가로+세로) 가까이 오면

        public static string EventId(NpcData npc) => EventPrefix + npc.NpcId;

        // 이미 뛰어들어 그 지역에 없음
        public static bool Gone(NpcData npc, WorldState world) => npc != null && npc.LeapsIntoSpring && world.HasEvent(EventId(npc));

        // 보스를 물리쳤고 아직 안 뛰어듦 (샘으로 갈 곳이 있는지는 화면이 따로 봄)
        public static bool Ready(NpcData npc, WorldState world) =>
            npc != null && npc.LeapsIntoSpring && world.IsBossDefeated(npc.SpringLeapAfter.BossId) && !world.HasEvent(EventId(npc));

        // 누군가 샘으로 뛰어드는 걸 봤는지 — 주인공 혼잣말이 달라짐
        public static bool AnyoneLeapt(WorldState world) => world.HasEventStartingWith(EventPrefix);
    }
}
