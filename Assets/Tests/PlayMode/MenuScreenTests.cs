using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 필드 오른쪽 메뉴: 가방(소지품) · 설정(소리·진동·저장 데이터 지우기) — 실제 버튼을 눌러 확인
    public class MenuScreenTests
    {
        //   y=2  #C..#   ← 보물상자 (1,2)
        //   y=1  #.P.#   ← 시작 (2,1)
        private const string Map = "#####\n#C..#\n#.P.#\n#####";

        private ItemData ink, cover, keepsake, potion;
        private RelicData quill, book, lantern;
        private FieldArea area;
        private GameDatabase database;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            ink = TestData.Item("shiny_ink").Set("displayName", "빛나는 잉크").Set("description", "깃펜을 강화하는 잉크.");
            cover = TestData.Item("hard_cover").Set("displayName", "단단한 표지");
            keepsake = TestData.Item("keepsake_meadow").Set("displayName", "네잎클로버 책갈피").Set("kind", ItemKind.Keepsake);
            potion = TestData.Potion("potion", 40).Set("displayName", "상처약");
            var splash = TestData.Skill("ink_splash", SkillKind.Damage, SkillTarget.AllEnemies, 14, QuizDirection.MeaningToEnglish)
                .Set("displayName", "잉크 뿌리기").Set("description", "적 전체에 잉크를 뿌린다.");
            var storm = TestData.Skill("ink_storm", SkillKind.Damage, SkillTarget.AllEnemies, 22, QuizDirection.MeaningToEnglish)
                .Set("displayName", "잉크 폭풍");
            quill = TestData.Relic("relic_quill", splash, new MonsterStats(0, 4, 0), ink, storm)
                .Set("displayName", "깃펜").Set("description", "펜촉이가 남긴 깃펜.").Set("bonusPerLevel", new MonsterStats(0, 2, 0));
            book = TestData.Relic("relic_book", TestData.Skill("shield", SkillKind.Guard, SkillTarget.AllAllies, 4).Set("displayName", "책 방패"),
                new MonsterStats(10, 0, 4), cover).Set("displayName", "백과사전").Set("role", MonsterRole.Defender);
            lantern = TestData.Relic("relic_lantern", TestData.Skill("light", SkillKind.Heal, SkillTarget.AllAllies, 8).Set("displayName", "쪽잠자기"),
                new MonsterStats(15, 0, 0), cover).Set("displayName", "등불");
            var enemySkill = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 5);
            var enemy = TestData.Species("slime", new MonsterStats(20, 5, 5), new MonsterStats(0, 0, 0), enemySkill);

            var words = ScriptableObject.CreateInstance<WordDatabase>()
                .Set("regionId", "meadow").Set("regionName", "초원").Set("completionKeepsake", keepsake).Set("completionGold", 500);
            words.ReplaceWords(TestData.SampleWords());
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "meadow").Set("displayName", "초원").Set("map", Map)
                .Set("encounters", table).Set("words", words);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { enemy }, new[] { ink, cover, keepsake, potion }, new[] { area }, new[] { quill, book, lantern });

            var hero = TestData.Hero(new MonsterStats(60, 14, 10), quill).Set("description", "단어의 힘을 성유물에 담아 싸우는 견습 모험가.");
            session = GameSession.NewGame(hero, 5);
            session.Inventory.Add(ink, 3);
            session.Inventory.Add(cover, 1);
            session.Inventory.Add(keepsake);
            session.Record.MarkRegionClaimed("meadow");
            session.Inventory.AddGold(90);
        }

        private FieldScreen MakeField(GameSettings settings = null, System.Action changed = null, System.Action delete = null)
        {
            var go = new GameObject("MenuUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, gameDatabase: database,
                gameSettings: settings ?? new GameSettings(), onSettingsChanged: changed, onDeleteSave: delete);
            return field;
        }

        [UnityTest]
        public IEnumerator BagShowsMaterialsAndKeepsakeShelf()
        {
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            Assert.AreEqual(Music.Meadow, Sound.CurrentMusic, "초원에 들어가면 초원 음악");

            FindButton(hud, "BagButton").onClick.Invoke();
            var bag = hud.Find("InventoryView").gameObject;
            Assert.IsTrue(bag.activeSelf, "가방 버튼 → 소지품");
            Assert.IsTrue(field.IsPanelOpen, "소지품이 열려 있는 동안은 걷지 않음");
            Assert.IsFalse(bag.transform.Find("DetailBox").gameObject.activeSelf, "처음엔 주인공 탭 (아이템 상세는 숨김)");

            // 아이템 탭: 잉크(선택) · 표지, 나머지는 빈 칸. 징표는 아이템 칸에 안 나옴
            FindButton(bag.transform, "TabItems").onClick.Invoke();
            var text = AllText(bag.transform);
            StringAssert.Contains("보유 골드  90G", text);
            StringAssert.Contains("× 3", text);
            StringAssert.Contains("× 1", text);
            StringAssert.Contains("빛나는 잉크", text);
            StringAssert.Contains("깃펜 +0 → +1", text, "쓰는 곳: 성유물 제단에서 깃펜 강화");
            StringAssert.Contains("지금 강화할 수 있어요", text);
            Assert.IsFalse(FindButton(bag.transform, "ItemSlot_2").interactable, "세 번째 칸은 빈 칸");

            FindButton(bag.transform, "ItemSlot_1").onClick.Invoke();
            StringAssert.Contains("단단한 표지", AllText(bag.transform.Find("DetailBox")));
            StringAssert.Contains("강화하는 것이 없어요", AllText(bag.transform.Find("DetailBox")), "백과사전·등불을 아직 안 모음");

            // 다른 메뉴는 겹쳐 열리지 않음
            FindButton(hud, "SettingsButton").onClick.Invoke();
            Assert.IsFalse(hud.Find("SettingsView").gameObject.activeSelf);

            // 징표 탭: 초원 징표 획득 + 다음 지역 자리
            FindButton(bag.transform, "TabKeepsakes").onClick.Invoke();
            var shelfText = AllText(bag.transform.Find("KeepsakesPage"));
            StringAssert.Contains("네잎클로버 책갈피", shelfText);
            StringAssert.Contains("다음 지역", shelfText);
            StringAssert.Contains("500 골드", AllText(bag.transform.Find("DetailBox")));

            FindButton(bag.transform, "InventoryCloseButton").onClick.Invoke();
            Assert.IsFalse(bag.activeSelf);
            Assert.IsFalse(field.IsPanelOpen);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 가방 > 주인공: 레벨·경험치·능력치(성유물 보너스)·기술(기본 + 성유물). 성유물 탭: 모은 것·??? · 장착/빼기
        [UnityTest]
        public IEnumerator BagHeroAndRelicTabs()
        {
            session.GrantRelic(book);                // 빈 칸에 바로 끼워짐 → 깃펜·백과사전 장착
            session.Hero.TakeDamage(session.Hero.CurrentHp - 20);
            session.Hero.GainExp(10);
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            var bag = hud.Find("InventoryView").gameObject;

            // HUD 주인공 배지 → 가방 > 주인공
            StringAssert.Contains("주인공 Lv5", AllText(hud.Find("HeroStrip")));
            FindButton(hud, "HeroBadge").onClick.Invoke();
            Assert.IsTrue(bag.activeSelf);
            var heroText = AllText(bag.transform.Find("HeroPage"));
            StringAssert.Contains("Lv5 · 성유물 2개 장착", heroText);
            StringAssert.Contains($"20 / {session.Hero.Stats.MaxHp}", heroText, "현재 HP / 최대 HP");
            StringAssert.Contains("(+4)", heroText, "공격·방어에 성유물 보너스");
            StringAssert.Contains("기본 기술 · ", heroText);
            StringAssert.Contains("깃펜 +0 · ", heroText, "성유물 기술은 어느 성유물인지");
            StringAssert.Contains("잉크 뿌리기", heroText);
            StringAssert.Contains("책 방패", heroText);
            StringAssert.Contains("한→영 · 어려움", heroText, "기술의 문제 유형");
            StringAssert.Contains($"다음 레벨까지 {LevelCurve.ExpToNextLevel(5) - 10}", heroText);
            StringAssert.Contains("견습 모험가", heroText);

            // 성유물 탭: 깃펜·백과사전 + 못 찾은 등불은 ???
            FindButton(bag.transform, "TabRelics").onClick.Invoke();
            var relicPage = bag.transform.Find("RelicPage");
            var relicText = AllText(relicPage);
            StringAssert.Contains("모은 성유물  2 / 3", relicText);
            StringAssert.Contains("???", relicText);
            StringAssert.Contains("깃펜  +0", relicText);
            StringAssert.Contains("+3 각성 → 잉크 폭풍", relicText);
            StringAssert.Contains("빼기", relicText);

            // 깃펜 빼기 → 기술·공격이 줄어든다 → 다시 장착
            int attack = session.Hero.Stats.Attack;
            FindButton(relicPage, "RelicActionButton").onClick.Invoke();
            Assert.IsFalse(session.Hero.IsEquipped(session.Hero.Find(quill)));
            Assert.AreEqual(attack - 4, session.Hero.Stats.Attack);
            StringAssert.Contains("장착하기", AllText(relicPage));
            FindButton(relicPage, "RelicActionButton").onClick.Invoke();
            Assert.IsTrue(session.Hero.IsEquipped(session.Hero.Find(quill)));

            // 못 찾은 칸(???)을 누르면 안내
            FindButton(relicPage, "Back").onClick.Invoke(); // 첫 칸 (깃펜)
            var cells = relicPage.Find("Grid");
            FindButton(cells.Find("RelicCell_2"), "Back").onClick.Invoke();
            StringAssert.Contains("아직 찾지 못한 성유물", AllText(relicPage));

            FindButton(bag.transform, "InventoryCloseButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.IsPanelOpen);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 가방 > 아이템: 상처약 [사용하기] — HP가 가득이면 잠금, 다치면 회복 + 개수 감소
        [UnityTest]
        public IEnumerator PotionCanBeUsedFromBag()
        {
            session.Inventory.Add(potion, 2);
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            var bag = hud.Find("InventoryView").gameObject;

            FindButton(hud, "BagButton").onClick.Invoke();
            FindButton(bag.transform, "TabItems").onClick.Invoke();
            int potionSlot = -1;
            for (int i = 0; i < InventoryView.SlotCount; i++)
            {
                FindButton(bag.transform, $"ItemSlot_{i}").onClick.Invoke();
                if (AllText(bag.transform.Find("DetailBox")).Contains("상처약")) { potionSlot = i; break; }
            }
            Assert.GreaterOrEqual(potionSlot, 0, "아이템 칸에 상처약");
            var use = FindButton(bag.transform, "UseItemButton");
            Assert.IsTrue(use.gameObject.activeSelf);
            Assert.IsFalse(use.interactable, "HP가 가득하면 잠금");
            FindButton(bag.transform, "InventoryCloseButton").onClick.Invoke();
            yield return null;

            session.Hero.TakeDamage(50);
            FindButton(hud, "BagButton").onClick.Invoke();
            FindButton(bag.transform, "TabItems").onClick.Invoke();
            FindButton(bag.transform, $"ItemSlot_{potionSlot}").onClick.Invoke();
            int hp = session.Hero.CurrentHp;
            Assert.IsTrue(use.interactable);
            use.onClick.Invoke();
            Assert.AreEqual(hp + 40, session.Hero.CurrentHp);
            Assert.AreEqual(1, session.Inventory.GetCount(potion));
            StringAssert.Contains("HP +40 회복했어요", AllText(bag.transform.Find("DetailBox")));

            FindButton(bag.transform, "InventoryCloseButton").onClick.Invoke();
            yield return null;
            StringAssert.Contains($"HP {session.Hero.CurrentHp}/", AllText(hud.Find("HeroStrip")), "HUD도 갱신");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 기술 칸이 가득일 때 가방 > 아이템의 기술문서로 배우기 → 바꿀 기술 고르기, 빠진 성유물 기술은 성유물 탭에서 다시 넣기
        [UnityTest]
        public IEnumerator SkillDocumentFromBagReplacesASkillAndRelicSkillComesBack()
        {
            var cram = TestData.Skill("cram", SkillKind.Damage, SkillTarget.SingleEnemy, 30).Set("displayName", "벼락치기");
            var doc = TestData.Item("skilldoc_cram").Set("displayName", "기술문서: 벼락치기")
                .Set("kind", ItemKind.SkillDocument).Set("taughtSkill", cram);
            database.ReplaceContents(new MonsterSpecies[0], new[] { ink, cover, keepsake, potion, doc }, new[] { area }, new[] { quill, book, lantern });
            session.GrantRelic(book);
            session.GrantRelic(lantern);
            session.Inventory.Add(doc);
            Assert.IsFalse(session.Hero.HasSkillRoom, "기술 칸 가득 (깃펜·백과사전·등불)");
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            var bag = hud.Find("InventoryView");

            FindButton(hud, "BagButton").onClick.Invoke();
            FindButton(bag, "TabItems").onClick.Invoke();
            bool found = false;
            for (int i = 0; i < InventoryView.SlotCount && !found; i++)
            {
                FindButton(bag, $"ItemSlot_{i}").onClick.Invoke();
                found = AllText(bag.Find("DetailBox")).Contains("기술문서: 벼락치기");
            }
            Assert.IsTrue(found, "아이템 칸에 기술문서");
            var learnButton = FindButton(bag, "UseItemButton");
            StringAssert.Contains("배우기", AllText(learnButton));
            StringAssert.Contains("기술 칸이 가득", AllText(bag.Find("DetailBox")));
            learnButton.onClick.Invoke();
            yield return null;

            var learn = bag.Find("SkillLearnView");
            Assert.IsTrue(learn.gameObject.activeSelf, "기술 배우기 창");
            StringAssert.Contains("바꿀 기술을 고르세요", AllText(learn));
            StringAssert.Contains("책 방패", AllText(learn));
            var confirm = FindButton(learn, "LearnConfirmButton");
            Assert.IsFalse(confirm.interactable, "바꿀 기술을 고르기 전엔 못 누름");
            learn.Find("LearnPanel/Replace_1/Hit").GetComponent<Button>().onClick.Invoke(); // 책 방패
            Assert.IsTrue(confirm.interactable);
            StringAssert.Contains("책 방패 → 벼락치기", AllText(confirm));
            confirm.onClick.Invoke();
            yield return null;

            Assert.IsFalse(learn.gameObject.activeSelf);
            CollectionAssert.Contains(session.Hero.Skills.ToArray(), cram);
            Assert.IsFalse(session.Hero.Skills.Any(s => s.DisplayName == "책 방패"));
            Assert.AreEqual(1, session.Inventory.GetCount(doc), "문서는 남음");
            StringAssert.Contains("배웠어요", AllText(bag.Find("DetailBox")));
            Assert.IsFalse(learnButton.interactable, "이미 배운 기술");

            // 성유물 탭: 백과사전은 끼웠지만 기술이 빠져 있음 → [기술 넣기]로 벼락치기와 바꿔 다시 넣기
            FindButton(bag, "InventoryCloseButton").onClick.Invoke();
            yield return null;
            FindButton(hud, "RelicSlot_1").onClick.Invoke();
            yield return null;
            var relicPage = bag.Find("RelicPage");
            StringAssert.Contains("기술은 기술 칸에 없음", AllText(relicPage));
            var put = FindButton(relicPage, "RelicSkillButton");
            Assert.IsTrue(put.gameObject.activeSelf);
            put.onClick.Invoke();
            yield return null;
            Assert.IsTrue(learn.gameObject.activeSelf);
            StringAssert.Contains("기술 칸에 넣기", AllText(learn));
            learn.Find("LearnPanel/Replace_1/Hit").GetComponent<Button>().onClick.Invoke(); // 벼락치기
            FindButton(learn, "LearnConfirmButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(session.Hero.Skills.Any(s => s.DisplayName == "책 방패"), "다시 넣음");
            CollectionAssert.DoesNotContain(session.Hero.Skills.ToArray(), cram);
            Assert.IsFalse(FindButton(relicPage, "RelicSkillButton").gameObject.activeSelf);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MinimapFollowsPlayerAndOpensFullMap()
        {
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");

            // 미니맵은 맵 데이터 크기대로 (한 칸 = 한 점), 내 위치 점이 시작 칸에
            var texture = field.Minimap.Texture;
            Assert.AreEqual(5, texture.width);
            Assert.AreEqual(4, texture.height);
            Assert.AreEqual((Color)MinimapArt.Chest, (Color)texture.GetPixel(1, 2), "보물상자 점");
            var before = field.Minimap.Picture.PlayerAnchor;

            // 위로 한 칸 걸으면 점도 따라 움직인다
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            Assert.AreEqual(new Vector2Int(2, 2), field.PlayerCell);
            Assert.Greater(field.Minimap.Picture.PlayerAnchor.y, before.y);

            // 미니맵을 누르면 큰 지도
            FindButton(hud, "Minimap").onClick.Invoke();
            var map = hud.Find("MapView").gameObject;
            Assert.IsTrue(map.activeSelf);
            Assert.IsTrue(field.IsPanelOpen, "지도가 열려 있는 동안은 걷지 않음");
            var text = AllText(map.transform);
            StringAssert.Contains("지도 · 초원", text);
            StringAssert.Contains("남은 보물상자 1개", text);
            FindButton(map.transform, "MapCloseButton").onClick.Invoke();
            Assert.IsFalse(map.activeSelf);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 탐험 안개: 멀리 있는 상자는 처음엔 안 보이다가 가까이 걸어가면 미니맵에 나타난다
        [UnityTest]
        public IEnumerator WalkingRevealsFogOnMinimap()
        {
            //   y=1  #P..........C#   ← 시작 (1,1), 상자 (12,1)
            area.Set("map", "##############\n#P..........C#\n##############");
            var field = MakeField();
            yield return null;
            yield return null;
            var chest = new Vector2Int(12, 1);
            Assert.AreEqual((Color)MinimapArt.Fog, (Color)field.Minimap.Texture.GetPixel(chest.x, chest.y), "처음엔 안개");
            Assert.IsFalse(session.World.IsExplored("meadow", chest));

            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 9, 8f);
            Assert.IsTrue(session.World.IsExplored("meadow", chest), "3칸 안으로 다가가면 밝혀짐");
            Assert.AreEqual((Color)MinimapArt.Chest, (Color)field.Minimap.Texture.GetPixel(chest.x, chest.y), "미니맵에 상자가 나타남");

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 안드로이드 뒤로가기: 맨 위 창부터 닫고, 아무 창도 없으면 '게임을 끝낼까요?' (한 번 더 누르면 닫힘)
        [UnityTest]
        public IEnumerator BackButtonClosesTopPanelThenAsksToQuit()
        {
            var field = MakeField();
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            var quit = hud.Find("ConfirmDialog").gameObject;

            FindButton(hud, "BagButton").onClick.Invoke();
            Assert.IsTrue(hud.Find("InventoryView").gameObject.activeSelf);
            field.HandleBack();
            Assert.IsFalse(hud.Find("InventoryView").gameObject.activeSelf, "뒤로가기 → 가방 닫힘");
            Assert.IsFalse(quit.activeSelf, "창을 닫을 때는 끝내기를 묻지 않음");

            FindButton(hud, "SettingsButton").onClick.Invoke();
            field.HandleBack();
            Assert.IsFalse(hud.Find("SettingsView").gameObject.activeSelf, "뒤로가기 → 설정 닫힘");
            Assert.IsFalse(field.IsPanelOpen);

            field.HandleBack();
            Assert.IsTrue(field.IsQuitDialogOpen, "아무 창도 없으면 끝낼지 물음");
            Assert.IsTrue(field.IsPanelOpen, "묻는 동안은 걷지 않음");
            StringAssert.Contains("게임을 끝낼까요?", AllText(quit.transform));
            StringAssert.Contains("자동으로 저장", AllText(quit.transform));

            field.HandleBack();
            Assert.IsFalse(field.IsQuitDialogOpen, "한 번 더 누르면 취소");

            field.HandleBack();
            FindButton(quit.transform, "DialogCancelButton").onClick.Invoke();
            Assert.IsFalse(field.IsQuitDialogOpen, "[취소]로도 닫힘");
            Assert.IsFalse(field.IsPanelOpen);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsSaveChangesAndDeleteNeedsConfirm()
        {
            var settings = new GameSettings();
            int saved = 0, deleted = 0;
            var field = MakeField(settings, () => saved++, () => deleted++);
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");

            FindButton(hud, "SettingsButton").onClick.Invoke();
            var view = hud.Find("SettingsView").gameObject;
            Assert.IsTrue(view.activeSelf, "설정 버튼 → 설정");
            Assert.AreEqual(0, saved, "열기만 해서는 저장하지 않음");

            FindButton(view.transform, "VibrationSwitch").onClick.Invoke();
            Assert.IsFalse(settings.Vibration);
            Assert.AreEqual(1, saved);

            var music = view.GetComponentsInChildren<Slider>(true).First(s => s.name == "MusicSlider");
            Assert.AreEqual(GameSettings.DefaultMusicVolume, music.value, 0.001f, "현재 값으로 열림");
            music.value = 0.2f;
            music.value = 0.3f;
            Assert.AreEqual(0.3f, settings.MusicVolume, 0.001f, "소리는 바로 바뀜");
            Assert.AreEqual(1, saved, "슬라이더를 끄는 동안은 저장하지 않음 (#48)");
            FindButton(view.transform, "SettingsCloseButton").onClick.Invoke();
            Assert.AreEqual(2, saved, "창을 닫을 때 한 번 저장");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            Assert.AreEqual(2, saved);

            // 처음부터 다시 하기: 첫 확인에서 취소하면 아무 일도 없음
            var dialog = view.transform.Find("ConfirmDialog").gameObject;
            FindButton(view.transform, "RestartButton").onClick.Invoke();
            Assert.IsTrue(dialog.activeSelf);
            StringAssert.Contains("처음부터 다시 할까요?", AllText(dialog.transform));
            FindButton(dialog.transform, "DialogCancelButton").onClick.Invoke();
            Assert.IsFalse(dialog.activeSelf);
            Assert.AreEqual(0, deleted);

            // 한 번 확인해도 아직 안 지우고 다시 묻는다 → 두 번째 [지우기]에서 지운다
            FindButton(view.transform, "RestartButton").onClick.Invoke();
            FindButton(dialog.transform, "DialogConfirmButton").onClick.Invoke();
            Assert.AreEqual(0, deleted, "한 번만 눌러서는 안 지움");
            StringAssert.Contains("되돌릴 수 없어요", AllText(dialog.transform));
            FindButton(dialog.transform, "DialogConfirmButton").onClick.Invoke();
            Assert.AreEqual(1, deleted);
            Assert.IsFalse(view.activeSelf, "지운 뒤 설정 창은 닫힘");

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
