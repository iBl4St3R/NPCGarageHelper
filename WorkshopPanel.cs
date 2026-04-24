using CMS2026UITKFramework;
using System;
using UnityEngine;

namespace NPCGarageHelper
{
    internal class WorkshopPanel
    {
        // ── Auto-scan ──────────────────────────────────────────────────────────

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

        private float _outputFullTimer = 0f;
        private const float OUTPUT_FULL_CHECK_INTERVAL = 4f;

        private readonly System.Collections.Generic.HashSet<ulong> _repairedUIDs = new System.Collections.Generic.HashSet<ulong>();

        private readonly WorkerNPC _npc = new WorkerNPC();
        private static readonly System.Random _rng = new System.Random();


        private NpcSaveData.SavePayload _pendingSave = null;
        private float _pendingSaveDelay = 0f;
        private const float AUTO_HIRE_DELAY = 5f; // sekundy po załadowaniu sceny

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

        private float _transferOnlyTimer = 0f;
        private Il2CppCMS.Player.Containers.IBaseItem _transferOnlyItem = null;

        // ── Ticker ────────────────────────────────────────────────────────────
        private static readonly string[] TickFrames = { "●", "○" };
        private int _tickIdx;
        private float _tickTimer;

        private ulong _currentItemUID = 0;

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

        // ── Notyfikacje popup ─────────────────────────────────────────────────
        private bool _notifOutputFull = false;   // czy już pokazaliśmy "output pełny"
        private bool _notifInputEmpty = false;   // czy już pokazaliśmy "input pusty"


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

            _skillsPanel.OnSkillUpgraded = OnSkillUpgraded;

            // Wczytaj save ale nie aplikuj od razu — scena jeszcze nie gotowa
            _pendingSave = NpcSaveData.Load();
            _pendingSaveDelay = AUTO_HIRE_DELAY;

            _panel.SetVisible(false);
        }


        // ── Wczytywanie save ──────────────────────────────────────────────────
        private void ApplySave(NpcSaveData.SavePayload save)
        {
            if (save == null) return;

            _npcLevel = save.NpcLevel;
            _npcXp = save.NpcXp;
            _allocatedFunds = save.AllocatedFunds;

            NpcSkillData.LoadFromSave(
                save.SuccessLvl,
                save.MaxRepairLvl,
                save.MinRepairLvl,
                save.AvailablePoints);

            _pbXp?.SetValue(_npcXp / 1000f);
            _lblNpcLevel?.SetText($"NPC  LVL {_npcLevel}");
            _lblXpValue?.SetText($"{_npcXp} / 1000 XP");
            _lblFunds?.SetText($"Środki: {_allocatedFunds:F0} CR");
            RefreshBadge();

            // ── Auto-hire po załadowaniu ──────────────────────────────────────
            if (save.IsHired)
            {
                string status = BuildSetupStatus();
                if (status.StartsWith("✅"))
                {
                    _npcHired = true;
                    _lastWageDay = GameServices.GetGameDay();   // nie pobieramy wypłaty wstecz
                    SetNpcStatusLabel(" NPC: hired (waiting for shift)", true);
                    RefreshBadge();

                    float hour = GameServices.GetGameHour();
                    bool isWorkTime = hour >= WORK_START && hour < WORK_END;
                    if (isWorkTime)
                    {
                        var spawnPos = StorageCache.AnchorPos + new Vector3(1.5f, 0f, 0f);
                        _npc.TrySpawn(spawnPos);
                        _wasWorkTime = true;
                    }
                    else
                    {
                        _wasWorkTime = false;
                    }

                    Plugin.Log.Msg($"[WorkshopPanel] Auto-hired from save — workTime={isWorkTime}");
                }
                else
                {
                    // Setup się rozjechał (gracz przestawił skrzynie) — nie hirujemy, logujemy
                    Plugin.Log.Warning($"[WorkshopPanel] Auto-hire skipped — setup invalid: {status}");
                }
            }
        }

