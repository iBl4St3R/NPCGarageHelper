using CMS2026UITKFramework;
using System;
using UnityEngine;

namespace NPCGarageHelper
{
    internal class WorkshopPanel
    {
        // ── Auto-scan ──────────────────────────────────────────────────────────
        private float _autoScanTimer = 0f;
        private const float AUTO_SCAN_INTERVAL = 10f;

        // ── Stałe ─────────────────────────────────────────────────────────────
        private const string PANEL_ID = "NGH_Workshop_Main";
        private const float WORK_START = 8f;
        private const float WORK_END = 16f;
        private const float DAILY_WAGE = 600f;
        private const float REPAIR_COST = 50f;
        private const float REPAIR_TIME_BASE = 30f;

        // ── Stan ──────────────────────────────────────────────────────────────
        private bool _npcHired;
        private float _repairTimer;
        private string _currentItemId = "";
        private string _currentItemName = "";
        private int _totalRepaired;
        private int _lastWageDay = -1;
        private bool _wasWorkTime;
        private float _allocatedFunds;

        private readonly System.Collections.Generic.HashSet<ulong> _repairedUIDs = new System.Collections.Generic.HashSet<ulong>();

        private readonly WorkerNPC _npc = new WorkerNPC();
        private static readonly System.Random _rng = new System.Random();

        // ── Stan NPC — level/xp ───────────────────────────────────────────────────
        private int _npcXp = 0;
        private int _npcLevel = 1;


        // ── UI refs ───────────────────────────────────────────────────────────
        private UIPanel _panel;
        private UILabelHandle _lblTick, _lblHour, _lblTitle;
        private UILabelHandle _lblState;
        private UIProgressBarHandle _pbRepair;
        private UILabelHandle _lblNpcStatus;
        private UILabelHandle _lblInput, _lblOutput;
        private UILabelHandle _lblWork;
        private UILabelHandle _lblFunds, _lblStats;
        private UIButtonHandle _btnSetupBadge;

        private SetupPanel _setupPanel;
        // ── UI refs — level ───────────────────────────────────────────────────────
        private UIProgressBarHandle _pbXp;
        private UILabelHandle _lblNpcLevel;
        private UILabelHandle _lblXpValue;

        // ── Panele pomocnicze ─────────────────────────────────────────────────────
        private SkillsPanel _skillsPanel;

        // ── Ticker ────────────────────────────────────────────────────────────
        private static readonly string[] TickFrames = { "●", "○" };
        private int _tickIdx;
        private float _tickTimer;

        // ── Widoczność ────────────────────────────────────────────────────────
        private bool _isVisible;
        public bool IsVisible => _isVisible;
        public void Open() { _isVisible = true; _panel?.SetVisible(true); }
        public void Close() { _isVisible = false; _panel?.SetVisible(false); }
        public void Toggle() { if (_isVisible) Close(); else Open(); }


        public bool IsSetupVisible => _setupPanel?.IsVisible ?? false;
        public void CloseSetup() => _setupPanel?.Close();
        public bool IsSkillsVisible => _skillsPanel?.IsVisible ?? false;
        public void CloseSkills() => _skillsPanel?.Close();


        // ── Build ─────────────────────────────────────────────────────────────
        public void Build()
        {
            FrameworkAPI.DestroyPanel(PANEL_ID);
            StorageCache.FindAnchor();

            const int W = 500, H = 620;
            _panel = UIPanel.Create(PANEL_ID, 30, 20, W, H);
            _panel.AddTitleButton("✕", () => Close(),
                new Color(0.42f, 0.08f, 0.08f, 1f));
            _panel.Build(9999);
            _panel.SetScrollbarVisible(false);
            _panel.SetDragWhenScrollable(true);

            StylePanel();
            AddHeader();
            AddStateSection();
            AddStatusSection();
            AddWorkSection();
            AddFundsSection();
            AddLevelSection();
            AddStatsSection();
            AddControlButtons();

            _setupPanel = new SetupPanel();
            _setupPanel.Build();

            _skillsPanel = new SkillsPanel();
            _skillsPanel.Build();

            _panel.SetVisible(false);
        }

