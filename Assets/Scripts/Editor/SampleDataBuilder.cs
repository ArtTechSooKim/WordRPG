using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
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

            // --- 누군가의 기록 (#200): 상자에서 얻어 도감에서 읽음. 단어를 찾을수록 일그러진 줄이 선명해짐 ---
            var diaryJournal = Item("journal_diary", "누군가의 일기",
                "누군가 서고에 남기고 간 일기. 모르는 단어가 있는 줄은 글자가 일그러져 읽을 수 없다.",
                new Color(0.55f, 0.32f, 0.2f), ItemKind.Journal);
            var survivalJournal = Item("journal_survival", "누군가의 생존일지",
                "누군가 숲에 남기고 간 생존일지. 진흙투성이다. 모르는 단어가 있는 줄은 글자가 일그러져 읽을 수 없다.",
                new Color(0.4f, 0.45f, 0.22f), ItemKind.Journal);

            // --- 상처약 (상점에서 판매, 필드·전투에서 사용) ---
            var potion = Item("potion", "상처약", "상처에 바르면 HP를 40 회복한다. 필드·전투에서 쓸 수 있다.",
                new Color(0.9f, 0.35f, 0.3f), ItemKind.Consumable, 40);
            var largePotion = Item("potion_large", "큰 상처약", "HP를 120 회복하는 진한 약. 필드·전투에서 쓸 수 있다.",
                new Color(0.85f, 0.2f, 0.3f), ItemKind.Consumable, 120);
            // --- 숲 상점부터 파는 물약 (#146 사용자): 특제 상처약은 큰 상처약에 비례한 값, 효과 물약은 필드에서 마심 ---
            var greatPotion = Item("potion_great", "특제 상처약", "HP를 240 회복하는 특제 약. 필드·전투에서 쓸 수 있다.",
                new Color(0.95f, 0.45f, 0.25f), ItemKind.Consumable, 240);
            var quietPotion = Item("potion_quiet", "살금살금 물약", "마시면 100걸음 동안 몬스터를 만나지 않는다. 필드에서 쓸 수 있다.",
                new Color(0.45f, 0.75f, 0.85f), ItemKind.Consumable, effect: ItemEffect.Quiet, effectAmount: 100);
            var curiousPotion = Item("potion_curious", "호기심 물약", "마시면 전투 문제에 새 단어만 10개 나온다. 필드에서 쓸 수 있다.",
                new Color(1f, 0.82f, 0.3f), ItemKind.Consumable, effect: ItemEffect.NewWords, effectAmount: 10);
            // #232 사용자: 숲은 특제 상처약까지, 효과 물약 두 가지는 정글 상점부터 (값도 밸런스로 올림)
            var forestPotions = new[] { new ShopEntry(potion, 30), new ShopEntry(largePotion, 90), new ShopEntry(greatPotion, 180) };
            var junglePotions = new[] { new ShopEntry(potion, 30), new ShopEntry(largePotion, 90), new ShopEntry(greatPotion, 180),
                new ShopEntry(quietPotion, 2000), new ShopEntry(curiousPotion, 10000) };

            // --- 지역 도감 완성 징표 (지역 특색에 맞는 기념물) ---
            var meadowKeepsake = Item("keepsake_meadow", "네잎클로버 책갈피",
                "초원에서 단어를 모아 받은 증표. 행운을 부르는 네잎클로버가 곱게 눌려 있다.", new Color(0.35f, 0.8f, 0.35f), ItemKind.Keepsake);
            var forestKeepsake = Item("keepsake_forest", "도토리 책갈피",
                "숲에서 단어를 모아 받은 증표. 반질반질한 도토리가 매달려 있다.", new Color(0.6f, 0.4f, 0.2f), ItemKind.Keepsake);
            var libraryKeepsake = Item("keepsake_library", "깃털 책갈피",
                "초원과 잊혀진 서고의 단어를 모두 찾은 증표. 오래된 책장 사이에서 찾은 깃털이 끼워져 있다.", new Color(0.95f, 0.85f, 0.6f), ItemKind.Keepsake);

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
            // 까먹대왕 전용 (#120 사용자: 한 방 40 이상 · 힘을 모았다가 기억상실 100 — 까먹깨비 기술과 따로)
            var kingBonk = Skill("boss_forget_bonk", "깜빡 대방망이", "까먹대왕의 커다란 방망이질. 맞으면 머리가 새하얗게 된다.", SkillKind.Damage, SkillTarget.SingleEnemy, 100, QuizDirection.EnglishToMeaning);
            var amnesia = Skill("boss_amnesia", "기억상실", "힘을 잔뜩 모았다가 내리치는 한 방. 맞으면 기억이 하얗게 날아간다.", SkillKind.Damage, SkillTarget.SingleEnemy, 230, QuizDirection.EnglishToMeaning,
                chargeTurns: 1);
            var blankBonk = Skill("goblin_blank_bonk", "깜빡 방망이", "머리를 하얗게 만드는 방망이질.", SkillKind.Damage, SkillTarget.SingleEnemy, 18, QuizDirection.EnglishToMeaning);
            var spore = Skill("mushroom_spell_spore", "철자 포자", "철자를 뒤섞는 포자를 흩뿌린다.", SkillKind.Damage, SkillTarget.AllEnemies, 12, QuizDirection.EnglishToMeaning);
            var squeeze = Skill("snake_squiggle_squeeze", "꼬부랑 조이기", "꼬부랑 글씨처럼 몸을 비틀어 조인다.", SkillKind.Damage, SkillTarget.SingleEnemy, 20, QuizDirection.EnglishToMeaning);
            var peck = Skill("owl_question_peck", "물음표 쪼기", "'그게 뭐더라?' 하며 부리로 쫀다.", SkillKind.Damage, SkillTarget.SingleEnemy, 18, QuizDirection.EnglishToMeaning);
            // 헷갈너구리 (#144 사용자: 서고 보스보다 너무 약함 — 비슷하거나 더 어렵게): 까먹대왕처럼 큰 한 방 + 힘을 모았다가 더 큰 한 방
            var muddle = Skill("raccoon_muddle_whirl", "뒤죽박죽 소용돌이", "힘을 잔뜩 모았다가 단어를 뒤죽박죽 섞는 큰 소용돌이를 일으킨다.", SkillKind.Damage, SkillTarget.AllEnemies, 250, QuizDirection.EnglishToMeaning,
                chargeTurns: 1);
            var tailSlam = Skill("raccoon_tail_slam", "꼬리 후려치기", "커다란 꼬리로 힘껏 후려친다.", SkillKind.Damage, SkillTarget.SingleEnemy, 110, QuizDirection.EnglishToMeaning);
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
            var quill = Relic("relic_quill", "깃펜", "펜촉이가 남긴 깃펜. 손에 쥐면 잊고 있던 문장의 첫 글자가 떠오른다.", MonsterRole.Attacker,
                new Color(0.25f, 0.45f, 0.95f), inkSplash, inkStorm, new MonsterStats(0, 4, 0), new MonsterStats(0, 2, 0), shinyInk);
            var book = Relic("relic_book", "백과사전", "책껍질이 지고 다니던 두꺼운 사전. 몇 페이지는 누군가 뜯어 갔다.", MonsterRole.Defender,
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
            var leaf = Relic("relic_leaf", "세계수 잎", "헷갈너구리가 품고 있던 세계수의 잎. 아직 가 보지 않은 땅의 윤곽이 희미하게 비친다.", MonsterRole.Supporter,
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
                new Color(0.15f, 0.6f, 0.55f), new MonsterStats(100, 14, 12), new MonsterStats(16, 3, 2), new[] { kingBonk, memoryDrain, amnesia }, // HP 2배 (#120)
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
                new Color(0.8f, 0.35f, 0.25f), new MonsterStats(100, 16, 14), new MonsterStats(16, 3, 2), new[] { muddle, tailSlam, acornSnack }, // HP·기술 #144
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
            // 단어장 (#147 사용자): 초원·서고가 150개, 숲·정글·동굴이 300개를 같이 씀. 징표는 지역마다 그대로 — 발견한 수에 닿을 때마다
            ConfigureRegion(CommonWords, "common", "초원·서고",
                new DexReward("meadow", "초원", meadowKeepsake, 500, 75, 1),
                new DexReward("library", "잊혀진 서고", libraryKeepsake, 600, 0, 2));
            SetJournal(CommonWords, diaryJournal);
            // (#62) 서고도 자기 단어장: 교육과정 기본 어휘 중·고 공통 중 어려운 600

            // --- 필드: 초원. 보물상자는 맵의 C를 위→아래, 왼→오른 순으로 대응 (강화 재료·성유물을 얻는 곳) ---
            Area("meadow", "초원", MeadowMap, meadowEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>(CommonWords),
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

            // --- 던전: 잊혀진 서고 (초원 북쪽 동굴 입구로 연결). 단어장은 서고 것 (#62 — 예전에는 초원과 같았음) ---
            Area("library", "잊혀진 서고", LibraryMap, dungeonEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>(CommonWords),
                new ChestContent(null, 0, 120),          // 보스 앞 회랑 왼쪽 구석
                new ChestContent(diaryJournal, 1),       // 보스 문 앞 회랑 오른쪽 벽감 — 누군가의 일기 (#200)
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
                AssetDatabase.LoadAssetAtPath<WordDatabase>(AdvancedWords),
                new ChestContent(null, 0, 60, whip),     // 부엉이 골짜기 북서 끝 — 덩굴 채찍
                new ChestContent(hardCover, 3),          // 골짜기 위 풀숲
                new ChestContent(shinyInk, 3),           // 개울 건너 징검다리
                new ChestContent(sparkleDust, 3),        // 늪 한가운데
                new ChestContent(null, 0, 60, hourglass),// 버섯 공터 — 시간의 모래시계
                new ChestContent(survivalJournal, 1),    // 야영지 아래 틈 — 누군가의 생존일지 (#200)
                new ChestContent(null, 0, 150));         // 버섯 공터 아래 구석
            var forestShop = ShopAsset("forest_shop", "숲속 쉼터 가게", forestPotions);
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

            // 보스 방 문 (#76, 사용자 결정): 맵에 숨긴 열쇠(K)를 찾고 그 지역 단어 40개를 발견해야 보스 방(G)이 열린다
            var libraryKey = Item("library_key", "서고 열쇠", "잊혀진 서고 깊은 곳, 보스 방 문을 여는 금빛 열쇠.",
                new Color(1f, 0.8f, 0.25f), ItemKind.Key);
            var forestKey = Item("forest_key", "숲 열쇠", "숲 깊은 곳, 보스 방 문을 여는 은빛 열쇠.",
                new Color(0.8f, 0.82f, 0.86f), ItemKind.Key);
            AddGateToMap("Assets/Data/Areas/library.asset", LibraryMap);
            AddGateToMap("Assets/Data/Areas/forest.asset", ForestMap);
            SetBossGate("Assets/Data/Areas/library.asset", libraryKey, 40);
            SetBossGate("Assets/Data/Areas/forest.asset", forestKey, 40);

            // --- 동료 (#77, 1.1 메아리 정글): 유적 석판 4개 + 정글 단어 60개 → 아라를 만남 (얻는 곳은 정글을 만들 때 붙임) ---
            var araPebble = Skill("ara_pebble", "돌팔매", "돌을 던져 적 하나를 맞힌다.", SkillKind.Damage, SkillTarget.SingleEnemy, 12, QuizDirection.EnglishToMeaning);
            var araSprout = Skill("ara_sprout", "새싹 치유", "새싹의 힘으로 아군 하나를 치유한다.", SkillKind.Heal, SkillTarget.SingleAlly, 20, QuizDirection.MeaningToEnglish);
            var araEcho = Skill("ara_echo", "메아리 방패", "메아리를 둘러 모두를 지킨다.", SkillKind.Guard, SkillTarget.AllAllies, 8, QuizDirection.EnglishToMeaning);
            Companion("ara", "아라", "유적에 갇혀 고대 단어를 지키던 정글 소녀. 석판의 빈칸을 채워 준 주인공을 따라 함께 떠난다.",
                "정글 소녀 · 회복·보조", new MonsterStats(52, 11, 11), new MonsterStats(6, 2, 2), araPebble, (1, araSprout), (8, araEcho));

            // --- 1.1 메아리 정글 (#80, GDD 9-6): 숲 보스 뒤 새 지역(정식판). 보이는 몬스터 + [선제공격], 깊은 강은 뗏목, 유적에서 아라 ---
            var vineFiber = Item("vine_fiber", "덩굴 섬유", "정글 덩굴에서 뽑은 질긴 섬유. 정글의 성유물을 강화한다.", new Color(0.4f, 0.65f, 0.25f));
            var jungleKeepsake = Item("keepsake_jungle", "메아리 풀잎 책갈피",
                "메아리 정글에서 단어를 모아 받은 증표. 귀에 대면 단어가 메아리처럼 들리는 풀잎이 끼워져 있다.", new Color(0.35f, 0.75f, 0.4f), ItemKind.Keepsake);
            var jungleKey = Item("jungle_key", "정글 열쇠", "메아리 정글 깊은 곳, 빈칸두꺼비의 공터 문을 여는 이끼 낀 열쇠.",
                new Color(0.6f, 0.85f, 0.4f), ItemKind.Key);
            var echoVeil = Skill("flute_echo_veil", "메아리 장막", "피리 소리가 메아리 장막이 되어 모두를 지킨다.", SkillKind.Guard, SkillTarget.AllAllies, 10, QuizDirection.MeaningToEnglish);
            var echoWall = Skill("flute_echo_wall", "겹메아리 방벽", "메아리가 겹겹이 울려 단단한 방벽을 세운다.", SkillKind.Guard, SkillTarget.AllAllies, 16, QuizDirection.MeaningToEnglish);
            var gourdSip = Skill("gourd_sip", "조롱박 한 모금", "시원한 정글 샘물을 나눠 마셔 모두의 HP를 회복한다.", SkillKind.Heal, SkillTarget.AllAllies, 12, QuizDirection.MeaningToEnglish);
            var gourdSpring = Skill("gourd_spring", "솟아나는 샘", "조롱박에서 샘물이 솟아 모두의 HP를 크게 회복한다.", SkillKind.Heal, SkillTarget.AllAllies, 20, QuizDirection.MeaningToEnglish);
            var letterShower = Skill("gem_letter_shower", "글자 소나기", "구슬에 갇혀 있던 글자들이 쏟아져 적 전체를 때린다.", SkillKind.Damage, SkillTarget.AllEnemies, 24, QuizDirection.MeaningToEnglish);
            var letterEcho = Skill("gem_letter_echo", "메아리 글자비", "되찾은 글자가 메아리치며 적 전체에 쏟아진다.", SkillKind.Damage, SkillTarget.AllEnemies, 34, QuizDirection.MeaningToEnglish);
            var flute = Relic("relic_flute", "메아리 피리", "유적에 잠들어 있던 피리. 불면 메아리가 장막처럼 둘러싼다.", MonsterRole.Defender,
                new Color(0.75f, 0.55f, 0.3f), echoVeil, echoWall, new MonsterStats(20, 0, 6), new MonsterStats(6, 0, 2), vineFiber);
            var gourd = Relic("relic_gourd", "정글 조롱박", "강 위쪽 모래톱에 떠밀려 온 조롱박. 함께 다니는 동료까지 시원하게 회복시킨다.", MonsterRole.Supporter,
                new Color(0.85f, 0.7f, 0.35f), gourdSip, gourdSpring, new MonsterStats(25, 0, 2), new MonsterStats(6, 0, 1), vineFiber);
            var letterGem = Relic("relic_letter_gem", "글자 구슬", "빈칸두꺼비가 삼켰던 석판의 글자들이 뭉친 구슬.", MonsterRole.Attacker,
                new Color(0.4f, 0.85f, 0.5f), letterShower, letterEcho, new MonsterStats(0, 8, 0), new MonsterStats(0, 3, 0), vineFiber);
            var fillBlank = Skill("doc_fill_blank", "빈칸 채우기", "빈칸에 꼭 맞는 단어를 떠올려 적 하나를 크게 공격한다.", SkillKind.Damage, SkillTarget.SingleEnemy, 38, QuizDirection.MeaningToEnglish);
            var fillBlankDoc = Item("skilldoc_fill_blank", "기술문서: 빈칸 채우기", "읽으면 기술 '빈칸 채우기'를 배운다. 배워도 사라지지 않아 언제든 다시 배울 수 있다.",
                new Color(0.9f, 0.9f, 0.8f), ItemKind.SkillDocument, taughtSkill: fillBlank);

            var tailGrab = Skill("lizard_tail_grab", "말꼬리 잡기", "말꼬리를 붙잡고 늘어져 끝말을 흐리게 만든다.", SkillKind.Damage, SkillTarget.SingleEnemy, 22, QuizDirection.EnglishToMeaning);
            var plateSplash = Skill("kappa_plate_splash", "물접시 튀기기", "머리 접시의 물을 사방에 튀긴다.", SkillKind.Damage, SkillTarget.AllEnemies, 14, QuizDirection.EnglishToMeaning);
            var batchimSwap = Skill("kappa_batchim_swap", "받침 바꾸기", "받침을 몰래 바꿔 철자를 헷갈리게 한다.", SkillKind.Damage, SkillTarget.SingleEnemy, 20, QuizDirection.EnglishToMeaning);
            var inkMimic = Skill("octopus_ink_mimic", "먹물 흉내", "들은 말을 엉터리로 흉내 내며 먹물을 뿜는다.", SkillKind.Damage, SkillTarget.AllEnemies, 16, QuizDirection.EnglishToMeaning);
            // 빈칸두꺼비 숫자는 #88 시뮬레이션으로 맞춤 (아라와 함께 정답률 80%면 10라운드쯤, 혼자·정답률 60%면 도전)
            var tongueLash = Skill("frog_tongue_lash", "혀 날름", "긴 혀를 잔뜩 말아 힘을 모았다가 힘껏 후려친다.", SkillKind.Damage, SkillTarget.SingleEnemy, 270, QuizDirection.EnglishToMeaning,
                chargeTurns: 1); // 보스가 뒤로 갈수록 세게 (#147)
            var blankCroak = Skill("frog_blank_croak", "빈칸 울음", "개굴! 울음소리에 머릿속이 하얗게 빈칸이 된다.", SkillKind.Damage, SkillTarget.AllEnemies, 120, QuizDirection.EnglishToMeaning);
            var gulp = Skill("frog_gulp", "꿀꺽 삼키기", "석판의 글자를 꿀꺽 삼켜 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 10, QuizDirection.EnglishToMeaning);
            var tailLizard = Monster("tail_lizard", "말꼬리 도마뱀", "남의 말꼬리를 잡고 늘어지는 도마뱀. 정글 풀숲을 재빠르게 누빈다.", MonsterRole.Attacker,
                new Color(0.5f, 0.75f, 0.35f), new MonsterStats(28, 14, 10), new MonsterStats(5, 3, 2), new[] { tailGrab },
                enemyExp: 14, enemyGold: 13, drops: new[] { new ItemDrop(vineFiber, 0.4f), new ItemDrop(shinyInk, 0.2f) });
            var batchimKappa = Monster("batchim_kappa", "받침갓파", "머리 접시에 받침을 담고 다니며 몰래 바꿔 치는 갓파. 강가 풀숲에 산다.", MonsterRole.Defender,
                new Color(0.85f, 0.35f, 0.3f), new MonsterStats(36, 13, 13), new MonsterStats(7, 2, 3), new[] { plateSplash, batchimSwap },
                enemyExp: 16, enemyGold: 15, drops: new[] { new ItemDrop(vineFiber, 0.35f), new ItemDrop(hardCover, 0.25f) });
            var mimicOctopus = Monster("mimic_octopus", "흉내문어", "들은 말을 엉터리로 흉내 내는 문어. 깊은 강에 살며 뗏목을 쫓아온다.", MonsterRole.Attacker,
                new Color(0.4f, 0.7f, 0.45f), new MonsterStats(30, 15, 9), new MonsterStats(6, 3, 2), new[] { inkMimic },
                enemyExp: 15, enemyGold: 14, drops: new[] { new ItemDrop(sparkleDust, 0.4f), new ItemDrop(vineFiber, 0.25f) });
            var blankFrog = Monster("boss_blank_frog", "빈칸두꺼비", "유적 석판의 글자를 꿀꺽 삼켜 빈칸으로 만든 거대한 두꺼비. 정글의 메아리를 앗아 갔다.", MonsterRole.Attacker,
                new Color(0.45f, 0.7f, 0.35f), new MonsterStats(124, 30, 12), new MonsterStats(16, 5, 1), new[] { tongueLash, blankCroak, gulp }, // HP·기술 #147
                enemyExp: 70, enemyGold: 90,
                drops: new[] { new ItemDrop(vineFiber, 1f, 3), new ItemDrop(shinyInk, 1f, 2), new ItemDrop(hardCover, 1f, 2), new ItemDrop(sparkleDust, 1f, 2) });
            var jungleEncounters = Encounters("jungle_field", 1, 2,
                new EncounterTable.Entry(tailLizard, 12, 14, 10),
                new EncounterTable.Entry(batchimKappa, 13, 15, 8));
            var riverEncounters = Encounters("jungle_river", 1, 2,
                new EncounterTable.Entry(mimicOctopus, 13, 15, 10));

            const string junglePath = "Assets/Data/Areas/jungle.asset";
            // 보물상자 15개 (맵의 C를 위→아래, 왼→오른 순). 강 위쪽·아래쪽 모래톱 4개는 뗏목에서 [확인] (사용자)
            Area("jungle", "메아리 정글", JungleMap, jungleEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>(AdvancedWords),
                new ChestContent(null, 0, 80, gourd),     // 강 위쪽 모래톱 — 정글 조롱박
                new ChestContent(null, 0, 60, flute),     // 유적 북서 방 — 메아리 피리
                new ChestContent(null, 0, 200),           // 유적 북동 방
                new ChestContent(vineFiber, 3),           // 강 위쪽 모래톱 둘째
                new ChestContent(potion, 3),              // 유적 다리 앞 서쪽 풀밭
                new ChestContent(hardCover, 3),           // 보스 길 옆 풀숲
                new ChestContent(shinyInk, 3),            // 동쪽 기슭 (뗏목)
                new ChestContent(vineFiber, 2),           // 얕은 강 남쪽 풀숲
                new ChestContent(sparkleDust, 3),         // 동쪽 기슭 남쪽
                new ChestContent(largePotion, 1),         // 버섯 늪 나루터 옆
                new ChestContent(null, 0, 150),           // 강 아래쪽 모래톱
                new ChestContent(vineFiber, 3),           // 버섯 늪
                new ChestContent(sparkleDust, 2, 60),     // 동쪽 기슭 맨 아래
                new ChestContent(potion, 2),              // 야영지
                new ChestContent(null, 0, 200));          // 강 아래쪽 모래톱 둘째
            var jungleShop = ShopAsset("jungle_shop", "메아리 야영지 가게", junglePotions);
            AssignShopIfEmpty(junglePath, jungleShop);
            SetFullVersionArea(junglePath); // 정식판에 포함 (추가 결제 없음 — 사용자)
            SetDictionary(junglePath, "정글의 사전");
            SetBossGate(junglePath, jungleKey, 80);
            ConfigureAreaIfNew(junglePath, so =>
            {
                Prop(so, "theme").enumValueIndex = (int)FieldTheme.Jungle;
                Prop(so, "encounterStyle").enumValueIndex = (int)EncounterStyle.Roaming;
                Prop(so, "waterEncounters").objectReferenceValue = riverEncounters;
                Prop(so, "shrineCompanion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CompanionData>($"{Root}/Companions/ara.asset");
                Prop(so, "shrineWords").intValue = 100;
                var boss = Prop(so, "boss");
                boss.FindPropertyRelative("species").objectReferenceValue = blankFrog;
                boss.FindPropertyRelative("level").intValue = 17;
                boss.FindPropertyRelative("rewardRelic").objectReferenceValue = letterGem;
                boss.FindPropertyRelative("rewardItem").objectReferenceValue = fillBlankDoc;
            });

            // --- 1.2 어두운 밀림 동굴 (#115, GDD 9-7): 정글 강 가운데 섬 밑(정식판, 입장 조건 없음 — 사용자). 어둠 · 글자 반딧돌 8개 · 먹물도롱뇽 ---
            //     숫자는 BalanceSimulationTests(동굴 전투·보스)로 맞춤 — 정글 보스를 이긴 주인공 Lv17~19 + 아라 기준
            var glowMoss = Item("glow_moss", "빛이끼", "동굴 벽에서 은은하게 빛나는 이끼. 동굴의 성유물을 강화한다.", new Color(0.4f, 0.85f, 0.8f));
            var caveKeepsake = Item("keepsake_cave", "반딧불 책갈피",
                "숲과 정글과 동굴의 단어를 모두 찾은 증표. 반딧불 한 마리가 깃들어 은은하게 빛난다.", new Color(1f, 0.9f, 0.4f), ItemKind.Keepsake);
            var caveKey = Item("cave_key", "동굴 열쇠", "어두운 밀림 동굴 깊은 곳, 말줄임덩굴의 방 문을 여는 푸른 수정빛 열쇠.",
                new Color(0.6f, 0.8f, 1f), ItemKind.Key);
            var glowFlash = Skill("lamp_glow_flash", "반딧빛 번쩍", "등잔의 반딧빛이 번쩍 터져 적 전체를 눈부시게 때린다.", SkillKind.Damage, SkillTarget.AllEnemies, 26, QuizDirection.MeaningToEnglish);
            var fireflySwarm = Skill("lamp_firefly_swarm", "반딧불 떼", "깨어난 반딧불이 떼 지어 적 전체를 휘감는다.", SkillKind.Damage, SkillTarget.AllEnemies, 38, QuizDirection.MeaningToEnglish);
            var fullStop = Skill("seed_full_stop", "마침표 찍기", "또박또박 마침표를 찍듯 모두에게 단단한 보호막을 친다.", SkillKind.Guard, SkillTarget.AllAllies, 14, QuizDirection.MeaningToEnglish);
            var periodGrove = Skill("seed_period_grove", "마침표 울타리", "씨앗에서 자란 덩굴 울타리가 모두를 겹겹이 지킨다.", SkillKind.Guard, SkillTarget.AllAllies, 22, QuizDirection.MeaningToEnglish);
            var glowLamp = Relic("relic_glow_lamp", "반딧돌 등잔", "동굴의 반딧돌을 모두 밝히자 천장의 반딧불이 내려와 깃든 등잔. 빛으로 적 전체를 공격한다.", MonsterRole.Attacker,
                new Color(0.45f, 0.85f, 1f), glowFlash, fireflySwarm, new MonsterStats(0, 9, 0), new MonsterStats(0, 3, 0), glowMoss);
            var periodSeed = Relic("relic_period_seed", "마침표 씨앗", "말줄임덩굴이 삼켰던 문장의 끝이 뭉친 씨앗. 끝맺은 문장처럼 단단하다.", MonsterRole.Defender,
                new Color(0.4f, 0.35f, 0.5f), fullStop, periodGrove, new MonsterStats(24, 0, 7), new MonsterStats(7, 0, 2), glowMoss);
            var finishSentence = Skill("doc_finish_sentence", "문장 끝맺기", "말줄임표로 끝난 문장을 끝까지 맺어 적 전체를 공격한다.", SkillKind.Damage, SkillTarget.AllEnemies, 30, QuizDirection.MeaningToEnglish);
            var finishSentenceDoc = Item("skilldoc_finish_sentence", "기술문서: 문장 끝맺기", "읽으면 기술 '문장 끝맺기'를 배운다. 배워도 사라지지 않아 언제든 다시 배울 수 있다.",
                new Color(0.75f, 0.7f, 0.9f), ItemKind.SkillDocument, taughtSkill: finishSentence);

            var blinkBrush = Skill("ghost_blink_brush", "깜빡 스침", "스르륵 스쳐 지나가며 방금 외운 단어를 깜빡 잊게 한다.", SkillKind.Damage, SkillTarget.SingleEnemy, 20, QuizDirection.EnglishToMeaning);
            var chew = Skill("larva_chew", "곱씹기", "단어를 곱씹듯 물고 늘어진다.", SkillKind.Damage, SkillTarget.SingleEnemy, 20, QuizDirection.EnglishToMeaning);
            var silkSpray = Skill("larva_silk_spray", "실 뿜기", "끈끈한 실을 뿜어 모두를 휘감는다.", SkillKind.Damage, SkillTarget.AllEnemies, 14, QuizDirection.EnglishToMeaning);
            var tangleWeb = Skill("spider_tangle_web", "엉킴 거미줄", "철자를 엉키게 하는 거미줄을 흩뿌린다.", SkillKind.Damage, SkillTarget.AllEnemies, 16, QuizDirection.EnglishToMeaning);
            var spellBite = Skill("spider_spell_bite", "철자 깨물기", "단어의 철자 하나를 콱 깨물어 떼어 간다.", SkillKind.Damage, SkillTarget.SingleEnemy, 24, QuizDirection.EnglishToMeaning);
            var inkSpit = Skill("newt_ink_spit", "먹물 뱉기", "웅덩이에서 머금은 먹물을 힘껏 뱉는다.", SkillKind.Damage, SkillTarget.SingleEnemy, 22, QuizDirection.EnglishToMeaning);
            var vineSlam = Skill("vine_slam", "덩굴 후려치기", "굵은 덩굴에 힘을 잔뜩 모았다가 내리친다.", SkillKind.Damage, SkillTarget.SingleEnemy, 310, QuizDirection.EnglishToMeaning,
                chargeTurns: 1); // 보스가 뒤로 갈수록 세게 (#147)
            var ellipsisSpore = Skill("vine_ellipsis_spore", "말줄임 포자", "포자를 흩뿌려 하려던 말이 '…'로 끊기게 한다.", SkillKind.Damage, SkillTarget.AllEnemies, 135, QuizDirection.EnglishToMeaning);
            var swallowSentence = Skill("vine_swallow_sentence", "문장 삼키기", "문장의 끝을 꿀꺽 삼켜 HP를 회복한다.", SkillKind.Heal, SkillTarget.Self, 10, QuizDirection.EnglishToMeaning);
            var blinkGhost = Monster("blink_ghost", "깜빡유령", "방금 외운 단어를 깜빡 잊게 만드는 유령. 빛 밖에서 깜빡깜빡 나타났다 사라진다.", MonsterRole.Attacker,
                new Color(0.8f, 0.85f, 0.95f), new MonsterStats(24, 14, 8), new MonsterStats(5, 3, 2), new[] { blinkBrush },
                enemyExp: 15, enemyGold: 14, drops: new[] { new ItemDrop(glowMoss, 0.35f), new ItemDrop(sparkleDust, 0.2f) });
            var chewLarva = Monster("chew_larva", "곱씹애벌레", "단어를 곱씹고 또 곱씹다 꿀꺽 삼켜 버리는 애벌레. 빛이끼 덤불에 산다.", MonsterRole.Defender,
                new Color(0.75f, 0.8f, 0.45f), new MonsterStats(34, 12, 13), new MonsterStats(7, 2, 3), new[] { chew, silkSpray },
                enemyExp: 17, enemyGold: 16, drops: new[] { new ItemDrop(glowMoss, 0.35f), new ItemDrop(hardCover, 0.25f) });
            var tangleSpider = Monster("tangle_spider", "엉킴거미", "철자를 거미줄로 엉키게 하는 거미. 거미 굴 천장에 매달려 지나가는 사람을 노린다.", MonsterRole.Attacker,
                new Color(0.8f, 0.3f, 0.3f), new MonsterStats(32, 16, 10), new MonsterStats(6, 3, 2), new[] { tangleWeb, spellBite },
                enemyExp: 19, enemyGold: 18, drops: new[] { new ItemDrop(glowMoss, 0.4f), new ItemDrop(shinyInk, 0.25f) });
            var inkNewt = Monster("ink_newt", "먹물도롱뇽", "먹물 웅덩이에 숨어 있다가 튀어나오는 도롱뇽. 지나간 자리마다 먹물 방울이 떨어진다.", MonsterRole.Attacker,
                new Color(0.4f, 0.3f, 0.7f), new MonsterStats(30, 15, 9), new MonsterStats(6, 3, 2), new[] { inkSpit },
                enemyExp: 17, enemyGold: 16, drops: new[] { new ItemDrop(glowMoss, 0.3f), new ItemDrop(shinyInk, 0.3f) });
            var ellipsisVine = Monster("boss_ellipsis_vine", "말줄임덩굴", "문장의 끝을 삼켜 '…'만 남기는 거대한 덩굴. 덩굴로 반딧돌을 덮어 동굴을 캄캄하게 만들었다.", MonsterRole.Attacker,
                new Color(0.35f, 0.6f, 0.35f), new MonsterStats(116, 32, 13), new MonsterStats(16, 5, 1), new[] { vineSlam, ellipsisSpore, swallowSentence }, // HP·기술 #147
                enemyExp: 90, enemyGold: 120,
                drops: new[] { new ItemDrop(glowMoss, 1f, 3), new ItemDrop(vineFiber, 1f, 2), new ItemDrop(shinyInk, 1f, 2), new ItemDrop(hardCover, 1f, 2), new ItemDrop(sparkleDust, 1f, 2) });
            var caveEncounters = Encounters("cave_field", 1, 2,
                new EncounterTable.Entry(blinkGhost, 15, 17, 10),
                new EncounterTable.Entry(chewLarva, 15, 18, 8),
                new EncounterTable.Entry(tangleSpider, 16, 18, 6));
            var inkPoolEncounters = Encounters("cave_water", 1, 2,
                new EncounterTable.Entry(inkNewt, 16, 18, 10));

            ConfigureRegion(AdvancedWords, "advanced", "숲·정글·동굴",
                new DexReward("forest", "숲", forestKeepsake, 700, 100, 3),
                new DexReward("jungle", "메아리 정글", jungleKeepsake, 800, 200, 4),
                new DexReward("cave", "어두운 밀림 동굴", caveKeepsake, 900, 0, 5));
            SetJournal(AdvancedWords, survivalJournal);
            const string cavePath = "Assets/Data/Areas/cave.asset";
            // 보물상자 10개 (맵의 C를 위→아래, 왼→오른 순). 성유물은 반딧돌을 모두 밝힌 보상(반딧돌 등잔)과 보스(마침표 씨앗)
            Area("cave", "어두운 밀림 동굴", CaveMap, caveEncounters,
                AssetDatabase.LoadAssetAtPath<WordDatabase>(AdvancedWords),
                new ChestContent(null, 0, 250),           // 옛 광부 쉼터 (숨은 방)
                new ChestContent(glowMoss, 3),            // 수정 굴 (열쇠 옆)
                new ChestContent(largePotion, 2),         // 숨은 틈
                new ChestContent(glowMoss, 2),            // 버섯 굴
                new ChestContent(potion, 3),              // 대동굴 서쪽
                new ChestContent(sparkleDust, 3, 80),     // 거미 굴 깊은 곳
                new ChestContent(glowMoss, 2),            // 거미 굴
                new ChestContent(hardCover, 3),           // 개울가 굴 (레버 옆)
                new ChestContent(potion, 2),              // 반딧불 쉼터
                new ChestContent(shinyInk, 3, 200));      // 무너진 갱도 (숨은 방)
            // 동굴엔 상점 없음 (#232 사용자: 동굴 속 가게는 이상함 — 샘 길도 있고 정글에 가게가 있음)
            SetFullVersionArea(cavePath); // 정식판에 포함 (추가 결제 없음)
            SetDictionary(cavePath, "동굴의 사전");
            SetBossGate(cavePath, caveKey, 120); // #233 사용자: 동굴은 열쇠·단어 대신 반딧돌을 모두 밝히면 열림 (맵에 K 없음 — BossGate.OpensWithStones). 열쇠 아이템은 예전 세이브에 남은 것을 치우려고 둠
            ConfigureAreaIfNew(cavePath, so =>
            {
                Prop(so, "theme").enumValueIndex = (int)FieldTheme.Cave;
                Prop(so, "encounterStyle").enumValueIndex = (int)EncounterStyle.Roaming;
                Prop(so, "waterEncounters").objectReferenceValue = inkPoolEncounters; // 나루터가 없어 웅덩이에 숨는 몬스터 (#113)
                Prop(so, "glowStoneRelic").objectReferenceValue = glowLamp;
                var boss = Prop(so, "boss");
                boss.FindPropertyRelative("species").objectReferenceValue = ellipsisVine;
                boss.FindPropertyRelative("level").intValue = 20;
                boss.FindPropertyRelative("rewardRelic").objectReferenceValue = periodSeed;
                boss.FindPropertyRelative("rewardItem").objectReferenceValue = finishSentenceDoc;
            });

            // --- 도망 등급 (#84 사용자 결정): 약함 = 항상 도망 / 보통 = 1/4 확률로 단어 문제 / 강함 = 늘 단어 문제 (틀리면 차례를 놓침).
            //     초원의 잉크 슬라임·낙서 박쥐·동굴의 깜빡유령은 약함, 숲·정글·동굴의 보통 몬스터는 보통,
            //     던전의 까먹깨비·정글의 받침갓파(방어형)·동굴의 엉킴거미와 보스는 강함
            SetFleeTier(FleeTier.Weak, inkSlime, scribbleBat, blinkGhost);
            SetFleeTier(FleeTier.Normal, spellMushroom, squiggleSnake, questionOwl, tailLizard, mimicOctopus, chewLarva, inkNewt);
            SetFleeTier(FleeTier.Strong, forgetGoblin, batchimKappa, forgetKing, muddleRaccoon, blankFrog, tangleSpider, ellipsisVine);

            // --- 출입구 연결 (맵의 D를 위→아래, 왼→오른 순): 초원 0 = 서고, 초원 1 = 숲(서고 보스를 물리치면 열림).
            //     숲 0 = 정글(숲 보스를 물리치면 열림, #80 — 보스 공터 위쪽), 숲 1 = 초원.
            //     정글 0 = 동굴(강 가운데 섬의 'O', 1.2 — 처음부터 열림, #115), 정글 1 = 숲 ---
            const string meadowPath = "Assets/Data/Areas/meadow.asset", libraryPath = "Assets/Data/Areas/library.asset",
                forestPath = "Assets/Data/Areas/forest.asset";
            UpgradeMapIf(forestPath, ForestMap, "DCNnS"); // 숲 보스 공터 위쪽에 정글 출입구가 생김 (#80), 야영지 아래 일지 상자 (#200), 인물 (#209)
            UpgradeMapIf(libraryPath, LibraryMap, "CN"); // 보스 문 앞 일기 상자 (#200), 인물 (#209)
            UpgradeMapIf(meadowPath, MeadowMap, "NS"); // 상점 가판대 3×2 (#210)
            UpgradeMapIf(junglePath, JungleMap, "NnS");
            UpgradeMapIf(cavePath, CaveMap, "nS");
            InsertChestIf(libraryPath, 5, 1, diaryJournal); // 상자는 맵의 위→아래 순서라 그 자리에 끼움. 있던 상자 내용물은 그대로
            InsertChestIf(forestPath, 6, 5, survivalJournal);
            BuildNpcs(meadowPath, libraryPath, forestPath, junglePath, cavePath);
            // 샘 이동 창의 지역 순서 (#216)
            SetStoryOrder(meadowPath, 1);
            SetStoryOrder(libraryPath, 2);
            SetStoryOrder(forestPath, 3);
            SetStoryOrder(junglePath, 4);
            SetStoryOrder(cavePath, 5);
            // 보스 대사 (#207, 스토리 문서 5장)
            SetBossLines(libraryPath, "잊어버리면… 아플 일도 없어!", "기억을 돌려줘! 아픈 기억도 우리 것이야.",
                "책 속의 글자가 돌아왔다.\n하지만 몇 페이지는 누군가 일부러 뜯어냈다.");
            SetBossLines(forestPath, "이쪽이 맞나? 저쪽이 맞나? 나도 모르겠어!", "길을 헷갈리는 건 괜찮아. 다시 확인하면 돼.",
                "이정표가 제자리를 찾았다.\n숲 깊은 곳에서 오래된 메아리가 들린다.");
            SetBossLines(junglePath, "내가 삼키면 아무도 잊지 않아. 아무도 틀리지 않아!", "빈칸은 끝이 아니야. 다시 채울 수 있어.",
                "정글에 메아리가 돌아왔다.");
            SetBossLines(cavePath, "끝을 말하지 마… 끝나면 다시 아파질 테니까…", "끝맺는다고 모든 것이 사라지는 건 아니야.",
                "마지막 문장이 완성됐다.\n'사막 아래의 첫 기록'이라는 말이 적혀 있다.");
            // 필드의 보스 그림 크기 (#233 사용자: 작아서 위엄이 없음 → 보스 방에 꽉 차게. 빈칸두꺼비 6배, 말줄임덩굴 가로 5·세로 6배)
            SetBossFieldSize(libraryPath, 5, 5);
            SetBossFieldSize(forestPath, 5, 5);
            SetBossFieldSize(junglePath, 6, 6);
            SetBossFieldSize(cavePath, 5, 6);
            // 성유물 설명에 이야기 단서 (#207, 스토리 문서 7장). 설명을 사람이 고쳤으면 건드리지 않음
            SetRelicDescriptionIf("relic_quill", "펜촉이가 남긴 깃펜. 잉크를 뿌려 적을 한꺼번에 공격한다.",
                "펜촉이가 남긴 깃펜. 손에 쥐면 잊고 있던 문장의 첫 글자가 떠오른다.");
            SetRelicDescriptionIf("relic_book", "책껍질이 지고 다니던 두꺼운 사전. 펼치면 단단한 방패가 된다.",
                "책껍질이 지고 다니던 두꺼운 사전. 몇 페이지는 누군가 뜯어 갔다.");
            SetRelicDescriptionIf("relic_leaf", "헷갈너구리가 품고 있던 세계수의 잎. 숲의 생명력이 담겨 있다.",
                "헷갈너구리가 품고 있던 세계수의 잎. 아직 가 보지 않은 땅의 윤곽이 희미하게 비친다.");
            SetExitDoorIf(meadowPath, 1, 0, 1);         // 그래서 숲의 초원 출입구는 0번 → 1번
            SetExitDoorIf(forestPath, 0, 0, 1);         // 정글의 'O'(위쪽)가 출입구가 되어 정글의 숲 출입구는 0번 → 1번 (#115)
            SetExitsIfMismatch(meadowPath, (libraryPath, 0, null), (forestPath, 1, libraryPath));
            SetExitsIfMismatch(libraryPath, (meadowPath, 0, null));
            SetExitsIfMismatch(forestPath, (junglePath, 1, forestPath), (meadowPath, 1, null));
            SetExitsIfMismatch(junglePath, (cavePath, 0, junglePath), (forestPath, 0, null)); // 동굴은 빈칸두꺼비 뒤 (#233 사용자: 열쇠보다 동굴을 먼저 가게 됨)
            SetExitsIfMismatch(cavePath, (junglePath, 0, null));

            AssetDatabase.SaveAssets();
            GameDatabaseBuilder.Refresh();
            // 정식판 특전 (#233 사용자 ⑨): 호기심 물약 3 + 특제 상처약 5 — 세이브마다 한 번 (FullVersionGift)
            SetFullVersionGift((curiousPotion, 3), (greatPotion, 5));
            Debug.Log("[WordRPG] 샘플 데이터 생성 완료 (기존 에셋은 유지)");
        }

        private static void SetFullVersionGift(params (ItemData item, int count)[] gift)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabaseBuilder.DatabasePath);
            if (database == null) return;
            var so = new SerializedObject(database);
            var list = Prop(so, "fullVersionGift");
            list.arraySize = gift.Length;
            for (int i = 0; i < gift.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = gift[i].item;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = gift[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // . 길  : 잔디(조우 없음)  , 풀숲(조우)  # 나무  ~ 물  F 회복의 샘  C 보물상자  E 성유물 제단  S 상점  P 시작 위치  D 출입구
        // 38×46. 아래 마을에서 출발 → 남쪽 들판(풀숲 띠) → 강의 여울 두 곳 → 호수 언덕(곶 끝 상자) → 북쪽 오래된 숲의 풀숲 오솔길
        // → 맨 위 동굴(서고). 마을 동쪽 길 끝 출입구는 숲으로 — 서고 보스를 물리치기 전엔 덤불로 막혀 있다
        public const string MeadowMap =
                "##################D###################\n" +
                "##################.###################\n" +
                "#####,,,,,,,C###::.::######,,,,,,#####\n" +
                "####,,,,,,,,,##:::.:::####,,,,,,,,,,##\n" +
                "###,,##########:::.:::###,,##~~~#,,,##\n" +
                "####,::####,,###::.::####,,#~~~~~,,,##\n" +
                "####,::####,,####,,######,,##~~~#,,,##\n" +
                "###,,,,,#,,,,####,,,,,,,,,,,,,,##,,C##\n" +
                "####,,,,,,,,,#####,,,,,,,,,,,,,,######\n" +
                "#######,,########,,###########,,######\n" +
                "#######,,########,,###########,,######\n" +
                "#:::::#:.:#:::::::.::::::#####:.:::::#\n" +
                "#:::::#:.::::.###.........:::::.::#::#\n" +
                "#::#::::.::::.:::::::::::.......:::::#\n" +
                "#::##:::......:::::::~~~~~:::::.:,,,:#\n" +
                "#::##:::,,,,,::::::~~~~~~~~~:::.,,,,,#\n" +
                "#::#::,,,,,,,,,:::~~~~~~~~~~~::.,,,,,#\n" +
                "#::#::,,,,,,,,,:::~~~~~C~~~~~::.,,,,,#\n" +
                "##C##:,,,,,,,,,:::~~~~..~~~~~::.,,,,,#\n" +
                "##,,,:::,,,,,::::::~~~..~~~~:::.,,,,,#\n" +
                "##,,,,::.::::::::::::~....::#::.:,,,:#\n" +
                "#####:::.::::::::#::.......:#::.:::::#\n" +
                "#~~~~~::.:::~~~~~~::::::~~~~~~:.::::~#\n" +
                "#~~~~~~..~~~~~~~~~~~~~~~~~~~~~..~~~~~#\n" +
                "#:::::~..~~~::::::~~~~~~::::::..~~~~:#\n" +
                "#::,,,,,:::::#####::::::::::::..:::::#\n" +
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,:::::::#\n" +
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,:::::::#\n" +
                "#:,,,,,,,:::::::::::.::,,,,,::##,,####\n" +
                "#::,,,,,.............:,,,,,,,:##,,,,##\n" +
                "#:::::::::::.:,,,,,::::,,,,,::##,,,,##\n" +
                "#:::#:::::::.,,,,,,,::::::::::##,,,,##\n" +
                "#::#::::::::.:,,,,,::::::#::::##,,,C##\n" +
                "#:::::::::::.:::::::::::::#:::########\n" +
                "###########..############::::::::::::#\n" +
                "#:::::::::::.:::::::::::#::,,,,,,,:::#\n" +
                "#:#::#.........SSS.#:#::#:,,,,,,,,,::#\n" +
                "#::::...E...F..SSS..::::#::,,,,,,,:::#\n" +
                "#::#:......N........::::#::::::::::::#\n" +
                "#::::............N...................D\n" +
                "#::::.......P........................#\n" +
                "#::::...N...........::::#::,:::::::::#\n" +
                "#:~~:#.............#::::#::,,,,,,,,,##\n" +
                "#:~~::::::::::::::::::#:#::,,,,,,,,,##\n" +
                "#:::::::::::::::::::::::#::#,,,,,,,C##\n" +
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
                "####,,,#########GGG#########C#####\n" +
                "####,,,,,,,,,,,,,,,,,,,,,,,,,,####\n" +
                "####,,,,,,##,,,,,,,,,,##,,,,,,####\n" +
                "####,,,,,,,,,,,,,,,,,,,,,,,,,,####\n" +
                "################,,,###############\n" +
                "################...###############\n" +
                "##C,::::::::::##...#,,,,,,,,~~~~##\n" +
                "##,,::::::::::##...#,,,,,,,,~~~~##\n" +
                "##::#:::#:::#:##...#,,,~~~,,~~~~##\n" +
                "##::#:::#:::#:,,...,,,~~~~~,,,,,##\n" +
                "##:::::::::::,,,,.,,,,~~~~~,,,,C##\n" +
                "##::::::::::::##...#,,~~~~~,,,,,##\n" +
                "##::#:::#:::#:##...#,,,~~~,,~~~,,#\n" +
                "##::::::::::::##...#,,,,,,,,~~~,,#\n" +
                "##K:::::::::::##...#,,,,,,,,~~~,,#\n" +
                "####..##########...#,,,,,,,,,,,,##\n" +
                "####..##########...######,,#,,,###\n" +
                "####..####::::::::::::::#,,#######\n" +
                "####.....::::::::F:::::::##..C####\n" +
                "#####....::::::::::::::::.N..#####\n" +
                "#########:::::::::::L::::....#####\n" +
                "##########::::::...:::::##########\n" +
                "##C,,,,,,,,,,,,,...,,,,,,,,,,,,,##\n" +
                "###,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "#####,,###,,##############,,######\n" +
                "##,,,,,,,,,,,,,~~~~~~~,,,,,,,,,,##\n" +
                "##,,,,,,,,,,,,,~~~~~~~,,,,,,,,,,##\n" +
                "##,,#######,,########,,######,,###\n" +
                "##,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "##,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,##\n" +
                "#######,,#######...#####,,########\n" +
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
                "#############################D######\n" +
                "###########################:::::####\n" +
                "########################:::::B:::::#\n" +
                "##C,,,,#################::.......::#\n" +
                "##,,,,,#################::.......::#\n" +
                "##,,,,,##,,,,,C###:::::#::.......::#\n" +
                "##,,,,,##,,,,,,###:::::##:.......:##\n" +
                "##,,,,,##,,,,,,####:::#####:::::####\n" +
                "##,,,,,###,#:::::##:::::###GGG###~~#\n" +
                "##,,,,,,,,,:::::::##:::::.....###~~#\n" +
                "##,,#,,,#,,:::::::######,,,######~~#\n" +
                "###,,,,,,,,:::::::######,,,####,,,~#\n" +
                "##,,,,####,,:::::#######,,,###,,,,C#\n" +
                "##,,,#####,,,::#########,,,##,,,,~~#\n" +
                "##,,,#####,,,:K#########,,,##,,##~~#\n" +
                "##,,,#####,,,############......##~~#\n" +
                "#,,##,,##,,,,#:::::~~~###:::#,,,#~~#\n" +
                "##,,,,,,,,,,,::::::~~~##:::::,,,#~~#\n" +
                "##,,#,,,,,,,,:~~~::~~~##::::::,,#~~#\n" +
                "##,,,,,,,,,::~~~~~:::::#:::::,,,#~~#\n" +
                "##,,,,#,,,,#::~~~~:#:::##:::#,,,#~~#\n" +
                "###..#######:::::C:::::######,,,#~~#\n" +
                "###..##:::::#::#::~~~::####....##~~#\n" +
                "###..::::::::::::~~~~~####,,,####~~#\n" +
                "####:::::::::##:::~~~#####,,,####~~#\n" +
                "###..#:::::::###::########,,,####~~#\n" +
                "###..##:::::####::########,,,####~~#\n" +
                "###..###########:::::###..,,,#######\n" +
                "####.....########::::::.....########\n" +
                "#####....############,,,############\n" +
                "#######..############,,,############\n" +
                "#######..############,,,######,,,C##\n" +
                "#######..##########..,,,#####,,,,,##\n" +
                "######:...:######......#####,,,,,,,#\n" +
                "######:...SSS###,,,#########,,,,,,,#\n" +
                "####E:::F:SSS###,,,#########,,,,,,,#\n" +
                "###::.......::..,,,##########,,,,,##\n" +
                "#..::......N......############,,,,##\n" +
                "D..::P......::##..#,,,,#,,#,,#,,,,##\n" +
                "####::::L::::###...,,,,,,,,,,,,,,###\n" +
                "######:::n:######..............,,,##\n" +
                "########C##########,,,,,,,,,,,,,,,##\n" +
                "###################,,#,,,,#,,,,,,C##\n" +
                "####################################\n";

        // 1.1 메아리 정글 (#80, GDD 9-6) 72×88 — 원본은 python Tools/jungle_map.py (Builds/maps/JungleMap.txt를 여기로 옮김).
        // . 진흙길  : 이끼 땅  , 짙은 풀숲(보이는 몬스터 서식지)  # 정글 나무  ~ 깊은 강·골짜기  > < ^ v 얕은 강(물살)  J 나루터(뗏목)
        // Y 레버  H 덩굴 다리  U 유적 돌벽  ; 유적 바닥  T 단어 석판  A 유적 제단(아라)  O 동굴 입구(1.2)  G 보스 방 문  K 열쇠
        // 남서 야영지(D·P·F·S·E·L) → 얕은 강 → 북서 고대 유적 · 북쪽 빈칸두꺼비 공터 · 동쪽 깊은 강(섬·모래톱 상자) · 남동 버섯 늪
        public const string JungleMap =
                "###############################################################################\n" +
                "###############################################################################\n" +
                "##################################################~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "##################################################~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "####UUUUUUUUUUUUUUUUUUUUUUUU######################~~~~~~~~~~~~~~~~~:::::###::##\n" +
                "####U;;;;;UUUUUUUUUU;;;;;;;U###########::#########~~~~~~C~~~~~~~~~~::::::######\n" +
                "####U;C;;;U;;;;;;;;U;;;;;C;U#######:::::::::######~~~~~~~~~~~~~~~~~::::::######\n" +
                "####U;;;;;U;;;;;;;;U;;;;;;;U######:::::::::::#####~~~~~~~~~~~~~~~~~::::K###::##\n" +
                "####U;;Q;;U;;;A;;;;U;;;Q;;;U#####:::::::::::::####~~~~~~~~~~~~~~~~~:::::##:::##\n" +
                "####U;;;;;U;;;;;;;;U;;;;;;;U####:::::::B:::::::###~~~~~~~~~~C~~~~~~:::,:##,::##\n" +
                "####U;;;;;U;;;;;;;;U;;;;;;;U####:::::::::::::::###~~~~~~~~~~~~~~~~~:Q,,,,,,::##\n" +
                "####U;;Q;;U;;;;;;;;U;;;Q;;;U###:::::::::::::::::##~~~~~~~~~~~~~~~~~,,:::,,:::##\n" +
                "####U;;;;;U;;;;;;;;U;;;;;;;U###:::::::::::::::::##~~~~~~~~~~~~~~~~~::,:#:::::##\n" +
                "####U;;;;;UUUU;;UUUU;;;;;;;U####:::::::::::::::###~~~~~~~~~~~~~~~~~:::##:::::##\n" +
                "####U;;;;;;;;;;;;;;;;;;;;;;U#####:::::::::::::####~~~~~~~~~~~~~~~~~:::####:::##\n" +
                "####UUUUUU;;Q;;;;Q;;UUUUUUUU######:::::::::::#####~~~~~~~~~~~~~~~~JJ:#####:::##\n" +
                "####UUT;;U;;;;;;;;;;U;;;;TUU#######:::::::::######~~~~~~~~~~~~~~~~JJ:####::::##\n" +
                "####UU;;;;;;;;;;;;;;;;;;;;UU##########:::#########~~~~~~~~~~~~~~~~~:::#::::::##\n" +
                "####UU;;;;;;;;;;;;;;;;;;;;UU##########:::#########~~~~~~~~~~~~~~~~~:Q::::::::##\n" +
                "####UU;;;U;;;;;;;;;;U;;;;;UU##########GGG#########~~~~~~~~~~~~~~~~~:::::::#::##\n" +
                "####UUUUUU;;;;;;;;;;UUUUUUUU##########...#########~~~~~~~~~~~~~~~~~::::::#:::##\n" +
                "####U;;;;;;;;;;;;;;;;;;;;;;U##########...#########~~~~~~~~~~~~~~~~~::::::#:::##\n" +
                "####U;;;;;;;;;;;;;;;;;;;;;;U##########...#########~~~~~~~~~~~~~~~~~:::::::#####\n" +
                "####UUUUUU;;;;;;;;;;UUUUUUUU##########...#########~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "####UUT;;U;;;;;;;;;;U;;;;TUU##########...#########~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "####UU;;;;;;;;;;;;;;;;;;;;UU##########...#########~~~~~~~~~~~~~~~~~::#:::##::##\n" +
                "####UU;;;;;;;;;;;;;;;;;;;;UU##########...#########~~~~~~~~~~~~~~~~~::##::##::##\n" +
                "####UU;;;U;;Q;;;;Q;;U;;;;;UU##########...#########~~~~~~~~~~~~~~~~~#::::#:::,##\n" +
                "####UU;;;U;;;;;;;;;;U;;;;;UU##########...#########~~~~~~~~~~~~~~~~~##::::,,,:##\n" +
                "####UUUUUU;;;;;;;;;;UUUUUUUU##########...#########~~~~~~~~~~~~~~~~~###::,,,,,##\n" +
                "####UUUUUUUUUU;;UUUUUUUUUUUU##########...#########~~~~~~~~~~~~~~~~~::::,,,,,,##\n" +
                "##############;;######################...#########~~~~~~~~~~~~~~~~~:::::,,,,:##\n" +
                "###~~~~~~~~~~~HH~~~~~~~~~~~~~#########...#########~~~~~~~~~~~~~~~~~::,,,,,#####\n" +
                "###~~~~~~~~~~~HH~~~~~~~~~~~~~#########...#########~~~~~~~~~~~~~~~~~::::,::#::##\n" +
                "##::::::::###:..::::::::::::::::#:,:::..::::::::::~~~~~~~~~~~~~~~~~:::,,::#::##\n" +
                "##:::::::::##:..:::::::::::::::,,:::....:::::,::::~~~~~~~~~~~~~~~~~,Q::::::::##\n" +
                "##:::C:::::#::...:Y:::::::::::,,,#,....,,,:,,C#:::~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "######::::::::....::::::::Q::,,,,....::,,,,,,,#:::~~~~~~~~~~~~~~~~~,:::::::::##\n" +
                "##::###,,##::#::.....::::::,,,,.....:::,:,Q,,,##::~~~~~~~~~~~~~~~~~,,,:::::Q:##\n" +
                "##::##,,,::##::::.....::::,,,.....:::::,,:,,,,:#::~~~~~~~~~~~~~~~~~,,,,###:::##\n" +
                "##:::#,,Q::#::::::::....:::.....::::::::::,,::::::~~~~~~~:::~~~~~~~:,,,####::##\n" +
                "####:::,,,,#::::::#::...:.....,,,##:::::::,:::::::~~~~~~::O::~~~~~~::,,########\n" +
                "#####:,,,,::::#::###::......,##,:##::Q::::,,###:::~~~~~~:::::~~~~~~:::,##:#::##\n" +
                "##::#:,::,:::###::##::....,,,::###::::::::####::::~~~~JJ:::::JJ~~~~:::,,,:#::##\n" +
                "##:::,::::::#####::#::..:,,,,::#::::::::::::#:::::~~~~JJ:::::JJ~~~~:::,,,C#::##\n" +
                "##:::::::::::####::##:..:::,::##::::::::::::::::::~~~~~~:::::~~~~~~::::,:::::##\n" +
                "##:>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>~~~~~~:::::~~~~~~::::Q:::::##\n" +
                "##:>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>~~~~~~:::::~~~~~~::::::::::##\n" +
                "##:>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>~~~~~~~:::~~~~~~~::::::::::##\n" +
                "##::::::::::::::::::::..::::::::,,,,,:::::::::::::~~~~~~~~~~~~~~~~~####::::::##\n" +
                "##::::::,:::::::::Q:::..::::::::,,,C,:::::::::::::~~~~~~~~~~~~~~~~~##::::#:::##\n" +
                "##:::::,,,,,,:::::::::..::::#:::,,,,,:::::::::::::~~~~~~~~~~~~~~~~~:,,::::#::##\n" +
                "##:::,:,,,,,,::::,,::...:::###:::,,,,::::Q::::::::~~~~~~~~~~~~~~~~~,,,::::#####\n" +
                "##::::Q,,,,,::::,,,.....::::###:Q,,,::::::,:::::::~~~~~~~~~~~~~~~~~,Q:,::######\n" +
                "##:::::::,,:::::......:....::##::::::::::::,,,Q:::~~~~~~~~~~~~~~~~~,:,,:::#::##\n" +
                "##::::::::::::......:::..........::::::::::,,,::::~~~~~~~~~~~~~~~~~::,,,:::::##\n" +
                "##::::::::::.....,,:::::::...........::::,,,,,::::~~~~~~~~~~~~~~~~~,::,::::::##\n" +
                "##::::::::::...:,,::::::::::::::.........:,,,,,:::~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "##::::::::::..::::::::::::::::::::::.......:,,,,::~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "###########...##########################...#######~~~~~~~~~~~~~~~~~::,Q,::#::##\n" +
                "##::::::::::..:::::::::::::~~::##::::::::..,,,:,:#~~~~~~~~~~~~~~~~~,:,,,#######\n" +
                "##::::::::::..:::::::::::::~~::##:Q::::::...,:,::#~~~~~~~~~~~~~~~~~,,##########\n" +
                "##::::::::::..:::::::::::::~~:::::::::::::..::::##~~~~~~~~~~~~~~~~~::##,::#::##\n" +
                "##::::::::::..:::::::::::::~~:::::::::::,:..::::##~~~~~~~~~~~~~~~~~::##,:::::##\n" +
                "##::::::::::..:::::::::::::~~::::::::::,::...::#::~~~~~~~~~~~~~~~~~::::,:::::##\n" +
                "##::::::::::..:::::::::::::~~:::::::,:,,,::...::::~~~~~~~~~~~~~~~~~::::,:::::##\n" +
                "##::::::::::..:::::::::::::~~::::Q::,,,,,:::..::::~~~~~~~~~~~~~~~~~:Q:,C:::::##\n" +
                "##::::::::::..:::::::::::::~~::::::,#,,::Q::...:::~~~~~~~~~~~~~~~~~:::::::Q::##\n" +
                "##::::::::::..:::::::::::::~~::::,,,###,:::::...::~~~~~~~~~~~~~~~~~::###:::::##\n" +
                "##::::::::::..:::::::::::::~~:::::,,,####:::::..:JJ~~~~~~~~~~~~~~~~:::###::::##\n" +
                "##::::::Q:::..::::Q:::n:n::~~:::::,,,####:::::...JJ~~~~~~~~~~~~~~~~::,:#:::::##\n" +
                "##::::::::F:..:::::::::n:::~~:::::,:::####:::::..:~~~~~~~~~~~~~~~~~:,,:,:#:::##\n" +
                "##:::::::::...::SSS:::::Y::~~::Q::::::##::#...::::~~~~~~~~~~~~~~~~~,,,#::::::##\n" +
                "##:::E::::...:::SSS::::::::~~::::::::###::....::Q:~~~~~~~~~~~~~~~~~,:,,##::::##\n" +
                "##::::::::..:::::::::::::::HH...::::::#.....::::::~~~~~~~~~~~~~~~~~:::,,##:::##\n" +
                "##::::::::.N:::::::::::::::HH.......::.....:::::::~~~~~~~~~~~~~~~~~:::,:,,:::##\n" +
                "##:::::::...::::::::Q::::::~~::.........,:::::,:::~~~~~~~~~~~~~~~~~::::,,::::##\n" +
                "##::::::...::::::::::::::::~~::::::....,,,::,,,:::~~~~~~~~~~~~~~~~~::::,:#:::##\n" +
                "D.....P...:::::::::::::::::~~:::::::,,,,,:,,,,,C::~~~~~~~~~~~~~~~~~::,Q,,:#::##\n" +
                "#.........:::::::::::Q:::::~~::#:::,#,,,#::,,,::,:~~~~~~~~~~~~~~~~~:::,::::::##\n" +
                "##:::::::::::::::::::::::::~~::#:::###:,:#:,,,::::~~~~~~~~~~~~~~~~~::::::::::##\n" +
                "##:::::::::::::::Q:::::::::~~:::#####,,,,#:Q,,::::~~~~~C~~~~~~~~~~~:::#::::::##\n" +
                "##:::::::::::::L:::::::::::~~::::C::#,,:##,,,:::Q:~~~~~~~~~~~~~~~~~::::#::C::##\n" +
                "##:::::::::::::::::::::::::~~::::::,#:::#:,,:,::::~~~~~~~~~~~~~~~~~::#::#::::##\n" +
                "##::::::Q::::::::::::::C:::~~:::::::::::::::,:::::~~~~~~~~~C~~~~~~~:::#::#:::##\n" +
                "##:::::::::::::::::::::::::~~:::::::::::::::::::::~~~~~~~~~~~~~~~~~:::#::#:::##\n" +
                "###############################################################################\n" +
                "###############################################################################\n";

        // 1.2 어두운 밀림 동굴 (#115, GDD 9-7) 50×62 — 원본은 python Tools/cave_map.py (Builds/maps/CaveMap.txt를 여기로 옮김).
        // . 다져진 길  : 바위 바닥  , 빛이끼 덤불(보이는 몬스터 서식지)  # 바위벽  ~ 먹물 웅덩이·개울(먹물도롱뇽이 숨음)  I 글자 반딧돌
        // Y 레버  H 덩굴 다리  Q 꾸밈  G 보스 방 문  K 열쇠  D 출입구(정글 섬으로)
        // 아래 출입구 → 반딧불 쉼터(F·S·E·L) → 대동굴 → 서쪽 고리(버섯 굴·먹물 호수·개울가 굴 레버) · 동쪽 고리(거미 굴·수정 굴 열쇠) → 맨 위 보스 방
        public const string CaveMap =
                "##################################################\n" +
                "##################################################\n" +
                "##################################################\n" +
                "########################:::#######################\n" +
                "####################:::::::::#####################\n" +
                "###################:::::::::::::##################\n" +
                "######:::::::#####:::::::::::::::#################\n" +
                "#####::::C::::####::::::::::::::::################\n" +
                "#####:::::::::####:::::::B::::::::################\n" +
                "######:::::::#####:::::::::::::::#####:::::::#####\n" +
                "#######:::::#######::::::::::::::###C:::::::::####\n" +
                "########:::########::::::::::::#####:::::::::::###\n" +
                "#########::#########::::::::::######:::::::::::###\n" +
                "########:::#############:::##########:I:::::::####\n" +
                "########::##############:::##########::::::::#####\n" +
                "########::#::###########:::###########:::::::#####\n" +
                "#######:::::::##########GGG#############::::######\n" +
                "####::::::::::##########...########:#####:::######\n" +
                "####::::I::::::#########...#######::######:::#####\n" +
                "###::::::::::::#########...#####:::C::####:::#####\n" +
                "###:::::::,:,Q:#########...####::::::######:::####\n" +
                "##::C::,:::,,::#######::...::##::::::#######::####\n" +
                "####,,,,,:::,,,######I:::.::::###:::########:::###\n" +
                "####,,:,:::::,#######::::.::::###::#########:::###\n" +
                "#######:::Q:::#######::::.:::###:::#########,,,###\n" +
                "######::::::::#########::.:#####::##########,,####\n" +
                "######:::##::::#####,,,,:.:::##::,#########::,####\n" +
                "#####:::####:,:##:,,,,,,..::::::,,,:#####:::,:####\n" +
                "#####:Q:#####,::#:,,Q,,:..::::,:,,,::###:::,,::###\n" +
                "#####:::######,::~~::,,:..::::Q,,:,:::#::Q,,::::##\n" +
                "#####:::#######,:~~~~:::.I::::,,,,:::::::,,:::::##\n" +
                "######:::######,:::::::..:::::,:::::::::::,,::I::#\n" +
                "######:::######::::::::..:::::::::Q::::::,,,:::###\n" +
                "######:Q:######:,C:::::..::::Q:::::::###::,,,,,###\n" +
                "######:::::::###::,:,:::..:::::::::::####:,,,,,###\n" +
                "####:::::::::,,#,,:,:::::..:::~~~:::#####C:,,,####\n" +
                "####:::::::::,,,,:#,:Q:::..:::~~~::########,:,####\n" +
                "####,,:::::~,:,Q:###::::::.:::::~###########:::###\n" +
                "####I,:::~~~~~:::###::::::..::::############:::###\n" +
                "####,,,,:~~~~~::::####::::..:###############:::###\n" +
                "####,,,:~~~~~~~:::######::..###############:::####\n" +
                "####:,::~~~~~~~:::#####::...###############:::####\n" +
                "####,Q::~~~~~~~:Q:####::...############:,::::#####\n" +
                "####:,:::~~~~~::::###:....############,,,,::::####\n" +
                "####:::::~~~~~:::####...:#############,,,,:,:::###\n" +
                "#####::::::~~~::#####..:##############C,,:::,I:###\n" +
                "#####::Q::::~~::#####..::############,:,~~::,,,,##\n" +
                "#####:::::::~~#######..:::###########::~~~::,,::##\n" +
                "####:::::::::~~#######..:::##########:::~:::,,,:##\n" +
                "####:::##::::~~~####::..:::::#:#######::.:Q:,,,###\n" +
                "####::::::####~~###::::..:::::::######:..::::#####\n" +
                "####:::Q:::::#~~##:::::...::::::::###::.:#########\n" +
                "###:::::::::::~~##::F:::..::::::::###:..:#########\n" +
                "###:::::::::::HH:::::::::.:::::::::::..:::########\n" +
                "###:::::::::::HH::#:::n::.:::::.....:..#:::::#####\n" +
                "###C:I:,,::::#~~###::::::.:::::::....##:::::::::##\n" +
                "####:::,,,Y::#~~###::E:::P:::L::#######:::::::::##\n" +
                "#####,,,,,::##~~##::C::::.:::##########:::::::C::#\n" +
                "#####,:,:,::##~~#####:##...#############:::::::###\n" +
                "########################...##############::::::###\n" +
                "########################...#######################\n" +
                "#########################D########################\n";

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

        // 보스 방 문의 열쇠·필요한 단어 수 (#76, 비어 있을 때만)
        private static void SetBossGate(string areaPath, ItemData key, int words)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || area.BossKey != null) return;
            var so = new SerializedObject(area);
            Prop(so, "bossKey").objectReferenceValue = key;
            Prop(so, "bossGateWords").intValue = words;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 원본 맵에 보스 방 문(G)·열쇠(K)가 생겼을 때 (#76): 에셋의 맵이 그 칸만 빼고 원본과 같으면 원본으로 바꾼다
        // (인스펙터에서 따로 고친 맵은 건드리지 않음 — 그때는 직접 옮길 것)
        private static void AddGateToMap(string areaPath, string map)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            var prop = Prop(so, "map");
            string current = prop.stringValue.Replace("\r\n", "\n"), source = map.Replace("\r\n", "\n");
            if (current == source || current.IndexOf('G') >= 0 || current.Length != source.Length) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != 'G' && source[i] != 'K' && source[i] != current[i]) return;
            prop.stringValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 도망 등급: 기본값(약함)일 때만 정함 (인스펙터에서 바꾼 값은 유지)
        private static void SetFleeTier(FleeTier tier, params MonsterSpecies[] species)
        {
            foreach (var monster in species)
            {
                if (monster == null || monster.FleeTier != FleeTier.Weak || tier == FleeTier.Weak) continue;
                var so = new SerializedObject(monster);
                Prop(so, "fleeTier").enumValueIndex = (int)tier;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 원본 맵에 새 칸(added 글자들)이 생겼을 때: 에셋의 맵이 그 칸만 빼고 원본과 같으면 원본으로 바꾼다 (#80 숲의 정글 출입구 D)
        // 말을 잃어 가는 사람들 (#209, GDD 9-10). 맵의 N·n을 위→아래, 왼→오른 순서로 채움
        private static void BuildNpcs(string meadowPath, string libraryPath, string forestPath, string junglePath, string cavePath)
        {
            FieldArea Load(string path) => AssetDatabase.LoadAssetAtPath<FieldArea>(path);
            var library = Load(libraryPath);
            var forest = Load(forestPath);
            var cave = Load(cavePath);

            var elder = Npc("npc_elder", "어르신", 0,
                new NpcStage("……", NpcMotionKind.Sit),
                new NpcStage("내 사전을… 마을 위 어디 뒀더라.", NpcMotionKind.Stroll, library, 0, 0.75f)); // 노인: 아주 천천히
            // #223 사용자: 서고 보스 뒤 다른 샘으로 갈 수 있게 되면, 가까이 갔을 때 한 번 — 샘 이야기를 하고 샘으로 쓩 (다시 안 나옴)
            elder.SetSpringLeap(library,
                "내 사전은 아직 못 찾았지만… 샘 길은 기억났단다!",
                "샘물은 땅 밑으로 다 이어져 있지. 옛날엔 다들 이렇게 다녔어.",
                "얍!");
            EditorUtility.SetDirty(elder);
            var chief = Npc("npc_chief", "촌장", 0,
                new NpcStage("자네 이름이… 펜… 아니, 뭐였더라?", NpcMotionKind.Pace, null, 0, 0.7f), // 뒷짐 지고 느긋하게
                new NpcStage("{이름}! 그래, 그 이름이야!", NpcMotionKind.Stand, library));
            // #222 사용자: '첫 글자가 뭐였지?'는 부탁(서브퀘스트)처럼 들림 → 땅에 이름을 쓰는 아이의 혼잣말, 단어를 찾을수록 한 글자씩
            var child = Npc("npc_child", "아이", 0,
                new NpcStage("땅에 내 이름을 쓰는 중이에요. 첫 글자가… 음…", NpcMotionKind.Sit),
                new NpcStage("첫 글자가 생각났어요! 그다음은…", NpcMotionKind.Sit, null, 25),
                new NpcStage("제 이름 두 글자가 생각났어요. 마지막 하나가 뭐였더라…", NpcMotionKind.Sit, null, 50), // #232 사용자
                new NpcStage("이제 세 글자 다 썼어요!", NpcMotionKind.Wander, null, 75, 1.6f)); // 신나서 뛰어다님
            var librarian = Npc("npc_librarian", "사서", 0,
                new NpcStage("쉿. 소리 내어 읽으면 도깨비들이 와요.", NpcMotionKind.Pace, null, 0, 0.55f), // 발소리를 죽여 살금살금
                new NpcStage("서쪽 열람실 구석은 보셨나요?", NpcMotionKind.Pace, null, 40, 0.55f));
            var woodcutter = Npc("npc_woodcutter", "나무꾼", 0,
                new NpcStage("길을 내가 잃었다, 아니 나를 길이…", NpcMotionKind.Wander, null, 0, 1.25f), // 허둥지둥
                new NpcStage("이정표가 돌아왔군. 늪 너머 공터를 가 봐.", NpcMotionKind.Pace, forest));
            // #234 사용자 ⑲: 숲에 처음 들어오면 달려와 상황을 알려 줌 · 헷갈너구리 뒤 정글 쪽 길 옆에서 배웅 (할머니 = 아라, 이스터에그 — 말로는 밝히지 않음)
            woodcutter.SetCutscenes(
                new[]
                {
                    "이보게, 거기! 잠깐 멈추게!",
                    "이 숲에선 길을 잃기 쉬워. 헷갈너구리 녀석이 이정표를 몽땅 뒤섞어 놨거든.",
                    "나도 벌써 사흘째 같은 자리를 맴돌고 있다네…",
                    "녀석은 북쪽 공터에 숨어 있어. 공터 문은 잠겼는데, 열쇠가 숲 어딘가에 떨어져 있다더군.",
                    "숲의 단어를 많이 찾을수록 힘이 붙을 걸세. 부디 이정표를 되찾아 주게!"
                },
                new[] { "이정표가 돌아왔군!", "고맙네. 덕분에 이제 길을 헷갈릴 일이 없겠어." },
                new[] { "이 너머로 갈 생각인가?", "와서 들어 보게나. 이상한 메아리 소리가 들리지 않나?" },
                new[] { "내 할머니가 메아리를 따라가다가 이 너머에서 실종되었다지…", "위험이 도사리고 있으니 조심하는 게 좋을 거야.", "행운을 빌겠네!" });
            EditorUtility.SetDirty(woodcutter);
            var mule = Npc("npc_mule", "노새", 23,
                new NpcStage("히힝… 입가에 사과 조각이 붙어 있다.", NpcMotionKind.Stand));
            var frog = Npc("npc_frog", "개구리", 16,
                new NpcStage("개굴! (다른 개구리들이 메아리처럼 따라 운다)", NpcMotionKind.Hop)); // #232 사용자: '표결 중'은 뜻이 안 와닿음
            var pilgrim = Npc("npc_pilgrim", "순례자", 0,
                new NpcStage("강을 ___ 건너면 ___에 ___가 있어.", NpcMotionKind.Sit),
                new NpcStage("강을 뗏목으로 건너면 동쪽 기슭에 열쇠가 있어.", NpcMotionKind.Pace, null, 60, 0.65f)); // 경건하게
            // #232 사용자: 움직임이 이상함 → 가만히 서 있고, 말을 걸면 바라보기만
            var miner = Npc("npc_miner", "광부", 0,
                new NpcStage("나가는 길은 저쪽으로 쭉 가서…", NpcMotionKind.Stand),
                new NpcStage("동굴이 환해졌어! 다시 일할 수 있겠다!", NpcMotionKind.Stand, cave));

            SetNpcs(meadowPath, elder, chief, child);
            // 서고 보스 뒤 [돌아가기]는 초원 시작 지점으로 (#232 사용자)
            var librarySo = new SerializedObject(library);
            Prop(librarySo, "returnAfterBoss").objectReferenceValue = Load(meadowPath);
            librarySo.ApplyModifiedPropertiesWithoutUndo();
            SetNpcs(libraryPath, librarian);
            SetNpcs(forestPath, woodcutter, mule);
            SetNpcs(junglePath, frog, frog, frog, pilgrim);
            SetNpcs(cavePath, miner);

            // 상점의 상인과 호객 말 (#210). 그림 = Npcs/{id}.png, 첫 단계의 움직임만 씀
            SetMerchant("meadow_shop", Npc("npc_merchant_meadow", "잡화점 아주머니", 0, new NpcStage("", NpcMotionKind.Busy)),
                "상처약 있어요, 상처약!", "거기 모험가님, 그냥 가시게요?", "풀숲 가기 전에 하나 챙겨 가요!");
            SetMerchant("forest_shop", Npc("npc_merchant_forest", "약초 할아버지", 0, new NpcStage("", NpcMotionKind.Stand)),
                "이 숲에서 길 잃으면 약도 없어요! 약 사 가요!", "특제 상처약 막 들어왔어요!", "숲이 깊어요. 상처약 넉넉히 챙겨 가요!");
            SetMerchant("jungle_shop", Npc("npc_merchant_jungle", "원숭이 상인", 0, new NpcStage("", NpcMotionKind.Hop, null, 0, 1.5f)),
                "끽끽! 뗏목 타기 전에 하나 챙겨요!", "끽! 살금살금 물약이 정글엔 필수라니까!", "끽! 호기심 물약 마시면 새 단어가 쏟아진대요!",
                "끽! 여기가 제일 싸요, 정글에 여기밖에 없지만!");
        }

        private static void SetMerchant(string shopId, NpcData merchant, params string[] pitches)
        {
            var shop = AssetDatabase.LoadAssetAtPath<ShopData>($"{Root}/Shops/{shopId}.asset");
            if (shop == null) return;
            var so = new SerializedObject(shop);
            Prop(so, "merchant").objectReferenceValue = merchant;
            var list = Prop(so, "pitches");
            list.arraySize = pitches.Length;
            for (int i = 0; i < pitches.Length; i++) list.GetArrayElementAtIndex(i).stringValue = pitches[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static NpcData Npc(string id, string name, int animalFrameWidth, params NpcStage[] stages)
        {
            var path = $"{Root}/Npcs/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NpcData>(path);
            if (existing == null)
            {
                EnsureFolder($"{Root}/Npcs");
                existing = ScriptableObject.CreateInstance<NpcData>();
                AssetDatabase.CreateAsset(existing, path);
            }
            existing.Configure(id, name, animalFrameWidth, stages);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void SetStoryOrder(string areaPath, int order)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            Prop(so, "storyOrder").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetNpcs(string areaPath, params NpcData[] npcs)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            var list = Prop(so, "npcs");
            list.arraySize = npcs.Length;
            for (int i = 0; i < npcs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = npcs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 보스 대사 (#207)
        private static void SetBossLines(string areaPath, string bossLine, string heroLine, string afterLine)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            var boss = Prop(so, "boss");
            boss.FindPropertyRelative("bossLine").stringValue = bossLine;
            boss.FindPropertyRelative("heroLine").stringValue = heroLine;
            boss.FindPropertyRelative("afterLine").stringValue = afterLine;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBossFieldSize(string areaPath, float width, float height)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            Prop(so, "boss").FindPropertyRelative("fieldSize").vector2Value = new Vector2(width, height);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRelicDescriptionIf(string relicId, string from, string to)
        {
            var relic = AssetDatabase.LoadAssetAtPath<RelicData>($"{Root}/Relics/{relicId}.asset");
            if (relic == null) return;
            var so = new SerializedObject(relic);
            var description = Prop(so, "description");
            if (description.stringValue != from) return;
            description.stringValue = to;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 이미 있는 지역에 상자를 더했을 때 (#200): 내용물 수가 예전 그대로면 맵 순서 자리에 끼운다
        private static void InsertChestIf(string areaPath, int oldCount, int index, ItemData item)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            var chests = Prop(so, "chests");
            if (chests.arraySize != oldCount) return;
            chests.InsertArrayElementAtIndex(index);
            var chest = chests.GetArrayElementAtIndex(index);
            chest.FindPropertyRelative("item").objectReferenceValue = item;
            chest.FindPropertyRelative("count").intValue = 1;
            chest.FindPropertyRelative("gold").intValue = 0;
            chest.FindPropertyRelative("relic").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 단어장에 누군가의 기록 연결 (#200)
        private static void SetJournal(string wordBookPath, ItemData journal)
        {
            var book = AssetDatabase.LoadAssetAtPath<WordDatabase>(wordBookPath);
            if (book == null) return;
            var so = new SerializedObject(book);
            Prop(so, "journal").objectReferenceValue = journal;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpgradeMapIf(string areaPath, string map, string added)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null) return;
            var so = new SerializedObject(area);
            var prop = Prop(so, "map");
            string current = prop.stringValue.Replace("\r\n", "\n"), source = map.Replace("\r\n", "\n");
            if (current == source || current.Length != source.Length) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != current[i] && added.IndexOf(source[i]) < 0) return;
            prop.stringValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 도착 지역 맵에 출입구가 늘어 번호가 밀렸을 때: 그 연결의 도착 번호가 from이면 to로
        private static void SetExitDoorIf(string areaPath, int exitIndex, int from, int to)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            if (area == null || exitIndex >= area.Exits.Count || area.Exits[exitIndex].TargetDoorIndex != from) return;
            var target = area.Exits[exitIndex].Target;
            if (target == null || target.Map.Doors.Count <= to) return;
            var so = new SerializedObject(area);
            Prop(so, "exits").GetArrayElementAtIndex(exitIndex).FindPropertyRelative("targetDoorIndex").intValue = to;
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

        // 이미 있는 지역이 예전 단어장(from)을 쓰고 있으면 새 단어장(to)으로 (#62 서고가 초원 단어장을 같이 쓰던 것)
        private static void SwitchWordBookIf(string areaPath, string fromPath, string toPath)
        {
            var area = AssetDatabase.LoadAssetAtPath<FieldArea>(areaPath);
            var from = AssetDatabase.LoadAssetAtPath<WordDatabase>(fromPath);
            var to = AssetDatabase.LoadAssetAtPath<WordDatabase>(toPath);
            if (area == null || to == null || area.Words != from) return;
            var so = new SerializedObject(area);
            Prop(so, "words").objectReferenceValue = to;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private const string CommonWords = "Assets/Data/Words/tier1_common.asset", AdvancedWords = "Assets/Data/Words/tier2_advanced.asset";

        private static void ConfigureRegion(string wordBookPath, string regionId, string regionName, params DexReward[] rewards)
        {
            var book = AssetDatabase.LoadAssetAtPath<WordDatabase>(wordBookPath);
            if (book == null)
            {
                Debug.LogWarning($"[WordRPG] 단어장이 없어 지역 설정을 건너뜀: {wordBookPath}");
                return;
            }
            if (!string.IsNullOrEmpty(book.RegionId)) return;

            book.ConfigureRegion(regionId, regionName, rewards);
            EditorUtility.SetDirty(book);
        }

        private static ItemData Item(string id, string name, string description, Color color,
            ItemKind kind = ItemKind.Material, int healAmount = 0, SkillData taughtSkill = null, ItemEffect effect = ItemEffect.None, int effectAmount = 0)
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
                Prop(so, "effect").enumValueIndex = (int)effect;
                Prop(so, "effectAmount").intValue = effectAmount;
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
                // 처음에 고르는 모습 8가지 (#124 — 그림은 import_ninja_art.py HERO_LOOKS, 첫 번째가 기본)
                var looks = new[]
                {
                    ("boy", "소년"), ("eskimo", "에스키모"), ("villager", "마을 아이"), ("princess", "공주"),
                    ("hunter", "사냥꾼"), ("sorcerer", "마법사"), ("knight", "기사"), ("villager4", "시골 소년")
                };
                var lookList = Prop(so, "looks");
                lookList.arraySize = looks.Length;
                for (int i = 0; i < looks.Length; i++)
                {
                    var element = lookList.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("lookId").stringValue = looks[i].Item1;
                    element.FindPropertyRelative("displayName").stringValue = looks[i].Item2;
                }
            });
        }

        // 동료 (#77): 기본 기술 + (배우는 레벨, 기술)
        private static CompanionData Companion(string id, string name, string description, string role, MonsterStats baseStats,
            MonsterStats growth, SkillData basicSkill, params (int level, SkillData skill)[] learned)
        {
            return CreateIfMissing<CompanionData>($"{Root}/Companions/{id}.asset", so =>
            {
                Prop(so, "companionId").stringValue = id;
                Prop(so, "displayName").stringValue = name;
                Prop(so, "description").stringValue = description;
                Prop(so, "role").stringValue = role;
                SetStats(Prop(so, "baseStats"), baseStats);
                SetStats(Prop(so, "growthPerLevel"), growth);
                Prop(so, "basicSkill").objectReferenceValue = basicSkill;
                var list = Prop(so, "learnedSkills");
                list.arraySize = learned.Length;
                for (int i = 0; i < learned.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("level").intValue = learned[i].level;
                    element.FindPropertyRelative("skill").objectReferenceValue = learned[i].skill;
                }
            });
        }

        // 기술마다 전투 연출 (#137, 그림 = UI SkillLooks). 없는 기술은 Auto(종류대로)
        private static readonly Dictionary<string, SkillFx> SkillFxById = new Dictionary<string, SkillFx>
        {
            ["hero_swing"] = SkillFx.Slash,
            ["nib_ink_splash"] = SkillFx.Ink, ["quill_ink_storm"] = SkillFx.Ink,
            ["wand_spell_bolt"] = SkillFx.Thunder, ["wand_thunder_spell"] = SkillFx.Thunder, ["doc_cram_bolt"] = SkillFx.Thunder,
            ["lamp_firefly_swarm"] = SkillFx.Light, ["lamp_glow_flash"] = SkillFx.Light,
            ["gem_letter_shower"] = SkillFx.Letters, ["gem_letter_echo"] = SkillFx.Letters, ["doc_fill_blank"] = SkillFx.Letters,
            ["doc_highlight_sweep"] = SkillFx.Highlight, ["doc_finish_sentence"] = SkillFx.Heavy,
            ["whip_vine_lash"] = SkillFx.Plant, ["whip_thorn_storm"] = SkillFx.Plant,
            ["ara_pebble"] = SkillFx.Rock,
            ["lantern_wisdom_light"] = SkillFx.HealLight, ["lumi_healing_light"] = SkillFx.HealLight,
            ["grail_recall"] = SkillFx.HealLight, ["grail_blessing"] = SkillFx.HealLight,
            ["leaf_sprout_heal"] = SkillFx.HealLeaf, ["leaf_world_breath"] = SkillFx.HealLeaf, ["ara_sprout"] = SkillFx.HealLeaf,
            ["gourd_sip"] = SkillFx.HealWater, ["gourd_spring"] = SkillFx.HealWater,
            ["shell_book_shield"] = SkillFx.GuardBook, ["tortoise_encyclo_wall"] = SkillFx.GuardBook, ["doc_pencilcase_guard"] = SkillFx.GuardBook,
            ["flute_echo_veil"] = SkillFx.GuardEcho, ["flute_echo_wall"] = SkillFx.GuardEcho, ["ara_echo"] = SkillFx.GuardEcho,
            ["hourglass_pause"] = SkillFx.GuardTime, ["hourglass_rewind"] = SkillFx.GuardTime,
            ["seed_full_stop"] = SkillFx.GuardSeed, ["seed_period_grove"] = SkillFx.GuardSeed,
            // 적
            ["slime_splat"] = SkillFx.Ink, ["newt_ink_spit"] = SkillFx.Ink, ["octopus_ink_mimic"] = SkillFx.Ink,
            ["bat_scratch"] = SkillFx.Claw, ["larva_chew"] = SkillFx.Claw, ["owl_question_peck"] = SkillFx.Claw, ["spider_spell_bite"] = SkillFx.Claw,
            ["goblin_blank_bonk"] = SkillFx.Heavy, ["boss_forget_bonk"] = SkillFx.Heavy, ["boss_amnesia"] = SkillFx.Heavy,
            ["raccoon_tail_slam"] = SkillFx.Heavy, ["vine_slam"] = SkillFx.Heavy,
            ["frog_tongue_lash"] = SkillFx.Whip, ["lizard_tail_grab"] = SkillFx.Whip,
            ["goblin_forget_fog"] = SkillFx.Spore, ["mushroom_spell_spore"] = SkillFx.Spore, ["larva_silk_spray"] = SkillFx.Spore,
            ["spider_tangle_web"] = SkillFx.Spore, ["vine_ellipsis_spore"] = SkillFx.Spore,
            ["ghost_blink_brush"] = SkillFx.Swirl, ["kappa_batchim_swap"] = SkillFx.Swirl, ["raccoon_muddle_whirl"] = SkillFx.Swirl,
            ["snake_squiggle_squeeze"] = SkillFx.Swirl,
            ["kappa_plate_splash"] = SkillFx.Water, ["frog_blank_croak"] = SkillFx.Sound,
            ["boss_memory_drain"] = SkillFx.Drain, ["frog_gulp"] = SkillFx.Drain, ["vine_swallow_sentence"] = SkillFx.Drain,
            ["raccoon_acorn_snack"] = SkillFx.HealLeaf,
        };

        private static SkillData Skill(string id, string name, string description, SkillKind kind, SkillTarget target,
            int power, QuizDirection direction, int chargeTurns = 0)
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
                Prop(so, "chargeTurns").intValue = chargeTurns;
                Prop(so, "fx").enumValueIndex = SkillFxById.TryGetValue(id, out var fx) ? (int)fx : (int)SkillFx.Auto;
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
