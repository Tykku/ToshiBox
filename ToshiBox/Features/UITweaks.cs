using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class UITweaks : IDisposable
    {
        private readonly Config _config;
        private readonly Dictionary<nint, float> _originalX = new();
        private bool _hooked;
        private bool _lbHooked;
        private string? _lbOriginalText;
        private int _lbLastPercent = -1;

        private UITweaksConfig Cfg => _config.UITweaksConfig;

        public UITweaks(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (Cfg.PartyListBuffsOnLeft) Enable();
            else Disable();

            if (Cfg.LimitBreakPercentage) EnableLimitBreak();
            else DisableLimitBreak();
        }

        private void Enable()
        {
            if (_hooked) return;
            _hooked = true;
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_PartyList", OnPartyListPostDraw);
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_PartyList", OnPartyListPreFinalize);
        }

        private void Disable()
        {
            if (!_hooked) return;
            _hooked = false;
            Svc.AddonLifecycle.UnregisterListener(OnPartyListPostDraw);
            Svc.AddonLifecycle.UnregisterListener(OnPartyListPreFinalize);

            if (GenericHelpers.TryGetAddonByName<AddonPartyList>("_PartyList", out _))
                RestoreAll();

            _originalX.Clear();
        }

        public void Dispose()
        {
            Disable();
            DisableLimitBreak();
        }

        private void EnableLimitBreak()
        {
            if (_lbHooked) return;
            _lbHooked = true;
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_LimitBreak", OnLimitBreakPostDraw);
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_LimitBreak", OnLimitBreakPreFinalize);
        }

        private void DisableLimitBreak()
        {
            if (!_lbHooked) return;
            _lbHooked = false;
            Svc.AddonLifecycle.UnregisterListener(OnLimitBreakPostDraw);
            Svc.AddonLifecycle.UnregisterListener(OnLimitBreakPreFinalize);

            if (_lbOriginalText != null && GenericHelpers.TryGetAddonByName<AddonLimitBreak>("_LimitBreak", out var addon)
                && addon->LimitBreakTextNode != null)
                addon->LimitBreakTextNode->SetText(_lbOriginalText);

            _lbOriginalText = null;
            _lbLastPercent = -1;
        }

        private void OnLimitBreakPreFinalize(AddonEvent type, AddonArgs args)
        {
            _lbOriginalText = null;
            _lbLastPercent = -1;
        }

        private void OnLimitBreakPostDraw(AddonEvent type, AddonArgs args)
        {
            var addon = (AddonLimitBreak*)args.Addon.Address;
            if (addon == null || addon->LimitBreakTextNode == null) return;

            var lb = LimitBreakController.Instance();
            if (lb == null) return;

            if (lb->BarUnits == 0) return;

            _lbOriginalText ??= addon->LimitBreakTextNode->NodeText.ToString(); // remember the label so disabling can restore it

            var percent = (int)(lb->CurrentUnits * 100L / lb->BarUnits); // per bar, so 3 full bars reads 300%
            if (percent == _lbLastPercent) return; // only rewrite the text when the value changes
            _lbLastPercent = percent;

            addon->LimitBreakTextNode->SetText($"{_lbOriginalText} {percent}%");
        }
        
        private void OnPartyListPreFinalize(AddonEvent type, AddonArgs args) => _originalX.Clear();

        private void OnPartyListPostDraw(AddonEvent type, AddonArgs args)
        {
            var partyList = (AddonPartyList*)args.Addon.Address;
            if (partyList == null) return;

            if (!ShouldApply())
            {
                if (_originalX.Count > 0)
                {
                    RestoreAll();
                    _originalX.Clear();
                }
                return;
            }

            for (var i = 0; i < 8; i++)
                ApplyMember(partyList->PartyMembers[i]);
        }

        private bool ShouldApply()
        {
            if (Cfg.PartyListBuffsOnLeftHealerOnly && !Player.Job.IsHealer()) return false;
            if (Cfg.PartyListBuffsOnLeftDutyOnly && !Svc.Condition[ConditionFlag.BoundByDuty]) return false;
            return true;
        }
        
        private void ApplyMember(AddonPartyList.PartyListMemberStruct member)
        {
            if (member.PartyMemberComponent == null) return;

            var firstIcon = member.StatusIcons[0].Value;
            if (firstIcon == null || firstIcon->OwnerNode == null) return;

            var iconWidth = firstIcon->OwnerNode->Width;

            for (var s = 0; s < 10; s++)
            {
                var node = member.StatusIcons[s].Value != null ? (AtkResNode*)member.StatusIcons[s].Value->OwnerNode : null;
                if (node == null) continue;

                CacheOriginal(node); // remember the default X so disabling can restore it
                node->SetPositionFloat(-5 - s * iconWidth, node->Y);
            }
        }

        private float CacheOriginal(AtkResNode* node)
        {
            var addr = (nint)node;
            if (!_originalX.TryGetValue(addr, out var x))
            {
                x = node->X;
                _originalX[addr] = x;
            }
            return x;
        }

        private void RestoreAll()
        {
            foreach (var (addr, x) in _originalX)
            {
                var node = (AtkResNode*)addr;
                node->SetPositionFloat(x, node->Y);
            }
        }
    }
}
