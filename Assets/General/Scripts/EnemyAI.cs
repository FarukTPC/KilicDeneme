using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public enum SaldiriYonu { Yukari = 0, Sag = 1, Sol = 2 }

    [System.Serializable]
    public class SavasDegerleri
    {
        [Tooltip("Düşmanın normal kılıç vuruşunun hasarı.")]
        public int hasar = 10;
        [Tooltip("Düşmanın blok yapma ihtimali (0.0 = Asla yapmaz, 1.0 = Hep bloklar).")]
        [Range(0f, 1f)] public float savunmaSikligi = 0.6f;
    }

    [System.Serializable]
    public class OzelSaldiriAyarlari
    {
        [Tooltip("Düşmanın özel saldırı yapma şansı.")]
        [Range(0f, 1f)] public float ihtimal = 0.3f;
        
        [Tooltip("Özel saldırılar kullanıldıktan sonra kaç saniye beklensin?")]
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
        public float saldiriBeklemeSuresi = 2.0f;

        [Header("HIZ AYARLARI")]
        public float kovalamaHizi = 4.0f; 
        public float savasYuruyusHizi = 2.0f; 
        public float devriyeHizi = 1.5f;

        [Header("TAKTİKSEL BEKLEME")]
        [Range(0f, 1f)] public float taktikselBeklemeIhtimali = 0.4f;
        public float taktikselBeklemeSuresi = 2.0f;
        [Tooltip("Taktiksel bekleme sırasında oyuncu yaklaşırsa Parry atma şansı.")]
        [Range(0f, 1f)] public float taktikselParrySansi = 0.5f;

        [Header("DEVRİYE")]
        public float devriyeMenzili = 10f;
        public float devriyeBeklemeSuresi = 3.0f;
    }

    [System.Serializable]
    public class Referanslar
    {
        public GameObject vurusEfekti;
        public AudioSource sesKaynagi;
        public AudioClip hasarSesi;
        public AudioClip sallamaSesi; 
        public AudioClip zaferSesi;
    }

    [Header("SAVAŞ DEĞERLERİ")]
    public SavasDegerleri savas;
    [Header("ÖZEL SALDIRI")]
    public OzelSaldiriAyarlari ozelSaldiri;
    [Header("PARRY SİSTEMİ")]
    public SavusturmaAyarlari savusturma;
    [Header("SAĞLIK DURUMU")]
    public SaglikAyarlari saglik;
    [Header("YAPAY ZEKA (AI)")]
    public YapayZekaAyarlari yapayZeka;
    [Header("REFERANSLAR")]
    public Referanslar referanslar;

    // --- GİZLİ ---
    private int mevcutCan;
    private bool saldiriyorMu = false;
    private bool blokluyorMu = false;
    private bool zaferKutlamasiYaptiMi = false;
    private bool taktikselBeklemeAktif = false;

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
        if(!referanslar.sesKaynagi) referanslar.sesKaynagi = gameObject.AddComponent<AudioSource>();
        mevcutCan = saglik.maksimumCan;
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) 
        {
            oyuncu = p.transform;
            oyuncuScripti = p.GetComponent<PlayerCombat>();
        }
        
        devriyeZamanlayicisi = yapayZeka.devriyeBeklemeSuresi;
        if(ajan) ajan.speed = yapayZeka.devriyeHizi; 
    }

    private void Update()
    {
        // 1. ÖLÜM VE STUN KİLİDİ (KESİN ÇÖZÜM)
        if (saglik.olduMu || saglik.sersemlediMi) 
        {
            if(ajan != null && ajan.isActiveAndEnabled && ajan.isOnNavMesh)
            {
                ajan.isStopped = true;
                ajan.velocity = Vector3.zero;
                ajan.ResetPath(); // Hedefi unuttur
            }
            return; // Aşağıdaki kodlara gitme
        }

        // 2. OYUNCU ÖLDÜ MÜ?
        if (oyuncuScripti != null && oyuncuScripti.durum.olduMu)
        {
            if (!zaferKutlamasiYaptiMi) StartCoroutine(ZaferKutlamasi());
            return; 
        }
        else
        {
            zaferKutlamasiYaptiMi = false; 
        }

        float mesafe = oyuncu ? Vector3.Distance(transform.position, oyuncu.position) : 999f;

        // 3. AI KARAR MEKANİZMASI
        
        // ÖNCELİK: Eğer taktiksel bekleme aktifse, mesafeye bakmaksızın bekle (Çok dibine girilmediyse)
        if (taktikselBeklemeAktif)
        {
            // Eğer oyuncu çok dibimize girdiyse (Attack Range'in yarısı kadar), tuzağı tetikle
            if (mesafe <= yapayZeka.saldiriMenzili * 0.8f)
            {
                TaktikselBeklemeIptalVeTuzak();
            }
            else
            {
                // Beklemeye devam et, sadece dön
                if(ajan.isActiveAndEnabled) ajan.isStopped = true;
                OyuncuyaDon();
                
                // Hız animasyonu 0 olsun
                if(animator) animator.SetFloat("Hiz", 0);
                return; // Aşağıdaki kovalama/savaş kodlarını çalıştırma
            }
        }

        // NORMAL DAVRANIŞLAR
        if (mesafe <= yapayZeka.saldiriMenzili)
        {
            // SAVAŞ
            if(ajan.isActiveAndEnabled) ajan.speed = yapayZeka.savasYuruyusHizi;
            SavasMantigi();
        }
        else if (mesafe <= yapayZeka.farkEtmeMenzili)
        {
            // KOVALAMA
            BlokuBirak();
            if(ajan.isActiveAndEnabled) ajan.speed = yapayZeka.kovalamaHizi; 
            Kovala();
        }
        else
        {
            // DEVRİYE
            BlokuBirak();
            if(ajan.isActiveAndEnabled) ajan.speed = yapayZeka.devriyeHizi; 
            DevriyeGez(); 
        }

        // Animasyon hızı
        if(ajan && !blokluyorMu) animator.SetFloat("Hiz", ajan.velocity.magnitude);
        else animator.SetFloat("Hiz", 0); 
    }

    private void TaktikselBeklemeIptalVeTuzak()
    {
        taktikselBeklemeAktif = false;
        StopCoroutine("TaktikselBeklemeSureci");

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
            if (!taktikselBeklemeAktif) StartCoroutine(TaktikselBeklemeSureci());
        }
    }

    private IEnumerator TaktikselBeklemeSureci()
    {
        taktikselBeklemeAktif = true;
        // Oyuncuya bakarak bekle
        yield return new WaitForSeconds(yapayZeka.taktikselBeklemeSuresi);
        taktikselBeklemeAktif = false;
    }

    private IEnumerator ZaferKutlamasi()
    {
        zaferKutlamasiYaptiMi = true;
        ajan.isStopped = true;
        BlokuBirak();
        animator.SetTrigger("Zafer"); 
        if (referanslar.zaferSesi && referanslar.sesKaynagi) 
            referanslar.sesKaynagi.PlayOneShot(referanslar.zaferSesi);
        yield return new WaitForSeconds(4.0f);
        ajan.isStopped = false;
    }

    private void DevriyeGez()
    {
        if(!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return;
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
        if (NavMesh.SamplePosition(rastgeleYon, out hit, yaricap, 1)) return hit.position;
        return merkez; 
    }

    private void Kovala()
    {
        if(!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return;
        ajan.isStopped = false;
        ajan.SetDestination(oyuncu.position);
    }

    private void SavasMantigi()
    {
        if(!ajan.isActiveAndEnabled || !ajan.isOnNavMesh) return;
        ajan.isStopped = true;
        OyuncuyaDon();

        if (!saldiriyorMu && Time.time >= sonrakiEylemZamani)
        {
            float karar = Random.value;
            if (karar < savas.savunmaSikligi) StartCoroutine(SavunmaYap());
            else StartCoroutine(SaldiriYap());
        }
    }

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
            }
            else
            {
                animator.SetTrigger("KalkanSaldirisi"); 
                guncelHasar = ozelSaldiri.kalkanHasari;
                guncelSersem = ozelSaldiri.kalkanSersemletmesi;
                guncelItme = ozelSaldiri.kalkanItmeGucu;
                saldiriYonuInt = 3; 
                if(ozelSaldiri.kalkanSesi) oynatilacakSes = ozelSaldiri.kalkanSesi;
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

        if(referanslar.sesKaynagi && oynatilacakSes) 
            referanslar.sesKaynagi.PlayOneShot(oynatilacakSes);

        yield return new WaitForSeconds(0.5f); 

        if (oyuncu != null && Vector3.Distance(transform.position, oyuncu.position) <= yapayZeka.saldiriMenzili + 0.8f)
        {
            PlayerCombat pc = oyuncu.GetComponent<PlayerCombat>();
            if (pc)
            {
                pc.HasarAl(guncelHasar, transform, saldiriYonuInt, guncelItme, 0.2f);
                if (guncelSersem > 0) pc.Sersemle(guncelSersem);
            }
        }

        yield return new WaitForSeconds(1.0f);
        
        sonrakiEylemZamani = Time.time + 0.5f; 
        saldiriyorMu = false;
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
            if(referanslar.sesKaynagi && savusturma.basariSesi) referanslar.sesKaynagi.PlayOneShot(savusturma.basariSesi);
            PlayerCombat pc = saldiran.GetComponent<PlayerCombat>();
            if(pc != null) pc.Sersemle(savusturma.sersemletmeSuresi);
            return;
        }

        mevcutCan -= dmg;
        
        if(referanslar.vurusEfekti)
        {
             GameObject fx = Instantiate(referanslar.vurusEfekti, transform.position + Vector3.up, Quaternion.identity);
             Destroy(fx, 2.0f); 
        }

        if(referanslar.sesKaynagi && referanslar.hasarSesi) referanslar.sesKaynagi.PlayOneShot(referanslar.hasarSesi);
        if(saldiran && itmeGucu > 0) StartCoroutine(GeriTepme(saldiran, itmeGucu, itmeSuresi));

        if (mevcutCan <= 0)
        {
            Ol(saldiriYonu);
        }
        else
        {
            if (!saldiriyorMu) 
            {
                BlokuBirak(); 
                animator.SetTrigger("Hasar");
                TaktikselBeklemeKarariVer();
            }
        }
    }

    // --- FIX: ÖLÜM ANİMASYONU ---
    private void Ol(int oldurenYon)
    {
        if(saglik.olduMu) return;
        
        // Eğer Özel Vuruş (3) ile öldüyse, Animator bunu tanımayabilir.
        // Bunu 1 (Normal Ön Ölüm) veya 0 olarak değiştirelim ki kesin çalışsın.
        if (oldurenYon == 3) oldurenYon = 1;

        StartCoroutine(OlumSureci(oldurenYon));
    }
    // ----------------------------

    private IEnumerator OlumSureci(int oldurenYon)
    {
        saglik.olduMu = true;
        BlokuBirak();
        
        if(ajan) 
        {
            ajan.isStopped = true;
            ajan.velocity = Vector3.zero;
            ajan.enabled = false;
        }
        if(GetComponent<Collider>()) GetComponent<Collider>().enabled = false;

        yield return new WaitForEndOfFrame();

        animator.SetInteger("OlumTipi", oldurenYon);
        animator.SetTrigger("Olum");
        
        this.enabled = false;
    }

    public void Sersemle(float sure)
    {
        if(saglik.olduMu) return;
        StartCoroutine(SersemlemeSureci(sure));
    }

    private IEnumerator SersemlemeSureci(float sure)
    {
        saglik.sersemlediMi = true;
        BlokuBirak();
        yield return new WaitForEndOfFrame();
        
        animator.SetTrigger("Sersemleme");
        
        // STUN BAŞLAR BAŞLAMAZ DURDUR (Agent aktifse)
        if(ajan != null && ajan.isActiveAndEnabled)
        {
             ajan.isStopped = true;
             ajan.velocity = Vector3.zero;
             ajan.ResetPath();
        }
        
        yield return new WaitForSeconds(sure);
        
        // Stun bitince tekrar aç
        if(ajan != null && ajan.isActiveAndEnabled && ajan.isOnNavMesh) 
            ajan.isStopped = false;
            
        saglik.sersemlediMi = false;
        sonrakiEylemZamani = Time.time + 0.5f;
    }

    private void OyuncuyaDon()
    {
        if(!oyuncu) return;
        Vector3 yon = (oyuncu.position - transform.position).normalized; yon.y=0;
        if(yon != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(yon), Time.deltaTime * 5f);
    }

    private IEnumerator GeriTepme(Transform saldiran, float guc, float sure)
    {
        if(ajan) ajan.enabled = false; // Agent'ı tamamen kapat
        Vector3 yon = (transform.position - saldiran.position).normalized; yon.y=0;
        float t=0;
        while(t<sure) { transform.Translate(yon*guc*Time.deltaTime, Space.World); t+=Time.deltaTime; yield return null; }
        
        if(!saglik.olduMu)
        {
            // Eğer sersemlemişse Agent'ı aç ama HAREKET ETTİRME
            if(saglik.sersemlediMi)
            {
                if(ajan) 
                {
                    ajan.enabled = true;
                    // Tekrar aktif olunca hemen durdur
                    if(ajan.isOnNavMesh)
                    {
                        ajan.isStopped = true;
                        ajan.velocity = Vector3.zero;
                    }
                }
            }
            else
            {
                if(ajan) ajan.enabled = true;
            }
        }
    }
}