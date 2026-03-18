using System;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Numerics;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class ActionTweaks : IDisposable
    {
        private readonly Config _config;
        private ActionTweaksConfig Cfg => _config.ActionTweaksConfig;
        private ActionManager* _inst => ActionManager.Instance();

        // Animation lock tweak state
        private float _lastReqInitialAnimLock;
        private int _lastReqSequence = -1;
        private const float DelaySmoothing = 0.8f;
        public float DelayAverage { get; private set; } = 0.1f;
        private float DelayMax => Cfg.AnimationLockDelayMax * 0.001f;

        // Cooldown delay tweak state
        private float _cooldownAdjustment;

        // Hooks
        private Hook<ActionManager.Delegates.Update>? _updateHook;
        private Hook<ActionManager.Delegates.UseActionLocation>? _useActionLocationHook;
        private Hook<ActionEffectHandler.Delegates.Receive>? _actionEffectHook;

        public ActionTweaks(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (Cfg.RemoveAnimationLockDelay || Cfg.RemoveCooldownDelay)
                Enable();
            else
                Disable();
        }

        public void Enable()
        {
            if (_updateHook != null) return;

            _updateHook = Svc.Hook.HookFromAddress<ActionManager.Delegates.Update>(
                ActionManager.Addresses.Update.Value, UpdateDetour);
            _useActionLocationHook = Svc.Hook.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
                ActionManager.Addresses.UseActionLocation.Value, UseActionLocationDetour);
            _actionEffectHook = Svc.Hook.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                ActionEffectHandler.Addresses.Receive.Value, ActionEffectReceiveDetour);

            _updateHook.Enable();
            _useActionLocationHook.Enable();
            _actionEffectHook.Enable();
        }

        public void Disable()
        {
            _updateHook?.Disable();
            _updateHook?.Dispose();
            _updateHook = null;

            _useActionLocationHook?.Disable();
            _useActionLocationHook?.Dispose();
            _useActionLocationHook = null;

            _actionEffectHook?.Disable();
            _actionEffectHook?.Dispose();
            _actionEffectHook = null;

            _cooldownAdjustment = 0;
            _lastReqInitialAnimLock = 0;
            _lastReqSequence = -1;
        }

        public void Dispose() => Disable();

        private void UpdateDetour(ActionManager* self)
        {
            var fwk = Framework.Instance();
            var dt = fwk->GameSpeedMultiplier * fwk->FrameDeltaTime;
            _cooldownAdjustment = Cfg.RemoveCooldownDelay ? CalculateCooldownAdjustment(dt) : 0;

            _updateHook!.Original(self);

            _cooldownAdjustment = 0;
        }

        private float CalculateCooldownAdjustment(float dt)
        {
            var animLock = _inst->AnimationLock;
            float remainingCooldown = 0;

            if (_inst->ActionQueued)
            {
                var recastGroup = _inst->GetRecastGroup((int)_inst->QueuedActionType, _inst->QueuedActionId);
                if (recastGroup >= 0)
                {
                    var recast = _inst->GetRecastGroupDetail(recastGroup);
                    if (recast != null && recast->IsActive)
                        remainingCooldown = recast->Total - recast->Elapsed;
                }
            }

            var maxDelay = Math.Max(animLock, remainingCooldown);
            if (maxDelay <= 0)
                return 0;

            var overflow = dt - maxDelay;
            return Math.Clamp(overflow, 0, Cfg.CooldownDelayMax * 0.001f);
        }

        private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
        {
            var prevSeq = _inst->LastUsedActionSequence;
            var ret = _useActionLocationHook!.Original(self, actionType, actionId, targetId, location, extraParam, a7);
            var currSeq = _inst->LastUsedActionSequence;

            if (currSeq != prevSeq)
            {
                _lastReqInitialAnimLock = _inst->AnimationLock;
                _lastReqSequence = (int)currSeq;

                if (_cooldownAdjustment > 0)
                {
                    var castTimeRemaining = _inst->CastSpellId != 0 ? _inst->CastTimeTotal - _inst->CastTimeElapsed : 0f;
                    if (castTimeRemaining > 0)
                        _inst->CastTimeElapsed += _cooldownAdjustment;
                    else
                        _inst->AnimationLock = Math.Max(0, _inst->AnimationLock - _cooldownAdjustment);

                    var recastGroup = _inst->GetRecastGroup((int)actionType, actionId);
                    if (recastGroup >= 0)
                    {
                        var recast = _inst->GetRecastGroupDetail(recastGroup);
                        if (recast != null)
                            recast->Elapsed += _cooldownAdjustment;
                    }
                }
            }

            return ret;
        }

        private void ActionEffectReceiveDetour(uint casterID, Character* casterObj, Vector3* targetPos,
            ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targets)
        {
            var packetAnimLock = header->AnimationLock;
            var prevAnimLock = _inst->AnimationLock;

            _actionEffectHook!.Original(casterID, casterObj, targetPos, header, effects, targets);

            var localPlayer = Player.Object;
            if (localPlayer == null || casterID != localPlayer.EntityId || header->SourceSequence == 0)
                return;

            if (Cfg.RemoveAnimationLockDelay && _lastReqSequence == (int)header->SourceSequence && _lastReqInitialAnimLock > 0)
            {
                var delay = _lastReqInitialAnimLock - prevAnimLock;
                var alpha = delay > DelayAverage ? (1 - DelaySmoothing) * 2.5f : (1 - DelaySmoothing) * 0.5f;
                DelayAverage = delay * alpha + DelayAverage * (1 - alpha);

                SanityCheck(packetAnimLock, header->AnimationLock, _inst->AnimationLock);

                if (Cfg.RemoveAnimationLockDelay)
                {
                    var reduction = Math.Clamp(DelayAverage - DelayMax, 0, _inst->AnimationLock);
                    _inst->AnimationLock -= reduction;
                }
            }

            _lastReqInitialAnimLock = 0;
            _lastReqSequence = -1;
        }

        private void SanityCheck(float packetOriginalAnimLock, float packetModifiedAnimLock, float gameCurrAnimLock)
        {
            if (!Cfg.RemoveAnimationLockDelay)
                return;
            if (packetOriginalAnimLock == packetModifiedAnimLock && packetOriginalAnimLock == gameCurrAnimLock
                && packetOriginalAnimLock % 0.01 is <= 0.0005f or >= 0.0095f)
                return;

            Svc.Log.Warning($"[ActionTweaks] Unexpected animation lock {packetOriginalAnimLock:f6} -> {packetModifiedAnimLock:f6} -> {gameCurrAnimLock:f6}, disabling anim lock tweak");
            Helpers.PrintToshi("Unexpected animation lock detected! Disabling animation lock reduction. This may be caused by another plugin (XivAlexander, NoClippy).", 17);
            Cfg.RemoveAnimationLockDelay = false;
        }
    }
}