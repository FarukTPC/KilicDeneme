using UnityEngine;
using System.Collections.Generic;

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
        [Header("HASAR SESLERİ")]
        public AudioClip[] hasarAlmaSesleri; 
        public AudioClip olumSesi;
        
        [Header("ADIM SESLERİ")]
        [Tooltip("Buraya 3-4 farklı 'Tak' sesi sürükle. Rastgele seçilecek.")]
        public AudioClip[] adimSesleri; // ARTIK DİZİ OLDU
        
        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class DusmanSesleri
    {
        public AudioClip kesifSesi; 
        public AudioClip zaferBagirmasi;
        public AudioClip[] hasarAlmaSesleri;
        public AudioClip olumSesi;
        
        [Tooltip("Düşman için loop sesi kullanmaya devam ediyoruz.")]
        public AudioClip ayakSesiLoop; 

        [Range(0, 1)] public float sesDuzeyi = 1.0f;
    }

    [System.Serializable]
    public class EfektSesleri
    {
        [Header("GENEL")]
        public AudioClip silahSallamaSesi; 
        [Range(0, 1)] public float sallamaSesDuzeyi = 1.0f;

        public AudioClip parrySesi;        
        [Range(0, 1)] public float parrySesDuzeyi = 1.0f;

        [Header("ÖZEL VURUŞ SESLERİ")]
        public AudioClip kılıcVurmaSesi;   
        [Range(0, 1)] public float kılıcVurmaSesDuzeyi = 1.0f;

        public AudioClip tekmeVurmaSesi;   
        [Range(0, 1)] public float tekmeVurmaSesDuzeyi = 1.0f;

        public AudioClip kalkanVurmaSesi;  
        [Range(0, 1)] public float kalkanVurmaSesDuzeyi = 1.0f;
    }

    [Header("KATEGORİLER")]
    public OrtamSesleri ortam;
    public OyuncuSesleri oyuncu;
    public DusmanSesleri dusman;
    public EfektSesleri efekt;

    private AudioSource muzikKaynagi;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        muzikKaynagi = gameObject.AddComponent<AudioSource>();
        muzikKaynagi.loop = true;
        
        if (ortam.fonMuzigi)
        {
            muzikKaynagi.clip = ortam.fonMuzigi;
            muzikKaynagi.volume = ortam.muzikSesDuzeyi;
            muzikKaynagi.Play();
        }
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
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.0f;
        source.maxDistance = maxMesafe; 
        source.Play();

        Destroy(sesObjesi, klip.length + 0.1f);
    }

    public void SallamaSesiVer(Vector3 pos) => SesCal(efekt.silahSallamaSesi, pos, efekt.sallamaSesDuzeyi, 15f);
    public void ParrySesiVer(Vector3 pos) => SesCal(efekt.parrySesi, pos, efekt.parrySesDuzeyi, 20f);
    
    public void DusmanKesifSesiVer(Vector3 pos) => SesCal(dusman.kesifSesi, pos, dusman.sesDuzeyi, 25f);
    public void DusmanZaferSesiVer(Vector3 pos) => SesCal(dusman.zaferBagirmasi, pos, dusman.sesDuzeyi, 25f);
    public void DusmanOlumSesiVer(Vector3 pos) => SesCal(dusman.olumSesi, pos, dusman.sesDuzeyi, 20f);
    
    public void OyuncuOlumSesiVer(Vector3 pos) => SesCal(oyuncu.olumSesi, pos, oyuncu.sesDuzeyi, 20f);

    public void OyuncuHasarCal(Vector3 pos)
    {
        if (oyuncu.hasarAlmaSesleri != null && oyuncu.hasarAlmaSesleri.Length > 0)
        {
            int i = Random.Range(0, oyuncu.hasarAlmaSesleri.Length);
            SesCal(oyuncu.hasarAlmaSesleri[i], pos, oyuncu.sesDuzeyi, 15f);
        }
    }

    public void DusmanHasarCal(Vector3 pos)
    {
        if (dusman.hasarAlmaSesleri != null && dusman.hasarAlmaSesleri.Length > 0)
        {
            int i = Random.Range(0, dusman.hasarAlmaSesleri.Length);
            SesCal(dusman.hasarAlmaSesleri[i], pos, dusman.sesDuzeyi, 20f);
        }
    }

    public void VurusSesiVer(Vector3 pos, SilahSesTuru tur)
    {
        AudioClip klip = null;
        float secilenSesDuzeyi = 1.0f;

        switch (tur)
        {
            case SilahSesTuru.StandartKilic:
                klip = efekt.kılıcVurmaSesi;
                secilenSesDuzeyi = efekt.kılıcVurmaSesDuzeyi;
                break;
            case SilahSesTuru.Tekme:
                klip = efekt.tekmeVurmaSesi;
                secilenSesDuzeyi = efekt.tekmeVurmaSesDuzeyi;
                break;
            case SilahSesTuru.Kalkan:
                klip = efekt.kalkanVurmaSesi;
                secilenSesDuzeyi = efekt.kalkanVurmaSesDuzeyi;
                break;
        }

        if (klip != null) SesCal(klip, pos, secilenSesDuzeyi, 20f);
    }
}