        // ── Style ─────────────────────────────────────────────────────────────
        private void StylePanel()
        {
            var ve = UIRuntime.WrapVE(_panel.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, new Color(0.05f, 0.07f, 0.08f, 0.97f));
            S.BorderRadius(st, 14f);
            S.BorderColor(st, new Color(0.20f, 0.70f, 0.40f, 0.60f));
            S.BorderWidth(st, 1.5f);
        }

        // ── Sekcje ────────────────────────────────────────────────────────────
        private void AddHeader()
        {
            var row = _panel.AddRow(30f, 5f);
            _lblTick = row.AddLabel("●", 24f, new Color(0.2f, 0.9f, 0.4f, 1f));
            _lblTitle = row.AddLabel("NPC GARAGE HELPER", 280f, new Color(0.3f, 1f, 0.5f, 1f));
            _lblTitle.SetFontSize(15);
            _lblHour = row.AddLabel("--:--", 130f, new Color(0.5f, 0.5f, 0.6f, 1f));
            _lblHour.SetFontSize(13);
            _panel.AddSeparator();
        }

        private void AddStateSection()
        {
            _lblState = _panel.AddRow(28f, 4f)
                .AddLabel("⏸ Oczekiwanie na zatrudnienie", 480f,
                    new Color(0.6f, 0.6f, 0.7f, 1f));
            _lblState.SetFontSize(14);

            _pbRepair = _panel.AddProgressBar("Postęp naprawy:", 0f,
                fillColor: new Color(0.2f, 0.8f, 0.4f, 1f), height: 28f);

            _panel.AddSeparator();
        }

        private void AddStatusSection()
        {
            _lblNpcStatus = _panel.AddRow(24f, 3f)
                .AddLabel("● NPC: niezatrudniony", 480f, new Color(0.5f, 0.5f, 0.6f, 1f));
            _lblNpcStatus.SetFontSize(13);

            _lblInput = _panel.AddRow(22f, 2f).AddLabel("INPUT:  --", 480f, new Color(0.4f, 0.4f, 0.5f, 1f));
            _lblOutput = _panel.AddRow(22f, 2f).AddLabel("OUTPUT: --", 480f, new Color(0.4f, 0.4f, 0.5f, 1f));
            _lblInput.SetFontSize(12);
            _lblOutput.SetFontSize(12);
            _panel.AddSeparator();
        }

        private void AddWorkSection()
        {
            _lblWork = _panel.AddRow(24f, 3f)
                .AddLabel("Brak pracy", 480f, new Color(0.5f, 0.5f, 0.6f, 1f));
            _lblWork.SetFontSize(13);
            _panel.AddSeparator();
        }

        private void AddFundsSection()
        {
            var row = _panel.AddRow(28f, 5f);
            _lblFunds = row.AddLabel("Środki: 0 CR", 230f, new Color(1f, 0.8f, 0.2f, 1f));
            _lblFunds.SetFontSize(13);
            row.AddButton("+500", 100f, () => AllocateFunds(500f), new Color(0.08f, 0.26f, 0.12f, 1f));
            row.AddButton("+2000", 100f, () => AllocateFunds(2000f), new Color(0.08f, 0.26f, 0.12f, 1f));
        }

        private void AddLevelSection()
        {
            var row = _panel.AddRow(24f, 4f);
            _lblNpcLevel = row.AddLabel("NPC  LVL 1", 120f, new Color(0.4f, 0.8f, 1.0f, 1f));
            _lblNpcLevel.SetFontSize(13);
            _pbXp = row.AddProgressBar(70f, 0f, fillColor: new Color(0.3f, 0.6f, 1.0f, 1f));
            _lblXpValue = row.AddLabel("0 / 1000 XP", 160f, new Color(0.45f, 0.55f, 0.70f, 1f));
            _lblXpValue.SetFontSize(11);
            _panel.AddSeparator();
        }

        private void AddStatsSection()
        {
            _lblStats = _panel.AddRow(22f, 3f)
                .AddLabel("Naprawiono: 0  |  wydano: 0 CR", 480f,
                    new Color(0.4f, 0.6f, 0.8f, 1f));
            _lblStats.SetFontSize(12);
            _panel.AddSeparator();
        }

