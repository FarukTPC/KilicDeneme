using UnityEngine;
using System.Collections.Generic;

public class SilahHasar : MonoBehaviour
{
    [Header("AYARLAR")]
    public bool debugModu = true; // Bunu işaretle ki her detayı görelim!
    public Collider silahCollideri;
    public GameObject vurusEfektiPrefab;
    
    [Header("ÖZEL SES")]
    public AudioClip ozelVurusSesi;
    private AudioSource sesKaynagi;

    // Saldırı verileri
    private int guncelHasar;
    private float guncelSersemletme;
    private float guncelItme;
    private Transform saldiranKisi;
    private int saldiriYonu;

    // Kontrol değişkenleri
    private bool saldiriAktifMi = false;
    private List<GameObject> vurulanlarListesi = new List<GameObject>();

    private void Awake()
    {
        if (silahCollideri == null) silahCollideri = GetComponent<Collider>();
        sesKaynagi = GetComponent<AudioSource>();
        if (sesKaynagi == null)
        {
            sesKaynagi = gameObject.AddComponent<AudioSource>();
            sesKaynagi.spatialBlend = 1.0f;
        }
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
        if (silahCollideri) 
        {
            silahCollideri.enabled = true;
            if(debugModu) Debug.Log($"<color=cyan>AÇILDI:</color> {this.name} saldırıya hazır! (Frame: {Time.frameCount})");
        }
    }

    public void VurusuBitir()
    {
        if(debugModu && saldiriAktifMi) Debug.Log($"<color=orange>KAPANDI:</color> {this.name} saldırısı bitti. (Frame: {Time.frameCount})");
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
        // 1. FİZİKSEL TEMAS ALGILANDI MI?
        if (debugModu) Debug.Log($"<color=grey>TEMAS:</color> {this.name}, fiziksel olarak {other.name}'a değdi.");

        // 2. SALDIRI MODU AÇIK MI?
        if (!saldiriAktifMi) 
        {
            if (debugModu) Debug.Log($"<color=red>İPTAL (Pasif):</color> Saldırı emri yokken {other.name}'a değdi.");
            return;
        }

        // 3. KENDİ KENDİNE Mİ ÇARPIYOR?
        if (saldiranKisi != null && other.transform.root == saldiranKisi.root) 
        {
            // Kendi vücuduna çarpıyorsa loglamaya bile gerek yok, çok kirlilik yapar.
            return; 
        }

        // 4. KİMLİK TESPİTİ
        EnemyAI vurulanEnemy = other.GetComponentInParent<EnemyAI>();
        PlayerCombat vurulanPlayer = other.GetComponentInParent<PlayerCombat>();
        
        GameObject hasarAlanAnaObje = null;
        string hedefTuru = "Bilinmiyor";

        if (vurulanEnemy != null) { hasarAlanAnaObje = vurulanEnemy.gameObject; hedefTuru = "Enemy"; }
        else if (vurulanPlayer != null) { hasarAlanAnaObje = vurulanPlayer.gameObject; hedefTuru = "Player"; }

        // Duvar, zemin vb.
        if (hasarAlanAnaObje == null) 
        {
             if (debugModu) Debug.Log($"<color=yellow>İPTAL (Hedef Değil):</color> {other.name} üzerinde canı olan bir script bulunamadı.");
             return;
        }

        // 5. DOST ATEŞİ KONTROLÜ
        bool saldiranPlayerMi = saldiranKisi.GetComponent<PlayerCombat>() != null;
        
        // Player -> Player'a vuruyorsa (Hata)
        if (saldiranPlayerMi && vurulanPlayer != null) 
        {
            if (debugModu) Debug.Log($"<color=red>İPTAL (Dost):</color> Kendine veya başka oyuncuya vurmaya çalıştın.");
            return;
        }
        // Enemy -> Enemy'e vuruyorsa (Hata)
        if (!saldiranPlayerMi && vurulanEnemy != null) 
        {
            if (debugModu) Debug.Log($"<color=red>İPTAL (Dost):</color> Düşman düşmana vurmaya çalıştı.");
            return;
        }

        // 6. DAHA ÖNCE VURDUK MU?
        if (vurulanlarListesi.Contains(hasarAlanAnaObje)) 
        {
            if (debugModu) Debug.Log($"<color=magenta>İPTAL (Tekrar):</color> {hasarAlanAnaObje.name}'a bu saldırıda zaten vuruldu.");
            return;
        }

        // --- BINGO! HASAR İŞLEMİ ---
        vurulanlarListesi.Add(hasarAlanAnaObje);
        Debug.Log($"<color=green>SUCCESS:</color> {this.name} -> {hasarAlanAnaObje.name} ({hedefTuru}) HASAR VERDİ!");

        KanEfektiOlustur(other);
        OzelSesCal();

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
    
    void OzelSesCal()
    {
        if (ozelVurusSesi != null && sesKaynagi != null)
        {
            sesKaynagi.PlayOneShot(ozelVurusSesi);
        }
    }
}