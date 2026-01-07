using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public enum AttackDir { Up = 0, Right = 1, Left = 2 }

    [Header("Combat Stats")]
    public int damage = 10;
    
    [Header("Special Attacks (New)")]
    [Range(0f, 1f)] public float specialAttackChance = 0.3f; // %30 Şans
    public int kickDamage = 5;
    public float kickStunDuration = 1.5f;
    public float kickKnockback = 6f;
    
    public int shieldDamage = 8;
    public float shieldStunDuration = 1.0f;
    public float shieldKnockback = 3f;

    [Header("Parry System")]
    public float parryStunDuration = 2.0f;
    public AudioClip parrySuccessSound;
    [Range(0f, 1f)] public float defenseFrequency = 0.6f; 

    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;
    public bool isDead = false;
    private bool isStunned = false;
    
    private bool isAttacking = false;
    private bool isBlocking = false;
    private AttackDir currentDirection = AttackDir.Right; 

    [Header("Movement & AI")]
    public float attackRange = 1.5f;
    public float detectionRange = 10f;
    public float attackCooldown = 2.0f; 
    private float nextActionTime;

    [Header("References")]
    public GameObject hitEffectPrefab; 
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip swingSound;

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if(!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        currentHealth = maxHealth;
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    private void Update()
    {
        if (isDead || isStunned) return;

        float dist = player ? Vector3.Distance(transform.position, player.position) : 999f;

        if (dist <= attackRange)
        {
            CombatLogic();
        }
        else if (dist <= detectionRange)
        {
            StopBlocking();
            ChasePlayer();
        }
        else
        {
            StopBlocking();
            if(animator) animator.SetFloat("Speed", 0);
        }

        if(agent && !isBlocking) animator.SetFloat("Speed", agent.velocity.magnitude);
        else animator.SetFloat("Speed", 0); 
    }

    private void ChasePlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private void CombatLogic()
    {
        agent.isStopped = true;
        FacePlayer();

        if (!isAttacking && Time.time >= nextActionTime)
        {
            // Yapay Zeka Kararı
            float decision = Random.value;

            if (decision < defenseFrequency)
            {
                StartCoroutine(DefensiveManeuver());
            }
            else
            {
                StartCoroutine(AttackRoutine());
            }
        }
    }

    private IEnumerator AttackRoutine()
    {
        StopBlocking(); 
        isAttacking = true;

        // --- SPECIAL ATTACK KONTROLÜ ---
        float specialRoll = Random.value;
        bool isSpecial = (specialRoll < specialAttackChance);
        
        // Değerler
        int currentDmg = damage;
        float currentStun = 0f;
        float currentKbForce = 0f;
        int attackDirInt = 0;

        if (isSpecial)
        {
            // Tekme mi Kalkan mı? (%50 şans)
            if (Random.value > 0.5f)
            {
                // KICK
                animator.SetTrigger("Kick");
                currentDmg = kickDamage;
                currentStun = kickStunDuration;
                currentKbForce = kickKnockback;
                attackDirInt = 3; // Special (Unparryable)
            }
            else
            {
                // SHIELD
                animator.SetTrigger("ShieldA"); // Senin resimdeki parametre adı
                currentDmg = shieldDamage;
                currentStun = shieldStunDuration;
                currentKbForce = shieldKnockback;
                attackDirInt = 3; // Special
            }
        }
        else
        {
            // NORMAL ATAK
            int randDir = Random.Range(0, 3);
            currentDirection = (AttackDir)randDir;
            animator.SetInteger("AttackDirection", (int)currentDirection);
            
            // Unity Bug Fix: Trigger'ı hemen algılaması için 1 kare bekle
            yield return new WaitForEndOfFrame();
            animator.SetTrigger("Attack");
            attackDirInt = (int)currentDirection;
        }

        if(audioSource && swingSound) audioSource.PlayOneShot(swingSound);

        // --- ANIMASYON SENKRONİZASYONU ---
        // Videodaki "vurmama" sorunu için süreyi biraz arttırdım
        // Eğer özel saldırıysa biraz daha uzun bekleyebiliriz (animasyona bağlı)
        yield return new WaitForSeconds(0.5f); 

        // HASAR KONTROLÜ
        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.8f) // Menzili azıcık arttırdım (Tekme için)
        {
            PlayerCombat pc = player.GetComponent<PlayerCombat>();
            if (pc)
            {
                // Hasar ver + Varsa Stun + Varsa Knockback
                pc.TakeDamage(currentDmg, transform, attackDirInt, currentKbForce, 0.2f);
                if (currentStun > 0) pc.GetStunned(currentStun);
            }
        }

        yield return new WaitForSeconds(1.0f);
        
        nextActionTime = Time.time + 0.5f; 
        isAttacking = false;
    }

    private IEnumerator DefensiveManeuver()
    {
        isBlocking = true;
        int randDir = Random.Range(0, 3);
        currentDirection = (AttackDir)randDir;

        animator.SetInteger("AttackDirection", (int)currentDirection);
        animator.SetBool("IsBlocking", true);

        float waitTime = Random.Range(1.0f, 3.0f);
        yield return new WaitForSeconds(waitTime);

        StopBlocking();
        nextActionTime = Time.time + 0.2f;
    }

    private void StopBlocking()
    {
        isBlocking = false;
        animator.SetBool("IsBlocking", false);
    }

    public void TakeDamage(int dmg, Transform attacker, int attackDir, float kbForce, float kbTime)
    {
        if (isDead) return;

        // Parry Kontrolü
        if (isBlocking && (int)currentDirection == attackDir && attackDir != 3)
        {
            animator.SetTrigger("ParrySuccess"); 
            if(audioSource && parrySuccessSound) audioSource.PlayOneShot(parrySuccessSound);
            
            PlayerCombat pc = attacker.GetComponent<PlayerCombat>();
            if(pc != null) pc.GetStunned(parryStunDuration);
            return;
        }

        currentHealth -= dmg;
        
        // --- PARTICLE OPTIMIZATION ---
        if(hitEffectPrefab)
        {
             GameObject fx = Instantiate(hitEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
             Destroy(fx, 2.0f);
        }
        // -----------------------------

        if(audioSource && hitSound) audioSource.PlayOneShot(hitSound);
        if(attacker && kbForce > 0) StartCoroutine(KnockbackRoutine(attacker, kbForce, kbTime));

        if (currentHealth <= 0)
        {
            Die(attackDir);
        }
        else
        {
            if (!isAttacking) 
            {
                StopBlocking(); 
                animator.SetTrigger("Hit");
            }
        }
    }

    private void Die(int killingDir)
    {
        if(isDead) return;
        isDead = true;
        
        StopBlocking();
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;

        animator.SetInteger("DeathType", killingDir);
        animator.SetTrigger("Die");
        
        this.enabled = false;
    }

    public void GetStunned(float duration)
    {
        if(isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        StopBlocking();
        isAttacking = false;
        
        animator.SetTrigger("Hit"); 
        if(agent.isOnNavMesh) agent.isStopped = true;
        
        yield return new WaitForSeconds(duration);
        
        if(agent.isOnNavMesh) agent.isStopped = false;
        isStunned = false;
        nextActionTime = Time.time + 0.5f;
    }

    private void FacePlayer()
    {
        if(!player) return;
        Vector3 dir = (player.position - transform.position).normalized; dir.y=0;
        if(dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
    }

    private IEnumerator KnockbackRoutine(Transform attacker, float force, float duration)
    {
        agent.enabled = false;
        Vector3 dir = (transform.position - attacker.position).normalized; dir.y=0;
        float t=0;
        while(t<duration) { transform.Translate(dir*force*Time.deltaTime, Space.World); t+=Time.deltaTime; yield return null; }
        agent.enabled = true;
    }
}