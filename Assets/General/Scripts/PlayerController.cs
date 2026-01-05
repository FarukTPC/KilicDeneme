using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    #region Variables (Değişkenler)

    [Header("Movement Settings")]
    
    [Tooltip("Yürüme Hızı")]
    [Range(0f, 10f)] // 0 ile 10 arasında bir slider oluşturur
    [SerializeField] private float walkSpeed = 3f; 

    [Tooltip("Koşma Hızı (Shift'e basınca)")]
    [Range(0f, 20f)] // 0 ile 20 arasında bir slider oluşturur
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("Dönüş yumuşaklığı (Daha düşük = Daha keskin)")]
    [Range(0f, 1f)] // 0 ile 1 arasında hassas ayar
    [SerializeField] private float turnSmoothTime = 0.1f;
    
    [Tooltip("Yerçekimi kuvveti")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Animation Settings")]
    
    [Tooltip("Animasyonlar arası geçiş süresi")]
    [Range(0f, 1f)]
    [SerializeField] private float animDampTime = 0.1f;

    // --- Private References ---
    private CharacterController _characterController;
    private Animator _animator;
    private Transform _cameraTransform;
    
    // --- Helper Variables ---
    private Vector3 _velocity;           
    private float _turnSmoothVelocity;   

    #endregion

    #region Unity Methods

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    #endregion

    #region Custom Methods

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // --- YÖN VE DÖNÜŞ (Kameraya Göre) ---
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // --- HIZ VE ANİMASYON AYARI ---
            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            // 1. Fiziksel Hız (Slider ile ayarladığın gerçek hızlar)
            float currentMoveSpeed = isRunning ? runSpeed : walkSpeed;

            // 2. Animasyon Değeri (Animatöre giden sabit değerler: 0.5 veya 1)
            float animationValue = isRunning ? 1f : 0.5f;

            // Hareketi uygula
            _characterController.Move(moveDir.normalized * currentMoveSpeed * Time.deltaTime);
            
            // Animasyonu uygula
            _animator.SetFloat("Speed", animationValue, animDampTime, Time.deltaTime);
        }
        else
        {
            // Durma animasyonu
            _animator.SetFloat("Speed", 0f, animDampTime, Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; 
        }

        _velocity.y += gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    #endregion
}