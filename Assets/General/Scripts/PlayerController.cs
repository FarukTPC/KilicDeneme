using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class HareketAyarlari
    {
        public float yurumeHizi = 2.0f;
        public float kosmaHizi = 5.0f;
        public float donusYumusakligi = 0.1f;
        public float ziplamaGucu = 1.0f;
        public float yerCekimi = -9.81f;
        
        [Tooltip("Hız değişimlerinde animasyonun ne kadar yumuşak geçeceği.")]
        public float animasyonYumusatma = 0.15f; 
    }

    [System.Serializable]
    public class Referanslar
    {
        public Transform kameraTransform;
        public Transform zeminKontrol;
        public float zeminMesafe = 0.4f;
        public LayerMask zeminKatmani;
    }

    [Header("HAREKET AYARLARI")]
    public HareketAyarlari hareket;
    
    [Header("REFERANSLAR")]
    public Referanslar referans;

    private CharacterController kontrolcu;
    private PlayerCombat savasScripti;
    private Animator animator;
    
    // Ayak sesi için kaynak
    private AudioSource ayakSesiKaynagi;

    private Vector3 hizVektoru;
    private bool yerdeMi;
    private float donusHiziRef;

    private void Awake()
    {
        kontrolcu = GetComponent<CharacterController>();
        savasScripti = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        // Ses kaynağını ayarla
        ayakSesiKaynagi = GetComponent<AudioSource>();
        if (ayakSesiKaynagi == null) ayakSesiKaynagi = gameObject.AddComponent<AudioSource>();
        
        ayakSesiKaynagi.loop = true; // Döngü açık
        ayakSesiKaynagi.playOnAwake = false; // Başlangıçta sessiz
        ayakSesiKaynagi.spatialBlend = 0.5f; // Player sesi hem 2D hem 3D karışık olsun (net duyulsun)
        ayakSesiKaynagi.volume = 1.0f;

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        // Sesi Manager'dan al ve yükle
        if (SesYonetici.Instance != null && SesYonetici.Instance.oyuncu.ayakSesiLoop != null)
        {
            ayakSesiKaynagi.clip = SesYonetici.Instance.oyuncu.ayakSesiLoop;
        }
    }

    private void Update()
    {
        // Stun veya ölüm durumunda
        if (savasScripti != null && (savasScripti.durum.olduMu || savasScripti.durum.mesgulMu))
        {
            animator.SetFloat("Hiz", 0f, hareket.animasyonYumusatma, Time.deltaTime);
            if (ayakSesiKaynagi.isPlaying) ayakSesiKaynagi.Stop();
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

        float hedefAnimHizi = 0f; 

        // 1. SAVAŞ MODU HAREKETİ
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
        // 2. NORMAL HAREKET
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

        // --- AYAK SESİ MANTIĞI ---
        // Input var mı veya gerçek hız 0.1'den büyük mü?
        bool hareketEdiyor = girisYonu.magnitude > 0.1f || kontrolcu.velocity.magnitude > 0.2f;

        if (hareketEdiyor && yerdeMi)
        {
            if (!ayakSesiKaynagi.isPlaying && ayakSesiKaynagi.clip != null)
            {
                // Robotik sesi engellemek için hafif ton değişimi
                ayakSesiKaynagi.pitch = Random.Range(0.9f, 1.1f);
                ayakSesiKaynagi.Play();
            }
        }
        else
        {
            if (ayakSesiKaynagi.isPlaying)
            {
                ayakSesiKaynagi.Stop();
            }
        }
        // -------------------------

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