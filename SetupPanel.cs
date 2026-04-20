using CMS2026UITKFramework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCGarageHelper
{
    internal class SetupPanel
    {

        private UILabelHandle _lblLastScan;
        private const string PANEL_ID = "NGH_Setup";
        private const int MAX_STORAGE_ROWS = 6;

        private UIPanel _panel;
        private bool _isVisible;

        // Sekcja 1 — Narzędzia
        private UILabelHandle _lblRepairMain;
        private UILabelHandle _lblRepairBody;
        private UILabelHandle _lblUpgradeTable;

        // Sekcja 2 — Storage (pre-allokowane wiersze)
        private readonly UILabelHandle[] _lblStorages = new UILabelHandle[MAX_STORAGE_ROWS];

        // Sekcja 3 — Status
        private UILabelHandle _lblStAnchor;
        private UILabelHandle _lblStUpgrade;
        private UILabelHandle _lblStInput;
        private UILabelHandle _lblStOutput;
        private UILabelHandle _lblStFunds;

        // ── Widoczność ────────────────────────────────────────────────────────
        public bool IsVisible => _isVisible;
        public void Open() { _isVisible = true; _panel?.SetVisible(true); }
        public void Close() { _isVisible = false; _panel?.SetVisible(false); }
        public void Toggle() { if (_isVisible) Close(); else Open(); }

        // ── Build ─────────────────────────────────────────────────────────────
        public void Build()
        {
            FrameworkAPI.DestroyPanel(PANEL_ID);

            const int W = 580, H = 640;
            _panel = UIPanel.Create(PANEL_ID, 560, 20, W, H);
            _panel.AddTitleButton("✕", () => Close(),
                new Color(0.42f, 0.08f, 0.08f, 1f));
            _panel.Build(10000);
            _panel.SetScrollbarVisible(false);
            _panel.SetDragWhenScrollable(true);

            StylePanel();
            AddTitleRow();
            BuildSection1_Tools();
            BuildSection2_Storages();
            BuildSection3_Status();

            _panel.SetVisible(false);
        }

        private void StylePanel()
        {
            var ve = UIRuntime.WrapVE(_panel.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, new Color(0.04f, 0.06f, 0.09f, 0.97f));
            S.BorderRadius(st, 14f);
            S.BorderColor(st, new Color(0.25f, 0.55f, 0.85f, 0.65f));
            S.BorderWidth(st, 1.5f);
        }

        private void AddTitleRow()
        {
            var lbl = _panel.AddRow(28f, 5f)
                .AddLabel("⚙  SETUP — NPC GARAGE HELPER", 560f,
                    new Color(0.40f, 0.80f, 1.00f, 1f));
            lbl.SetFontSize(14);

            _lblLastScan = _panel.AddRow(18f, 2f)
                .AddLabel("Last scan: —", 560f, new Color(0.40f, 0.40f, 0.50f, 1f));
            _lblLastScan.SetFontSize(11);

            _panel.AddSeparator();
        }

        // ── Sekcja 1: Narzędzia ───────────────────────────────────────────────
        private UILabelHandle _lblBodyTool1;
        private UILabelHandle _lblBodyTool2;

        private void BuildSection1_Tools()
        {
            SectionHeader("NARZĘDZIA");

            _lblRepairMain = MakeRow("  RepairTable (parts)       …");
            _lblRepairBody = MakeRow("  RepairTable (body)        …");
            _lblUpgradeTable = MakeRow("  UpgradeTable              …");
            _lblBodyTool1 = MakeRow("  Body_Repair_Tool_1(Clone) …");
            _lblBodyTool2 = MakeRow("  Body_Repair_Tool_2(Clone) …");

            _panel.AddSeparator();
        }


        // ── Sekcja 2: Storage w zasięgu ───────────────────────────────────────
        private void BuildSection2_Storages()
        {
            SectionHeader("STORAGE W ZASIĘGU  (10 m od RepairTable)");

            for (int i = 0; i < MAX_STORAGE_ROWS; i++)
            {
                _lblStorages[i] = MakeRow("");
                _lblStorages[i].SetVisible(false);
            }

            _panel.AddSeparator();
        }

        // ── Sekcja 3: Status systemu ──────────────────────────────────────────
        private void BuildSection3_Status()
        {
            SectionHeader("STATUS SYSTEMU");

            _lblStAnchor = MakeRow("  … RepairTable anchor");
            _lblStUpgrade = MakeRow("  … UpgradeTable w zasięgu");
            _lblStInput = MakeRow("  … INPUT storage");
            _lblStOutput = MakeRow("  … OUTPUT storage");
            _lblStFunds = MakeRow("  … Środki gracza");

            _panel.AddSeparator();
        }


        // ── Refresh ───────────────────────────────────────────────────────────
        /// <param name="allocatedFunds">Przekazane z WorkshopPanel — ile CR przeznaczono.</param>
        public void Refresh(float allocatedFunds)
        {
            RefreshLastScan();
            RefreshTools();
            RefreshStorages();
            RefreshStatus(allocatedFunds);
        }

        private void RefreshLastScan()
        {
            float t = StorageCache.LastScanTime;
            if (t < 0f)
            {
                _lblLastScan.SetText("Last scan: brak — uruchom grę w garażu");
                _lblLastScan.SetColor(new Color(1f, 0.4f, 0.4f, 1f));
                return;
            }

            float ago = UnityEngine.Time.time - t;
            string agoStr = ago < 60f
                ? $"{ago:F0}s ago"
                : $"{(int)(ago / 60)}m {(int)(ago % 60)}s ago";

            Color col = ago < 15f
                ? new Color(0.30f, 0.90f, 0.50f, 1f)
                : ago < 40f
                    ? new Color(1.0f, 0.70f, 0.20f, 1f)
                    : new Color(0.6f, 0.6f, 0.7f, 1f);

            _lblLastScan.SetText($"Last scan: {agoStr}  (auto co 10s)");
            _lblLastScan.SetColor(col);
        }

        // Overload bez parametru — tylko dla przycisku Rescan wewnątrz panelu
        private void RefreshInternal() => Refresh(GameServices.GetMoney());

        // ── Sekcja 1 ──────────────────────────────────────────────────────────
        private void RefreshTools()
        {
            if (StorageCache.HasAnchor)
            {
                var p = StorageCache.AnchorPos;
                Set(_lblRepairMain,
                    $"  RepairTable (parts)   ✅  @ ({p.x:F2}, {p.y:F2}, {p.z:F2})",
                    ColorOK);
            }
            else
            {
                Set(_lblRepairMain,
                    "  RepairTable (parts)   ❌  nie wykryto",
                    ColorErr);
            }

            Vector3? bodyPos = StorageCache.BodyRepairTablePos;
            if (bodyPos.HasValue)
            {
                var p = bodyPos.Value;
                Set(_lblRepairBody,
                    $"  RepairTable (body)    ✅  @ ({p.x:F2}, {p.y:F2}, {p.z:F2})  [nieużywany]",
                    ColorDim);
            }
            else
            {
                Set(_lblRepairBody,
                    "  RepairTable (body)    —  nie znaleziono",
                    ColorDim);
            }

            if (StorageCache.UpgradeTable != null)
            {
                var p = StorageCache.UpgradeTable.transform.position;
                float d = Vector3.Distance(StorageCache.AnchorPos, p);
                Set(_lblUpgradeTable,
                    $"  UpgradeTable          ✅  @ ({p.x:F2}, {p.y:F2}, {p.z:F2})  dist: {d:F1}m",
                    ColorOK);
            }
            else
            {
                Set(_lblUpgradeTable,
                    "  UpgradeTable          ⚠   nie znaleziono w 10 m",
                    ColorWarn);
            }


            // Body Repair Tools
            if (StorageCache.BodyRepairTool1 != null)
            {
                var p = StorageCache.BodyRepairTool1.position;
                float d = Vector3.Distance(StorageCache.AnchorPos, p);
                Set(_lblBodyTool1,
                    $"  Body_Repair_Tool_1  ✅  @ ({p.x:F1}, {p.z:F1})  dist: {d:F1}m",
                    ColorDim);
            }
            else
                Set(_lblBodyTool1, "  Body_Repair_Tool_1  —  poza zasięgiem lub brak", ColorDim);

            if (StorageCache.BodyRepairTool2 != null)
            {
                var p = StorageCache.BodyRepairTool2.position;
                float d = Vector3.Distance(StorageCache.AnchorPos, p);
                Set(_lblBodyTool2,
                    $"  Body_Repair_Tool_2  ✅  @ ({p.x:F1}, {p.z:F1})  dist: {d:F1}m",
                    ColorDim);
            }
            else
                Set(_lblBodyTool2, "  Body_Repair_Tool_2  —  poza zasięgiem lub brak", ColorDim);

        }

        // ── Sekcja 2 ──────────────────────────────────────────────────────────
        private void RefreshStorages()
        {
            for (int i = 0; i < MAX_STORAGE_ROWS; i++)
                _lblStorages[i].SetVisible(false);

            var all = StorageCache.GetAllStoragesWithDistance();

            if (all == null || all.Count == 0)
            {
                Set(_lblStorages[0],
                    "  Brak storage w zasięgu 10 m — umieść co najmniej 2",
                    ColorErr);
                _lblStorages[0].SetVisible(true);
                return;
            }

            int rows = Math.Min(all.Count, MAX_STORAGE_ROWS);
            for (int i = 0; i < rows; i++)
            {
                var (dist, wo) = all[i];

                string tag;
                Color col;

                if (wo == StorageCache.InputStorage)
                    (tag, col) = ("← INPUT", ColorOK);
                else if (wo == StorageCache.OutputStorage)
                    (tag, col) = ("← OUTPUT", ColorBlue);
                else
                    (tag, col) = ("  (ignorowany — za daleko)", ColorDim);

                int items = 0, max = 0;
                try { items = wo.ItemsCount; max = wo.MaxCapacity; } catch { }

                string name = "?";
                try { name = wo.StorageName; } catch { }

                Set(_lblStorages[i],
                    $"  [{i}] {name,-18}  dist: {dist,5:F1}m   items: {items,3}/{max,-3}  {tag}",
                    col);
                _lblStorages[i].SetVisible(true);
            }
        }

        // ── Sekcja 3 ──────────────────────────────────────────────────────────
        private void RefreshStatus(float allocatedFunds)
        {
            StatusRow(_lblStAnchor,
                StorageCache.HasAnchor,
                "RepairTable anchor OK",
                "Brak RepairTable — kliknij Rescan");

            StatusRow(_lblStUpgrade,
                StorageCache.UpgradeTable != null,
                "UpgradeTable w zasięgu",
                "UpgradeTable poza zasięgiem — patrol ograniczony",
                warn: true);

            StatusRow(_lblStInput,
                StorageCache.InputStorage != null,
                "INPUT storage przypisany",
                "Brak INPUT storage — kliknij Rescan");

            StatusRow(_lblStOutput,
                StorageCache.OutputStorage != null,
                "OUTPUT storage przypisany",
                "Brak OUTPUT storage  (potrzeba 2 w zasięgu)");

            StatusRow(_lblStFunds,
                allocatedFunds >= 1f,
                $"Środki: {allocatedFunds:F0} CR  — OK",
                "Brak środków — użyj +500 / +2000 w głównym panelu");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private UILabelHandle MakeRow(string text)
        {
            var lbl = _panel.AddRow(22f, 2f)
                .AddLabel(text, 560f, ColorDim);
            lbl.SetFontSize(12);
            return lbl;
        }

        private void SectionHeader(string text)
        {
            var lbl = _panel.AddRow(22f, 4f)
                .AddLabel($"▸  {text}", 560f,
                    new Color(0.40f, 0.78f, 1.00f, 1f));
            lbl.SetFontSize(12);
        }

        private static void Set(UILabelHandle lbl, string text, Color color)
        {
            lbl.SetText(text);
            lbl.SetColor(color);
        }

        private void StatusRow(UILabelHandle lbl, bool ok,
            string okText, string failText, bool warn = false)
        {
            if (ok)
                Set(lbl, $"  ✅  {okText}", ColorOK);
            else if (warn)
                Set(lbl, $"  ⚠   {failText}", ColorWarn);
            else
                Set(lbl, $"  ❌  {failText}", ColorErr);
        }

       

        // Kolory — stałe
        private static readonly Color ColorOK = new(0.20f, 0.90f, 0.40f, 1f);
        private static readonly Color ColorWarn = new(1.00f, 0.70f, 0.20f, 1f);
        private static readonly Color ColorErr = new(1.00f, 0.30f, 0.30f, 1f);
        private static readonly Color ColorDim = new(0.45f, 0.45f, 0.55f, 1f);
        private static readonly Color ColorBlue = new(0.25f, 0.70f, 1.00f, 1f);
    }
}