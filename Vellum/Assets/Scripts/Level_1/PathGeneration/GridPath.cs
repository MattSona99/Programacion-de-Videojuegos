using System.Collections.Generic;
using UnityEngine;

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
