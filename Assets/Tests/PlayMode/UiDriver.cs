using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // PlayMode 테스트 공용: 화면에 보이는 버튼을 실제로 눌러서 진행시키는 도우미
    internal static class UiDriver
    {
        public static Button FindButton(Transform root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name) return button;
            }
            return null;
        }

        public static Button ActiveButton(Transform root, string name)
        {
            var button = FindButton(root, name);
            return button != null && button.gameObject.activeInHierarchy && button.interactable ? button : null;
        }

        public static string AllText(Component root) =>
            string.Join(" | ", Array.ConvertAll(root.GetComponentsInChildren<Text>(true), t => t.text));

        // 결과 패널이 뜰 때까지: 새 단어 카드 확인 → 정답(또는 오답) 고르기 → 첫 번째 스킬(공격이면 강도 ×1) 사용 반복
        public static IEnumerator PlayUntilResult(BattleScreen screen, bool answerCorrectly)
        {
            var root = screen.transform;
            for (int frame = 0; frame < 3000 && !screen.IsResultVisible; frame++)
            {
                var engine = screen.Engine;
                var confirm = ActiveButton(root, "CardConfirmButton");
                if (confirm != null)
                {
                    confirm.onClick.Invoke();
                }
                else if (engine != null && engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                {
                    int index = engine.CurrentQuestion.CorrectIndex;
                    if (!answerCorrectly) index = (index + 1) % engine.CurrentQuestion.Choices.Count;
                    ActiveButton(root, $"Choice_{index}")?.onClick.Invoke();
                }
                else if (engine != null && engine.Phase == BattlePhase.ChoosingSkill)
                {
                    (ActiveButton(root, "Intensity_0") ?? ActiveButton(root, "SkillButton_0"))?.onClick.Invoke();
                }
                yield return null;
            }
        }

        // 프레임 수가 아니라 실제 시간으로 기다림 (배치모드는 프레임이 아주 빠름)
        public static IEnumerator WaitFor(Func<bool> condition, float maxSeconds = 10f)
        {
            float end = Time.realtimeSinceStartup + maxSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        // 가상 스틱을 손가락으로 끌듯: 제자리 받침 가운데를 누르고 → 그 방향으로 끌고(run이면 끝까지 = 달리기)
        // → 조건이 맞으면 떼고 → 이동이 끝날 때까지 대기
        public static IEnumerator HoldStick(FieldScreen field, Direction direction, Func<bool> until, float maxSeconds = 5f,
            bool run = false)
        {
            var stick = field.Stick;
            var data = new PointerEventData(EventSystem.current) { pointerId = 1, position = stick.ScreenPointFor(Vector2.zero) };
            ExecuteEvents.Execute(stick.gameObject, data, ExecuteEvents.pointerDownHandler);
            Vector2 offset = direction.ToOffset();
            data.position = stick.ScreenPointFor(offset * (run ? 1f : 0.6f));
            ExecuteEvents.Execute(stick.gameObject, data, ExecuteEvents.dragHandler);
            yield return WaitFor(until, maxSeconds);
            ExecuteEvents.Execute(stick.gameObject, data, ExecuteEvents.pointerUpHandler);
            yield return WaitFor(() => !field.IsMoving, maxSeconds);
        }

        // 막힌 쪽(상자·제단 등)으로 스틱을 밀어 그쪽을 바라보게 한다 — 부딪히기만 하고 쓰지는 않음
        public static IEnumerator FaceStick(FieldScreen field, Direction direction)
        {
            yield return HoldStick(field, direction, () => field.Facing == direction);
            yield return null;
        }

        // 왼쪽 아래 [확인] 버튼을 누른다 (필드가 다음 프레임에 처리)
        public static IEnumerator PressConfirm(FieldScreen field)
        {
            var button = FindButton(field.transform, "Pad_Confirm");
            if (button == null) throw new ArgumentException("확인 버튼 Pad_Confirm 없음");
            button.onClick.Invoke();
            yield return null;
            yield return null;
        }
    }
}
