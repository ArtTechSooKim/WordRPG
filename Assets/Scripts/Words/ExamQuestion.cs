using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WordRPG.Words
{
    // 확인문제 하나 (#280 사용자: 의대생이 장마다 만든 문제 — 글자는 원본 그대로, Docs/의학용어_원본.html → Tools/medical_course.py).
    // 원본 모양 세 가지를 화면용으로 읽기만 한다 (글은 안 바꿈):
    //  · 5지선다: 문제 끝(괄호 안이나 그냥)의 '① … ⑤ …' → Options, 정답 '② VSD'의 원 숫자 → AnswerIndex
    //  · [보기] 목록: '[보기] a / b / …'(끝) 또는 '(보기: a, b, …)'(아무 데나) → Pool (오답 보기 재료), 화면 문제 글에서는 접음
    //  · 그 밖 = 주관식 (정답 하나 + 같은 장의 다른 정답으로 4지선다 — ExamQuiz)
    [Serializable]
    public class ExamQuestion
    {
        [SerializeField] private string id;
        [SerializeField] private int chapter;
        [SerializeField] private string chapterTitle;
        [SerializeField] private int number;
        [SerializeField] private string question;
        [SerializeField] private string answer;

        public string Id => id;
        public int Chapter => chapter;
        public string ChapterTitle => chapterTitle ?? "";
        public int Number => number;
        public string Question => question ?? "";
        public string Answer => answer ?? "";

        [NonSerialized] private bool parsed;
        [NonSerialized] private string stem;
        [NonSerialized] private List<string> options;
        [NonSerialized] private List<string> pool;
        [NonSerialized] private int answerIndex;

        private ExamQuestion() { } // Unity 직렬화용

        public ExamQuestion(string id, int chapter, string chapterTitle, int number, string question, string answer)
        {
            this.id = (id ?? "").Trim();
            this.chapter = chapter;
            this.chapterTitle = (chapterTitle ?? "").Trim();
            this.number = number;
            this.question = (question ?? "").Trim();
            this.answer = (answer ?? "").Trim();
        }

        // 화면에 보일 문제 글: 보기(① …)·[보기] 목록·앞의 '18. ' 같은 번호를 뺀 것 (번호는 원본과 장 번호가 어긋나는 곳이 있어 화면에선 안 씀)
        public string Stem { get { Parse(); return stem; } }
        public IReadOnlyList<string> Options { get { Parse(); return options; } } // 5지선다 보기 (원 숫자 없이). 비면 주관식
        public IReadOnlyList<string> Pool { get { Parse(); return pool; } }       // [보기] 목록 (없으면 빈 목록)
        public bool IsMultipleChoice => Options.Count >= 2 && AnswerIndex >= 0;
        public int AnswerIndex { get { Parse(); return answerIndex; } }           // 5지선다의 정답 자리 (0부터, 아니면 -1)

        public const char FirstCircled = '①';

        public static int CircledIndex(char c) => c >= '①' && c <= '⑳' ? c - FirstCircled : -1;
        public static string Circled(int index) => ((char)(FirstCircled + index)).ToString();

        private static readonly Regex LeadingNumber = new Regex(@"^\d+\.\s*");

        private void Parse()
        {
            if (parsed) return;
            parsed = true;
            options = new List<string>();
            pool = new List<string>();
            answerIndex = -1;
            string text = Question;

            // [보기] a / b / c … (문제 끝)
            int bracket = text.IndexOf("[보기]", StringComparison.Ordinal);
            if (bracket >= 0)
            {
                pool.AddRange(SplitList(text.Substring(bracket + 4), '/'));
                text = text.Substring(0, bracket);
            }
            // (보기: a, b, c …) — 앞이나 끝 (안에 괄호가 또 있을 수 있어 짝을 맞춰 찾음)
            int paren = text.IndexOf("(보기:", StringComparison.Ordinal);
            if (paren >= 0)
            {
                int close = MatchingParen(text, paren);
                string inside = text.Substring(paren + 4, (close < 0 ? text.Length : close) - paren - 4);
                pool.AddRange(SplitList(inside, ','));
                text = text.Substring(0, paren) + (close < 0 ? "" : text.Substring(close + 1));
            }

            // 5지선다 보기 ① … ⑤ …
            int first = text.IndexOf(FirstCircled);
            if (first >= 0)
            {
                string tail = text.Substring(first);
                string head = text.Substring(0, first).TrimEnd();
                if (head.EndsWith("(", StringComparison.Ordinal))
                {
                    head = head.Substring(0, head.Length - 1).TrimEnd();
                    if (tail.EndsWith(")", StringComparison.Ordinal)) tail = tail.Substring(0, tail.Length - 1);
                }
                var current = new StringBuilder();
                foreach (char c in tail)
                {
                    if (CircledIndex(c) >= 0)
                    {
                        if (current.Length > 0) options.Add(current.ToString().Trim());
                        current.Clear();
                        continue;
                    }
                    current.Append(c);
                }
                if (current.Length > 0) options.Add(current.ToString().Trim());
                text = head;
                if (Answer.Length > 0) answerIndex = CircledIndex(Answer[0]);
                if (answerIndex >= options.Count) answerIndex = -1;
            }

            stem = LeadingNumber.Replace(Regex.Replace(text, @"\s+", " ").Trim(), "");
        }

        private static IEnumerable<string> SplitList(string list, char separator)
        {
            foreach (var part in list.Split(separator))
            {
                string item = part.Trim();
                // 목록을 닫는 괄호만 뗌 — 'colpitis(=vaginitis)'처럼 짝이 맞는 괄호는 그대로
                while (item.EndsWith(")", StringComparison.Ordinal) && Count(item, ')') > Count(item, '('))
                    item = item.Substring(0, item.Length - 1).TrimEnd();
                if (item.Length > 0) yield return item;
            }
        }

        private static int Count(string text, char c)
        {
            int n = 0;
            foreach (char x in text) if (x == c) n++;
            return n;
        }

        private static int MatchingParen(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')' && --depth == 0) return i;
            }
            return -1;
        }

        // 같은 답인지 (보기 목록의 'colpitis(=vaginitis)'와 정답 'colpitis (= vaginitis)'): 괄호 안·빈칸·대소문자를 빼고 비교
        public static string Normalize(string answer)
        {
            if (string.IsNullOrEmpty(answer)) return "";
            string s = Regex.Replace(answer, @"\([^)]*\)", "");
            s = Regex.Replace(s, @"\s+", "");
            return s.ToLowerInvariant();
        }
    }
}
