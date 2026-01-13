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
        // Zıplama gücü tamamen kaldırıldı
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
    
    // YENİ: Gerçek hızı hesaplamak için önceki pozisyonu tutuyoruz
    private Vector3 oncekiPozisyon;

    private void Awake()
    {
        kontrolcu = GetComponent<CharacterController>();
        savasScripti = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        ayakSesiKaynagi = GetComponent<AudioSource>();
        if (ayakSesiKaynagi == null) ayakSesiKaynagi = gameObject.AddComponent<AudioSource>();
        
        ayakSesiKaynagi.loop = false; 
        ayakSesiKaynagi.playOnAwake = false;
        ayakSesiKaynagi.spatialBlend = 0.0f; 
        ayakSesiKaynagi.volume = 1.0f;

        if (referans.kameraTransform == null && Camera.main != null)
            referans.kameraTransform = Camera.main.transform;
            
        oncekiPozisyon = transform.position;
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
        // 1. ZEMİN KONTROLÜ
        if (referans.zeminKontrol)
        {
            yerdeMi = Physics.CheckSphere(referans.zeminKontrol.position, referans.zeminMesafe, referans.zeminKatmani);
        }
        else
        {
            yerdeMi = kontrolcu.isGrounded;
        }
        
        if (yerdeMi && hizVektoru.y < 0) hizVektoru.y = -2f; 

        // 2. HAREKET INPUTLARI
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 girisYonu = new Vector3(x, 0f, z).normalized;

        // --- MANUEL HIZ HESAPLAMA (KESİN ÇÖZÜM) ---
        Vector3 anlikPozisyon = transform.position;
        float katEdilenMesafe = Vector3.Distance(new Vector3(anlikPozisyon.x, 0, anlikPozisyon.z), 
                                                 new Vector3(oncekiPozisyon.x, 0, oncekiPozisyon.z));
                                                 
        float gercekHiz = katEdilenMesafe / Time.deltaTime;
        
        oncekiPozisyon = anlikPozisyon;

        // HIZ EŞİĞİ: 0.1f
        // SALDIRI KONTROLÜ: Saldırı yaparken ayak sesi çıkmasın
        bool saldiriyor = (savasScripti != null && savasScripti.saldiriyorMu);
        bool hareketEdiyor = (gercekHiz > 0.1f) && yerdeMi && !saldiriyor;

        if (hareketEdiyor)
        {
            bool kosuyor = Input.GetKey(KeyCode.LeftShift);
            float hedefSure = kosuyor ? hareket.kosmaAdimSikligi : hareket.yurumeAdimSikligi;

            adimZamanlayicisi += Time.deltaTime;

            if (adimZamanlayicisi >= hedefSure)
            {
                RastgeleAdimSesiCal(kosuyor);
                adimZamanlayicisi = 0f;
            }
        }
        else
        {
            adimZamanlayicisi = hareket.yurumeAdimSikligi;
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
        if (SesYonetici.Instance == null || SesYonetici.Instance.oyuncu.adimSesleri == null) return;

        if (SesYonetici.Instance.oyuncu.adimSesleri.Length > 0)
        {
            int index = Random.Range(0, SesYonetici.Instance.oyuncu.adimSesleri.Length);
            AudioClip secilenSes = SesYonetici.Instance.oyuncu.adimSesleri[index];

            if (secilenSes != null)
            {
                ayakSesiKaynagi.pitch = Random.Range(0.85f, 1.15f);
                
                // Temel ses seviyesini belirle (Koşarken biraz daha kısık)
                float minVol = kosuyorMu ? 0.6f : 0.8f;
                float maxVol = kosuyorMu ? 0.8f : 1.0f;
                float baseVolume = Random.Range(minVol, maxVol);

                // DÜZELTME BURADA: Ses Yöneticisindeki "Ses Düzeyi" slider değeriyle çarpıyoruz
                ayakSesiKaynagi.volume = baseVolume * SesYonetici.Instance.oyuncu.sesDuzeyi;
                
                ayakSesiKaynagi.PlayOneShot(secilenSes);
            }
        }
    }

    private void YerCekimiUygula(bool kilitliMod)
    {
        if (kilitliMod) { hizVektoru.x = 0; hizVektoru.z = 0; }
        hizVektoru.y += hareket.yerCekimi * Time.deltaTime;
        kontrolcu.Move(hizVektoru * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (referans.zeminKontrol != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(referans.zeminKontrol.position, referans.zeminMesafe);
        }
    }
}