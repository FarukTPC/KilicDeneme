using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public enum AttackDir { Up = 0, Right = 1, Left = 2 }

    // --- GRUPLAMA SINIFLARI ---

    [System.Serializable]
    public class CombatStats
    {
        [Tooltip("Temel vuruş hasarı")]
        public int damage = 10;
        [Tooltip("Blok yapma sıklığı (0.0 - 1.0)")]
        [Range(0f, 1f)] public float defenseFrequency = 0.6f;
    }

    [System.Serializable]
    public class SpecialAttackSettings
    {
        [Range(0f, 1f)] public float chance = 0.3f;
        
        [Header("Kick")]
        public int kickDamage = 5;
        public float kickStun = 1.5f;
        public float kickKnockback = 6f;

        [Header("Shield")]
        public int shieldDamage = 8;
        public float shieldStun = 1.0f;
        public float shieldKnockback = 3f;
    }

    [System.Serializable]
    public class ParrySettings
    {
        public float stunDuration = 2.0f;
        public AudioClip successSound;
    }

    [System.Serializable]
    public class HealthSettings
    {
        public int maxHealth = 100;
        public bool isDead = false;
        public bool isStunned = false; // Inspector'dan görmek için buraya koyduk
    }

    [System.Serializable]
    public class AISettings
    {
        public float attackRange = 1.5f;
        public float detectionRange = 10f;
        public float attackCooldown = 2.0f;
    }

    [System.Serializable]
    public class ReferenceSettings
    {
        public GameObject hitEffectPrefab;
        public AudioSource audioSource;
        public AudioClip hitSound;
        public AudioClip swingSound;
    }

    // --- ANA DEĞİŞKENLER ---

    // Inspector'da açılıp kapananlar bunlar:
    public CombatStats stats;
    public SpecialAttackSettings special;
    public ParrySettings parry;
    public HealthSettings health;
    public AISettings ai;
    public ReferenceSettings refs;

    // --- GİZLİ STATE DEĞİŞKENLERİ ---
    private int currentHealth;
    private bool isAttacking = false;
    private bool isBlocking = false;
    private AttackDir currentDirection = AttackDir.Right;
    private float nextActionTime;

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if(!refs.audioSource) refs.audioSource = gameObject.AddComponent<AudioSource>();
        currentHealth = health.maxHealth;
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    private void Update()
    {
        if (health.isDead || health.isStunned) return;

        float dist = player ? Vector3.Distance(transform.position, player.position) : 999f;

        if (dist <= ai.attackRange)
        {
            CombatLogic();
        }
        else if (dist <= ai.detectionRange)
        {
            StopBlocking();
            ChasePlayer();
        }
        else
        {
            StopBlocking();
            if(animator) animator.SetFloat("Speed", 0);
        }

        if(agent != null && agent.isActiveAndEnabled && !isBlocking)
            animator.SetFloat("Speed", agent.velocity.magnitude);
        else animator.SetFloat("Speed", 0); 
    }

    private void ChasePlayer()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private void CombatLogic()
    {
        agent.isStopped = true;
        FacePlayer();

        if (!isAttacking && Time.time >= nextActionTime)
        {
            float decision = Random.value;
            if (decision < stats.defenseFrequency)
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

        float specialRoll = Random.value;
        bool isSpecial = (specialRoll < special.chance);
        
        int currentDmg = stats.damage;
        float currentStun = 0f;
        float currentKbForce = 0f;
        int attackDirInt = 0;

        if (isSpecial)
        {
            if (Random.value > 0.5f)
            {
                animator.SetTrigger("Kick");
                currentDmg = special.kickDamage;
                currentStun = special.kickStun;
                currentKbForce = special.kickKnockback;
                attackDirInt = 3; 
            }
            else
            {
                animator.SetTrigger("ShieldA");
                currentDmg = special.shieldDamage;
                currentStun = special.shieldStun;
                currentKbForce = special.shieldKnockback;
                attackDirInt = 3; 
            }
        }
        else
        {
            int randDir = Random.Range(0, 3);
            currentDirection = (AttackDir)randDir;
            animator.SetInteger("AttackDirection", (int)currentDirection);
            
            yield return new WaitForEndOfFrame();
            animator.SetTrigger("Attack");
            attackDirInt = (int)currentDirection;
        }

        if(refs.audioSource && refs.swingSound) refs.audioSource.PlayOneShot(refs.swingSound);

        yield return new WaitForSeconds(0.5f); 

        if (player != null && Vector3.Distance(transform.position, player.position) <= ai.attackRange + 0.8f)
        {
            PlayerCombat pc = player.GetComponent<PlayerCombat>();
            if (pc)
            {
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
        if (health.isDead) return;

        if (isBlocking && (int)currentDirection == attackDir && attackDir != 3)
        {
            animator.SetTrigger("ParrySuccess"); 
            if(refs.audioSource && parry.successSound) refs.audioSource.PlayOneShot(parry.successSound);
            
            PlayerCombat pc = attacker.GetComponent<PlayerCombat>();
            if(pc != null) pc.GetStunned(parry.stunDuration);
            return;
        }

        currentHealth -= dmg;
        
        if(refs.hitEffectPrefab)
        {
             GameObject fx = Instantiate(refs.hitEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
             Destroy(fx, 2.0f); 
        }

        if(refs.audioSource && refs.hitSound) refs.audioSource.PlayOneShot(refs.hitSound);
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
        if(health.isDead) return;
        health.isDead = true;
        StopBlocking();
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        animator.SetInteger("DeathType", killingDir);
        animator.SetTrigger("Die");
        this.enabled = false;
    }

    public void GetStunned(float duration)
    {
        if(health.isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        health.isStunned = true;
        
        isBlocking = false;
        animator.SetBool("IsBlocking", false);
        
        yield return new WaitForEndOfFrame();

        animator.SetTrigger("Stun"); 
        
        if(agent.isOnNavMesh) agent.isStopped = true;
        
        yield return new WaitForSeconds(duration);
        
        if(agent.isOnNavMesh) agent.isStopped = false;
        health.isStunned = false;
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