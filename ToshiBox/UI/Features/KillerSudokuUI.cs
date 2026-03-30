using System.Numerics;
using Dalamud.Bindings.ImGui;
using ToshiBox.KillerSudoku;

namespace ToshiBox.UI.Features
{
    public class KillerSudokuUI : IFeatureUI
    {
        private readonly KillerSudokuGame _game = new();

        public string Name            => "Killer Sudoku";
        public bool Enabled           { get => true; set { } }
        public bool Visible           => true;
        public bool HasEnabledToggle  => false;

        private const float CellSize  = 46f;
        private const float GridSize  = CellSize * 9; // 414

        private ValidationResult? _lastValidation;
        private string _inputBuf = string.Empty;
        private bool _refocusInput;

        // Cage coloring — rebuilt whenever the puzzle changes
        private KillerSudokuPuzzle? _coloredPuzzle;
        private int[] _cageColors = [];

        private void EnsureCageColors()
        {
            if (ReferenceEquals(_game.Puzzle, _coloredPuzzle)) return;
            _coloredPuzzle = _game.Puzzle;

            int n = _game.Puzzle.Cages.Count;
            var adj = new System.Collections.Generic.HashSet<int>[n];
            for (int i = 0; i < n; i++) adj[i] = [];

            // Two cages are adjacent if any of their cells share an edge
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = {  0, 0,-1, 1 };
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int a = _game.Puzzle.CageIndex[r, c];
                    for (int d = 0; d < 4; d++)
                    {
                        int nr = r + dr[d], nc = c + dc[d];
                        if (nr < 0 || nr > 8 || nc < 0 || nc > 8) continue;
                        int b = _game.Puzzle.CageIndex[nr, nc];
                        if (a != b) { adj[a].Add(b); adj[b].Add(a); }
                    }
                }
            }

