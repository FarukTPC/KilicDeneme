using UnityEngine;
using System.Collections.Generic;

public class SesYonetici : MonoBehaviour
{
    public static SesYonetici Instance;

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
        [Header("Çoklu Sesler")]
        [Tooltip("Buraya birden fazla hasar sesi ekleyebilirsin (Ah, Uh, Oof).")]
        public AudioClip[] hasarAlmaSesleri; 
        
        public AudioClip olum;
        public AudioClip ayakSesiLoop; // Yürüme sesi (Loop modunda çalınacak)
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class DusmanSesleri
    {
        [Tooltip("Düşman oyuncuyu ilk fark ettiğinde (Huh?) çıkaracağı ses.")]
        public AudioClip kesifSesi; 
        
        public AudioClip zaferBagirmasi;
        
        [Tooltip("Buraya birden fazla hasar sesi ekleyebilirsin.")]
        public AudioClip[] hasarAlmaSesleri;
        
        public AudioClip olum;
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class EfektSesleri
    {
        [Header("GENEL")]
        public AudioClip saldiriSallama; // Whoosh

        [Header("VURUŞ (TEMAS)")]
        public AudioClip standartVurus; 
        public AudioClip tekmeVurusu;   
        public AudioClip kalkanVurusu;  
        
        [Header("DİĞER")]
        public AudioClip parryBasarili;
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
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        muzikKaynagi = gameObject.AddComponent<AudioSource>();
        muzikKaynagi.loop = true;
        
        if (ortam.fonMuzigi)
        {
            muzikKaynagi.clip = ortam.fonMuzigi;
            muzikKaynagi.volume = ortam.muzikSesDuzeyi;
            muzikKaynagi.Play();
        }
    }

    // Geçici ses objesi oluşturup çalar
    public void SesCal(AudioClip klip, Vector3 pozisyon, float hacim = 1.0f)
    {
        if (klip == null) return;
        AudioSource.PlayClipAtPoint(klip, pozisyon, hacim);
    }

    // --- GENEL EFEKTLER ---
    public void SallamaSesiCal(Vector3 pos)
    {
        SesCal(efekt.saldiriSallama, pos, efekt.sesDuzeyi);
    }

    public void ParrySesiCal(Vector3 pos)
    {
        SesCal(efekt.parryBasarili, pos, efekt.sesDuzeyi);
    }

    // --- OYUNCU SESLERİ ---
    public void OyuncuHasarCal(Vector3 pos)
    {
        // Rastgele bir hasar sesi seç
        if (oyuncu.hasarAlmaSesleri != null && oyuncu.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, oyuncu.hasarAlmaSesleri.Length);
            SesCal(oyuncu.hasarAlmaSesleri[rastgeleIndex], pos, oyuncu.sesDuzeyi);
        }
    }
    
    public void OyuncuOlumCal(Vector3 pos)
    {
        SesCal(oyuncu.olum, pos, oyuncu.sesDuzeyi);
    }

    // --- DÜŞMAN SESLERİ ---
    public void DusmanKesifCal(Vector3 pos)
    {
        SesCal(dusman.kesifSesi, pos, dusman.sesDuzeyi);
    }

    public void DusmanZaferCal(Vector3 pos)
    {
        SesCal(dusman.zaferBagirmasi, pos, dusman.sesDuzeyi);
    }

    public void DusmanHasarCal(Vector3 pos)
    {
        // Rastgele bir hasar sesi seç
        if (dusman.hasarAlmaSesleri != null && dusman.hasarAlmaSesleri.Length > 0)
        {
            int rastgeleIndex = Random.Range(0, dusman.hasarAlmaSesleri.Length);
            SesCal(dusman.hasarAlmaSesleri[rastgeleIndex], pos, dusman.sesDuzeyi);
        }
    }

    public void DusmanOlumCal(Vector3 pos)
    {
        SesCal(dusman.olum, pos, dusman.sesDuzeyi);
    }

    // --- SİLAH VURUŞ SESİ ---
    public void VurusSesiCal(Vector3 pos, SilahSesTuru tur)
    {
        AudioClip calinacakKlip = null;

        switch (tur)
        {
            case SilahSesTuru.StandartKilic:
                calinacakKlip = efekt.standartVurus;
                break;
            case SilahSesTuru.Tekme:
                calinacakKlip = efekt.tekmeVurusu;
                break;
            case SilahSesTuru.Kalkan:
                calinacakKlip = efekt.kalkanVurusu;
                break;
        }

        if (calinacakKlip != null)
        {
            SesCal(calinacakKlip, pos, efekt.sesDuzeyi);
        }
    }
}