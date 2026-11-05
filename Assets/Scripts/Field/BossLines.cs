namespace WordRPG.Field
{
    // 보스 대사 (#207, 스토리 문서 5장): 전투를 시작할 때 보스 한 줄 + 주인공 한 줄 (전투 기록 칸 — 화면을 막지 않음).
    // 대사가 없는 보스는 예전 문구 그대로
    public static class BossLines
    {
        public static string Intro(BossEncounter boss, string heroName)
        {
            string name = boss.Species.DisplayName;
            if (boss.BossLine.Length == 0) return $"보스 출현! {name} Lv{boss.Level} — 정답으로 맞서라!";
            string text = $"{name}: \"{boss.BossLine}\"";
            return boss.HeroLine.Length == 0 ? text : $"{text}\n{heroName}: \"{boss.HeroLine}\"";
        }
    }
}
