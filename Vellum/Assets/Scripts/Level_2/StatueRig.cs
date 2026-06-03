using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The big statue (scale 10) is the actual rigged Jammo: all parts active, starting with the
/// transparent ghost material. "Revealing" a part = swapping that renderer's sharedMaterial from
/// ghost to solid. No orphan mesh, no alignment to compute: the rigged mesh is already in place.
/// </summary>
public class StatueRig : MonoBehaviour
{
    /// <summary>One assemblable part of the statue: its renderer, an eyes-material flag, and filled state.</summary>
    [System.Serializable]
    public class Slot
    {
        [Tooltip("Renderer of the big (rigged) statue part. Starts with the ghost material, becomes solid when filled.")]
        public Renderer bigStatuePart;

        [Tooltip("Tick for the eye renderers (e.g. head_eyes_low). The rig will use 'Solid Material Eyes' instead of the global 'Solid Material'.")]
        public bool useEyesMaterial;

        [HideInInspector] public bool filled;
    }

    [Header("Big statue parts")]
    [SerializeField] private Slot[] slots;

    [Header("Materials")]
    [Tooltip("Initial transparent outline (M_StatueGhost).")]
    [SerializeField] private Material ghostMaterial;
    [Tooltip("Default solid material for the statue body (m_jammo_metal). Used for all slots not flagged 'useEyesMaterial'.")]
    [SerializeField] private Material solidMaterial;
    [Tooltip("Eyes material (m_jammo_eyes). Used for slots with 'Use Eyes Material' = true (e.g. head_eyes_low).")]
    [SerializeField] private Material solidMaterialEyes;

    [Header("Eventi")]
    [SerializeField] private UnityEvent onPartRevealed;
    [SerializeField] private UnityEvent onStatueComplete;

    /// <summary>Fired when a part is placed (scoring). Code-friendly mirror of <see cref="onPartRevealed"/>.</summary>
    public event System.Action PartRevealed;

    /// <summary>Fired when the statue is completed (scoring). Code-friendly mirror of <see cref="onStatueComplete"/>.</summary>
    public event System.Action StatueCompleted;

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

    /// <summary>Picks a random free slot and marks it taken. Returns -1 if none are free.</summary>
    public int TakeRandomUnfilledSlot()
    {
        if (_free.Count == 0) return -1;

        int listIdx = Random.Range(0, _free.Count);
        int slotIdx = _free[listIdx];

        // swap-last: O(1) removal, order doesn't matter (random choice).
        _free[listIdx] = _free[_free.Count - 1];
        _free.RemoveAt(_free.Count - 1);

        slots[slotIdx].filled = true;
        return slotIdx;
    }

    /// <summary>Name of that slot's part: the correlation key with the scale-1 prefab's JammoPartSet (same GameObject.name).</summary>
    public string PartNameOf(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null || slots[index].bigStatuePart == null)
            return null;
        return slots[index].bigStatuePart.gameObject.name;
    }

    /// <summary>Placement done: the part becomes solid; fires reveal/complete events.</summary>
    public void OnSlotFilled(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null) return;

        if (slots[index].bigStatuePart != null)
        {
            Material target = slots[index].useEyesMaterial ? solidMaterialEyes : solidMaterial;
            if (target != null) slots[index].bigStatuePart.sharedMaterial = target;
        }

        onPartRevealed.Invoke();
        PartRevealed?.Invoke();
        if (_free.Count == 0) { onStatueComplete.Invoke(); StatueCompleted?.Invoke(); }
    }

    /// <summary>
    /// Piece lost (Jammo hit while carrying): the slot goes free again and stays ghost
    /// (OnSlotFilled was never called → no swap to solid). The statue doesn't progress: that
    /// piece must be re-earned with another kill.
    /// </summary>
    public void ReturnSlot(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null) return;
        if (!slots[index].filled) return; // already free
        slots[index].filled = false;
        _free.Add(index);
    }
}
