using System.Collections.Generic;
using UnityEngine;

/// <summary>Ordered sequence of grid cells (Vector2Int) representing a generated path. Used by the Act 1 path puzzle.</summary>
public class GridPath
{
    public readonly List<Vector2Int> Cells;

    public GridPath(int capacity = 0)
    {
        Cells = new List<Vector2Int>(capacity);
    }

    public int Count => Cells.Count;
    public Vector2Int First => Cells[0];
    public Vector2Int Last => Cells[Cells.Count - 1];

    public void Add(Vector2Int cell)
    {
        Cells.Add(cell);
    }

    public bool Contains(Vector2Int cell)
    {
        return Cells.Contains(cell);
    }

    /// <summary>Returns a new path with <paramref name="other"/> appended (optionally skipping its first, shared cell).</summary>
    public GridPath Concat(GridPath other, bool skipOtherFirst)
    {
        GridPath result = new GridPath(Cells.Count + other.Cells.Count);
        result.Cells.AddRange(Cells);
        int startIdx = skipOtherFirst ? 1 : 0;
        for (int i = startIdx; i < other.Cells.Count; i++)
        {
            result.Cells.Add(other.Cells[i]);
        }
        return result;
    }
}
