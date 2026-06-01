using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generator of 4-directional self-avoiding paths of EXACT length between two cells. It uses a
/// randomized backtracking search with Manhattan+parity pruning and the Warnsdorff heuristic:
/// it almost always finds a valid path within a few ms when one is feasible.
///
/// "Self-avoiding" = each cell used once → no crossings by construction. The aesthetic
/// selection is delegated to <see cref="FuzzyPathEvaluator"/>: several candidates are generated
/// and the one with the highest fuzzy score is kept.
/// </summary>
public class SelfAvoidingPathGenerator
{
    private static readonly Vector2Int[] _deltas =
    {
        new Vector2Int(0, 1),   // N
        new Vector2Int(0, -1),  // S
        new Vector2Int(1, 0),   // E
        new Vector2Int(-1, 0),  // W
    };

    // 8-neighborhood (orthogonal + diagonal) for the spacing check.
    private static readonly Vector2Int[] _deltas8 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    // Cap on expanded nodes per attempt: beyond this, restart from scratch with a new
    // random order (avoids very deep pathological branches).
    private const int NODE_BUDGET = 300000;
    // Restart attempts per requested candidate. The 8-dir spacing is a tight constraint:
    // a bit of restart margin is needed.
    private const int MAX_ATTEMPTS_PER_CANDIDATE = 150;

    private List<Vector2Int> _path;
    private HashSet<Vector2Int> _visited;
    private HashSet<Vector2Int> _allowed;
    private Vector2Int _target;
    private Vector2Int _mandatedPenult; // the penultimate tile is mandated (e.g. Tile_12_21)
    private int _penultIndex;           // 0-based index of the penultimate tile
    private int _totalCells;
    private int _nodeBudget;
    private System.Random _rng;

    // One direction-order buffer per depth level: prevents child recursive calls from
    // overwriting the parent's ordering and zeroes per-node allocations.
    private int[][] _dirBuffers;

    /// <summary>
    /// Generates the best of <paramref name="candidateCount"/> self-avoiding paths of exactly
    /// <paramref name="totalCells"/> cells from <paramref name="start"/> to <paramref name="target"/>,
    /// constrained to pass through <paramref name="mandatedPenult"/> as the penultimate tile and
    /// scored by <paramref name="fuzzy"/>. Throws if the constraints are infeasible.
    /// </summary>
    public GridPath Generate(
        Vector2Int start,
        Vector2Int target,
        Vector2Int mandatedPenult,
        int totalCells,
        HashSet<Vector2Int> allowed,
        RectInt bounds,
        FuzzyPathEvaluator fuzzy,
        System.Random rng,
        int candidateCount)
    {
        if (allowed == null || !allowed.Contains(start) || !allowed.Contains(target))
        {
            throw new System.InvalidOperationException(
                $"Start {start} or Target {target} do not match a PathTile of the grid.");
        }
        if (!allowed.Contains(mandatedPenult))
        {
            throw new System.InvalidOperationException(
                $"The mandated penultimate tile {mandatedPenult} does not match a PathTile of the grid.");
        }

        int manPenultTarget = Mathf.Abs(target.x - mandatedPenult.x) + Mathf.Abs(target.y - mandatedPenult.y);
        if (manPenultTarget != 1)
        {
            throw new System.InvalidOperationException(
                $"The mandated penultimate tile {mandatedPenult} must be (orthogonally) adjacent to the final tile {target}.");
        }

        int manhattan = Mathf.Abs(target.x - start.x) + Mathf.Abs(target.y - start.y);
        int moves = totalCells - 1;
        if (manhattan > moves)
        {
            throw new System.InvalidOperationException(
                $"Impossible path: Manhattan({start},{target})={manhattan} > available moves={moves}.");
        }
        if (((moves - manhattan) & 1) != 0)
        {
            throw new System.InvalidOperationException(
                $"Incompatible parity: moves={moves} and Manhattan={manhattan} must have the same parity.");
        }

        // Penultimate constraint: the path must reach mandatedPenult at index
        // totalCells-2 (penultIndex moves from the start).
        _penultIndex = totalCells - 2;
        int manStartPenult = Mathf.Abs(mandatedPenult.x - start.x) + Mathf.Abs(mandatedPenult.y - start.y);
        if (manStartPenult > _penultIndex || ((_penultIndex - manStartPenult) & 1) != 0)
        {
            throw new System.InvalidOperationException(
                $"Penultimate tile unreachable: Manhattan(Start,{mandatedPenult})={manStartPenult}, moves={_penultIndex}. " +
                $"Need value <= {_penultIndex} and same parity. Move start/end/penultimate.");
        }

        _allowed = allowed;
        _target = target;
        _mandatedPenult = mandatedPenult;
        _totalCells = totalCells;
        _rng = rng;

        _dirBuffers = new int[totalCells][];
        for (int i = 0; i < totalCells; i++) _dirBuffers[i] = new int[4];

        GridPath best = null;
        float bestScore = float.NegativeInfinity;

        for (int c = 0; c < Mathf.Max(1, candidateCount); c++)
        {
            GridPath candidate = SearchOne(start);
            if (candidate == null) continue;

            float score = fuzzy != null ? fuzzy.Score(candidate, bounds) : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            throw new System.InvalidOperationException(
                $"Backtracking produced no valid path of {totalCells} cells from {start} to {target} " +
                $"(attempts per candidate={MAX_ATTEMPTS_PER_CANDIDATE}). Check that the tile grid is continuous between start and end.");
        }
        return best;
    }

