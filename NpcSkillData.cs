using System;
using UnityEngine;

namespace NPCGarageHelper
{
    internal static class NpcSkillData
    {
        // ── Kategorie ──────────────────────────────────────────────────────────
        public enum Category { Engine, Drivetrain, Suspension, Brakes, Interior, Body }

        public static readonly string[] CategoryNames =
        {
            " Engine",
            " Drivetrain",
            " Suspension",
            " Brakes",
            " Interior",
            " Body"
        };

        // ── Tabele wartości ───────────────────────────────────────────────────
        // successLvl 0 = locked, 1-7 = wartości
        private static readonly float[] SuccessTable =
            { 0f, 0.40f, 0.50f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f };

        // maxRepairLvl 0 = free (40%), 1-5
        private static readonly float[] MaxRepairTable =
            { 0.40f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f };

        // minRepairLvl 0 = free (20%), 1-7
        private static readonly float[] MinRepairTable =
            { 0.20f, 0.30f, 0.40f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f };

        public const int MAX_SUCCESS_LVL = 7;
        public const int MAX_MAX_REPAIR_LVL = 5;
        public const int MAX_MIN_REPAIR_LVL = 7;

        // ── Stan ──────────────────────────────────────────────────────────────
        public static int AvailablePoints { get; private set; } = 6;

        private static readonly int[] _successLvl = new int[6];
        private static readonly int[] _maxRepairLvl = new int[6];
        private static readonly int[] _minRepairLvl = new int[6];

        // ── Gettery ───────────────────────────────────────────────────────────
        public static bool IsUnlocked(Category c) => _successLvl[(int)c] >= 1;
        public static float GetSuccessChance(Category c) => SuccessTable[_successLvl[(int)c]];
        public static float GetMaxRepair(Category c) => MaxRepairTable[_maxRepairLvl[(int)c]];
        public static float GetMinRepair(Category c) => MinRepairTable[_minRepairLvl[(int)c]];

        public static int GetSuccessLvl(Category c) => _successLvl[(int)c];
        public static int GetMaxRepairLvl(Category c) => _maxRepairLvl[(int)c];
        public static int GetMinRepairLvl(Category c) => _minRepairLvl[(int)c];

        // ── Upgrade checks ────────────────────────────────────────────────────
        public static bool CanUpgradeSuccess(Category c) =>
            AvailablePoints > 0 && _successLvl[(int)c] < MAX_SUCCESS_LVL;

        public static bool CanUpgradeMaxRepair(Category c) =>
            AvailablePoints > 0 && _maxRepairLvl[(int)c] < MAX_MAX_REPAIR_LVL;

        public static bool CanUpgradeMinRepair(Category c)
        {
            if (AvailablePoints <= 0) return false;
            if (_minRepairLvl[(int)c] >= MAX_MIN_REPAIR_LVL) return false;
            // min następnego poziomu nie może przekroczyć obecnego max
            float nextMin = MinRepairTable[_minRepairLvl[(int)c] + 1];
            float curMax = MaxRepairTable[_maxRepairLvl[(int)c]];
            return nextMin <= curMax;
        }

        // ── Upgrade ───────────────────────────────────────────────────────────
        public static bool UpgradeSuccess(Category c)
        {
            if (!CanUpgradeSuccess(c)) return false;
            _successLvl[(int)c]++;
            AvailablePoints--;
            return true;
        }

        public static bool UpgradeMaxRepair(Category c)
        {
            if (!CanUpgradeMaxRepair(c)) return false;
            _maxRepairLvl[(int)c]++;
            AvailablePoints--;
            return true;
        }

        public static bool UpgradeMinRepair(Category c)
        {
            if (!CanUpgradeMinRepair(c)) return false;
            _minRepairLvl[(int)c]++;
            AvailablePoints--;
            return true;
        }

        // ──
        //
        // / level up ────────────────────────────────────────────────────
        public static void AddSkillPoint()
        {
            AvailablePoints++;
            Plugin.Log.Msg($"[NpcSkillData] Skill point added → {AvailablePoints} available");
        }

        // ── Reset ─────────────────────────────────────────────────────────────
        public static void Reset()
        {
            AvailablePoints = 6;
            for (int i = 0; i < 6; i++)
            {
                _successLvl[i] = 0;
                _maxRepairLvl[i] = 0;
                _minRepairLvl[i] = 0;
            }
        }

        // ── Wykrywanie kategorii itemu ────────────────────────────────────────
        public static Category? GetItemCategory(Il2CppCMS.Player.Containers.IBaseItem baseItem)
        {
            try
            {
                string id = baseItem.ID;
                var inv = Il2CppCMS.Core.GameInventory.Instance;
                if (inv?.PartPropertyList == null) return null;
                if (!inv.PartPropertyList.ContainsKey(id)) return null;

                string group = inv.PartPropertyList[id].ShopGroup ?? "";

                return group switch
                {
                    "Engine" or "engine" or "Exhaust" => Category.Engine,
                    "Gearbox" => Category.Drivetrain,
                    "Suspension" or "Tires" or "Rims" => Category.Suspension,
                    "Brakes" => Category.Brakes,
                    "Seats" or "SteeringWheels" or "Benches" => Category.Interior,
                    "Noinshop" when id.StartsWith("car_") && id.Contains("-") => Category.Body,
                    _ => null
                };
            }
            catch { return null; }
        }
    }
}