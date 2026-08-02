using System.Collections.Generic;
using UnityEngine;

namespace TowardTheStars.Light
{
    // 하나의 광선(빛 세그먼트의 시작 상태). 방향은 정규화 저장.
    public struct Beam
    {
        public Vector2 origin;
        public Vector2 dir;
        public float intensity;

        public Beam(Vector2 origin, Vector2 dir, float intensity)
        {
            this.origin = origin;
            this.dir = dir.normalized;
            this.intensity = intensity;
        }
    }

    // 빛과 상호작용하는 오브젝트가 구현. 각 오브젝트가 "자기 연산"을 여기서 수행한다.
    //  - 거울: 반사광 1개 추가
    //  - 프리즘: 분기광 2개 추가(0.5+0.5)
    //  - 수광부: 흡수(추가 없음) + 광량 누적
    public interface IBeamHit
    {
        // incoming: 입사광, outgoing: 이어질 광선 추가 목록.
        // 반환: 실제 상호작용 지점(입사 세그먼트의 끝점 = 이어질 광선의 시작점). 각 오브젝트가 자기 기하로 계산한다
        //   (거울=입사광과 반사면 선분의 교점 → 맞는 위치에 따라 반사점이 달라짐, 프리즘·수광부=중심).
        Vector2 Interact(Beam incoming, List<Beam> outgoing);
    }
}
