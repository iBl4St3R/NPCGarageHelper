using CMS2026UITKFramework;

internal class SetupPanel
{
    private const string PANEL_ID = "NGH_Setup";
    private UIPanel _panel;
    private bool _isVisible;

    public bool IsVisible => _isVisible;
    public void Open() { _isVisible = true; _panel?.SetVisible(true); }
    public void Close() { _isVisible = false; _panel?.SetVisible(false); }
    public void Toggle() { if (_isVisible) Close(); else Open(); }

    public void Build() { /* tworzy panel, SetVisible(false) na końcu */ }
    public void Refresh() { /* wypełnia etykiety aktualnym stanem */ }
}