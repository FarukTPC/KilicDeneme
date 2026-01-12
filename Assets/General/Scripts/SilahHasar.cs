using UnityEngine;
using System.Collections.Generic;

public class SilahHasar : MonoBehaviour
{
    [Header("AYARLAR")]
    public Collider silahCollideri;
    [Tooltip("Inspector'da 'Everything' seçili kalsın.")]
    public LayerMask hedefKatmani; 
    public GameObject vurusEfektiPrefab;
    
    [Header("SİLAH TÜRÜ")]
    [Tooltip("Bu obje düşmana değince hangi ses çıksın?")]
    public SesYonetici.SilahSesTuru buSilahinTuru = SesYonetici.SilahSesTuru.StandartKilic;

    // Saldırı verileri
    private int guncelHasar;
    private float guncelSersemletme;
    private float guncelItme;
    private Transform saldiranKisi;
    private int saldiriYonu;

    // Kontrol
    private bool saldiriAktifMi = false;
    private List<GameObject> vurulanlarListesi = new List<GameObject>();

    private void Awake()
    {
        if (silahCollideri == null) silahCollideri = GetComponent<Collider>();
        Kapat();
    }

    public void VurusaHazirla(int hasar, float sersem, float itme, Transform sahip, int yon)
    {
        guncelHasar = hasar;
        guncelSersemletme = sersem;
        guncelItme = itme;
        saldiranKisi = sahip;
        saldiriYonu = yon;
        
        saldiriAktifMi = true;
        vurulanlarListesi.Clear();
        if (silahCollideri) silahCollideri.enabled = true;
    }

    public void VurusuBitir()
    {
        Kapat();
    }

    private void Kapat()
    {
        saldiriAktifMi = false;
        if (silahCollideri) silahCollideri.enabled = false;
        vurulanlarListesi.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. GÜVENLİK
        if (!saldiriAktifMi) return;
        if (saldiranKisi != null && other.transform.root == saldiranKisi.root) return;

        // 2. KİMLİK KONTROLÜ
        EnemyAI vurulanEnemy = other.GetComponentInParent<EnemyAI>();
        PlayerCombat vurulanPlayer = other.GetComponentInParent<PlayerCombat>();
        
        GameObject hasarAlanAnaObje = null;

        if (vurulanEnemy != null) hasarAlanAnaObje = vurulanEnemy.gameObject;
        else if (vurulanPlayer != null) hasarAlanAnaObje = vurulanPlayer.gameObject;

        if (hasarAlanAnaObje == null) return;

        // 3. DOST ATEŞİ KONTROLÜ
        bool saldiranPlayerMi = saldiranKisi.GetComponent<PlayerCombat>() != null;
        bool saldiranEnemyMi = saldiranKisi.GetComponent<EnemyAI>() != null;

        if (saldiranPlayerMi && vurulanPlayer != null) return;
        if (saldiranEnemyMi && vurulanEnemy != null) return;

        // 4. TEKRAR VURMA KONTROLÜ
        if (vurulanlarListesi.Contains(hasarAlanAnaObje)) return;

        // --- İŞLEM BAŞLIYOR ---
        vurulanlarListesi.Add(hasarAlanAnaObje);

        // A) Efekt Oluştur
        KanEfektiOlustur(other);
        
        // B) Vuruş Sesi (Metal/Küt sesi - Türe göre)
        if (SesYonetici.Instance != null)
        {
            SesYonetici.Instance.VurusSesiCal(transform.position, buSilahinTuru);
        }

        // C) Hasar Verme & Hasar Sesi (Senkronize Çözüm)
        if (vurulanEnemy != null)
        {
            // Düşmanın canı yandı sesini BURADA anında çalıyoruz
            if (SesYonetici.Instance != null) SesYonetici.Instance.DusmanHasarCal(transform.position);
            
            vurulanEnemy.HasarAl(guncelHasar, saldiranKisi, saldiriYonu, guncelItme, 0.2f);
            if (guncelSersemletme > 0) vurulanEnemy.Sersemle(guncelSersemletme);
        }
        else if (vurulanPlayer != null)
        {
            // Oyuncunun canı yandı sesini BURADA anında çalıyoruz
            if (SesYonetici.Instance != null) SesYonetici.Instance.OyuncuHasarCal(transform.position);

            vurulanPlayer.HasarAl(guncelHasar, saldiranKisi, saldiriYonu, guncelItme, 0.2f);
            if (guncelSersemletme > 0) vurulanPlayer.Sersemle(guncelSersemletme);
        }
    }

    void KanEfektiOlustur(Collider carpan)
    {
        if (vurusEfektiPrefab != null)
        {
            Vector3 temasNoktasi = carpan.ClosestPoint(transform.position);
            Vector3 kanYonu = (saldiranKisi.position - temasNoktasi).normalized;
            kanYonu += new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f));
            GameObject efekt = Instantiate(vurusEfektiPrefab, temasNoktasi, Quaternion.LookRotation(kanYonu));
            Destroy(efekt, 2.0f);
        }
    }
}