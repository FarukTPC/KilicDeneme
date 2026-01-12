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
        // Zıplama gücü tamamen silindi
        public float yerCekimi = -9.81f;
        public float animasyonYumusatma = 0.15f; 
        
        [Header("AYAK SESİ ZAMANLAMASI")]
        public float yurumeAdimSikligi = 0.5f; 
        public float kosmaAdimSikligi = 0.35f; 
    }

    [System.Serializable]
    public class Referanslar
    {
        public Transform kameraTransform;
        [Tooltip("Karakterin topuklarının hizasında boş bir obje olmalı.")]
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
    private AudioSource ayakSesiKaynagi;

    private Vector3 hizVektoru;
    private bool yerdeMi;
    private float donusHiziRef;
    
    private float adimZamanlayicisi = 0f;

    private void Awake()
    {
        // 1. BAŞLANGIÇ KONTROLÜ
        Debug.Log("PlayerController: BAŞLADI. Script aktif.");

        kontrolcu = GetComponent<CharacterController>();
        savasScripti = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        ayakSesiKaynagi = GetComponent<AudioSource>();
        if (ayakSesiKaynagi == null) ayakSesiKaynagi = gameObject.AddComponent<AudioSource>();
        
        ayakSesiKaynagi.loop = false; // Loop kesinlikle kapalı olmalı
        ayakSesiKaynagi.playOnAwake = false;
        ayakSesiKaynagi.spatialBlend = 0.0f; 
        ayakSesiKaynagi.volume = 1.0f;

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (savasScripti != null && (savasScripti.durum.olduMu || savasScripti.durum.mesgulMu))
        {
            animator.SetFloat("Hiz", 0f, hareket.animasyonYumusatma, Time.deltaTime);
            adimZamanlayicisi = 0f;
            YerCekimiUygula(true); 
            return; 
        }

        HareketEt();
        YerCekimiUygula(false);
    }

    private void HareketEt()
    {
        // --- ZEMİN KONTROLÜ ---
        // Eğer ZeminKontrol objesi atanmamışsa, Unity'nin kendi sistemini kullanır.
        if (referans.zeminKontrol != null)
        {
            yerdeMi = Physics.CheckSphere(referans.zeminKontrol.position, referans.zeminMesafe, referans.zeminKatmani);
        }
        else
        {
            yerdeMi = kontrolcu.isGrounded;
            // Eğer referans yoksa konsola uyarı basar ama çalışmaya devam eder
            // Debug.LogWarning("Zemin Kontrol objesi yok, isGrounded kullanılıyor: " + yerdeMi);
        }
        
        if (yerdeMi && hizVektoru.y < 0) hizVektoru.y = -2f; 

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 girisYonu = new Vector3(x, 0f, z).normalized;

        // --- SES SİSTEMİ DEDEKTİFİ ---
        float anlikHiz = kontrolcu.velocity.magnitude;
        
        // Hız 0.1'den büyükse ve yerdeyse yürüyor sayılır
        bool hareketEdiyor = anlikHiz > 0.1f && yerdeMi;

        if (hareketEdiyor)
        {
            bool kosuyor = Input.GetKey(KeyCode.LeftShift);
            float hedefSure = kosuyor ? hareket.kosmaAdimSikligi : hareket.yurumeAdimSikligi;

            adimZamanlayicisi += Time.deltaTime;

            if (adimZamanlayicisi >= hedefSure)
            {
                // Süre doldu, ses çalma emri veriliyor!
                RastgeleAdimSesiCal(kosuyor);
                adimZamanlayicisi = 0f;
            }
        }
        else
        {
            adimZamanlayicisi = hareket.yurumeAdimSikligi;
            
            // Eğer yürüdüğünü sanıyorsun ama ses çıkmıyorsa sebebi budur:
            // if (anlikHiz > 0.1f && !yerdeMi) Debug.LogWarning("Hareket var ama YERDE DEĞİL!");
        }

        // Animasyon
        float hedefAnimHizi = 0f; 
        if (savasScripti.savas.savasModunda && savasScripti.mevcutHedef != null)
        {
            Vector3 dusmanaYon = (savasScripti.mevcutHedef.position - transform.position).normalized;
            dusmanaYon.y = 0; 
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dusmanaYon), Time.deltaTime * 10f);
            Vector3 sagYon = Vector3.Cross(Vector3.up, dusmanaYon); 
            Vector3 hareketYonu = (sagYon * x) + (dusmanaYon * z);
            kontrolcu.Move(hareketYonu.normalized * hareket.yurumeHizi * Time.deltaTime);
            hedefAnimHizi = girisYonu.magnitude > 0.1f ? 1f : 0f;
        }
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
        }

        animator.SetFloat("Hiz", hedefAnimHizi, hareket.animasyonYumusatma, Time.deltaTime);
    }

    private void RastgeleAdimSesiCal(bool kosuyorMu)
    {
        // 1. SES YÖNETİCİSİ VAR MI?
        if (SesYonetici.Instance == null) 
        {
            Debug.LogError("HATA: 'SesYonetici' sahneye ekli değil!");
            return;
        }

        // 2. DİZİ BOŞ MU? (En sık yapılan hata burasıdır)
        if (SesYonetici.Instance.oyuncu.adimSesleri == null || SesYonetici.Instance.oyuncu.adimSesleri.Length == 0)
        {
            Debug.LogError("HATA: SesYonetici > Oyuncu Sesleri > Adim Sesleri listesi BOŞ! Inspector'dan sesleri ekle.");
            return;
        }

        // 3. SES DOSYASI BOŞ MU?
        int index = Random.Range(0, SesYonetici.Instance.oyuncu.adimSesleri.Length);
        AudioClip secilenSes = SesYonetici.Instance.oyuncu.adimSesleri[index];

        if (secilenSes == null)
        {
            Debug.LogError("HATA: Adim Sesleri listesindeki " + index + ". eleman BOŞ (None)!");
            return;
        }

        // --- SES ÇALIYOR ---
        ayakSesiKaynagi.pitch = Random.Range(0.85f, 1.15f);
        
        float minVol = kosuyorMu ? 0.6f : 0.8f;
        float maxVol = kosuyorMu ? 0.8f : 1.0f;
        ayakSesiKaynagi.volume = Random.Range(minVol, maxVol);
        
        ayakSesiKaynagi.PlayOneShot(secilenSes);
        
        // Ses çaldığında konsola yazacak (Bunu görürsen sistem çalışıyor demektir)
        // Debug.Log("SES ÇALDI: " + secilenSes.name); 
    }

    private void YerCekimiUygula(bool kilitliMod)
    {
        if (kilitliMod) { hizVektoru.x = 0; hizVektoru.z = 0; }
        hizVektoru.y += hareket.yerCekimi * Time.deltaTime;
        kontrolcu.Move(hizVektoru * Time.deltaTime);
    }

    // Karakterin altındaki kırmızı küreyi çizer
    private void OnDrawGizmosSelected()
    {
        if (referans.zeminKontrol != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(referans.zeminKontrol.position, referans.zeminMesafe);
        }
    }
}