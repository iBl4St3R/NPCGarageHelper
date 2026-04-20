using System;
using System.Linq;
using System.Reflection;

namespace NPCGarageHelper
{
    /// <summary>
    /// Wrappery na TimeManager i walutę.
    /// TimeManager jest cache'owany przy pierwszym użyciu po Reset() —
    /// zero FindObjectsOfType per frame.
    /// </summary>
    internal static class GameServices
    {
        // ── TimeManager — lazy-resolved once per scene ────────────────────────
        private static object _tmInst;
        private static MethodInfo _getHour, _getMin, _getDay;
        private static bool _tmResolved;

        public static void Reset()
        {
            _tmResolved = false;
            _tmInst = null;
            _getHour = _getMin = _getDay = null;
        }

        private static void EnsureTimeManager()
        {
            if (_tmResolved) return;
            _tmResolved = true;

            try
            {
                var tmType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "Il2CppCMS.Core.TimeManagement.TimeManager");

                if (tmType == null) { Plugin.Log.Warning("[GameServices] TimeManager type not found."); return; }

                var il2T = Il2CppInterop.Runtime.Il2CppType.From(tmType);
                var objs = UnityEngine.Object.FindObjectsOfType(il2T, true);
                if (objs.Length == 0) { Plugin.Log.Warning("[GameServices] No TimeManager instance."); return; }

                _tmInst = Activator.CreateInstance(tmType, new object[] { objs[0].Pointer });
                var f = BindingFlags.Public | BindingFlags.Instance;
                _getHour = tmType.GetMethod("GetCurrentHour", f);
                _getMin = tmType.GetMethod("GetCurrentMinute", f);
                _getDay = tmType.GetMethod("GetCurrentDay", f);

                Plugin.Log.Msg("[GameServices] TimeManager cached OK.");
            }
            catch (Exception ex) { Plugin.Log.Warning($"[GameServices] TimeManager resolve: {ex.Message}"); }
        }

        // ── Public Time API ───────────────────────────────────────────────────

        /// <summary>Zwraca godzinę jako float (np. 8.5 = 08:30).</summary>
        public static float GetGameHour()
        {
            EnsureTimeManager();
            if (_tmInst == null) return 12f;
            try
            {
                int h = (int)_getHour.Invoke(_tmInst, null);
                int m = (int)_getMin.Invoke(_tmInst, null);
                return h + m / 60f;
            }
            catch { return 12f; }
        }

        /// <summary>Zwraca numer dnia gry (do wykrywania nowego dnia dla wypłat).</summary>
        public static int GetGameDay()
        {
            EnsureTimeManager();
            if (_tmInst == null) return 0;
            try { return (int)_getDay.Invoke(_tmInst, null); }
            catch { return 0; }
        }

        // ── Public Money API ──────────────────────────────────────────────────

        /// <summary>Odczyt salda gracza.</summary>
        public static float GetMoney()
        {
            try { return Il2CppCMS.Shared.SharedGameDataManager.Instance.money; }
            catch { return 0f; }
        }

        /// <summary>Pobiera kwotę z konta gracza przez RPC (sieć/lokalne).</summary>
        public static bool SpendMoney(float amount)
        {
            try
            {
                if (GetMoney() < amount) return false;
                Il2CppCMS.Shared.SharedGameDataManager.Instance.AddMoneyRpc(-(int)amount);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[GameServices] SpendMoney({amount}): {ex.Message}");
                return false;
            }
        }
    }
}