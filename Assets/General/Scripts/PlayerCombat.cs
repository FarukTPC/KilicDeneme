using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    // Yönler
    public enum SaldiriYonu { Yukari = 0, Sag = 1, Sol = 2 }
    
    // Silah Tipi Seçimi (YENİ EKLENDİ)
    public enum SilahTipi { Kilic, Tekme, Kalkan }

    // --- AYAR GRUPLARI ---

    [System.Serializable]
    public class SavasAyarlari
    {
        public bool savasModunda = false;
        public float kilitlenmeMenzili = 10f;
        public float donusHizi = 10f;
        public float girdiTamponSuresi = 0.5f; 
    }

    [System.Serializable]
    public class YonAyarlari
    {
        public SaldiriYonu mevcutYon = SaldiriYonu.Sol;
        public float fareHassasiyeti = 0.1f;
        public float yukariZorlukCarpani = 1.5f; 
    }

    [System.Serializable]
    public class SavusturmaAyarlari 
    {
        public float sersemletmeSuresi = 2.0f;
        public AudioClip basariSesi;
    }

    [System.Serializable]
    public class SaldiriAyarlari
    {
        public float beklemeSuresi = 0.6f;
        public float atilmaGucu = 4.0f; 
        public float atilmaSuresi = 0.2f;
        public int hasar = 15;
        public float vurusSarsilmaSuresi = 0.5f;
    }

    [System.Serializable]
    public class DurumAyarlari
    {
        public int maksimumCan = 100;
        public bool olduMu = false;
        public bool mesgulMu = false; 
    }

    [System.Serializable]
    public class Referanslar
    {
        // --- BURASI DÜZELTİLDİ: ARTIK HEPSİ BURADA ---
        [Header("SİLAH HİTBOX REFERANSLARI")]
        public SilahHasar kilicScripti;  // Kılıç scriptini buraya at
        public SilahHasar tekmeScripti;  // Sağ Ayak scriptini buraya at
        public SilahHasar kalkanScripti; // Kalkan scriptini buraya at
        
        [Header("DİĞER")]
        public LayerMask dusmanKatmani; 
        public TPSCamera kameraScripti; 
        
        [Header("SESLER")]
        public AudioSource sesKaynagi;
        public AudioClip sallamaSesi;
        public AudioClip isabetSesi;

        [Header("SARSINTI")]
        public float sarsintiSuresi = 0.2f;
        public float sarsintiSiddeti = 0.3f;
    }

    [System.Serializable]
    public struct OzelSaldiri
    {
        [Header("TEMEL AYARLAR")]
        public string saldiriAdi;      
        public KeyCode tus;       
        public string animatorTetikleyici;    
        
        [Header("ZAMANLAMALAR")]
        public float animasyonSuresi; 
        public float cooldown; 

        [HideInInspector] public float sonrakiKullanimZamani;

        [Header("HASAR VE ETKİ")]
        public int hasar; 
        public bool sersemletirMi;
        public float sersemletmeSuresi;       
        public float geriItmeGucu;     
        public float geriItmeSuresi;  
        public AudioClip yetenekSesi; 

        // --- YENİ: Script sürüklemek yerine listeden seçiyoruz ---
        [Header("HANGİ UZUV KULLANILACAK?")]
        public SilahTipi kullanilacakSilah; 
    }

    [Header("SAVAŞ AYARLARI")] public SavasAyarlari savas;
    [Header("YÖN VE FARE")] public YonAyarlari yon;
    [Header("SAVUŞTURMA")] public SavusturmaAyarlari savusturma;
    [Header("SALDIRI GÜCÜ")] public SaldiriAyarlari saldiri;
    [Header("KARAKTER DURUMU")] public DurumAyarlari durum;
    [Header("BAĞLANTILAR")] public Referanslar referanslar;
    [Header("ÖZEL YETENEKLER")] public List<OzelSaldiri> ozelSaldirilar;

    // --- GİZLİ DEĞİŞKENLER ---
    private float sonrakiSaldiriZamani = 0f; 
    private int mevcutCan;
    private bool blokluyorMu = false;
    private bool saldiriyorMu = false;
    
    private float sonTiklamaZamani = -1f;

    private Vector3 baslangicPozisyonu;
    private Quaternion baslangicRotasyonu;

    public Transform mevcutHedef; 
    
    private Animator _animator;
    private CharacterController _karakterKontrol;
    private PlayerController _oyuncuHareketScripti;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _karakterKontrol = GetComponent<CharacterController>();
        _oyuncuHareketScripti = GetComponent<PlayerController>();

        if (!referanslar.sesKaynagi) 
        {
            referanslar.sesKaynagi = gameObject.AddComponent<AudioSource>();
        }

        mevcutCan = durum.maksimumCan;
        
        baslangicPozisyonu = transform.position;
        baslangicRotasyonu = transform.rotation;

        if (referanslar.kameraScripti == null && Camera.main) 
        {
            referanslar.kameraScripti = Camera.main.GetComponent<TPSCamera>();
        }
    }

    private void Update()
    {
        if (durum.olduMu) return;
        if (durum.mesgulMu) return;

        if (Input.GetKeyDown(KeyCode.LeftControl)) 
        {
            SavasModunuDegistir();
        }

        if (savas.savasModunda)
        {
            HedefleriTara();
            if (mevcutHedef != null) 
            {
                HedefeDon();
            }
            FareYonunuBelirle(); 
        }
        else
        {
            mevcutHedef = null;
            yon.mevcutYon = SaldiriYonu.Sol; 
            _animator.SetInteger("SaldiriYonu", 2); 
        }

        GirdileriKontrolEt();

        if (saldiriyorMu && Time.time > sonrakiSaldiriZamani + 2.0f)
        {
            saldiriyorMu = false;
        }
    }

    private void SavasModunuDegistir()
    {
        if (savas.savasModunda)
        {
            savas.savasModunda = false;
            mevcutHedef = null;
            _animator.SetBool("SavasModu", false); 
        }
        else
        {
            HedefleriTara(); 
            if (mevcutHedef != null)
            {
                savas.savasModunda = true;
                _animator.SetBool("SavasModu", true);
            }
        }
    }

    private void HedefleriTara()
    {
        Collider[] dusmanlar = Physics.OverlapSphere(transform.position, savas.kilitlenmeMenzili, referanslar.dusmanKatmani);
        float enKisaMesafe = Mathf.Infinity;
        Transform enYakin = null;
        
        foreach (var dusman in dusmanlar)
        {
            float mesafe = Vector3.Distance(transform.position, dusman.transform.position);
            if (dusman.transform != transform && mesafe < enKisaMesafe)
            {
                enKisaMesafe = mesafe;
                enYakin = dusman.transform;
            }
        }
        mevcutHedef = enYakin;
    }

    private void HedefeDon()
    {
        if (mevcutHedef == null) return;

        Vector3 yonVektoru = (mevcutHedef.position - transform.position).normalized;
        yonVektoru.y = 0;
        
        if (yonVektoru != Vector3.zero) 
        {
            Quaternion bakis = Quaternion.LookRotation(yonVektoru);
            transform.rotation = Quaternion.Slerp(transform.rotation, bakis, Time.deltaTime * savas.donusHizi);
        }
    }

    private void FareYonunuBelirle()
    {
        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Mouse Y");
        
        if (Mathf.Abs(x) < yon.fareHassasiyeti && Mathf.Abs(y) < yon.fareHassasiyeti) return;

        if (Mathf.Abs(y) > Mathf.Abs(x) * yon.yukariZorlukCarpani)
        {
            yon.mevcutYon = SaldiriYonu.Yukari;
        }
        else
        {
            yon.mevcutYon = x > 0 ? SaldiriYonu.Sag : SaldiriYonu.Sol;
        }

        if (!saldiriyorMu)
        {
            _animator.SetInteger("SaldiriYonu", (int)yon.mevcutYon);
        }
    }

    private void GirdileriKontrolEt()
    {
        if (Input.GetMouseButton(1)) 
        {
            blokluyorMu = true;
            _animator.SetBool("Blokluyor", true); 
            sonTiklamaZamani = -1f; 
            return;
        }
        else
        {
            blokluyorMu = false;
            _animator.SetBool("Blokluyor", false);
        }

        for (int i = 0; i < ozelSaldirilar.Count; i++)
        {
            if (Input.GetKeyDown(ozelSaldirilar[i].tus) && 
                Time.time >= sonrakiSaldiriZamani && 
                Time.time >= ozelSaldirilar[i].sonrakiKullanimZamani)
            {
                StartCoroutine(OzelYetenekYap(i));
                return; 
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            sonTiklamaZamani = Time.time;
        }

        bool tamponGecerli = (Time.time - sonTiklamaZamani) <= savas.girdiTamponSuresi;
        bool basiliTutuyor = Input.GetMouseButton(0);
        bool saldiriHazir = Time.time >= sonrakiSaldiriZamani;

        if ((tamponGecerli || basiliTutuyor) && saldiriHazir && !saldiriyorMu)
        {
            SaldiriYap();
            sonTiklamaZamani = -1f; 
        }
    }

    public void HasarAl(int gelenHasar, Transform saldiran, int saldiriYonu, float itmeGucu, float itmeSuresi)
    {
        if (durum.olduMu) return;

        if (blokluyorMu)
        {
            if ((int)yon.mevcutYon == saldiriYonu && saldiriYonu != 3) 
            {
                _animator.SetTrigger("SavusturmaBasarili"); 
                
                if (referanslar.sesKaynagi && savusturma.basariSesi) 
                {
                    referanslar.sesKaynagi.PlayOneShot(savusturma.basariSesi);
                }

                SarsintiTetikle();
                
                EnemyAI dusmanScripti = saldiran.GetComponent<EnemyAI>();
                if (dusmanScripti) 
                {
                    dusmanScripti.Sersemle(savusturma.sersemletmeSuresi);
                }
                return; 
            }
        }

        mevcutCan -= gelenHasar;
        
        if (saldiriYonu != 3)
        {
            if (referanslar.sesKaynagi && referanslar.isabetSesi) 
            {
                referanslar.sesKaynagi.PlayOneShot(referanslar.isabetSesi);
            }
        }
        
        SarsintiTetikle();
        
        if (mevcutCan <= 0)
        {
            Ol(saldiriYonu);
            return; 
        }
        
        if (saldiran && itmeGucu > 0) 
        {
            StartCoroutine(GeriTepme(saldiran, itmeGucu, itmeSuresi));
        }
        
        if (!saldiriyorMu && !durum.mesgulMu) 
        {
            _animator.SetTrigger("Hasar"); 
        }
    }

    public void SarsintiTetikle()
    {
        if (referanslar.kameraScripti)
        {
            referanslar.kameraScripti.KamerayiSalla(referanslar.sarsintiSuresi, referanslar.sarsintiSiddeti);
        }
    }

    public void Sersemle(float sure)
    {
        if (durum.olduMu) return;
        StartCoroutine(SersemlemeSureci(sure));
    }

    private IEnumerator SersemlemeSureci(float sure)
    {
        durum.mesgulMu = true; 
        saldiriyorMu = false;
        blokluyorMu = false;
        _animator.SetBool("Blokluyor", false);

        _animator.SetTrigger("Sersemleme"); 
        
        yield return new WaitForSeconds(0.2f);

        float animasyonUzunlugu = _animator.GetCurrentAnimatorStateInfo(0).length;
        float beklemeSuresi = Mathf.Max(sure, animasyonUzunlugu);

        yield return new WaitForSeconds(beklemeSuresi - 0.2f);

        durum.mesgulMu = false;
    }

    private void SaldiriYap() 
    { 
        saldiriyorMu = true; 
        
        sonrakiSaldiriZamani = Time.time + 2.0f; 
        
        _animator.SetInteger("SaldiriYonu", (int)yon.mevcutYon);
        _animator.SetTrigger("Saldiri"); 
        
        if (referanslar.sesKaynagi && referanslar.sallamaSesi) 
        {
            referanslar.sesKaynagi.PlayOneShot(referanslar.sallamaSesi);
        }
        
        StartCoroutine(AtilmaSureci()); 
        
        // Normal saldırıda her zaman Kılıç Scriptini kullan
        StartCoroutine(HitboxYonetimi(referanslar.kilicScripti, saldiri.hasar, false, 0, 0, 0));
        
        StartCoroutine(SaldiriSuresiYonetimi());
    }

    private IEnumerator SaldiriSuresiYonetimi()
    {
        yield return new WaitForSeconds(0.1f);

        float animasyonSuresi = _animator.GetCurrentAnimatorStateInfo(0).length;
        float gercekBekleme = Mathf.Max(animasyonSuresi, saldiri.beklemeSuresi);

        sonrakiSaldiriZamani = Time.time + (gercekBekleme - 0.1f);

        yield return new WaitForSeconds(gercekBekleme - 0.1f);
        
        saldiriyorMu = false;
    }

    private IEnumerator OzelYetenekYap(int yetenekIndex) 
    { 
        OzelSaldiri yetenek = ozelSaldirilar[yetenekIndex];

        sonrakiSaldiriZamani = Time.time + yetenek.animasyonSuresi; 
        yetenek.sonrakiKullanimZamani = Time.time + yetenek.cooldown;
        ozelSaldirilar[yetenekIndex] = yetenek;

        durum.mesgulMu = true; 
        _animator.SetTrigger(yetenek.animatorTetikleyici); 
        
        if (referanslar.sesKaynagi && yetenek.yetenekSesi) 
        {
            referanslar.sesKaynagi.PlayOneShot(yetenek.yetenekSesi);
        }
        
        StartCoroutine(SaldiriDurumuSifirla(yetenek.animasyonSuresi)); 
        
        // --- DÜZELTİLEN KISIM: SİLAH SEÇİMİ ---
        // Seçtiğin Enum değerine göre hangi scripti kullanacağını belirliyoruz.
        SilahHasar secilenSilah = referanslar.kilicScripti; // Varsayılan Kılıç

        if (yetenek.kullanilacakSilah == SilahTipi.Tekme)
        {
            secilenSilah = referanslar.tekmeScripti;
        }
        else if (yetenek.kullanilacakSilah == SilahTipi.Kalkan)
        {
            secilenSilah = referanslar.kalkanScripti;
        }

        // Seçilen silahı hitbox yönetimine gönder
        yield return StartCoroutine(HitboxYonetimi(secilenSilah, yetenek.hasar, yetenek.sersemletirMi, yetenek.sersemletmeSuresi, yetenek.geriItmeGucu, yetenek.geriItmeSuresi, true)); 
        
        durum.mesgulMu = false; 
    }

    private IEnumerator AtilmaSureci() 
    { 
        float zamanlayici = 0; 
        while (zamanlayici < saldiri.atilmaSuresi) 
        { 
            _karakterKontrol.Move(transform.forward * saldiri.atilmaGucu * Time.deltaTime); 
            zamanlayici += Time.deltaTime; 
            yield return null; 
        } 
    }

    private IEnumerator HitboxYonetimi(SilahHasar silah, int hasar, bool sersemlet, float sersSure, float itme, float itmeSure, bool ozelMi = false) 
    { 
        yield return new WaitForSeconds(0.2f); 
        
        if (silah != null)
        {
            int saldiriYonuInt = ozelMi ? 3 : (int)yon.mevcutYon;

            silah.VurusaHazirla(hasar, sersemlet ? sersSure : 0, itme, transform, saldiriYonuInt);
            
            yield return new WaitForSeconds(0.4f);
            
            silah.VurusuBitir();
        }
    }

    private void Ol(int oldurenYon) 
    { 
        if (durum.olduMu) return;

        durum.olduMu = true; 
        savas.savasModunda = false;
        _animator.SetBool("SavasModu", false);

        if (_oyuncuHareketScripti) 
        {
            _oyuncuHareketScripti.enabled = false;
        }
        
        if (oldurenYon == 3) oldurenYon = 1;

        _animator.SetInteger("OlumTipi", oldurenYon); 
        _animator.SetTrigger("Olum"); 

        StartCoroutine(YenidenDogmaSureci());
    }

    private IEnumerator YenidenDogmaSureci()
    {
        yield return new WaitForSeconds(5.0f);

        mevcutCan = durum.maksimumCan;
        durum.olduMu = false;
        durum.mesgulMu = false;
        blokluyorMu = false;
        saldiriyorMu = false;

        _karakterKontrol.enabled = false;
        transform.position = baslangicPozisyonu;
        transform.rotation = baslangicRotasyonu;
        _karakterKontrol.enabled = true;

        _animator.Rebind(); 
        _animator.Update(0f);

        if (_oyuncuHareketScripti) 
        {
            _oyuncuHareketScripti.enabled = true;
        }
    }

    private IEnumerator SaldiriDurumuSifirla(float sure) 
    { 
        yield return new WaitForSeconds(sure); 
        saldiriyorMu = false; 
    }
    
    private IEnumerator GeriTepme(Transform itenKisi, float guc, float sure) 
    { 
        float t = 0; 
        Vector3 yon = (transform.position - itenKisi.position).normalized; 
        yon.y = 0; 
        
        while (t < sure) 
        { 
            if (_karakterKontrol) 
            {
                _karakterKontrol.Move(yon * guc * Time.deltaTime); 
            }
            else 
            {
                transform.position += yon * guc * Time.deltaTime; 
            }
            t += Time.deltaTime; 
            yield return null; 
        } 
    }
}