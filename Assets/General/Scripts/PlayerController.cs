using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class HareketAyarlari
    {
        [Tooltip("Normal yürüme hızı.")]
        public float yurumeHizi = 2.0f;
        [Tooltip("Shift tuşuna basınca ulaşılan koşma hızı.")]
        public float kosmaHizi = 5.0f;
        [Tooltip("Karakterin dönme yumuşaklığı (Düşük = Hızlı döner).")]
        public float donusYumusakligi = 0.1f;
        [Tooltip("Karakterin zıplama yüksekliği.")]
        public float ziplamaGucu = 1.0f;
        [Tooltip("Yer çekimi kuvveti (Negatif olmalı, örn: -9.81).")]
        public float yerCekimi = -9.81f;
    }

    [System.Serializable]
    public class Referanslar
    {
        [Tooltip("Ana kamera transformu (Otomatik bulunur).")]
        public Transform kameraTransform;
        [Tooltip("Karakterin ayaklarının altındaki 'GroundCheck' objesi.")]
        public Transform zeminKontrol;
        [Tooltip("Zemini algılama yarıçapı.")]
        public float zeminMesafe = 0.4f;
        [Tooltip("Hangi katmanlar zemin (yer) olarak kabul edilecek?")]
        public LayerMask zeminKatmani;
    }

    [Header("HAREKET AYARLARI")]
    public HareketAyarlari hareket;
    
    [Header("REFERANSLAR")]
    public Referanslar referans;

    private CharacterController kontrolcu;
    private PlayerCombat savasScripti;
    private Animator animator;

    private Vector3 hizVektoru;
    private bool yerdeMi;
    private float donusHiziRef;

    private void Awake()
    {
        kontrolcu = GetComponent<CharacterController>();
        savasScripti = GetComponent<PlayerCombat>(); // Otomatik bul
        animator = GetComponent<Animator>();

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        // --- STUN VE ÖLÜM KİLİDİ (KESİN ÇÖZÜM) ---
        // Eğer savaş scripti varsa ve karakter ölü ya da meşgulse (stunluysa)
        if (savasScripti != null && (savasScripti.durum.olduMu || savasScripti.durum.mesgulMu))
        {
            // Sadece yer çekimini uygula, ASLA yürüme
            YerCekimiUygula();
            
            // Animasyon hızını zorla sıfırla ki koşuyor gibi görünmesin
            animator.SetFloat("Hiz", 0f);
            return; 
        }
        // ------------------------------------------

        HareketEt();
        YerCekimiUygula();
    }

    private void HareketEt()
    {
        if(referans.zeminKontrol)
            yerdeMi = Physics.CheckSphere(referans.zeminKontrol.position, referans.zeminMesafe, referans.zeminKatmani);
        
        if (yerdeMi && hizVektoru.y < 0)
        {
            hizVektoru.y = -2f; 
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 girisYonu = new Vector3(x, 0f, z).normalized;

        // 1. SAVAS MODU (LOCK-ON YAN YURUME)
        if (savasScripti.savas.savasModunda && savasScripti.mevcutHedef != null)
        {
            Vector3 dusmanaYon = (savasScripti.mevcutHedef.position - transform.position).normalized;
            dusmanaYon.y = 0; 
            
            if (dusmanaYon != Vector3.zero)
            {
                Quaternion bakis = Quaternion.LookRotation(dusmanaYon);
                transform.rotation = Quaternion.Slerp(transform.rotation, bakis, Time.deltaTime * 10f);
            }

            // Hareketi düşmana göre hesapla (İçeri çekilmeyi önleyen kod)
            Vector3 sagYon = Vector3.Cross(Vector3.up, dusmanaYon); 
            Vector3 hareketYonu = (sagYon * x) + (dusmanaYon * z);
            
            kontrolcu.Move(hareketYonu.normalized * hareket.yurumeHizi * Time.deltaTime);

            float animHizi = girisYonu.magnitude > 0.1f ? 1f : 0f;
            animator.SetFloat("Hiz", animHizi, 0.1f, Time.deltaTime); 
        }
        // 2. NORMAL MOD
        else
        {
            if (girisYonu.magnitude >= 0.1f)
            {
                float hedefAci = Mathf.Atan2(girisYonu.x, girisYonu.z) * Mathf.Rad2Deg + referans.kameraTransform.eulerAngles.y;
                float aci = Mathf.SmoothDampAngle(transform.eulerAngles.y, hedefAci, ref donusHiziRef, hareket.donusYumusakligi);
                transform.rotation = Quaternion.Euler(0f, aci, 0f);

                Vector3 hareketYonu = Quaternion.Euler(0f, hedefAci, 0f) * Vector3.forward;
                
                float hiz = Input.GetKey(KeyCode.LeftShift) ? hareket.kosmaHizi : hareket.yurumeHizi;
                kontrolcu.Move(hareketYonu.normalized * hiz * Time.deltaTime);
                
                float animHizi = Input.GetKey(KeyCode.LeftShift) ? 2f : 1f;
                animator.SetFloat("Hiz", animHizi, 0.1f, Time.deltaTime); 
            }
            else
            {
                animator.SetFloat("Hiz", 0, 0.1f, Time.deltaTime);
            }
        }

        if (Input.GetButtonDown("Jump") && yerdeMi)
        {
            hizVektoru.y = Mathf.Sqrt(hareket.ziplamaGucu * -2f * hareket.yerCekimi);
            animator.SetTrigger("Ziplama"); 
        }
    }

    private void YerCekimiUygula()
    {
        hizVektoru.y += hareket.yerCekimi * Time.deltaTime;
        kontrolcu.Move(hizVektoru * Time.deltaTime);
    }
}