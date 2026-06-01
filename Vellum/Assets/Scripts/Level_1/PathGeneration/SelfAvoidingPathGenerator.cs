using System.Collections.Generic;
using UnityEngine;

// Generatore di percorsi 4-direzionali self-avoiding di lunghezza ESATTA tra due
// celle. A differenza dell'approccio GA (rimosso), qui si usa un backtracking
// randomizzato con potatura Manhattan+parità ed euristica di Warnsdorff: trova
// quasi sempre un percorso valido in pochi ms quando è fattibile.
//
// "Self-avoiding" = ogni cella usata una sola volta → niente incroci per
// costruzione. La selezione estetica resta affidata a FuzzyPathEvaluator:
// si generano più candidati e si tiene quello col punteggio fuzzy più alto.
public class SelfAvoidingPathGenerator
{
    private static readonly Vector2Int[] _deltas =
    {
        new Vector2Int(0, 1),   // N
        new Vector2Int(0, -1),  // S
        new Vector2Int(1, 0),   // E
        new Vector2Int(-1, 0),  // W
    };

    // 8-vicinato (ortogonali + diagonali) per il controllo di spaziatura.
    private static readonly Vector2Int[] _deltas8 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    // Tetto di nodi espansi per singolo tentativo: oltre questo si riparte da
    // zero con un nuovo ordine random (evita rami patologici molto profondi).
    private const int NODE_BUDGET = 300000;
    // Tentativi di restart per ogni candidato richiesto. La spaziatura 8-dir è
    // un vincolo stretto: serve un po' di margine di restart.
    private const int MAX_ATTEMPTS_PER_CANDIDATE = 150;

    private List<Vector2Int> _path;
    private HashSet<Vector2Int> _visited;
    private HashSet<Vector2Int> _allowed;
    private Vector2Int _target;
    private Vector2Int _mandatedPenult; // la penultima tile è obbligata (es. Tile_12_21)
    private int _penultIndex;           // indice 0-based della penultima tile
    private int _totalCells;
    private int _nodeBudget;
    private System.Random _rng;

    // Un buffer d'ordine direzioni per livello di profondità: evita che le
    // chiamate ricorsive figlie sovrascrivano l'ordinamento del padre e azzera
    // le allocazioni per-nodo.
    private int[][] _dirBuffers;

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
                $"Start {start} o Target {target} non corrispondono a una PathTile della griglia.");
        }
        if (!allowed.Contains(mandatedPenult))
        {
            throw new System.InvalidOperationException(
                $"La penultima tile obbligata {mandatedPenult} non corrisponde a una PathTile della griglia.");
        }

        int manPenultTarget = Mathf.Abs(target.x - mandatedPenult.x) + Mathf.Abs(target.y - mandatedPenult.y);
        if (manPenultTarget != 1)
        {
            throw new System.InvalidOperationException(
                $"La penultima tile obbligata {mandatedPenult} deve essere adiacente (ortogonale) alla tile finale {target}.");
        }

        int manhattan = Mathf.Abs(target.x - start.x) + Mathf.Abs(target.y - start.y);
        int moves = totalCells - 1;
        if (manhattan > moves)
        {
            throw new System.InvalidOperationException(
                $"Percorso impossibile: Manhattan({start},{target})={manhattan} > mosse disponibili={moves}.");
        }
        if (((moves - manhattan) & 1) != 0)
        {
            throw new System.InvalidOperationException(
                $"Parità incompatibile: mosse={moves} e Manhattan={manhattan} devono avere la stessa parità.");
        }

        // Vincolo penultima: il percorso deve raggiungere mandatedPenult all'indice
        // totalCells-2 (penultIndex mosse dallo start).
        _penultIndex = totalCells - 2;
        int manStartPenult = Mathf.Abs(mandatedPenult.x - start.x) + Mathf.Abs(mandatedPenult.y - start.y);
        if (manStartPenult > _penultIndex || ((_penultIndex - manStartPenult) & 1) != 0)
        {
            throw new System.InvalidOperationException(
                $"Penultima tile irraggiungibile: Manhattan(Start,{mandatedPenult})={manStartPenult}, mosse={_penultIndex}. " +
                $"Servono valore <= {_penultIndex} e stessa parità. Sposta start/end/penultima.");
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
                $"Backtracking non ha prodotto nessun percorso valido di {totalCells} celle da {start} a {target} " +
                $"(tentativi per candidato={MAX_ATTEMPTS_PER_CANDIDATE}). Verifica che la griglia di tile sia continua tra start ed end.");
        }
        return best;
    }

    // Un candidato = una ricerca con backtracking; in caso di budget esaurito
    // riparte con un nuovo ordine random (il rng condiviso garantisce varietà).
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

        // Buffer d'ordine proprio di questo livello: le chiamate figlie usano
        // un buffer diverso, quindi l'iterazione del padre non viene corrotta.
        int[] order = _dirBuffers[_path.Count - 1];
        order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;

        // Ordine direzioni: shuffle random + tie-break Warnsdorff (meno uscite
        // libere prima) → backtracking rapido e percorsi sempre diversi.
        Shuffle(order);
        // Selection sort stabile su 4 elementi per grado di libertà del vicino.
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

        int movesRemaining = _totalCells - _path.Count; // celle ancora da aggiungere
        for (int d = 0; d < 4; d++)
        {
            Vector2Int next = current + _deltas[order[d]];
            if (!IsCandidate(next)) continue;

            // Potatura: dal "next" servono (movesRemaining-1) mosse per chiudere.
            int dist = Mathf.Abs(_target.x - next.x) + Mathf.Abs(_target.y - next.y);
            int budget = movesRemaining - 1;
            if (dist > budget || ((budget - dist) & 1) != 0) continue;
            // Il target si può toccare solo all'ultimo passo (altrimenti andrebbe
            // rivisitato, violando il self-avoiding).
            if (next == _target && _path.Count + 1 != _totalCells) continue;
            // Spaziatura (anche diagonale): gli unici contatti ammessi per "next"
            // sono il predecessore (current) e il pre-predecessore (l'angolo
            // naturale di una curva a L). Qualsiasi altra tile del percorso nel
            // suo 8-vicinato = due bracci che si toccano → vietato.
            Vector2Int prevPrev = _path.Count >= 2 ? _path[_path.Count - 2] : current;
            if (!IsSpaced(next, current, prevPrev)) continue;

            // Penultima obbligata: la tile imposta (es. Tile_12_21) può comparire
            // SOLO all'indice penultimo, e quell'indice DEVE essere proprio lei.
            // Così la direzione d'arrivo sulla tile finale è sempre la stessa.
            int nextIndex = _path.Count; // indice 0-based che avrà "next"
            if (next == _mandatedPenult && nextIndex != _penultIndex) continue;
            if (nextIndex == _penultIndex && next != _mandatedPenult) continue;

            // Anti-"diagonale": vietate due svolte consecutive → tra una curva e
            // l'altra c'è sempre almeno un tratto dritto, niente scalinate.
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

    // true se nel 8-vicinato di "cell" le uniche tile del percorso sono il
    // predecessore e il pre-predecessore (angolo della curva). Così due bracci
    // paralleli restano separati da almeno una tile anche in diagonale.
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
