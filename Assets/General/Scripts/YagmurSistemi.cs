using UnityEngine;
using System.Collections;

public class YagmurSistemi : MonoBehaviour
{
    [Header("GÖRSEL AYARLAR")]
    public Transform oyuncu;          
    public Light gunesIsigi;          
    public float yagmurYuksekligi = 15f; // Biraz daha yukarı aldım

    [Header("ŞİMŞEK ZAMANLAMASI")]
    public float minSimsekSure = 8f;  
    public float maxSimsekSure = 20f; 
    public float simsekParlakligi = 2.0f; 

    private float normalIsikSiddeti;
    
    private void Start()
    {
        if (gunesIsigi != null) normalIsikSiddeti = gunesIsigi.intensity;

        if (oyuncu == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) oyuncu = p.transform;
        }

        // Ses Yöneticisine "Yağmur sesini başlat" emri ver
        if (SesYonetici.Instance != null)
        {
            SesYonetici.Instance.YagmurSesiniBaslat();
        }

        StartCoroutine(SimsekDongusu());
    }

    private void Update()
    {
        // YAĞMUR TAKİBİ - Şerit sorununu çözen kısım burası değil, Inspector ayarıdır.
        // Ama burada sadece pozisyonu güncelleyip rotasyonu sabit tutmak önemli.
        if (oyuncu != null)
        {
            transform.position = new Vector3(oyuncu.position.x, oyuncu.position.y + yagmurYuksekligi, oyuncu.position.z);
        }
    }

    private IEnumerator SimsekDongusu()
    {
        while (true)
        {
            float bekleme = Random.Range(minSimsekSure, maxSimsekSure);
            yield return new WaitForSeconds(bekleme);
            yield return StartCoroutine(SimsekCaktir());
        }
    }

    private IEnumerator SimsekCaktir()
    {
        // 1. Ekran Parlaması
        if (gunesIsigi != null)
        {
            gunesIsigi.intensity = simsekParlakligi; 
            yield return new WaitForSeconds(0.1f);   
            gunesIsigi.intensity = normalIsikSiddeti; 
            yield return new WaitForSeconds(0.05f); 
            gunesIsigi.intensity = simsekParlakligi * 0.5f; 
            yield return new WaitForSeconds(0.05f);
            gunesIsigi.intensity = normalIsikSiddeti;
        }

        // 2. Sesi Tetikle (Gecikmeli)
        yield return new WaitForSeconds(Random.Range(0.2f, 1.0f));

        if (SesYonetici.Instance != null)
        {
            SesYonetici.Instance.GokGurultusuPatlat();
        }
    }
}