        private void AddControlButtons()
        {
            var row = _panel.AddRow(34f, 5f);

            _btnSetupBadge = row.AddButton(
                "⚙ SETUP", 100f,
                () => { _setupPanel?.Refresh(_allocatedFunds); _setupPanel?.Toggle(); },
                BadgeColor());

            row.AddButton("⚡ Skills", 95f,
                () => _skillsPanel?.Toggle(),
                new Color(0.15f, 0.15f, 0.35f, 1f));

            row.AddButton("✓ Zatrudnij", 130f, OnHire, new Color(0.08f, 0.26f, 0.12f, 1f));
            row.AddButton("✗ Zwolnij", 110f, OnFire, new Color(0.35f, 0.08f, 0.08f, 1f));
        }

        // ── Przyciski ─────────────────────────────────────────────────────────
        private void AllocateFunds(float amount)
        {
            if (!GameServices.SpendMoney(amount))
            {
                _lblWork.SetText($"❌ Za mało CR ({GameServices.GetMoney():F0} / {amount:F0})");
                return;
            }
            _allocatedFunds += amount;
            _lblFunds.SetText($"Środki: {_allocatedFunds:F0} CR");
            RefreshBadge();
        }

        private void OnHire()
        {
            string status = BuildSetupStatus();
            if (!status.StartsWith("✅"))
            {
                _lblWork.SetText(status);
                return;
            }
            if (GameServices.GetMoney() < DAILY_WAGE)
            {
                _lblWork.SetText($"❌ Potrzeba {DAILY_WAGE:F0} CR na pierwszą wypłatę");
                return;
            }

            GameServices.SpendMoney(DAILY_WAGE);
            _lastWageDay = GameServices.GetGameDay();

            var spawnPos = StorageCache.AnchorPos + new Vector3(1.5f, 0f, 0f);
            if (!_npc.TrySpawn(spawnPos))
            {
                _lblWork.SetText("❌ Spawn NPC failed — sprawdź logi");
                return;
            }

            _npcHired = true;
            SetNpcStatusLabel("● NPC: zatrudniony", true);
            RefreshBadge();
            Plugin.Log.Msg("[WorkshopPanel] NPC zatrudniony!");
        }

        private void OnFire()
        {
            _npcHired = false;
            _currentItemId = "";
            _currentItemName = "";
            _repairTimer = 0f;
            _npc.ResetPatrol();
            _repairedUIDs.Clear();
            _npc.Despawn();
            SetNpcStatusLabel("● NPC: niezatrudniony", false);
            _lblWork.SetText("Brak pracy");
            _lblState.SetText("⏸ Niezatrudniony");
            _lblState.SetColor(new Color(0.5f, 0.5f, 0.6f, 1f));
            _pbRepair.SetValue(0f);
            Plugin.Log.Msg("[WorkshopPanel] NPC zwolniony.");
        }

        private void OnScan()
        {
            StorageCache.Scan();
            RefreshBadge();
            RefreshStorageLabels();
            if (_setupPanel?.IsVisible == true)
                _setupPanel.Refresh(_allocatedFunds);
        }

