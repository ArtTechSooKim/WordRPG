using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Game
{
    // 누적 전적. 세이브 파일에 들어간다
    [Serializable]
    public class PlayerRecord
    {
        [SerializeField] private int battlesWon;
        [SerializeField] private int battlesLost;
        [SerializeField] private int battlesFled; // 도망친 전투 (#53, 예전 세이브는 0)
        [SerializeField] private int correctAnswers;
        [SerializeField] private int wrongAnswers;
        [SerializeField] private List<string> claimedRegionIds = new List<string>(); // 도감 완성 보상을 받은 지역

        public int BattlesWon => battlesWon;
        public int BattlesLost => battlesLost;
        public int BattlesFled => battlesFled;
        public int BattlesFought => battlesWon + battlesLost + battlesFled;
        public int CorrectAnswers => correctAnswers;
        public int WrongAnswers => wrongAnswers;

        public bool HasClaimedRegion(string regionId) => claimedRegionIds.Contains(regionId);

        public void MarkRegionClaimed(string regionId)
        {
            if (!claimedRegionIds.Contains(regionId)) claimedRegionIds.Add(regionId);
        }

        public void RecordBattle(bool won, int correct, int wrong)
        {
            if (won) battlesWon++;
            else battlesLost++;
            correctAnswers += correct;
            wrongAnswers += wrong;
        }

        // 도망친 전투: 이기지도 지지도 않음. 푼 문제는 그대로 센다
        public void RecordFled(int correct, int wrong)
        {
            battlesFled++;
            correctAnswers += correct;
            wrongAnswers += wrong;
        }
    }
}
