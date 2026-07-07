using UnityEngine;

namespace TowardTheStars.Level
{
    // 그리드 좌표 ↔ 월드 좌표 매핑 + 방향 화살표 해석.
    // 규약[GDD §30·§39]: 1셀 = 1.0 world unit. 그리드 y와 Unity y 모두 위로 증가 → 반전 없음.
    public static class GridMap
    {
        public const float CELL = 1.0f;   // 1셀 = 1유닛 = 40px

        public static Vector2 ToWorld(int gx, int gy) => new Vector2(gx, gy) * CELL;

        public static Vector2 ToWorld(int[] pos) =>
            (pos != null && pos.Length >= 2) ? new Vector2(pos[0], pos[1]) * CELL : Vector2.zero;

        // 각도 규약: 0°=동쪽(→), 시계방향(CW). 화살표 → 정규화 방향 벡터.
        public static Vector2 DirToVector(string arrow)
        {
            switch (arrow)
            {
                case "→": return Vector2.right;
                case "←": return Vector2.left;
                case "↑": return Vector2.up;
                case "↓": return Vector2.down;
                case "↗": return new Vector2( 1f,  1f).normalized;
                case "↖": return new Vector2(-1f,  1f).normalized;
                case "↘": return new Vector2( 1f, -1f).normalized;
                case "↙": return new Vector2(-1f, -1f).normalized;
                default:  return Vector2.zero;
            }
        }
    }
}
