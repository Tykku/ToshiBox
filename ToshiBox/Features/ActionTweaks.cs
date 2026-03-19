using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using ToshiBox.Common;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ToshiBox.Features
{
    public unsafe class ActionTweaks : IDisposable
    {
        private readonly Config _config;
        private TurboHotbarsConfig TurboCfg => _config.TurboHotbarsConfig;
        private CameraRelativeDashesConfig DashCfg => _config.CameraRelativeDashesConfig;
        private AutoDismountConfig DismountCfg => _config.AutoDismountConfig;

        // ======= Turbo Hotbars =======

        private class TurboInfo
        {
            public Stopwatch LastPress { get; } = new();
            public bool LastFrameHeld { get; set; } = false;
            public int RepeatDelay { get; set; } = 0;
            public bool IsReady => LastPress.IsRunning && LastPress.ElapsedMilliseconds >= RepeatDelay;
        }

        private static readonly Dictionary<uint, TurboInfo> inputIDInfos = new();
        private static bool isAnyTurboRunning;

        private delegate byte IsInputIDDelegate(nint inputData, uint id);
        private delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);

        private Hook<IsInputIDDelegate>? _isInputIDPressedHook;
        private Hook<CheckHotbarBindingsDelegate>? _checkHotbarBindingsHook;

        // ======= Camera Relative Dashes / Auto Dismount =======

        private Hook<ActionManager.Delegates.UseAction>? _useActionHook;

        // ======= Auto Dismount =======

        private bool _isMountActionQueued;
        private (ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId) _queuedMountAction;
        private readonly Stopwatch _mountActionTimer = new();

        // ======= Constructor =======

        public ActionTweaks(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (TurboCfg.Enabled) EnableTurbo();
            else DisableTurbo();

            if (DashCfg.Enabled || DismountCfg.Enabled) EnableUseAction();
            else DisableUseAction();

            if (DismountCfg.Enabled) Svc.Framework.Update += OnUpdate;
            else Svc.Framework.Update -= OnUpdate;
        }

        public void Dispose()
        {
            DisableTurbo();
            DisableUseAction();
            Svc.Framework.Update -= OnUpdate;
        }

        // ======= Turbo Hotbars =======

        private void EnableTurbo()
        {
            if (_checkHotbarBindingsHook != null) return;

            var bindingsAddr = Svc.SigScanner.ScanText("89 54 24 10 53 41 55 41 57 48 83 EC 40 4C 8B E9 8B DA");

            _isInputIDPressedHook    = Svc.Hook.HookFromAddress<IsInputIDDelegate>(InputData.Addresses.IsInputIdPressed.Value, IsInputIDPressedDetour);
            _checkHotbarBindingsHook = Svc.Hook.HookFromAddress<CheckHotbarBindingsDelegate>(bindingsAddr, CheckHotbarBindingsDetour);
            _checkHotbarBindingsHook.Enable();
            // _isInputIDPressedHook is enabled/disabled inside CheckHotbarBindingsDetour
        }

        private void DisableTurbo()
        {
            _isInputIDPressedHook?.Disable();
            _isInputIDPressedHook?.Dispose();
            _isInputIDPressedHook = null;

            _checkHotbarBindingsHook?.Disable();
            _checkHotbarBindingsHook?.Dispose();
            _checkHotbarBindingsHook = null;

            inputIDInfos.Clear();
        }

        private byte IsInputIDPressedDetour(nint inputData, uint id)
        {
            if (!inputIDInfos.TryGetValue(id, out var info))
                inputIDInfos[id] = info = new TurboInfo();

            var isPressed = _isInputIDPressedHook!.Original(inputData, id) != 0;
            var isHeld    = ((InputData*)inputData)->IsInputIdDown((InputId)id);
            var useHeld   = info.IsReady && (TurboCfg.EnableOutOfCombat || Svc.Condition[ConditionFlag.InCombat]);
            var ret       = useHeld ? isHeld : isPressed;

            if (ret)
            {
                info.RepeatDelay = isPressed && TurboCfg.InitialInterval > 0
                    ? TurboCfg.InitialInterval
                    : TurboCfg.Interval;
                info.LastPress.Restart();
            }
            else if (isHeld != info.LastFrameHeld)
            {
                if (isHeld && isAnyTurboRunning) { info.RepeatDelay = 200; info.LastPress.Restart(); }
                else info.LastPress.Reset();
            }

            info.LastFrameHeld = isHeld;
            return (byte)(ret ? 1 : 0);
        }

        private void CheckHotbarBindingsDetour(nint a1, byte a2)
        {
            isAnyTurboRunning = inputIDInfos.Any(t => t.Value.LastPress.IsRunning);
            _isInputIDPressedHook!.Enable();
            _checkHotbarBindingsHook!.Original(a1, a2);
            _isInputIDPressedHook.Disable();
        }

        // ======= Camera Relative Dashes =======

        private void EnableUseAction()
        {
            if (_useActionHook != null) return;
            try
            {
                _useActionHook = Svc.Hook.HookFromAddress<ActionManager.Delegates.UseAction>(
                    ActionManager.Addresses.UseAction.Value, UseActionDetour);
                _useActionHook.Enable();
            }
            catch
            {
                Svc.Log.Warning("[ActionTweaks] Failed to hook UseAction.");
            }
        }

        private void DisableUseAction()
        {
            _useActionHook?.Disable();
            _useActionHook?.Dispose();
            _useActionHook = null;
        }

        private bool UseActionDetour(ActionManager* am, ActionType actionType, uint actionId,
            ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
        {
            if (DashCfg.Enabled && actionType == ActionType.Action)
            {
                var adjustedId = am->GetAdjustedActionId(actionId);
                if (ShouldRotate(adjustedId)
                    && am->GetActionStatus(ActionType.Action, adjustedId) == 0
                    && am->AnimationLock == 0)
                {
                    var cm = CameraManager.Instance();
                    var cam = cm != null ? cm->GetActiveCamera() : null;
                    if (cam != null)
                    {
                        var localPlayer = Svc.Objects.LocalPlayer;
                        if (localPlayer != null)
                        {
                            var dirH = cam->DirH;
                            var gameObjRot = dirH > 0 ? dirH - MathF.PI : dirH + MathF.PI;
                            ((GameObject*)localPlayer.Address)->SetRotation(gameObjRot);
                        }
                    }
                }
            }

            if (DismountCfg.Enabled && ShouldDismount(am, actionType, actionId, targetId))
            {
                var dismounted = _useActionHook!.Original(am, ActionType.GeneralAction, 23, 0, 0, ActionManager.UseActionMode.None, 0, null);
                if (dismounted)
                {
                    _isMountActionQueued = true;
                    _queuedMountAction = (actionType, actionId, targetId, extraParam, mode, comboRouteId);
                    _mountActionTimer.Restart();
                }
                return dismounted;
            }

            return _useActionHook!.Original(am, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        }

        private bool ShouldDismount(ActionManager* am, ActionType actionType, uint actionId, ulong targetId)
        {
            if (!Svc.Condition[ConditionFlag.Mounted]) return false;
            if (actionType == ActionType.Action && actionId is 5 or 6) return false; // Teleport, Return
            if (actionType != ActionType.Action && actionType != ActionType.GeneralAction) return false;
            if (actionType == ActionType.GeneralAction && actionId is not (3 or 4)) return false; // Only LB and Sprint
            return am->GetActionStatus(actionType, actionId, targetId) != 0;
        }

        private void OnUpdate(IFramework framework)
        {
            if (!_isMountActionQueued || Svc.Condition[ConditionFlag.Mounted]) return;

            _isMountActionQueued = false;
            _mountActionTimer.Stop();

            if (_mountActionTimer.ElapsedMilliseconds > 2000) return;

            var am = ActionManager.Instance();
            if (am == null) return;
            _useActionHook!.Original(am, _queuedMountAction.actionType, _queuedMountAction.actionId,
                _queuedMountAction.targetId, _queuedMountAction.extraParam, _queuedMountAction.mode,
                _queuedMountAction.comboRouteId, null);
        }

        private bool ShouldRotate(uint adjustedId)
        {
            var action = Svc.Data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(adjustedId);
            if (action == null) return false;
            if (!action.Value.AffectsPosition && adjustedId != 29494) return false;
            if (!action.Value.CanTargetSelf) return false;
            if (DashCfg.BlockBackwardDashes && action.Value.BehaviourType is 3 or 4) return false;
            return action.Value.BehaviourType > 1;
        }
    }
}