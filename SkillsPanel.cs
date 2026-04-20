using CMS2026UITKFramework;
using UnityEngine;

namespace NPCGarageHelper
{
    internal class SkillsPanel
    {
        private const string PANEL_ID = "NGH_Skills";
        private UIPanel _panel;
        private bool _isVisible;

        // ── Widoczność ────────────────────────────────────────────────────────
        public bool IsVisible => _isVisible;
        public void Open() { _isVisible = true; _panel?.SetVisible(true); }
        public void Close() { _isVisible = false; _panel?.SetVisible(false); }
        public void Toggle() { if (_isVisible) Close(); else Open(); }

        // ── UI refs ───────────────────────────────────────────────────────────
        private UILabelHandle _lblPoints;

        public Action OnSkillUpgraded;

        // Per kategoria: 3 labele wartości + 3 przyciski
        private readonly UILabelHandle[] _lblSuccess = new UILabelHandle[6];
        private readonly UILabelHandle[] _lblMaxRepair = new UILabelHandle[6];
        private readonly UILabelHandle[] _lblMinRepair = new UILabelHandle[6];
        private readonly UIButtonHandle[] _btnSuccess = new UIButtonHandle[6];
        private readonly UIButtonHandle[] _btnMaxRepair = new UIButtonHandle[6];
        private readonly UIButtonHandle[] _btnMinRepair = new UIButtonHandle[6];

