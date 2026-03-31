using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ToshiBox.KillerSudoku
{
    public class Cage
    {
        public List<(int Row, int Col)> Cells { get; } = new();
        public int Sum { get; set; }
    }

    public class KillerSudokuPuzzle
    {
        public int[,] Solution  { get; } = new int[9, 9];
        public List<Cage> Cages { get; } = new();
        public int[,] CageIndex { get; } = new int[9, 9];
    }

    public enum ValidationResult { Correct, Conflicts, Incomplete }

    public class KillerSudokuGame
    {
        public KillerSudokuPuzzle Puzzle   { get; private set; }
        public int[,] Board                { get; private set; } = new int[9, 9];
        public bool[,] Conflicts           { get; private set; } = new bool[9, 9];
        // Notes[row, col, digit-1] — candidate pencil marks per cell
        public bool[,,] Notes              { get; private set; } = new bool[9, 9, 9];
        public bool NotesMode              { get; set; }
        public int SelectedRow             { get; set; } = -1;
        public int SelectedCol             { get; set; } = -1;
        public bool IsComplete             { get; private set; }
        public int Mistakes                { get; private set; }
        public TimeSpan Elapsed            => _timer.Elapsed;

        private readonly Stopwatch _timer = new();

        public KillerSudokuGame()
        {
            Puzzle = KillerSudokuGenerator.Generate();
            _timer.Start();
        }

        public void NewPuzzle()
        {
            Puzzle      = KillerSudokuGenerator.Generate();
            Board       = new int[9, 9];
            Conflicts   = new bool[9, 9];
            Notes       = new bool[9, 9, 9];
            SelectedRow = SelectedCol = -1;
            IsComplete  = false;
            NotesMode   = false;
            Mistakes    = 0;
            _timer.Restart();
        }

        public void SetNumber(int row, int col, int number)
        {
            // Count as a mistake if placing a wrong (non-zero) number
            if (number != 0 && number != Puzzle.Solution[row, col])
                Mistakes++;

            Board[row, col] = number;

            if (number != 0)
            {
                // Clear all notes in this cell
                for (int i = 0; i < 9; i++) Notes[row, col, i] = false;

                // Remove this number's note from every peer (same row, col, 3×3 box)
                int noteIdx = number - 1;
                int boxR    = row / 3 * 3;
                int boxC    = col / 3 * 3;

                for (int i = 0; i < 9; i++)
                {
                    Notes[row, i,    noteIdx] = false; // row
                    Notes[i,    col, noteIdx] = false; // col
                }
                for (int r = boxR; r < boxR + 3; r++)
                    for (int c = boxC; c < boxC + 3; c++)
                        Notes[r, c, noteIdx] = false;  // box
            }

            IsComplete = false;
            UpdateConflicts();
        }

        public void ToggleNote(int row, int col, int number)
        {
            if (number < 1 || number > 9) return;
            Notes[row, col, number - 1] = !Notes[row, col, number - 1];
        }

        public void ClearSelected()
        {
            if (SelectedRow >= 0 && SelectedCol >= 0)
                SetNumber(SelectedRow, SelectedCol, 0);
        }

        public ValidationResult Validate()
        {
            UpdateConflicts();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (Conflicts[r, c]) return ValidationResult.Conflicts;

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (Board[r, c] != Puzzle.Solution[r, c]) return ValidationResult.Incomplete;

            IsComplete = true;
            _timer.Stop();
            return ValidationResult.Correct;
        }

        private void UpdateConflicts()
        {
            Conflicts = new bool[9, 9];

            // Any filled cell that doesn't match the solution is immediately an error
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (Board[r, c] != 0 && Board[r, c] != Puzzle.Solution[r, c])
                        Conflicts[r, c] = true;

            for (int i = 0; i < 9; i++)
            {
                CheckGroup(Row(i));
                CheckGroup(Col(i));
                CheckGroup(Box(i));
            }

            foreach (var cage in Puzzle.Cages)
            {
                int sum       = 0;
                bool allFilled = true;
                foreach (var (r, c) in cage.Cells)
                {
                    if (Board[r, c] == 0) { allFilled = false; break; }
                    sum += Board[r, c];
                }
                if (allFilled && sum != cage.Sum)
                    foreach (var (r, c) in cage.Cells)
                        Conflicts[r, c] = true;
            }
        }

        private void CheckGroup(List<(int, int)> cells)
        {
            var seen = new Dictionary<int, (int, int)>();
            foreach (var (r, c) in cells)
            {
                int val = Board[r, c];
                if (val == 0) continue;
                if (seen.TryGetValue(val, out var prev))
                {
                    Conflicts[r, c]            = true;
                    Conflicts[prev.Item1, prev.Item2] = true;
                }
                else seen[val] = (r, c);
            }
        }

        private static List<(int, int)> Row(int r) { var l = new List<(int,int)>(); for (int c=0;c<9;c++) l.Add((r,c)); return l; }
        private static List<(int, int)> Col(int c) { var l = new List<(int,int)>(); for (int r=0;r<9;r++) l.Add((r,c)); return l; }
        private static List<(int, int)> Box(int b)
        {
            var l  = new List<(int,int)>();
            int br = b / 3 * 3, bc = b % 3 * 3;
            for (int r = br; r < br + 3; r++)
                for (int c = bc; c < bc + 3; c++)
                    l.Add((r, c));
            return l;
        }
    }

    public static class KillerSudokuGenerator
    {
        private static readonly Random Rng = new();

        public static KillerSudokuPuzzle Generate()
        {
            var puzzle = new KillerSudokuPuzzle();
            FillBoard(puzzle.Solution);
            BuildCages(puzzle);
            return puzzle;
        }

        // ── Sudoku solver ─────────────────────────────────────────────────────────

        private static void FillBoard(int[,] board)
        {
            // Fill the three diagonal 3x3 boxes independently (no conflicts possible)
            for (int b = 0; b < 3; b++)
                FillBox(board, b * 3, b * 3);
            Solve(board);
        }

        private static void FillBox(int[,] board, int row, int col)
        {
            var nums = Shuffle(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            int k = 0;
            for (int r = row; r < row + 3; r++)
                for (int c = col; c < col + 3; c++)
                    board[r, c] = nums[k++];
        }

        private static bool Solve(int[,] board)
        {
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    if (board[r, c] != 0) continue;
                    foreach (var n in Shuffle(new[] { 1,2,3,4,5,6,7,8,9 }))
                    {
                        if (IsPlaceable(board, r, c, n))
                        {
                            board[r, c] = n;
                            if (Solve(board)) return true;
                            board[r, c] = 0;
                        }
                    }
                    return false;
                }
            }
            return true;
        }

        private static bool IsPlaceable(int[,] board, int row, int col, int num)
        {
            for (int i = 0; i < 9; i++)
                if (board[row, i] == num || board[i, col] == num) return false;
            int br = row / 3 * 3, bc = col / 3 * 3;
            for (int r = br; r < br + 3; r++)
                for (int c = bc; c < bc + 3; c++)
                    if (board[r, c] == num) return false;
            return true;
        }

        // ── Cage builder ──────────────────────────────────────────────────────────

        private static void BuildCages(KillerSudokuPuzzle puzzle)
        {
            var assigned = new bool[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    puzzle.CageIndex[r, c] = -1;

            // Visit cells in random order so cage shapes are varied
            var order = new List<(int, int)>();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    order.Add((r, c));
            ShuffleList(order);

            int cageIdx = 0;
            foreach (var (startR, startC) in order)
            {
                if (assigned[startR, startC]) continue;

                var cells   = new List<(int, int)> { (startR, startC) };
                assigned[startR, startC] = true;

                // 40% → 2 cells, 40% → 3 cells, 20% → 4 cells; no singles, no 5s
                int maxSize = Rng.Next(10) switch { < 4 => 2, < 8 => 3, _ => 4 };
                while (cells.Count < maxSize)
                {
                    var (r, c)   = cells[Rng.Next(cells.Count)];
                    var neighbors = FreeNeighbors(assigned, r, c);
                    if (neighbors.Count == 0) break;
                    var next = neighbors[Rng.Next(neighbors.Count)];
                    cells.Add(next);
                    assigned[next.Item1, next.Item2] = true;
                }

                var cage = new Cage();
                foreach (var (r, c) in cells)
                {
                    cage.Cells.Add((r, c));
                    cage.Sum                   += puzzle.Solution[r, c];
                    puzzle.CageIndex[r, c]      = cageIdx;
                }
                puzzle.Cages.Add(cage);
                cageIdx++;
            }

            MergeSingleCells(puzzle);
        }

        // Merge excess single-cell cages, keeping exactly 3.
        private static void MergeSingleCells(KillerSudokuPuzzle puzzle)
        {
            const int KeepSingles = 3;

            int[] dr = { -1, 1, 0, 0 };
            int[] dc = {  0, 0,-1, 1 };

            // Collect indices of all single-cell cages, shuffled so we keep random ones
            var singles = new List<int>();
            for (int i = 0; i < puzzle.Cages.Count; i++)
                if (puzzle.Cages[i].Cells.Count == 1) singles.Add(i);
            ShuffleList(singles);

            // Merge all but the first KeepSingles
            for (int s = KeepSingles; s < singles.Count; s++)
            {
                int i = singles[s];
                var (r, c) = puzzle.Cages[i].Cells[0];
                int target = -1;
                for (int d = 0; d < 4; d++)
                {
                    int nr = r + dr[d], nc = c + dc[d];
                    if (nr < 0 || nr > 8 || nc < 0 || nc > 8) continue;
                    int adj = puzzle.CageIndex[nr, nc];
                    if (adj != i) { target = adj; break; }
                }
                if (target < 0) continue;

                puzzle.Cages[target].Cells.Add((r, c));
                puzzle.Cages[target].Sum += puzzle.Solution[r, c];
                puzzle.CageIndex[r, c]   = target;
                puzzle.Cages[i].Cells.Clear();
                puzzle.Cages[i].Sum = 0;
            }

            // Compact: remove empty cages and reindex
            var newCages  = new List<Cage>();
            var oldToNew  = new int[puzzle.Cages.Count];
            for (int i = 0; i < puzzle.Cages.Count; i++)
            {
                if (puzzle.Cages[i].Cells.Count == 0) { oldToNew[i] = -1; continue; }
                oldToNew[i] = newCages.Count;
                newCages.Add(puzzle.Cages[i]);
            }
            puzzle.Cages.Clear();
            puzzle.Cages.AddRange(newCages);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    puzzle.CageIndex[r, c] = oldToNew[puzzle.CageIndex[r, c]];
        }

        private static List<(int, int)> FreeNeighbors(bool[,] assigned, int r, int c)
        {
            var result = new List<(int, int)>();
            int[] dr   = { -1, 1,  0, 0 };
            int[] dc   = {  0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nr = r + dr[i], nc = c + dc[i];
                if (nr >= 0 && nr < 9 && nc >= 0 && nc < 9 && !assigned[nr, nc])
                    result.Add((nr, nc));
            }
            return result;
        }

        // ── Utilities ────────────────────────────────────────────────────────────

        private static T[] Shuffle<T>(T[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}