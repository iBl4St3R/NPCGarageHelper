using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NPCGarageHelper
{
    internal class WorkerNPC
    {
        // ── Stałe ─────────────────────────────────────────────────────────────
        private const float WALK_SPEED = 1.8f;
        private const float ANIM_SPEED = 2.4f;
        private const float ROT_SPEED = 8f;
        private const float ARRIVE_DIST = 0.85f;

        // ── Unity refs ────────────────────────────────────────────────────────
        private GameObject _npcGO;

        // ── Animator ──────────────────────────────────────────────────────────
        private object _animInst;
        private MethodInfo _setFloat;
        private MethodInfo _setBool;

        // ── Public ────────────────────────────────────────────────────────────
        public bool IsAlive => _npcGO != null;

        // ── Spawn ─────────────────────────────────────────────────────────────
        public bool TrySpawn(Vector3 spawnPos)
        {
            try
            {
                var pcs = UnityEngine.Object.FindObjectsOfType<Il2CppCMS.Player.Controller.PlayerController > (true);
                if (pcs.Length == 0) { Plugin.Log.Warning("[WorkerNPC] No PlayerController."); return false; }

                var playerGO = pcs[0].gameObject;
                Transform modelChild = null;
                for (int i = 0; i < playerGO.transform.childCount; i++)
                {
                    var ch = playerGO.transform.GetChild(i);
                    if (ch.name.StartsWith("casual-m-")) { modelChild = ch; break; }
                }
                if (modelChild == null) { Plugin.Log.Warning("[WorkerNPC] Model child not found."); return false; }

                _npcGO = UnityEngine.Object.Instantiate(modelChild.gameObject);
                _npcGO.name = "NGH_WorkerNPC";
                SceneManager.MoveGameObjectToScene(
                    _npcGO, SceneManager.GetSceneByName("garage"));

                _npcGO.transform.position = spawnPos;
                _npcGO.transform.rotation = Quaternion.identity;
                _npcGO.transform.localScale = Vector3.one;
                _npcGO.SetActive(true);

                foreach (var t in _npcGO.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 0;
                foreach (var smr in _npcGO.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.enabled = true;
                }

                SetupAnimator();
                SetIdle();

                Plugin.Log.Msg($"[WorkerNPC] Spawned @ {spawnPos}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[WorkerNPC] TrySpawn: {ex.Message}");
                return false;
            }
        }

        public void Despawn()
        {
            if (_npcGO != null) UnityEngine.Object.Destroy(_npcGO);
            _npcGO = null;
            _animInst = null;
            ResetPatrol();
        }

        /// <summary>Pokazuje / chowa model NPC (SetActive). Wywoływane przy zmianie godziny pracy.</summary>
        public void SetVisible(bool visible)
        {
            if (_npcGO == null) return;
            _npcGO.SetActive(visible);
            if (!visible) ResetPatrol();
        }

        // ── Animator ──────────────────────────────────────────────────────────
        private void SetupAnimator()
        {
            try
            {
                Type animType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                            if (t.Name == "Animator" && t.Namespace == "UnityEngine")
                            { animType = t; break; }
                    }
                    catch { }
                    if (animType != null) break;
                }
                if (animType == null) return;

                UnityEngine.Component animRaw = null;
                foreach (var c in _npcGO.GetComponents<UnityEngine.Component>())
                {
                    if (c?.GetIl2CppType().FullName == "UnityEngine.Animator") { animRaw = c; break; }
                }
                if (animRaw == null) return;

                var ctor = animType.GetConstructor(new[] { typeof(IntPtr) });
                _animInst = ctor.Invoke(new object[] { animRaw.Pointer });
                _setFloat = animType.GetMethod("SetFloat", new[] { typeof(string), typeof(float) });
                _setBool = animType.GetMethod("SetBool", new[] { typeof(string), typeof(bool) });

                Plugin.Log.Msg("[WorkerNPC] Animator OK");
            }
            catch (Exception ex) { Plugin.Log.Warning($"[WorkerNPC] Animator setup: {ex.Message}"); }
        }

        private void AnimSet(float motionZ)
        {
            try
            {
                _setFloat?.Invoke(_animInst, new object[] { "MotionZ", motionZ });
                _setFloat?.Invoke(_animInst, new object[] { "MotionX", 0f });
                _setBool?.Invoke(_animInst, new object[] { "Crouch", false });
            }
            catch { }
        }

        public void SetIdle()
        {
            AnimSet(0f);
            _patrolWalking = false;
        }

        // ── Patrol state machine ──────────────────────────────────────────────
        // Wywoływane TYLKO gdy NPC ma pracę (są itemy lub trwa naprawa).
        // Gdy brak pracy — WorkshopPanel wywołuje SetIdle().

        private enum PatrolState
        {
            WaitAtRepair, WalkNext,
            WaitAtUpgrade, WaitAtInput, WaitAtOutput
        }

        private PatrolState _patrolState = PatrolState.WaitAtRepair;
        private float _patrolTimer;
        private bool _patrolInited;
        private bool _patrolWalking;
        private Vector3 _patrolTarget;
        private int _patrolWaypointIdx; // 0=Repair 1=Upgrade 2=Input 3=Output

        private static readonly System.Random _rng = new System.Random();

        private static float RndWait(float min, float max) =>
            min + (float)_rng.NextDouble() * (max - min);

        public void ResetPatrol()
        {
            _patrolInited = false;
            _patrolState = PatrolState.WaitAtRepair;
            _patrolWalking = false;
        }

        /// <summary>
        /// 4-punktowy patrol: RepairTable → UpgradeTable → InputStorage → OutputStorage → losowo.
        /// tablePos     = AnchorPos (RepairTable)
        /// upgradePos   = UpgradeTable.transform.position
        /// inputPos     = InputStorage.transform.position
        /// outputPos    = OutputStorage.transform.position
        /// </summary>
        public void PatrolTick(float dt,
            Vector3 tablePos, Vector3 upgradePos,
            Vector3 inputPos, Vector3 outputPos)
        {
            if (_npcGO == null || !_npcGO.activeSelf) return;

            if (!_patrolInited)
            {
                _patrolInited = true;
                _patrolWaypointIdx = 0;
                _patrolState = PatrolState.WaitAtRepair;
                _patrolTimer = RndWait(5f, 20f);
            }

            switch (_patrolState)
            {
                // ── Stoi przy stole napraw ────────────────────────────────────
                case PatrolState.WaitAtRepair:
                    AnimSet(0f);
                    _patrolTimer -= dt;
                    if (_patrolTimer <= 0f)
                    {
                        _patrolState = PatrolState.WalkNext;
                        _patrolWaypointIdx = NextWaypoint();
                        StartMove(GetWaypointPos(_patrolWaypointIdx,
                            tablePos, upgradePos, inputPos, outputPos));
                    }
                    break;

                // ── Idzie do następnego punktu ────────────────────────────────
                case PatrolState.WalkNext:
                    if (MoveStep(dt))
                    {
                        // Dotarł — ustal czas czekania wg typu punktu
                        bool isTable = _patrolWaypointIdx <= 1; // 0=Repair 1=Upgrade
                        _patrolTimer = isTable ? RndWait(5f, 20f) : RndWait(2f, 5f);
                        _patrolState = _patrolWaypointIdx switch
                        {
                            0 => PatrolState.WaitAtRepair,
                            1 => PatrolState.WaitAtUpgrade,
                            2 => PatrolState.WaitAtInput,
                            _ => PatrolState.WaitAtOutput
                        };
                        AnimSet(0f);
                    }
                    break;

                // ── Stoi przy stole upgradów ──────────────────────────────────
                case PatrolState.WaitAtUpgrade:
                    AnimSet(0f);
                    _patrolTimer -= dt;
                    if (_patrolTimer <= 0f)
                    {
                        _patrolWaypointIdx = NextWaypoint();
                        StartMove(GetWaypointPos(_patrolWaypointIdx,
                            tablePos, upgradePos, inputPos, outputPos));
                        _patrolState = PatrolState.WalkNext;
                    }
                    break;

                // ── Stoi przy INPUT ───────────────────────────────────────────
                case PatrolState.WaitAtInput:
                    AnimSet(0f);
                    _patrolTimer -= dt;
                    if (_patrolTimer <= 0f)
                    {
                        _patrolWaypointIdx = NextWaypoint();
                        StartMove(GetWaypointPos(_patrolWaypointIdx,
                            tablePos, upgradePos, inputPos, outputPos));
                        _patrolState = PatrolState.WalkNext;
                    }
                    break;

                // ── Stoi przy OUTPUT ──────────────────────────────────────────
                case PatrolState.WaitAtOutput:
                    AnimSet(0f);
                    _patrolTimer -= dt;
                    if (_patrolTimer <= 0f)
                    {
                        _patrolWaypointIdx = NextWaypoint();
                        StartMove(GetWaypointPos(_patrolWaypointIdx,
                            tablePos, upgradePos, inputPos, outputPos));
                        _patrolState = PatrolState.WalkNext;
                    }
                    break;
            }
        }

        private int NextWaypoint() => _rng.Next(0, 4);


        private static Vector3 GetWaypointPos(int idx,
            Vector3 table, Vector3 upgrade, Vector3 input, Vector3 output)
        {
            return idx switch
            {
                0 => table,
                1 => upgrade,
                2 => input,
                _ => output
            };
        }


        private void StartMove(Vector3 target)
        {
            _patrolTarget = target;
            _patrolWalking = true;
            AnimSet(ANIM_SPEED);   
        }

        private bool MoveStep(float dt)
        {
            if (!_patrolWalking || _npcGO == null) return true;

            var pos = _npcGO.transform.position;
            var toTarget = new Vector3(_patrolTarget.x - pos.x, 0f, _patrolTarget.z - pos.z);
            float dist = toTarget.magnitude;

            if (dist < ARRIVE_DIST)
            {
                _patrolWalking = false;
                AnimSet(0f);
                return true;
            }

            var dir = toTarget.normalized;
            _npcGO.transform.rotation = Quaternion.Slerp(
                _npcGO.transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                dt * ROT_SPEED);
            _npcGO.transform.position = new Vector3(
                pos.x + dir.x * WALK_SPEED * dt,
                pos.y,
                pos.z + dir.z * WALK_SPEED * dt);

            AnimSet(ANIM_SPEED);
            return false;
        }
    }
}