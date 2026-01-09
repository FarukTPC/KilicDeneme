using UnityEngine;
using System.Collections.Generic;

public class BloodDecalHandler : MonoBehaviour
{
    [Header("AYARLAR")]
    
    [Tooltip("Yere yapışacak kan prefablarını buraya sürükle.")]
    public GameObject[] kanIziPrefableri; 
    
    [Tooltip("Her damlanın yerde iz bırakma şansı (0.0 - 1.0).")]
    [Range(0f, 1f)] 
    public float izBirakmaSansi = 0.5f; 
    
    [Tooltip("Kan izinin yerde kalma süresi (Saniye). Test için 5-10 yapabilirsin.")]
    public float yokOlmaSuresi = 10f; 
    
    [Tooltip("Hangi katmanlar zemin olarak kabul edilsin?")]
    public LayerMask zeminKatmani;

    private ParticleSystem partikulSistemi;
    private List<ParticleCollisionEvent> carpismaOlaylari;

    void Start()
    {
        partikulSistemi = GetComponent<ParticleSystem>();
        carpismaOlaylari = new List<ParticleCollisionEvent>();
        
        if (zeminKatmani == 0) 
        {
            zeminKatmani = LayerMask.GetMask("Default", "Terrain", "Ground");
        }
    }

    void OnParticleCollision(GameObject other)
    {
        // Çarpan obje zemin mi?
        if ((zeminKatmani.value & (1 << other.layer)) > 0)
        {
            int olaySayisi = partikulSistemi.GetCollisionEvents(other, carpismaOlaylari);

            for (int i = 0; i < olaySayisi; i++)
            {
                if (Random.value <= izBirakmaSansi)
                {
                    IzOlustur(carpismaOlaylari[i]);
                    
                    // Sadece 1 tane oluştur ve döngüden çık (Spam engelleme)
                    break; 
                }
            }
        }
    }

    void IzOlustur(ParticleCollisionEvent olay)
    {
        // 1. Liste Kontrolü: Liste boşsa hiç uğraşma
        if (kanIziPrefableri == null || kanIziPrefableri.Length == 0) return;

        Vector3 pos = olay.intersection;
        Vector3 normal = olay.normal;

        // 2. Rastgele bir prefab seç
        int rastgeleIndex = Random.Range(0, kanIziPrefableri.Length);
        GameObject secilenKanPrefabi = kanIziPrefableri[rastgeleIndex];

        // --- KRİTİK DÜZELTME BURASI ---
        // Eğer seçilen kutu Inspector'da boş bırakıldıysa (None), 
        // sakın "boş obje" oluşturma. Direkt fonksiyonu bitir.
        if (secilenKanPrefabi == null) 
        {
            return;
        }

        // 3. Kanı oluştur (Yüksekliği 0.04f yaptık)
        GameObject yeniIz = Instantiate(secilenKanPrefabi, pos + normal * 0.04f, Quaternion.LookRotation(normal));

        // 4. İsimlendirme (Hierarchy'de takip edebilmen için)
        yeniIz.name = "Kan_Izi_" + Random.Range(100, 999);

        // 5. Rastgele Döndür ve Boyutlandır
        yeniIz.transform.Rotate(Vector3.forward, Random.Range(0, 360));
        
        float rastgeleBoyut = Random.Range(0.4f, 0.9f);
        yeniIz.transform.localScale = new Vector3(rastgeleBoyut, rastgeleBoyut, 1f);

        // 6. Kesin Silinme Emri
        Destroy(yeniIz, yokOlmaSuresi);
    }
}