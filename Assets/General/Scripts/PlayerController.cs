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
        
        [Tooltip("Dururken veya koşmaya başlarken animasyonun ne kadar yumuşak geçiş yapacağı. (0.1 = Hızlı, 0.3 = Ağır).")]
        public float animasyonYumusatma = 0.15f; // YENİ AYAR
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
        savasScripti = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        // --- STUN VE ÖLÜM KİLİDİ ---
        if (savasScripti != null && (savasScripti.durum.olduMu || savasScripti.durum.mesgulMu))
        {
            // Stun yediğinde de animasyon aniden kesilmesin, yavaşça sıfıra insin
            animator.SetFloat("Hiz", 0f, hareket.animasyonYumusatma, Time.deltaTime);
            YerCekimiUygula(true); 
            return; 
        }

        HareketEt();
        YerCekimiUygula(false);
    }

    private void HareketEt()
    {
        if (referans.zeminKontrol)
            yerdeMi = Physics.CheckSphere(referans.zeminKontrol.position, referans.zeminMesafe, referans.zeminKatmani);
        
        if (yerdeMi && hizVektoru.y < 0)
        {
            hizVektoru.y = -2f; 
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 girisYonu = new Vector3(x, 0f, z).normalized;

        float hedefAnimHizi = 0f; // Animasyona göndereceğimiz ham değer

        // 1. SAVAS MODU
        if (savasScripti.savas.savasModunda && savasScripti.mevcutHedef != null)
        {
            Vector3 dusmanaYon = (savasScripti.mevcutHedef.position - transform.position).normalized;
            dusmanaYon.y = 0; 
            
            if (dusmanaYon != Vector3.zero)
            {
                Quaternion bakis = Quaternion.LookRotation(dusmanaYon);
                transform.rotation = Quaternion.Slerp(transform.rotation, bakis, Time.deltaTime * 10f);
            }

            Vector3 sagYon = Vector3.Cross(Vector3.up, dusmanaYon); 
            Vector3 hareketYonu = (sagYon * x) + (dusmanaYon * z);
            
            kontrolcu.Move(hareketYonu.normalized * hareket.yurumeHizi * Time.deltaTime);

            hedefAnimHizi = girisYonu.magnitude > 0.1f ? 1f : 0f;
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
                
                hedefAnimHizi = Input.GetKey(KeyCode.LeftShift) ? 2f : 1f;
            }
            else
            {
                hedefAnimHizi = 0f;
            }
        }

        // --- DÜZELTME BURADA ---
        // SetFloat'ın 3. ve 4. parametreleri "DampTime" (Yumuşatma) ve "DeltaTime"dır.
        // Bu sayede BlendTree değerleri anında değişmez, akıcı bir şekilde kayar.
        animator.SetFloat("Hiz", hedefAnimHizi, hareket.animasyonYumusatma, Time.deltaTime);

        if (Input.GetButtonDown("Jump") && yerdeMi)
        {
            hizVektoru.y = Mathf.Sqrt(hareket.ziplamaGucu * -2f * hareket.yerCekimi);
            animator.SetTrigger("Ziplama"); 
        }
    }

    private void YerCekimiUygula(bool kilitliMod)
    {
        if (kilitliMod) { hizVektoru.x = 0; hizVektoru.z = 0; }
        hizVektoru.y += hareket.yerCekimi * Time.deltaTime;
        kontrolcu.Move(hizVektoru * Time.deltaTime);
    }
}