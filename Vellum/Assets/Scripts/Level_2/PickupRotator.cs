using UnityEngine;

public class PickupRotator : MonoBehaviour
{
    [Header("Rotazione")]
    [Tooltip("Velocità di rotazione sull'asse Y")]
    public float rotationSpeed = 100f; 

    [Header("Fluttuazione (Su e Giù)")]
    public bool enableBobbing = true;
    [Tooltip("Quanto velocemente va su e giù")]
    public float bobbingSpeed = 2f;    
    [Tooltip("Di quanto si alza e si abbassa")]
    public float bobbingAmount = 0.2f; 

    private Vector3 _startPosition;

    void Start()
    {
        // Salviamo la posizione iniziale da cui partirà l'effetto su e giù
        _startPosition = transform.localPosition;
    }

    void Update()
    {
        // 1. ROTAZIONE: Lo fa girare su se stesso (sull'asse Y)
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // 2. FLUTTUAZIONE: Usa una curva matematica (Seno) per un movimento morbido
        if (enableBobbing)
        {
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
            transform.localPosition = new Vector3(_startPosition.x, newY, _startPosition.z);
        }
    }
}