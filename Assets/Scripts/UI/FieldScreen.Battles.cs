using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 필드 > 전투: 조우 → 전투 화면을 덮고 결과 처리, 보스를 이긴 뒤(시작 지점·길 열림 연출), 지면 다시 일어남, 수련
    public partial class FieldScreen
    {
        private IEnumerator Encounter(List<MonsterInstance> enemies = null, string intro = null, bool boss = false)
        {
            transitioning = true;
            Sound.Play(Sfx.Encounter);
            Sound.PlayMusic(boss ? Music.Boss : Music.Battle);
            flash.gameObject.SetActive(true);
            float duration = 0.4f * animationScale;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                flash.color = new Color(1, 1, 1, Mathf.PingPong(k * 3f, 1f) * 0.85f);
                yield return null;
            }
            flash.gameObject.SetActive(false);

            enemies = enemies ?? area.Encounters.Roll(rng);
            transitioning = false;
            inBattle = true;
            bossBattle = boss;
            HideToast();
            battle.BeginBattle(enemies, area.Words, OnBattleFinished, intro, area.Theme, boss);
        }

        private Music AreaMusic => ThemeLooks.Of(area.Theme).Music; // 테마 표 (#118)

        // 이기면 보상(보스면 길 열림 등), 지면 시작 지점에서 다시 일어남, 도망치면 (#53) 그 자리 그대로
        private void OnBattleFinished(BattlePhase outcome)
        {
            bool won = outcome == BattlePhase.Victory;
            inBattle = false;
            var foughtRoamer = battleRoamer;
            battleRoamer = null;
            if (foughtRoamer != null && roaming != null)
            {
                if (won)
                {
                    roaming.Defeat(foughtRoamer);
                    roamerViews.Remove(foughtRoamer);
                }
                else roaming.Stun(foughtRoamer); // 도망쳤거나 져서 시작 지점으로 — 잠깐 멈춰 거리를 벌림
            }
            RefreshPreemptBooks();
            Sound.PlayMusic(AreaMusic);
            encounterCounter.Reset();
            bool wasBoss = bossBattle;
            bossBattle = false;

            if (won && wasBoss)
            {
                session.World.MarkBossDefeated(area.BossId);
                var bossCell = walker.Map.BossPosition.Value;
                tilemap.SetTile(new Vector3Int(bossCell.x, bossCell.y, 0), TileFor(FieldTile.Boss, true));
                var lectern = walker.Map.Find(FieldTile.Lectern);
                if (lectern.HasValue && area.HasDictionary)
                    tilemap.SetTile(new Vector3Int(lectern.Value.x, lectern.Value.y, 0), TileFor(FieldTile.Lectern, true));
                minimap.Redraw();
                RefreshNameTags();
                string name = area.Boss.Species.DisplayName;
                var relic = session.GrantRelic(area.Boss.RewardRelic);
                var rewardItem = area.Boss.RewardItem; // 기술문서 등
                if (rewardItem != null) session.Inventory.Add(rewardItem);
                SaveNow();
                string message = relic != null
                    ? $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n성유물 '{relic.Data.DisplayName}' 획득 — {RelicHint(relic)}"
                    : $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n{area.DisplayName}에 잊혀진 기억이 돌아왔다";
                if (rewardItem != null) message += $"\n{rewardItem.DisplayName} 획득!";
                if (area.HasDictionary) message += $"\n쉼터 받침대에 {UiKit.WithJosa(area.DictionaryName, "이", "가")} 놓였다";
                // 이 보스가 여는 출입구가 있으면 카메라가 가서 보여 준다 → 기술 배우기 → 시작 지점으로 돌아갈지 묻기
                var gates = database != null ? FieldArea.GatesOpenedBy(area, database.Areas) : new List<AreaGate>();
                StartCoroutine(AfterBossVictory(message, gates, rewardItem != null && rewardItem.IsSkillDocument ? rewardItem : null));
            }
            else if (outcome == BattlePhase.Defeat)
            {
                // 패배: 전투 화면이 이미 주인공을 회복시켰다. 이 지역의 시작 위치로 돌아간다
                walker.WarpTo(walker.Map.Start);
                SnapPlayer();
                RefreshNameTags();
                session.World.SetPosition(area.AreaId, walker.Position);
                SaveNow();
                StartCoroutine(Respawn($"{area.DisplayName} 시작 지점으로 돌아왔다. HP가 회복되었다!"));
            }
            RefreshHud();
        }

        // 보스를 이긴 뒤 (#60 사용자 요청): 승리 알림(또는 길 열림 연출) → 기술문서를 받았으면 배우기 →
        // "시작 지점으로 돌아갈까요?" — 보스 방은 대개 지역 맨 끝이라 걸어서 돌아가는 수고를 덜어 준다
        private IEnumerator AfterBossVictory(string message, List<AreaGate> gates, ItemData document)
        {
            var bossArea = area;
            if (gates.Count > 0) yield return RevealGates(gates, message);
            else
            {
                ShowToast(message, 4.5f);
                yield return WaitUnscaled(2.5f); // 받은 성유물·사전 소식을 먼저 읽게
            }
            if (document != null && !session.Hero.Knows(document))
            {
                OfferDocument(document);
                while (learnView.IsOpen) yield return null;
            }
            while (inBattle || transitioning || IsPanelOpen) yield return null;
            if (area != bossArea || walker.Position == walker.Map.Start) yield break; // 이미 다른 지역이나 시작 지점이면 묻지 않음
            returnDialog.Show(BossDefeatedTitle, $"{area.DisplayName} 시작 지점으로 돌아갈까요?", "돌아가기", false,
                () => StartCoroutine(ReturnToStart()), "star_full", "여기 있기");
        }

        private const string BossDefeatedTitle = "보스를 물리쳤다!";

        // 시작 지점으로: 어두워짐 + 문 소리 → 시작 지점에 정면으로 서서 밝아짐
        private IEnumerator ReturnToStart()
        {
            transitioning = true;
            ReturnsToStart++;
            Sound.Play(Sfx.Door);
            bubble.Hide();
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);
            walker.WarpTo(walker.Map.Start);
            walker.Face(Direction.Down);
            SnapPlayer();
            if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, walker.Position) > 0)
            {
                minimap.Redraw();
                RefreshDarkness();
            }
            RefreshNameTags();
            session.World.SetPosition(area.AreaId, walker.Position);
            SaveNow();
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);
            Destroy(cutscene.gameObject);
            transitioning = false;
            ShowToast($"{area.DisplayName} 시작 지점으로 돌아왔다");
        }

        // 지고 나서 시작 지점에서 다시 일어나는 연출: 어둠이 걷힘 → 쓰러진 주인공이 발밑 빛 고리 속에 나타남 + 소리 →
        // 반짝이와 함께 쓰러짐 → 웅크림 → 서기 (팩의 '쓰러짐' 그림을 거꾸로 — #51 사용자 요청). 빛 효과는 #43 그대로
        private IEnumerator Respawn(string message)
        {
            transitioning = true;
            RespawnsPlayed++;
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(1f, 1f, 0f);
            var home = player.position;
            walker.Face(Direction.Down);
            playerRenderer.sprite = PlayerArt.Pose(PlayerPose.Dead);
            playerRenderer.color = new Color(1f, 1f, 1f, 0f);
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.5f * animationScale);

            // 빛 고리 속에 쓰러진 채로 나타남
            Sound.Play(Sfx.Respawn);
            // 빛 고리는 주인공 뒤에 (쓰러진 모습이 빛에 가리지 않게), 반짝이는 앞에
            StartCoroutine(FieldFx.Play(transform, home, Fx.Heal, 2.6f, animationScale * 1.5f, 9));
            float appear = 0.45f * animationScale;
            for (float t = 0f; t < appear; t += Time.unscaledDeltaTime)
            {
                playerRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, appear)));
                yield return null;
            }
            playerRenderer.color = Color.white;
            StartCoroutine(FieldFx.Play(transform, home + Vector3.up * 0.35f, Fx.Sparkle, 1.9f, animationScale * 1.3f, 12));
            yield return WaitUnscaled(0.35f);

            // 거꾸로 돌린 쓰러짐: 웅크림 → 서기
            playerRenderer.sprite = PlayerArt.Pose(PlayerPose.Crouch);
            SawRespawnPose = true;
            yield return WaitUnscaled(0.25f);
            playerRenderer.sprite = PlayerArt.Get(Direction.Down, 0);
            player.position = home;
            yield return WaitUnscaled(0.35f);
            Destroy(cutscene.gameObject);
            transitioning = false;
            ShowToast(message);
        }

        // ------------------------------------------------------------------ 길 열림 연출

        // 승리 알림을 잠깐 보여 준 뒤, 열린 출입구마다: 어두워짐 → 그 지역의 출입구 앞으로 카메라 이동 →
        // 막힌 덤불이 흔들리며 사라지고 '열렸다!' → 어두워짐 → 원래 자리로
        private IEnumerator RevealGates(List<AreaGate> gates, string victoryMessage)
        {
            transitioning = true;
            revealingGate = true;
            ShowToast(victoryMessage, 30f);
            yield return WaitUnscaled(2.4f);
            HideToast();

            foreach (var gate in gates) yield return RevealGate(gate);

            revealingGate = false;
            transitioning = false;
            var last = gates[gates.Count - 1];
            string target = last.Exit.Target != null ? last.Exit.Target.DisplayName : "새 지역";
            ShowToast($"{last.Area.DisplayName}에서 {UiKit.WithJosa(target, "으로", "로")} 가는 길이 열렸다!", 4f);
        }

        private IEnumerator RevealGate(AreaGate gate)
        {
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);

            // 그 지역을 그려 두고 (이 출입구만 아직 잠긴 모습으로) 카메라를 출입구 조금 앞에서 출발
            hudCanvas.enabled = false;
            player.gameObject.SetActive(false);
            DrawTiles(gate.Area, cell => cell == gate.Cell || IsDoorLocked(gate.Area, cell));
            var gateCenter = CellCenter(gate.Cell);
            var approach = ApproachDirection(gate.Area.Map, gate.Cell);
            // 출입구는 대개 맵 끝에 있으므로 카메라는 출입구보다 몇 칸 안쪽을 비춘다 (맵 밖 빈 곳이 덜 보이게)
            var focus = gateCenter - approach * 3f;
            cameraFocus = focus - approach * 4f;
            cutscene.ShowLetterbox(gate.Area.DisplayName);
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);

            // 카메라가 천천히 출입구로
            float pan = 1.2f * animationScale;
            var from = cameraFocus.Value;
            for (float t = 0; t < pan; t += Time.unscaledDeltaTime)
            {
                cameraFocus = Vector3.Lerp(from, focus, Mathf.SmoothStep(0f, 1f, t / pan));
                yield return null;
            }
            cameraFocus = focus;
            yield return WaitUnscaled(0.4f);

            // 덤불이 흔들리다가 연기와 함께 사라짐 → 열린 출입구
            Sound.PlayJingle(Sfx.GateOpen);
            float shake = 0.6f * animationScale;
            for (float t = 0; t < shake; t += Time.unscaledDeltaTime)
            {
                cameraFocus = focus + new Vector3(Mathf.Sin(t * 70f), Mathf.Cos(t * 55f), 0f) * 0.08f;
                yield return null;
            }
            cameraFocus = focus;
            UpdateCamera();
            StartCoroutine(cutscene.PlaySmoke(cam, gateCenter, animationScale));
            tilemap.SetTile(new Vector3Int(gate.Cell.x, gate.Cell.y, 0), DoorTile(gate.Area, gate.Cell, false));
            string target = gate.Exit.Target != null ? gate.Exit.Target.DisplayName : "새 지역";
            cutscene.ShowBanner($"{UiKit.WithJosa(target, "으로", "로")} 가는 길이 열렸다!");
            lastGateBanner = cutscene.BannerText;
            lastGateCamera = cam.transform.position;
            revealedGates.Add(gate);
            yield return WaitUnscaled(2.2f);

            // 원래 자리로
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);
            cameraFocus = null;
            cutscene.HideLetterbox();
            DrawTiles(area, IsDoorLocked);
            nameTags.SetArea(area, IsDoorLocked);
            RefreshNameTags();
            player.gameObject.SetActive(true);
            hudCanvas.enabled = true;
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);
            Destroy(cutscene.gameObject);
        }

        // 출입구로 걸어 들어가는 방향 (출입구 옆 걸을 수 있는 칸 → 출입구). 카메라가 그 길을 따라 다가간다
        private static Vector3 ApproachDirection(FieldMap map, Vector2Int door)
        {
            foreach (var direction in new[] { Direction.Left, Direction.Down, Direction.Right, Direction.Up })
            {
                var from = door - direction.ToOffset();
                if (map.IsWalkable(from)) return new Vector3(direction.ToOffset().x, direction.ToOffset().y, 0f);
            }
            return Vector3.up;
        }

        // ------------------------------------------------------------------ 수련 (#44)

        // 수련할 단어장: 모든 지역의 단어장 (도감 속 단어). GameDatabase가 없으면(테스트) 지금 지역만
        private List<WordDatabase> TrainingBooks()
        {
            var books = new List<WordDatabase>();
            if (database != null)
                foreach (var candidate in database.Areas)
                    if (candidate != null && candidate.Words != null && !books.Contains(candidate.Words)) books.Add(candidate.Words);
            if (!books.Contains(area.Words)) books.Insert(0, area.Words);
            return books;
        }

        private MonsterSpecies TrainingDummy()
        {
            if (database == null) return null;
            foreach (var monster in database.Monsters)
                if (monster != null && monster.IsTrainingDummy) return monster;
            return null;
        }

        private void OpenTraining()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Training);
            var books = TrainingBooks().ConvertAll(b => b.Words);
            var (discovered, wrong) = TrainingQuizProvider.Count(books, session.Vocabulary);
            trainingView.Show(discovered, wrong);
        }

        private void StartTraining(TrainingMode mode)
        {
            var dummy = TrainingDummy();
            if (dummy == null || inBattle || transitioning)
            {
                ShowToast("수련장을 준비하고 있어요");
                return;
            }
            inBattle = true;
            HideToast();
            Sound.PlayMusic(Music.Battle);
            battle.BeginTraining(dummy, TrainingBooks(), area.Words, mode, OnTrainingFinished, area.Theme);
        }

        private void OnTrainingFinished()
        {
            inBattle = false;
            Sound.PlayMusic(AreaMusic);
            SaveNow();
            RefreshHud();
        }
    }
}
