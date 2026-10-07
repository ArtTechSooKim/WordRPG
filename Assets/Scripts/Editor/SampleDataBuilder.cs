using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.EditorTools
{
    // 샘플 데이터(주인공·성유물·스킬·아이템·적 몬스터·출현표·지역 — 초원·서고·숲) 생성. 이미 있는 에셋은 건드리지 않으므로
    // 인스펙터에서 수치를 고친 뒤 다시 실행해도 안전하다. 배치모드: -executeMethod WordRPG.EditorTools.SampleDataBuilder.Build
    public static class SampleDataBuilder
    {
        private const string Root = "Assets/Data";

        public const string HeroPath = Root + "/Hero/hero.asset";

        [MenuItem("WordRPG/Data/Create Sample Data")]
        public static void Build()
        {
            // --- 아이템: 성유물 강화 재료 (전투·보물상자·보스에서 얻음. 상점에서는 팔지 않음) ---
            var shinyInk = Item("shiny_ink", "빛나는 잉크", "깃펜을 강화하는 반짝이는 잉크.", new Color(0.3f, 0.4f, 1f));
            var hardCover = Item("hard_cover", "단단한 표지", "백과사전·기억의 성배를 강화하는 두꺼운 가죽 표지.", new Color(0.55f, 0.35f, 0.2f));
            var sparkleDust = Item("sparkle_dust", "반짝 가루", "등불·마법 지팡이를 강화하는 빛나는 가루.", new Color(1f, 0.9f, 0.4f));

            // --- 상처약 (상점에서 판매, 필드·전투에서 사용) ---
            var potion = Item("potion", "상처약", "상처에 바르면 HP를 40 회복한다. 필드·전투에서 쓸 수 있다.",
                new Color(0.9f, 0.35f, 0.3f), ItemKind.Consumable, 40);
            var largePotion = Item("potion_large", "큰 상처약", "HP를 120 회복하는 진한 약. 필드·전투에서 쓸 수 있다.",
                new Color(0.85f, 0.2f, 0.3f), ItemKind.Consumable, 120);

            // --- 지역 도감 완성 징표 (지역 특색에 맞는 기념물) ---
            var meadowKeepsake = Item("keepsake_meadow", "네잎클로버 책갈피",
                "초원 도감을 완성한 증표. 행운을 부르는 네잎클로버가 곱게 눌려 있다.", new Color(0.35f, 0.8f, 0.35f), ItemKind.Keepsake);
            var forestKeepsake = Item("keepsake_forest", "도토리 책갈피",
                "숲 도감을 완성한 증표. 반질반질한 도토리가 매달려 있다.", new Color(0.6f, 0.4f, 0.2f), ItemKind.Keepsake);

            // --- 기술: 기본기는 쉬운 영→한, 성유물 기술은 대부분 어려운 한→영 ---
            var swing = Skill("hero_swing", "휘두르기", "들고 있는 사전으로 힘껏 휘두른다.", SkillKind.Damage, SkillTarget.SingleEnemy, 18, QuizDirection.EnglishToMeaning);
            var inkSplash = Skill("nib_ink_splash", "잉크 뿌리기", "적 전체에 잉크를 뿌린다.", SkillKind.Damage, SkillTarget.AllEnemies, 14, QuizDirection.MeaningToEnglish);
            var inkStorm = Skill("quill_ink_storm", "잉크 폭풍", "거센 잉크 폭풍으로 적 전체를 공격한다.", SkillKind.Damage, SkillTarget.AllEnemies, 22, QuizDirection.MeaningToEnglish);
            var bookShield = Skill("shell_book_shield", "책 방패", "두꺼운 책으로 몸을 가려 보호막을 친다.", SkillKind.Guard, SkillTarget.AllAllies, 4, QuizDirection.MeaningToEnglish);
            var encycloWall = Skill("tortoise_encyclo_wall", "백과 방벽", "백과사전을 펼쳐 단단한 방벽을 세운다.", SkillKind.Guard, SkillTarget.AllAllies, 10, QuizDirection.MeaningToEnglish);
            var healingLight = Skill("lumi_healing_light", "쪽잠자기", "잠깐 눈을 붙이고 일어나 HP를 회복한다.", SkillKind.Heal, SkillTarget.AllAllies, 8, QuizDirection.MeaningToEnglish);
            var wisdomLight = Skill("lantern_wisdom_light", "지혜의 빛", "지혜의 빛으로 HP를 크게 회복한다.", SkillKind.Heal, SkillTarget.AllAllies, 14, QuizDirection.MeaningToEnglish);
            var spellBolt = Skill("wand_spell_bolt", "철자 번개", "철자를 정확히 외우면 적 하나에게 번개가 떨어진다.", SkillKind.Damage, SkillTarget.SingleEnemy, 26, QuizDirection.MeaningToEnglish);
            var spellStorm = Skill("wand_thunder_spell", "낙뢰 주문", "긴 주문으로 적 하나에게 거대한 벼락을 떨어뜨린다.", SkillKind.Damage, SkillTarget.SingleEnemy, 34, QuizDirection.MeaningToEnglish);
            var recall = Skill("grail_recall", "되찾은 기억", "잊었던 기억을 되찾아 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 16, QuizDirection.EnglishToMeaning);
            var blessing = Skill("grail_blessing", "기억의 축복", "되찾은 기억이 빛이 되어 HP를 크게 회복한다.", SkillKind.Heal, SkillTarget.Self, 26, QuizDirection.EnglishToMeaning);
            var vineLash = Skill("whip_vine_lash", "덩굴 휘감기", "덩굴 채찍으로 적 전체를 휘감아 조인다.", SkillKind.Damage, SkillTarget.AllEnemies, 18, QuizDirection.MeaningToEnglish);
            var thornStorm = Skill("whip_thorn_storm", "가시덩굴 폭풍", "가시 돋친 덩굴이 소용돌이치며 적 전체를 휩쓴다.", SkillKind.Damage, SkillTarget.AllEnemies, 28, QuizDirection.MeaningToEnglish);
            var timePause = Skill("hourglass_pause", "멈춘 시간", "모래시계를 뒤집어 잠깐 시간을 멈추고 보호막을 친다.", SkillKind.Guard, SkillTarget.AllAllies, 8, QuizDirection.MeaningToEnglish);
            var timeRewind = Skill("hourglass_rewind", "되감은 시간", "시간을 되감아 맞을 공격을 미리 막아 낸다.", SkillKind.Guard, SkillTarget.AllAllies, 14, QuizDirection.MeaningToEnglish);
            var sproutHeal = Skill("leaf_sprout_heal", "새싹 치유", "세계수 잎의 새싹 기운으로 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 22, QuizDirection.MeaningToEnglish);
            var worldBreath = Skill("leaf_world_breath", "세계수의 숨결", "세계수의 숨결이 온몸을 감싸 HP를 크게 회복한다.", SkillKind.Heal, SkillTarget.Self, 34, QuizDirection.MeaningToEnglish);

            var splat = Skill("slime_splat", "끈적 공격", "끈적한 잉크를 튀긴다.", SkillKind.Damage, SkillTarget.SingleEnemy, 14, QuizDirection.EnglishToMeaning);
            var scratch = Skill("bat_scratch", "낙서 할퀴기", "삐뚤빼뚤한 발톱으로 할퀸다.", SkillKind.Damage, SkillTarget.SingleEnemy, 16, QuizDirection.EnglishToMeaning);
            var forgetFog = Skill("goblin_forget_fog", "망각의 안개", "기억을 흐리는 안개로 주인공을 공격한다.", SkillKind.Damage, SkillTarget.AllEnemies, 10, QuizDirection.EnglishToMeaning);
            var memoryDrain = Skill("boss_memory_drain", "기억 흡수", "빼앗은 기억으로 자신의 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 8, QuizDirection.EnglishToMeaning);
            var blankBonk = Skill("goblin_blank_bonk", "깜빡 방망이", "머리를 하얗게 만드는 방망이질.", SkillKind.Damage, SkillTarget.SingleEnemy, 18, QuizDirection.EnglishToMeaning);
            var spore = Skill("mushroom_spell_spore", "철자 포자", "철자를 뒤섞는 포자를 흩뿌린다.", SkillKind.Damage, SkillTarget.AllEnemies, 12, QuizDirection.EnglishToMeaning);
            var squeeze = Skill("snake_squiggle_squeeze", "꼬부랑 조이기", "꼬부랑 글씨처럼 몸을 비틀어 조인다.", SkillKind.Damage, SkillTarget.SingleEnemy, 20, QuizDirection.EnglishToMeaning);
            var peck = Skill("owl_question_peck", "물음표 쪼기", "'그게 뭐더라?' 하며 부리로 쫀다.", SkillKind.Damage, SkillTarget.SingleEnemy, 18, QuizDirection.EnglishToMeaning);
            var muddle = Skill("raccoon_muddle_whirl", "뒤죽박죽 소용돌이", "단어를 뒤죽박죽 섞는 소용돌이를 일으킨다.", SkillKind.Damage, SkillTarget.AllEnemies, 18, QuizDirection.EnglishToMeaning);
            var tailSlam = Skill("raccoon_tail_slam", "꼬리 후려치기", "커다란 꼬리로 힘껏 후려친다.", SkillKind.Damage, SkillTarget.SingleEnemy, 28, QuizDirection.EnglishToMeaning);
            var acornSnack = Skill("raccoon_acorn_snack", "도토리 간식", "숨겨 둔 도토리를 먹고 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 14, QuizDirection.EnglishToMeaning);

            // --- 기술문서로 배우는 기술 (공부 테마). 보스·보물상자에서 문서를 얻는다. 새 기술은 앞으로 더 추가 ---
            var cramBolt = Skill("doc_cram_bolt", "벼락치기", "시험 전날 밤처럼 몰아쳐서 적 하나를 세게 공격한다.", SkillKind.Damage, SkillTarget.SingleEnemy, 30, QuizDirection.MeaningToEnglish);
            var highlightSweep = Skill("doc_highlight_sweep", "형광펜 긋기", "형광펜으로 쭉 그어 적 전체를 공격한다.", SkillKind.Damage, SkillTarget.AllEnemies, 20, QuizDirection.MeaningToEnglish);
            var pencilcaseGuard = Skill("doc_pencilcase_guard", "필통 방패", "단단한 필통으로 몸을 가려 보호막을 친다.", SkillKind.Guard, SkillTarget.AllAllies, 10, QuizDirection.EnglishToMeaning);
            var cramDoc = Item("skilldoc_cram", "기술문서: 벼락치기", "읽으면 기술 '벼락치기'를 배운다. 배워도 사라지지 않아 언제든 다시 배울 수 있다.",
                new Color(0.95f, 0.85f, 0.3f), ItemKind.SkillDocument, taughtSkill: cramBolt);
            var highlightDoc = Item("skilldoc_highlight", "기술문서: 형광펜 긋기", "읽으면 기술 '형광펜 긋기'를 배운다. 배워도 사라지지 않아 언제든 다시 배울 수 있다.",
                new Color(0.5f, 0.85f, 0.35f), ItemKind.SkillDocument, taughtSkill: highlightSweep);
            var pencilcaseDoc = Item("skilldoc_pencilcase", "기술문서: 필통 방패", "읽으면 기술 '필통 방패'를 배운다. 배워도 사라지지 않아 언제든 다시 배울 수 있다.",
                new Color(0.75f, 0.55f, 0.4f), ItemKind.SkillDocument, taughtSkill: pencilcaseGuard);

            // --- 성유물 (예전 아군 몬스터 3마리 → 깃펜·백과사전·등불, 그리고 새로 찾는 마법 지팡이·기억의 성배) ---
            var quill = Relic("relic_quill", "깃펜", "펜촉이가 남긴 깃펜. 잉크를 뿌려 적을 한꺼번에 공격한다.", MonsterRole.Attacker,
                new Color(0.25f, 0.45f, 0.95f), inkSplash, inkStorm, new MonsterStats(0, 4, 0), new MonsterStats(0, 2, 0), shinyInk);
            var book = Relic("relic_book", "백과사전", "책껍질이 지고 다니던 두꺼운 사전. 펼치면 단단한 방패가 된다.", MonsterRole.Defender,
                new Color(0.6f, 0.4f, 0.2f), bookShield, encycloWall, new MonsterStats(10, 0, 4), new MonsterStats(4, 0, 1), hardCover);
            var lantern = Relic("relic_lantern", "등불", "등불이가 남긴 작은 등불. 은은한 불빛 아래 잠깐 눈을 붙이면 기운이 돌아온다.", MonsterRole.Supporter,
                new Color(1f, 0.85f, 0.3f), healingLight, wisdomLight, new MonsterStats(15, 0, 0), new MonsterStats(5, 0, 0), sparkleDust);
            var wand = Relic("relic_wand", "마법 지팡이", "철자를 정확히 외우면 번개가 떨어지는 지팡이. 서고 깊은 곳에 잠들어 있었다.", MonsterRole.Attacker,
                new Color(0.55f, 0.45f, 0.95f), spellBolt, spellStorm, new MonsterStats(0, 6, 0), new MonsterStats(0, 2, 0), sparkleDust);
            var grail = Relic("relic_grail", "기억의 성배", "까먹대왕이 삼켰던 기억이 담긴 잔. 잊었던 힘을 되찾아 준다.", MonsterRole.Supporter,
                new Color(1f, 0.75f, 0.2f), recall, blessing, new MonsterStats(20, 0, 2), new MonsterStats(5, 0, 1), hardCover);
            // 숲 (두 번째 지역): 상자 2개 + 보스 보상
            var whip = Relic("relic_whip", "덩굴 채찍", "숲의 덩굴을 엮어 만든 채찍. 단어를 정확히 외우면 적 전체를 휘감는다.", MonsterRole.Attacker,
                new Color(0.35f, 0.65f, 0.3f), vineLash, thornStorm, new MonsterStats(0, 6, 0), new MonsterStats(0, 2, 0), shinyInk);
            var hourglass = Relic("relic_hourglass", "시간의 모래시계", "뒤집으면 잠깐 시간이 멈추는 모래시계. 버섯 공터 깊은 곳에 묻혀 있었다.", MonsterRole.Defender,
                new Color(0.85f, 0.65f, 0.35f), timePause, timeRewind, new MonsterStats(15, 0, 5), new MonsterStats(5, 0, 2), hardCover);
            var leaf = Relic("relic_leaf", "세계수 잎", "헷갈너구리가 품고 있던 세계수의 잎. 숲의 생명력이 담겨 있다.", MonsterRole.Supporter,
                new Color(0.45f, 0.8f, 0.35f), sproutHeal, worldBreath, new MonsterStats(30, 2, 2), new MonsterStats(6, 1, 1), sparkleDust);

            // --- 주인공: 혼자 싸운다. 시작 성유물은 깃펜 하나 ---
            HeroAsset(swing, quill);

            // --- 적 몬스터 ---
            var inkSlime = Monster("ink_slime", "잉크 슬라임", "쏟아진 잉크가 뭉쳐 생긴 슬라임.", MonsterRole.Attacker,
                new Color(0.35f, 0.2f, 0.5f), new MonsterStats(22, 9, 8), new MonsterStats(5, 2, 2), new[] { splat },
                enemyExp: 6, enemyGold: 5, drops: new[] { new ItemDrop(shinyInk, 0.4f), new ItemDrop(hardCover, 0.15f) });
            var scribbleBat = Monster("scribble_bat", "낙서 박쥐", "공책 귀퉁이 낙서에서 태어난 박쥐.", MonsterRole.Attacker,
                new Color(0.5f, 0.5f, 0.55f), new MonsterStats(18, 11, 6), new MonsterStats(4, 2, 1), new[] { scratch },
                enemyExp: 7, enemyGold: 6, drops: new[] { new ItemDrop(sparkleDust, 0.4f), new ItemDrop(shinyInk, 0.15f) });
            var forgetGoblin = Monster("forget_goblin", "까먹깨비", "외운 단어를 까먹게 만드는 도깨비. 던전 깊은 곳에 산다.", MonsterRole.Defender,
                new Color(0.2f, 0.7f, 0.6f), new MonsterStats(40, 12, 10), new MonsterStats(8, 3, 2), new[] { forgetFog, blankBonk },
                enemyExp: 15, enemyGold: 20, drops: new[] { new ItemDrop(hardCover, 0.6f), new ItemDrop(sparkleDust, 0.3f) });

            // --- 보스: 잊혀진 서고 꼭대기의 까먹대왕. 강화 재료 3종 + 성유물 '기억의 성배' ---
            var forgetKing = Monster("boss_forget_king", "까먹대왕", "까먹깨비들의 우두머리. 서고의 기억을 몽땅 먹어 치우고 있다.", MonsterRole.Attacker,
                new Color(0.15f, 0.6f, 0.55f), new MonsterStats(50, 14, 12), new MonsterStats(8, 3, 2), new[] { forgetFog, blankBonk, memoryDrain },
                enemyExp: 25, enemyGold: 30,
                drops: new[] { new ItemDrop(shinyInk, 1f), new ItemDrop(hardCover, 1f), new ItemDrop(sparkleDust, 1f) });

            // --- 숲의 적 (두 번째 지역, Lv7~10) + 보스 헷갈너구리 ---
            var spellMushroom = Monster("spell_mushroom", "철자버섯", "철자를 뒤섞는 포자를 뿜는 버섯. 숲 그늘에 무리 지어 산다.", MonsterRole.Attacker,
                new Color(0.85f, 0.3f, 0.3f), new MonsterStats(26, 12, 9), new MonsterStats(5, 2, 2), new[] { spore },
                enemyExp: 10, enemyGold: 9, drops: new[] { new ItemDrop(shinyInk, 0.35f), new ItemDrop(sparkleDust, 0.2f) });
            var squiggleSnake = Monster("squiggle_snake", "꼬부랑뱀", "몸을 꼬부랑 글씨처럼 비틀어 읽는 사람을 헷갈리게 하는 뱀.", MonsterRole.Attacker,
                new Color(0.4f, 0.65f, 0.3f), new MonsterStats(24, 14, 8), new MonsterStats(5, 3, 1), new[] { squeeze },
                enemyExp: 11, enemyGold: 10, drops: new[] { new ItemDrop(hardCover, 0.35f), new ItemDrop(shinyInk, 0.2f) });
            var questionOwl = Monster("question_owl", "물음표부엉이", "무엇이든 '그게 뭐더라?' 하고 되묻는 부엉이. 밤낮없이 숲을 지킨다.", MonsterRole.Defender,
                new Color(0.85f, 0.5f, 0.25f), new MonsterStats(34, 12, 12), new MonsterStats(6, 2, 2), new[] { peck },
                enemyExp: 13, enemyGold: 12, drops: new[] { new ItemDrop(sparkleDust, 0.4f), new ItemDrop(hardCover, 0.2f) });
            var muddleRaccoon = Monster("boss_muddle_raccoon", "헷갈너구리", "숲의 단어를 뒤죽박죽 섞어 버리는 커다란 너구리. 숲 깊은 공터에 산다.", MonsterRole.Attacker,
                new Color(0.8f, 0.35f, 0.25f), new MonsterStats(70, 16, 14), new MonsterStats(9, 3, 2), new[] { muddle, tailSlam, acornSnack },
                enemyExp: 45, enemyGold: 60,
                drops: new[] { new ItemDrop(shinyInk, 1f, 2), new ItemDrop(hardCover, 1f, 2), new ItemDrop(sparkleDust, 1f, 2) });

            // --- 수련 (#44): 허수아비. 기술이 없어 공격하지 않고, 맞아도 HP가 줄지 않는다. 레벨 = 주인공 레벨 ---
            Monster("training_scarecrow", "허수아비", "수련장의 허수아비. 아무리 때려도 끄떡없어서 기술을 연습하기 좋다.", MonsterRole.Defender,
                new Color(0.85f, 0.7f, 0.35f), new MonsterStats(30, 10, 10), new MonsterStats(4, 2, 2), new SkillData[0],
                enemyExp: 0, enemyGold: 0, trainingDummy: true);

            // --- 출현표 ---
            var meadowEncounters = Encounters("meadow_field", 1, 2,
                new EncounterTable.Entry(inkSlime, 1, 3, 10),
                new EncounterTable.Entry(scribbleBat, 1, 3, 8));
            var dungeonEncounters = Encounters("word_dungeon", 1, 3,
                new EncounterTable.Entry(forgetGoblin, 3, 5, 5),
                new EncounterTable.Entry(inkSlime, 2, 4, 5),
                new EncounterTable.Entry(scribbleBat, 2, 4, 5));
            var forestEncounters = Encounters("forest_field", 1, 3,
                new EncounterTable.Entry(spellMushroom, 7, 9, 10),
                new EncounterTable.Entry(squiggleSnake, 7, 10, 8),
                new EncounterTable.Entry(questionOwl, 8, 10, 6));

            // 단어장(CSV에서 만들어짐)에 지역 정보와 도감 완성 보상 지정. 이미 지정돼 있으면 건드리지 않음
            WordCsvImporter.ImportAll();
            ConfigureRegion("Assets/Data/Words/tier1_meadow.asset", "meadow", "초원", meadowKeepsake, 500);
            ConfigureRegion("Assets/Data/Words/tier2_forest.asset", "forest", "숲", forestKeepsake, 700);

            // --- 필드: 초원. 보물상자는 맵의 C를 위→아래, 왼→오른 순으로 대응 (강화 재료·성유물을 얻는 곳) ---
            Area("meadow", "초원", MeadowMap, meadowEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>("Assets/Data/Words/tier1_meadow.asset"),
                new ChestContent(sparkleDust, 2),        // 북서: 오래된 숲 풀숲 오솔길 끝
                new ChestContent(hardCover, 2),          // 북동: 작은 못 옆 풀숲
                new ChestContent(null, 0, 50, book),     // 호수 곶 끝 — 두 번째 성유물
                new ChestContent(pencilcaseDoc, 1, 30),  // 서쪽 덤불 속 숨은 상자 — 기술문서: 필통 방패
                new ChestContent(shinyInk, 2),           // 남동: 나무 고리 안 숨은 공터
                new ChestContent(potion, 2));            // 마을 동쪽 길 아래 풀숲

            // --- 마을 상점: 상처약 (강화 재료는 팔지 않음 — 사용자 결정, 진화가 너무 쉬워서) ---
            var meadowShop = ShopAsset("meadow_shop", "초원 마을 잡화점",
                new ShopEntry(potion, 30), new ShopEntry(largePotion, 90));
            AssignShopIfEmpty("Assets/Data/Areas/meadow.asset", meadowShop);

            // --- 던전: 잊혀진 서고 (초원 북쪽 동굴 입구로 연결). MVP는 난이도 하나라 단어장은 초원과 같음 ---
            Area("library", "잊혀진 서고", LibraryMap, dungeonEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>("Assets/Data/Words/tier1_meadow.asset"),
                new ChestContent(null, 0, 120),          // 보스 앞 회랑 왼쪽 구석
                new ChestContent(null, 0, 100, lantern), // 서쪽 열람실
                new ChestContent(null, 0, 80, wand),     // 동쪽 잉크 서고
                new ChestContent(largePotion, 1),        // 샘의 방 옆 벽감
                new ChestContent(shinyInk, 3));          // 아래 서가 막다른 곳
            ConfigureAreaIfNew("Assets/Data/Areas/library.asset", so =>
            {
                Prop(so, "theme").enumValueIndex = (int)FieldTheme.Library;
                Prop(so, "encounterRate").floatValue = 0.14f;
                var boss = Prop(so, "boss");
                boss.FindPropertyRelative("species").objectReferenceValue = forgetKing;
                boss.FindPropertyRelative("level").intValue = 7;
                boss.FindPropertyRelative("rewardRelic").objectReferenceValue = grail;
                boss.FindPropertyRelative("rewardItem").objectReferenceValue = cramDoc;
            });
            // --- 두 번째 지역: 숲 (2단계 단어). 초원 마을 동쪽 출입구 — 서고 보스를 물리치면 열린다 ---
            Area("forest", "숲", ForestMap, forestEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>("Assets/Data/Words/tier2_forest.asset"),
                new ChestContent(null, 0, 60, whip),     // 부엉이 골짜기 북서 끝 — 덩굴 채찍
                new ChestContent(hardCover, 3),          // 골짜기 위 풀숲
                new ChestContent(shinyInk, 3),           // 개울 건너 징검다리
                new ChestContent(sparkleDust, 3),        // 늪 한가운데
                new ChestContent(null, 0, 60, hourglass),// 버섯 공터 — 시간의 모래시계
                new ChestContent(null, 0, 150));         // 버섯 공터 아래 구석
            var forestShop = ShopAsset("forest_shop", "숲속 쉼터 가게",
                new ShopEntry(potion, 30), new ShopEntry(largePotion, 90));
            AssignShopIfEmpty("Assets/Data/Areas/forest.asset", forestShop);
            SetFullVersionArea("Assets/Data/Areas/forest.asset"); // 숲부터는 정식판 (#40)
            // 보스의 사전 (#45): 보스를 물리치면 그 맵 쉼터의 받침대(L)에 놓인다 — 서고는 샘의 방, 숲은 입구 야영지
            SetDictionary("Assets/Data/Areas/library.asset", "서고의 사전");
            SetDictionary("Assets/Data/Areas/forest.asset", "숲의 사전");
            ConfigureAreaIfNew("Assets/Data/Areas/forest.asset", so =>
            {
                Prop(so, "theme").enumValueIndex = (int)FieldTheme.Forest;
                Prop(so, "encounterRate").floatValue = 0.13f;
                var boss = Prop(so, "boss");
                boss.FindPropertyRelative("species").objectReferenceValue = muddleRaccoon;
                boss.FindPropertyRelative("level").intValue = 12;
                boss.FindPropertyRelative("rewardRelic").objectReferenceValue = leaf;
                boss.FindPropertyRelative("rewardItem").objectReferenceValue = highlightDoc;
            });

            // --- 출입구 연결 (맵의 D를 위→아래, 왼→오른 순): 초원 0 = 서고, 초원 1 = 숲(서고 보스를 물리치면 열림) ---
            const string meadowPath = "Assets/Data/Areas/meadow.asset", libraryPath = "Assets/Data/Areas/library.asset",
                forestPath = "Assets/Data/Areas/forest.asset";
            SetExitsIfMismatch(meadowPath, (libraryPath, 0, null), (forestPath, 0, libraryPath));
            SetExitsIfMismatch(libraryPath, (meadowPath, 0, null));
            SetExitsIfMismatch(forestPath, (meadowPath, 1, null));

            AssetDatabase.SaveAssets();
            GameDatabaseBuilder.Refresh();
            Debug.Log("[WordRPG] 샘플 데이터 생성 완료 (기존 에셋은 유지)");
        }

        // . 길  : 잔디(조우 없음)  , 풀숲(조우)  # 나무  ~ 물  F 회복의 샘  C 보물상자  E 성유물 제단  S 상점  P 시작 위치  D 출입구
        // 38×46. 아래 마을에서 출발 → 남쪽 들판(풀숲 띠) → 강의 여울 두 곳 → 호수 언덕(곶 끝 상자) → 북쪽 오래된 숲의 풀숲 오솔길
        // → 맨 위 동굴(서고). 마을 동쪽 길 끝 출입구는 숲으로 — 서고 보스를 물리치기 전엔 덤불로 막혀 있다
        public const string MeadowMap =
                "##################D###################\n" +
                "##################.###################\n" +
                "##########,,C###::.::#################\n" +
                "####,,,,,,,,,##:::.:::####,,,,,,,,,,##\n" +
                "####,##########:::.:::####,##~~~#,,,##\n" +
                "####,::#####,###::.::#####,#~~~~~,,,##\n" +
                "####,::#####,#####,#######,##~~~#,,,##\n" +
                "####,#######,#####,#######,######,,C##\n" +
                "####,,,,,,,,,#####,,,,,,,,,,,,,,######\n" +
                "########,#########,############,######\n" +
                "########,#########,############,######\n" +
                "#:::::#:.:#:::::::.::::::#####:.:::::#\n" +
                "#:::::#:.::::.###.........:::::.::#::#\n" +
                "#::#::::.::::.:::::::::::.......:::::#\n" +
                "#:###:::......:::::::~~~~~:::::.:,,,:#\n" +
                "#:###:::,,,,,::::::~~~~~~~~~:::.,,,,,#\n" +
                "#:###:,,,,,,,,,:::~~~~~~~~~~~::.,,,,,#\n" +
                "#::#::,,,,,,,,,:::~~~~~C~~~~~::.,,,,,#\n" +
                "##C##:,,,,,,,,,:::~~~~~.~~~~~::.,,,,,#\n" +
                "##,,#:::,,,,,::::::~~~~.~~~~:::.,,,,,#\n" +
                "##,,,,::.::::::::::::~~.~~::#::.:,,,:#\n" +
                "#####:::.::::::::#::.......:#::.:::::#\n" +
                "#~~~~~::.:::~~~~~~::::::~~~~~~:.::::~#\n" +
                "#~~~~~~~.~~~~~~~~~~~~~~~~~~~~~~.~~~~~#\n" +
                "#:::::~~.~~~::::::~~~~~~::::::~.~~~~:#\n" +
                "#::,,,,,:::::#####::::::::::::..:::::#\n" +
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,:::::::#\n" +
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,:::::::#\n" +
                "#:,,,,,,,:::::::::::.::,,,,,::###,####\n" +
                "#::,,,,,.............:,,,,,,,:##,,,,##\n" +
                "#:::::::::::.:,,,,,::::,,,,,::##,,,,##\n" +
                "#:#:#:::::::.,,,,,,,::::::::::##,,,,##\n" +
                "#::#::::::::.:,,,,,::::::#::::##,,,C##\n" +
                "#:::::::::::.:::::::::::::#:::########\n" +
                "############.############::::::::::::#\n" +
                "#:::::::::::.:::::::::::#::,,,,,,,:::#\n" +
                "#:#::#.............#:#::#:,,,,,,,,,::#\n" +
                "#::::...E...F...S...::::#::,,,,,,,:::#\n" +
                "#::#:...............::::#::::::::::::#\n" +
                "#:#::................................D\n" +
                "#::::.......P........................#\n" +
                "#::::...............::::#::,:::::::::#\n" +
                "#:~~:#.............#::::#:#,,,,,,,,###\n" +
                "#:~~::::::::::::::::::#:#:#,,,,,,,,,##\n" +
                "#:::::::::::::::::::::::#:##,,,,,,,C##\n" +
                "######################################\n";

        private static void Area(string id, string name, string map, EncounterTable encounters, WordDatabase words,
            params ChestContent[] chests)
        {
            CreateIfMissing<FieldArea>($"{Root}/Areas/{id}.asset", so =>
            {
                Prop(so, "areaId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "map").stringValue = map;
                Prop(so, "encounters").objectReferenceValue = encounters;
                Prop(so, "words").objectReferenceValue = words;
                var list = Prop(so, "chests");
                list.arraySize = chests.Length;
                for (int i = 0; i < chests.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("item").objectReferenceValue = chests[i].Item;
                    element.FindPropertyRelative("count").intValue = chests[i].Count;
                    element.FindPropertyRelative("gold").intValue = chests[i].Gold;
                    element.FindPropertyRelative("relic").objectReferenceValue = chests[i].Relic;
                }
            });
        }

        private static ShopData ShopAsset(string id, string name, params ShopEntry[] entries)
        {
            return CreateIfMissing<ShopData>($"{Root}/Shops/{id}.asset", so =>
            {
                Prop(so, "displayName").stringValue = name;
                var list = Prop(so, "entries");
                list.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("item").objectReferenceValue = entries[i].Item;
                    element.FindPropertyRelative("price").intValue = entries[i].Price;
                }
            });
        }

        // 이미 있는 지역 에셋에 상점이 비어 있을 때만 연결 (인스펙터에서 바꾼 값은 유지)
        private static void AssignShopIfEmpty(string areaPath, ShopData shop)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.Shop != null) return;
            var so = new SerializedObject(area);
            Prop(so, "shop").objectReferenceValue = shop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // . 돌바닥  : 열람실 카펫(조우 없음)  , 흩어진 종이(조우)  # 책장  ~ 잉크 웅덩이  F 회복의 샘  C 보물상자  B 보스  D 출입구(초원으로)
        // 34×48. 아래 입구 홀 → 책장 줄 사이 서가(막다른 곳 상자) → 샘의 방(옆 벽감) → 서쪽 열람실·동쪽 잉크 서고 → 회랑 → 꼭대기 보스 방
        public const string LibraryMap =
                "##################################\n" +
                "#############.........############\n" +
                "############..:::B:::..###########\n" +
                "############.~:::::::~.###########\n" +
                "############.~:::::::~.###########\n" +
                "############...........###########\n" +
                "####C########.........############\n" +
                "####,,,#########...###############\n" +
                "####,,,,,,,,,,,,,,,,,,,,,,,,,,####\n" +
                "####,,,,,,##,,,,,,,,,,##,,,,,,####\n" +
                "####,,,,,,,,,,,,,,,,,,,,,,,,,,####\n" +
                "################,,,###############\n" +
                "################...###############\n" +
                "##C,::::::::::##...#,,,,,,,,~~~~##\n" +
                "##,,::::::::::##...#,,,,,,,,~~~~##\n" +
                "##::#:::#:::#:##...#,,,~~~,,~~~~##\n" +
                "##::#:::#:::#:##...#,,~~~~~,,,,,##\n" +
                "##:::::::::::,,,,.,,,,~~~~~,,,,C##\n" +
                "##::::::::::::##...#,,~~~~~,,,,,##\n" +
                "##::#:::#:::#:##...#,,,~~~,,~~~,##\n" +
                "##::#:::#:::#:##...#,,,,,,,,~~~,##\n" +
                "##::::::::::::##...#,,,,,,,,~~~,##\n" +
                "#####.##########...#,,,,,,,,,,,,##\n" +
                "#####.##########...######,,#######\n" +
                "#####.####::::::::::::::#,,#######\n" +
                "#####.###::::::::F:::::::###.C####\n" +
                "#####....::::::::::::::::....#####\n" +
                "#########:::::::::::L::::###.#####\n" +
                "##########::::::...:::::##########\n" +
                "##C,,,,,,,#,,,,,...,,,,,#,,,,,,,##\n" +
                "###,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "######,####,###############,######\n" +
                "##,,,,,,,,,,,,,~~~~~~~,,,,,,,,,,##\n" +
                "##,,,,,,,,,,,,,~~~~~~~,,,,,,,,,,##\n" +
                "###,########,#########,#######,###\n" +
                "##,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "##,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "########,#######...######,########\n" +
                "##~~,,,,,,,,,,,,...,,,,,,,,,,~~~##\n" +
                "##~~,,,,,,,,,,,,...,,,,,,,,,,~~~##\n" +
                "################...###############\n" +
                "###########............###########\n" +
                "##########...::::::::...##########\n" +
                "##########...::::::::...##########\n" +
                "##########...::::::::...##########\n" +
                "##########.......P......##########\n" +
                "###########............###########\n" +
                "#################D################\n";

        // . 흙길  : 이끼 땅(조우 없음)  , 고사리(조우)  # 짙은 덤불  ~ 늪  F 회복의 샘  C 보물상자  E 성유물 제단  S 상점  B 보스  D 출입구(초원으로)
        // 36×44. 왼쪽 아래 출입구 옆 야영지(샘·상점·제단) → 굽이진 숲길(고사리 덤불) → 늪 · 부엉이 골짜기 · 버섯 공터 · 개울
        // → 오른쪽 위 공터의 보스 헷갈너구리
        public const string ForestMap =
                "####################################\n" +
                "###########################:::::####\n" +
                "#########################::::B::::##\n" +
                "##C,,,,#################::.......::#\n" +
                "##,,,,,##################:.......:##\n" +
                "##,,,,,##,,,,,C####:::##::.......::#\n" +
                "##,,,,,##,,,,,,###:::::##:.......:##\n" +
                "##,,,,,##,,,,,,####:::#####:::::####\n" +
                "##,,,,,###,#:::::###:########.###~~#\n" +
                "##,,,,,,,,,:::::::##:::::.....###~~#\n" +
                "##,,#,,,#,,:::::::######,,,######~~#\n" +
                "###,,,,,,,,:::::::######,,,#####,~~#\n" +
                "##,,,,####,,:::::#######,,,###,,,,C#\n" +
                "##,,,#####,,,#:#########,,,###,#,~~#\n" +
                "##,,,#####,,,#:#########,,,###,##~~#\n" +
                "##,,,#####,,,############......##~~#\n" +
                "##,##,###,,,,##::::~~~###:::#,,,#~~#\n" +
                "##,,,,,,,,,,,::::::~~~##:::::,,,#~~#\n" +
                "##,,#,,,,,,,,:~~~::~~~##::::::,,#~~#\n" +
                "##,,,,,,,,,#:~~~~~:::::#:::::,,,#~~#\n" +
                "##,,,,#,,,,#::~~~~:#:::##:::#,,,#~~#\n" +
                "####.#######:::::C:::::######,,,#~~#\n" +
                "####.##:::::#::#::~~~:#####....##~~#\n" +
                "####.#:::::::::::~~~~~####,,,####~~#\n" +
                "####:::::::::##:::~~~#####,,,####~~#\n" +
                "####.#:::::::####:########,,,####~~#\n" +
                "####.##:::::#####:########,,,####~~#\n" +
                "####.############:########,,,#######\n" +
                "####.....########::::::.....########\n" +
                "########.############,,,############\n" +
                "########.############,,,############\n" +
                "########.############,,,######,,,C##\n" +
                "########.############,,,#####,,,,,##\n" +
                "#######...#######......#####,,,,,,,#\n" +
                "######:...:#####,,,#########,,,,,,,#\n" +
                "#####:E:F:S:####,,,#########,,,,,,,#\n" +
                "###::.......::##,,,##########,,,,,##\n" +
                "#..::.............############,,,###\n" +
                "D..::P......::###.#,,,,#,,#,,#,,,,##\n" +
                "####::::L::::####.#,,,,,,,,,,,,,,###\n" +
                "######:::::######..............,,,##\n" +
                "###################,,,,,,,,,,,,,,,##\n" +
                "###################,,#,,,,#,,,,,,C##\n" +
                "####################################\n";

        // 새로 만든 지역에만 추가 설정 (보스가 아직 없을 때 = 처음 만들 때). 인스펙터에서 바꾼 값은 유지
        // 정식판을 사야 들어가는 지역 표시 (이미 켜져 있으면 그대로)
        private static void SetFullVersionArea(string areaPath)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.RequiresFullVersion) return;
            var so = new SerializedObject(area);
            Prop(so, "requiresFullVersion").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 보스의 사전 이름 (비어 있을 때만)
        private static void SetDictionary(string areaPath, string name)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.HasDictionary) return;
            var so = new SerializedObject(area);
            Prop(so, "dictionaryName").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAreaIfNew(string areaPath, Action<SerializedObject> configure)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.Boss != null) return;
            var so = new SerializedObject(area);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 출입구 연결: (도착 지역 경로, 도착 지역의 몇 번째 D, 이 지역의 보스를 물리쳐야 열림(없으면 null)).
        // 맵의 출입구 수와 맞고 지금 연결 수와 다를 때만 (인스펙터에서 고친 연결은 유지)
        public static void SetExitsIfMismatch(string areaPath, params (string target, int door, string openedByBossOf)[] exits)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.Map.Doors.Count != exits.Length || area.Exits.Count == exits.Length) return;
            var so = new SerializedObject(area);
            var list = Prop(so, "exits");
            list.arraySize = exits.Length;
            for (int i = 0; i < exits.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("target").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FieldArea>(exits[i].target);
                element.FindPropertyRelative("targetDoorIndex").intValue = exits[i].door;
                element.FindPropertyRelative("openedByBossOf").objectReferenceValue =
                    exits[i].openedByBossOf != null ? AssetDatabase.LoadAssetAtPath<FieldArea>(exits[i].openedByBossOf) : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRegion(string wordBookPath, string regionId, string regionName, ItemData keepsake, int gold)
        {
            var book = AssetDatabase.LoadAssetAtPath<WordDatabase>(wordBookPath);
            if (book == null)
            {
                Debug.LogWarning($"[WordRPG] 단어장이 없어 지역 설정을 건너뜀: {wordBookPath}");
                return;
            }
            if (!string.IsNullOrEmpty(book.RegionId)) return;

            var so = new SerializedObject(book);
            Prop(so, "regionId").stringValue = regionId;
            Prop(so, "regionName").stringValue = regionName;
            Prop(so, "completionKeepsake").objectReferenceValue = keepsake;
            Prop(so, "completionGold").intValue = gold;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ItemData Item(string id, string name, string description, Color color,
            ItemKind kind = ItemKind.Material, int healAmount = 0, SkillData taughtSkill = null)
        {
            return CreateIfMissing<ItemData>($"{Root}/Items/{id}.asset", so =>
            {
                Prop(so, "itemId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "description").stringValue = description;
                Prop(so, "kind").enumValueIndex = (int)kind;
                Prop(so, "placeholderColor").colorValue = color;
                Prop(so, "healAmount").intValue = healAmount;
                Prop(so, "taughtSkill").objectReferenceValue = taughtSkill;
            });
        }

        // 강화 비용 (+0→+1 … +4→+5): 재료 개수 · 골드
        private static readonly (int items, int gold)[] UpgradeCosts = { (1, 30), (2, 60), (2, 90), (3, 120), (3, 150) };

        private static RelicData Relic(string id, string name, string description, MonsterRole role, Color color,
            SkillData skill, SkillData awakened, MonsterStats baseBonus, MonsterStats perLevel, ItemData material)
        {
            return CreateIfMissing<RelicData>($"{Root}/Relics/{id}.asset", so =>
            {
                Prop(so, "relicId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "description").stringValue = description;
                Prop(so, "role").enumValueIndex = (int)role;
                Prop(so, "placeholderColor").colorValue = color;
                Prop(so, "skill").objectReferenceValue = skill;
                Prop(so, "awakenedSkill").objectReferenceValue = awakened;
                Prop(so, "awakenLevel").intValue = 3;
                SetStats(Prop(so, "baseBonus"), baseBonus);
                SetStats(Prop(so, "bonusPerLevel"), perLevel);
                Prop(so, "upgradeItem").objectReferenceValue = material;
                var costs = Prop(so, "upgradeCosts");
                costs.arraySize = UpgradeCosts.Length;
                for (int i = 0; i < UpgradeCosts.Length; i++)
                {
                    costs.GetArrayElementAtIndex(i).FindPropertyRelative("itemCount").intValue = UpgradeCosts[i].items;
                    costs.GetArrayElementAtIndex(i).FindPropertyRelative("gold").intValue = UpgradeCosts[i].gold;
                }
            });
        }

        private static HeroData HeroAsset(SkillData basicSkill, params RelicData[] startingRelics)
        {
            return CreateIfMissing<HeroData>(HeroPath, so =>
            {
                Prop(so, "displayName").stringValue = "주인공";
                Prop(so, "description").stringValue = "단어의 힘을 성유물에 담아 싸우는 견습 모험가.";
                SetStats(Prop(so, "baseStats"), new MonsterStats(60, 14, 10));
                SetStats(Prop(so, "growthPerLevel"), new MonsterStats(8, 3, 2));
                Prop(so, "startLevel").intValue = 3;
                Prop(so, "basicSkill").objectReferenceValue = basicSkill;
                var list = Prop(so, "startingRelics");
                list.arraySize = startingRelics.Length;
                for (int i = 0; i < startingRelics.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = startingRelics[i];
            });
        }

        private static SkillData Skill(string id, string name, string description, SkillKind kind, SkillTarget target,
            int power, QuizDirection direction)
        {
            return CreateIfMissing<SkillData>($"{Root}/Skills/{id}.asset", so =>
            {
                Prop(so, "skillId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "description").stringValue = description;
                Prop(so, "kind").enumValueIndex = (int)kind;
                Prop(so, "target").enumValueIndex = (int)target;
                Prop(so, "power").intValue = power;
                Prop(so, "quizDirection").enumValueIndex = (int)direction;
            });
        }

        private static MonsterSpecies Monster(string id, string name, string description, MonsterRole role, Color color,
            MonsterStats baseStats, MonsterStats growth, SkillData[] skills,
            int enemyExp = 5, int enemyGold = 5, ItemDrop[] drops = null, bool trainingDummy = false)
        {
            return CreateIfMissing<MonsterSpecies>($"{Root}/Monsters/{id}.asset", so =>
            {
                Prop(so, "speciesId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "description").stringValue = description;
                Prop(so, "role").enumValueIndex = (int)role;
                Prop(so, "placeholderColor").colorValue = color;
                SetStats(Prop(so, "baseStats"), baseStats);
                SetStats(Prop(so, "growthPerLevel"), growth);

                var skillList = Prop(so, "skills");
                skillList.arraySize = skills.Length;
                for (int i = 0; i < skills.Length; i++) skillList.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];

                Prop(so, "expReward").intValue = enemyExp;
                Prop(so, "goldReward").intValue = enemyGold;
                Prop(so, "trainingDummy").boolValue = trainingDummy;
                var dropList = Prop(so, "drops");
                dropList.arraySize = drops?.Length ?? 0;
                for (int i = 0; i < dropList.arraySize; i++)
                {
                    var element = dropList.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("item").objectReferenceValue = drops[i].Item;
                    element.FindPropertyRelative("chance").floatValue = drops[i].Chance;
                    element.FindPropertyRelative("count").intValue = drops[i].Count;
                }
            });
        }

        private static EncounterTable Encounters(string id, int minGroup, int maxGroup, params EncounterTable.Entry[] entries)
        {
            return CreateIfMissing<EncounterTable>($"{Root}/Encounters/{id}.asset", so =>
            {
                Prop(so, "minGroupSize").intValue = minGroup;
                Prop(so, "maxGroupSize").intValue = maxGroup;
                var list = Prop(so, "entries");
                list.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("species").objectReferenceValue = entries[i].Species;
                    element.FindPropertyRelative("minLevel").intValue = entries[i].MinLevel;
                    element.FindPropertyRelative("maxLevel").intValue = entries[i].MaxLevel;
                    element.FindPropertyRelative("weight").intValue = entries[i].Weight;
                }
            });
        }

        private static void SetStats(SerializedProperty property, MonsterStats stats)
        {
            property.FindPropertyRelative("maxHp").intValue = stats.MaxHp;
            property.FindPropertyRelative("attack").intValue = stats.Attack;
            property.FindPropertyRelative("defense").intValue = stats.Defense;
        }

        private static T CreateIfMissing<T>(string path, Action<SerializedObject> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            return so.FindProperty(name) ?? throw new ArgumentException($"{so.targetObject.GetType().Name}에 '{name}' 필드가 없습니다");
        }
    }
}