        // ── Per-frame Tick ────────────────────────────────────────────────────
        public void Tick(float dt)
        {
            if (_panel == null) return;

            // Ticker
            _tickTimer += dt;
            if (_tickTimer >= 0.5f)
            {
                _tickTimer = 0f;
                _tickIdx = 1 - _tickIdx;
                _lblTick.SetText(TickFrames[_tickIdx]);
            }

            // Auto-scan co 10s
            _autoScanTimer -= dt;
            if (_autoScanTimer <= 0f)
            {
                _autoScanTimer = AUTO_SCAN_INTERVAL;
                StorageCache.Scan();
                RefreshBadge();
                RefreshStorageLabels();
                if (_setupPanel?.IsVisible == true)
                    _setupPanel.Refresh(_allocatedFunds);
            }

            float hour = GameServices.GetGameHour();
            _lblHour.SetText($"{(int)hour:D2}:{(int)((hour % 1f) * 60):D2}");

            UpdateHeaderColor();

            if (!_npcHired) return;

            // Dzienna wypłata
            int today = GameServices.GetGameDay();
            if (today != _lastWageDay)
            {
                if (!GameServices.SpendMoney(DAILY_WAGE))
                {
                    _lblWork.SetText("❌ Brak CR na wypłatę — NPC zwolniony");
                    OnFire();
                    return;
                }
                _lastWageDay = today;
                Plugin.Log.Msg($"[WorkshopPanel] Wypłata: {DAILY_WAGE} CR (dzień {today})");
            }

            bool isWorkTime = hour >= WORK_START && hour < WORK_END;
            if (isWorkTime != _wasWorkTime)
            {
                _wasWorkTime = isWorkTime;
                _npc.SetVisible(isWorkTime);
                if (!isWorkTime)
                {
                    _npc.SetIdle();
                    _currentItemId = "";
                    _currentItemName = "";
                    _repairTimer = 0f;
                    _pbRepair.SetValue(0f);
                }
            }

            if (!isWorkTime)
            {
                SetNpcStatusLabel("● NPC: poza godzinami (8:00-16:00)", false);
                _lblWork.SetText("NPC odpoczywa — model ukryty");
                SetStateLabel("😴 Poza godzinami pracy", new Color(0.7f, 0.5f, 0.2f, 1f));
                return;
            }

            SetNpcStatusLabel("● NPC: pracuje", true);
            TickRepairLogic(dt);
        }

        // ── Kolor nagłówka ────────────────────────────────────────────────────
        private void UpdateHeaderColor()
        {
            Color tickCol, titleCol;

            if (!_npcHired)
            {
                bool hasError = !StorageCache.HasAnchor
                             || StorageCache.InputStorage == null
                             || StorageCache.OutputStorage == null;

                if (hasError)
                {
                    tickCol = new Color(1.0f, 0.25f, 0.25f, 1f);
                    titleCol = new Color(1.0f, 0.35f, 0.35f, 1f);
                }
                else
                {
                    tickCol = new Color(0.5f, 0.5f, 0.6f, 1f);
                    titleCol = new Color(0.5f, 0.6f, 0.7f, 1f);
                }
            }
            else
            {
                float hour = GameServices.GetGameHour();
                bool isWorkTime = hour >= WORK_START && hour < WORK_END;

                if (!isWorkTime)
                {
                    tickCol = new Color(1.0f, 0.60f, 0.10f, 1f);
                    titleCol = new Color(1.0f, 0.65f, 0.20f, 1f);
                }
                else if (!string.IsNullOrEmpty(_currentItemId))
                {
                    tickCol = new Color(0.20f, 1.0f, 0.40f, 1f);
                    titleCol = new Color(0.25f, 1.0f, 0.45f, 1f);
                }
                else if (!StorageCache.IsValid())
                {
                    tickCol = new Color(1.0f, 0.25f, 0.25f, 1f);
                    titleCol = new Color(1.0f, 0.35f, 0.35f, 1f);
                }
                else
                {
                    tickCol = new Color(1.0f, 0.60f, 0.10f, 1f);
                    titleCol = new Color(1.0f, 0.65f, 0.20f, 1f);
                }
            }

            _lblTick?.SetColor(tickCol);
            _lblTitle?.SetColor(titleCol);
        }

