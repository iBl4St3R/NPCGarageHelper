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
        // ── Stałe ─────────────────────────────────────────────────────────────

        // ── Stan ──────────────────────────────────────────────────────────────
        public static Il2CppCMS.Warehouse.WarehouseObject InputStorage { get; private set; }
        public static Il2CppCMS.Warehouse.WarehouseObject OutputStorage { get; private set; }
        public static Vector3 AnchorPos { get; private set; } = new Vector3(-10.3f, 0f, 2.1f);
        public static bool HasAnchor { get; private set; }
        public static Il2CppCMS.Garage.Tools.UpgradeTable UpgradeTable { get; private set; }
        private const float SCAN_RADIUS = 10f; // jeden promień dla wszystkiego

        // ── Reset przy zmianie sceny ──────────────────────────────────────────

        public static void Reset()
        {
            InputStorage = OutputStorage = null;
            UpgradeTable = null;
            HasAnchor = false;
            AnchorPos = new Vector3(-10.3f, 0f, 2.1f);
        }


        // ── Znajdź RepairTable — wywołaj raz po załadowaniu sceny ─────────────
        public static void FindAnchor()
        {
            try
            {
                var allMB = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>(true);
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Garage.Tools.RepairTable") continue;
                        var rt = new Il2CppCMS.Garage.Tools.RepairTable(obj.Pointer);
                        if (rt.forBodyParts) continue;
                        AnchorPos = rt.transform.position;
                        HasAnchor = true;
                        Plugin.Log.Msg($"[StorageCache] Anchor: RepairTable @ {AnchorPos}");
                        break;
                    }
                    catch { }
                }

                // Znajdź UpgradeTable w tym samym przebiegu
                foreach (var obj in allMB)
                {
                    try
                    {
                        if (obj.GetIl2CppType().FullName != "CMS.Garage.Tools.UpgradeTable") continue;
                        var ut = new Il2CppCMS.Garage.Tools.UpgradeTable(obj.Pointer);
                        float d = UnityEngine.Vector3.Distance(AnchorPos, ut.transform.position);
                        if (d <= SCAN_RADIUS)
                        {
                            UpgradeTable = ut;
                            Plugin.Log.Msg($"[StorageCache] UpgradeTable @ {ut.transform.position}  dist={d:F1}m");
                            break;
                        }
                    }
                    catch { }
                }

                if (!HasAnchor)
                    Plugin.Log.Warning("[StorageCache] RepairTable not found — using hardcoded fallback.");
                if (UpgradeTable == null)
                    Plugin.Log.Warning("[StorageCache] UpgradeTable not found within 10m.");
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

            if (candidates.Count >= 2)
            {
                InputStorage = candidates[0].wo;
                OutputStorage = candidates[1].wo;
                Plugin.Log.Msg($"[StorageCache] IN={InputStorage.StorageName}  OUT={OutputStorage.StorageName}");
                return ScanResult.OK;
            }
            if (candidates.Count == 1)
            {
                InputStorage = candidates[0].wo;
                return ScanResult.MissingOutput;
            }
            return ScanResult.NoStorages;
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