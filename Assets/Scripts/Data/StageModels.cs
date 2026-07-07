using System.Collections.Generic;
using Newtonsoft.Json;

namespace TowardTheStars.Data
{
    // stages_unified.json 전체 매핑.
    // 스테이지마다 필드가 다르므로(예: stage2=decoys/wall, stage3=prism/terrain, stage4=layout),
    // 없는 필드는 Newtonsoft가 기본값(null/빈 컬렉션)으로 둔다. 규약: [GDD §30] rotation_z = -angle.
    public class UnifiedData
    {
        [JsonProperty("project")] public string Project;
        [JsonProperty("demo_scope")] public string DemoScope;
        [JsonProperty("angle_convention")] public string AngleConvention;
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
        [JsonProperty("jump_units")] public float JumpUnits;

        [JsonProperty("source")] public Endpoint Source;
        [JsonProperty("prism")] public PrismData Prism;      // null 가능(stage1/2/4)
        [JsonProperty("gate")] public GateData Gate;

        [JsonProperty("mirrors")] public List<MirrorData> Mirrors = new();
        [JsonProperty("platforms")] public List<PlatformData> Platforms = new();
        [JsonProperty("decoys")] public List<DecoyData> Decoys = new();

        [JsonProperty("spawn")] public int[] Spawn;
        [JsonProperty("gate_open_zone")] public List<int[]> GateOpenZone = new();

        // 지형/벽: 스테이지별로 키가 다름(wall / wall_x25)
        [JsonProperty("terrain")] public Dictionary<string, int> Terrain = new();   // x → 지면 표면 높이(솔리드)
        [JsonProperty("wall")] public List<int[]> Wall = new();
        [JsonProperty("wall_x25")] public List<int[]> WallX25 = new();

        [JsonProperty("fixed_mirrors")] public List<string> FixedMirrors = new();
        [JsonProperty("status")] public string Status;

        // 모든 벽 셀을 한 번에 얻기 위한 헬퍼(스테이지별 키 통합).
        public IEnumerable<int[]> AllWalls()
        {
            if (Wall != null) foreach (var c in Wall) yield return c;
            if (WallX25 != null) foreach (var c in WallX25) yield return c;
        }
    }

    public class GridData
    {
        [JsonProperty("W")] public int W;
        [JsonProperty("H")] public int H;
        [JsonProperty("fine_grid")] public bool FineGrid;   // stage4 세밀격자 표식
    }

    public class Endpoint
    {
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("dir")] public string Dir;   // 화살표: ↙ ↑ ← → ↗ ↖ ↘ ↓
    }

    public class GateData
    {
        [JsonProperty("pos")] public int[] Pos;
    }

    public class PrismData
    {
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("in")] public string In;
        [JsonProperty("out")] public string[] Out;   // 프리즘은 다중 출력(직선+상단대각)
        [JsonProperty("fixed")] public bool Fixed;
    }

    public class MirrorData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("in")] public string In;
        [JsonProperty("out")] public string Out;     // 거울은 단일 출력(프리즘과 달리 문자열)
        [JsonProperty("angle_deg")] public float AngleDeg;
        [JsonProperty("rotation_z")] public float RotationZ;   // = -angle_deg [GDD §30]
        [JsonProperty("fixed")] public bool Fixed;   // stage1 거울엔 없음 → 기본 false
    }

    public class PlatformData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("cells")] public List<int[]> Cells = new();
        [JsonProperty("transmit")] public bool Transmit;   // true=빛 투과(빛 판정 레이어 제외)
        [JsonProperty("MISSING")] public bool Missing;      // stage4 발판 미설계 표식 [갭]
    }

    public class DecoyData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("isolated")] public bool Isolated;
    }
}
