namespace WordRPG.Monsters
{
    // 보스마다 다른 기믹 (#236 사용자 ㉑㉝: 보스들이 기믹이 똑같아서 식상함). 에셋에 번호로 저장 — 새 기믹은 맨 뒤
    public enum BossGimmick
    {
        None,
        Forget, // 까먹대왕: 보스전 문제마다 크리티컬 시간이 지나면 정답 버튼이 사라져 무조건 틀림 (BattleScreen RunQuiz)
        Blank,  // 빈칸두꺼비: 싸우는 동안 늘 문제 단어 글자의 25%가 빈칸, 답하면 채워 보여 줌 (WordBlanks, #245)
        Shuffle, // 헷갈너구리: 보기 4개가 2초마다 자리를 섞음, 섞이기 0.3초 전에 흔들림 (#238 사용자 아이디어 1)
        Overgrow // 말줄임덩굴: 매 차례 덩굴이 하나도 없으면 양옆에 덩굴 둘이 자람 (MonsterSpecies.Minion, #246 사용자)
    }
}
