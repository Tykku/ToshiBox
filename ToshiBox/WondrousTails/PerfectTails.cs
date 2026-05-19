using System;
using System.Collections.Generic;
using System.Linq;

namespace ToshiBox.WondrousTails;

public sealed partial class PerfectTails
{
    private static readonly Random Random = new();
    private readonly Dictionary<int, long[]> possibleBoards = [];
    private readonly Dictionary<int, double[]> sampleProbabilities = [];

    public readonly bool[] GameState = new bool[16];

    public PerfectTails()
    {
        CalculateBoards(0, 0, 0, 0, 0);
        CalculateSamples();
    }

    private static double[] Error { get; } = [-1, -1, -1];

    public double[] Solve(bool[] cells)
    {
        var counts = Values(cells);
        if (counts == null) return Error;

        var divisor = (double)counts[0];
        return counts.Skip(1).Select(c => Math.Round(c / divisor, 4)).ToArray();
    }

    public double[] GetSample(int stickersPlaced)
        => sampleProbabilities.GetValueOrDefault(stickersPlaced, Error);

    private long[]? Values(bool[] cells)
        => possibleBoards.GetValueOrDefault(CellsToMask(cells));

    private long[] CalculateBoards(int mask, int numStickers, int numRows, int numCols, int numDiags)
    {
        if (possibleBoards.TryGetValue(mask, out var result)) return result;

        if (numStickers == 9)
        {
            var lines = numRows + numCols + numDiags;
            return possibleBoards[mask] =
            [
                1,
                lines >= 1 ? 1 : 0,
                lines >= 2 ? 1 : 0,
                lines >= 3 ? 1 : 0,
            ];
        }

        if (numStickers > 9)
            return possibleBoards[mask] = [0, 0, 0, 0];

        result = possibleBoards[mask] = [0, 0, 0, 0];

        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                if (MaskHasBit(mask, r, c)) continue;

                var nMask  = SetMaskBit(mask, r, c);
                var nRows  = MaskHasRow(nMask, r) ? 1 : 0;
                var nCols  = MaskHasCol(nMask, c) ? 1 : 0;
                var nDiag1 = MaskHasDiag1(nMask) && r == c     ? 1 : 0;
                var nDiag2 = MaskHasDiag2(nMask) && r == 3 - c ? 1 : 0;

                var nResult = CalculateBoards(nMask, numStickers + 1, numRows + nRows, numCols + nCols, numDiags + nDiag1 + nDiag2);
                for (var i = 0; i < 4; i++)
                    result[i] += nResult[i];
            }
        }

        return result;
    }

    private void CalculateSamples()
    {
        for (var stickersPlaced = 1; stickersPlaced <= 7; stickersPlaced++)
        {
            var samples = new List<double[]>();
            for (var i = 0; i < 500; i++)
            {
                var sampleState   = new bool[16];
                var sampleIndexes = Enumerable.Range(0, 16).OrderBy(_ => Random.Next()).Take(stickersPlaced);
                foreach (var idx in sampleIndexes) sampleState[idx] = true;
                samples.Add(Solve(sampleState));
            }

            sampleProbabilities[stickersPlaced] =
            [
                Math.Round(samples.Average(s => s[0]), 4),
                Math.Round(samples.Average(s => s[1]), 4),
                Math.Round(samples.Average(s => s[2]), 4),
            ];
        }
    }
}

public sealed partial class PerfectTails
{
    private static int CellsToMask(bool[] cells)
    {
        var mask = 0;
        for (var r = 0; r < 4; r++)
            for (var c = 0; c < 4; c++)
                if (cells[r * 4 + c]) mask = SetMaskBit(mask, r, c);
        return mask;
    }

    private static int GetMaskBit(int r, int c) => 1 << (4 * r + c);
    private static int  SetMaskBit(int mask, int r, int c) => mask | GetMaskBit(r, c);
    private static bool MaskHasBit(int mask, int r, int c) => (mask & GetMaskBit(r, c)) == GetMaskBit(r, c);
    private static bool MaskHasRow(int mask, int r)  => Enumerable.Range(0, 4).All(c => MaskHasBit(mask, r, c));
    private static bool MaskHasCol(int mask, int c)  => Enumerable.Range(0, 4).All(r => MaskHasBit(mask, r, c));
    private static bool MaskHasDiag1(int mask)       => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, i));
    private static bool MaskHasDiag2(int mask)       => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, 3 - i));
}
