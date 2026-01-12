using UnityEngine;

public class SesYonetici : MonoBehaviour
{
    public static SesYonetici Instance;

    public enum SilahSesTuru {StandartKilic, Tekme, Kalkan }

    [System.Serializable]
    public class OrtamSesleri
    {
        public AudioClip fonMuzigi;
        [Range(0, 1)] public float muzikSesDuzeyi = 0.5f;
    }

    [System.Serializable]
    public class OyuncuSesleri
    {
        [Header("Hasar Sesleri (Array)")]
        [Tooltip("Buraya birden fazla 'Ah/Uh' sesi ekle. Rastgele seçilecek.")]
        public AudioClip[] hasarAlmaSesleri; 
        
        public AudioClip olumSesi;
        
        [Tooltip("Oyuncunun yürürken çalacağı loop sesi.")]
        public AudioClip ayakSesiLoop; 
        
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class DusmanSesleri
    {
        [Tooltip("Düşman oyuncuyu ilk gördüğünde (Huh?) çıkaracağı ses.")]
        public AudioClip kesifSesi; 
        
        public AudioClip zaferBagirmasi;
        
        [Tooltip("Buraya birden fazla hasar sesi ekle.")]
        public AudioClip[] hasarAlmaSesleri;
        
        public AudioClip olumSesi;
        
        [Tooltip("Düşmanın yürürken çalacağı loop sesi.")]
        public AudioClip ayakSesiLoop; 

        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class EfektSesleri
    {
        [Header("GENEL")]
        public AudioClip silahSallamaSesi; // Whoosh
        [Range(0, 1)] public float sallamaSesDuzeyi = 1.0f; // BİREYSEL AYAR

        public AudioClip parrySesi;        // Savuşturma
        [Range(0, 1)] public float parrySesDuzeyi = 1.0f;   // BİREYSEL AYAR

        [Header("ÖZEL VURUŞ SESLERİ")]
        public AudioClip kılıcVurmaSesi;   // Kılıç ete değince
        [Range(0, 1)] public float kılıcVurmaSesDuzeyi = 1.0f; // BİREYSEL AYAR

        public AudioClip tekmeVurmaSesi;   // Tekme atınca (Küt)
        [Range(0, 1)] public float tekmeVurmaSesDuzeyi = 1.0f; // BİREYSEL AYAR

        public AudioClip kalkanVurmaSesi;  // Kalkan vurunca (Metal/Tok)
        [Range(0, 1)] public float kalkanVurmaSesDuzeyi = 1.0f; // BİREYSEL AYAR
    }

    [Header("KATEGORİLER")]
    public OrtamSesleri ortam;
    public OyuncuSesleri oyuncu;
    public DusmanSesleri dusman;
    public EfektSesleri efekt;

    private AudioSource muzikKaynagi;

    private void Awake()
    {
        // Singleton Kurulumu
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Müzik Kaynağı
        muzikKaynagi = gameObject.AddComponent<AudioSource>();
        muzikKaynagi.loop = true;
        
        if (ortam.fonMuzigi)
        {
            muzikKaynagi.clip = ortam.fonMuzigi;
            muzikKaynagi.volume = ortam.muzikSesDuzeyi;
            muzikKaynagi.Play();
        }
    }

    /// <summary>
    /// Belirtilen noktada geçici bir ses objesi oluşturur.
    /// MaxMesafe: Sesin ne kadar uzaktan duyulacağını belirler (Radius).
    /// </summary>
    public void SesCal(AudioClip klip, Vector3 pozisyon, float hacim = 1.0f, float maxMesafe = 20f)
    {
        if (klip == null) return;

        // Geçici obje oluştur
        GameObject sesObjesi = new GameObject("GeçiciSes_" + klip.name);
        sesObjesi.transform.position = pozisyon;

        // AudioSource ekle ve ayarla
        AudioSource source = sesObjesi.AddComponent<AudioSource>();
        source.clip = klip;
        source.volume = hacim;
        source.spatialBlend = 1.0f; // Tamamen 3D
        source.rolloffMode = AudioRolloffMode.Linear; // Mesafeye göre düzgün azalsın
        source.minDistance = 1.0f;
        source.maxDistance = maxMesafe; // RADIUS AYARI
        
        source.Play();

        // Ses bitince objeyi yok et
        Destroy(sesObjesi, klip.length + 0.1f);
    }

    // --- GENEL EFEKTLER (BİREYSEL SES AYARLARI KULLANILIYOR) ---
    
    public void SallamaSesiVer(Vector3 pos)
    {
        SesCal(efekt.silahSallamaSesi, pos, efekt.sallamaSesDuzeyi, 15f);
    }

    public void ParrySesiVer(Vector3 pos)
    {
        SesCal(efekt.parrySesi, pos, efekt.parrySesDuzeyi, 20f);
    }

    // --- OYUNCU SESLERİ ---
    public void OyuncuHasarCal(Vector3 pos)
    {
        if (oyuncu.hasarAlmaSesleri != null && oyuncu.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, oyuncu.hasarAlmaSesleri.Length);
            SesCal(oyuncu.hasarAlmaSesleri[rastgeleIndex], pos, oyuncu.sesDuzeyi, 15f);
        }
    }
    
    public void OyuncuOlumSesiVer(Vector3 pos)
    {
        SesCal(oyuncu.olumSesi, pos, oyuncu.sesDuzeyi, 20f);
    }

    // --- DÜŞMAN SESLERİ ---
    public void DusmanKesifSesiVer(Vector3 pos)
    {
        SesCal(dusman.kesifSesi, pos, dusman.sesDuzeyi, 25f);
    }

    public void DusmanZaferSesiVer(Vector3 pos)
    {
        SesCal(dusman.zaferBagirmasi, pos, dusman.sesDuzeyi, 25f);
    }

    public void DusmanHasarCal(Vector3 pos)
    {
        if (dusman.hasarAlmaSesleri != null && dusman.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, dusman.hasarAlmaSesleri.Length);
            SesCal(dusman.hasarAlmaSesleri[rastgeleIndex], pos, dusman.sesDuzeyi, 20f);
        }
    }

    public void DusmanOlumSesiVer(Vector3 pos)
    {
        SesCal(dusman.olumSesi, pos, dusman.sesDuzeyi, 20f);
    }

    // --- SİLAH TÜRÜNE GÖRE VURUŞ SESİ (BİREYSEL AYARLAR) ---
    public void VurusSesiVer(Vector3 pos, SilahSesTuru tur)
    {
        AudioClip calinacakKlip = null;
        float secilenSesDuzeyi = 1.0f; // Varsayılan

        switch (tur)
        {
            case SilahSesTuru.StandartKilic:
                calinacakKlip = efekt.kılıcVurmaSesi;
                secilenSesDuzeyi = efekt.kılıcVurmaSesDuzeyi; // Kılıç ayarını çek
                break;
            case SilahSesTuru.Tekme:
                calinacakKlip = efekt.tekmeVurmaSesi;
                secilenSesDuzeyi = efekt.tekmeVurmaSesDuzeyi; // Tekme ayarını çek
                break;
            case SilahSesTuru.Kalkan:
                calinacakKlip = efekt.kalkanVurmaSesi;
                secilenSesDuzeyi = efekt.kalkanVurmaSesDuzeyi; // Kalkan ayarını çek
                break;
        }

        if (calinacakKlip != null)
        {
            SesCal(calinacakKlip, pos, secilenSesDuzeyi, 20f);
        }
    }
}