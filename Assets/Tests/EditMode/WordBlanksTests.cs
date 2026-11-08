using NUnit.Framework;
using WordRPG.Words;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 빈칸 울음 (#236 사용자 ㉝): 영어 단어 글자의 25%를 빈칸으로 (security → s_cur_ty처럼)
    public class WordBlanksTests
    {
        [Test]
        public void AQuarterOfTheLettersBecomeBlanksButTheFirstStays()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                string masked = WordBlanks.Mask("security", new Random(seed));
                Assert.AreEqual(8, masked.Length);
                Assert.AreEqual('s', masked[0], "첫 글자는 남김");
                Assert.AreEqual(2, masked.Split(WordBlanks.Blank).Length - 1, "8글자의 25% = 2칸");
                for (int i = 0; i < masked.Length; i++)
                    if (masked[i] != WordBlanks.Blank) Assert.AreEqual("security"[i], masked[i], "남은 글자는 제자리");
            }
        }

        [Test]
        public void ShortWordsAndSpacesAreHandled()
        {
            Assert.AreEqual("a", WordBlanks.Mask("a", new Random(1)), "한 글자는 그대로");
            string two = WordBlanks.Mask("go", new Random(1));
            Assert.AreEqual("g_", two, "두 글자는 뒤 글자 하나");
            string phrase = WordBlanks.Mask("look up", new Random(3));
            Assert.AreEqual(' ', phrase[4], "띄어쓰기는 그대로");
            Assert.AreEqual('l', phrase[0]);
            Assert.AreEqual(2, phrase.Split(WordBlanks.Blank).Length - 1, "글자 6개의 25% → 2칸 (반올림)");
            Assert.AreEqual("", WordBlanks.Mask("", new Random(1)));
        }
    }
}
