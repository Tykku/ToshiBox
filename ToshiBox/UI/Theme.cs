using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;

namespace ToshiBox.UI
{

    public static class Theme
    {
        // --- Color Palette ---

        public static readonly Vector4 Accent        = new(0.40f, 0.70f, 1.00f, 1.00f);
        public static readonly Vector4 AccentDim     = new(0.30f, 0.50f, 0.75f, 1.00f);
        public static readonly Vector4 Gold          = new(0.92f, 0.80f, 0.35f, 1.00f);
        public static readonly Vector4 GoldDim       = new(0.70f, 0.60f, 0.25f, 1.00f);

        public static readonly Vector4 Success       = new(0.30f, 0.85f, 0.45f, 1.00f);
        public static readonly Vector4 SuccessDim    = new(0.20f, 0.60f, 0.30f, 1.00f);
        public static readonly Vector4 Warning       = new(0.95f, 0.75f, 0.20f, 1.00f);
        public static readonly Vector4 WarningDim    = new(0.70f, 0.55f, 0.15f, 1.00f);
        public static readonly Vector4 Error         = new(0.95f, 0.35f, 0.35f, 1.00f);
        public static readonly Vector4 ErrorDim      = new(0.70f, 0.25f, 0.25f, 1.00f);

        public static readonly Vector4 TextPrimary   = new(1.00f, 1.00f, 1.00f, 1.00f);
        public static readonly Vector4 TextSecondary = new(0.70f, 0.70f, 0.70f, 1.00f);
        public static readonly Vector4 TextMuted     = new(0.50f, 0.50f, 0.50f, 1.00f);
        public static readonly Vector4 TextDisabled  = new(0.35f, 0.35f, 0.35f, 1.00f);

        public static readonly Vector4 CardBg           = new(0.14f, 0.14f, 0.16f, 1.00f);
        public static readonly Vector4 CardBgHover      = new(0.18f, 0.18f, 0.22f, 1.00f);
        public static readonly Vector4 SectionBg        = new(0.10f, 0.10f, 0.12f, 0.60f);
        public static readonly Vector4 ProgressBg       = new(0.15f, 0.15f, 0.18f, 1.00f);

        public static readonly Vector4 SidebarBg          = new(0.10f, 0.10f, 0.12f, 1.00f);
        public static readonly Vector4 SidebarHeaderBg    = new(0.16f, 0.16f, 0.19f, 1.00f);
        public static readonly Vector4 SidebarHeaderHover = new(0.20f, 0.20f, 0.24f, 1.00f);

        // --- Spacing / Rounding ---

        public const float Pad         = 8f;
        public const float PadSmall    = 4f;
        public const float PadLarge    = 16f;
        public const float SectionGap  = 12f;
        public const float Rounding    = 8f;
        public const float RoundingSmall = 4f;
        public const float RoundingLarge = 10f;

        // ──────────────────────────────────────────────
        // Sidebar helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Draws a collapsible sidebar group header. Returns true when expanded.
        /// </summary>
        public static bool SidebarGroupHeader(string label, ref bool isExpanded, FontAwesomeIcon icon = 0)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos      = ImGui.GetCursorScreenPos();
            var avail    = ImGui.GetContentRegionAvail().X;
            var lineH    = ImGui.GetTextLineHeightWithSpacing() + 10;

            var isHovered = ImGui.IsMouseHoveringRect(pos, new Vector2(pos.X + avail, pos.Y + lineH));
            var bgColor   = isHovered ? SidebarHeaderHover : SidebarHeaderBg;
            drawList.AddRectFilled(pos, new Vector2(pos.X + avail, pos.Y + lineH),
                ImGui.ColorConvertFloat4ToU32(bgColor), RoundingSmall);

            var textY  = pos.Y + (lineH - ImGui.GetTextLineHeight()) / 2;
            var labelX = 6f;

            if (icon != 0)
            {
                var iconFont = UiBuilder.IconFont;
                const float iconFontSize = 16f;
                drawList.AddText(iconFont, iconFontSize, new Vector2(pos.X + 8, textY),
                    ImGui.ColorConvertFloat4ToU32(TextSecondary), icon.ToIconString());
                labelX = 28f;
            }

