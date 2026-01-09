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
        [Tooltip("Savaş modu açık mı?")]
        public bool savasModunda = false;
        [Tooltip("Otomatik kilitlenme menzili.")]
        public float kilitlenmeMenzili = 10f;
        [Tooltip("Düşmana dönme hızı.")]
        public float donusHizi = 10f;
    }

    [System.Serializable]
    public class YonAyarlari
    {
        public SaldiriYonu mevcutYon = SaldiriYonu.Sol;
        [Tooltip("Fare hassasiyeti.")]
        public float fareHassasiyeti = 0.1f;
        [Tooltip("Yukarı saldırı zorluğu (1.5 önerilir).")]
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
        public string saldiriAdi;
        public KeyCode tus;
        public string animatorTetikleyici;
        public float sure;
        public int hasar;
        public bool sersemletirMi;
        public float sersemletmeSuresi;
        public float geriItmeGucu;
        public float geriItmeSuresi;
        public AudioClip yetenekSesi;
    }

    // --- ANA DEĞİŞKENLER ---
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
        
        // Meşgulken (Stun yemişse) işlem yapma
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

        // Güvenlik: Saldırı takılırsa resetle
        if (saldiriyorMu && Time.time > sonrakiSaldiriZamani + 1.0f) 
            saldiriyorMu = false;
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
            yon.mevcutYon = SaldiriYonu.Yukari;
        else 
            yon.mevcutYon = x > 0 ? SaldiriYonu.Sag : SaldiriYonu.Sol;
            
        _animator.SetInteger("SaldiriYonu", (int)yon.mevcutYon);
    }

    private void GirdileriKontrolEt()
    {
        if (saldiriyorMu && Time.time < sonrakiSaldiriZamani) return;

        if (Input.GetMouseButton(1))
        {
            blokluyorMu = true;
            _animator.SetBool("Blokluyor", true);
        }
        else
        {
            blokluyorMu = false;
            _animator.SetBool("Blokluyor", false);
        }

        if (Input.GetMouseButtonDown(0) && !blokluyorMu)
        {
            if(Time.time >= sonrakiSaldiriZamani) SaldiriYap();
        }
        
        if (!blokluyorMu)
        {
            foreach(var yetenek in ozelSaldirilar)
            {
                if(Input.GetKeyDown(yetenek.tus) && Time.time >= sonrakiSaldiriZamani)
                {
                    StartCoroutine(OzelYetenekYap(yetenek));
                    break;
                }
            }
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
                if(referanslar.sesKaynagi && savusturma.basariSesi) referanslar.sesKaynagi.PlayOneShot(savusturma.basariSesi);
                SarsintiTetikle();
                
                EnemyAI dusmanScripti = saldiran.GetComponent<EnemyAI>();
                if(dusmanScripti) dusmanScripti.Sersemle(savusturma.sersemletmeSuresi);
                return;
            }
        }

        mevcutCan -= gelenHasar;
        
        if(referanslar.vurusEfekti)
        {
            GameObject efekt = Instantiate(referanslar.vurusEfekti, transform.position + Vector3.up, Quaternion.identity);
            Destroy(efekt, 2.0f);
        }

        if(referanslar.sesKaynagi && referanslar.isabetSesi) referanslar.sesKaynagi.PlayOneShot(referanslar.isabetSesi);
        SarsintiTetikle();

        if (mevcutCan <= 0)
        {
            Ol(saldiriYonu);
            return;
        }

        if(saldiran && itmeGucu > 0) StartCoroutine(GeriTepme(saldiran, itmeGucu, itmeSuresi));
        if (!saldiriyorMu && !durum.mesgulMu) _animator.SetTrigger("Hasar"); 
    }

    public void SarsintiTetikle()
    {
        if(referanslar.kameraScripti) referanslar.kameraScripti.KamerayiSalla(referanslar.sarsintiSuresi, referanslar.sarsintiSiddeti);
    }
    
    public void Sersemle(float sure)
    {
        if (durum.olduMu) return;
        StartCoroutine(SersemlemeSureci(sure));
    }

    private IEnumerator SersemlemeSureci(float sure)
    {
        durum.mesgulMu = true; // PlayerController bunu okuyup hareketi kesecek
        saldiriyorMu = false;
        blokluyorMu = false;
        _animator.SetBool("Blokluyor", false);

        yield return new WaitForEndOfFrame();
        _animator.SetTrigger("Sersemleme"); 
        
        yield return new WaitForSeconds(sure);
        
        durum.mesgulMu = false; // Hareket tekrar serbest
    }

    private void SaldiriYap()
    {
        saldiriyorMu = true;
        sonrakiSaldiriZamani = Time.time + saldiri.beklemeSuresi;
        _animator.SetTrigger("Saldiri");
        if (referanslar.sesKaynagi && referanslar.sallamaSesi) referanslar.sesKaynagi.PlayOneShot(referanslar.sallamaSesi);
        StartCoroutine(AtilmaSureci());
        StartCoroutine(SaldiriDurumuSifirla(saldiri.beklemeSuresi));
        StartCoroutine(HasarKontrolu(saldiri.hasar, false, 0, 0, 0));
    }

    private IEnumerator OzelYetenekYap(OzelSaldiri yetenek)
    {
        durum.mesgulMu = true;
        sonrakiSaldiriZamani = Time.time + yetenek.sure;
        _animator.SetTrigger(yetenek.animatorTetikleyici);
        if(referanslar.sesKaynagi && yetenek.yetenekSesi) referanslar.sesKaynagi.PlayOneShot(yetenek.yetenekSesi);
        StartCoroutine(SaldiriDurumuSifirla(yetenek.sure));
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
        
        // Animasyon hatasını önlemek için (3) yerine (1)
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

    private IEnumerator SaldiriDurumuSifirla(float sure)
    {
        yield return new WaitForSeconds(sure);
        saldiriyorMu = false;
    }

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