using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 > 처음 시작 (#124 사용자 — 테스터 "갑자기 초원에 떨어져 당황", 스토리 = Docs/GameStory 프롤로그):
    // (새 게임이면 맨 먼저 입장 영상 #129) →
    // 모습 고르기(8명) → 환영 → 혼잣말 "내 이름조차 기억나지 않아" → 깃펜으로 떠오른 이름 → 걷기·달리기 →
    // "풀숲으로 가서 몬스터와 싸워 보세요" → 첫 승리 뒤 목표(까먹대왕) 한 번. 1.0 세이브로 업데이트한 사람은 모습·이름만 한 번
    public partial class FieldScreen
    {
        private CharacterSelectView characterSelect;
        private NameEntryView nameEntry;
        private IntroCinematicView introView;
        private Image heroFace; // HUD 주인공 얼굴 (모습을 바꾸면 다시 그림)
        private bool heroSetupRunning;

        public CharacterSelectView CharacterSelect => characterSelect;
        public NameEntryView NameEntry => nameEntry;
        public IntroCinematicView Intro => introView;
        public bool IsHeroSetupRunning => heroSetupRunning;

        private void BuildHeroSetup()
        {
            characterSelect = CharacterSelectView.Create(hudRoot);
            nameEntry = NameEntryView.Create(hudRoot);
            introView = IntroCinematicView.Create(hudRoot); // 맨 위 (#129)
        }

        private IEnumerator HeroSetup()
        {
            heroSetupRunning = true;
            bool fresh = session.IsFreshStart;
            yield return null; // 화면 배치가 끝난 뒤

            // 입장 영상 (#129, 새 게임에서만 — [건너뛰기]·뒤로가기로 넘김). 그동안은 타이틀 음악
            if (fresh && playIntro)
            {
                bool done = false;
                Sound.PlayMusic(Music.Title);
                introView.Play(() => done = true);
                while (!done) yield return null;
                Sound.PlayMusic(AreaMusic);
            }

            string look = null;
            characterSelect.Show(session.Hero.Data.Looks, session.Hero.LookOrDefault, !fresh, id => look = id);
            while (look == null) yield return null;
            session.Hero.SetLook(look);
            ApplyHeroLook();
            if (!fresh)
            {
                // 이미 하던 사람은 풀숲·목표 안내를 다시 보지 않음
                session.Tutorials.Mark(TutorialProgress.GrassFight);
                session.Tutorials.Mark(TutorialProgress.Goal);
            }

            if (fresh)
            {
                yield return pauseTutorial.Tell(new[] { "영단어RPG에 오신 걸 환영합니다!" });
                yield return Monologue("여긴 어디지…?\n내 이름조차 기억나지 않아.", 2.8f);
                yield return Monologue("하지만 이 깃펜은…\n처음 보는 물건 같지 않아.", 2.8f);
            }

            string name = null;
            bubble.Hide();
            nameEntry.Show(fresh ? "깃펜으로 떠오른\n이름을 적어 주세요" : "이름을 정해 주세요", fresh ? "" : session.Hero.DisplayName, n => name = n);
            while (name == null) yield return null;
            session.Hero.Rename(name);
            RefreshHud();
            SaveNow();
            if (fresh) yield return Monologue($"{session.Hero.DisplayName}…\n그래, 내 이름이야.", 2.4f);

            heroSetupRunning = false;
            if (showTutorials && !session.Tutorials.Has(TutorialProgress.Field)) StartCoroutine(FieldGuide());
        }

        // 주인공 머리 위 혼잣말 — 그동안 움직이지 않음 (heroSetupRunning)
        private IEnumerator Monologue(string line, float seconds)
        {
            bubble.Say(line, seconds + 0.5f);
            yield return new WaitForSecondsRealtime(seconds * animationScale);
        }

        // 고른 모습으로 필드 주인공·HUD 얼굴을 다시 그림 (전투·가방·타이틀은 PlayerArt를 그때그때 씀)
        private void ApplyHeroLook()
        {
            PlayerArt.SetLook(session.Hero.LookOrDefault);
            if (playerRenderer != null) playerRenderer.sprite = PlayerArt.Get(walker != null ? walker.Facing : Direction.Down, 0);
            if (heroFace != null) heroFace.sprite = UiKit.HeroPortrait(session.Hero.Data);
        }

        // 첫 승리 뒤 (#124 사용자: "까먹대왕이라는 목표를 한번 상기"): 목표 한 줄이 가리키는 보스를 큰 안내로 한 번
        private IEnumerator GoalReminder()
        {
            var goal = Objectives.Current(session, area);
            var boss = goal.Destination != null && goal.Destination.Boss != null ? goal.Destination.Boss.Species : null;
            session.Tutorials.Mark(TutorialProgress.Goal);
            SaveNow();
            if (boss == null) yield break;
            yield return new WaitForSecondsRealtime(0.3f * animationScale);
            yield return pauseTutorial.Tell(new[]
            {
                $"목표는 {goal.Destination.DisplayName}의 {UiKit.WithJosa(boss.DisplayName, "을", "를")} 물리치는 것입니다!\n미니맵 아래 목표 한 줄이 길을 알려 줘요."
            });
        }
    }
}
