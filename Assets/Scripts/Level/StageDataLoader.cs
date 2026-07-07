using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using TowardTheStars.Data;

namespace TowardTheStars.Level
{
    // StreamingAssets/stages_unified.json 을 읽어 UnifiedData로 파싱.
    // (에디터/Windows 스탠드얼론 기준 File 직접 읽기. Android 등은 UnityWebRequest 필요 — 데모 범위 밖.)
    public static class StageDataLoader
    {
        public const string FileName = "stages_unified.json";

        public static string FullPath => Path.Combine(Application.streamingAssetsPath, FileName);

        public static UnifiedData Load()
        {
            string path = FullPath;
            if (!File.Exists(path))
            {
                Debug.LogError($"[StageDataLoader] 데이터 파일을 찾을 수 없음: {path}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<UnifiedData>(json);
                if (data == null || data.Stages == null || data.Stages.Count == 0)
                {
                    Debug.LogError("[StageDataLoader] 파싱 결과가 비어 있음(스키마 확인 필요).");
                    return null;
                }
                return data;
            }
            catch (JsonException e)
            {
                Debug.LogError($"[StageDataLoader] JSON 파싱 실패: {e.Message}");
                return null;
            }
        }

        // 단일 스테이지만 필요할 때. stageKey 예: "stage2".
        public static StageData LoadStage(string stageKey)
        {
            var data = Load();
            if (data == null) return null;
            if (!data.Stages.TryGetValue(stageKey, out var stage))
            {
                Debug.LogError($"[StageDataLoader] 스테이지 키 없음: {stageKey} " +
                               $"(사용가능: {string.Join(", ", data.Stages.Keys)})");
                return null;
            }
            return stage;
        }
    }
}