    // One candidate = one backtracking search; if the budget is exhausted it restarts
    // with a new random order (the shared rng guarantees variety).
    private GridPath SearchOne(Vector2Int start)
    {
        for (int attempt = 0; attempt < MAX_ATTEMPTS_PER_CANDIDATE; attempt++)
        {
            _path = new List<Vector2Int>(_totalCells) { start };
            _visited = new HashSet<Vector2Int> { start };
            _nodeBudget = NODE_BUDGET;

            if (Recurse(start))
            {
                GridPath result = new GridPath(_path.Count);
                for (int i = 0; i < _path.Count; i++) result.Add(_path[i]);
                return result;
            }
        }
        return null;
    }

    private bool Recurse(Vector2Int current)
    {
        if (_path.Count == _totalCells)
        {
            return current == _target;
        }
        if (--_nodeBudget <= 0) return false;

        // This level's own order buffer: child calls use a different buffer, so the
        // parent's iteration is not corrupted.
        int[] order = _dirBuffers[_path.Count - 1];
        order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;

        // Direction order: random shuffle + Warnsdorff tie-break (fewest free exits
        // first) → fast backtracking and always-varied paths.
        Shuffle(order);
        // Stable selection sort over 4 elements by the neighbor's degree of freedom.
        for (int a = 0; a < 4; a++)
        {
            int minDeg = int.MaxValue;
            int pick = a;
            for (int b = a; b < 4; b++)
            {
                Vector2Int n = current + _deltas[order[b]];
                int deg = IsCandidate(n) ? FreeDegree(n) : int.MaxValue;
                if (deg < minDeg)
                {
                    minDeg = deg;
                    pick = b;
                }
            }
            int tmp = order[a]; order[a] = order[pick]; order[pick] = tmp;
        }

        int movesRemaining = _totalCells - _path.Count; // cells still to add
        for (int d = 0; d < 4; d++)
        {
            Vector2Int next = current + _deltas[order[d]];
            if (!IsCandidate(next)) continue;

            // Pruning: from "next", (movesRemaining-1) moves are needed to close.
            int dist = Mathf.Abs(_target.x - next.x) + Mathf.Abs(_target.y - next.y);
            int budget = movesRemaining - 1;
            if (dist > budget || ((budget - dist) & 1) != 0) continue;
            // The target can only be touched on the last step (otherwise it would be
            // revisited, violating self-avoidance).
            if (next == _target && _path.Count + 1 != _totalCells) continue;
            // Spacing (diagonal too): the only contacts allowed for "next" are the
            // predecessor (current) and the pre-predecessor (the natural corner of an
            // L turn). Any other path tile in its 8-neighborhood = two arms touching → forbidden.
            Vector2Int prevPrev = _path.Count >= 2 ? _path[_path.Count - 2] : current;
            if (!IsSpaced(next, current, prevPrev)) continue;

            // Mandated penultimate: the fixed tile (e.g. Tile_12_21) may appear ONLY at the
            // penultimate index, and that index MUST be it. This keeps the arrival direction
            // onto the final tile always the same.
            int nextIndex = _path.Count; // 0-based index that "next" will have
            if (next == _mandatedPenult && nextIndex != _penultIndex) continue;
            if (nextIndex == _penultIndex && next != _mandatedPenult) continue;

            // Anti-"diagonal": two consecutive turns are forbidden → between one curve and
            // the next there's always at least one straight stretch, no staircases.
            if (_path.Count >= 3)
            {
                Vector2Int dirInCur = current - _path[_path.Count - 2];
                Vector2Int dirInPrev = _path[_path.Count - 2] - _path[_path.Count - 3];
                Vector2Int moveDir = next - current;
                bool turnNow = moveDir != dirInCur;
                bool prevWasTurn = dirInCur != dirInPrev;
                if (turnNow && prevWasTurn) continue;
            }

            _path.Add(next);
            _visited.Add(next);

            if (Recurse(next)) return true;

            _visited.Remove(next);
            _path.RemoveAt(_path.Count - 1);
            if (_nodeBudget <= 0) return false;
        }
        return false;
    }

    private bool IsCandidate(Vector2Int cell)
    {
        return _allowed.Contains(cell) && !_visited.Contains(cell);
    }

    // true if, in "cell"'s 8-neighborhood, the only path tiles are the predecessor and
    // the pre-predecessor (the curve's corner). This keeps two parallel arms separated by
    // at least one tile, even diagonally.
    private bool IsSpaced(Vector2Int cell, Vector2Int predecessor, Vector2Int prevPrev)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2Int nb = cell + _deltas8[i];
            if (nb == predecessor || nb == prevPrev) continue;
            if (_visited.Contains(nb)) return false;
        }
        return true;
    }

    private int FreeDegree(Vector2Int cell)
    {
        int deg = 0;
        for (int i = 0; i < 4; i++)
        {
            if (IsCandidate(cell + _deltas[i])) deg++;
        }
        return deg;
    }

    private void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(0, i + 1);
            int tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
        }
    }
}
