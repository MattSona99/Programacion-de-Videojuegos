using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class JammoCompanion : MonoBehaviour
{
    [Header("Connessione al Puzzle")]
    [Tooltip("Trascina qui l'oggetto PuzzleManager dalla tua scena")]
    public PathPuzzleManager puzzleManager;

    [Header("Attivazione")]
    [Tooltip("Dialogo che parte la prima volta che il player entra nel trigger di Jammo")]
    public DialogueAsset activationDialogue;

    [Tooltip("Nome del bool nell'Animator che fa partire l'animazione di alzata")]
    public string animatorActivatedBool = "IsActivated";

    [Tooltip("Secondi di attesa dopo il dialogo prima che Jammo inizi a camminare")]
    public float getUpDelay = 5f;

    [Header("Blocco Player")]
    [Tooltip("Riferimento al CinematicFallManager (stesso che usa JammoGuideController). Serve a bloccare il Player nei secondi in cui Jammo si alza.")]
    public CinematicFallManager cinematicManager;

    private Animator anim;
    private NavMeshAgent agent;
    private bool _hasBeenTriggered = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        
        // Disabilitiamo subito l'Agent, non ci serve più per l'inseguimento libero!
        if (agent != null) agent.enabled = false;
    }

    public void HandleActivationInteraction()
    {
        if (_hasBeenTriggered) return;
        _hasBeenTriggered = true;

        if (activationDialogue != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.PlayDialogue(
                activationDialogue,
                () => StartCoroutine(GetUpAndActivateRoutine()));
        }
        else
        {
            StartCoroutine(GetUpAndActivateRoutine());
        }
    }

    private IEnumerator GetUpAndActivateRoutine()
    {
        // keepLookActive=true di default → mouse/right-stick continuano a muovere la camera,
        // solo Move/Sprint/Jump vengono congelati. Lo sblocco lo farà JammoGuideController
        // a fine camminata del robottino.
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(false);
        }

        // 1. Fai partire l'animazione di risveglio
        if (anim != null && !string.IsNullOrEmpty(animatorActivatedBool))
        {
            anim.SetBool(animatorActivatedBool, true);
        }

        // 2. Aspetta che finisca di alzarsi
        yield return new WaitForSeconds(getUpDelay);

        // 3. INIZIA IL PUZZLE!
        if (puzzleManager != null)
        {
            Debug.Log("Jammo si è alzato! Inizio la sequenza del puzzle.");
            puzzleManager.StartPuzzleSequence();
        }
        else
        {
            Debug.LogError("Attenzione: Manca il PuzzleManager nello script di JammoCompanion!");
            // Senza puzzleManager nessuno sbloccherebbe il Player: lo sblocchiamo qui per evitare lo stuck.
            if (cinematicManager != null) cinematicManager.SetPlayerMovement(true);
        }

        // 4. Disattiviamo questo script (JammoCompanion) perché ora Jammo 
        // è controllato da JammoGuideController!
        this.enabled = false; 
    }
}