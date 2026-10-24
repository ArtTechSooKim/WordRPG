using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 입장 영상 (#129): Tools/build_intro_video.py가 만든 영상 + 자막 시간표
    public class IntroCinematicTests
    {
        [Test]
        public void IntroVideoAndCaptionsExist()
        {
            var clip = Resources.Load<VideoClip>(IntroCinematicView.VideoPath);
            Assert.IsNotNull(clip, "Resources/Video/intro.mp4");
            Assert.Less(clip.width, clip.height, "세로 영상");
            var captions = IntroCaptions.Load();
            // #133 사용자: 세 줄로는 세계관 설명이 부족 → 말의 세계 · 서고와 기억 · 말이 사라짐 · 하얀 날개 · 깃펜 · 깨어남 · 모험의 시작
            Assert.GreaterOrEqual(captions.captions.Count, 8, "세계관을 풀어 주는 자막");
            Assert.That(captions.duration, Is.EqualTo((float)clip.length).Within(0.6f), "자막표 길이 = 영상 길이");
            Assert.LessOrEqual(captions.duration, 40f, "입장 영상은 짧게");
            float lastEnd = 0f;
            foreach (var line in captions.captions)
            {
                Assert.GreaterOrEqual(line.end - line.start, 2.5f, $"읽을 시간: {line.text}");
                Assert.GreaterOrEqual(line.start, lastEnd, $"앞 자막과 겹치지 않음: {line.text}");
                Assert.LessOrEqual(line.text.Split('\n').Length, 2, $"두 줄까지 (화면 아래 자막 칸): {line.text}");
                Assert.LessOrEqual(line.end, captions.duration, $"영상이 끝나기 전에: {line.text}");
                lastEnd = line.end;
            }
        }

        [Test]
        public void CaptionsFollowTheTimeline()
        {
            var c = IntroCaptions.Parse("{\"duration\":26.5,\"captions\":[{\"start\":0.6,\"end\":5.4,\"text\":\"A\"},{\"start\":10.1,\"end\":14.9,\"text\":\"B\"}]}");
            Assert.AreEqual("", c.At(0.2f), "시작 직후는 비어 있음");
            Assert.AreEqual("A", c.At(1f));
            Assert.AreEqual("", c.At(7f), "자막 사이");
            Assert.AreEqual("B", c.At(12f));
            Assert.AreEqual("", IntroCaptions.Parse("").At(1f), "자막표가 없어도 멈추지 않음");
        }
    }
}
