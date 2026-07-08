using System.Collections.Generic;
using Newtonsoft.Json;

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
        [JsonProperty("wall")] public List<int[]> Wall = new();
        [JsonProperty("wall_x25")] public List<int[]> WallX25 = new();

        [JsonProperty("fixed_mirrors")] public List<string> FixedMirrors = new();

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
        [JsonProperty("fine_grid")] public bool FineGrid;
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

    // Stage 4 사다리: 하단 pos에서 위로 height 칸.
    public class LadderData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("pos")] public int[] Pos;
        [JsonProperty("height")] public int Height = 1;
    }
}
