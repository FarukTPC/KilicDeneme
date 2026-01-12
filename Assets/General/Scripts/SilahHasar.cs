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
    // GÜNCELLEME: Ses dosyası yerine TÜR seçiyoruz
    public SesYonetici.SilahSesTuru buSilahinTuru = SesYonetici.SilahSesTuru.StandartKilic;

    private int guncelHasar;
    private float guncelSersemletme;
    private float guncelItme;
    private Transform saldiranKisi;
    private int saldiriYonu;

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
        if (!saldiriAktifMi) return;
        if (saldiranKisi != null && other.transform.root == saldiranKisi.root) return;

        EnemyAI vurulanEnemy = other.GetComponentInParent<EnemyAI>();
        PlayerCombat vurulanPlayer = other.GetComponentInParent<PlayerCombat>();
        
        GameObject hasarAlanAnaObje = null;

        if (vurulanEnemy != null) hasarAlanAnaObje = vurulanEnemy.gameObject;
        else if (vurulanPlayer != null) hasarAlanAnaObje = vurulanPlayer.gameObject;

        if (hasarAlanAnaObje == null) return;

        bool saldiranPlayerMi = saldiranKisi.GetComponent<PlayerCombat>() != null;
        bool saldiranEnemyMi = saldiranKisi.GetComponent<EnemyAI>() != null;

        if (saldiranPlayerMi && vurulanPlayer != null) return;
        if (saldiranEnemyMi && vurulanEnemy != null) return;

        if (vurulanlarListesi.Contains(hasarAlanAnaObje)) return;

        vurulanlarListesi.Add(hasarAlanAnaObje);

        KanEfektiOlustur(other);
        
        // GÜNCELLEME: Sesi Manager'a tür belirterek çaldırıyoruz
        if (SesYonetici.Instance != null)
        {
            SesYonetici.Instance.VurusSesiCal(transform.position, buSilahinTuru);
        }

        if (vurulanEnemy != null)
        {
            vurulanEnemy.HasarAl(guncelHasar, saldiranKisi, saldiriYonu, guncelItme, 0.2f);
            if (guncelSersemletme > 0) vurulanEnemy.Sersemle(guncelSersemletme);
        }
        else if (vurulanPlayer != null)
        {
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