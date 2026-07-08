using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TowardTheStars.Data
{
    // stages_unified.json 매핑. MapLoader가 이 데이터를 읽어 각 오브젝트에 변수를 주입한다.
    // 규약[GDD §30]: rotation_z = -angle_deg. 좌표 y는 위로 증가(반전 없음).
    public class UnifiedData
    {
        [JsonProperty("unit")] public UnitData Unit;
        [JsonProperty("stages")] public Dictionary<string, StageData> Stages = new();
    }

    public class UnitData
    {
        [JsonProperty("cell_px")] public int CellPx = 40;
        [JsonProperty("unity_world_per_cell")] public float UnityWorldPerCell = 1.0f;
    }

    public class StageData
    {
        [JsonProperty("stage")] public int Stage;
        [JsonProperty("grid")] public GridData Grid;
        [JsonProperty("camera")] public CameraSettings Camera;   // 스테이지별 카메라 오버라이드(선택)
        [JsonProperty("jump_units")] public float JumpUnits;

        [JsonProperty("source")] public Endpoint Source;      // 랜즈/광원
        [JsonProperty("prism")] public PrismData Prism;        // null 가능
        [JsonProperty("gate")] public GateData Gate;           // 수광부

        [JsonProperty("mirrors")] public List<MirrorData> Mirrors = new();
        [JsonProperty("platforms")] public List<PlatformData> Platforms = new();
        [JsonProperty("decoys")] public List<DecoyData> Decoys = new();
        [JsonProperty("ladders")] public List<LadderData> Ladders = new();   // Stage 4 신규

        [JsonProperty("spawn")] public int[] Spawn;
        [JsonProperty("gate_open_zone")] public List<int[]> GateOpenZone = new();

        [JsonProperty("terrain")] public Dictionary<string, int> Terrain = new();

        [JsonProperty("fixed_mirrors")] public List<string> FixedMirrors = new();

        // 매핑 안 된 나머지 필드(예: wall / wall_x25 / wall_x41, layout, status ...)를 통째로 수집.
        [JsonExtensionData] public Dictionary<string, JToken> Extra = new();

        // "wall"로 시작하는 모든 키를 벽으로 취급 → 스테이지마다 다른 wall_* 키를 일반 처리.
        public IEnumerable<int[]> AllWalls()
        {
            if (Extra == null) yield break;
            foreach (var kv in Extra)
            {
                if (!kv.Key.StartsWith("wall")) continue;
                if (kv.Value is JArray arr)
                    foreach (var cell in arr)
                    {
                        var c = cell.ToObject<int[]>();
                        if (c != null && c.Length >= 2) yield return c;
                    }
            }
        }
    }

    public class GridData
    {
        [JsonProperty("W")] public int W;
        [JsonProperty("H")] public int H;
        [JsonProperty("fine_grid")] public bool FineGrid;
    }

    // 스테이지별 카메라 오버라이드. 모든 값 선택(기본 0 = MapLoader 기본값/그리드 경계 사용).
    public class CameraSettings
    {
        [JsonProperty("view_cells")] public float ViewCells;   // 화면 세로에 담을 셀 수(작을수록 확대). 0=기본
        [JsonProperty("top_pad")] public float TopPad;         // 상단 경계에 더할 여유 칸(화면 상한선↑)
        [JsonProperty("bottom_pad")] public float BottomPad;   // 하단 여유 칸
        [JsonProperty("side_pad")] public float SidePad;       // 좌우 여유 칸
    }

    public class Endpoint
    {
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("dir")] public string Dir;   // 화살표 ↙ ↑ ← → ↗ ↖ ↘ ↓
    }

    public class GateData
    {
        [JsonProperty("pos")] public int[] Pos;
    }

    public class PrismData
    {
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("in")] public string In;
        [JsonProperty("out")] public string[] Out;   // 다중 출력(직선+상단대각)
        [JsonProperty("fixed")] public bool Fixed;
    }

    public class MirrorData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("angle_deg")] public float AngleDeg;
        [JsonProperty("rotation_z")] public float RotationZ;
        [JsonProperty("fixed")] public bool Fixed;
    }

    public class PlatformData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("cells")] public List<int[]> Cells = new();
        [JsonProperty("transmit")] public bool Transmit;
        [JsonProperty("MISSING")] public bool Missing;
    }

    public class DecoyData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("isolated")] public bool Isolated;
    }

    // Stage 4 사다리: 열(col)과 세로 구간(y_span=[yStart,yEnd]).
    public class LadderData
    {
        [JsonProperty("col")] public int Col;
        [JsonProperty("y_span")] public int[] YSpan;   // [아래y, 위y]
    }
}
