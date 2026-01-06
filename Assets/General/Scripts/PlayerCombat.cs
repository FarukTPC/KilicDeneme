using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    public struct  SpecialAttack
    {
        public string attackName;
        public KeyCode inputKey;
        public string triggerName;
        [Tooltip("Bu animasyon kaç saniye sürüyor? O süre boyunca başka tuşa basılamaz")]
        public float duration;
    }

    #region Variables
    [Header("Basic Attack")]
    [Tooltip("Sol tık ile yapılan temel saldırı")]
    public bool canBasicAttack = true;

    [Tooltip("Sol tık yaptıktan sonra kaç saniye boyunca özel güç (E,Q vs.) KULLANILAMASIN? (Kombo süresi kadar yap)")]
    public float basicAttackBlockDuration = 0.7f;

    [Header("Special Attacks Automation")]
    [Tooltip("Buraya + butonuna basıp istedigin kadar yetenek ekleyebilirsin")]
    public List<SpecialAttack> specialAttacks;

    private bool isBusy = false;
    private float lastBasicAttackTime = -999f;

    private Animator _animator;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (isBusy) return;

        if (canBasicAttack && Input.GetMouseButtonDown(0))
        {
            PerformBasicAttack();
        }

        CheckSpecialAttacks();
    }
    #endregion

    #region Combat Methods

    private void PerformBasicAttack()
    {
        _animator.SetTrigger("Attack");

        lastBasicAttackTime =  Time.time;
    }

    private void CheckSpecialAttacks()
    {
        if (Time.time < lastBasicAttackTime + basicAttackBlockDuration)
        {
            return;
        }
        
        foreach (var skill in specialAttacks)
        {
            if (Input.GetKeyDown(skill.inputKey))
            {
                _animator.SetTrigger(skill.triggerName);
                StartCoroutine(BusyRoutine(skill.duration));
                Debug.Log(skill.attackName + "kullanildi!" + skill.duration + "sn kilitlendi.");
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
    
    #endregion
}