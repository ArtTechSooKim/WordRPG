using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 > 처음 시작 (#124 사용자 — 테스터 "갑자기 초원에 떨어져 당황", 스토리 = Docs/GameStory 프롤로그):
    // (입장 영상을 아직 못 봤으면 맨 먼저 #129 — #131부터 하던 사람도 한 번) →
    // 모습 고르기(8명) → 환영 → 혼잣말 "내 이름조차 기억나지 않아" → 깃펜으로 떠오른 이름 → 걷기·달리기 →
    // (#133 분신이 풀숲으로 걸어 들어가는 모습 →) "풀숲으로 가서 몬스터와 싸워 보세요" → 첫 승리 뒤 목표(까먹대왕) 한 번.
    // 1.0 세이브로 업데이트한 사람은 영상 → 모습·이름만 한 번
    public partial class FieldScreen
    {
        private const float WayGhostStep = 0.13f; // 풀숲 가는 길을 보여 주는 분신이 한 칸 가는 시간 (달리기쯤)

        private CharacterSelectView characterSelect;
        private NameEntryView nameEntry;
        private IntroCinematicView introView;
        private Image heroFace; // HUD 주인공 얼굴 (모습을 바꾸면 다시 그림)
        private bool heroSetupRunning;

        public CharacterSelectView CharacterSelect => characterSelect;
        public NameEntryView NameEntry => nameEntry;
        public IntroCinematicView Intro => introView;
        public bool IsHeroSetupRunning => heroSetupRunning;
        public int WayToGrassShown { get; private set; }  // 풀숲 가는 길을 보여 준 횟수 (테스트용)
        public int WayToGrassTiles { get; private set; }  // 그때 분신이 걸은 칸 수 (테스트용)
        public int WayGhostTrails { get; private set; }   // 그때 분신이 남긴 잔상 수 (테스트용)

        // 필드에 들어올 때 처음 시작 흐름이 필요한지: 모습·이름을 아직 안 정했거나 입장 영상을 아직 못 봄
        private bool NeedsPrologue => heroSetup && (session.NeedsHeroSetup || WantsIntro);
        private bool WantsIntro => playIntro && !session.Tutorials.Has(TutorialProgress.Intro);
        // 아직 한 번도 싸워 보지 않은 처음 하는 사람 — 풀숲 두 번째 칸에서 반드시 몬스터 (#133, EnterArea)
        private bool FirstFightPending => heroSetup && session.Record.BattlesFought == 0;

        private void BuildHeroSetup()
        {
            characterSelect = CharacterSelectView.Create(hudRoot);
            nameEntry = NameEntryView.Create(hudRoot);
            introView = IntroCinematicView.Create(hudRoot); // 맨 위 (#129)
        }

        private IEnumerator Prologue()
        {
            heroSetupRunning = true;
            bool fresh = session.IsFreshStart;
            yield return null; // 화면 배치가 끝난 뒤

            // 입장 영상 (#129, [건너뛰기]·뒤로가기로 넘김). 음악은 영상 전용 (#133 사용자: 이야기를 보여 주니 따로).
            // #131 사용자 "이미 했던 사람도 영상을 볼 수 있게": 새 게임만이 아니라 못 본 사람 모두 한 번 (본 기록은 세이브)
            if (WantsIntro)
            {
                bool done = false;
                Sound.PlayMusic(Music.Intro);
                introView.Play(() => done = true);
                while (!done) yield return null;
                MarkIntroSeen(); // 못 열었으면 다음에 다시 (#138)
            }

            if (session.NeedsHeroSetup) yield return ChooseLookAndName(fresh);
            Sound.PlayMusic(AreaMusic);

            heroSetupRunning = false;
            if (showTutorials && !session.Tutorials.Has(TutorialProgress.Field)) StartCoroutine(FieldGuide());
        }

        private void MarkIntroSeen()
        {
            if (introView.Failed) return;
            session.Tutorials.Mark(TutorialProgress.Intro);
            SaveNow();
        }

        // 설정 > [입장 영상 다시 보기] (#138 사용자: 스토리 영상을 다시 볼 수 있게) — 끝나면 필드로
        private void ReplayIntro()
        {
            Sound.PlayMusic(Music.Intro);
            introView.Play(() =>
            {
                Sound.PlayMusic(AreaMusic);
                if (introView.Failed) ShowToast("영상을 열 수 없어요. 잠시 뒤에 다시 해 보세요", 3f);
                MarkIntroSeen();
            });
        }

        private IEnumerator ChooseLookAndName(bool fresh)
        {
            string look = null;
            Sound.PlayMusic(Music.HeroSelect); // #133 사용자: 캐릭터 고르기는 신나는 음악으로 — 고르면 지역 음악
            characterSelect.Show(session.Hero.Data.Looks, session.Hero.LookOrDefault, !fresh, id => look = id);
            while (look == null) yield return null;
            Sound.PlayMusic(AreaMusic);
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
        }

        // 주인공 머리 위 혼잣말 — 그동안 움직이지 않음 (heroSetupRunning)
        private IEnumerator Monologue(string line, float seconds)
        {
            bubble.Say(line, seconds + 0.5f);
            yield return WaitLine(seconds);
            if (lineSkipped) bubble.Hide();
        }

        // 고른 모습으로 필드 주인공·HUD 얼굴을 다시 그림 (전투·가방·타이틀은 PlayerArt를 그때그때 씀)
        private void ApplyHeroLook()
        {
            PlayerArt.SetLook(session.Hero.LookOrDefault);
            if (playerRenderer != null) playerRenderer.sprite = PlayerArt.Get(walker != null ? walker.Facing : Direction.Down, 0);
            if (heroFace != null) heroFace.sprite = UiKit.HeroPortrait(session.Hero.Data);
        }

        // #133 사용자: "처음 들어온 사람은 풀숲이 뭔지 몰라" → 카메라가 가장 가까운 풀숲으로 따라가며 주인공의 반투명 분신이
        // 잔상을 남기고 풀숲 안으로 걸어 들어감 → 잠깐 멈췄다 옅어지고 카메라가 주인공에게 돌아옴. 그동안 주인공은 움직이지 않음
        private IEnumerator ShowWayToGrass()
        {
            var path = FieldReach.PathToNearest(walker.Map, walker.Position, cell => walker.CanStand(cell),
                tile => tile == FieldTile.Grass, extra: 1);
            if (path.Count == 0) yield break;
            if (moving) SnapPlayer();
            transitioning = true;
            WayToGrassShown++;
            WayToGrassTiles = path.Count;

            var offset = Vector3.down * (tilesAcross / Mathf.Max(0.1f, cam.aspect) * 0.10f); // UpdateCamera와 같은 자리
            var points = new List<Vector3> { player.position };
            points.AddRange(path.ConvertAll(CellCenter));
            var ghost = new FieldGhost(transform, player.position, playerRenderer.sortingOrder);
            yield return WaitUnscaled(0.25f);
            yield return ghost.Walk(points, WayGhostStep * animationScale, at => cameraFocus = at + offset);
            WayGhostTrails = ghost.TrailsLeft;
            yield return WaitUnscaled(0.6f);
            yield return ghost.FadeOut(0.4f * animationScale);
            ghost.Destroy();
            yield return PanCamera(null, 0.7f);
            transitioning = false;
        }

        // 첫 승리 뒤 (#124 사용자: "까먹대왕이라는 목표를 한번 상기"): 목표 한 줄이 가리키는 보스를 큰 안내로 한 번
        private IEnumerator GoalReminder()
        {
            var goal = Objectives.Current(session, area);
            var boss = goal.Destination != null && goal.Destination.Boss != null ? goal.Destination.Boss.Species : null;
            session.Tutorials.Mark(TutorialProgress.Goal);
            SaveNow();
            if (boss == null) yield break;
            if (database != null && goal.Destination != SpringWarp.FirstBossArea(database.Areas)) yield break; // #234: 숲 등은 그 지역 인물이 알려 줌
            yield return new WaitForSecondsRealtime(0.3f * animationScale);
            // 목표 한 줄을 막 위로 올려 [확인]을 누를 때까지 글자·별이 밝아졌다 어두워졌다 (#233 사용자: 글을 안 읽어도 어디를 보라는지 알게)
            int order = objectivePill.GetSiblingIndex();
            objectivePill.SetAsLastSibling();
            ObjectivePulsing = true;
            var pulse = StartCoroutine(PulseObjective());
            yield return pauseTutorial.Tell(new[]
            {
                $"목표는 {goal.Destination.DisplayName}의 {UiKit.WithJosa(boss.DisplayName, "을", "를")} 물리치는 것입니다!\n미니맵 아래에 목표가 있으니 잘 확인해주세요!"
            });
            StopCoroutine(pulse);
            ObjectivePulsing = false;
            ResetObjectiveGlow();
            objectivePill.SetSiblingIndex(order);
        }

        public bool ObjectivePulsing { get; private set; } // 테스트용

        // 시간이 멈춘 안내 중이라 실제 시간으로. 한 번 밝아졌다 어두워지는 데 1초
        private IEnumerator PulseObjective()
        {
            while (true)
            {
                SetObjectiveGlow(Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                yield return null;
            }
        }

        // glow 0 = 어둡게, 1 = 가장 밝게 (글자는 흐린 흰색 → 금빛 흰색, 별은 어두운 금색 → 밝은 금색 + 살짝 커짐)
        private void SetObjectiveGlow(float glow)
        {
            objectiveText.color = Color.Lerp(new Color(0.6f, 0.62f, 0.7f), new Color(1f, 0.96f, 0.72f), glow);
            SetStar(Color.Lerp(Palette.Gold * new Color(0.55f, 0.55f, 0.55f, 1f), new Color(1f, 0.95f, 0.55f), glow), 1f + 0.25f * glow);
        }

        private void ResetObjectiveGlow()
        {
            objectiveText.color = Palette.Text;
            SetStar(Palette.Gold, 1f);
        }

        private void SetStar(Color color, float scale)
        {
            var star = objectivePill.Find("Star");
            if (star == null) return;
            star.GetComponent<UnityEngine.UI.Text>().color = color;
            star.localScale = Vector3.one * scale;
        }
    }
}
