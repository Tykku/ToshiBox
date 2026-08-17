using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
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

        private UITweaksConfig Cfg => _config.UITweaksConfig;

        public UITweaks(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (Cfg.PartyListBuffsOnLeft) Enable();
            else Disable();
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

        public void Dispose() => Disable();
        
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
                node->SetPositionFloat(-s * iconWidth, node->Y);
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
