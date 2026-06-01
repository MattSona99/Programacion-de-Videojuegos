using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class CinematicFallManager : MonoBehaviour
{
    [Header("Telecamera Cinematica")]
    public CinemachineCamera topDownCamera;

    [Header("Riferimenti Player ed Environment")]
    public GameObject player;
    public Transform environmentToFall;

    [Header("Impostazioni Caduta")]
    public float targetYPosition = -200f;
    public float fallDuration = 5f;

    [Header("Oggetti da far SVANIRe subito")]
    public GameObject[] objectsToVanish;

    [Header("Effetti Particellari")]
    [Tooltip("Trascina qui i Particle Systems (Libro, Tomba, ecc.) per spegnerli durante la caduta")]
    public ParticleSystem[] particlesToStop;

    private bool _hasStarted = false;

    public bool IsPlaying { get; private set; }

    public void StartFallSequence()
    {
        if (_hasStarted) return;
        _hasStarted = true;
        IsPlaying = true;
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        // La caduta iniziale spegne tutto, mouse incluso: keepLookActive = false.
        SetPlayerMovement(false, keepLookActive: false);
        if (topDownCamera != null) topDownCamera.Priority = 20;

        yield return new WaitForSeconds(2f);
        
        // --- SPEGNIMENTO PARTICELLE ---
        foreach (ParticleSystem ps in particlesToStop)
        {
            if (ps != null) 
            {
                ps.Stop(); // Smette di crearne di nuove
                ps.Clear(); // Cancella istantaneamente quelle già visibili
            }
        }

        // --- OGGETTI DA FAR SPARIRE (Plane, ecc.) ---
        foreach (GameObject obj in objectsToVanish)
        {
            if (obj != null) obj.SetActive(false);
        }

        Vector3 playerStart = player.transform.position;
        Vector3 envStart = environmentToFall != null ? environmentToFall.position : Vector3.zero;
        float dropDistance = playerStart.y - targetYPosition;

        float timeElapsed = 0f;
        while (timeElapsed < fallDuration)
        {
            timeElapsed += Time.deltaTime;
            float t = timeElapsed / fallDuration; 
            float smoothT = t * t * t; 

            Shader.SetGlobalVector("_GlobalPlayerPos", player.transform.position);

            float currentRadius = Mathf.Lerp(0f, 100f, t);
            Shader.SetGlobalFloat("_GlobalDissolveRadius", currentRadius);

            Vector3 playerTarget = new Vector3(playerStart.x, targetYPosition, playerStart.z);
            player.transform.position = Vector3.Lerp(playerStart, playerTarget, smoothT);

            if (environmentToFall != null)
            {
                Vector3 envTarget = new Vector3(envStart.x, envStart.y - dropDistance, envStart.z);
                environmentToFall.position = Vector3.Lerp(envStart, envTarget, smoothT);
            }

            yield return null;
        }

        player.transform.position = new Vector3(playerStart.x, targetYPosition, playerStart.z);
        if (environmentToFall != null)
        {
            environmentToFall.position = envStart; 
        }

        foreach (GameObject obj in objectsToVanish)
        {
            if (obj != null) obj.SetActive(true);
        }

        Shader.SetGlobalFloat("_GlobalDissolveRadius", 0f);

        SetPlayerMovement(true, keepLookActive: false);
        if (topDownCamera != null) topDownCamera.Priority = 9;

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.enabled = false;

        IsPlaying = false;
    }

    public void SetPlayerMovement(bool canMove, bool keepLookActive = true)
    {
        // keepLookActive = true: blocchiamo solo il movimento (WASD/gamepad sinistro),
        // lasciando viva la rotazione di camera (mouse / right stick). Caso usato da
        // JammoGuideController durante le camminate guidate.
        // keepLookActive = false: spegnimento completo (caduta cinematica iniziale).

        if (player != null)
        {
            // 1. Azzeriamo il vettore di movimento sui receiver del broadcast (StarterAssetsInputs)
            if (!canMove)
            {
                player.BroadcastMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.BroadcastMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
            }

            // 2. CharacterController: lo spegniamo solo se dobbiamo spostare il player via transform
            //    (caduta cinematica). In keepLookActive deve restare attivo per gravità e collisioni.
            if (!keepLookActive)
            {
                CharacterController charController = player.GetComponent<CharacterController>();
                if (charController != null) charController.enabled = canMove;
            }

            // 3. Script del controller. ThirdPersonController e StarterAssetsInputs alimentano sia
            //    il movimento sia il look della camera: in keepLookActive li lasciamo accesi
            //    (l'azzeramento del broadcast basta a fermare le gambe del player).
            MonoBehaviour[] allScripts = player.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in allScripts)
            {
                string scriptName = script.GetType().Name;
                if (scriptName == "ThirdPersonController" || scriptName == "StarterAssetsInputs")
                {
                    if (!keepLookActive) script.enabled = canMove;
                }
                else if (scriptName == "PlayerCombat")
                {
                    script.enabled = canMove;
                }
            }

            // 4. Input System:
            //    - keepLookActive=false → spegniamo TUTTO il PlayerInput (look incluso).
            //    - keepLookActive=true → disabilitiamo solo le action di movimento
            //      (Move/Sprint/Jump). Senza questo, se il player sta TENENDO PREMUTO
            //      W al momento del lock (caso: arrivo al checkpoint mentre cammina),
            //      l'Input System continua a ri-sparare OnMove e rialimenta il
            //      vettore subito dopo il nostro BroadcastMessage(zero).
            var playerInput = player.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                if (!keepLookActive)
                {
                    if (canMove) playerInput.ActivateInput();
                    else playerInput.DeactivateInput();
                }
                else if (playerInput.actions != null)
                {
                    var moveAction = playerInput.actions.FindAction("Move");
                    var sprintAction = playerInput.actions.FindAction("Sprint");
                    var jumpAction = playerInput.actions.FindAction("Jump");
                    if (canMove)
                    {
                        moveAction?.Enable();
                        sprintAction?.Enable();
                        jumpAction?.Enable();
                    }
                    else
                    {
                        moveAction?.Disable();
                        sprintAction?.Disable();
                        jumpAction?.Disable();
                    }
                }
            }

            // 5. Animator: forziamo idle nello stesso frame per evitare scivolate sul blend tree.
            if (!canMove)
            {
                Animator anim = player.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                    anim.Play("Idle Walk Run Blend", 0, 0f);
                    anim.Update(0f);
                }
            }
        }

        // 6. Cursore: lo gestiamo solo nello spegnimento totale; in keepLookActive resta com'era.
        if (!keepLookActive)
        {
            if (canMove)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    void OnValidate()
    {
        Shader.SetGlobalFloat("_GlobalDissolveRadius", -1000f);
    }
}