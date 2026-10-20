using System;
using UnityEngine;

namespace Combat.Parry
{
    public class ParryController : MonoBehaviour
    {
        [Header("=== CONFIG ===")]
        [SerializeField] private ParryConfig config;

        [Header("=== REFERENCES ===")]
        [SerializeField] private LayerMask enemyLayer;

        public ParryState CurrentState { get; private set; } = ParryState.Idle;
        public int CurrentWindowFrames { get; private set; }
        public int CurrentChainCount { get; private set; }
        public bool IsTelegraphing { get; private set; }
        public ParryConfig Config => config;

        public event Action<GameObject, Vector3> OnParrySuccessWithPoint;
        public event Action<GameObject> OnParrySuccess;   // giữ cái cũ
        public event Action OnParryFail;
        public event Action<int> OnParryWindowOpened;
        public event Action OnParryWindowClosed;
        public event Action<int> OnSpamPenaltyApplied;
        public event Action<int> OnChainParrySuccess;
        public event Action<ParryMovementType> OnParryMovementRequested;

        private float _windowTimer;
        private float _frameAccumulator;
        private float _inputCooldownTimer;
        private float _chainGraceTimer;
        private float _recoveryAccumulator;

        private PlayerController _playerCtx;

        private void Awake()
        {
            _playerCtx = GetComponent<PlayerController>();
            CurrentWindowFrames = config.defaultWindowFrames;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateTimers(dt);

            if (CurrentState == ParryState.WindowOpen)
                UpdateWindowOpen(dt);
        }

        private void UpdateTimers(float dt)
        {
            if (_inputCooldownTimer > 0f)
                _inputCooldownTimer -= dt;

            if (_chainGraceTimer > 0f)
            {
                _chainGraceTimer -= dt;
                if (_chainGraceTimer <= 0f)
                {
                    CurrentChainCount = 0;
                    CurrentState = ParryState.Idle;
                }
            }

            if (CurrentState != ParryState.WindowOpen && CurrentWindowFrames < config.defaultWindowFrames)
            {
                _recoveryAccumulator += dt;
                if (_recoveryAccumulator >= 1f)
                {
                    _recoveryAccumulator = 0f;
                    CurrentWindowFrames = Mathf.Min(
                        CurrentWindowFrames + Mathf.RoundToInt(config.windowRecoveryRate),
                        config.defaultWindowFrames);
                }
            }
        }

        private void UpdateWindowOpen(float dt)
        {
            _windowTimer -= dt;

            _frameAccumulator += dt;
            if (_frameAccumulator >= (1f / 60f))
            {
                _frameAccumulator -= (1f / 60f);
                CurrentWindowFrames = Mathf.Max(0, CurrentWindowFrames - 1);
            }

            if (_windowTimer <= 0f || CurrentWindowFrames <= 0)
                OnWindowExpired();
        }

        private void OnWindowExpired()
        {
            if (_chainGraceTimer > 0f && CurrentChainCount < config.maxChainParryCount)
            {
                OpenParryWindow(isChain: true);
                return;
            }

            CurrentState = ParryState.Fail;
            OnParryWindowClosed?.Invoke();
            OnParryFail?.Invoke();
            CurrentState = ParryState.Idle;
        }

        public void OnParryInput()
        {
            if (_inputCooldownTimer > 0f) return;

            if (_chainGraceTimer > 0f)
            {
                OpenParryWindow(isChain: false);
                _inputCooldownTimer = config.parryInputCooldown;
                return;
            }

            if (config.requireTelegraph && !IsTelegraphing)
            {
                CurrentState = ParryState.Blocking;
                OnParryFail?.Invoke();
                return;
            }

            if (CurrentState == ParryState.WindowOpen)
            {
                CurrentWindowFrames = Mathf.Max(
                    config.minWindowFrames,
                    CurrentWindowFrames - config.spamPenaltyPerPress);

                OnSpamPenaltyApplied?.Invoke(CurrentWindowFrames);

                if (CurrentWindowFrames <= config.minWindowFrames)
                {
                    CurrentState = ParryState.Idle;
                    _windowTimer = 0f;
                }
                else
                {
                    _windowTimer = CurrentWindowFrames * (1f / 60f);
                }

                _inputCooldownTimer = config.parryInputCooldown;
                return;
            }

            OpenParryWindow(isChain: false);
            _inputCooldownTimer = config.parryInputCooldown;
        }

        public bool TryParry(GameObject attacker, Vector3 hitPoint = default)
        {
            bool inGrace = _chainGraceTimer > 0f && CurrentChainCount < config.maxChainParryCount;
            bool inWindow = CurrentState == ParryState.WindowOpen && CurrentWindowFrames > 0;

            if (!inGrace && !inWindow) return false;

            ExecuteParrySuccess(attacker, hitPoint, isChain: inGrace);
            return true;
        }


        private void OpenParryWindow(bool isChain)
        {
            CurrentState = ParryState.WindowOpen;

            if (isChain)
                CurrentWindowFrames = config.defaultWindowFrames;

            _windowTimer = CurrentWindowFrames * (1f / 60f);
            _frameAccumulator = 0f;

            OnParryWindowOpened?.Invoke(CurrentWindowFrames);
        }

        private void ExecuteParrySuccess(GameObject attacker, Vector3 hitPoint, bool isChain)
        {
            CurrentWindowFrames = config.defaultWindowFrames;
            _chainGraceTimer = config.chainParryGraceTime;
            CurrentChainCount = isChain ? CurrentChainCount + 1 : 1;

            OnParrySuccess?.Invoke(attacker);
            OnParrySuccessWithPoint?.Invoke(attacker, hitPoint);
            OnParryWindowClosed?.Invoke();

            if (isChain)
                OnChainParrySuccess?.Invoke(CurrentChainCount);

            // 🔥 Đọc input NGAY và fire movement — không có window
            if (config.enableDirectionalJump)
            {
                var movement = ReadCurrentInput();
                OnParryMovementRequested?.Invoke(movement);
            }

            CurrentState = ParryState.Idle;
        }

        private ParryMovementType ReadCurrentInput()
        {
            Vector2 input = _playerCtx.InputVector;
            const float DZ = 0.3f;

            bool f = input.y > DZ;
            bool b = input.y < -DZ;
            bool l = input.x < -DZ;
            bool r = input.x > DZ;

            if (f && l) return ParryMovementType.ForwardLeft;
            if (f && r) return ParryMovementType.ForwardRight;
            if (b && l) return ParryMovementType.BackwardLeft;
            if (b && r) return ParryMovementType.BackwardRight;
            if (f) return ParryMovementType.Forward;
            if (b) return ParryMovementType.Backward;
            if (l) return ParryMovementType.Left;
            if (r) return ParryMovementType.Right;

            return ParryMovementType.Backward;
        }

        public void SetTelegraphing(bool value) => IsTelegraphing = value;
    }
}