        // ── Wywołania Save — trzy miejsca ─────────────────────────────────────

        // 1. Po ukończeniu naprawy — na końcu TryTransferItem(), po bloku if(repairSuccess):
        //    NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds);
        //    (dodaj jako ostatnią linię przed zamknięciem catch)

        // 2. Po dodaniu funduszy — na końcu AllocateFunds():
        //    NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds);

        // 3. Po ulepszeniu pasywa — w SkillsPanel przyciski już wywołują NpcSkillData.Upgrade*()
        //    więc potrzebujemy callback. Dodaj do WorkshopPanel:
        public void OnSkillUpgraded() => NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, _npcHired);

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
                .AddLabel("Waiting for hire", 480f,
                    new Color(0.6f, 0.6f, 0.7f, 1f));
            _lblState.SetFontSize(14);

            _pbRepair = _panel.AddProgressBar("Repair progress:", 0f,
                fillColor: new Color(0.2f, 0.8f, 0.4f, 1f), height: 28f);

            _panel.AddSeparator();
        }

        private void AddStatusSection()
        {
            _lblNpcStatus = _panel.AddRow(24f, 3f)
                .AddLabel("● NPC: not hired", 480f, new Color(0.5f, 0.5f, 0.6f, 1f));
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
                .AddLabel("No work", 480f, new Color(0.5f, 0.5f, 0.6f, 1f));
            _lblWork.SetFontSize(13);
            _panel.AddSeparator();
        }

        private void AddFundsSection()
        {
            var row = _panel.AddRow(28f, 5f);
            _lblFunds = row.AddLabel("Funds: 0 CR", 230f, new Color(1f, 0.8f, 0.2f, 1f));
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
                .AddLabel("Fixed: 0  |  spent: 0 CR", 480f,
                    new Color(0.4f, 0.6f, 0.8f, 1f));
            _lblStats.SetFontSize(12);
            _panel.AddSeparator();
        }

        private void AddControlButtons()
        {
            var row = _panel.AddRow(34f, 5f);

            _btnSetupBadge = row.AddButton(
                " SETUP", 100f,
                () => { _setupPanel?.Refresh(_allocatedFunds); _setupPanel?.Toggle(); },
                BadgeColor());

            row.AddButton(" Skills", 95f, () => { _skillsPanel?.Refresh(); _skillsPanel?.Toggle(); }, new Color(0.15f, 0.15f, 0.35f, 1f));

            // NOWY przycisk:
            row.AddButton("Scan", 80f, OnScan, new Color(0.10f, 0.20f, 0.30f, 1f));

            row.AddButton("Hire", 100f, OnHire, new Color(0.08f, 0.26f, 0.12f, 1f));
            row.AddButton("Fire", 85f, OnFire, new Color(0.35f, 0.08f, 0.08f, 1f));
        }

        // ── Przyciski ─────────────────────────────────────────────────────────
        private void AllocateFunds(float amount)
        {
            if (!GameServices.SpendMoney(amount))
            {
                _lblWork.SetText($" Not enough CR ({GameServices.GetMoney():F0} / {amount:F0})");
                return;
            }
            _allocatedFunds += amount;
            _lblFunds.SetText($"Środki: {_allocatedFunds:F0} CR");
            RefreshBadge();

            NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, _npcHired);
        }

        private static void ShowPopup(string msg)
        {
            try
            {
                Il2CppCMS.UI.UIManager.Get().ShowPopup(msg, Il2Cpp.PopupType.Normal);
            }
            catch (Exception ex) { Plugin.Log.Warning($"[NGH] ShowPopup: {ex.Message}"); }
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
                _lblWork.SetText($" Need {DAILY_WAGE:F0} CR for first wage");
                return;
            }

            GameServices.SpendMoney(DAILY_WAGE);
            _lastWageDay = GameServices.GetGameDay();

            _npcHired = true;
            SetNpcStatusLabel(" NPC: hired (waiting for 8:00)", true);
            RefreshBadge();
            Plugin.Log.Msg("[WorkshopPanel] NPC hired!");

            float hour = GameServices.GetGameHour();
            bool isWorkTime = hour >= WORK_START && hour < WORK_END;
            if (isWorkTime)
            {
                var spawnPos = StorageCache.AnchorPos + new Vector3(1.5f, 0f, 0f);
                if (!_npc.TrySpawn(spawnPos))
                {
                    _lblWork.SetText(" Spawn NPC failed — check logs");
                    _npcHired = false;
                    return;
                }
                _wasWorkTime = true;
            }
            else
            {
                _lblWork.SetText(" NPC waiting for shift start (8:00)");
                SetStateLabel(" Waiting for 8:00", new Color(0.7f, 0.5f, 0.2f, 1f));
                _wasWorkTime = false;
            }

            // ← BRAKUJĄCY ZAPIS
            NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, _npcHired);
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
            SetNpcStatusLabel(" Not hired", false);
            _lblWork.SetText("No job");
            _lblState.SetText(" Not hired");
            _lblState.SetColor(new Color(0.5f, 0.5f, 0.6f, 1f));
            _pbRepair.SetValue(0f);
            _transferOnlyTimer = 0f;
            _transferOnlyItem = null;

            _currentItemUID = 0;

            NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, false);

            NpcSkillData.Reset();
            if (_skillsPanel?.IsVisible == true)
                _skillsPanel.Refresh();

            Plugin.Log.Msg("[WorkshopPanel] NPC fired.");


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

            // Opóźniony auto-hire po załadowaniu sceny
            if (_pendingSave != null)
            {
                _pendingSaveDelay -= dt;
                if (_pendingSaveDelay <= 0f)
                {
                    StorageCache.Scan();
                    RefreshBadge();
                    RefreshStorageLabels();
                    ApplySave(_pendingSave);
                    _pendingSave = null;
                }
                return; // czekaj — nie rób nic więcej
            }

            // Ticker
            _tickTimer += dt;
            if (_tickTimer >= 0.5f)
            {
                _tickTimer = 0f;
                _tickIdx = 1 - _tickIdx;
                _lblTick.SetText(TickFrames[_tickIdx]);
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
                    _lblWork.SetText(" Not enough CR for wage — NPC fired");
                    OnFire();
                    return;
                }
                _lastWageDay = today;
                Plugin.Log.Msg($"[WorkshopPanel] Wage paid: {DAILY_WAGE} CR (day {today})");
            }

            bool isWorkTime = hour >= WORK_START && hour < WORK_END;
            if (isWorkTime != _wasWorkTime)
            {
                _wasWorkTime = isWorkTime;
                if (isWorkTime)
                {
                    if (!_npc.IsAlive)
                    {
                        var spawnPos = StorageCache.AnchorPos + new Vector3(1.5f, 0f, 0f);
                        _npc.TrySpawn(spawnPos);
                    }
                    _npc.SetVisible(true);

                    ShowPopup("<color=#60ff90> NPC arrived — starting shift (8:00)</color>");

                    _notifOutputFull = false;
                    _notifInputEmpty = false;
                }
                else
                {
                    _npc.SetVisible(false);
                    _npc.SetIdle();
                    _currentItemId = "";
                    _currentItemName = "";
                    _currentItemUID = 0;
                    _repairTimer = 0f;
                    _pbRepair.SetValue(0f);

                    ShowPopup("<color=#ffaa30> NPC finished shift — going home (16:00)</color>");
                }
            }

            if (!isWorkTime)
            {
                SetNpcStatusLabel("NPC: off hours (8:00-16:00)", false);
                _lblWork.SetText("NPC resting — model hidden");
                SetStateLabel(" Outside working hours", new Color(0.7f, 0.5f, 0.2f, 1f));
                return;
            }

            SetNpcStatusLabel("NPC: working", true);
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

        // ── Logika naprawy (Zaktualizowana wg sugestii Claude) ────────────────────────
        private void TickRepairLogic(float dt)
        {
            var repairPos = StorageCache.RepairTablePos ?? StorageCache.AnchorPos;

            // ── Guard: OUTPUT pełny — czekaj i sprawdzaj co 4s ────────────────
            if (!StorageCache.OutputStorage.ItemsManager.CanAddItems())
            {
                _outputFullTimer -= dt;
                if (_outputFullTimer > 0f)
                {
                    SetStateLabel(" OUTPUT full — waiting…", new Color(0.9f, 0.5f, 0.1f, 1f));
                    _lblWork.SetText($"OUTPUT full — checking in {_outputFullTimer:F0}s");
                    _npc.SetIdle();
                    return;
                }
                _outputFullTimer = OUTPUT_FULL_CHECK_INTERVAL;

                // ── Notyfikacja: output pełny — tylko raz per zmiana ─────────
                if (!_notifOutputFull)
                {
                    _notifOutputFull = true;
                    ShowPopup("<color=#ff9030> NPC: OUTPUT storage is full — work paused</color>");
                }
                return;
            }
            // OUTPUT ma miejsce — resetuj flagę i timer
            if (_notifOutputFull)
            {
                _notifOutputFull = false;
                ShowPopup("<color=#60ff90>📦 NPC: space in OUTPUT — resuming work</color>");
            }
            _outputFullTimer = 0f;

            // ── Transfer-only ──────────────────────────────────────────────────
            if (_transferOnlyItem != null)
            {
                _transferOnlyTimer -= dt;
                SetStateLabel(" Transferring item…", new Color(0.6f, 0.6f, 0.3f, 1f));
                _lblWork.SetText($"Transferring: {_transferOnlyItem.GetLocalizedName()}  ({_transferOnlyTimer:F0}s)");

                if (_transferOnlyTimer <= 0f)
                {
                    try
                    {
                        if (StorageCache.OutputStorage.ItemsManager.CanAddItems())
                        {
                            StorageCache.InputStorage.ItemsManager.Delete(_transferOnlyItem);
                            StorageCache.OutputStorage.ItemsManager.Add(_transferOnlyItem, false);
                            Plugin.Log.Msg($"[Workshop] Moved (unrepairable): {_transferOnlyItem.GetLocalizedName()}");
                        }
                    }
                    catch (Exception ex) { Plugin.Log.Warning($"[Workshop] Transfer-only ERR: {ex.Message}"); }
                    _transferOnlyItem = null;
                    _transferOnlyTimer = 0f;
                }
                return;
            }

            if (!string.IsNullOrEmpty(_currentItemId))
            {
                _repairTimer -= dt;
                float repTime = RepairTime();
                float progress = 1f - Mathf.Clamp01(_repairTimer / repTime);

                _pbRepair.SetValue(progress);
                SetStateLabel($" Repairing: {_currentItemName}", new Color(0.3f, 1f, 0.5f, 1f));
                _lblWork.SetText($"Repairing: {_currentItemName}  ({_repairTimer:F0}s)");

                if (StorageCache.IsValid() && StorageCache.UpgradeTable != null)
                    // Używamy repairPos zamiast StorageCache.AnchorPos
                    _npc.PatrolTick(dt,
                        repairPos,
                        StorageCache.UpgradeTable.transform.position,
                        StorageCache.InputStorage.transform.position,
                        StorageCache.OutputStorage.transform.position);

                if (_repairTimer > 0f) return;

                TryTransferItem();
                _currentItemId = "";
                _currentItemName = "";
                _pbRepair.SetValue(0f);
                _lblWork.SetText("Looking for next part…");
                return;
            }

            if (StorageCache.IsValid())
                // Używamy repairPos zamiast StorageCache.AnchorPos
                _npc.PatrolTick(dt,
                    repairPos,
                    StorageCache.UpgradeTable.transform.position,
                    StorageCache.InputStorage.transform.position,
                    StorageCache.OutputStorage.transform.position);


            // Sprawdź czy jest item który trzeba tylko przenieść
            var transferCandidate = FindTransferOnlyItem();
            if (transferCandidate != null)
            {
                _transferOnlyItem = transferCandidate;
                _transferOnlyTimer = 1f;
                return;
            }

            var nextItem = FindNextItem();
            if (nextItem == null)
            {
                _npc.SetIdle();
                _lblWork.SetText(" No parts in INPUT — waiting");
                SetStateLabel(" No parts — idle", new Color(0.5f, 0.5f, 0.6f, 1f));

                // ── Notyfikacja: input pusty — tylko raz per zmiana ──────────
                if (!_notifInputEmpty)
                {
                    _notifInputEmpty = true;
                    ShowPopup("<color=#a0a0ff>📭 NPC: INPUT storage is empty — waiting for parts</color>");
                }
                return;
            }
            // Jest praca — resetuj flagę input empty
            _notifInputEmpty = false;

            // w TickRepairLogic przy starcie naprawy:
            _currentItemId = nextItem.ID;
            _currentItemUID = nextItem.UID;      // ← dodaj tę linię
            _currentItemName = nextItem.GetLocalizedName();
            _repairTimer = RepairTime();

            _lblWork.SetText($" Fixes: {_currentItemName}");
            SetStateLabel($" Fixes:: {_currentItemName}", new Color(0.3f, 1f, 0.5f, 1f));
            Plugin.Log.Msg($"[Workshop] Start: {_currentItemName}  t={_repairTimer:F0}s");
        }

        private Il2CppCMS.Player.Containers.IBaseItem FindTransferOnlyItem()
        {
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

                    var item = candidate.TryCast<Il2CppCMS.Player.Containers.Item>();
                    if (item == null) return candidate;
                    if (item.IsJunk) return candidate;

                    var cat = NpcSkillData.GetItemCategory(candidate);
                    if (cat == null) return candidate;
                    if (!NpcSkillData.IsUnlocked(cat.Value)) return candidate;

                    // Kondycja >= maxRepair — NPC nie może poprawić, przenosi do OUTPUT
                    float condVal = candidate.GetConditionToShow();
                    float maxRepair = NpcSkillData.GetMaxRepair(cat.Value);
                    if (condVal >= maxRepair) return candidate;
                }
            }
            catch { }
            return null;
        }

        private float RepairTime() => REPAIR_TIME_BASE * (1f - (_npcLevel - 1) / 60f);

        private Il2CppCMS.Player.Containers.IBaseItem FindNextItem()
        {
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

                    var ci = candidate.TryCast<Il2CppCMS.Player.Containers.Item>();
                    if (ci == null) continue;
                    if (ci.IsJunk) continue;

                    var cat = NpcSkillData.GetItemCategory(candidate);
                    if (cat == null || !NpcSkillData.IsUnlocked(cat.Value)) continue;

                    float cond = candidate.GetConditionToShow();
                    float maxRepair = NpcSkillData.GetMaxRepair(cat.Value);

                    if (cond >= maxRepair) continue;  // NPC nie poprawi — pomijamy
                    // już obsłużone przez maxRepair powyżej, ten check jest redundantny — można zostawić jako safety net
                    //if (cond >= 0.99f) continue; // już idealna

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

                // NOWY GUARD: item zniknął ze stacka (gracz zabrał) — abort
                if (baseItem == null)
                {
                    Plugin.Log.Msg($"[Workshop] Item {_currentItemName} gone from INPUT — player took it, aborting transfer");
                    _currentItemId = "";
                    _currentItemName = "";
                    return;
                }

                // NOWY GUARD: sprawdź czy to ten sam konkretny obiekt po UID
                // (gracz mógł zabrać i włożyć inny item tego samego typu)
                // _currentItemUID musisz zapamiętać przy starcie naprawy
                if (baseItem.UID != _currentItemUID)
                {
                    Plugin.Log.Msg($"[Workshop] UID mismatch — different item in slot, aborting");
                    _currentItemId = "";
                    _currentItemName = "";
                    return;
                }


                bool repairSuccess = TryRepairItem(baseItem, out float condBefore, out float condAfter);

                StorageCache.InputStorage.ItemsManager.Delete(baseItem);
                StorageCache.OutputStorage.ItemsManager.Add(baseItem, false);

                _totalRepaired++;
                _allocatedFunds -= REPAIR_COST;
                _lblFunds.SetText($"Funds: {_allocatedFunds:F0} CR");

                if (repairSuccess)
                {
                    _repairedUIDs.Add(baseItem.UID);
                    AddXp(120);
                    _lblStats.SetText(
                        $"Repaired: {_totalRepaired}  |  spent: {_totalRepaired * REPAIR_COST:F0} CR");
                    Plugin.Log.Msg($"[Workshop] ✓ {_currentItemName}  {condBefore:P0}→{condAfter:P0}");

                    NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, _npcHired);
                }
                else
                {
                    AddXp(40);
                    Plugin.Log.Msg($"[Workshop] ✗ FAIL {_currentItemName}  → destroyed");

                    NpcSaveData.Save(_npcLevel, _npcXp, _allocatedFunds, _npcHired);
                }
            }
            catch (Exception ex) { Plugin.Log.Warning($"[Workshop] Transfer ERR: {ex.Message}"); }
        }

        private void AddXp(int amount)
        {
            if (_npcLevel >= 199) return;

            _npcXp += amount;
            if (_npcXp >= 1000)
            {
                _npcXp -= 1000;
                _npcLevel = Math.Min(_npcLevel + 1, 199);
                NpcSkillData.AddSkillPoint();              
                if (_skillsPanel?.IsVisible == true)
                    _skillsPanel.Refresh();                
                Plugin.Log.Msg($"[Workshop] NPC Level UP → {_npcLevel}  pts={NpcSkillData.AvailablePoints}");
            }

            _pbXp?.SetValue(_npcXp / 1000f);
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

            var cat = NpcSkillData.GetItemCategory(baseItem);
            if (cat == null || !NpcSkillData.IsUnlocked(cat.Value)) return false;

            float successChance = NpcSkillData.GetSuccessChance(cat.Value);
            float maxRepair = NpcSkillData.GetMaxRepair(cat.Value);
            float minRepair = NpcSkillData.GetMinRepair(cat.Value);

            // Rzut na sukces
            if ((float)_rng.NextDouble() >= successChance)
            {
                item.Condition = 3;
                item.Dent = 255;
                condAfter = item.GetConditionToShow();
                return false;
            }

            // Naprawa do losowego % między min a max
            float repairTarget = minRepair + (float)_rng.NextDouble() * (maxRepair - minRepair);
            item.Condition = (byte)Mathf.Clamp(repairTarget * 255f, 1f, 255f);
            item.Dent = 0;
            condAfter = item.GetConditionToShow();
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private string BuildSetupStatus()
        {
            if (!StorageCache.HasRepairTable) return "❌ No RepairTable detected";
            if (!StorageCache.HasAnchor) return "❌ No UpgradeTable (anchor) — click Scan";
            if (StorageCache.InputStorage == null) return "❌ No INPUT storage — click Scan";
            if (StorageCache.OutputStorage == null) return "❌ No OUTPUT storage (need 2 in range)";
            if (_allocatedFunds < 1f) return "❌ No funds (add funds)";
            return "✅ Ready to hire";
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


        public void AddXpPublic(int amount) => AddXp(amount);
        public void RefreshSkillsIfOpen()
        {
            if (_skillsPanel?.IsVisible == true)
                _skillsPanel.Refresh();
        }




    }
}