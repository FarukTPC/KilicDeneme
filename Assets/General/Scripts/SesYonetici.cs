using UnityEngine;
using System.Collections.Generic;

public class SesYonetici : MonoBehaviour
{
    public static SesYonetici Instance;

    // Silah tiplerini ayırt etmek için Enum
    public enum SilahSesTuru 
    { 
        StandartKilic, 
        Tekme,         
        Kalkan         
    }

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
        public AudioClip parrySesi;        // Savuşturma (Metal sesi)

        [Header("ÖZEL VURUŞ SESLERİ")]
        public AudioClip kılıcVurmaSesi;   // Kılıç ete değince
        public AudioClip tekmeVurmaSesi;   // Tekme atınca (Küt)
        public AudioClip kalkanVurmaSesi;  // Kalkan vurunca (Metal/Tok)
        
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
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

    // --- GENEL EFEKTLER ---
    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void SallamaSesiVer(Vector3 pos)
    {
        SesCal(efekt.silahSallamaSesi, pos, efekt.sesDuzeyi, 15f);
    }

    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void ParrySesiVer(Vector3 pos)
    {
        SesCal(efekt.parrySesi, pos, efekt.sesDuzeyi, 20f);
    }

    // --- OYUNCU SESLERİ ---
    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void OyuncuHasarCal(Vector3 pos) // Bunu OyuncuHasarSesiVer yapabiliriz ama SilahHasar.cs bunu çağırıyor mu kontrol etmek lazım. Genelde kodlarda bu isim kalmıştı.
    {
        // Rastgele seçim
        if (oyuncu.hasarAlmaSesleri != null && oyuncu.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, oyuncu.hasarAlmaSesleri.Length);
            SesCal(oyuncu.hasarAlmaSesleri[rastgeleIndex], pos, oyuncu.sesDuzeyi, 15f);
        }
    }
    
    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void OyuncuOlumSesiVer(Vector3 pos)
    {
        SesCal(oyuncu.olumSesi, pos, oyuncu.sesDuzeyi, 20f);
    }

    // --- DÜŞMAN SESLERİ ---
    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void DusmanKesifSesiVer(Vector3 pos)
    {
        SesCal(dusman.kesifSesi, pos, dusman.sesDuzeyi, 25f);
    }

    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void DusmanZaferSesiVer(Vector3 pos)
    {
        SesCal(dusman.zaferBagirmasi, pos, dusman.sesDuzeyi, 25f);
    }

    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void DusmanHasarCal(Vector3 pos)
    {
        // Rastgele seçim
        if (dusman.hasarAlmaSesleri != null && dusman.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, dusman.hasarAlmaSesleri.Length);
            SesCal(dusman.hasarAlmaSesleri[rastgeleIndex], pos, dusman.sesDuzeyi, 20f);
        }
    }

    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void DusmanOlumSesiVer(Vector3 pos)
    {
        SesCal(dusman.olumSesi, pos, dusman.sesDuzeyi, 20f);
    }

    // --- SİLAH TÜRÜNE GÖRE VURUŞ SESİ ---
    // SilahHasar scripti burayı çağırır.
    // İSİM DEĞİŞİKLİĞİ: Cal -> Ver
    public void VurusSesiVer(Vector3 pos, SilahSesTuru tur)
    {
        AudioClip calinacakKlip = null;

        switch (tur)
        {
            case SilahSesTuru.StandartKilic:
                calinacakKlip = efekt.kılıcVurmaSesi;
                break;
            case SilahSesTuru.Tekme:
                calinacakKlip = efekt.tekmeVurmaSesi;
                break;
            case SilahSesTuru.Kalkan:
                calinacakKlip = efekt.kalkanVurmaSesi;
                break;
        }

        if (calinacakKlip != null)
        {
            SesCal(calinacakKlip, pos, efekt.sesDuzeyi, 20f);
        }
    }
}