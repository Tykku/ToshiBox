using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ToshiBox.UI.Features
{
    public unsafe class DebugUI : IFeatureUI
    {
        public string Name           => "Debug";
        public string Group          => "Debug";
        public Dalamud.Interface.FontAwesomeIcon Icon => Dalamud.Interface.FontAwesomeIcon.Bug;
        public bool Enabled          { get => true; set { } }
        public bool Visible          => true;
        public bool HasEnabledToggle => false;

        private static readonly Vector4 ValueColor   = new(1f, 0.85f, 0.4f, 1f);
        private static readonly Vector4 SectionColor = new(0.6f, 0.8f, 1f, 1f);

        public void DrawSettings()
        {
            var gm      = GameMain.Instance();
            var fw      = Framework.Instance();
            var proxy   = fw  != null ? fw->NetworkModuleProxy  : null;
            var net     = proxy != null ? proxy->NetworkModule   : null;
            var grp     = GroupManager.Instance();
            var ef      = EventFramework.Instance();
            var replay  = ContentsReplayManager.Instance();
            var agent   = AgentLookingForGroup.Instance();
            var ps      = PlayerState.Instance();
            var wh      = WorldHelper.Instance();

            var chara   = Player.Object != null ? (Character*)(void*)Player.Object.Address : null;

            ContentDirector*         cd  = ef != null ? ef->GetContentDirector()         : null;
            InstanceContentDirector* icd = ef != null ? ef->GetInstanceContentDirector() : null;
            Director*                dir = cd  != null ? (Director*)(void*)cd             : null;

            // ── IClientState ─────────────────────────────────────────────
            Section("IClientState");
            Row("ClientLanguage",      Svc.ClientState.ClientLanguage.ToString());
            Row("TerritoryType",       Svc.ClientState.TerritoryType.ToString());
            Row("MapId",               Svc.ClientState.MapId.ToString());
            Row("Instance",            Svc.ClientState.Instance.ToString());
            Row("IsLoggedIn",          Svc.ClientState.IsLoggedIn.ToString());
            Row("IsPvP",               Svc.ClientState.IsPvP.ToString());
            Row("IsPvPExcludingDen",   Svc.ClientState.IsPvPExcludingDen.ToString());
            Row("IsGPosing",           Svc.ClientState.IsGPosing.ToString());
            Row("IsClientIdle",        Svc.ClientState.IsClientIdle(out var idleBlocker).ToString());
            Row("IsClientIdle.Blocker",idleBlocker.ToString());

            // ── Zone Instance ────────────────────────────────────────────
            ImGui.Spacing();
            Section("Zone Instance");
            Row("ClientState.Instance",                  Svc.ClientState.Instance.ToString());
            Row("NetworkModuleProxy.GetCurrentInstance", proxy != null ? proxy->GetCurrentInstance().ToString()   : "n/a");
            Row("NetworkModule.CurrentInstance",         net   != null ? net->CurrentInstance.ToString()          : "n/a");
            Row("GameMain.IsInInstanceArea",             gm    != null ? gm->IsInInstanceArea().ToString()        : "n/a");
            Row("GameMain.CurrentTerritoryFilterKey",    gm    != null ? gm->CurrentTerritoryFilterKey.ToString() : "n/a");
            Row("GameMain.TransitionTerritoryFilterKey", gm    != null ? gm->TransitionTerritoryFilterKey.ToString() : "n/a");

            // ── Party / Session ──────────────────────────────────────────
            ImGui.Spacing();
            Section("Party / Session");
            Row("GroupManager.MainGroup.PartyId",    grp != null ? grp->MainGroup.PartyId.ToString()    : "n/a");
            Row("GroupManager.MainGroup.PartyId_2",  grp != null ? grp->MainGroup.PartyId_2.ToString()  : "n/a");
            Row("GroupManager.MainGroup.MemberCount",grp != null ? grp->MainGroup.MemberCount.ToString(): "n/a");

            // ── EventFramework / Content Director ────────────────────────
            ImGui.Spacing();
            Section("EventFramework / Content Director");
            Row("EventFramework.GetCurrentContentId",   ef != null ? EventFramework.GetCurrentContentId().ToString()   : "n/a");
            Row("EventFramework.GetCurrentContentType", ef != null ? EventFramework.GetCurrentContentType().ToString() : "n/a");
            Row("Director.ContentId",                   dir != null ? dir->ContentId.ToString()           : "n/a");
            Row("Director.Sequence",                    dir != null ? dir->Sequence.ToString()             : "n/a");
            Row("ContentDirector.ContentTypeRowId",     cd  != null ? cd->ContentTypeRowId.ToString()     : "n/a");
            Row("ContentDirector.ContentTimeLeft",      cd  != null ? cd->ContentTimeLeft.ToString("F1")  : "n/a");
            Row("InstanceContentDirector.InstanceContentType", icd != null ? icd->InstanceContentType.ToString() : "n/a");

            // ── AgentLookingForGroup ContentUI ───────────────────────────
            ImGui.Spacing();
            Section("AgentLookingForGroup ContentUI");
            Row("ContentUI.InstanceContent.ContentFinderConditionRowId", agent != null ? agent->ContentUI.InstanceContent.ContentFinderConditionRowId.ToString() : "n/a");
            Row("ContentUI.InstanceContent.InstanceContentId",           agent != null ? agent->ContentUI.InstanceContent.InstanceContentId.ToString()           : "n/a");
            Row("ContentUI.PartyContent.ContentFinderConditionRowId",    agent != null ? agent->ContentUI.PartyContent.ContentFinderConditionRowId.ToString()    : "n/a");
            Row("ContentUI.PartyContent.PartyContentId",                 agent != null ? agent->ContentUI.PartyContent.PartyContentId.ToString()                 : "n/a");
            Row("ContentUI.PublicContent.PublicContentId",               agent != null ? agent->ContentUI.PublicContent.PublicContentId.ToString()               : "n/a");
            Row("ContentUI.ContentRoulette.ContentRouletteRowId",        agent != null ? agent->ContentUI.ContentRoulette.ContentRouletteRowId.ToString()        : "n/a");
            Row("AgentLFG.PartyContent.PartyContentId",                  agent != null ? agent->PartyContent.PartyContentId.ToString()                           : "n/a");

            // ── ContentsReplayManager ────────────────────────────────────
            ImGui.Spacing();
            Section("ContentsReplayManager");
            Row("LocalContentId",                    replay != null ? replay->LocalContentId.ToString()                           : "n/a");
            Row("Header.LocalContentId",             replay != null ? replay->Header.LocalContentId.ToString()                    : "n/a");
            Row("Header.ContentFinderConditionId",   replay != null ? replay->Header.ContentFinderConditionId.ToString()          : "n/a");
            Row("ZoneInitPacket.ServerId",           replay != null ? replay->ZoneInitPacket.ServerId.ToString()                  : "n/a");
            Row("ZoneInitPacket.TerritoryTypeId",    replay != null ? replay->ZoneInitPacket.TerritoryTypeId.ToString()           : "n/a");
            Row("ZoneInitPacket.ContentFinderConditionId", replay != null ? replay->ZoneInitPacket.ContentFinderConditionId.ToString() : "n/a");
            Row("ZoneInitPacket.TransitionTerritoryFilterKey", replay != null ? replay->ZoneInitPacket.TransitionTerritoryFilterKey.ToString() : "n/a");
            Row("ZoneInitPacket.PopRangeId",         replay != null ? replay->ZoneInitPacket.PopRangeId.ToString()                : "n/a");

            // ── PlayerState ──────────────────────────────────────────────
            ImGui.Spacing();
            Section("PlayerState");
            Row("PlayerState.ContentId", ps != null ? ps->ContentId.ToString() : "n/a");
            Row("PlayerState.EntityId",  ps != null ? ps->EntityId.ToString()  : "n/a");

            // ── World / Server ───────────────────────────────────────────
            ImGui.Spacing();
            Section("World / Server");
            if (chara != null)
            {
                var currentWorld = chara->CurrentWorld;
                var homeWorld    = chara->HomeWorld;
                var currentName  = wh != null ? wh->GetWorldNameById(currentWorld).ToString() : "?";
                var homeName     = wh != null ? wh->GetWorldNameById(homeWorld).ToString()    : "?";
                Row("Character.CurrentWorld", $"{currentWorld} ({currentName})");
                Row("Character.HomeWorld",    $"{homeWorld} ({homeName})");
                Row("Character.ContentId",    chara->ContentId.ToString());
            }
            else
            {
                ImGui.TextDisabled("No local player");
            }

            // ── GameMain Content ─────────────────────────────────────────
            ImGui.Spacing();
            Section("GameMain Content");
            Row("CurrentContentFinderConditionId", gm != null ? gm->CurrentContentFinderConditionId.ToString() : "n/a");
            Row("CurrentTerritoryTypeId",          gm != null ? gm->CurrentTerritoryTypeId.ToString()          : "n/a");
            Row("CurrentMapId",                    gm != null ? gm->CurrentMapId.ToString()                    : "n/a");

            // ── AddonPartyList (buff icon layout) ────────────────────────
            ImGui.Spacing();
            Section("AddonPartyList Layout");
            if (GenericHelpers.TryGetAddonByName<AddonPartyList>("_PartyList", out var partyList))
            {
                Row("MemberCount", partyList->MemberCount.ToString());
                Row("RowHeight",   partyList->RowHeight.ToString());

                for (var i = 0; i < partyList->MemberCount && i < 8; i++)
                {
                    var member = partyList->PartyMembers[i];
                    if (member.PartyMemberComponent == null) continue;

                    ImGui.Spacing();
                    ImGui.TextColored(SectionColor, $"Member {i}");

                    var row = member.PartyMemberComponent->OwnerNode;
                    Row("  Row",              row != null ? $"X={row->X} Y={row->Y} W={row->Width} H={row->Height}" : "n/a");

                    if (member.NameAndBarsContainer != null)
                        Row("  NameAndBarsContainer", $"X={member.NameAndBarsContainer->X} Y={member.NameAndBarsContainer->Y} W={member.NameAndBarsContainer->Width} H={member.NameAndBarsContainer->Height}");

                    if (member.ClassJobIcon != null)
                        Row("  ClassJobIcon", $"X={member.ClassJobIcon->X} Y={member.ClassJobIcon->Y} W={member.ClassJobIcon->Width} H={member.ClassJobIcon->Height}");

                    var hp = member.HPGaugeComponent != null ? member.HPGaugeComponent->OwnerNode : null;
                    Row("  HPGaugeComponent", hp != null ? $"X={hp->X} Y={hp->Y} W={hp->Width} H={hp->Height}" : "n/a");

                    for (var s = 0; s < 10; s++)
                    {
                        var icon = member.StatusIcons[s].Value;
                        if (icon == null) continue;
                        var node = icon->OwnerNode;
                        if (node == null) continue;
                        Row($"  StatusIcon[{s}]", $"X={node->X} Y={node->Y} W={node->Width} H={node->Height} Visible={node->IsVisible}");
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("_PartyList addon not found (not in a party / addon not visible).");
            }
        }

        private static void Section(string title)
        {
            ImGui.Separator();
            ImGui.TextColored(SectionColor, title);
            ImGui.Separator();
        }

        private static void Row(string label, string value)
        {
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.TextColored(ValueColor, value);
        }
    }
}