        // ── Kolory ────────────────────────────────────────────────────────────
        private static readonly Color ColActive = new(0.10f, 0.28f, 0.50f, 1f);
        private static readonly Color ColDisabled = new(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color ColUnlock = new(0.40f, 0.10f, 0.10f, 1f);
        private static readonly Color ColUnlocked = new(0.08f, 0.28f, 0.14f, 1f);

        // ── Build ─────────────────────────────────────────────────────────────
        public void Build()
        {
            FrameworkAPI.DestroyPanel(PANEL_ID);

            const int W = 600, H = 720;
            _panel = UIPanel.Create(PANEL_ID, 640, 20, W, H);
            _panel.AddTitleButton("✕", () => Close(),
                new Color(0.42f, 0.08f, 0.08f, 1f));
            _panel.Build(10001);
            _panel.SetScrollbarVisible(true);
            _panel.SetDragWhenScrollable(true);

            StylePanel();
            AddHeader();

            for (int i = 0; i < 6; i++)
                AddCategorySection((NpcSkillData.Category)i, i);

            _panel.SetVisible(false);
        }

        private void StylePanel()
        {
            var ve = UIRuntime.WrapVE(_panel.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, new Color(0.04f, 0.05f, 0.10f, 1.0f)); // było 0.97f
            S.BorderRadius(st, 14f);
            S.BorderColor(st, new Color(0.45f, 0.20f, 0.80f, 0.65f));
            S.BorderWidth(st, 1.5f);
        }

        private void AddHeader()
        {
            var title = _panel.AddRow(32f, 5f)          // było 28f
                .AddLabel("⚡  SKILLS — NPC GARAGE HELPER", 580f,
                    new Color(0.65f, 0.40f, 1.00f, 1f));
            title.SetFontSize(16);                       // było 14

            _lblPoints = _panel.AddRow(24f, 3f)          // było 22f
                .AddLabel("Skill points: 6", 580f, new Color(1f, 0.85f, 0.20f, 1f));
            _lblPoints.SetFontSize(14);                  // było 12

            _panel.AddSeparator();
        }

        // AddCategorySection() — większe fonty wszędzie
        private void AddCategorySection(NpcSkillData.Category cat, int idx)
        {
            var hdr = _panel.AddRow(28f, 4f)             // było 24f
                .AddLabel(NpcSkillData.CategoryNames[idx], 580f,
                    new Color(0.55f, 0.85f, 1.00f, 1f));
            hdr.SetFontSize(15);                         // było 13

            // Success chance
            {
                var row = _panel.AddRow(26f, 2f);        // było 22f
                var lbl = row.AddLabel("  Szansa naprawy:", 200f, new Color(0.55f, 0.55f, 0.65f, 1f));
                lbl.SetFontSize(13);                     // było 11
                _lblSuccess[idx] = row.AddLabel("--", 160f, new Color(0.80f, 0.80f, 0.90f, 1f));
                _lblSuccess[idx].SetFontSize(13);        // było 11
                _btnSuccess[idx] = row.AddButton("+ Upgrade", 140f, () => { NpcSkillData.UpgradeSuccess(cat); Refresh(); OnSkillUpgraded?.Invoke(); }, ColDisabled);
            }

            // Max repair
            {
                var row = _panel.AddRow(26f, 2f);
                var lbl = row.AddLabel("  Maks. naprawa:", 200f, new Color(0.55f, 0.55f, 0.65f, 1f));
                lbl.SetFontSize(13);
                _lblMaxRepair[idx] = row.AddLabel("--", 160f, new Color(0.80f, 0.80f, 0.90f, 1f));
                _lblMaxRepair[idx].SetFontSize(13);
                _btnMaxRepair[idx] = row.AddButton("+ Upgrade", 140f,() => { NpcSkillData.UpgradeMaxRepair(cat); Refresh(); OnSkillUpgraded?.Invoke(); },ColDisabled);
            }

            // Min repair
            {
                var row = _panel.AddRow(26f, 2f);
                var lbl = row.AddLabel("  Min. naprawa:", 200f, new Color(0.55f, 0.55f, 0.65f, 1f));
                lbl.SetFontSize(13);
                _lblMinRepair[idx] = row.AddLabel("--", 160f, new Color(0.80f, 0.80f, 0.90f, 1f));
                _lblMinRepair[idx].SetFontSize(13);
                _btnMinRepair[idx] = row.AddButton("+ Upgrade", 140f,() => { NpcSkillData.UpgradeMinRepair(cat); Refresh(); OnSkillUpgraded?.Invoke(); },ColDisabled);
            }

            _panel.AddSeparator();
        }

        // ── Refresh ───────────────────────────────────────────────────────────
        public void Refresh()
        {
            _lblPoints?.SetText($"Skill points dostępne: {NpcSkillData.AvailablePoints}");

            for (int i = 0; i < 6; i++)
            {
                var cat = (NpcSkillData.Category)i;
                bool unlocked = NpcSkillData.IsUnlocked(cat);

                // Success chance
                int sLvl = NpcSkillData.GetSuccessLvl(cat);
                _lblSuccess[i]?.SetText(sLvl == 0
                    ? "🔒 Zablokowane"
                    : $"Lvl {sLvl}  ({NpcSkillData.GetSuccessChance(cat):P0})");
                _lblSuccess[i]?.SetColor(sLvl == 0
                    ? new Color(0.6f, 0.3f, 0.3f, 1f)
                    : new Color(0.3f, 1f, 0.5f, 1f));

                bool canUpS = NpcSkillData.CanUpgradeSuccess(cat);
                _btnSuccess[i]?.SetBgColor(sLvl == 0 ? ColUnlock : canUpS ? ColActive : ColDisabled);
                _btnSuccess[i]?.SetText(sLvl == 0
                    ? $"🔓 Odblokuj (1pt)"
                    : canUpS
                        ? $"+ Lvl {sLvl + 1} (1pt)"
                        : sLvl >= NpcSkillData.MAX_SUCCESS_LVL ? "MAX" : "Brak pt");

                // Max repair
                int mrLvl = NpcSkillData.GetMaxRepairLvl(cat);
                _lblMaxRepair[i]?.SetText(unlocked
                    ? $"Lvl {mrLvl}  ({NpcSkillData.GetMaxRepair(cat):P0})"
                    : "— wymaga odblokowania");
                _lblMaxRepair[i]?.SetColor(unlocked
                    ? new Color(0.3f, 0.8f, 1f, 1f)
                    : new Color(0.4f, 0.4f, 0.5f, 1f));

                bool canUpMR = unlocked && NpcSkillData.CanUpgradeMaxRepair(cat);
                _btnMaxRepair[i]?.SetBgColor(canUpMR ? ColActive : ColDisabled);
                _btnMaxRepair[i]?.SetText(canUpMR
                    ? $"+ Lvl {mrLvl + 1} (1pt)"
                    : mrLvl >= NpcSkillData.MAX_MAX_REPAIR_LVL ? "MAX"
                    : !unlocked ? "🔒" : "Brak pt");

                // Min repair
                int mnLvl = NpcSkillData.GetMinRepairLvl(cat);
                _lblMinRepair[i]?.SetText(unlocked
                    ? $"Lvl {mnLvl}  ({NpcSkillData.GetMinRepair(cat):P0})"
                    : "— wymaga odblokowania");
                _lblMinRepair[i]?.SetColor(unlocked
                    ? new Color(0.3f, 0.7f, 1f, 1f)
                    : new Color(0.4f, 0.4f, 0.5f, 1f));

                bool canUpMN = unlocked && NpcSkillData.CanUpgradeMinRepair(cat);
                _btnMinRepair[i]?.SetBgColor(canUpMN ? ColActive : ColDisabled);
                _btnMinRepair[i]?.SetText(canUpMN
                    ? $"+ Lvl {mnLvl + 1} (1pt)"
                    : mnLvl >= NpcSkillData.MAX_MIN_REPAIR_LVL ? "MAX"
                    : !unlocked ? "🔒" : "Brak pt / min≥max");
            }
        }
    }
}