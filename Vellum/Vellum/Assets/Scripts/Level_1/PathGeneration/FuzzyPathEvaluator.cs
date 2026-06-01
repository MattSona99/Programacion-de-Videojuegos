using UnityEngine;

// Valutatore Fuzzy (Mamdani semplificato) della qualità estetica di un percorso.
// Input fuzzificati: turnDensity, edgeProximity, spread.
// Output: score in [0,1] tramite centroide su [0,1] discretizzato.
// Le regole sono pensate per premiare percorsi "bilanciati e spaziosi" e penalizzare
// quelli troppo dritti, troppo a zig-zag o appiccicati ai bordi.
public class FuzzyPathEvaluator
{
    private const int DefuzzSteps = 20;

    public float Score(GridPath path, RectInt bounds)
    {
        if (path == null || path.Count < 2) return 0f;

        float turnDensity = ComputeTurnDensity(path);
        float edgeProximity = ComputeEdgeProximity(path, bounds);
        float spread = ComputeSpread(path, bounds);

        float straight = Triangular(turnDensity, 0f, 0f, 0.25f);
        float balanced = Triangular(turnDensity, 0.15f, 0.40f, 0.65f);
        float wiggly = Triangular(turnDensity, 0.55f, 1f, 1f);

        float far = Triangular(edgeProximity, 0f, 0f, 0.5f);
        float near = Triangular(edgeProximity, 0.4f, 1f, 1f);

        float cramped = Triangular(spread, 0f, 0f, 0.5f);
        float spacious = Triangular(spread, 0.4f, 1f, 1f);

        // Rule base. Activation = min(antecedenti). Consequent = etichetta linguistica del set di output.
        float r1 = Mathf.Min(balanced, spacious);              // HIGH
        float r2 = straight;                                    // LOW
        float r3 = Mathf.Min(wiggly, near);                     // LOW
        float r4 = Mathf.Min(far, spacious);                    // HIGH
        float r5 = Mathf.Min(balanced, far);                    // HIGH
        float r6 = cramped;                                     // LOW
        float r7 = Mathf.Min(wiggly, spacious);                 // MEDIUM

        float highActivation = Mathf.Max(r1, Mathf.Max(r4, r5));
        float mediumActivation = r7;
        float lowActivation = Mathf.Max(r2, Mathf.Max(r3, r6));

        return Defuzzify(lowActivation, mediumActivation, highActivation);
    }

    private static float Triangular(float x, float a, float b, float c)
    {
        if (x <= a || x >= c) return 0f;
        if (x == b) return 1f;
        if (x < b) return (x - a) / (b - a);
        return (c - x) / (c - b);
    }

    private static float OutputLow(float x) => Triangular(x, 0f, 0f, 0.4f);
    private static float OutputMedium(float x) => Triangular(x, 0.3f, 0.5f, 0.7f);
    private static float OutputHigh(float x) => Triangular(x, 0.6f, 1f, 1f);

    private static float Defuzzify(float lowAct, float medAct, float highAct)
    {
        float num = 0f;
        float den = 0f;
        for (int i = 0; i <= DefuzzSteps; i++)
        {
            float x = i / (float)DefuzzSteps;
            float muLow = Mathf.Min(lowAct, OutputLow(x));
            float muMed = Mathf.Min(medAct, OutputMedium(x));
            float muHigh = Mathf.Min(highAct, OutputHigh(x));
            float agg = Mathf.Max(muLow, Mathf.Max(muMed, muHigh));
            num += x * agg;
            den += agg;
        }
        return den > 0f ? num / den : 0f;
    }

    private static float ComputeTurnDensity(GridPath path)
    {
        int turns = 0;
        for (int i = 2; i < path.Cells.Count; i++)
        {
            Vector2Int d1 = path.Cells[i - 1] - path.Cells[i - 2];
            Vector2Int d2 = path.Cells[i] - path.Cells[i - 1];
            if (d1 != d2) turns++;
        }
        int moves = path.Cells.Count - 1;
        return moves > 1 ? turns / (float)(moves - 1) : 0f;
    }

    private static float ComputeEdgeProximity(GridPath path, RectInt bounds)
    {
        float halfW = bounds.width * 0.5f;
        float halfH = bounds.height * 0.5f;
        float normalizer = Mathf.Max(1f, Mathf.Min(halfW, halfH));

        float sumNorm = 0f;
        foreach (Vector2Int c in path.Cells)
        {
            int dxMin = Mathf.Min(c.x - bounds.xMin, bounds.xMax - 1 - c.x);
            int dyMin = Mathf.Min(c.y - bounds.yMin, bounds.yMax - 1 - c.y);
            int chebyshev = Mathf.Min(dxMin, dyMin);
            sumNorm += Mathf.Clamp01(chebyshev / normalizer);
        }
        float avgDistFromEdge = sumNorm / path.Cells.Count;
        return Mathf.Clamp01(1f - avgDistFromEdge);
    }

    private static float ComputeSpread(GridPath path, RectInt bounds)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int c in path.Cells)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }
        int bboxArea = (maxX - minX + 1) * (maxY - minY + 1);
        float ratio = bboxArea / (float)path.Cells.Count;
        // ratio = 1 → perfettamente impacchettato (path "denso"); ratio alto → path "spazioso".
        // Mappiamo ratio in [0,1] con saturazione a 4 (un quarto delle celle del bbox occupate).
        return Mathf.Clamp01((ratio - 1f) / 3f);
    }
}
