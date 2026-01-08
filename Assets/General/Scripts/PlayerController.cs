using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // --- KATLANABİLİR AYAR GRUPLARI ---

    [System.Serializable]
    public class MovementSettings
    {
        [Tooltip("Yürüme hızı.")]
        public float walkSpeed = 2.0f;
        [Tooltip("Koşma hızı (Shift ile).")]
        public float runSpeed = 5.0f;
        [Tooltip("Karakterin dönme yumuşaklığı.")]
        public float turnSmoothTime = 0.1f;
        [Tooltip("Zıplama yüksekliği.")]
        public float jumpHeight = 1.0f;
        [Tooltip("Yer çekimi kuvveti (Negatif olmalı).")]
        public float gravity = -9.81f;
    }

    [System.Serializable]
    public class ReferenceSettings
    {
        [Tooltip("Ana Kamera Transformu (Otomatik bulunur).")]
        public Transform cameraTransform;
        [Tooltip("Zemin kontrolü için ayakların altındaki boş obje.")]
        public Transform groundCheck;
        [Tooltip("Zemin algılama yarıçapı.")]
        public float groundDistance = 0.4f;
        [Tooltip("Zemin katmanı.")]
        public LayerMask groundMask;
    }

    // --- ANA DEĞİŞKENLER ---
    
    public MovementSettings moveSettings;
    public ReferenceSettings refs;

    // --- GİZLİ DEĞİŞKENLER ---
    
    private CharacterController controller;
    private PlayerCombat combatScript;
    private Animator animator;

    private Vector3 velocity;
    private bool isGrounded;
    private float turnSmoothVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        combatScript = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        // Kamerayı otomatik bul
        if (refs.cameraTransform == null && Camera.main != null)
            refs.cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        // 1. ÖLÜM VE STUN KONTROLÜ
        if (combatScript != null && (combatScript.status.isDead || combatScript.status.isBusy))
        {
            ApplyGravity();
            return;
        }

        HandleMovement();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // Zemin Kontrolü
        if(refs.groundCheck) // Hata vermesin diye null check
            isGrounded = Physics.CheckSphere(refs.groundCheck.position, refs.groundDistance, refs.groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        // Girdiler
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(x, 0f, z).normalized;

        // --- HAREKET MANTIĞI ---

        // DURUM A: SAVAŞ MODUNDAYIZ VE HEDEF VAR (LOCK-ON MOVEMENT)
        if (combatScript.combat.isInCombatMode && combatScript.currentTarget != null)
        {
            // 1. Karakteri Düşmana Döndür (Gövdeyi Kilitle)
            Vector3 dirToEnemy = (combatScript.currentTarget.position - transform.position).normalized;
            dirToEnemy.y = 0; // Karakter yukarı/aşağı bakmasın, sadece sağ/sol
            
            if (dirToEnemy != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dirToEnemy);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
            }

            // 2. Yönlü Hareket (Strafe) - Düşmana kilitliyken yan yan yürüme
            // Transform.right = Karakterin sağı, Transform.forward = Karakterin önü
            Vector3 moveDir = (transform.right * x) + (transform.forward * z);
            
            // Combat'ta genelde yürüme hızı kullanılır
            controller.Move(moveDir.normalized * moveSettings.walkSpeed * Time.deltaTime);

            // Animasyon (Eğer hareket ediyorsak Speed=1, yoksa 0)
            // İleride buraya "Blend Tree" ile sağ/sol animasyonları eklenebilir.
            float animSpeed = inputDir.magnitude > 0.1f ? 1f : 0f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
        }
        // DURUM B: NORMAL KEŞİF MODU (FREE ROAM)
        else
        {
            if (inputDir.magnitude >= 0.1f)
            {
                // Kameranın baktığı yöne göre hesapla
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + refs.cameraTransform.eulerAngles.y;
                
                // Karakteri gittiği yöne döndür
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, moveSettings.turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                
                // Shift ile koşma
                float speed = Input.GetKey(KeyCode.LeftShift) ? moveSettings.runSpeed : moveSettings.walkSpeed;
                controller.Move(moveDir.normalized * speed * Time.deltaTime);
                
                float animSpeed = Input.GetKey(KeyCode.LeftShift) ? 2f : 1f;
                animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            }
            else
            {
                animator.SetFloat("Speed", 0, 0.1f, Time.deltaTime);
            }
        }

        // Zıplama
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(moveSettings.jumpHeight * -2f * moveSettings.gravity);
            animator.SetTrigger("Jump");
        }
    }

    private void ApplyGravity()
    {
        velocity.y += moveSettings.gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}