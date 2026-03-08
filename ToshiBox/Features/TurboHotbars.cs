using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class TurboHotbars
    {
        private class TurboInfo
        {
            public Stopwatch LastPress { get; } = new();
            public bool LastFrameHeld { get; set; } = false;
            public int RepeatDelay { get; set; } = 0;
            public bool IsReady => LastPress.IsRunning && LastPress.ElapsedMilliseconds >= RepeatDelay;
        }

        private readonly Config _config;
        private static readonly Dictionary<uint, TurboInfo> inputIDInfos = new();
        private static bool isAnyTurboRunning;

        private delegate byte IsInputIDDelegate(nint inputData, uint id);
        private delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);

        private Hook<IsInputIDDelegate>? _isInputIDPressedHook;
        private Hook<CheckHotbarBindingsDelegate>? _checkHotbarBindingsHook;
        private IsInputIDDelegate? _isInputIDHeld;

        public TurboHotbars(Config config) => _config = config;

        public void IsEnabled()
        {
            if (_config.TurboHotbarsConfig.Enabled) Enable();
            else Disable();
        }

        public void Enable()
        {
            var pressedAddr  = Svc.SigScanner.ScanText("E9 ?? ?? ?? ?? 83 7F 44 02");
            var heldAddr     = Svc.SigScanner.ScanText("E9 ?? ?? ?? ?? B9 4F 01 00 00");
            var bindingsAddr = Svc.SigScanner.ScanText("89 54 24 10 53 41 55 41 57");

            _isInputIDHeld = Marshal.GetDelegateForFunctionPointer<IsInputIDDelegate>(heldAddr);
            _isInputIDPressedHook = Svc.Hook.HookFromAddress<IsInputIDDelegate>(pressedAddr, IsInputIDPressedDetour);
            _checkHotbarBindingsHook = Svc.Hook.HookFromAddress<CheckHotbarBindingsDelegate>(bindingsAddr, CheckHotbarBindingsDetour);
            _checkHotbarBindingsHook.Enable();
            // _isInputIDPressedHook is enabled/disabled inside CheckHotbarBindingsDetour
        }

        public void Disable()
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
            var isHeld    = _isInputIDHeld!(inputData, id) != 0;
            var useHeld   = info.IsReady && (_config.TurboHotbarsConfig.EnableOutOfCombat || Svc.Condition[ConditionFlag.InCombat]);
            var ret       = useHeld ? isHeld : isPressed;

            if (ret)
            {
                info.RepeatDelay = isPressed && _config.TurboHotbarsConfig.InitialInterval > 0
                    ? _config.TurboHotbarsConfig.InitialInterval
                    : _config.TurboHotbarsConfig.Interval;
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
    }
}
