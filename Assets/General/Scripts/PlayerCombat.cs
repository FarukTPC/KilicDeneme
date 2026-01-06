using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    // --- ÖZEL YETENEK YAPISI ---
    [System.Serializable]
    public struct SpecialAttack
    {
        public string attackName;      
        public KeyCode inputKey;       
        public string triggerName;     
        [Tooltip("Bu yetenek kaç saniye sürüyor? (Kilitlenme süresi)")]
        public float duration;         
    }

    #region Variables

    [Header("Health System")]
    public int maxHealth = 100;
    public int currentHealth;
    public bool isDead = false;

    [Header("Parry System (Savuşturma)")]
    [Tooltip("Parry penceresi kaç saniye açık kalsın? (Örn: 0.5sn)")]
    public float parryWindowDuration = 0.5f;
    [Tooltip("Parry tekrar dolum süresi")]
    public float parryCooldown = 1.0f;
    public bool isParrying = false; // Düşman buna bakıp hasar veremeyecek

    [Header("Effects (Hasar Alma)")]
    public ParticleSystem bloodEffect; // Kan efekti
    public Transform cameraTransform;  // Titreme için kamera
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.1f;

    [Header("Basic Attack")]
    public bool canBasicAttack = true;
    public float basicAttackBlockDuration = 0.7f;

    [Header("Special Attacks List")]
    public List<SpecialAttack> specialAttacks;

    // --- KONTROL DEĞİŞKENLERİ ---
    private bool isBusy = false;
    private float lastBasicAttackTime = -999f;
    private float lastParryTime = -999f;
    private Vector3 originalCameraPos;

    // Referanslar
    private Animator _animator;
    private AudioSource _audioSource;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
        
        currentHealth = maxHealth;

        // Kamera boşsa otomatik bul
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (isDead) return;
        if (isBusy) return;

        // 1. PARRY KONTROLÜ (Sağ Tık - En öncelikli)
        if (Input.GetMouseButtonDown(1) && Time.time >= lastParryTime + parryCooldown)
        {
            PerformParry();
            return; // Parry yaptıysak saldırı yapma
        }

        // 2. Saldırı sonrası bekleme süresi (Animation Lock)
        if (Time.time < lastBasicAttackTime + basicAttackBlockDuration) return;

        // 3. Temel Saldırı (Sol Tık)
        if (canBasicAttack && Input.GetMouseButtonDown(0))
        {
            PerformBasicAttack();
        }

        // 4. Özel Saldırılar (Listedeki Tuşlar)
        CheckSpecialAttacks();
    }

    #endregion

    #region Combat Logic

    // --- PARRY MEKANİĞİ ---
    private void PerformParry()
    {
        _animator.SetTrigger("Parry");
        lastParryTime = Time.time;
        StartCoroutine(ParryRoutine());
    }

    private IEnumerator ParryRoutine()
    {
        isBusy = true;      // Başka tuşa basmayı engelle
        isParrying = true;  // Ölümsüzlük penceresini aç
        
        // Debug.Log("🛡️ Parry Açıldı!");

        yield return new WaitForSeconds(parryWindowDuration);
        
        isParrying = false; // Ölümsüzlük bitti
        isBusy = false;     // Hareket serbest
        
        // Debug.Log("❌ Parry Bitti");
    }

    // --- HASAR ALMA (Enemy bu fonksiyonu çağıracak) ---
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // KRİTİK NOKTA: Eğer Parry yapıyorsak hasarı engelle!
        if (isParrying)
        {
            Debug.Log("✨ PARRY BAŞARILI! Hasar engellendi.");
            // Buraya "Kılıç çınlama sesi" ekleyebilirsin
            return; 
        }

        // Parry yapmıyorsak hasarı ye
        currentHealth -= damage;
        _animator.SetTrigger("Hit"); // Hasar animasyonu

        // Efektler
        if (bloodEffect != null) bloodEffect.Play();
        if (cameraTransform != null) StartCoroutine(ShakeCamera());

        Debug.Log("🩸 Hasar alındı! Can: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        _animator.SetTrigger("Die");
        Debug.Log("💀 Oyuncu Öldü.");
        // Buraya "Game Over" ekranı kodu gelebilir
    }

    #endregion

    #region Attack Helper Methods

    private void PerformBasicAttack()
    {
        _animator.SetTrigger("Attack");
        lastBasicAttackTime = Time.time;
    }

    private void CheckSpecialAttacks()
    {
        foreach (var skill in specialAttacks)
        {
            if (Input.GetKeyDown(skill.inputKey))
            {
                _animator.SetTrigger(skill.triggerName);
                StartCoroutine(BusyRoutine(skill.duration));
                break;
            }
        }
    }

    private IEnumerator BusyRoutine(float time)
    {
        isBusy = true;
        yield return new WaitForSeconds(time); 
        isBusy = false;
    }

    // Ekran Titretme
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