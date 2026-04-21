using UnityEngine;
using TMPro;

/// <summary>
/// Controls the behavior of transient UI text elements, handling upward translation, 
/// alpha-channel fading, and automated lifecycle management for feedback like damage numbers.
/// </summary>
public class FloatingText : MonoBehaviour
{
    [Header("Animation Settings")]
    public float moveSpeed = 2f;
    public float destroyTime = 1.5f;
    
    [Tooltip("Applies a random positional deviation to prevent multiple text instances from overlapping perfectly.")]
    public Vector3 randomizeOffset = new Vector3(0.5f, 0.5f, 0);

    private TextMeshPro textMesh;
    private Color textColor;

    /// <summary>
    /// Caches the TextMeshPro component reference on instantiation.
    /// </summary>
    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// Initializes the random offset and schedules the object for destruction 
    /// to prevent memory leaks in the scene hierarchy.
    /// </summary>
    void Start()
    {
        transform.position += new Vector3(
            Random.Range(-randomizeOffset.x, randomizeOffset.x),
            Random.Range(-randomizeOffset.y, randomizeOffset.y),
            0
        );

        Destroy(gameObject, destroyTime);
    }

    /// <summary>
    /// Handles the frame-by-frame transformation logic, moving the text upward 
    /// and linearly interpolating the alpha transparency toward zero.
    /// </summary>
    void Update()
    {
        // Translate the object along the Y-axis based on moveSpeed
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // Calculate the alpha decay rate relative to the total destruction time
        textColor.a -= (1f / destroyTime) * Time.deltaTime;
        textMesh.color = textColor;
    }

    /// <summary>
    /// Configures the visual properties of the text. Must be called immediately 
    /// after instantiation to define the displayed content and initial color.
    /// </summary>
    /// <param name="textContent">The string value to display (e.g., damage amount).</param>
    /// <param name="color">The starting color of the text.</param>
    public void Setup(string textContent, Color color)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();
        
        textMesh.text = textContent;
        textMesh.color = color;
        
        // Cache initial color to allow the Update loop to modify the alpha channel
        textColor = color; 
    }
}