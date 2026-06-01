using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Modello 2. La statua grande (scala 10) è il Jammo riggato vero: tutte le parti
// attive, di partenza col materiale fantasma trasparente. "Rivelare" una parte
// = swap di sharedMaterial ghost -> solido su quel renderer. Nessuna mesh
// orfana, nessun allineamento da calcolare: la mesh riggata è già al suo posto.
public class StatueRig : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("Renderer della parte della statua grande (riggata). Parte col materiale fantasma, diventa solido a riempimento.")]
        public Renderer bigStatuePart;

        [Tooltip("Spuntare per i renderer degli occhi (es. head_eyes_low). Il rig userà 'Solid Material Eyes' invece del 'Solid Material' globale.")]
        public bool useEyesMaterial;

        [HideInInspector] public bool filled;
    }

    [Header("Parti della statua grande")]
    [SerializeField] private Slot[] slots;

    [Header("Materiali")]
    [Tooltip("Contorno trasparente iniziale (M_StatueGhost).")]
    [SerializeField] private Material ghostMaterial;
    [Tooltip("Materiale solido di default per il corpo della statua (m_jammo_metal). Usato per tutti gli slot non flaggati 'useEyesMaterial'.")]
    [SerializeField] private Material solidMaterial;
    [Tooltip("Materiale degli occhi (m_jammo_eyes). Usato per gli slot con 'Use Eyes Material' = true (es. head_eyes_low).")]
    [SerializeField] private Material solidMaterialEyes;

    [Header("Eventi")]
    [SerializeField] private UnityEvent onPartRevealed;
    [SerializeField] private UnityEvent onStatueComplete;

    private readonly List<int> _free = new List<int>();
    private int _totalSlots;

    public int RemainingCount => _free.Count;
    public bool IsComplete => _free.Count == 0;
    public int TotalSlots => _totalSlots;
    public int FilledCount => _totalSlots - _free.Count;
    public float Normalized => _totalSlots > 0 ? (float)FilledCount / _totalSlots : 0f;

    void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].bigStatuePart == null) continue;
            slots[i].filled = false;
            if (ghostMaterial != null) slots[i].bigStatuePart.sharedMaterial = ghostMaterial;
            _free.Add(i);
        }
        _totalSlots = _free.Count;
    }

    // Prende a caso uno slot libero e lo marca occupato. -1 se non ce ne sono.
    public int TakeRandomUnfilledSlot()
    {
        if (_free.Count == 0) return -1;

        int listIdx = Random.Range(0, _free.Count);
        int slotIdx = _free[listIdx];

        // swap-last: rimozione O(1), l'ordine non conta (scelta random).
        _free[listIdx] = _free[_free.Count - 1];
        _free.RemoveAt(_free.Count - 1);

        slots[slotIdx].filled = true;
        return slotIdx;
    }

    // Nome della parte di quello slot: chiave di correlazione col JammoPartSet
    // del prefab scala-1 (stesso GameObject.name).
    public string PartNameOf(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null || slots[index].bigStatuePart == null)
            return null;
        return slots[index].bigStatuePart.gameObject.name;
    }

    // Posizionamento avvenuto: la parte diventa solida.
    public void OnSlotFilled(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null) return;

        if (slots[index].bigStatuePart != null)
        {
            Material target = slots[index].useEyesMaterial ? solidMaterialEyes : solidMaterial;
            if (target != null) slots[index].bigStatuePart.sharedMaterial = target;
        }

        onPartRevealed.Invoke();
        if (_free.Count == 0) onStatueComplete.Invoke();
    }

    // Pezzo perso (Jammo colpito durante il trasporto): lo slot torna libero e
    // resta fantasma (OnSlotFilled non è stato chiamato → nessuno swap a solido).
    // La statua non progredisce: quel pezzo va riguadagnato con un altro kill.
    public void ReturnSlot(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null) return;
        if (!slots[index].filled) return; // già libero
        slots[index].filled = false;
        _free.Add(index);
    }
}
