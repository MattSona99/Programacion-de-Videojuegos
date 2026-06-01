using UnityEngine;
using System.Collections;

public class JammoGuideController : MonoBehaviour
{
    [Header("Impostazioni Base")]
    public PathPuzzleManager manager;
    public float walkSpeed = 3f;
    public Animator jammoAnimator; 

    [Header("Blocco Player")]
    [Tooltip("Trascina qui l'oggetto della scena che contiene il CinematicFallManager")]
    public CinematicFallManager cinematicManager;

    [Header("Collisioni durante la camminata")]
    [Tooltip("Opzionale: collider non-trigger di Jammo da disabilitare durante la camminata, così il player non lo spinge al checkpoint.")]
    [SerializeField] private Collider jammoBodyCollider;

    [Header("Posa finale")]
    [Tooltip("Transform della porta (o di qualsiasi punto): quando Jammo arriva sull'ultima tile si gira a guardare questo target.")]
    [SerializeField] private Transform doorLookTarget;

    [Tooltip("Velocità di rotazione (Slerp) di Jammo verso il target finale.")]
    [SerializeField] private float turnSpeed = 5f;

    private Coroutine _currentWalk;

    // true mentre Jammo sta percorrendo il tracciato (il player è già bloccato
    // dal cinematicManager): serve a PathPuzzleManager per non concedere l'aiuto
    // a memoria durante la camminata guidata.
    public bool IsWalking => _currentWalk != null;

    public void WalkToCheckpoint()
    {
        if (_currentWalk != null) StopCoroutine(_currentWalk);
        _currentWalk = StartCoroutine(FollowPathRoutine(0, manager.checkpointIndex));
    }

    public void ResumeWalkToEnd()
    {
        if (_currentWalk != null) StopCoroutine(_currentWalk);
        _currentWalk = StartCoroutine(FollowPathRoutine(manager.checkpointIndex, manager.correctPath.Count - 1));
    }

    private IEnumerator FollowPathRoutine(int startIndex, int endIndex)
    {
        // 1. BLOCCHIAMO IL PLAYER ALL'INIZIO DELLA CAMMINATA
        //    (keepLookActive resta su true di default → mouse / right-stick continuano a funzionare)
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(false);
        }

        if (jammoBodyCollider != null) jammoBodyCollider.enabled = false;

        if (jammoAnimator != null)
        {
            jammoAnimator.SetFloat("Speed", 1f); 
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            PathTile targetTileScript = manager.correctPath[i];
            Transform targetTile = targetTileScript.transform;

            Vector3 targetPosition = new Vector3(targetTile.position.x, transform.position.y, targetTile.position.z);

            while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
            {
                Vector3 direction = (targetPosition - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(transform.position, targetPosition, walkSpeed * Time.deltaTime);

                yield return null;
            }

            // Scia esplicita: i trigger di Jammo possono essere spenti durante la
            // camminata (jammoBodyCollider.enabled = false sopra), quindi non ci
            // affidiamo a OnTriggerEnter per accendere la tile.
            targetTileScript.SetColor(targetTileScript.robotColor);
        }

        if (jammoAnimator != null)
        {
            jammoAnimator.SetFloat("Speed", 0f);
        }

        // Se è l'ultima camminata (Jammo è arrivato alla fine del percorso) e
        // c'è un target, lo facciamo girare verso la porta prima di sbloccare il player.
        if (endIndex == manager.correctPath.Count - 1 && doorLookTarget != null)
        {
            yield return TurnToFace(doorLookTarget);
        }

        if (jammoBodyCollider != null) jammoBodyCollider.enabled = true;

        // Spegniamo la scia viola PRIMA di ridare il controllo al player: così le
        // tile che lui ri-attraverserà partiranno da default e si accenderanno in
        // playerColor passo passo. Lampeggia 3 volte prima di sparire.
        if (manager != null) yield return manager.StartCoroutine(manager.BlinkAndClearRobotTrail());

        // 2. SBLOCCHIAMO IL PLAYER QUANDO JAMMO SI FERMA (Al checkpoint o alla fine)
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(true);
        }

        _currentWalk = null;
    }

    private IEnumerator TurnToFace(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f; // ruotiamo solo sul piano XZ, niente pitch.
        if (toTarget.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        // Safety: limite di 3s nel caso il target sia praticamente sopra Jammo.
        float elapsed = 0f;
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f && elapsed < 3f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRot;
    }
}