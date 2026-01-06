using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    public struct SpecialAttack
    {
        public string attackName;      
        public KeyCode inputKey;       
        public string triggerName;     
        public float duration;
        
        [Tooltip("Bu yetenek düşmana kaç hasar versin?")]
        public int damage; 
        
        public bool causesStun; 

        [Header("Audio")]
        public AudioClip skillActionSound; 
        public AudioClip skillHitSound;    
    }

    #region Variables

    [Header("Damage Settings")]
    [Tooltip("Sol Tık (Kılıç) vuruşunun hasarı")]
    public int basicAttackDamage = 10; 

    private int currentAttackDamage; 

    [Header("Global Audio")]
    public AudioSource audioSource;
    public AudioClip swordSwingSound;
    public AudioClip swordHitSound;
    public AudioClip parryActionSound; 
    public AudioClip parrySuccessSound; 
    
    private AudioClip currentHitSound; 

    [Header("Attack Detection")]
    public Transform attackPoint;
    public float attackRange = 0.8f;
    public LayerMask enemyLayers;
    public float hitTimingDelay = 0.2f; 

    [Header("Health & Status")]
    public int maxHealth = 100;
    public int currentHealth;
    public bool isDead = false;

    [Header("Stun Settings")]
    public float stunDuration = 0.5f;
    private bool isStunned = false; 

    // --- YENİ EKLENEN KISIM: PLAYER KNOCKBACK AYARLARI ---
    [Header("Player Knockback Settings (YENİ)")]
    [Tooltip("Düşman vurduğunda ne kadar geriye uçalım?")]
    public float knockbackForce = 8f; 
    [Tooltip("Geriye uçma süresi")]
    public float knockbackDuration = 0.2f;
    // -----------------------------------------------------

    [Header("Parry System")]
    public float parryWindowDuration = 0.5f;
    public float parryCooldown = 1.0f;
    public bool isParrying = false; 

    [Header("Effects")]
    public ParticleSystem bloodEffect; 
    public Transform cameraTransform;  
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 0.4f;

    [Header("Basic Attack Settings")]
    public bool canBasicAttack = true;
    public float basicAttackBlockDuration = 0.7f;

    [Header("Special Attacks List")]
    public List<SpecialAttack> specialAttacks;

    // Kontroller
    private bool isBusy = false;
    private float lastBasicAttackTime = -999f;
    private float lastParryTime = -999f;
    private Vector3 originalCameraPos;
    private bool currentAttackCausesStun = false;

    // Bileşenler
    private Animator _animator;
    private CharacterController _characterController; // Varsa kullanalım

    #endregion

    #region Unity Methods

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>(); // Otomatik bul

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        currentHealth = maxHealth;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (isDead) return;
        if (isStunned) return; 
        if (isBusy) return;

        // 1. PARRY
        if (Input.GetMouseButtonDown(1) && Time.time >= lastParryTime + parryCooldown)
        {
            PerformParry();
            return; 
        }

        if (Time.time < lastBasicAttackTime + basicAttackBlockDuration) return;

        // 3. Temel Saldırı
        if (canBasicAttack && Input.GetMouseButtonDown(0))
        {
            PerformBasicAttack();
        }

        // 4. Özel Saldırılar
        CheckSpecialAttacks();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    #endregion

    #region Combat Logic

    public bool TryBlockAttack(int incomingDamage, Transform attacker)
    {
        if (isDead) return false;

        if (isParrying)
        {
            Debug.Log("✨ OYUNCU PARRYLEDİ!");
            if (audioSource && parrySuccessSound)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(parrySuccessSound);
            }
            StartCoroutine(ShakeCamera());
            return true; 
        }

        TakeDamage(incomingDamage, attacker);
        return false;
    }

    public void TakeDamage(int damageAmount, Transform attacker)
    {
        if (isDead || isParrying) return;

        currentHealth -= damageAmount;
        _animator.SetTrigger("Hit");
        
        if (bloodEffect != null) bloodEffect.Play();
        if (cameraTransform != null) StartCoroutine(ShakeCamera());
        
        // Geriye tepme başlat
        if (attacker != null) StartCoroutine(PlayerKnockbackRoutine(attacker));

        Debug.Log("Player Hasar Aldı! Kalan Can: " + currentHealth);

        if (currentHealth <= 0) Die();
    }
    
    public void GetStunned()
    {
        if (isDead || isParrying) return;
        StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        _animator.SetTrigger("Hit"); 
        Debug.Log("😵 OYUNCU STUN YEDİ!");
        
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
    }

    // --- GÜNCELLENEN KNOCKBACK RUTİNİ ---
    private IEnumerator PlayerKnockbackRoutine(Transform attacker)
    {
        float timer = 0;
        
        // Saldırganın ters yönüne doğru vektör
        Vector3 pushDir = (transform.position - attacker.position).normalized;
        pushDir.y = 0; // Havaya fırlamasın

        while(timer < knockbackDuration)
        {
            // İtme vektörü (Hız * Zaman)
            Vector3 moveVector = pushDir * knockbackForce * Time.deltaTime;

            // Eğer karakterde CharacterController varsa onunla hareket et (Daha güvenli)
            if (_characterController != null)
            {
                _characterController.Move(moveVector);
            }
            else
            {
                // Yoksa Transform ile hareket et
                transform.position += moveVector;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
    }
    // -------------------------------------

    private void PerformBasicAttack()
    {
        _animator.SetTrigger("Attack");
        lastBasicAttackTime = Time.time;
        
        currentHitSound = swordHitSound;
        currentAttackCausesStun = false; 
        currentAttackDamage = basicAttackDamage; 

        if (audioSource && swordSwingSound) audioSource.PlayOneShot(swordSwingSound);
        StartCoroutine(AttackRoutine());
    }
    
    private void CheckSpecialAttacks()
    {
        foreach (var skill in specialAttacks)
        {
            if (Input.GetKeyDown(skill.inputKey))
            {
                _animator.SetTrigger(skill.triggerName);
                StartCoroutine(BusyRoutine(skill.duration));
                
                currentHitSound = skill.skillHitSound;
                currentAttackCausesStun = skill.causesStun;
                currentAttackDamage = skill.damage;

                if (audioSource && skill.skillActionSound) audioSource.PlayOneShot(skill.skillActionSound);
                StartCoroutine(AttackRoutine());
                break;
            }
        }
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitTimingDelay);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers);
        
        bool hasHitTarget = false;
        foreach(Collider enemy in hitEnemies)
        {
            EnemyAI enemyScript = enemy.GetComponent<EnemyAI>();
            if(enemyScript != null)
            {
                // Hasar verirken kendimizi (transform) de yolluyoruz ki Enemy bizi tanısın ve bizden uzağa savrulsun
                enemyScript.TakeDamage(currentAttackDamage, transform); 
                hasHitTarget = true;
                
                if (currentAttackCausesStun)
                {
                    enemyScript.GetStunned(1.5f); 
                }
                else
                {
                    enemyScript.GetStunned(0.2f); 
                }
            }
        }

        if (hasHitTarget && audioSource && currentHitSound != null)
        {
             audioSource.pitch = Random.Range(0.9f, 1.1f);
             audioSource.PlayOneShot(currentHitSound);
             StartCoroutine(ShakeCamera());
        }
    }

    private void PerformParry()
    {
        _animator.SetTrigger("Parry");
        lastParryTime = Time.time;
        if (audioSource && parryActionSound) audioSource.PlayOneShot(parryActionSound);
        StartCoroutine(ParryRoutine());
    }

    private IEnumerator ParryRoutine()
    {
        isBusy = true;      
        isParrying = true;  
        yield return new WaitForSeconds(parryWindowDuration);
        isParrying = false; 
        isBusy = false;     
    }

    private void Die()
    {
        isDead = true;
        _animator.SetTrigger("Die");
        this.enabled = false; 
    }

    private IEnumerator BusyRoutine(float time)
    {
        isBusy = true;
        yield return new WaitForSeconds(time); 
        isBusy = false;
    }

    private IEnumerator ShakeCamera()
    {
        originalCameraPos = cameraTransform.localPosition;
        float elapsed = 0.0f;
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            cameraTransform.localPosition = new Vector3(originalCameraPos.x + x, originalCameraPos.y + y, originalCameraPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cameraTransform.localPosition = originalCameraPos;
    }

    #endregion
}