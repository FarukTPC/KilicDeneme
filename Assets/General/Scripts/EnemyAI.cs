using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    #region Variables

    [Header("11. Madde: Hasar Ayarı")]
    public int damage = 10;

    [Header("Health & Combat")]
    public int maxHealth = 100;
    private int currentHealth;
    public bool isDead = false;

    [Header("8. ve 9. Madde: Ses ve Efektler")]
    public AudioSource audioSource;
    public AudioClip swingSound;
    public AudioClip hitSound;
    public ParticleSystem bloodEffect;

    [Header("Parry System (6. Madde)")]
    [Tooltip("Düşmanın bizi parryleme şansı (0 ile 1 arası). Örn: 0.3 = %30")]
    [Range(0f, 1f)] public float parryChance = 0.3f; // Şansı biraz arttırdım

    [Header("Movement Settings (1. ve 3. Madde)")]
    public float walkSpeed = 0.5f; 
    public float runSpeed = 1.0f;  
    public float patrolRadius = 10f;
    public float detectionRange = 10f; 
    public float attackRange = 1.5f;   
    public float patrolWaitTime = 3f;
    public float chaseTimeout = 10f; // 10 saniye vuramazsa vazgeçsin

    [Header("Stun & Knockback (2. ve 4. Madde)")]
    public float stunDuration = 0.5f;
    public float knockbackForce = 5f; // İtme gücünü arttırdım
    public float knockbackDuration = 0.2f;

    // Durumlar
    private bool isStunned = false;
    private bool isAttacking = false;
    private float lastAttackTime; // Son saldırdığı zaman
    
    // Referanslar
    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;
    private float patrolTimer;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if(audioSource == null) audioSource = GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        currentHealth = maxHealth;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
        if (foundPlayer != null && foundPlayer.transform != this.transform)
        {
            player = foundPlayer.transform;
        }
    }

    private void Update()
    {
        // 10. Madde: Ölünce oyun bozulmasın, sadece return atalım
        if (isDead) return;

        // Mesafeyi ölç
        float distanceToPlayer = (player != null) ? Vector3.Distance(transform.position, player.position) : 999f;

        // --- 3. Madde: Chase Timeout (Takibi Bırakma) ---
        // Eğer kovalıyorsak ama uzun süredir (10sn) vuramadıysak, mesafeyi yapay olarak arttırıp devriyeye zorlayalım
        if (Time.time > lastAttackTime + chaseTimeout && distanceToPlayer > attackRange)
        {
            Patrol(); // Takibi bırak
        }
        else if (distanceToPlayer <= attackRange)
        {
            CombatIdleAndAttack(); 
            lastAttackTime = Time.time; // Menzildeysek süreyi sıfırla
        }
        else if (distanceToPlayer <= detectionRange)
        {
            ChasePlayer(); 
        }
        else
        {
            Patrol(); 
        }

        // --- 1. Madde: Yumuşak Animasyon Geçişi ---
        if (agent != null)
        {
            float currentSpeed = animator.GetFloat("Speed");
            float targetSpeed = agent.velocity.magnitude;
            // Mathf.Lerp ile anlık geçiş yerine yumuşak geçiş yapıyoruz
            animator.SetFloat("Speed", Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f));
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange); 
    }

    #endregion

    #region AI Logic

    private void Patrol()
    {
        if (!agent.isOnNavMesh || isStunned) return;

        agent.speed = walkSpeed; 
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            patrolTimer += Time.deltaTime;
            if (patrolTimer >= patrolWaitTime)
            {
                SetRandomPatrolPoint();
                patrolTimer = 0;
            }
        }
    }

    private void SetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void ChasePlayer()
    {
        if (!agent.isOnNavMesh || isStunned) return;
        agent.speed = runSpeed; 
        agent.SetDestination(player.position);
    }

    private void CombatIdleAndAttack()
    {
        if (!agent.isOnNavMesh || isStunned) return;

        agent.SetDestination(transform.position); // Dur
        
        // Oyuncuya dön
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if(direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (!isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // RASTGELE SALDIRI
        int randomAttack = Random.Range(0, 3); 
        animator.SetInteger("AttackIndex", randomAttack);
        animator.SetTrigger("Attack");

        // 8. Madde: Vuruş Sesi (Swing)
        if(audioSource && swingSound) audioSource.PlayOneShot(swingSound);

        yield return new WaitForSeconds(0.5f); // Vuruş anı

        // Menzil ve Durum Kontrolü
        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 1.0f && !isDead)
        {
            // Stun yemiş olsak bile vuruş çıktıysa hasar verelim mi? 
            // Eğer "Stun yerse vuruş iptal olsun" istiyorsan buraya !isStunned eklersin.
            // Ama sen "vurulmaya devam ettiğinde animasyon çalışsın" dedin, o yüzden stun hasarı engellemiyor.
            
            PlayerCombat playerScript = player.GetComponent<PlayerCombat>();
            if(playerScript != null)
            {
                // Hasar ver + Bizim pozisyonumuzu yolla (İttirmek için)
                bool playerParried = playerScript.TryBlockAttack(damage, transform);

                // 8. Madde: Hit Sesi
                if(audioSource && hitSound && !playerParried) audioSource.PlayOneShot(hitSound);

                if (playerParried)
                {
                    GetStunned(2.0f); // Parry yersek uzun stun yiyelim
                }
            }
        }

        yield return new WaitForSeconds(1f); 
        isAttacking = false; 
    }

    #endregion

    #region Damage & Stun Logic

    // 4. Madde: Attacker parametresi eklendi (Bizi iten kişi)
    public void TakeDamage(int damageAmount, Transform attacker)
    {
        if (isDead) return;

        // 6. Madde: Parry Şansı
        // Stunlıysak parry atamayız
        if (!isStunned && Random.value <= parryChance)
        {
            PerformParry();
            return;
        }

        currentHealth -= damageAmount;
        
        // 2. Madde: Stun yesek bile Hit animasyonu oynasın
        animator.SetTrigger("Hit"); 

        // 9. Madde: Kan Efekti
        if(bloodEffect != null) bloodEffect.Play();

        // 4. Madde: İttirme (Knockback)
        if(attacker != null) StartCoroutine(KnockbackRoutine(attacker));

        if (currentHealth <= 0) Die();
    }

    private void PerformParry()
    {
        animator.SetTrigger("Parry");
        Debug.Log("🛡️ Düşman Parry Attı!");
        
        // Oyuncuyu Stunla
        if (player != null && player.GetComponent<PlayerCombat>())
        {
            player.GetComponent<PlayerCombat>().GetStunned();
        }
    }

    // 5. Madde: Player bizi kısa süreliğine sersemletebilir
    public void GetStunned(float duration = 0.5f)
    {
        if (isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        animator.SetTrigger("Stun"); // Eğer Stun anim yoksa Hit kullanır
        
        if(agent.isOnNavMesh) agent.isStopped = true;

        yield return new WaitForSeconds(duration);

        if(agent.isOnNavMesh) agent.isStopped = false;
        isStunned = false;
    }

    // 4. Madde: Doğru Knockback (Saldırgandan uzağa itilme)
    private IEnumerator KnockbackRoutine(Transform attacker)
    {
        agent.enabled = false; // NavMesh'i kapat ki itilebilelim
        
        Vector3 pushDirection = (transform.position - attacker.position).normalized;
        pushDirection.y = 0; // Havaya uçmayalım

        float timer = 0;
        while(timer < knockbackDuration)
        {
            // Transform.Translate ile geriye kayma
            transform.Translate(pushDirection * knockbackForce * Time.deltaTime, Space.World);
            timer += Time.deltaTime;
            yield return null;
        }

        agent.enabled = true; // Tekrar aç
        // Agent'ı yeni pozisyona snaple
        if(agent.isOnNavMesh) agent.SetDestination(transform.position);
    }

    private void Die()
    {
        isDead = true;
        animator.SetTrigger("Die");
        
        // 10. Madde: Yerde sabit kalması için
        agent.enabled = false; 
        GetComponent<Collider>().enabled = false; // Cesede basılmasın
        this.enabled = false; // Scripti kapat
    }

    #endregion
}