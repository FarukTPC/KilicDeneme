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
        public float animasyonYumusatma = 0.15f; 
        
        [Header("AYAK SESİ ZAMANLAMASI")]
        public float yurumeAdimSikligi = 0.5f; // 0.5 saniyede bir adım sesi
        public float kosmaAdimSikligi = 0.3f;  // 0.3 saniyede bir (daha seri)
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
    private AudioSource ayakSesiKaynagi;

    private Vector3 hizVektoru;
    private bool yerdeMi;
    private float donusHiziRef;
    
    // Adım sayacı
    private float adimZamanlayicisi = 0f;

    private void Awake()
    {
        kontrolcu = GetComponent<CharacterController>();
        savasScripti = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        // Ses Kaynağı Ayarı
        ayakSesiKaynagi = GetComponent<AudioSource>();
        if (ayakSesiKaynagi == null) ayakSesiKaynagi = gameObject.AddComponent<AudioSource>();
        
        ayakSesiKaynagi.loop = false; // LOOP KAPALI! Tek tek çalacağız.
        ayakSesiKaynagi.playOnAwake = false;
        ayakSesiKaynagi.spatialBlend = 0.0f; // 2D Ses (Kulağımızın dibinde)
        ayakSesiKaynagi.volume = 1.0f;

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
    }

    // Start'a gerek kalmadı, sesleri anlık çekeceğiz.

    private void Update()
    {
        if (savasScripti != null && (savasScripti.durum.olduMu || savasScripti.durum.mesgulMu))
        {
            animator.SetFloat("Hiz", 0f, hareket.animasyonYumusatma, Time.deltaTime);
            adimZamanlayicisi = 0f; // Sayacı sıfırla
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
        
        if (yerdeMi && hizVektoru.y < 0) hizVektoru.y = -2f; 

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 girisYonu = new Vector3(x, 0f, z).normalized;

        // --- GELİŞMİŞ ADIM SESİ ---
        float anlikHiz = kontrolcu.velocity.magnitude;
        bool hareketEdiyor = anlikHiz > 0.2f && yerdeMi;

        if (hareketEdiyor)
        {
            // Koşuyor muyuz?
            bool kosuyor = Input.GetKey(KeyCode.LeftShift);
            float hedefSure = kosuyor ? hareket.kosmaAdimSikligi : hareket.yurumeAdimSikligi;

            // Sayacı ilerlet
            adimZamanlayicisi += Time.deltaTime;

            // Süre dolunca sesi çal ve sayacı sıfırla
            if (adimZamanlayicisi >= hedefSure)
            {
                RastgeleAdimSesiCal();
                adimZamanlayicisi = 0f;
            }
        }
        else
        {
            // Durunca sayacı fulle ki ilk adımda hemen ses çıksın
            adimZamanlayicisi = hareket.yurumeAdimSikligi;
        }
        // --------------------------

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
            else
            {
                hedefAnimHizi = 0f;
            }
        }

        animator.SetFloat("Hiz", hedefAnimHizi, hareket.animasyonYumusatma, Time.deltaTime);

        if (Input.GetButtonDown("Jump") && yerdeMi)
        {
            hizVektoru.y = Mathf.Sqrt(hareket.ziplamaGucu * -2f * hareket.yerCekimi);
            animator.SetTrigger("Ziplama"); 
            RastgeleAdimSesiCal(); // Zıplayınca da ses çıksın
        }
    }

    private void RastgeleAdimSesiCal()
    {
        // Ses Yöneticisindeki diziden rastgele ses seç
        if (SesYonetici.Instance != null && SesYonetici.Instance.oyuncu.adimSesleri != null && SesYonetici.Instance.oyuncu.adimSesleri.Length > 0)
        {
            int index = Random.Range(0, SesYonetici.Instance.oyuncu.adimSesleri.Length);
            AudioClip secilenSes = SesYonetici.Instance.oyuncu.adimSesleri[index];

            // Varyasyon (Doğallık için)
            ayakSesiKaynagi.pitch = Random.Range(0.9f, 1.1f);
            ayakSesiKaynagi.volume = Random.Range(0.8f, 1.0f);
            
            ayakSesiKaynagi.PlayOneShot(secilenSes);
        }
    }

    private void YerCekimiUygula(bool kilitliMod)
    {
        if (kilitliMod) { hizVektoru.x = 0; hizVektoru.z = 0; }
        hizVektoru.y += hareket.yerCekimi * Time.deltaTime;
        kontrolcu.Move(hizVektoru * Time.deltaTime);
    }
}