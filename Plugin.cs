using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Linq;

[assembly: MelonInfo(typeof(NPCGarageHelper.Plugin),
    "NPCGarageHelper", "0.1.0", "iBlaster")]
[assembly: MelonGame("Red Dot Games", "Car Mechanic Simulator 2026 Demo")]
[assembly: MelonGame("Red Dot Games", "Car Mechanic Simulator 2026")]

namespace NPCGarageHelper
{
    public class Plugin : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Plugin>.Logger;

        private WorkshopPanel _panel;


        private Il2CppCMS.Core.GameScript _gameScript;
        private bool _gsResolved;


        // ── Scene load ────────────────────────────────────────────────────────
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (!sceneName.ToLower().Contains("garage")) return;
            if (!FrameworkAPI.IsReady) return;

            // Reset cached singletons przy każdym załadowaniu sceny
            GameServices.Reset();
            StorageCache.Reset();

            // Cursor hooks — unsubscribe first, żeby nie duplikować
            CursorManager.OnCursorShow -= OnCursorShow;
            CursorManager.OnCursorHide -= OnCursorHide;
            CursorManager.OnCursorShow += OnCursorShow;
            CursorManager.OnCursorHide += OnCursorHide;

            _panel = new WorkshopPanel();
            _panel.Build();

            TryRegisterConsole();

            _gsResolved = false;
            _gameScript = null;
        }

        // ── Cursor ────────────────────────────────────────────────────────────
        private static void OnCursorShow()
        {
            try
            {
                if (Il2CppCMS.Core.GameMode.Get().currentMode != Il2Cpp.gameMode.UI)
                    Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.UI);
            }
            catch (Exception ex) { Log.Warning($"[NGH] OnCursorShow: {ex.Message}"); }
        }

        private void OnCursorHide()
        {
            try
            {
                if (Il2CppCMS.Core.GameMode.Get().currentMode == Il2Cpp.gameMode.UI)
                {
                    var wm = Il2CppCMS.UI.WindowManager.Instance;
                    if (wm == null || wm.activeWindows.Count <= 0)
                        Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.Garage);
                }
            }
            catch (Exception ex) { Log.Warning($"[NGH] OnCursorHide: {ex.Message}"); }
        }

        // ── Per-frame tick ────────────────────────────────────────────────────
        public override void OnUpdate()
        {
            _panel?.Tick(UnityEngine.Time.deltaTime);

            // ESC zamyka panel
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
            {
                if (_panel != null && _panel.IsSetupVisible) { _panel.CloseSetup(); return; }
                if (_panel != null && _panel.IsSkillsVisible) { _panel.CloseSkills(); return; }
                if (_panel != null && _panel.IsVisible) { _panel.Close(); return; }
            }

            // Interakcja przez E — tylko gdy panel zamknięty
            if (_panel == null || _panel.IsVisible) return;
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.E)) return;

            EnsureGameScript();
            if (_gameScript == null) return;

            try
            {
                var hit = _gameScript.raycast.hit;
                var t = hit.transform;
                if (t == null) return;

                string hitName = t.gameObject.name;

                // Trigger 1: klik E na NPC
                if (hitName == "NGH_WorkerNPC")
                {
                    Plugin.Log.Msg("[NGH] E na NPC → Toggle panel");
                    _panel.Toggle();
                    return;
                }

                // Trigger 2: klik E na UpgradeTable (hit trafia w "Logic" child)
                if (IsUpgradeTable(t))
                {
                    Plugin.Log.Msg("[NGH] E na UpgradeTable → Toggle panel");
                    _panel.Toggle();
                    return;
                }
            }
            catch { }
        }


        // Sprawdza czy transform lub któryś z jego rodziców to Upgrade_Table
        private static bool IsUpgradeTable(UnityEngine.Transform t)
        {
            for (int i = 0; i < 4; i++)   // max 4 poziomy w górę
            {
                if (t == null) return false;
                if (t.name.StartsWith("Upgrade_Table")) return true;
                t = t.parent;
            }
            return false;
        }

        private void EnsureGameScript()
        {
            if (_gsResolved) return;
            _gsResolved = true;
            try
            {
                _gameScript = Il2CppCMS.Core.GameScript.Get();
                Plugin.Log.Msg("[NGH] GameScript cached OK");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[NGH] GameScript resolve: {ex.Message}");
            }
        }



        // ── Console integration ───────────────────────────────────────────────
        private void TryRegisterConsole()
        {
            try
            {
                var apiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "CMS2026SimpleConsole.ConsoleAPI");

                if (apiType == null) return;

                var print = apiType.GetMethod("Print", new[] { typeof(string), typeof(string) });
                var register = apiType.GetMethod("RegisterCommand");

                void Print(string msg) => print?.Invoke(null, new object[] { msg, "NGH" });

                apiType.GetMethod("RegisterMod")?.Invoke(null, new object[]
                {
                    "NPCGarageHelper",
                    "NPC Garage Helper",
                    "YourName",
                    "Automated NPC workshop worker",
                    null, null, null
                });

                register?.Invoke(null, new object[]
{
    "ngh_addxp",
    "ngh_addxp <n> — dodaj XP do NPC",
    (Action<string[]>)(args =>
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int xp))
        { Print("Użycie: ngh_addxp <n>"); return; }
        _panel?.AddXpPublic(xp);
        Print($"Dodano {xp} XP do NPC.");
    })
});

                register?.Invoke(null, new object[]
                {
    "ngh_resetskills",
    "Resetuje skille NPC i oddaje skill pointy",
    (Action<string[]>)(_ =>
    {
        NpcSkillData.Reset();
        _panel?.RefreshSkillsIfOpen();
        Print($"Skille zresetowane. Dostępne punkty: {NpcSkillData.AvailablePoints}");
    })
                });


                register?.Invoke(null, new object[]
                {
                    "ngh_open",
                    "Toggle the NPC Garage Helper panel",
                    (Action<string[]>)(_ => _panel?.Toggle())
                });

                register?.Invoke(null, new object[]
                {
                    "ngh_scan",
                    "Force a storage scan",
                    (Action<string[]>)(_ =>
                    {
                        var result = StorageCache.Scan();
                        Print($"Scan result: {result}  " +
                              $"IN={StorageCache.InputStorage?.StorageName ?? "null"}  " +
                              $"OUT={StorageCache.OutputStorage?.StorageName ?? "null"}");
                    })
                });

                register?.Invoke(null, new object[]
                {
                    "ngh_status",
                    "Print current workshop state",
                    (Action<string[]>)(_ =>
                    {
                        Print($"Money: {GameServices.GetMoney():F0} CR");
                        Print($"Time:  {GameServices.GetGameHour():F2}h  Day:{GameServices.GetGameDay()}");
                        Print($"Input:  {StorageCache.InputStorage?.StorageName ?? "none"} " +
                              $"[{StorageCache.InputStorage?.ItemsCount ?? 0}/" +
                              $"{StorageCache.InputStorage?.MaxCapacity ?? 0}]");
                        Print($"Output: {StorageCache.OutputStorage?.StorageName ?? "none"}");
                    })
                });

                Log.Msg("[NGH] Registered in SimpleConsole.");
            }
            catch (Exception ex) { Log.Warning($"[NGH] Console registration failed: {ex.Message}"); }
        }
    }
}