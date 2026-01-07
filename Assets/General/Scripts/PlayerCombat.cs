using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    // Yönler: 0=Up, 1=Right, 2=Left
    public enum AttackDir { Up = 0, Right = 1, Left = 2 }

    // --- GRUPLAMA SINIFLARI (COLLAPSIBLE CLASSES) ---
    
    [System.Serializable]
    public class CombatSettings
    {
        [Tooltip("Savaş modu aktif mi?")]
        public bool isInCombatMode = false;
        [Tooltip("Otomatik kilitlenme mesafesi.")]
        public float lockOnRange = 10f;
        [Tooltip("Düşmana dönüş hızı.")]
        public float rotationSpeed = 10f;
    }

    [System.Serializable]
    public class DirectionalSettings
    {
        [Tooltip("Şu anki saldırı yönü.")]
        public AttackDir currentDirection = AttackDir.Left;
        [Tooltip("Mouse hassasiyeti.")]
        public float mouseThreshold = 0.1f;
    }

    [System.Serializable]
    public class ParrySettings
    {
        [Tooltip("Parry sonrası düşman ne kadar sersemlesin?")]
        public float stunDuration = 2.0f;
        public AudioClip successSound;
    }

    [System.Serializable]
    public class AttackSettings
    {
        [Tooltip("Saldırı bekleme süresi.")]
        public float cooldown = 0.6f;
        [Tooltip("İleri atılma gücü.")]
        public float lungeForce = 4.0f;
        [Tooltip("İleri atılma süresi.")]
        public float lungeDuration = 0.2f;
        [Tooltip("Temel hasar.")]
        public int damage = 15;
        public float hitStunDuration = 0.5f;
    }

    [System.Serializable]
    public class StatusSettings
    {
        public int maxHealth = 100;
        public bool isDead = false;
        public bool isBusy = false; // Stun vb. durumlarda true olur
    }

    [System.Serializable]
    public class ReferenceSettings
    {
        public Transform attackPoint;
        public float attackRange = 1.0f;
        public LayerMask enemyLayers;
        public GameObject hitEffectPrefab;
        public Transform cameraTransform;
        
        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip swingSound;
        public AudioClip hitSound;

        [Header("Camera Shake")]
        public float shakeDuration = 0.2f;
        public float shakeMagnitude = 0.3f;
    }

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

    // --- ANA DEĞİŞKENLER (INSPECTOR'DA GÖRÜNENLER) ---

    // Artık Inspector'da bu başlıklar açılıp kapanabilir olacak!
    public CombatSettings combat;
    public DirectionalSettings direction;
    public ParrySettings parry;
    public AttackSettings attack;
    public StatusSettings status;
    public ReferenceSettings refs;
    
    [Tooltip("Özel Yetenek Listesi")]
    public List<SpecialAttack> specialAttacks;

    // --- GİZLİ DEĞİŞKENLER (STATE) ---
    private float nextAttackTime = 0f;
    private int currentHealth;
    private bool isBlocking = false;
    private bool isAttacking = false;
    private Transform currentTarget;
    private Vector3 initialCamPos;
    
    private Animator _animator;
    private CharacterController _characterController;
    private PlayerController _playerController;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();

        if (!refs.audioSource) refs.audioSource = gameObject.AddComponent<AudioSource>();
        
        currentHealth = status.maxHealth;
        
        if (refs.cameraTransform == null && Camera.main) refs.cameraTransform = Camera.main.transform;
        if (refs.cameraTransform) initialCamPos = refs.cameraTransform.localPosition;
    }

    private void Update()
    {
        if (status.isDead || status.isBusy) return;

        if (Input.GetKeyDown(KeyCode.LeftControl)) ToggleCombatMode();

        if (combat.isInCombatMode)
        {
            ScanForTargets();
            if (currentTarget != null) FaceTarget();
            
            if (!isAttacking) DetermineMouseDirection();
        }
        else
        {
            currentTarget = null;
            direction.currentDirection = AttackDir.Left; 
            _animator.SetInteger("AttackDirection", 2);
        }

        HandleCombatInput();
    }

    private void ToggleCombatMode()
    {
        if (combat.isInCombatMode)
        {
            combat.isInCombatMode = false;
            currentTarget = null;
            _animator.SetBool("CombatMode", false);
        }
        else
        {
            ScanForTargets(); 
            if (currentTarget != null)
            {
                combat.isInCombatMode = true;
                _animator.SetBool("CombatMode", true);
            }
        }
    }

    private void ScanForTargets()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, combat.lockOnRange, refs.enemyLayers);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * combat.rotationSpeed);
        }
    }

    private void DetermineMouseDirection()
    {
        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Mouse Y");
        
        if (Mathf.Abs(x) < direction.mouseThreshold && Mathf.Abs(y) < direction.mouseThreshold) return;

        if (Mathf.Abs(x) > Mathf.Abs(y)) direction.currentDirection = x > 0 ? AttackDir.Right : AttackDir.Left;
        else direction.currentDirection = AttackDir.Up;

        _animator.SetInteger("AttackDirection", (int)direction.currentDirection);
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
        if (status.isDead) return;

        // PARRY KONTROLÜ
        if (isBlocking)
        {
            if ((int)direction.currentDirection == attackDir && attackDir != 3) 
            {
                _animator.SetTrigger("ParrySuccess");
                if(refs.audioSource && parry.successSound) refs.audioSource.PlayOneShot(parry.successSound);
                TriggerShake();
                
                EnemyAI enemyScript = attacker.GetComponent<EnemyAI>();
                if(enemyScript) enemyScript.GetStunned(parry.stunDuration);
                return; 
            }
        }

        currentHealth -= damage;
        
        if(refs.hitEffectPrefab) 
        {
            GameObject fx = Instantiate(refs.hitEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(fx, 2.0f);
        }

        if(refs.audioSource && refs.hitSound) refs.audioSource.PlayOneShot(refs.hitSound);
        TriggerShake();
        if(attacker && kbForce > 0) StartCoroutine(KnockbackRoutine(attacker, kbForce, kbTime));
        
        if (currentHealth <= 0) Die(attackDir);
        else if (!isAttacking && !status.isBusy) _animator.SetTrigger("Hit");
    }

    public void GetStunned(float duration)
    {
        if (status.isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        status.isBusy = true; 
        isAttacking = false;
        
        isBlocking = false;
        _animator.SetBool("IsBlocking", false);

        if (_playerController != null) _playerController.enabled = false;

        yield return new WaitForEndOfFrame();
        _animator.SetTrigger("Stun"); 
        
        yield return new WaitForSeconds(duration);

        if (_playerController != null) _playerController.enabled = true;
        status.isBusy = false;
    }

    private void PerformAttack() 
    { 
        isAttacking = true; 
        nextAttackTime = Time.time + attack.cooldown; 
        _animator.SetTrigger("Attack"); 
        if (refs.audioSource && refs.swingSound) refs.audioSource.PlayOneShot(refs.swingSound); 
        StartCoroutine(LungeRoutine()); 
        StartCoroutine(AttackStateRoutine(attack.cooldown)); 
        StartCoroutine(AttackHitCheckRoutine(attack.damage, false, 0, 0, 0)); 
    }

    private IEnumerator PerformSpecialAttack(SpecialAttack skill) 
    { 
        status.isBusy = true; 
        nextAttackTime = Time.time + skill.duration; 
        _animator.SetTrigger(skill.triggerName); 
        if(refs.audioSource && skill.skillActionSound) refs.audioSource.PlayOneShot(skill.skillActionSound); 
        StartCoroutine(AttackStateRoutine(skill.duration)); 
        yield return StartCoroutine(AttackHitCheckRoutine(skill.damage, skill.causesStun, skill.stunDuration, skill.knockbackForce, skill.knockbackDuration, true)); 
        status.isBusy = false; 
    }

    private IEnumerator LungeRoutine() 
    { 
        float timer = 0; 
        while(timer < attack.lungeDuration) 
        { 
            _characterController.Move(transform.forward * attack.lungeForce * Time.deltaTime); 
            timer += Time.deltaTime; 
            yield return null; 
        } 
    }

    private IEnumerator AttackHitCheckRoutine(int damage, bool stun, float stunTime, float kbForce, float kbTime, bool isSpecial = false) 
    { 
        yield return new WaitForSeconds(0.2f); 
        Collider[] hits = Physics.OverlapSphere(refs.attackPoint.position, refs.attackRange, refs.enemyLayers); 
        foreach(var hit in hits) 
        { 
            EnemyAI enemy = hit.GetComponent<EnemyAI>(); 
            if(enemy) 
            { 
                int attackDirInt = isSpecial ? 3 : (int)direction.currentDirection; 
                enemy.TakeDamage(damage, transform, attackDirInt, kbForce, kbTime); 
                if(stun) enemy.GetStunned(stunTime); 
            } 
        } 
    }

    private void Die(int killingBlowDir) 
    { 
        status.isDead = true; 
        this.enabled = false; 
        _animator.SetInteger("DeathType", killingBlowDir); 
        _animator.SetTrigger("Die"); 
    }

    private IEnumerator AttackStateRoutine(float time) { yield return new WaitForSeconds(time); isAttacking = false; }
    
    private void TriggerShake() { if(refs.cameraTransform) StartCoroutine(ShakeRoutine()); }
    
    private IEnumerator ShakeRoutine() 
    { 
        refs.cameraTransform.localPosition = initialCamPos; 
        float e = 0; 
        while(e < refs.shakeDuration) 
        { 
            Vector3 rnd = Random.insideUnitSphere * refs.shakeMagnitude; 
            refs.cameraTransform.localPosition = initialCamPos + rnd; 
            e += Time.deltaTime; 
            yield return null; 
        } 
        refs.cameraTransform.localPosition = initialCamPos; 
    }

    private IEnumerator KnockbackRoutine(Transform attacker, float force, float duration) 
    { 
        float t = 0; 
        Vector3 dir = (transform.position - attacker.position).normalized; 
        dir.y=0; 
        while(t<duration) 
        { 
            if(_characterController) _characterController.Move(dir*force*Time.deltaTime); 
            else transform.position += dir*force*Time.deltaTime; 
            t+=Time.deltaTime; 
            yield return null; 
        } 
    }
}