            // Greedy coloring: assign lowest palette index not used by any already-colored neighbor
            _cageColors = new int[n];
            for (int i = 0; i < n; i++) _cageColors[i] = -1;
            for (int i = 0; i < n; i++)
            {
                var used = new System.Collections.Generic.HashSet<int>();
                foreach (int nb in adj[i])
                    if (_cageColors[nb] >= 0) used.Add(_cageColors[nb]);
                int col = 0;
                while (used.Contains(col)) col++;
                _cageColors[i] = col;
            }
        }

        // ── IFeatureUI ────────────────────────────────────────────────────────────

        public void DrawSettings()
        {
            DrawControls();
            ImGui.Spacing();
            HandleKeyInput();
            DrawGrid();
        }

        // ── Controls bar ──────────────────────────────────────────────────────────

        private void DrawControls()
        {
            var elapsed = _game.Elapsed;
            ImGui.Text($"Time: {(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}");

            ImGui.SameLine(0, 16f);
            ImGui.TextColored(_game.Mistakes == 0 ? Theme.TextMuted : Theme.Error,
                $"Mistakes: {_game.Mistakes}");

            ImGui.SameLine(0, 16f);

            if (Theme.PrimaryButton("New Puzzle"))
            {
                _game.NewPuzzle();
                _lastValidation = null;
            }

            ImGui.SameLine(0, 6f);

            if (Theme.SecondaryButton("Clear"))
            {
                _game.ClearSelected();
                _lastValidation = null;
            }

            ImGui.SameLine(0, 6f);

            if (Theme.SecondaryButton("Validate"))
                _lastValidation = _game.Validate();

            ImGui.SameLine(0, 6f);

            if (_game.NotesMode)
            {
                if (Theme.ColoredButton("Notes: ON", new System.Numerics.Vector2(0, 30),
                        new System.Numerics.Vector4(0.20f, 0.55f, 0.30f, 1f), Theme.TextPrimary))
                    _game.NotesMode = false;
            }
            else
            {
                if (Theme.SecondaryButton("Notes: OFF"))
                    _game.NotesMode = true;
            }

            if (_lastValidation.HasValue)
            {
                ImGui.SameLine(0, 12f);
                switch (_lastValidation.Value)
                {
                    case ValidationResult.Correct:
                        ImGui.TextColored(Theme.Success, "Solved!");
                        break;
                    case ValidationResult.Conflicts:
                        ImGui.TextColored(Theme.Error, "Conflicts found.");
                        break;
                    case ValidationResult.Incomplete:
                        ImGui.TextColored(Theme.Warning, "No conflicts, but not complete.");
                        break;
                }
            }

            ImGui.TextColored(Theme.TextMuted, "Click a cell, then type 1–9.  Backspace to clear.  Arrow keys to move.");
        }

        // ── Keyboard input ────────────────────────────────────────────────────────

        private void HandleKeyInput()
        {
            if (_game.SelectedRow < 0 || _game.SelectedCol < 0 || _game.IsComplete)
                return;

            // A tiny invisible InputText sets WantCaptureKeyboard = true, which
            // prevents FFXIV from consuming number keys (hotbar 1–9) this frame.
            ImGui.SetNextItemWidth(1f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg,        Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text,           Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border,         Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);

            if (_refocusInput)
            {
                ImGui.SetKeyboardFocusHere();
                _refocusInput = false;
            }

            // CallbackCharFilter intercepts each char BEFORE ImGui adds it to the buffer.
            // We process it immediately and reject it (EventChar = 0), so the buffer
            // always stays empty — no accumulation, instant replacement.
            var self = this;
            ImGui.InputText(
                "##sudoku_kb", ref _inputBuf, 2,
                ImGuiInputTextFlags.CallbackCharFilter,
                static (scoped ref ImGuiInputTextCallbackData data, scoped ref KillerSudokuUI ctx) =>
                {
                    char c = (char)data.EventChar;
                    if (c >= '1' && c <= '9')
                    {
                        int n = c - '0';
                        if (ctx._game.NotesMode)
                        {
                            if (ctx._game.Board[ctx._game.SelectedRow, ctx._game.SelectedCol] == 0)
                                ctx._game.ToggleNote(ctx._game.SelectedRow, ctx._game.SelectedCol, n);
                            // cell already has a number in notes mode — do nothing
                        }
                        else
                        {
                            ctx._game.SetNumber(ctx._game.SelectedRow, ctx._game.SelectedCol, n);
                            ctx._lastValidation = null;
                        }
                    }
                    data.EventChar = 0; // reject — keep buffer empty
                    return 0;
                }, ref self);

            bool focused = ImGui.IsItemFocused();
            ImGui.PopStyleColor(4);

            if (!focused) return;

            if (ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.IsKeyPressed(ImGuiKey.Delete))
            {
                _game.SetNumber(_game.SelectedRow, _game.SelectedCol, 0);
                _lastValidation = null;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Space))
                _game.NotesMode = !_game.NotesMode;

            int r = _game.SelectedRow, c = _game.SelectedCol;
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)    && r > 0) _game.SelectedRow--;
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)  && r < 8) _game.SelectedRow++;
            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow)  && c > 0) _game.SelectedCol--;
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && c < 8) _game.SelectedCol++;
        }

        // ── Grid rendering ────────────────────────────────────────────────────────

        private void DrawGrid()
        {
            var origin = ImGui.GetCursorScreenPos();

            // Reserve the grid area and capture mouse interaction
            ImGui.InvisibleButton("##sudoku_grid", new Vector2(GridSize, GridSize));
            bool clicked = ImGui.IsItemClicked();
            bool hovered = ImGui.IsItemHovered();

            int hovRow = -1, hovCol = -1;
            if (hovered || clicked)
            {
                var mouse = ImGui.GetMousePos();
                hovCol = (int)((mouse.X - origin.X) / CellSize);
                hovRow = (int)((mouse.Y - origin.Y) / CellSize);
                if (hovRow < 0 || hovRow > 8 || hovCol < 0 || hovCol > 8)
                    hovRow = hovCol = -1;
            }

            if (clicked && hovRow >= 0)
            {
                _game.SelectedRow = hovRow;
                _game.SelectedCol = hovCol;
                _refocusInput     = true;
            }

            EnsureCageColors();
            var dl = ImGui.GetWindowDrawList();
            DrawCellBackgrounds(dl, origin, hovRow, hovCol);
            DrawCageNumbers(dl, origin);
            DrawNotes(dl, origin);
            DrawBoardNumbers(dl, origin);
            DrawBorders(dl, origin);
        }

        // Distinct muted cage colors — dark base tinted with a unique hue per cage
        private static readonly Vector4[] CagePalette =
        {
            new(0.26f, 0.13f, 0.13f, 1f), // deep red
            new(0.12f, 0.24f, 0.14f, 1f), // deep green
            new(0.12f, 0.15f, 0.30f, 1f), // deep blue
            new(0.26f, 0.24f, 0.11f, 1f), // deep yellow
            new(0.22f, 0.10f, 0.27f, 1f), // deep purple
            new(0.10f, 0.22f, 0.27f, 1f), // deep teal
            new(0.28f, 0.17f, 0.10f, 1f), // deep orange
            new(0.24f, 0.12f, 0.21f, 1f), // deep rose
            new(0.14f, 0.26f, 0.14f, 1f), // deep lime
            new(0.10f, 0.22f, 0.30f, 1f), // deep cyan
            new(0.26f, 0.20f, 0.10f, 1f), // deep amber
            new(0.18f, 0.12f, 0.28f, 1f), // deep indigo
        };

        // ── Cell backgrounds ──────────────────────────────────────────────────────

        private void DrawCellBackgrounds(ImDrawListPtr dl, Vector2 origin, int hovRow, int hovCol)
        {
            // Number in the selected cell (0 = none)
            int selNum = (_game.SelectedRow >= 0 && _game.SelectedCol >= 0)
                ? _game.Board[_game.SelectedRow, _game.SelectedCol]
                : 0;

            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var p0 = new Vector2(origin.X + c * CellSize, origin.Y + r * CellSize);
                    var p1 = new Vector2(p0.X + CellSize, p0.Y + CellSize);

                    // Base layer: cage color
                    int cageIdx = _game.Puzzle.CageIndex[r, c];
                    var cageColor = CagePalette[_cageColors[cageIdx] % CagePalette.Length];
                    dl.AddRectFilled(p0, p1, ImGui.ColorConvertFloat4ToU32(cageColor));

                    // Overlay layer: state-driven highlight
                    if (_game.IsComplete)
                    {
                        dl.AddRectFilled(p0, p1, Col(0.10f, 0.50f, 0.15f, 0.55f));
                    }
                    else if (r == _game.SelectedRow && c == _game.SelectedCol)
                    {
                        dl.AddRectFilled(p0, p1, Col(0.40f, 0.65f, 1.00f, 0.45f));
                    }
                    else if (_game.Conflicts[r, c])
                    {
                        dl.AddRectFilled(p0, p1, Col(0.80f, 0.10f, 0.10f, 0.55f));
                    }
                    else if (selNum != 0 && _game.Board[r, c] == selNum)
                    {
                        // Same number as selected cell — highlight it
                        dl.AddRectFilled(p0, p1, Col(0.90f, 0.75f, 0.20f, 0.35f));
                    }
                    else if (r == _game.SelectedRow || c == _game.SelectedCol)
                    {
                        dl.AddRectFilled(p0, p1, Col(0.30f, 0.50f, 0.80f, 0.18f));
                    }
                    else if (r == hovRow && c == hovCol)
                    {
                        dl.AddRectFilled(p0, p1, Col(1f, 1f, 1f, 0.10f));
                    }
                }
            }
        }

        // ── Cage sum labels ───────────────────────────────────────────────────────

        private void DrawCageNumbers(ImDrawListPtr dl, Vector2 origin)
        {
            var font       = ImGui.GetFont();
            const float fs = 14f;
            uint color     = ImGui.ColorConvertFloat4ToU32(Theme.Gold);

            foreach (var cage in _game.Puzzle.Cages)
            {
                // Top-left cell: minimum row, then minimum col within that row
                int labelR = 9, labelC = 9;
                foreach (var (r, c) in cage.Cells)
                {
                    if (r < labelR || (r == labelR && c < labelC))
                    { labelR = r; labelC = c; }
                }

                var pos = new Vector2(
                    origin.X + labelC * CellSize + 3,
                    origin.Y + labelR * CellSize + 2);

                dl.AddText(font, fs, pos, color, cage.Sum.ToString());
            }
        }

        // ── Player numbers ────────────────────────────────────────────────────────

        private void DrawBoardNumbers(ImDrawListPtr dl, Vector2 origin)
        {
            var   font       = ImGui.GetFont();
            float defaultFs  = ImGui.GetFontSize();
            float fs         = defaultFs * 1.6f;   // noticeably larger than default
            float scale      = fs / defaultFs;
            const float topPad = 17f;              // leave room for cage label

            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int num = _game.Board[r, c];
                    if (num == 0) continue;

                    var str      = num.ToString();
                    // TextSize uses default font size — scale to match our larger fs
                    var textSize = ImGui.CalcTextSize(str) * scale;

                    // Center horizontally, vertically in the space below the cage label
                    var p = new Vector2(
                        origin.X + c * CellSize + (CellSize - textSize.X) / 2f,
                        origin.Y + r * CellSize + topPad + (CellSize - topPad - textSize.Y) / 2f);

                    uint color;
                    if (_game.IsComplete)
                        color = ImGui.ColorConvertFloat4ToU32(Theme.TextPrimary);
                    else if (_game.Conflicts[r, c])
                        color = ImGui.ColorConvertFloat4ToU32(Theme.Error);
                    else if (r == _game.SelectedRow && c == _game.SelectedCol)
                        color = ImGui.ColorConvertFloat4ToU32(Theme.Accent);
                    else
                        color = ImGui.ColorConvertFloat4ToU32(Theme.TextPrimary);

                    dl.AddText(font, fs, p, color, str);
                }
            }
        }

        // ── Notes (pencil marks) ─────────────────────────────────────────────────

        private void DrawNotes(ImDrawListPtr dl, Vector2 origin)
        {
            var font         = ImGui.GetFont();
            const float fs   = 9.5f;
            const float topPad = 17f; // same as board number top pad — stay below cage label

            float noteW = CellSize / 3f;
            float noteH = (CellSize - topPad) / 3f;

            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    // Only draw notes when the cell is empty
                    if (_game.Board[r, c] != 0) continue;

                    float cellX = origin.X + c * CellSize;
                    float cellY = origin.Y + r * CellSize;

                    for (int n = 0; n < 9; n++)
                    {
                        if (!_game.Notes[r, c, n]) continue;

                        int nRow = n / 3;
                        int nCol = n % 3;

                        // Centre the digit within its note sub-cell
                        string s      = (n + 1).ToString();
                        var    tSize  = ImGui.CalcTextSize(s) * (fs / ImGui.GetFontSize());
                        var    pos    = new Vector2(
                            cellX + nCol * noteW + (noteW - tSize.X) / 2f,
                            cellY + topPad - 2f + nRow * noteH + (noteH - tSize.Y) / 2f);

                        uint color = ImGui.ColorConvertFloat4ToU32(Theme.TextSecondary);
                        dl.AddText(font, fs, pos, color, s);
                    }
                }
            }
        }

        // ── Borders ───────────────────────────────────────────────────────────────

        private void DrawBorders(ImDrawListPtr dl, Vector2 origin)
        {
            uint colorThin  = Col(0.45f, 0.45f, 0.45f, 1f); // grey — internal cell lines
            uint colorCage  = Col(0.70f, 0.70f, 0.78f, 1f); // medium — cage boundaries
            uint colorBox   = Col(1.00f, 1.00f, 1.00f, 1f); // bright white — 3×3 box lines

            // Pass 1: thin lines between all adjacent cells (dim grid)
            for (int r = 0; r <= 9; r++)
            {
                float y = origin.Y + r * CellSize;
                for (int c = 0; c < 9; c++)
                    dl.AddLine(new Vector2(origin.X + c * CellSize, y),
                               new Vector2(origin.X + (c + 1) * CellSize, y),
                               colorThin, 1f);
            }
            for (int c = 0; c <= 9; c++)
            {
                float x = origin.X + c * CellSize;
                for (int r = 0; r < 9; r++)
                    dl.AddLine(new Vector2(x, origin.Y + r * CellSize),
                               new Vector2(x, origin.Y + (r + 1) * CellSize),
                               colorThin, 1f);
            }

            // Pass 2: cage boundary lines (drawn over thin grid)
            for (int r = 1; r < 9; r++)
            {
                float y = origin.Y + r * CellSize;
                for (int c = 0; c < 9; c++)
                {
                    if (_game.Puzzle.CageIndex[r - 1, c] != _game.Puzzle.CageIndex[r, c])
                        dl.AddLine(new Vector2(origin.X + c * CellSize, y),
                                   new Vector2(origin.X + (c + 1) * CellSize, y),
                                   colorCage, 1f);
                }
            }
            for (int c = 1; c < 9; c++)
            {
                float x = origin.X + c * CellSize;
                for (int r = 0; r < 9; r++)
                {
                    if (_game.Puzzle.CageIndex[r, c - 1] != _game.Puzzle.CageIndex[r, c])
                        dl.AddLine(new Vector2(x, origin.Y + r * CellSize),
                                   new Vector2(x, origin.Y + (r + 1) * CellSize),
                                   colorCage, 1f);
                }
            }

            // Pass 3: 3×3 box lines — thick bright white, drawn on top of everything
            foreach (int r in new[] { 0, 3, 6, 9 })
            {
                float y = origin.Y + r * CellSize;
                dl.AddLine(new Vector2(origin.X, y),
                           new Vector2(origin.X + GridSize, y),
                           colorBox, 5f);
            }
            foreach (int c in new[] { 0, 3, 6, 9 })
            {
                float x = origin.X + c * CellSize;
                dl.AddLine(new Vector2(x, origin.Y),
                           new Vector2(x, origin.Y + GridSize),
                           colorBox, 5f);
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private static uint Col(float r, float g, float b, float a)
            => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));
    }
}
