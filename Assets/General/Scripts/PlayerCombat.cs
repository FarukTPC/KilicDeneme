using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    // Yönler: 0=Up, 1=Right, 2=Left
    public enum AttackDir { Up = 0, Right = 1, Left = 2 }

    [System.Serializable]
    public struct SpecialAttack
    {
        public string attackName;      
        public KeyCode inputKey;       
        public string triggerName;     
        public float duration;
        public int damage; 
        public bool causesStun;
        public float stunDuration;       
        public float knockbackForce;     
        public float knockbackDuration;  
        public AudioClip skillActionSound; 
    }

    #region Variables

    [Header("Combat Mode & Targeting")]
    public bool isInCombatMode = false;
    private Transform currentTarget; 
    public float lockOnRange = 10f; 
    public float rotationSpeed = 10f;

    [Header("Directional Combat")]
    public AttackDir currentDirection = AttackDir.Left; 
    public float mouseThreshold = 0.1f;

    [Header("Parry System")]
    [Tooltip("Başarılı bir parry sonrası düşmanın ne kadar süre sersemleyeceği")]
    public float parryStunDuration = 2.0f;
    public AudioClip parrySuccessSound;

    [Header("Attack Settings")]
    public float attackCooldown = 0.6f; 
    private float nextAttackTime = 0f;
    
    [Header("Lunge Settings")]
    public float attackLungeForce = 4.0f; 
    public float lungeDuration = 0.2f;

    [Header("Damage Settings")]
    public int basicAttackDamage = 15; 
    public float basicStunDuration = 0.5f; 

    [Header("Status")]
    public int maxHealth = 100;
    private int currentHealth;
    public bool isDead = false;
    private bool isBlocking = false;
    private bool isAttacking = false; 
    private bool isBusy = false;

    [Header("References")]
    public Transform attackPoint;
    public float attackRange = 1.0f;
    public LayerMask enemyLayers; 
    public AudioSource audioSource;
    public AudioClip swingSound;
    public AudioClip hitSound;
    public GameObject hitEffectPrefab; 
    public Transform cameraTransform;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.3f;
    private Vector3 initialCamPos;

    public List<SpecialAttack> specialAttacks;

    private Animator _animator;
    private CharacterController _characterController;

    #endregion

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        currentHealth = maxHealth;
        if (cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        if(cameraTransform) initialCamPos = cameraTransform.localPosition;
    }

    private void Update()
    {
        if (isDead || isBusy) return;

        if (Input.GetKeyDown(KeyCode.LeftControl)) ToggleCombatMode();

        if (isInCombatMode)
        {
            ScanForTargets();
            if (currentTarget != null) FaceTarget();
            
            if (!isAttacking) 
            {
                DetermineMouseDirection();
            }
        }
        else
        {
            currentTarget = null;
            currentDirection = AttackDir.Left; 
            _animator.SetInteger("AttackDirection", 2);
        }

        HandleCombatInput();
    }

    private void ToggleCombatMode()
    {
        if (isInCombatMode)
        {
            isInCombatMode = false;
            currentTarget = null;
            _animator.SetBool("CombatMode", false);
        }
        else
        {
            ScanForTargets(); 
            if (currentTarget != null)
            {
                isInCombatMode = true;
                _animator.SetBool("CombatMode", true);
            }
        }
    }

    private void ScanForTargets()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, lockOnRange, enemyLayers);
        float shortestDist = Mathf.Infinity;
        Transform nearest = null;
        foreach (var enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (enemy.transform != transform && dist < shortestDist)
            {
                shortestDist = dist;
                nearest = enemy.transform;
            }
        }
        currentTarget = nearest;
    }

    private void FaceTarget()
    {
        if(currentTarget == null) return;
        Vector3 dir = (currentTarget.position - transform.position).normalized;
        dir.y = 0;
        if(dir != Vector3.zero) 
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void DetermineMouseDirection()
    {
        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Mouse Y");
        
        if (Mathf.Abs(x) < mouseThreshold && Mathf.Abs(y) < mouseThreshold) return;

        if (Mathf.Abs(x) > Mathf.Abs(y)) currentDirection = x > 0 ? AttackDir.Right : AttackDir.Left;
        else currentDirection = AttackDir.Up;

        _animator.SetInteger("AttackDirection", (int)currentDirection);
    }

    private void HandleCombatInput()
    {
        if (isAttacking && Time.time < nextAttackTime) return;

        // BLOK
        if (Input.GetMouseButton(1)) 
        {
            isBlocking = true;
            _animator.SetBool("IsBlocking", true);
        }
        else
        {
            isBlocking = false;
            _animator.SetBool("IsBlocking", false);
        }

        // SALDIRI
        if (Input.GetMouseButtonDown(0) && !isBlocking)
        {
            if(Time.time >= nextAttackTime)
            {
                PerformAttack();
            }
        }
        
        if (!isBlocking)
        {
            foreach(var skill in specialAttacks)
            {
                 if(Input.GetKeyDown(skill.inputKey) && Time.time >= nextAttackTime)
                 {
                     StartCoroutine(PerformSpecialAttack(skill));
                     break;
                 }
            }
        }
    }

    public void TakeDamage(int damage, Transform attacker, int attackDir, float kbForce, float kbTime)
    {
        if (isDead) return;

        // PARRY KONTROLÜ
        if (isBlocking)
        {
            if ((int)currentDirection == attackDir && attackDir != 3) // 3=Special (Parrylenemez)
            {
                _animator.SetTrigger("ParrySuccess");
                if(audioSource && parrySuccessSound) audioSource.PlayOneShot(parrySuccessSound);
                TriggerShake();
                
                EnemyAI enemyScript = attacker.GetComponent<EnemyAI>();
                if(enemyScript) enemyScript.GetStunned(parryStunDuration);
                return; 
            }
        }

        currentHealth -= damage;
        
        // --- PARTICLE OPTİMİZASYONU ---
        if(hitEffectPrefab) 
        {
            GameObject fx = Instantiate(hitEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(fx, 2.0f); // 2 saniye sonra silinsin
        }
        // ------------------------------

        if(audioSource && hitSound) audioSource.PlayOneShot(hitSound);
        TriggerShake();
        if(attacker && kbForce > 0) StartCoroutine(KnockbackRoutine(attacker, kbForce, kbTime));
        
        if (currentHealth <= 0) Die(attackDir);
        else if (!isAttacking && !isBusy) _animator.SetTrigger("Hit");
    }

    public void GetStunned(float duration)
    {
        if (isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isBusy = true; 
        isAttacking = false;
        isBlocking = false;
        _animator.SetBool("IsBlocking", false);
        _animator.SetTrigger("Hit"); 
        
        yield return new WaitForSeconds(duration);

        isBusy = false;
    }

    private void PerformAttack() { isAttacking = true; nextAttackTime = Time.time + attackCooldown; _animator.SetTrigger("Attack"); if (audioSource && swingSound) audioSource.PlayOneShot(swingSound); StartCoroutine(LungeRoutine()); StartCoroutine(AttackStateRoutine(attackCooldown)); StartCoroutine(AttackHitCheckRoutine(basicAttackDamage, false, 0, 0, 0)); }
    private IEnumerator PerformSpecialAttack(SpecialAttack skill) { isBusy = true; nextAttackTime = Time.time + skill.duration; _animator.SetTrigger(skill.triggerName); if(audioSource && skill.skillActionSound) audioSource.PlayOneShot(skill.skillActionSound); StartCoroutine(AttackStateRoutine(skill.duration)); yield return StartCoroutine(AttackHitCheckRoutine(skill.damage, skill.causesStun, skill.stunDuration, skill.knockbackForce, skill.knockbackDuration, true)); isBusy = false; }
    private IEnumerator LungeRoutine() { float timer = 0; while(timer < lungeDuration) { _characterController.Move(transform.forward * attackLungeForce * Time.deltaTime); timer += Time.deltaTime; yield return null; } }
    private IEnumerator AttackHitCheckRoutine(int damage, bool stun, float stunTime, float kbForce, float kbTime, bool isSpecial = false) { yield return new WaitForSeconds(0.2f); Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers); foreach(var hit in hits) { EnemyAI enemy = hit.GetComponent<EnemyAI>(); if(enemy) { int attackDirInt = isSpecial ? 3 : (int)currentDirection; enemy.TakeDamage(damage, transform, attackDirInt, kbForce, kbTime); if(stun) enemy.GetStunned(stunTime); } } }
    private void Die(int killingBlowDir) { isDead = true; this.enabled = false; _animator.SetInteger("DeathType", killingBlowDir); _animator.SetTrigger("Die"); }
    private IEnumerator AttackStateRoutine(float time) { yield return new WaitForSeconds(time); isAttacking = false; }
    private void TriggerShake() { if(cameraTransform) StartCoroutine(ShakeRoutine()); }
    private IEnumerator ShakeRoutine() { cameraTransform.localPosition = initialCamPos; float e = 0; while(e < shakeDuration) { Vector3 rnd = Random.insideUnitSphere * shakeMagnitude; cameraTransform.localPosition = initialCamPos + rnd; e += Time.deltaTime; yield return null; } cameraTransform.localPosition = initialCamPos; }
    private IEnumerator KnockbackRoutine(Transform attacker, float force, float duration) { float t = 0; Vector3 dir = (transform.position - attacker.position).normalized; dir.y=0; while(t<duration){ if(_characterController) _characterController.Move(dir*force*Time.deltaTime); else transform.position += dir*force*Time.deltaTime; t+=Time.deltaTime; yield return null; } }
}