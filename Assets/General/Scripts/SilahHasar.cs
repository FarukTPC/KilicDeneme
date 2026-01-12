using UnityEngine;
using System.Collections.Generic;

public class SilahHasar : MonoBehaviour
{
    [Header("Ayarlar")]
    public Collider silahCollideri;
    public LayerMask hedefKatmani; 
    public GameObject vurusEfektiPrefab;

    private int guncelHasar;
    private float guncelSersemletme;
    private float guncelItme;
    private Transform saldiranKisi;
    private int saldiriYonu;

    private List<GameObject> vurulanlarListesi = new List<GameObject>();

    private void Awake()
    {
        if (silahCollideri == null) silahCollideri = GetComponent<Collider>();
        if (silahCollideri) 
        {
            silahCollideri.isTrigger = true; 
            silahCollideri.enabled = false;
        }
    }

    public void VurusaHazirla(int hasar, float sersem, float itme, Transform sahip, int yon)
    {
        guncelHasar = hasar;
        guncelSersemletme = sersem;
        guncelItme = itme;
        saldiranKisi = sahip;
        saldiriYonu = yon;
        
        vurulanlarListesi.Clear(); 
        if (silahCollideri) silahCollideri.enabled = true; 
    }

    public void VurusuBitir()
    {
        if (silahCollideri) silahCollideri.enabled = false;
        vurulanlarListesi.Clear();
    }

private void OnTriggerEnter(Collider other)
    {

        Debug.Log("Kılıç şuna çarptı: " + other.name); // <--- BU SATIRI EKLE

        if ((hedefKatmani.value & (1 << other.gameObject.layer)) > 0)
        {
            if (vurulanlarListesi.Contains(other.gameObject)) return;

            // --- DÜŞMAN KONTROLÜ (Bunu zaten düzeltmiştik) ---
            EnemyAI dusman = other.GetComponentInParent<EnemyAI>(); 
            if (dusman != null)
            {
                KanEfektiOlustur(other);
                dusman.HasarAl(guncelHasar, saldiranKisi, saldiriYonu, guncelItme, 0.2f);
                if (guncelSersemletme > 0) dusman.Sersemle(guncelSersemletme);
                vurulanlarListesi.Add(other.gameObject);
            }
            
            // --- OYUNCU KONTROLÜ (BURAYI DEĞİŞTİRECEKSİN) ---
            // Eskisi: other.GetComponent<PlayerCombat>();
            // Yenisi: Aşağıdaki gibi InParent eklenmiş hali.
            
            PlayerCombat oyuncu = other.GetComponentInParent<PlayerCombat>(); 
            
            if (oyuncu != null)
            {
                KanEfektiOlustur(other);
                
                oyuncu.HasarAl(guncelHasar, saldiranKisi, saldiriYonu, guncelItme, 0.2f);
                if (guncelSersemletme > 0) oyuncu.Sersemle(guncelSersemletme);
                
                vurulanlarListesi.Add(other.gameObject);
            }
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