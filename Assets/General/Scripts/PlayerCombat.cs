using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    public enum SaldiriYonu { Yukari = 0, Sag = 1, Sol = 2 }

    // --- AYAR GRUPLARI ---

    [System.Serializable]
    public class SavasAyarlari
    {
        [Tooltip("Savaş modu (CTRL tuşu) şu an açık mı?")]
        public bool savasModunda = false;
        [Tooltip("Etraftaki düşmanları otomatik kilitlenme (Lock-On) menzili.")]
        public float kilitlenmeMenzili = 10f;
        [Tooltip("Karakterin kilitlendiği düşmana dönme hızı.")]
        public float donusHizi = 10f;
        [Tooltip("Erken tıklamaları ne kadar süre hafızada tutsun?")]
        public float girdiTamponSuresi = 0.5f; 
    }

    [System.Serializable]
    public class YonAyarlari
    {
        public SaldiriYonu mevcutYon = SaldiriYonu.Sol;
        [Tooltip("Farenin sağ/sol/yukarı hareketini algılama hassasiyeti (Ölü bölge).")]
        public float fareHassasiyeti = 0.1f;
        [Tooltip("Yukarı saldırının algılanması için dikey hareketin ne kadar baskın olması gerektiğini belirler.")]
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
        public Transform saldiriNoktasi;
        public float saldiriCapi = 1.0f;
        public LayerMask dusmanKatmani;
        public GameObject vurusEfekti;
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
        [Tooltip("Animasyonun (meşguliyetin) süresi. Bu süre boyunca başka hareket yapılamaz.")]
        public float animasyonSuresi; 
        
        [Tooltip("Bu yeteneğin tekrar kullanılabilmesi için geçmesi gereken süre (Cooldown).")]
        public float cooldown; 

        [HideInInspector] public float sonrakiKullanimZamani;

        [Header("HASAR VE ETKİ")]
        public int hasar; 
        public bool sersemletirMi;
        public float sersemletmeSuresi;       
        public float geriItmeGucu;     
        public float geriItmeSuresi;  
        public AudioClip yetenekSesi; 
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

        if (!referanslar.sesKaynagi) referanslar.sesKaynagi = gameObject.AddComponent<AudioSource>();
        mevcutCan = durum.maksimumCan;
        
        baslangicPozisyonu = transform.position;
        baslangicRotasyonu = transform.rotation;

        if (referanslar.kameraScripti == null && Camera.main) 
            referanslar.kameraScripti = Camera.main.GetComponent<TPSCamera>();
    }

    private void Update()
    {
        if (durum.olduMu) return;
        
        // Burası çok önemli: Meşgulse (Stun, Özel Saldırı vb.) hiçbir input alma.
        if (durum.mesgulMu) return;

        if (Input.GetKeyDown(KeyCode.LeftControl)) SavasModunuDegistir();

        if (savas.savasModunda)
        {
            HedefleriTara();
            if (mevcutHedef != null) HedefeDon();
            FareYonunuBelirle(); 
        }
        else
        {
            mevcutHedef = null;
            yon.mevcutYon = SaldiriYonu.Sol; 
            _animator.SetInteger("SaldiriYonu", 2); 
        }

        GirdileriKontrolEt();

        if (saldiriyorMu && Time.time > sonrakiSaldiriZamani + 1.0f)
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
        if(mevcutHedef == null) return;
        Vector3 yonVektoru = (mevcutHedef.position - transform.position).normalized;
        yonVektoru.y = 0;
        if(yonVektoru != Vector3.zero) 
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

        if(!saldiriyorMu)
            _animator.SetInteger("SaldiriYonu", (int)yon.mevcutYon);
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

        if ((tamponGecerli || basiliTutuyor) && saldiriHazir)
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
            if(referanslar.sesKaynagi && savusturma.basariSesi) 
            {
                referanslar.sesKaynagi.PlayOneShot(savusturma.basariSesi);
            }
            SarsintiTetikle();
            
            EnemyAI dusmanScripti = saldiran.GetComponent<EnemyAI>();
            if(dusmanScripti) 
            {
                dusmanScripti.Sersemle(savusturma.sersemletmeSuresi);
            }
            return; 
        }
    }

    mevcutCan -= gelenHasar;
    
    // Kan Efekti
    if(referanslar.vurusEfekti) 
    {
        Vector3 vurusNoktasi = transform.position + Vector3.up;
        Vector3 kanYonu = (saldiran.position - transform.position).normalized;
        kanYonu += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.5f), Random.Range(-0.5f, 0.5f)); 

        GameObject efekt = Instantiate(referanslar.vurusEfekti, vurusNoktasi, Quaternion.LookRotation(-kanYonu));
        Destroy(efekt, 2.0f);
    }

    // --- SES DÜZELTMESİ BURADA ---
    // Sadece normal saldırılarda (kılıç) isabet sesi çal.
    if (saldiriYonu != 3)
    {
        if(referanslar.sesKaynagi && referanslar.isabetSesi) 
        {
            referanslar.sesKaynagi.PlayOneShot(referanslar.isabetSesi);
        }
    }
    // ----------------------------
    
    SarsintiTetikle();
    
    if (mevcutCan <= 0)
    {
        Ol(saldiriYonu);
        return; 
    }
    
    if(saldiran && itmeGucu > 0) 
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
        if(referanslar.kameraScripti)
            referanslar.kameraScripti.KamerayiSalla(referanslar.sarsintiSuresi, referanslar.sarsintiSiddeti);
    }

    public void Sersemle(float sure)
    {
        if (durum.olduMu) return;
        StartCoroutine(SersemlemeSureci(sure));
    }

    // --- STUN HAREKET FIX ---
    private IEnumerator SersemlemeSureci(float sure)
    {
        // 1. Önce kilitleri vur
        durum.mesgulMu = true; 
        saldiriyorMu = false;
        blokluyorMu = false;
        _animator.SetBool("Blokluyor", false);

        // 2. Animasyonu tetikle
        _animator.SetTrigger("Sersemleme"); 
        
        // 3. Animasyonun devreye girmesi için çok kısa bekle (0.2sn)
        // Bu süre içinde Animator State değişmiş olur.
        yield return new WaitForSeconds(0.2f);

        // 4. ŞİMDİ HESAPLAMA YAP:
        // O an oynayan animasyonun (Stun animasyonu) uzunluğunu al.
        float animasyonUzunlugu = _animator.GetCurrentAnimatorStateInfo(0).length;

        // Bize gelen stun süresi (örn: 2sn) ile animasyon süresini (örn: 2.5sn) kıyasla.
        // HANGİSİ BÜYÜKSE ONU KULLAN.
        // Böylece animasyon bitmeden karakter ayağa kalkıp kaymaz.
        float beklemeSuresi = Mathf.Max(sure, animasyonUzunlugu);

        // İlk baştaki 0.2 saniyelik beklemeyi düşerek kalanı bekle.
        yield return new WaitForSeconds(beklemeSuresi - 0.2f);

        // 5. Kilitleri aç
        durum.mesgulMu = false;
    }

    private void SaldiriYap() 
    { 
        saldiriyorMu = true; 
        sonrakiSaldiriZamani = Time.time + saldiri.beklemeSuresi; 
        
        _animator.SetInteger("SaldiriYonu", (int)yon.mevcutYon);
        _animator.SetTrigger("Saldiri"); 
        
        if (referanslar.sesKaynagi && referanslar.sallamaSesi) referanslar.sesKaynagi.PlayOneShot(referanslar.sallamaSesi); 
        StartCoroutine(AtilmaSureci()); 
        StartCoroutine(SaldiriDurumuSifirla(saldiri.beklemeSuresi)); 
        StartCoroutine(HasarKontrolu(saldiri.hasar, false, 0, 0, 0)); 
    }

    private IEnumerator OzelYetenekYap(int yetenekIndex) 
    { 
        OzelSaldiri yetenek = ozelSaldirilar[yetenekIndex];

        sonrakiSaldiriZamani = Time.time + yetenek.animasyonSuresi; 
        yetenek.sonrakiKullanimZamani = Time.time + yetenek.cooldown;
        ozelSaldirilar[yetenekIndex] = yetenek;

        durum.mesgulMu = true; 
        _animator.SetTrigger(yetenek.animatorTetikleyici); 
        
        if(referanslar.sesKaynagi && yetenek.yetenekSesi) referanslar.sesKaynagi.PlayOneShot(yetenek.yetenekSesi); 
        
        StartCoroutine(SaldiriDurumuSifirla(yetenek.animasyonSuresi)); 
        
        yield return StartCoroutine(HasarKontrolu(yetenek.hasar, yetenek.sersemletirMi, yetenek.sersemletmeSuresi, yetenek.geriItmeGucu, yetenek.geriItmeSuresi, true)); 
        
        durum.mesgulMu = false; 
    }

    private IEnumerator AtilmaSureci() 
    { 
        float zamanlayici = 0; 
        while(zamanlayici < saldiri.atilmaSuresi) 
        { 
            _karakterKontrol.Move(transform.forward * saldiri.atilmaGucu * Time.deltaTime); 
            zamanlayici += Time.deltaTime; 
            yield return null; 
        } 
    }

    private IEnumerator HasarKontrolu(int hasarMiktari, bool sersemlet, float sersSure, float itmeGucu, float itmeSuresi, bool ozelMi = false) 
    { 
        yield return new WaitForSeconds(0.2f); 
        Collider[] carpanlar = Physics.OverlapSphere(referanslar.saldiriNoktasi.position, referanslar.saldiriCapi, referanslar.dusmanKatmani); 
        foreach(var carpan in carpanlar) 
        { 
            EnemyAI dusman = carpan.GetComponent<EnemyAI>(); 
            if(dusman) 
            { 
                Vector3 vurusNoktasi = carpan.ClosestPoint(referanslar.saldiriNoktasi.position);
                Vector3 kanYonu = (dusman.transform.position - transform.position).normalized;
                kanYonu += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.5f), Random.Range(-0.5f, 0.5f)); 

                if(referanslar.vurusEfekti) 
                {
                    GameObject efekt = Instantiate(referanslar.vurusEfekti, vurusNoktasi, Quaternion.LookRotation(kanYonu));
                    float rBoyut = Random.Range(0.8f, 1.2f);
                    efekt.transform.localScale = Vector3.one * rBoyut;
                    Destroy(efekt, 2.0f);
                }

                int saldiriYonuInt = ozelMi ? 3 : (int)yon.mevcutYon; 
                dusman.HasarAl(hasarMiktari, transform, saldiriYonuInt, itmeGucu, itmeSuresi); 
                if(sersemlet) dusman.Sersemle(sersSure); 
            } 
        } 
    }

    private void Ol(int oldurenYon) 
    { 
        if (durum.olduMu) return;

        durum.olduMu = true; 
        savas.savasModunda = false;
        _animator.SetBool("SavasModu", false);

        if (_oyuncuHareketScripti) _oyuncuHareketScripti.enabled = false;
        
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

        if (_oyuncuHareketScripti) _oyuncuHareketScripti.enabled = true;
    }

    private IEnumerator SaldiriDurumuSifirla(float sure) { yield return new WaitForSeconds(sure); saldiriyorMu = false; }
    
    private IEnumerator GeriTepme(Transform itenKisi, float guc, float sure) 
    { 
        float t = 0; 
        Vector3 yon = (transform.position - itenKisi.position).normalized; 
        yon.y=0; 
        while(t<sure) 
        { 
            if(_karakterKontrol) _karakterKontrol.Move(yon*guc*Time.deltaTime); 
            else transform.position += yon*guc*Time.deltaTime; 
            t+=Time.deltaTime; 
            yield return null; 
        } 
    }
}