using UnityEngine;
using System.Collections;

public class SesYonetici : MonoBehaviour
{
    public static SesYonetici Instance;

    public enum SilahSesTuru { Kilic, Tekme, Kalkan }

    [System.Serializable]
    public class OrtamSesleri
    {
        public AudioClip fonMuzigi;
        public AudioClip savasMuzigi;
        [Range(0, 1)] public float muzikSesDuzeyi = 0.5f;
    }

    [System.Serializable]
    public class HavaDurumuSesleri
    {
        [Header("YAĞMUR AYARLARI")]
        public AudioClip yagmurLoopSesi; 
        [Range(0, 1)] public float yagmurSesDuzeyi = 0.5f; // Sadece yağmur için slider

        [Header("FIRTINA AYARLARI")]
        public AudioClip[] gokGurultusuSesleri; 
        [Range(0, 1)] public float gokGurultusuSesDuzeyi = 1.0f; // Sadece gök gürültüsü için slider
    }

    [System.Serializable]
    public class OyuncuSesleri
    {
        public AudioClip[] hasarAlmaSesleri; 
        public AudioClip olumSesi;
        public AudioClip[] adimSesleri; 
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class DusmanSesleri
    {
        public AudioClip kesifSesi; 
        public AudioClip zaferBagirmasi;
        public AudioClip[] hasarAlmaSesleri;
        public AudioClip olumSesi;
        public AudioClip[] adimSesleri;
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class EfektSesleri
    {
        public AudioClip silahSallamaSesi; 
        public AudioClip parrySesi;        
        public AudioClip kılıcVurmaSesi;   
        public AudioClip tekmeVurmaSesi;   
        public AudioClip kalkanVurmaSesi;  
        [Range(0, 1)] public float sallamaSesDuzeyi = 1.0f;
        [Range(0, 1)] public float parrySesDuzeyi = 1.0f;
        [Range(0, 1)] public float kılıcVurmaSesDuzeyi = 1.0f;
        [Range(0, 1)] public float tekmeVurmaSesDuzeyi = 1.0f;
        [Range(0, 1)] public float kalkanVurmaSesDuzeyi = 1.0f;
    }

    [Header("KATEGORİLER")]
    public OrtamSesleri ortam;
    public HavaDurumuSesleri hava;
    public OyuncuSesleri oyuncu;
    public DusmanSesleri dusman;
    public EfektSesleri efekt;

    private AudioSource muzikKaynagi;
    private AudioSource yagmurKaynagi; // Sadece yağmur için
    private AudioSource firtinaKaynagi; // Sadece gök gürültüsü için

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        // 1. Müzik Kaynağı
        muzikKaynagi = gameObject.AddComponent<AudioSource>();
        muzikKaynagi.loop = true;
        
        // 2. Yağmur Kaynağı (Loop)
        yagmurKaynagi = gameObject.AddComponent<AudioSource>();
        yagmurKaynagi.loop = true;

        // 3. Fırtına Kaynağı (One Shot)
        firtinaKaynagi = gameObject.AddComponent<AudioSource>();
        firtinaKaynagi.loop = false;

        BaslangicMuzigiCal();
    }

    private void BaslangicMuzigiCal()
    {
        if (ortam.fonMuzigi)
        {
            muzikKaynagi.clip = ortam.fonMuzigi;
            muzikKaynagi.volume = ortam.muzikSesDuzeyi;
            muzikKaynagi.Play();
        }
    }

    // --- HAVA DURUMU FONKSİYONLARI ---
    public void YagmurSesiniBaslat()
    {
        if (hava.yagmurLoopSesi != null && !yagmurKaynagi.isPlaying)
        {
            yagmurKaynagi.clip = hava.yagmurLoopSesi;
            // Buradaki ses düzeyi artık sadece YAĞMUR sliderına bağlı
            yagmurKaynagi.volume = hava.yagmurSesDuzeyi; 
            yagmurKaynagi.Play();
        }
    }

    public void GokGurultusuPatlat()
    {
        if (hava.gokGurultusuSesleri != null && hava.gokGurultusuSesleri.Length > 0)
        {
            AudioClip clip = hava.gokGurultusuSesleri[Random.Range(0, hava.gokGurultusuSesleri.Length)];
            
            // Buradaki ses düzeyi artık sadece GÖK GÜRÜLTÜSÜ sliderına bağlı
            firtinaKaynagi.PlayOneShot(clip, hava.gokGurultusuSesDuzeyi);
        }
    }
    // ----------------------------------

