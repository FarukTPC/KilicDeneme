using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public enum SaldiriYonu 
    { 
        Yukari = 0, 
        Sag = 1, 
        Sol = 2 
    }

    // --- AYAR GRUPLARI ---

    [System.Serializable]
    public class SavasDegerleri
    {
        public int hasar = 10;
        [Range(0f, 1f)] public float savunmaSikligi = 0.6f;
    }

    [System.Serializable]
    public class OzelSaldiriAyarlari
    {
        [Range(0f, 1f)] public float ihtimal = 0.3f;
        public float beklemeSuresi = 5.0f;
        
        [Header("TEKME")]
        public int tekmeHasari = 5;
        public float tekmeSersemletmesi = 1.5f;
        public float tekmeItmeGucu = 6f;
        public AudioClip tekmeSesi;
        
        [Header("KALKAN")]
        public int kalkanHasari = 8;
        public float kalkanSersemletmesi = 1.0f;
        public float kalkanItmeGucu = 3f;
        public AudioClip kalkanSesi;
    }

    [System.Serializable]
    public class SavusturmaAyarlari
    {
        public float sersemletmeSuresi = 2.0f;
        public AudioClip basariSesi;
    }

    [System.Serializable]
    public class SaglikAyarlari
    {
        public int maksimumCan = 100;
        public bool olduMu = false;
        public bool sersemlediMi = false;
    }

    [System.Serializable]
    public class YapayZekaAyarlari
    {
        [Header("MESAFELER")]
        public float saldiriMenzili = 1.5f;
        public float farkEtmeMenzili = 10f;
        public float vazgecmeMenzili = 20f; 
        public float saldiriBeklemeSuresi = 2.0f;

        [Header("HIZ AYARLARI")]
        public float kovalamaHizi = 4.0f;
        public float savasYuruyusHizi = 2.0f;
        public float devriyeHizi = 1.5f;
        public float animasyonYumusatma = 0.15f; 

        [Header("TAKTİKSEL BEKLEME")]
        [Range(0f, 1f)] public float taktikselBeklemeIhtimali = 0.4f;
        public float taktikselBeklemeSuresi = 2.0f;
        [Range(0f, 1f)] public float taktikselParrySansi = 0.5f;

        [Header("DEVRİYE")]
        public float devriyeMenzili = 10f;
        public float devriyeBeklemeSuresi = 3.0f;
    }

    [System.Serializable]
    public class Referanslar
    {
        // --- YENİ EKLENEN SİLAH REFERANSLARI ---
        [Header("SİLAH HİTBOX")]
        [Tooltip("Düşmanın kılıcındaki SilahHasar scripti.")]
        public SilahHasar kilicScripti;
        
        [Tooltip("Düşmanın ayağındaki (Tekme için) SilahHasar scripti.")]
        public SilahHasar tekmeScripti;
        
        [Tooltip("Düşmanın kalkanındaki SilahHasar scripti.")]
        public SilahHasar kalkanScripti;

        [Header("DİĞER")]
        public GameObject vurusEfekti;
        public AudioSource sesKaynagi;
        public AudioClip hasarSesi;
        public AudioClip sallamaSesi;
        public AudioClip zaferSesi;
    }

    [Header("SAVAŞ DEĞERLERİ")] public SavasDegerleri savas;
    [Header("ÖZEL SALDIRI")] public OzelSaldiriAyarlari ozelSaldiri;
    [Header("PARRY SİSTEMİ")] public SavusturmaAyarlari savusturma;
    [Header("SAĞLIK DURUMU")] public SaglikAyarlari saglik;
    [Header("YAPAY ZEKA (AI)")] public YapayZekaAyarlari yapayZeka;
    [Header("REFERANSLAR")] public Referanslar referanslar;

    private int mevcutCan;
    private bool saldiriyorMu = false;
    private bool blokluyorMu = false;
    private bool zaferKutlamasiYaptiMi = false;
    private bool taktikselBeklemeAktif = false;
    private bool oyuncuyuGordu = false;
    private SaldiriYonu mevcutYon = SaldiriYonu.Sag;
    private float sonrakiEylemZamani;
    private float sonrakiOzelSaldiriZamani = 0f;
    private float devriyeZamanlayicisi;
    private Transform oyuncu;
    private PlayerCombat oyuncuScripti; 
    private NavMeshAgent ajan;
    private Animator animator;

    private void Awake()
    {
        ajan = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (!referanslar.sesKaynagi) 
        {
            referanslar.sesKaynagi = gameObject.AddComponent<AudioSource>();
        }

        mevcutCan = saglik.maksimumCan;
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) 
        {
            oyuncu = p.transform;
            oyuncuScripti = p.GetComponent<PlayerCombat>();
        }

        devriyeZamanlayicisi = yapayZeka.devriyeBeklemeSuresi;
        
        if (ajan) 
        {
            ajan.speed = yapayZeka.devriyeHizi;
        }
    }

    private void Update()
    {
        // Stun ve Ölüm Kontrolü (Crash Fix)
        if (saglik.olduMu || saglik.sersemlediMi) 
        {
            if (ajan != null && ajan.isActiveAndEnabled && ajan.isOnNavMesh)
            {
                ajan.isStopped = true;
                ajan.velocity = Vector3.zero; 
                ajan.ResetPath(); 
            }
            if (animator) 
            {
                animator.SetFloat("Hiz", 0f, yapayZeka.animasyonYumusatma, Time.deltaTime);
            }
            return; 
        }

        // Oyuncu öldü mü?
        if (oyuncuScripti != null && oyuncuScripti.durum.olduMu)
        {
            if (!zaferKutlamasiYaptiMi) 
            {
                StartCoroutine(ZaferKutlamasi());
            }
            return;
        }
        else 
        { 
            zaferKutlamasiYaptiMi = false; 
        }

        float mesafe = oyuncu ? Vector3.Distance(transform.position, oyuncu.position) : 999f;

        // Görüş Mantığı
        if (!oyuncuyuGordu) 
        { 
            if (mesafe <= yapayZeka.farkEtmeMenzili) 
            {
                oyuncuyuGordu = true;
            }
        }
        else 
        { 
            if (mesafe > yapayZeka.vazgecmeMenzili) 
            { 
                oyuncuyuGordu = false; 
                taktikselBeklemeAktif = false; 
            } 
        }

        // Aksiyon Kararları
        if (oyuncuyuGordu)
        {
            if (taktikselBeklemeAktif)
            {
                if (mesafe <= yapayZeka.saldiriMenzili * 0.3f) 
                {
                    TaktikselBeklemeIptalVeTuzak();
                }
                else 
                { 
                    if (ajan.isActiveAndEnabled) 
                    {
                        ajan.isStopped = true; 
                    }

                    OyuncuyaDon(); 
                    
                    if (animator) 
                    {
                        animator.SetFloat("Hiz", 0f, yapayZeka.animasyonYumusatma, Time.deltaTime); 
                    }
                    return; 
                }
            }

            if (mesafe <= yapayZeka.saldiriMenzili) 
            { 
                if (ajan.isActiveAndEnabled) 
                {
                    ajan.speed = yapayZeka.savasYuruyusHizi; 
                }
                SavasMantigi(); 
            }
            else 
            { 
                BlokuBirak(); 
                if (ajan.isActiveAndEnabled) 
                {
                    ajan.speed = yapayZeka.kovalamaHizi; 
                }
                Kovala(); 
            }
        }
        else 
        { 
            BlokuBirak(); 
            if (ajan.isActiveAndEnabled) 
            {
                ajan.speed = yapayZeka.devriyeHizi; 
            }
            DevriyeGez(); 
        }

        // Animasyon Güncelleme
        float hedefHiz = 0f;
        if (ajan && !blokluyorMu && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        {
            hedefHiz = ajan.velocity.magnitude;
        }

        if (animator) 
        {
            animator.SetFloat("Hiz", hedefHiz, yapayZeka.animasyonYumusatma, Time.deltaTime);
        }
    }

    private void TaktikselBeklemeIptalVeTuzak() 
    { 
        taktikselBeklemeAktif = false; 
        StopCoroutine("TaktikselBeklemeSureci"); 
        
        OyuncuyaDon(); 
        
        float zar = Random.value; 
        if (zar < yapayZeka.taktikselParrySansi) 
        {
            StartCoroutine(SavunmaYap()); 
        }
        else 
        {
            StartCoroutine(SaldiriYap()); 
        }
    }
    
    private void TaktikselBeklemeKarariVer() 
    { 
        if (Random.value < yapayZeka.taktikselBeklemeIhtimali) 
        { 
            if (!taktikselBeklemeAktif) 
            {
                StartCoroutine(TaktikselBeklemeSureci()); 
            }
        } 
    }
    
    private IEnumerator TaktikselBeklemeSureci() 
    { 
        taktikselBeklemeAktif = true; 
        yield return new WaitForSeconds(yapayZeka.taktikselBeklemeSuresi); 
        taktikselBeklemeAktif = false; 
    }
    
    private IEnumerator ZaferKutlamasi() 
    { 
        zaferKutlamasiYaptiMi = true; 
        if (ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        {
            ajan.isStopped = true; 
        }

        BlokuBirak(); 
        animator.SetTrigger("Zafer"); 
        
        if (referanslar.zaferSesi && referanslar.sesKaynagi) 
        {
            referanslar.sesKaynagi.PlayOneShot(referanslar.zaferSesi); 
        }

        yield return new WaitForSeconds(4.0f); 
        
        if (ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        {
            ajan.isStopped = false; 
        }
    }
    
    private void DevriyeGez() 
    { 
        if (!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return; 
        
        ajan.isStopped = false; 
        if (ajan.remainingDistance <= ajan.stoppingDistance) 
        { 
            devriyeZamanlayicisi += Time.deltaTime; 
            if (devriyeZamanlayicisi >= yapayZeka.devriyeBeklemeSuresi) 
            { 
                Vector3 yeniHedef = RastgeleDevriyeNoktasi(transform.position, yapayZeka.devriyeMenzili); 
                ajan.SetDestination(yeniHedef); 
                devriyeZamanlayicisi = 0; 
            } 
        } 
    }
    
    private Vector3 RastgeleDevriyeNoktasi(Vector3 merkez, float yaricap) 
    { 
        Vector3 rastgeleYon = Random.insideUnitSphere * yaricap; 
        rastgeleYon += merkez; 
        NavMeshHit hit; 
        if (NavMesh.SamplePosition(rastgeleYon, out hit, yaricap, 1)) 
        {
            return hit.position; 
        }
        return merkez; 
    }
    
    private void Kovala() 
    { 
        if (!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return; 
        ajan.isStopped = false; 
        ajan.SetDestination(oyuncu.position); 
    }
    
    private void SavasMantigi() 
    { 
        if (!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return; 
        
        ajan.isStopped = true; 
        OyuncuyaDon(); 
        
        if (!saldiriyorMu && Time.time >= sonrakiEylemZamani) 
        { 
            float karar = Random.value; 
            if (karar < savas.savunmaSikligi) 
            {
                StartCoroutine(SavunmaYap()); 
            }
            else 
            {
                StartCoroutine(SaldiriYap()); 
            }
        } 
    }

    // --- SALDIRI YAPISI (HITBOX ENTEGRASYONLU) ---
    private IEnumerator SaldiriYap()
    {
        BlokuBirak();
        saldiriyorMu = true;
        
        float ozelZar = Random.value;
        bool ozelMi = (ozelZar < ozelSaldiri.ihtimal);
        bool ozelSaldiriHazir = (Time.time >= sonrakiOzelSaldiriZamani);
        
        int guncelHasar = savas.hasar;
        float guncelSersem = 0f;
        float guncelItme = 0f;
        int saldiriYonuInt = 0;
        
        AudioClip oynatilacakSes = referanslar.sallamaSesi;

        // Hangi silahı kullanacağız?
        SilahHasar aktifSilah = referanslar.kilicScripti;

        if (ozelMi && ozelSaldiriHazir)
        {
            sonrakiOzelSaldiriZamani = Time.time + ozelSaldiri.beklemeSuresi;
            
            if (Random.value > 0.5f)
            {
                animator.SetTrigger("Tekme");
                guncelHasar = ozelSaldiri.tekmeHasari;
                guncelSersem = ozelSaldiri.tekmeSersemletmesi;
                guncelItme = ozelSaldiri.tekmeItmeGucu;
                saldiriYonuInt = 3;
                if(ozelSaldiri.tekmeSesi) oynatilacakSes = ozelSaldiri.tekmeSesi;
                
                // Tekme silahını seç
                aktifSilah = referanslar.tekmeScripti;
            }
            else
            {
                animator.SetTrigger("KalkanSaldirisi");
                guncelHasar = ozelSaldiri.kalkanHasari;
                guncelSersem = ozelSaldiri.kalkanSersemletmesi;
                guncelItme = ozelSaldiri.kalkanItmeGucu;
                saldiriYonuInt = 3;
                if(ozelSaldiri.kalkanSesi) oynatilacakSes = ozelSaldiri.kalkanSesi;
                
                // Kalkan silahını seç
                aktifSilah = referanslar.kalkanScripti;
            }
        }
        else
        {
            int rastgeleYon = Random.Range(0, 3);
            mevcutYon = (SaldiriYonu)rastgeleYon;
            animator.SetInteger("SaldiriYonu", (int)mevcutYon);
            yield return new WaitForEndOfFrame();
            animator.SetTrigger("Saldiri");
            saldiriYonuInt = (int)mevcutYon;
        }

        if (referanslar.sesKaynagi && oynatilacakSes) 
        {
            referanslar.sesKaynagi.PlayOneShot(oynatilacakSes);
        }

        // --- AKILLI BEKLEME VE HİTBOX SÜRECİ ---
        // Animasyonun başlaması için 0.1s bekle
        yield return new WaitForSeconds(0.1f);

        float animasyonSuresi = animator.GetCurrentAnimatorStateInfo(0).length;
        
        // Vuruş anı (Örn: animasyonun %40'ı)
        float hasarVurmaZamani = animasyonSuresi * 0.4f;
        float kalanSure = animasyonSuresi - hasarVurmaZamani;

        // Vuruş anına kadar bekle (0.1s beklediğimizi düşüyoruz)
        yield return new WaitForSeconds(hasarVurmaZamani - 0.1f);

        // --- COLLIDER AKTİF ETME ---
        if (aktifSilah != null)
        {
            aktifSilah.VurusaHazirla(guncelHasar, guncelSersem, guncelItme, transform, saldiriYonuInt);
            
            // Silahın açık kalma süresi (Savurma anı kadar, örn: %30)
            StartCoroutine(SilahKapatici(aktifSilah, animasyonSuresi * 0.3f));
        }

        // Animasyonun bitmesini bekle
        yield return new WaitForSeconds(kalanSure);

        sonrakiEylemZamani = Time.time + yapayZeka.saldiriBeklemeSuresi;
        saldiriyorMu = false;
    }
    
    // Yardımcı: Belirli süre sonra silahı kapatır
    private IEnumerator SilahKapatici(SilahHasar silah, float sure)
    {
        yield return new WaitForSeconds(sure);
        
        if (silah != null) 
        {
            silah.VurusuBitir();
        }
    }

    private IEnumerator SavunmaYap()
    {
        blokluyorMu = true;
        int rastgeleYon = Random.Range(0, 3);
        mevcutYon = (SaldiriYonu)rastgeleYon;
        animator.SetInteger("SaldiriYonu", (int)mevcutYon);
        animator.SetBool("Blokluyor", true);
        float bekleme = Random.Range(1.0f, 3.0f);
        yield return new WaitForSeconds(bekleme);
        BlokuBirak();
        sonrakiEylemZamani = Time.time + 0.2f;
    }

    private void BlokuBirak() 
    { 
        blokluyorMu = false; 
        animator.SetBool("Blokluyor", false); 
    }

    public void HasarAl(int dmg, Transform saldiran, int saldiriYonu, float itmeGucu, float itmeSuresi)
    {
        if (saglik.olduMu) return;

        if (blokluyorMu && (int)mevcutYon == saldiriYonu && saldiriYonu != 3)
        {
            animator.SetTrigger("SavusturmaBasarili");
            
            if (referanslar.sesKaynagi && savusturma.basariSesi) 
            {
                referanslar.sesKaynagi.PlayOneShot(savusturma.basariSesi);
            }

            PlayerCombat pc = saldiran.GetComponent<PlayerCombat>();
            if (pc != null) 
            {
                pc.Sersemle(savusturma.sersemletmeSuresi);
            }
            return;
        }

        mevcutCan -= dmg;
        
        // Ses: Sadece normal saldırılarda hasar sesi çal
        if (saldiriYonu != 3)
        {
            if (referanslar.sesKaynagi && referanslar.hasarSesi) 
            {
                referanslar.sesKaynagi.PlayOneShot(referanslar.hasarSesi);
            }
        }
        
        if (mevcutCan <= 0)
        {
            Ol(saldiriYonu);
        }
        else
        {
            if (saldiran && itmeGucu > 0) 
            {
                StartCoroutine(GeriTepme(saldiran, itmeGucu, itmeSuresi));
            }

            if (!saldiriyorMu) 
            { 
                BlokuBirak(); 
                animator.SetTrigger("Hasar"); 
                TaktikselBeklemeKarariVer(); 
            }
            else 
            { 
                saldiriyorMu = false; 
                TaktikselBeklemeKarariVer(); 
            }
        }
    }

    private void Ol(int oldurenYon) 
    { 
        if (saglik.olduMu) return; 
        if (oldurenYon == 3) oldurenYon = 1; 
        StartCoroutine(OlumSureci(oldurenYon)); 
    }
    
    private IEnumerator OlumSureci(int oldurenYon)
    {
        saglik.olduMu = true;
        BlokuBirak();
        
        if (ajan && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        { 
            ajan.isStopped = true; 
            ajan.velocity = Vector3.zero; 
        }

        if (ajan) ajan.enabled = false;
        if (GetComponent<Collider>()) GetComponent<Collider>().enabled = false;
        
        yield return new WaitForEndOfFrame();
        
        animator.SetInteger("OlumTipi", oldurenYon);
        animator.SetTrigger("Olum");
        
        this.enabled = false;
    }

    public void Sersemle(float sure) 
    { 
        if (saglik.olduMu) return; 
        StartCoroutine(SersemlemeSureci(sure)); 
    }
    
    private IEnumerator SersemlemeSureci(float sure)
    {
        saglik.sersemlediMi = true;
        BlokuBirak();
        
        yield return new WaitForEndOfFrame();
        
        animator.SetTrigger("Sersemleme");
        
        if (ajan != null && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        { 
            ajan.isStopped = true; 
            ajan.velocity = Vector3.zero; 
            ajan.ResetPath(); 
        }
        
        yield return new WaitForSeconds(sure);
        
        if (!saglik.olduMu && ajan != null && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
        {
            ajan.isStopped = false; 
        }

        saglik.sersemlediMi = false;
        sonrakiEylemZamani = Time.time + 0.5f;
    }

    private void OyuncuyaDon() 
    { 
        if (!oyuncu) return; 
        
        Vector3 yon = (oyuncu.position - transform.position).normalized; 
        yon.y = 0; 
        
        if (yon != Vector3.zero) 
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(yon), Time.deltaTime * 5f); 
        }
    }
    
    private IEnumerator GeriTepme(Transform saldiran, float guc, float sure)
    {
        if (ajan) ajan.enabled = false;
        
        Vector3 yon = (transform.position - saldiran.position).normalized;
        yon.y = 0;
        float t = 0;
        
        while (t < sure) 
        { 
            if (saglik.olduMu) yield break; 
            
            transform.Translate(yon * guc * Time.deltaTime, Space.World); 
            t += Time.deltaTime; 
            yield return null; 
        }
        
        if (!saglik.olduMu) 
        { 
            if (ajan) ajan.enabled = true; 
            
            if (saglik.sersemlediMi && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
            { 
                ajan.isStopped = true; 
                ajan.velocity = Vector3.zero; 
            } 
        }
    }
}