        // ── Logika naprawy ────────────────────────────────────────────────────
        private void TickRepairLogic(float dt)
        {
            if (!string.IsNullOrEmpty(_currentItemId))
            {
                _repairTimer -= dt;
                float repTime = RepairTime();
                float progress = 1f - Mathf.Clamp01(_repairTimer / repTime);

                _pbRepair.SetValue(progress);
                SetStateLabel($"🔧 Naprawia: {_currentItemName}", new Color(0.3f, 1f, 0.5f, 1f));
                _lblWork.SetText($"Naprawia: {_currentItemName}  ({_repairTimer:F0}s)");

                if (StorageCache.IsValid() && StorageCache.UpgradeTable != null)
                    _npc.PatrolTick(dt,
                        StorageCache.AnchorPos,
                        StorageCache.UpgradeTable.transform.position,
                        StorageCache.InputStorage.transform.position,
                        StorageCache.OutputStorage.transform.position);

                if (_repairTimer > 0f) return;

                TryTransferItem();
                _currentItemId = "";
                _currentItemName = "";
                _pbRepair.SetValue(0f);
                _lblWork.SetText("Szuka następnej części…");
                return;
            }

            if (StorageCache.IsValid())
                _npc.PatrolTick(dt, StorageCache.AnchorPos,
                    StorageCache.UpgradeTable.transform.position,
                    StorageCache.InputStorage.transform.position,
                    StorageCache.OutputStorage.transform.position);

            var nextItem = FindNextItem();
            if (nextItem == null)
            {
                _npc.SetIdle();
                _lblWork.SetText("📭 Brak części w INPUT — czeka");
                SetStateLabel("📭 Brak części — idle", new Color(0.5f, 0.5f, 0.6f, 1f));
                return;
            }

            _currentItemId = nextItem.ID;
            _currentItemName = nextItem.GetLocalizedName();
            _repairTimer = RepairTime();

            _lblWork.SetText($"🔧 Naprawia: {_currentItemName}");
            SetStateLabel($"🔧 Naprawia: {_currentItemName}", new Color(0.3f, 1f, 0.5f, 1f));
            Plugin.Log.Msg($"[Workshop] Start: {_currentItemName}  t={_repairTimer:F0}s");
        }

        private float RepairTime() => REPAIR_TIME_BASE * (1f - (_npcLevel - 1) / 60f);

        private Il2CppCMS.Player.Containers.IBaseItem FindNextItem()
        {
            try
            {
                var list = StorageCache.InputStorage.ItemsManager.GetItemsForRepairTable(false);
                if (list != null)
                    foreach (var candidate in list)
                    {
                        if (candidate == null) continue;
                        if (_repairedUIDs.Contains(candidate.UID)) continue;
                        if (candidate.GetConditionToShow() >= 0.99f) continue;
                        return candidate;
                    }
            }
            catch { }
            try
            {
                var stacks = StorageCache.InputStorage.ItemsManager.Stacks;
                for (int i = 0; i < stacks.Count; i++)
                {
                    var s = stacks[i];
                    if (s == null || s.Items.Count == 0) continue;
                    var candidate = s.Items[0];
                    if (candidate == null) continue;
                    if (_repairedUIDs.Contains(candidate.UID)) continue;
                    if (candidate.GetConditionToShow() >= 0.99f) continue;
                    return candidate;
                }
            }
            catch { }
            return null;
        }

        private void TryTransferItem()
        {
            try
            {
                var stacks = StorageCache.InputStorage.ItemsManager.Stacks;
                Il2CppCMS.Player.Containers.IBaseItem baseItem = null;

                for (int i = 0; i < stacks.Count; i++)
                {
                    var s = stacks[i];
                    if (s?.ItemID != _currentItemId) continue;
                    if (s.Items.Count > 0) { baseItem = s.Items[0]; break; }
                }

                if (baseItem == null || !StorageCache.OutputStorage.ItemsManager.CanAddItems()) return;

                bool repairSuccess = TryRepairItem(baseItem, out float condBefore, out float condAfter);

                StorageCache.InputStorage.ItemsManager.Delete(baseItem);
                StorageCache.OutputStorage.ItemsManager.Add(baseItem, false);

                _totalRepaired++;
                _allocatedFunds -= REPAIR_COST;
                _lblFunds.SetText($"Środki: {_allocatedFunds:F0} CR");

                if (repairSuccess)
                {
                    _repairedUIDs.Add(baseItem.UID);
                    AddXp(20);
                    _lblStats.SetText(
                        $"Naprawiono: {_totalRepaired}  |  wydano: {_totalRepaired * REPAIR_COST:F0} CR");
                    Plugin.Log.Msg($"[Workshop] ✓ {_currentItemName}  {condBefore:P0}→{condAfter:P0}");
                }
                else
                {
                    AddXp(5);
                    Plugin.Log.Msg($"[Workshop] ✗ FAIL {_currentItemName}  → zniszczony");
                }
            }
            catch (Exception ex) { Plugin.Log.Warning($"[Workshop] Transfer ERR: {ex.Message}"); }
        }