            drawList.AddText(new Vector2(pos.X + labelX, textY),
                ImGui.ColorConvertFloat4ToU32(TextSecondary), label.ToUpperInvariant());

            ImGui.InvisibleButton($"##sgh_{label}", new Vector2(avail, lineH));
            if (ImGui.IsItemClicked())
                isExpanded = !isExpanded;

            return isExpanded;
        }

        /// <summary>
        /// Draws a sidebar navigation item. Returns true when clicked.
        /// </summary>
        public static bool SidebarItem(string label, bool isSelected, FontAwesomeIcon icon = 0)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos      = ImGui.GetCursorScreenPos();
            var avail    = ImGui.GetContentRegionAvail().X;
            var lineH    = ImGui.GetTextLineHeightWithSpacing() + 10;
            var labelX   = 14f;

            if (isSelected)
            {
                drawList.AddRectFilled(pos, new Vector2(pos.X + avail, pos.Y + lineH),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(Accent.X, Accent.Y, Accent.Z, 0.15f)),
                    RoundingSmall);
                drawList.AddRectFilled(pos, new Vector2(pos.X + 3, pos.Y + lineH),
                    ImGui.ColorConvertFloat4ToU32(Accent));
            }

            var textColor = isSelected ? Accent : TextSecondary;

            if (icon != 0)
            {
                var iconFont = UiBuilder.IconFont;
                const float iconFontSize = 18f;
                var iconY = pos.Y + (lineH - iconFontSize) / 2;
                drawList.AddText(iconFont, iconFontSize, new Vector2(pos.X + 10, iconY),
                    ImGui.ColorConvertFloat4ToU32(textColor), icon.ToIconString());
                labelX = 34f;
            }

            drawList.AddText(
                new Vector2(pos.X + labelX, pos.Y + (lineH - ImGui.GetTextLineHeight()) / 2),
                ImGui.ColorConvertFloat4ToU32(textColor), label);

            ImGui.InvisibleButton($"##si_{label}", new Vector2(avail, lineH));
            return ImGui.IsItemClicked();
        }

        // ──────────────────────────────────────────────
        // Section / Card helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Draws a section header with a colored left accent bar.
        /// </summary>
        public static void SectionHeader(string label, Vector4? color = null)
        {
            var c        = color ?? Accent;
            var pos      = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            drawList.AddRectFilled(
                pos,
                new Vector2(pos.X + 3, pos.Y + ImGui.GetTextLineHeight()),
                ImGui.ColorConvertFloat4ToU32(c));

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
            ImGui.TextColored(c, label);
        }

        /// <summary>
        /// Begins a visual card (subtle background region).
        /// Must pair with EndCard().
        /// </summary>
        public static bool BeginCard(string id, float height = 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, Pad));
            var result = ImGui.BeginChild(id, new Vector2(-1, height), true);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();
            return result;
        }

        public static void EndCard() => ImGui.EndChild();

        // ──────────────────────────────────────────────
        // Button helpers
        // ──────────────────────────────────────────────

        public static bool PrimaryButton(string label, Vector2 size = default)
        {
            if (size == default) size = new Vector2(0, 30);
            return ColoredButton(label, size, new Vector4(0.20f, 0.45f, 0.80f, 1.00f), TextPrimary);
        }

        public static bool SecondaryButton(string label, Vector2 size = default)
        {
            if (size == default) size = new Vector2(0, 30);
            return ColoredButton(label, size, new Vector4(0.25f, 0.25f, 0.30f, 1.00f), TextSecondary);
        }

        public static bool ColoredButton(string label, Vector2 size, Vector4 bgColor, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(
                Math.Min(1, bgColor.X + 0.10f), Math.Min(1, bgColor.Y + 0.10f),
                Math.Min(1, bgColor.Z + 0.10f), bgColor.W));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(
                Math.Max(0, bgColor.X - 0.05f), Math.Max(0, bgColor.Y - 0.05f),
                Math.Max(0, bgColor.Z - 0.05f), bgColor.W));
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            var result = ImGui.Button(label, size);
            ImGui.PopStyleColor(4);
            return result;
        }

        // ──────────────────────────────────────────────
        // Frame / Checkbox style push/pop
        // ──────────────────────────────────────────────

        private static readonly Vector4 FrameBg         = new(0.08f, 0.08f, 0.10f, 1.00f);
        private static readonly Vector4 FrameBgHovered  = new(0.14f, 0.16f, 0.22f, 1.00f);
        private static readonly Vector4 FrameBgActive   = new(0.10f, 0.12f, 0.18f, 1.00f);
        private static readonly Vector4 FrameBorder     = new(0.40f, 0.45f, 0.55f, 0.80f);
        private static readonly Vector4 CheckMarkColor  = new(0.30f, 0.80f, 0.50f, 1.00f);

        public static void PushFrameStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBg);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHovered);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, FrameBgActive);
            ImGui.PushStyleColor(ImGuiCol.Border, FrameBorder);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, Accent);
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.50f, 0.80f, 1.00f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.20f, 0.25f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.28f, 0.35f, 1.00f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, RoundingSmall);
        }

        public static void PopFrameStyle()
        {
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(8);
        }

        public static void PushCheckboxStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.10f, 0.10f, 0.12f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.18f, 0.22f, 0.18f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.15f, 0.25f, 0.20f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, CheckMarkColor);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.35f, 0.40f, 0.45f, 0.80f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3, 3));
        }

        public static void PopCheckboxStyle()
        {
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(5);
        }

        // ──────────────────────────────────────────────
        // Toggle switch
        // ──────────────────────────────────────────────

        /// <summary>
        /// Draws a toggle switch with green/dim track and white knob.
        /// Returns true when the value changes.
        /// </summary>
        public static bool ToggleSwitch(string id, string label, ref bool value)
        {
            var pos    = ImGui.GetCursorScreenPos();
            var dl     = ImGui.GetWindowDrawList();
            var height = ImGui.GetFrameHeight() * 0.75f;
            var width  = height * 1.8f;
            var radius = height * 0.5f;

            var pressed = ImGui.InvisibleButton($"##{id}_toggle", new Vector2(width, height));
            if (pressed) value = !value;

            var hov = ImGui.IsItemHovered();
            var trackColor = value
                ? ImGui.ColorConvertFloat4ToU32(new Vector4(
                    hov ? 0.35f : 0.25f, hov ? 0.85f : 0.75f, hov ? 0.40f : 0.30f, 1f))
                : ImGui.ColorConvertFloat4ToU32(new Vector4(
                    hov ? 0.35f : 0.25f, hov ? 0.35f : 0.25f, hov ? 0.40f : 0.30f, 1f));

            dl.AddRectFilled(pos, new Vector2(pos.X + width, pos.Y + height), trackColor, radius);

            var knobX = value ? pos.X + width - radius : pos.X + radius;
            dl.AddCircleFilled(new Vector2(knobX, pos.Y + radius), radius - 1.5f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)));

            if (!string.IsNullOrEmpty(label))
            {
                ImGui.SameLine(0, PadSmall);
                ImGui.Text(label);
            }

            return pressed;
        }

        // ──────────────────────────────────────────────
        // Misc helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Draws a game icon by icon ID. Falls back to a dummy if unavailable.
        /// </summary>
        public static void DrawGameIcon(uint iconId, Vector2 size)
        {
            if (iconId == 0) { ImGui.Dummy(size); return; }
            var wrap = Svc.Texture.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
            if (wrap != null)
                ImGui.Image(wrap.Handle, size);
            else
                ImGui.Dummy(size);
        }

        public static void HelpMarker(string description)
        {
            ImGui.SameLine();
            ImGui.TextColored(TextMuted, "(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(300);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        public static void KeyValue(string key, string value, Vector4? valueColor = null)
        {
            ImGui.TextColored(TextSecondary, key);
            ImGui.SameLine();
            ImGui.TextColored(valueColor ?? TextPrimary, value);
        }
    }
}
