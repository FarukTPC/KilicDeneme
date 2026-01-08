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
        // Eğer karakter ölü ya da stun yemişse (Busy) hareket edemez.
        if (combatScript != null && (combatScript.status.isDead || combatScript.status.isBusy))
        {
            // Havada donup kalmasın diye yer çekimi işlemeye devam etsin
            ApplyGravity();
            return;
        }

        HandleMovement();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // Yere basıyor muyuz?
        isGrounded = Physics.CheckSphere(refs.groundCheck.position, refs.groundDistance, refs.groundMask);
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Yere yapışık kalması için küçük bir kuvvet
        }

        // Klavye Girdileri
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(x, 0f, z).normalized;

        // Hareket varsa
        if (direction.magnitude >= 0.1f)
        {
            // Kameranın baktığı yöne göre hareket açısını hesapla
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + refs.cameraTransform.eulerAngles.y;
            
            // Karakteri o yöne yumuşakça döndür
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, moveSettings.turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Hareket yönü
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            // Koşma kontrolü (Shift)
            float speed = Input.GetKey(KeyCode.LeftShift) ? moveSettings.runSpeed : moveSettings.walkSpeed;
            
            // Savaş Modunda mecburi yürüme (Opsiyonel: İstersen kaldırabilirsin)
            if (combatScript.combat.isInCombatMode) speed = moveSettings.walkSpeed;

            controller.Move(moveDir.normalized * speed * Time.deltaTime);
            
            // Animasyon Hızı
            // Blend Tree için Speed parametresi (0 = Dur, 1 = Yürü, 2+ = Koş)
            float animSpeed = Input.GetKey(KeyCode.LeftShift) ? 2f : 1f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
        }
        else
        {
            // Hareket yoksa animasyon hızı 0
            animator.SetFloat("Speed", 0, 0.1f, Time.deltaTime);
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