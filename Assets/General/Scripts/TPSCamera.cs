using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Target & Mode")]
    public Transform target;
    public Vector3 lookAtOffSet = new Vector3(0.6f, 1.4f, 0); // Sağ omuz ayarı

    [Header("Combat Settings")]
    public PlayerCombat playerCombat;
    public float lockOnSmoothSpeed = 10f; 

    [Header("Sensitivity & Smoothing")]
    public float mouseSensitivityX = 180f;
    public float mouseSensitivityY = 150f;
    [Range(0.01f, 0.5f)] public float rotationSmoothTime = 0.05f;
    [Range(0.01f, 0.5f)] public float moveSmoothTime = 0.1f;

    [Header("Distance & Collision")]
    public float normalDistance = 2.5f;
    public float combatDistance = 3.5f;
    public float minDistance = 1.0f;
    public float maxDistance = 5.0f;
    public LayerMask collisionLayers; // PLAYER ve ENEMY seçili OLMASIN!

    [Header("Limits")]
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    // --- GİZLİ DEĞİŞKENLER ---
    private float mouseX, mouseY;
    private float currentX, currentY;
    private float xVelocity, yVelocity;
    private Vector3 currentVelocity;
    private float finalDistance;

    // --- SHAKE SİSTEMİ ---
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;
    private Vector3 shakeOffset = Vector3.zero;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentX = NormalizeAngle(transform.eulerAngles.y);
        currentY = NormalizeAngle(transform.eulerAngles.x);
        mouseX = currentX;
        mouseY = currentY;

        if (!playerCombat && target) playerCombat = target.GetComponent<PlayerCombat>();
        finalDistance = normalDistance;
    }

    private void LateUpdate()
    {
        if (!target) return;

        HandleInput();
        UpdateShake(); // Sarsıntıyı hesapla
        MoveAndRotate();
    }

    // DIŞARIDAN ÇAĞRILACAK FONKSİYON
    public void ShakeCamera(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    private void UpdateShake()
    {
        if (shakeTimer > 0)
        {
            // Rastgele bir nokta belirle (Titreşim)
            shakeOffset = Random.insideUnitSphere * shakeMagnitude;
            shakeTimer -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
    }

    private void HandleInput()
    {
        if (playerCombat != null && playerCombat.combat.isInCombatMode && playerCombat.currentTarget != null)
        {
            // LOCK-ON
            Vector3 dirToEnemy = playerCombat.currentTarget.position - target.position;
            if(dirToEnemy != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dirToEnemy);
                Vector3 targetEuler = lookRot.eulerAngles;

                float targetPitch = targetEuler.x;
                if (targetPitch > 180) targetPitch -= 360;
                targetPitch = Mathf.Clamp(targetPitch, minVerticalAngle, maxVerticalAngle);

                mouseX = Mathf.LerpAngle(mouseX, targetEuler.y, Time.deltaTime * lockOnSmoothSpeed);
                mouseY = Mathf.Lerp(mouseY, targetPitch, Time.deltaTime * lockOnSmoothSpeed);
            }
        }
        else
        {
            // NORMAL
            mouseX += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
            mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;
            mouseY = Mathf.Clamp(mouseY, minVerticalAngle, maxVerticalAngle);
        }

        currentX = Mathf.SmoothDampAngle(currentX, mouseX, ref xVelocity, rotationSmoothTime);
        currentY = Mathf.SmoothDamp(currentY, mouseY, ref yVelocity, rotationSmoothTime);
    }

    private void MoveAndRotate()
    {
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // Hedef mesafeyi belirle
        float targetDist = (playerCombat && playerCombat.combat.isInCombatMode) ? combatDistance : normalDistance;
        
        // Hedef pozisyon (Omuz offsetli)
        Vector3 targetPos = target.position + lookAtOffSet;
        Vector3 dir = rotation * -Vector3.forward;

        // 1. DUVAR KONTROLÜ (Raycast)
        RaycastHit hit;
        if (Physics.SphereCast(targetPos, 0.2f, dir, out hit, targetDist, collisionLayers))
        {
            finalDistance = Mathf.Clamp(hit.distance - 0.2f, minDistance, targetDist);
        }
        else
        {
            finalDistance = Mathf.Lerp(finalDistance, targetDist, Time.deltaTime * 20f); // Hızı artırdık (10->20)
        }

        Vector3 negDistance = new Vector3(0.0f, 0.0f, -finalDistance);
        
        // Hesaplanan ham pozisyon (Sarsıntı dahil)
        Vector3 calculatedPos = rotation * negDistance + targetPos + shakeOffset;

        // 2. KİŞİSEL ALAN KORUMASI (Anti-Clipping Sphere)
        // Karakterin kendisine (Target.position) olan mesafeyi ölçüyoruz.
        // LookAtOffset'e değil, direkt karakterin merkezine olan mesafe önemli.
        Vector3 directionFromChar = calculatedPos - target.position;
        float distFromChar = directionFromChar.magnitude;

        // Eğer kamera karakterin merkezine "MinDistance"dan daha yakınsa:
        if (distFromChar < minDistance)
        {
            // Kamerayı karakterden uzağa, "MinDistance" sınırına kadar ittir.
            calculatedPos = target.position + (directionFromChar.normalized * minDistance);
            
            // Ani girişlerde titremeyi önlemek için hızı sıfırla
            currentVelocity = Vector3.zero; 
            transform.position = calculatedPos; // Direkt ata (Smooth yapma)
        }
        else
        {
            // Mesafe güvenliyse normal yumuşak takip yap
            transform.position = Vector3.SmoothDamp(transform.position, calculatedPos, ref currentVelocity, moveSmoothTime);
        }

        transform.rotation = rotation;
    }
    private float NormalizeAngle(float angle)
    {
        angle %= 360;
        if (angle > 180) return angle - 360;
        return angle;
    }
}