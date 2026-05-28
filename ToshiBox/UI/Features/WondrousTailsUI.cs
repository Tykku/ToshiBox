using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ToshiBox.WondrousTails;

namespace ToshiBox.UI.Features
{
    public class WondrousTailsUI : IFeatureUI
    {
        private readonly PerfectTails _solver;

        public string Name           => "Wondrous Tails";
        public string Group          => "Tools";
        public Dalamud.Interface.FontAwesomeIcon Icon => Dalamud.Interface.FontAwesomeIcon.Star;
        public bool Enabled          { get => true; set { } }
        public bool Visible          => true;
        public bool HasEnabledToggle => false;

        public WondrousTailsUI(PerfectTails solver) => _solver = solver;

        public void DrawSettings()
        {
            UpdateGameState();

            var stickersPlaced = _solver.GameState.Count(s => s);
            ImGui.Text($"Stickers: {stickersPlaced} / 9");
            ImGui.Spacing();

            DrawBingoGrid();
            ImGui.Spacing();
            ImGui.Spacing();

            DrawProbabilities(stickersPlaced);
        }

        private unsafe void UpdateGameState()
        {
            var playerState = PlayerState.Instance();
            for (var i = 0; i < 16; i++)
                _solver.GameState[i] = playerState->IsWeeklyBingoStickerPlaced(i);
        }

        private const float CellSize = 44f;
        private const float GridSize = CellSize * 4;

        private void DrawBingoGrid()
        {
            var origin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(GridSize, GridSize));
            var dl = ImGui.GetWindowDrawList();

            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var p0 = new Vector2(origin.X + c * CellSize, origin.Y + r * CellSize);
                    var p1 = new Vector2(p0.X + CellSize, p0.Y + CellSize);

                    var hasSticker = _solver.GameState[r * 4 + c];
                    var bgColor = hasSticker
                        ? new Vector4(0.18f, 0.50f, 0.26f, 0.60f)
                        : new Vector4(0.12f, 0.12f, 0.14f, 1.00f);

                    dl.AddRectFilled(p0, p1, ImGui.ColorConvertFloat4ToU32(bgColor), 5f);
                    dl.AddRect(p0, p1, ImGui.ColorConvertFloat4ToU32(new Vector4(0.32f, 0.32f, 0.38f, 1f)), 5f, 0, 1f);

                    if (hasSticker)
                    {
                        var center = new Vector2(p0.X + CellSize / 2f, p0.Y + CellSize / 2f);
                        dl.AddCircleFilled(center, CellSize * 0.26f, ImGui.ColorConvertFloat4ToU32(Theme.Success));
                    }
                }
            }

            HighlightCompletedLines(dl, origin);
        }

        private void HighlightCompletedLines(ImDrawListPtr dl, Vector2 origin)
        {
            var gold = ImGui.ColorConvertFloat4ToU32(new Vector4(0.92f, 0.80f, 0.35f, 0.30f));

            for (var r = 0; r < 4; r++)
            {
                if (!Enumerable.Range(0, 4).All(c => _solver.GameState[r * 4 + c])) continue;
                dl.AddRectFilled(
                    new Vector2(origin.X, origin.Y + r * CellSize),
                    new Vector2(origin.X + GridSize, origin.Y + (r + 1) * CellSize),
                    gold);
            }

            for (var c = 0; c < 4; c++)
            {
                if (!Enumerable.Range(0, 4).All(r => _solver.GameState[r * 4 + c])) continue;
                dl.AddRectFilled(
                    new Vector2(origin.X + c * CellSize, origin.Y),
                    new Vector2(origin.X + (c + 1) * CellSize, origin.Y + GridSize),
                    gold);
            }

            if (Enumerable.Range(0, 4).All(i => _solver.GameState[i * 4 + i]))
            {
                for (var i = 0; i < 4; i++)
                    dl.AddRectFilled(
                        new Vector2(origin.X + i * CellSize, origin.Y + i * CellSize),
                        new Vector2(origin.X + (i + 1) * CellSize, origin.Y + (i + 1) * CellSize),
                        gold);
            }

            if (Enumerable.Range(0, 4).All(i => _solver.GameState[i * 4 + (3 - i)]))
            {
                for (var i = 0; i < 4; i++)
                    dl.AddRectFilled(
                        new Vector2(origin.X + (3 - i) * CellSize, origin.Y + i * CellSize),
                        new Vector2(origin.X + (4 - i) * CellSize, origin.Y + (i + 1) * CellSize),
                        gold);
            }
        }

        private void DrawProbabilities(int stickersPlaced)
        {
            var probs = _solver.Solve(_solver.GameState);
            if (probs[0] < 0)
            {
                ImGui.TextColored(Theme.Error, "No data available.");
                return;
            }

            double[]? samples = stickersPlaced is > 0 and <= 7 ? _solver.GetSample(stickersPlaced) : null;

            Theme.SectionHeader("Line Chances");
            ImGui.Spacing();

            string[] labels = ["1 Line", "2 Lines", "3 Lines"];
            const double bound = 0.05;

            for (var i = 0; i < 3; i++)
            {
                var value = probs[i];

                Vector4 color;
                if (Math.Abs(value - 1) < 0.1)
                    color = Theme.Gold;
                else if (samples == null)
                    color = Theme.TextPrimary;
                else
                {
                    var sample     = samples[i];
                    var lowerBound = Math.Max(0, sample - bound);
                    if (value >= sample)
                        color = Theme.Success;
                    else if (value > lowerBound)
                        color = Theme.Warning;
                    else if (value > 0)
                        color = Theme.Error;
                    else
                        color = Theme.TextMuted;
                }

                Theme.KeyValue($"{labels[i]}:", $"{value * 100:F2}%", color);

                if (samples != null)
                {
                    ImGui.SameLine(0, 8f);
                    ImGui.TextColored(Theme.TextMuted, $"(avg: {samples[i] * 100:F2}%)");
                }
            }
        }
    }
}
