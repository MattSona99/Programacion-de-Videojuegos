using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

/// <summary>
/// Cinematic that drops the Player (and optionally the environment) into the tomb. Follows the
/// project's cinematic recipe: lock player input → raise a Cinemachine top-down camera → run a
/// timed coroutine driving a global dissolve shader → restore state. Triggered via UnityEvent.
/// </summary>
public class CinematicFallManager : MonoBehaviour
{
    [Header("Cinematic camera")]
    public CinemachineCamera topDownCamera;

    [Header("Player and environment references")]
    public GameObject player;
    public Transform environmentToFall;

    [Header("Fall settings")]
    public float targetYPosition = -200f;
    public float fallDuration = 5f;

    [Header("Objects to vanish immediately")]
    public GameObject[] objectsToVanish;

    [Header("Particle effects")]
    [Tooltip("Drag the Particle Systems (Book, Tomb, etc.) here to stop them during the fall")]
    public ParticleSystem[] particlesToStop;

    private bool _hasStarted = false;

    /// <summary>True while the fall cinematic is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Starts the fall cinematic once (ignored on subsequent calls).</summary>
    public void StartFallSequence()
    {
        if (_hasStarted) return;
        _hasStarted = true;
        IsPlaying = true;
        StartCoroutine(FallRoutine());
    }

    /// <summary>Locks input, raises the camera, stops particles, then drives the timed fall + dissolve and restores state.</summary>
    private IEnumerator FallRoutine()
    {
        // The initial fall disables everything, mouse included: keepLookActive = false.
        SetPlayerMovement(false, keepLookActive: false);
        if (topDownCamera != null) topDownCamera.Priority = 20;

        yield return new WaitForSeconds(2f);

        // --- STOP PARTICLES ---
        foreach (ParticleSystem ps in particlesToStop)
        {
            if (ps != null)
            {
                ps.Stop(); // Stops creating new ones
                ps.Clear(); // Instantly clears the ones already visible
            }
        }

        // --- OBJECTS TO HIDE (Plane, etc.) ---
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

    /// <summary>
    /// Locks/unlocks the Player. <paramref name="keepLookActive"/> = true blocks only movement
    /// (WASD/left stick) while keeping camera look alive (used by JammoGuideController during
    /// guided walks); false is a full shutdown (the initial cinematic fall).
    /// </summary>
    public void SetPlayerMovement(bool canMove, bool keepLookActive = true)
    {
        if (player != null)
        {
            // 1. Zero the movement vector on the broadcast receivers (StarterAssetsInputs)
            if (!canMove)
            {
                player.BroadcastMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.BroadcastMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
            }

            // 2. CharacterController: disable it only when we move the player via transform
            //    (cinematic fall). With keepLookActive it must stay on for gravity and collisions.
            if (!keepLookActive)
            {
                CharacterController charController = player.GetComponent<CharacterController>();
                if (charController != null) charController.enabled = canMove;
            }

            // 3. Controller scripts. ThirdPersonController and StarterAssetsInputs feed both
            //    movement and camera look: with keepLookActive we leave them on (zeroing the
            //    broadcast is enough to stop the player's legs).
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
            //    - keepLookActive=false → disable the WHOLE PlayerInput (look included).
            //    - keepLookActive=true → disable only the movement actions (Move/Sprint/Jump).
            //      Without this, if the player is HOLDING W at lock time (e.g. reaching the
            //      checkpoint while walking), the Input System keeps re-firing OnMove and
            //      re-feeds the vector right after our BroadcastMessage(zero).
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

            // 5. Animator: force idle in the same frame to avoid sliding on the blend tree.
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

        // 6. Cursor: handled only on a full shutdown; with keepLookActive it stays as it was.
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