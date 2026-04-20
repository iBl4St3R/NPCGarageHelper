using MelonLoader;
using System;
using System.IO;

namespace NPCGarageHelper
{
    internal static class NpcSaveData
    {
        // ── Ścieżka pliku ─────────────────────────────────────────────────────
        private static string SavePath =>Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),"NPCGarageHelper","NPCGarageHelper_save.json");

        // ── Guard anty-reentrant ──────────────────────────────────────────────
        private static bool _saveInProgress = false;

        // ── Struktura danych ──────────────────────────────────────────────────
        [Serializable]
        public class SavePayload
        {
            public int NpcLevel = 1;
            public int NpcXp = 0;
            public float AllocatedFunds = 0f;

            // Pasywy per kategoria (6 kategorii × 3 wartości)
            public int[] SuccessLvl = new int[6];
            public int[] MaxRepairLvl = new int[6];
            public int[] MinRepairLvl = new int[6];
            public int AvailablePoints = 10;
        }

        // ── Zapis ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Wywołuj po: ukończeniu naprawy, dodaniu funduszy, ulepszeniu pasywa.
        /// Guard blokuje ponowne wejście jeśli poprzedni save jeszcze trwa
        /// (np. dwa zdarzenia w tej samej klatce).
        /// </summary>
        public static void Save(int npcLevel, int npcXp, float allocatedFunds)
        {
            if (_saveInProgress)
            {
                Plugin.Log.Msg("[NpcSaveData] Save skipped — already in progress this frame");
                return;
            }

            _saveInProgress = true;
            try
            {
                var payload = new SavePayload
                {
                    NpcLevel = npcLevel,
                    NpcXp = npcXp,
                    AllocatedFunds = allocatedFunds,
                    AvailablePoints = NpcSkillData.AvailablePoints,
                };

                for (int i = 0; i < 6; i++)
                {
                    var cat = (NpcSkillData.Category)i;
                    payload.SuccessLvl[i] = NpcSkillData.GetSuccessLvl(cat);
                    payload.MaxRepairLvl[i] = NpcSkillData.GetMaxRepairLvl(cat);
                    payload.MinRepairLvl[i] = NpcSkillData.GetMinRepairLvl(cat);
                }

                string saveDir = Path.Combine(
                    Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "NPCGarageHelper");

                Directory.CreateDirectory(saveDir);

                string json = ToJson(payload);
                File.WriteAllText(Path.Combine(saveDir, "NPCGarageHelper_save.json"), json);
                Plugin.Log.Msg($"[NpcSaveData] Saved → lvl={npcLevel} xp={npcXp} funds={allocatedFunds:F0}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[NpcSaveData] Save failed: {ex.Message}");
            }
            finally
            {
                _saveInProgress = false;
            }
        }

        // ── Odczyt ────────────────────────────────────────────────────────────
        /// <summary>
        /// Zwraca null jeśli plik nie istnieje lub jest uszkodzony.
        /// </summary>
        public static SavePayload Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    Plugin.Log.Msg("[NpcSaveData] No save file found — fresh start");
                    return null;
                }

                string json = File.ReadAllText(SavePath);
                var payload = FromJson(json);
                Plugin.Log.Msg($"[NpcSaveData] Loaded → lvl={payload.NpcLevel} xp={payload.NpcXp} funds={payload.AllocatedFunds:F0}");
                return payload;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[NpcSaveData] Load failed: {ex.Message}");
                return null;
            }
        }

        // ── Minimalistyczny JSON (bez zewnętrznych zależności) ────────────────
        // Net6 ma System.Text.Json ale w IL2CPP MelonLoader bywa z nim krucho —
        // piszemy ręcznie dla tej prostej struktury.

        private static string ToJson(SavePayload p)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"NpcLevel\": {p.NpcLevel},");
            sb.AppendLine($"  \"NpcXp\": {p.NpcXp},");
            sb.AppendLine($"  \"AllocatedFunds\": {p.AllocatedFunds.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"AvailablePoints\": {p.AvailablePoints},");
            sb.AppendLine($"  \"SuccessLvl\":   [{string.Join(", ", p.SuccessLvl)}],");
            sb.AppendLine($"  \"MaxRepairLvl\": [{string.Join(", ", p.MaxRepairLvl)}],");
            sb.AppendLine($"  \"MinRepairLvl\": [{string.Join(", ", p.MinRepairLvl)}]");
            sb.Append("}");
            return sb.ToString();
        }

        private static SavePayload FromJson(string json)
        {
            var p = new SavePayload();

            p.NpcLevel = ReadInt(json, "NpcLevel", 1);
            p.NpcXp = ReadInt(json, "NpcXp", 0);
            p.AllocatedFunds = ReadFloat(json, "AllocatedFunds", 0f);
            p.AvailablePoints = ReadInt(json, "AvailablePoints", 6);
            p.SuccessLvl = ReadIntArray(json, "SuccessLvl", 6);
            p.MaxRepairLvl = ReadIntArray(json, "MaxRepairLvl", 6);
            p.MinRepairLvl = ReadIntArray(json, "MinRepairLvl", 6);

            return p;
        }

        // ── Parsery ───────────────────────────────────────────────────────────
        private static int ReadInt(string json, string key, int fallback)
        {
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern, StringComparison.Ordinal);
                if (idx < 0) return fallback;
                int start = idx + pattern.Length;
                // pomiń białe znaki
                while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
                int end = start;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
                return int.TryParse(json.Substring(start, end - start), out int v) ? v : fallback;
            }
            catch { return fallback; }
        }

        private static float ReadFloat(string json, string key, float fallback)
        {
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern, StringComparison.Ordinal);
                if (idx < 0) return fallback;
                int start = idx + pattern.Length;
                while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
                int end = start;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.')) end++;
                return float.TryParse(
                    json.Substring(start, end - start),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float v) ? v : fallback;
            }
            catch { return fallback; }
        }

        private static int[] ReadIntArray(string json, string key, int size)
        {
            var result = new int[size];
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern, StringComparison.Ordinal);
                if (idx < 0) return result;
                int start = json.IndexOf('[', idx);
                int end = json.IndexOf(']', start);
                if (start < 0 || end < 0) return result;

                string inner = json.Substring(start + 1, end - start - 1);
                string[] parts = inner.Split(',');
                for (int i = 0; i < Math.Min(parts.Length, size); i++)
                    if (int.TryParse(parts[i].Trim(), out int v))
                        result[i] = v;
            }
            catch { }
            return result;
        }
    }
}