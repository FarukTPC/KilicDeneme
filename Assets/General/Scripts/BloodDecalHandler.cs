using UnityEngine;
using System.Collections.Generic;

public class BloodDecalHandler : MonoBehaviour
{
    [Header("AYARLAR")]
    
    [Tooltip("Yere yapışacak kan prefablarını buraya sürükle.")]
    public GameObject[] kanIziPrefableri; 
    
    [Tooltip("Her vuruşta yere iz bırakma şansı (0.005 = %0.5, 1.0 = %100).")]
    [Range(0f, 1f)] 
    public float izBirakmaSansi = 0.005f; // Varsayılanı düşürdüm
    
    [Tooltip("Kan izinin yerde kalma süresi (Saniye).")]
    public float yokOlmaSuresi = 30f; 
    
    [Tooltip("Hangi katmanlar zemin olarak kabul edilsin?")]
    public LayerMask zeminKatmani;

    private ParticleSystem partikulSistemi;
    private List<ParticleCollisionEvent> carpismaOlaylari;
    
    // YENİ: Bu efekt daha önce kan bıraktı mı?
    private bool izBiraktiMi = false; 

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
        // 1. KİLİT KONTROLÜ: Eğer bu kan grubu zaten yere bir iz bıraktıysa,
        // diğer damlalar için işlem yapma. (Performans ve Görsellik için kritik)
        if (izBiraktiMi) return;

        // Çarpan obje zemin mi?
        if ((zeminKatmani.value & (1 << other.layer)) > 0)
        {
            int olaySayisi = partikulSistemi.GetCollisionEvents(other, carpismaOlaylari);

            for (int i = 0; i < olaySayisi; i++)
            {
                // Şans faktörü
                if (Random.value <= izBirakmaSansi)
                {
                    IzOlustur(carpismaOlaylari[i]);
                    
                    // KİLİDİ AKTİF ET: Artık bu efekt bir daha iz bırakamaz.
                    izBiraktiMi = true; 
                    break; 
                }
            }
        }
    }

    void IzOlustur(ParticleCollisionEvent olay)
    {
        // Liste boşsa veya prefab yoksa çık
        if (kanIziPrefableri == null || kanIziPrefableri.Length == 0) return;

        // Rastgele seçim
        int rastgeleIndex = Random.Range(0, kanIziPrefableri.Length);
        GameObject secilenKanPrefabi = kanIziPrefableri[rastgeleIndex];

        // Inspector'da boş kutu varsa hata vermesin
        if (secilenKanPrefabi == null) return;

        Vector3 pos = olay.intersection;
        Vector3 normal = olay.normal;

        // Kanı oluştur (Zeminle çakışmaması için 0.04f yükseklik)
        GameObject yeniIz = Instantiate(secilenKanPrefabi, pos + normal * 0.04f, Quaternion.LookRotation(normal));

        // İsimlendirme ve Hiyerarşi düzeni
        yeniIz.name = "Kan_Izi_" + Random.Range(100, 999);

        // Rastgele Döndür ve Boyutlandır
        yeniIz.transform.Rotate(Vector3.forward, Random.Range(0, 360));
        
        float rastgeleBoyut = Random.Range(0.4f, 0.9f);
        yeniIz.transform.localScale = new Vector3(rastgeleBoyut, rastgeleBoyut, 1f);

        // Sil
        Destroy(yeniIz, yokOlmaSuresi);
    }
}