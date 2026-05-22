using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class NewActionTimings : IDisposable
    {
        private readonly Config _config;
        private NewActionTimingsConfig Cfg => _config.NewActionTimingsConfig;
        private ActionManager* _inst => ActionManager.Instance();

        private float SimulatedRTT => Cfg.SimulatedRttMs * 0.001f;
        private const float DefaultClientAnimLock = 0.5f;
        private const float CooldownDelayMax = 0.1f;

        private float _cooldownAdjustment;

        private float _delayAverage = -1;
        private int _packetsSent = 0;
        private float _intervalPacketsTimer = 0;
        private int _intervalPacketsIndex = 0;
        private readonly int[] _intervalPackets = new int[5];
        private readonly Dictionary<ushort, float> _appliedLocks = new();
        private bool _saveConfig = false;

        private Hook<ActionManager.Delegates.Update>? _updateHook;
        private Hook<ActionManager.Delegates.UseActionLocation>? _useActionLocationHook;
        private Hook<ActionEffectHandler.Delegates.Receive>? _actionEffectHook;
        private Hook<ZoneClient.Delegates.SendPacket>? _sendPacketHook;

        public NewActionTimings(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (Cfg.Enabled)
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
            _sendPacketHook = Svc.Hook.HookFromAddress<ZoneClient.Delegates.SendPacket>(
                (nint)ZoneClient.MemberFunctionPointers.SendPacket, SendPacketDetour);

            _updateHook.Enable();
            _useActionLocationHook.Enable();
            _actionEffectHook.Enable();
            _sendPacketHook.Enable();
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

            _sendPacketHook?.Disable();
            _sendPacketHook?.Dispose();
            _sendPacketHook = null;

            _cooldownAdjustment = 0;
            _delayAverage = -1;
            _appliedLocks.Clear();
        }

        public void Dispose() => Disable();

        private void UpdateDetour(ActionManager* self)
        {
            var fwk = Framework.Instance();
            var dt = fwk->GameSpeedMultiplier * fwk->FrameDeltaTime;
            _cooldownAdjustment = CalculateCooldownAdjustment(dt);

            _updateHook!.Original(self);

            _cooldownAdjustment = 0;

            _intervalPacketsTimer += dt;
            while (_intervalPacketsTimer >= 0.01f)
            {
                _intervalPacketsTimer -= 0.01f;
                _intervalPacketsIndex = (_intervalPacketsIndex + 1) % _intervalPackets.Length;
                _intervalPackets[_intervalPacketsIndex] = 0;
            }

            if (_saveConfig && Svc.Condition[ConditionFlag.BetweenAreas])
            {
                EzConfig.Save();
                _saveConfig = false;
            }
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

            return Math.Clamp(dt - maxDelay, 0, CooldownDelayMax);
        }

        private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
        {
            var prevSeq = _inst->LastUsedActionSequence;
            var ret = _useActionLocationHook!.Original(self, actionType, actionId, targetId, location, extraParam, a7);
            var currSeq = _inst->LastUsedActionSequence;

            if (currSeq != prevSeq && _inst->AnimationLock == DefaultClientAnimLock)
            {
                _packetsSent = _intervalPackets.Sum();

                var spellId = ActionManager.GetSpellIdForAction(actionType, actionId);
                var dbLock = Cfg.AnimationLockDatabase.TryGetValue(spellId, out var v) && v >= DefaultClientAnimLock
                    ? v : DefaultClientAnimLock;

                _inst->AnimationLock = dbLock + SimulatedRTT;
                _appliedLocks[(ushort)currSeq] = dbLock + SimulatedRTT;

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
            var wasCasting = _inst->CastSpellId != 0;
            var oldLock = _inst->AnimationLock;

            _actionEffectHook!.Original(casterID, casterObj, targetPos, header, effects, targets);

            var localPlayer = Player.Object;
            if (localPlayer == null || casterID != localPlayer.EntityId || header->SourceSequence == 0)
                return;

            var newLock = _inst->AnimationLock;

            if (newLock >= DefaultClientAnimLock)
            {
                var spellId = header->SpellId;
                if (!Cfg.AnimationLockDatabase.TryGetValue(spellId, out var existing) || existing != newLock)
                {
                    Cfg.AnimationLockDatabase[spellId] = newLock;
                    _saveConfig = true;
                }
            }

            if (wasCasting && !Cfg.EnableIgnoreCasting)
            {
                _inst->AnimationLock = newLock;
                return;
            }

            var sequence = header->SourceSequence;
            float adjustedLock;

            if (Cfg.UsePercentageReduction)
            {
                adjustedLock = oldLock * (1f - Cfg.AnimationLockPercent / 100f);
            }
            else
            {
                var appliedLock = _appliedLocks.TryGetValue(sequence, out var al) ? al : DefaultClientAnimLock;
                var correction = newLock - (appliedLock - SimulatedRTT);
                var rtt = appliedLock - oldLock;

                if (rtt <= SimulatedRTT)
                    return;

                var weight = _packetsSent > 1 ? 0.1f : 1f;
                _delayAverage = _delayAverage > 0
                    ? _delayAverage * (1 - weight) + rtt * weight
                    : rtt;

                var average = Math.Max(_delayAverage, 0.001f);
                var variationMult = Math.Max(rtt / average, 1) - 1;
                var networkVariation = SimulatedRTT * variationMult;

                adjustedLock = Math.Max(oldLock + correction + networkVariation, 0);
            }

            if (!float.IsFinite(adjustedLock) || adjustedLock >= 20)
                return;

            _inst->AnimationLock = adjustedLock;
            _appliedLocks.Remove(sequence);
        }

        private bool SendPacketDetour(ZoneClient* zoneClient, nint packet, uint a3, uint a4, bool a5)
        {
            _intervalPackets[_intervalPacketsIndex]++;
            return _sendPacketHook!.Original(zoneClient, packet, a3, a4, a5);
        }
    }
}
