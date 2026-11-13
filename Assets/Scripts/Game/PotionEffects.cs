using WordRPG.Items;

namespace WordRPG.Game
{
    // 효과 물약이 남긴 효과 (#146 사용자, 숲 상점부터): 세이브에 들어간다 (SaveData.WithPotionEffects).
    // 새 단어 = 전투 문제에 새 단어만 남은 수만큼 (새 단어가 실제로 나올 때만 줄어 낭비가 없음) · 조용한 걸음 = 그 걸음 동안 몬스터를 안 만남.
    // 또 쓰면 더해진다
    public class PotionEffects
    {
        public int NewWordsLeft { get; private set; }
        public int QuietStepsLeft { get; private set; }
        public bool Quiet => QuietStepsLeft > 0;

        public PotionEffects(int newWords = 0, int quietSteps = 0)
        {
            NewWordsLeft = newWords > 0 ? newWords : 0;
            QuietStepsLeft = quietSteps > 0 ? quietSteps : 0;
        }

        public void Apply(ItemData item)
        {
            if (item == null) return;
            if (item.Effect == ItemEffect.NewWords) NewWordsLeft += item.EffectAmount;
            else if (item.Effect == ItemEffect.Quiet) QuietStepsLeft += item.EffectAmount;
        }

        // 이번 문제를 새 단어로 낼지. 보스전(다시 싸우기 포함)에서는 쉼 — 남은 수는 그대로 다음 전투로 (#265 사용자:
        // 새 단어는 뜻 카드를 보여 준 뒤 묻기 때문에, 물약을 마시고 보스와 싸우면 답을 다 알고 싸우게 됨)
        public bool WantsNewWord(bool bossBattle) => !bossBattle && NewWordsLeft > 0;

        public void SpendNewWord()
        {
            if (NewWordsLeft > 0) NewWordsLeft--;
        }

#if WORDRPG_TESTER || UNITY_EDITOR
        public void TesterSetQuietSteps(int steps) => QuietStepsLeft = steps > 0 ? steps : 0; // 테스터 메뉴 [살금살금 상시] (#251)
#endif

        // 한 걸음. 이번 걸음으로 효과가 끝났으면 true
        public bool TakeStep()
        {
            if (QuietStepsLeft <= 0) return false;
            QuietStepsLeft--;
            return QuietStepsLeft == 0;
        }
    }
}
