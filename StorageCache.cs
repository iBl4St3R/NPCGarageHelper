using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCGarageHelper
{
    /// <summary>
    /// Scan storage'ów wywoływany TYLKO na żądanie (przycisk Scan).
    /// W UpdateCallback czytamy wyłącznie z gotowych referencji — zero FindObjectsOfType per frame.
    /// </summary>
    internal static class StorageCache
    {

        // ── Stan ──────────────────────────────────────────────────────────────
        public static Il2CppCMS.Warehouse.WarehouseObject InputStorage { get; private set; }
        public static Il2CppCMS.Warehouse.WarehouseObject OutputStorage { get; private set; }
        public static Vector3 AnchorPos { get; private set; } = new Vector3(-10.3f, 0f, 2.1f);
        public static bool HasAnchor { get; private set; }
        public static Il2CppCMS.Garage.Tools.UpgradeTable UpgradeTable { get; private set; }
        private const float SCAN_RADIUS = 10f; // jeden promień dla wszystkiego

        public static UnityEngine.Transform BodyRepairTool1 { get; private set; }
        public static UnityEngine.Transform BodyRepairTool2 { get; private set; }
        public static float LastScanTime { get; private set; } = -999f;  // Time.time


        public static Vector3? BodyRepairTablePos { get; private set; }

        private static List<(float dist, Il2CppCMS.Warehouse.WarehouseObject wo)> _lastScanResults = new();

        
        public static List<(float dist, Il2CppCMS.Warehouse.WarehouseObject wo)> GetAllStoragesWithDistance() => _lastScanResults;

        public static bool HasRepairTable { get; private set; }  // RepairTable znaleziony

        // ── Reset przy zmianie sceny ──────────────────────────────────────────

        public static void Reset()
        {
            InputStorage = OutputStorage = null;
            UpgradeTable = null;
            HasAnchor = false;
            AnchorPos = new Vector3(-10.3f, 0f, 2.1f);
            BodyRepairTool1 = BodyRepairTool2 = null;
            LastScanTime = -999f;
            _lastScanResults.Clear();
            HasRepairTable = false;


            BodyRepairTablePos = null;
        }


        // ── Znajdź RepairTable — wywołaj raz po załadowaniu sceny ─────────────
        public static void FindAnchor()
        {
            try
            {
                var allMB = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>(true);

                // Pętla 1 — RepairTable (parts) — wymagany do działania
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Garage.Tools.RepairTable") continue;
                        var rt = new Il2CppCMS.Garage.Tools.RepairTable(obj.Pointer);
                        if (rt.forBodyParts) continue;
                        HasRepairTable = true;
                        Plugin.Log.Msg($"[StorageCache] RepairTable @ {rt.transform.position}");
                        break;
                    }
                    catch { }
                }

                // Pętla 2 — RepairTable (body) — tylko info
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Garage.Tools.RepairTable") continue;
                        var rt = new Il2CppCMS.Garage.Tools.RepairTable(obj.Pointer);
                        if (!rt.forBodyParts) continue;
                        BodyRepairTablePos = rt.transform.position;
                        Plugin.Log.Msg($"[StorageCache] BodyRepairTable @ {BodyRepairTablePos}");
                        break;
                    }
                    catch { }
                }

                // Pętla 3 — UpgradeTable — ANCHOR (pozycja bazowa dla storage scanu)
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Garage.Tools.UpgradeTable") continue;
                        var ut = new Il2CppCMS.Garage.Tools.UpgradeTable(obj.Pointer);
                        UpgradeTable = ut;
                        AnchorPos = ut.transform.position;   // ← ANCHOR = UpgradeTable
                        HasAnchor = true;
                        Plugin.Log.Msg($"[StorageCache] Anchor=UpgradeTable @ {AnchorPos}");
                        break;
                    }
                    catch { }
                }

                if (!HasRepairTable)
                    Plugin.Log.Warning("[StorageCache] RepairTable not found.");
                if (!HasAnchor)
                    Plugin.Log.Warning("[StorageCache] UpgradeTable not found — anchor missing.");
            }
            catch (Exception ex) { Plugin.Log.Warning($"[StorageCache] FindAnchor: {ex.Message}"); }
        }

        // ── Pełny scan — wywołaj TYLKO z przycisku Scan ───────────────────────
        public static ScanResult Scan()
        {
            InputStorage = OutputStorage = null;

            // Jeśli anchor lub UpgradeTable nie znaleziono przy starcie — spróbuj ponownie
            if (!HasAnchor || UpgradeTable == null)
                FindAnchor();

            var candidates = new List<(float dist, Il2CppCMS.Warehouse.WarehouseObject wo)>();

            try
            {
                var allMB = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>(true);
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Warehouse.WarehouseObject") continue;
                        var wo = new Il2CppCMS.Warehouse.WarehouseObject(obj.Pointer);
                        float d = Vector3.Distance(AnchorPos, wo.transform.position);
                        if (d <= SCAN_RADIUS) candidates.Add((d, wo));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Plugin.Log.Warning($"[StorageCache] Scan: {ex.Message}"); }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            _lastScanResults = new List<(float, Il2CppCMS.Warehouse.WarehouseObject)>(candidates);


            if (candidates.Count >= 2)
            {
                InputStorage = candidates[0].wo;
                OutputStorage = candidates[1].wo;
                Plugin.Log.Msg($"[StorageCache] IN={InputStorage.StorageName}  OUT={OutputStorage.StorageName}");
            }
            else if (candidates.Count == 1)
            {
                InputStorage = candidates[0].wo;
            }

            FindBodyRepairTools();
            LastScanTime = UnityEngine.Time.time;

            return candidates.Count >= 2 ? ScanResult.OK
                 : candidates.Count == 1 ? ScanResult.MissingOutput
                 : ScanResult.NoStorages;
        }

        private static void FindBodyRepairTools()
        {
            BodyRepairTool1 = BodyRepairTool2 = null;
            try
            {
                // Szukamy bezpośrednio po nazwie — taniej niż iteracja allMB
                var go1 = UnityEngine.GameObject.Find("Body_Repair_Tool_1(Clone)");
                var go2 = UnityEngine.GameObject.Find("Body_Repair_Tool_2(Clone)");

                if (go1 != null)
                {
                    float d = Vector3.Distance(AnchorPos, go1.transform.position);
                    if (d <= SCAN_RADIUS)
                    {
                        BodyRepairTool1 = go1.transform;
                        Plugin.Log.Msg($"[StorageCache] BodyRepairTool1 @ {go1.transform.position}  dist={d:F1}m");
                    }
                    else Plugin.Log.Msg($"[StorageCache] BodyRepairTool1 znaleziony ale poza zasięgiem ({d:F1}m)");
                }

                if (go2 != null)
                {
                    float d = Vector3.Distance(AnchorPos, go2.transform.position);
                    if (d <= SCAN_RADIUS)
                    {
                        BodyRepairTool2 = go2.transform;
                        Plugin.Log.Msg($"[StorageCache] BodyRepairTool2 @ {go2.transform.position}  dist={d:F1}m");
                    }
                }
            }
            catch (Exception ex) { Plugin.Log.Warning($"[StorageCache] FindBodyRepairTools: {ex.Message}"); }
        }

        // ── Szybka walidacja (bez FindObjects) ────────────────────────────────
        /// <summary>Sprawdza czy cached referencje wciąż żyją i są ok.</summary>
        public static bool IsValid()
        {
            try
            {
                if (InputStorage == null || OutputStorage == null) return false;
                // Prosty dostęp — jeśli obiekt null w Unity, rzuci wyjątek
                _ = InputStorage.ItemsCount;
                _ = OutputStorage.ItemsCount;
                return true;
            }
            catch { return false; }
        }

        // ── Wyniki scanu ──────────────────────────────────────────────────────
        public enum ScanResult
        {
            OK,
            MissingOutput,
            NoStorages
        }
    }
}