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
    // 필드 > 장소: [확인]으로 쓰는 것들 — 보스 방 문·열쇠·사전 받침대·보스 도전·보물상자·기술문서·회복의 샘, 정식판 안내(출입구)
    public partial class FieldScreen
    {
        // 정식판이 필요한 지역(숲)으로 가는 출입구인데 아직 안 샀는지 (#40)
        private bool IsPaywalled(Vector2Int cell)
        {
            if (entitlements == null || entitlements.HasFullVersion) return false;
            var target = area.GetExit(cell)?.Target;
            return target != null && target.RequiresFullVersion;
        }

        // 정식판 안내 (Figma '필드 — 정식판 안내 (#40)'). 사면 바로 그 출입구로 들어갈 수 있다
        private void ShowPaywall(Vector2Int cell)
        {
            paywallArmed = false;
            var target = area.GetExit(cell).Target;
            paywall.Show(store, entitlements, OfferFor(target), () =>
                ShowToast($"정식판이 열렸어요! 이제 {UiKit.WithJosa(target.DisplayName, "으로", "로")} 들어갈 수 있어요.", 3f));
        }

        // 설정 > 정식판 (#63): 정식판이 필요한 지역(지금은 숲)의 안내를 어디서든 띄운다
        private void OpenFullVersionOffer(FieldArea target) =>
            paywall.Show(store, entitlements, OfferFor(target), () =>
                ShowToast($"정식판이 열렸어요! 이제 {UiKit.WithJosa(target.DisplayName, "으로", "로")} 들어갈 수 있어요.", 3f));

        // 정식판이 필요한 지역: 게임 데이터에서 (없으면 지금 지역의 출입구가 이어지는 곳에서)
        private FieldArea FullVersionArea()
        {
            if (database != null)
                foreach (var candidate in database.Areas)
                    if (candidate != null && candidate.RequiresFullVersion) return candidate;
            foreach (var exit in area.Exits)
                if (exit.Target != null && exit.Target.RequiresFullVersion) return exit.Target;
            return null;
        }

        // 안내 문구는 그 지역 데이터에서: 단어 수 · 몬스터·보스 · 성유물(상자·보스 보상)
        private static PaywallOffer OfferFor(FieldArea target)
        {
            var offer = new PaywallOffer { AreaName = target.DisplayName };
            if (target.Words != null && target.Words.Words.Count > 0)
                offer.Lines.Add($"{target.DisplayName} 지역 — 새 영단어 {target.Words.Words.Count}개");
            var species = new HashSet<MonsterSpecies>();
            if (target.Encounters != null)
                foreach (var entry in target.Encounters.Entries)
                    if (entry.Species != null) species.Add(entry.Species);
            var boss = target.Boss;
            offer.Lines.Add(boss != null ? $"새 몬스터 {species.Count}종과 보스 '{boss.Species.DisplayName}'" : $"새 몬스터 {species.Count}종");
            var relics = new List<RelicData>();
            foreach (var chest in target.ChestContents)
                if (chest.Relic != null) relics.Add(chest.Relic);
            if (boss?.RewardRelic != null) relics.Add(boss.RewardRelic);
            if (relics.Count > 0)
                offer.Lines.Add($"{target.DisplayName}의 성유물 {relics.Count}개 ({string.Join("·", relics.ConvertAll(r => r.DisplayName))})");
            offer.Lines.Add("앞으로 추가될 지역도 모두");
            foreach (var relic in relics) offer.Icons.Add(UiKit.RelicIcon(relic));
            return offer;
        }

        // 보스 방 문 (#76, 사용자가 정한 말): 열쇠가 없으면 / 단어가 모자라면 주인공 혼잣말, 다 갖췄으면 문이 열린다
        private void KnockGate()
        {
            switch (BossGate.State(area, session))
            {
                case BossGateState.NeedKey:
                    bubble.Say("열쇠가 필요한 것 같다", 3f);
                    break;
                case BossGateState.NeedWords:
                    bubble.Say("보스를 상대하려면 새로운 단어로 힘을 모아야해", 4f,
                        $"이 지역에서 발견한 단어 {BossGate.WordsFound(area, session)} / {area.BossGateWords}");
                    break;
                case BossGateState.Ready:
                    if (!BossGate.TryOpen(area, session)) return;
                    foreach (var gate in BossGate.Cells(area))
                    {
                        tilemap.SetTile(new Vector3Int(gate.x, gate.y, 0), TileFor(FieldTile.Gate, true));
                        StartCoroutine(FieldFx.Play(transform, CellCenter(gate), Fx.Sparkle, 1.3f, animationScale, 12));
                    }
                    Sound.PlayJingle(Sfx.GateOpen);
                    ShowToast("보스 방 문이 열렸다!", 2.5f);
                    AfterFieldChange();
                    break;
            }
        }

        // 열쇠 받침대 'K' (#76): 처음 [확인]하면 열쇠를 얻고 빈 받침대가 된다
        private void TakeKey(Vector2Int cell)
        {
            if (!BossGate.TakeKey(area, session))
            {
                ShowToast("빈 받침대다.");
                return;
            }
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Key, true));
            Sound.Play(Sfx.Evolve);
            ShowToast($"{UiKit.WithJosa(area.BossKey.DisplayName, "을", "를")} 찾았다!\n보스 방 문을 열 수 있을 것 같다", 3f);
            AfterFieldChange();
        }

        // 문·열쇠처럼 맵 상태가 바뀐 뒤: 미니맵·이름표·목표 한 줄·메뉴 점을 다시 그리고 저장
        private void AfterFieldChange()
        {
            minimap.Redraw();
            RefreshNameTags();
            RefreshObjective();
            RefreshBadges();
            SaveNow();
            RefreshHud();
        }

        // 사전 받침대 (#45): 보스를 물리쳤으면 하루 한 번 읽고 이 지역 단어 하나를 발견.
        // 창 없이 주인공 머리 위 말풍선으로 (#46, 사용자 요청)
        private void ReadDictionary()
        {
            var now = LocalClock();
            var read = BossDictionary.Read(area, session, now, now.ToUniversalTime(), rng);
            switch (read.Outcome)
            {
                case DictionaryReadOutcome.Locked:
                    string boss = area.Boss != null ? UiKit.WithJosa(area.Boss.Species.DisplayName, "을", "를") : "보스를";
                    bubble.Say($"빈 받침대다.\n{boss} 물리치면 무언가 놓일 것 같다.", 4f);
                    break;
                case DictionaryReadOutcome.AlreadyReadToday:
                    bubble.Say("오늘 책은 충분히 읽은 것 같다.", 4f, "내일 다시 읽을 수 있어요 (밤 12시에 바뀜)");
                    break;
                case DictionaryReadOutcome.AllDiscovered:
                    bubble.Say($"{area.DictionaryName}의 단어를 모두 발견했다!", 3f);
                    break;
                case DictionaryReadOutcome.Discovered:
                    StartCoroutine(SpeakDictionaryWord(read.Word));
                    break;
                default:
                    bubble.Say("빈 받침대다.", 3f);
                    break;
            }
        }

        // '.' → '..' → '...' → '!' 풍선 (그동안은 움직일 수 없음) → 새 단어 말풍선 (움직이면 사라짐)
        private IEnumerator SpeakDictionaryWord(WordEntry word)
        {
            IsReadingDictionary = true;
            transitioning = true;
            SaveNow();
            foreach (var step in new[] { SpeechBubble.Icon.Dot1, SpeechBubble.Icon.Dot2, SpeechBubble.Icon.Dot3, SpeechBubble.Icon.Exclaim })
            {
                bubble.ShowIcon(step);
                Sound.Play(Sfx.Click);
                yield return WaitUnscaled(step == SpeechBubble.Icon.Exclaim ? 0.55f : 0.45f);
            }
            var progress = Dex.GetProgress(area.Words, session.Vocabulary);
            Sound.Play(Sfx.NewWord);
            bubble.ShowWord("새로운 단어를 발견했다!", word.English, word.Meaning,
                $"{area.Words.RegionName} 도감 {progress.Discovered} / {progress.Total}", 12f);
            transitioning = false;
            IsReadingDictionary = false;
            var completed = session.ClaimDexRewards(new[] { area.Words });
            if (completed.Count > 0)
            {
                SaveNow();
                ShowToast($"★ {area.Words.RegionName} 도감 완성! 보상을 받았다", 3f);
            }
            RefreshHud();
            interactCooldown = 0.5f;
        }

        private void ChallengeBoss()
        {
            var boss = area.Boss;
            if (boss == null)
            {
                ShowToast("아무도 없다.");
                return;
            }
            string name = boss.Species.DisplayName;
            if (session.World.IsBossDefeated(area.BossId))
            {
                ShowToast($"{UiKit.WithJosa(name, "이", "가")} 있던 자리다.");
                return;
            }
            var enemies = new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) };
            StartCoroutine(Encounter(enemies, $"보스 출현! {name} Lv{boss.Level} — 정답으로 맞서라!", true));
        }

        // 강화·구매·장착 직후 저장하고 HUD(주인공·성유물·골드) 갱신
        private void OnTownChanged()
        {
            SaveNow();
            RefreshHud();
        }

        private void OpenChest(Vector2Int cell)
        {
            var result = session.OpenChest(area, cell);
            if (result.WasEmpty)
            {
                ShowToast("빈 상자다.");
                return;
            }

            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Chest, true));
            Sound.Play(result.Relic != null ? Sfx.Evolve : Sfx.Coin);
            minimap.Redraw();
            if (result.Relic != null)
            {
                ShowToast($"성유물 '{result.Relic.Data.DisplayName}'을(를) 손에 넣었다!\n" + RelicHint(result.Relic), 3.5f);
            }
            else
            {
                bool document = result.Item != null && result.Item.IsSkillDocument;
                string loot = result.Item != null ? (document ? result.Item.DisplayName : $"{result.Item.DisplayName} x{result.Count}") : "";
                if (result.Gold > 0) loot += (loot.Length > 0 ? " + " : "") + $"{result.Gold} 골드";
                ShowToast($"보물상자를 열었다!\n{loot} 획득", 2.5f);
            }
            SaveNow();
            RefreshHud();
            if (result.Item != null && result.Item.IsSkillDocument) OfferDocument(result.Item);
        }

        // 기술문서를 얻으면 바로 배울지 묻는다 (안 배워도 가방 > 아이템에서 언제든)
        private void OfferDocument(ItemData document)
        {
            if (document == null || !document.IsSkillDocument || session.Hero.Knows(document)) return;
            learnView.ShowDocument(session, document, learned =>
            {
                OnTownChanged();
                string name = document.TaughtSkill.DisplayName;
                string josa = UiKit.WithJosa(name, "을", "를").Substring(name.Length);
                ShowToast(learned ? $"새 기술 '{name}'{josa} 배웠다!\n전투에서 바로 쓸 수 있어요"
                    : "기술문서는 가방 > 아이템에서\n언제든 배울 수 있어요", 3f);
            });
        }

        private void UseFountain()
        {
            session.RestoreHero();
            SaveNow();
            RefreshHud();
            Sound.Play(Sfx.Fountain);
            ShowToast("회복의 샘 — HP가 모두 회복되었다!");
        }

        // 새 성유물을 얻었을 때 안내: 빈 칸이 있어 바로 끼웠는지, 가방에서 바꿔 끼워야 하는지
        private string RelicHint(OwnedRelic relic) =>
            session.Hero.IsEquipped(relic) ? $"빈 칸에 끼웠다 — 새 기술 '{relic.Skill.DisplayName}'" : "칸이 가득 — 가방 > 성유물에서 바꿔 끼울 수 있어요";
    }
}