        private void AddXp(int amount)
        {
            if (_npcLevel >= 40) return;

            _npcXp += amount;
            if (_npcXp >= 1000)
            {
                _npcXp -= 1000;
                _npcLevel = Math.Min(_npcLevel + 1, 40);
                Plugin.Log.Msg($"[Workshop] NPC Level UP → {_npcLevel}");
            }

            float progress = _npcXp / 1000f;
            _pbXp?.SetValue(progress);
            _lblNpcLevel?.SetText($"NPC  LVL {_npcLevel}");
            _lblXpValue?.SetText($"{_npcXp} / 1000 XP");
        }

        private bool TryRepairItem(Il2CppCMS.Player.Containers.IBaseItem baseItem,
            out float condBefore, out float condAfter)
        {
            condBefore = baseItem.GetConditionToShow();
            condAfter = condBefore;

            var item = baseItem.TryCast<Il2CppCMS.Player.Containers.Item>();
            if (item == null) return false;

            float maxAttemptThreshold = 0.70f + ((_npcLevel - 1) / 39f) * 0.30f;
            if (condBefore >= maxAttemptThreshold) { condAfter = condBefore; return true; }

            float difficulty = 1f - condBefore;
            float baseChance = 0.50f + ((_npcLevel - 1) / 39f) * 0.45f;
            float successChance = Mathf.Clamp(baseChance - difficulty * 0.30f, 0.05f, 0.95f);

            if ((float)_rng.NextDouble() < successChance)
            {
                item.Condition = 255; item.Dent = 0;
                condAfter = item.GetConditionToShow();
                return true;
            }

            item.Condition = 0; item.Dent = 255;
            condAfter = 0f;
            return false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private string BuildSetupStatus()
        {
            if (!StorageCache.HasRepairTable) return "❌ Brak RepairTable";
            if (!StorageCache.HasAnchor) return "❌ Brak UpgradeTable (anchor) — kliknij Scan";
            if (StorageCache.InputStorage == null) return "❌ Brak INPUT storage — kliknij Scan";
            if (StorageCache.OutputStorage == null) return "❌ Brak OUTPUT storage (potrzeba 2 w zasięgu)";
            if (_allocatedFunds < 1f) return "❌ Brak środków (dodaj fundusze)";
            return "✅ Gotowe do zatrudnienia";
        }

        private void RefreshStorageLabels()
        {
            if (StorageCache.InputStorage != null)
            {
                var p = StorageCache.InputStorage.transform.position;
                float d = Vector3.Distance(StorageCache.AnchorPos, p);
                _lblInput.SetText(
                    $"INPUT:  {StorageCache.InputStorage.StorageName} " +
                    $"[{StorageCache.InputStorage.ItemsCount}/{StorageCache.InputStorage.MaxCapacity}]" +
                    $" @ {d:F1}m");
            }
            if (StorageCache.OutputStorage != null)
            {
                var p = StorageCache.OutputStorage.transform.position;
                float d = Vector3.Distance(StorageCache.AnchorPos, p);
                _lblOutput.SetText(
                    $"OUTPUT: {StorageCache.OutputStorage.StorageName} " +
                    $"[{StorageCache.OutputStorage.ItemsCount}/{StorageCache.OutputStorage.MaxCapacity}]" +
                    $" @ {d:F1}m");
            }
        }

        private void SetStateLabel(string text, Color color)
        {
            _lblState.SetText(text);
            _lblState.SetColor(color);
        }

        private void SetNpcStatusLabel(string text, bool active)
        {
            _lblNpcStatus.SetText(text);
            _lblNpcStatus.SetColor(active
                ? new Color(0.2f, 1f, 0.4f, 1f)
                : new Color(0.5f, 0.5f, 0.6f, 1f));
        }

        private Color BadgeColor()
        {
            string s = BuildSetupStatus();
            if (s.StartsWith("✅")) return new Color(0.08f, 0.28f, 0.14f, 1f);
            if (s.StartsWith("⚠")) return new Color(0.28f, 0.20f, 0.05f, 1f);
            return new Color(0.28f, 0.06f, 0.06f, 1f);
        }

        private void RefreshBadge()
        {
            _btnSetupBadge?.SetBgColor(BadgeColor());
        }
    }
}