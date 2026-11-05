using UnityEngine;

namespace WordRPG.UI
{
    // 왼손 모드 (#215 사용자, Figma 'Field — 왼손 모드 (#215)'): 필드의 스틱과 [확인]·[선제공격] 자리를 화면 가운데 기준으로 좌우 대칭으로 바꾼다.
    // 위쪽 HUD(미니맵·메뉴)는 그대로, 전투 버튼은 화면 전체 너비라 어느 손이든 같다
    public static class HandLayout
    {
        public readonly struct Area
        {
            public readonly float MinX, MinY, MaxX, MaxY;

            public Area(float minX, float minY, float maxX, float maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public Area Mirrored => new Area(1f - MaxX, MinY, 1f - MinX, MaxY);

            public void ApplyTo(RectTransform rect)
            {
                rect.anchorMin = new Vector2(MinX, MinY);
                rect.anchorMax = new Vector2(MaxX, MaxY);
            }
        }

        // 오른손(기본): 스틱은 오른쪽 아래 아무 곳이나, [확인]은 왼쪽 아래 (스틱을 밀면서 다른 손가락으로 누를 수 있게)
        private static readonly Area RightHandedStick = new Area(0.4f, 0f, 1f, 0.27f);
        private static readonly Area RightHandedConfirm = new Area(0.02f, 0.03f, 0.36f, 0.24f);
        private const float RightHandedStickHomeX = 0.52f; // 손을 떼면 받침이 돌아가는 자리 (스틱 영역 비율)
        private const float RightHandedPreemptX = 0.19f;   // [선제공격]은 [확인] 바로 위

        public static Area Stick(bool leftHanded) => leftHanded ? RightHandedStick.Mirrored : RightHandedStick;
        public static Area Confirm(bool leftHanded) => leftHanded ? RightHandedConfirm.Mirrored : RightHandedConfirm;
        public static float StickHomeX(bool leftHanded) => leftHanded ? 1f - RightHandedStickHomeX : RightHandedStickHomeX;
        public static float PreemptX(bool leftHanded) => leftHanded ? 1f - RightHandedPreemptX : RightHandedPreemptX;
    }
}
