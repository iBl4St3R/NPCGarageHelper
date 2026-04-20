using CMS2026UITKFramework;
using UnityEngine;

namespace NPCGarageHelper
{
    internal class SkillsPanel
    {
        private const string PANEL_ID = "NGH_Skills";
        private UIPanel _panel;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public void Open() { _isVisible = true; _panel?.SetVisible(true); }
        public void Close() { _isVisible = false; _panel?.SetVisible(false); }
        public void Toggle() { if (_isVisible) Close(); else Open(); }

        public void Build()
        {
            FrameworkAPI.DestroyPanel(PANEL_ID);

            const int W = 580, H = 580;
            _panel = UIPanel.Create(PANEL_ID, 560, 680, W, H);
            _panel.AddTitleButton("✕", () => Close(),
                new Color(0.42f, 0.08f, 0.08f, 1f));
            _panel.Build(10000);
            _panel.SetScrollbarVisible(false);
            _panel.SetDragWhenScrollable(true);

            StylePanel();
            AddContent();

            _panel.SetVisible(false);
        }

        private void StylePanel()
        {
            var ve = UIRuntime.WrapVE(_panel.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, new Color(0.05f, 0.04f, 0.09f, 0.97f));
            S.BorderRadius(st, 14f);
            S.BorderColor(st, new Color(0.45f, 0.20f, 0.80f, 0.65f));
            S.BorderWidth(st, 1.5f);
        }

        private void AddContent()
        {
            var title = _panel.AddRow(28f, 5f)
                .AddLabel("⚡  SKILLS — NPC GARAGE HELPER", 560f,
                    new Color(0.65f, 0.40f, 1.00f, 1f));
            title.SetFontSize(14);
            _panel.AddSeparator();

            _panel.AddSpace(20f);

            var placeholder = _panel.AddRow(28f, 4f)
                .AddLabel("[ Skill tree — coming soon ]", 560f,
                    new Color(0.35f, 0.35f, 0.45f, 1f));
            placeholder.SetFontSize(13);

            _panel.AddSpace(8f);

            var sub = _panel.AddRow(22f, 2f)
                .AddLabel(
                    "Planowane: odblokowanie naprawy silnika, zawieszenia, nadwozia",
                    560f, new Color(0.30f, 0.30f, 0.40f, 1f));
            sub.SetFontSize(11);
        }
    }
}