    public void MuzikDegistir(bool savasModu)
    {
        AudioClip hedefKlip = savasModu ? ortam.savasMuzigi : ortam.fonMuzigi;
        if (muzikKaynagi.clip == hedefKlip) return;
        if (savasModu && ortam.savasMuzigi == null) return;
        StopAllCoroutines();
        StartCoroutine(MuzikCrossfade(hedefKlip));
    }

    private IEnumerator MuzikCrossfade(AudioClip yeniKlip)
    {
        while (muzikKaynagi.volume > 0.01f) { muzikKaynagi.volume -= Time.deltaTime; yield return null; }
        muzikKaynagi.Stop();
        muzikKaynagi.clip = yeniKlip;
        muzikKaynagi.volume = 0;
        muzikKaynagi.Play();
        while (muzikKaynagi.volume < ortam.muzikSesDuzeyi) { muzikKaynagi.volume += Time.deltaTime; yield return null; }
        muzikKaynagi.volume = ortam.muzikSesDuzeyi;
    }

    public void SesCal(AudioClip klip, Vector3 pozisyon, float hacim = 1.0f, float maxMesafe = 20f)
    {
        if (klip == null) return;
        GameObject sesObjesi = new GameObject("TempAudio_" + klip.name);
        sesObjesi.transform.position = pozisyon;
        AudioSource source = sesObjesi.AddComponent<AudioSource>();
        source.clip = klip;
        source.volume = hacim;
        source.spatialBlend = 1.0f; 
        source.minDistance = 1.0f;
        source.maxDistance = maxMesafe;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
        Destroy(sesObjesi, klip.length + 0.1f);
    }

    public void SallamaSesiVer(Vector3 pos) => SesCal(efekt.silahSallamaSesi, pos, efekt.sallamaSesDuzeyi, 15f);
    public void ParrySesiVer(Vector3 pos) => SesCal(efekt.parrySesi, pos, efekt.parrySesDuzeyi, 20f);
    public void DusmanKesifSesiVer(Vector3 pos) => SesCal(dusman.kesifSesi, pos, dusman.sesDuzeyi, 25f);
    public void DusmanZaferSesiVer(Vector3 pos) => SesCal(dusman.zaferBagirmasi, pos, dusman.sesDuzeyi, 25f);
    public void DusmanOlumSesiVer(Vector3 pos) => SesCal(dusman.olumSesi, pos, dusman.sesDuzeyi, 20f);
    public void OyuncuOlumSesiVer(Vector3 pos) => SesCal(oyuncu.olumSesi, pos, oyuncu.sesDuzeyi, 20f);
    public void OyuncuHasarCal(Vector3 pos) { if (oyuncu.hasarAlmaSesleri != null && oyuncu.hasarAlmaSesleri.Length > 0) { int i = Random.Range(0, oyuncu.hasarAlmaSesleri.Length); SesCal(oyuncu.hasarAlmaSesleri[i], pos, oyuncu.sesDuzeyi, 15f); } }
    public void DusmanHasarCal(Vector3 pos) { if (dusman.hasarAlmaSesleri != null && dusman.hasarAlmaSesleri.Length > 0) { int i = Random.Range(0, dusman.hasarAlmaSesleri.Length); SesCal(dusman.hasarAlmaSesleri[i], pos, dusman.sesDuzeyi, 20f); } }
    public void VurusSesiVer(Vector3 pos, SilahSesTuru tur)
    {
        AudioClip klip = null; float vol = 1.0f;
        switch (tur) { case SilahSesTuru.Kilic: klip = efekt.kılıcVurmaSesi; vol = efekt.kılıcVurmaSesDuzeyi; break; case SilahSesTuru.Tekme: klip = efekt.tekmeVurmaSesi; vol = efekt.tekmeVurmaSesDuzeyi; break; case SilahSesTuru.Kalkan: klip = efekt.kalkanVurmaSesi; vol = efekt.kalkanVurmaSesDuzeyi; break; }
        if (klip != null) SesCal(klip, pos, vol, 20f);
    }
}