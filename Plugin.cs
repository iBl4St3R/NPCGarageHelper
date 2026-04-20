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

            // Reset cached singletons on every scene load
            GameServices.Reset();
            StorageCache.Reset();

            // Cursor hooks — unsubscribe first to avoid duplication
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
                    {
                        Il2CppCMS.Core.GameMode.Get().SetCurrentMode(Il2Cpp.gameMode.Garage);

                        // Force raycast refresh so the game updates hover/outline
                        _gameScript = null;
                        _gsResolved = false;
                    }
                }
            }
            catch (Exception ex) { Log.Warning($"[NGH] OnCursorHide: {ex.Message}"); }
        }

        // ── Per-frame tick ────────────────────────────────────────────────────
        public override void OnUpdate()
        {
            _panel?.Tick(UnityEngine.Time.deltaTime);

            // ESC closes the panel
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
            {
                if (_panel != null && _panel.IsSetupVisible) { _panel.CloseSetup(); return; }
                if (_panel != null && _panel.IsSkillsVisible) { _panel.CloseSkills(); return; }
                if (_panel != null && _panel.IsVisible) { _panel.Close(); return; }
            }

            // Interaction via E — only when panel is closed
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

                // Trigger 1: click E on NPC
                if (hitName == "NGH_WorkerNPC")
                {
                    Plugin.Log.Msg("[NGH] E on NPC → Toggle panel");
                    _panel.Toggle();
                    return;
                }

                // Trigger 2: click E on UpgradeTable (hit hits "Logic" child)
                if (IsUpgradeTable(t))
                {
                    Plugin.Log.Msg("[NGH] E on UpgradeTable → Toggle panel");
                    _panel.Toggle();
                    return;
                }
            }
            catch { }
        }


        // Checks if transform or any of its parents is Upgrade_Table
        private static bool IsUpgradeTable(UnityEngine.Transform t)
        {
            for (int i = 0; i < 4; i++)   // max 4 levels up
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
    "ngh_addxp <n> — add XP to NPC",
    (Action<string[]>)(args =>
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int xp))
        { Print("Usage: ngh_addxp <n>"); return; }
        _panel?.AddXpPublic(xp);
        Print($"Added {xp} XP to NPC.");
    })
});

                register?.Invoke(null, new object[]
                {
    "ngh_resetskills",
    "Resets NPC skills and refunds skill points",
    (Action<string[]>)(_ =>
    {
        NpcSkillData.Reset();
        _panel?.RefreshSkillsIfOpen();
        Print($"Skills reset. Available points: {NpcSkillData.AvailablePoints}");
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