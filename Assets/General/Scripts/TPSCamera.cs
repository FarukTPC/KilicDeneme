using UnityEngine;
using UnityEngine.InputSystem;

public class TPSCamera : MonoBehaviour
{
    [Header("Target & Mode")]
    [Tooltip("Takip edilecek oyuncu objesi.")]
    public Transform target;
    [Tooltip("Kameranın bakacağı nokta(Oyuncunun kafa hizası vb.)")]
    public Vector3 lookAtOffSet = new Vector3(0, 1.5f, 0);

    [Header("CombatSettings")]
    [Tooltip("Savaş modu durumunu okumak için referans")]
    public PlayerCombat playerCombat;

    [Header("Sensitivity & Smoothing")]
    public float mouseSensitivityX = 150f;
    public float mouseSensitivityY = 150f;
    [Tooltip("Kamera dönüş yumuşaklıgı (Daha düşük = Daha yumuşak)")]
    [Range(0.01f, 0.5f)] public float rotationSmoothTime = 0.12f;
    [Tooltip("Kamera takip yumuşaklıgı")]
    [Range(0.01f, 0.5f)] public float moveSmoothTime = 0.1f;

    [Header("Distance & Collision")]
    public float normalDistance = 3.5f;
    public float combatDistance = 4.5f;
    public float minDistance = 1.0f;
    public float maxDistance =  6.0f;
    public LayerMask collisionLayers;

    [Header("Limits")]
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;

    private float mouseX, mouseY;
    private float currentX, currentY;
    private float xVelocity, yVelocity;

    private Vector3 currentVelocity;
    private float finalDistance;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;

        if (!playerCombat && target) playerCombat = target.GetComponent<PlayerCombat>();
        finalDistance = normalDistance;
    }

    private void LateUpdate()
    {
        if (!target) return;
        HandleInput();
        MoveAndRotate();
    }

    private void HandleInput()
    {
        mouseX += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
        mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;

        mouseY = Mathf.Clamp(mouseY, minVerticalAngle, maxVerticalAngle);

        currentX = Mathf.SmoothDamp(currentX, mouseX, ref xVelocity, rotationSmoothTime);
        currentY = Mathf.SmoothDamp(currentY, mouseY, ref yVelocity, rotationSmoothTime);
    }

    private void MoveAndRotate()
    {
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        float targetDist = (playerCombat && playerCombat.combat.isInCombatMode)  ? combatDistance : normalDistance;
        Vector3 targetPos = target.position + lookAtOffSet;
        Vector3 dir = rotation * -Vector3.forward;

        RaycastHit hit;
        if (Physics.SphereCast(targetPos, 0.2f, dir, out hit, targetDist, collisionLayers))
        {
            finalDistance = Mathf.Clamp(hit.distance - 0.2f, minDistance, targetDist);
        }
        else
        {
            finalDistance = Mathf.Lerp(finalDistance, targetDist, Time.deltaTime * 10f);
        }
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -finalDistance);
        Vector3 position = rotation * negDistance + targetPos;

        transform.rotation = rotation;
        transform.position = Vector3.SmoothDamp(transform.position, position, ref currentVelocity, moveSmoothTime);
    }
}
