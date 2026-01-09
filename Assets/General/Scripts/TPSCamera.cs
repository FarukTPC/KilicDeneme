using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("--- TAKİP VE HEDEF ---")]
    [Tooltip("Takip edilecek karakter (Player).")]
    public Transform hedef;

    [Header("--- KONUM AYARLARI (BANNERLORD TARZI) ---")]
    [Tooltip("Kameranın karakterin ne kadar yukarısına bakacağı (Kafa hizası). Örn: 1.5")]
    public float yukseklikPayi = 1.4f;
    [Tooltip("Kameranın sağa/sola kayması (Sağ omuz için pozitif değer). Örn: 0.5")]
    public float omuzPayi = 0.5f;
    [Tooltip("Kameranın karakterden uzaklığı.")]
    public float varsayilanMesafe = 3.0f;

    [Header("--- HASSASİYET VE YUMUŞATMA ---")]
    [Tooltip("Fare çevirme hızı.")]
    public float fareHassasiyeti = 3.0f;
    [Tooltip("Kamera dönüş yumuşaklığı (Daha düşük = Daha hızlı). Örn: 0.1")]
    public float donusYumusakligi = 0.1f;
    [Tooltip("Kamera takip yumuşaklığı (Titremeyi önler). Örn: 0.2")]
    public float takipYumusakligi = 0.2f;

    [Header("--- SINIRLAR VE ENGEL ---")]
    [Tooltip("Aşağı bakma limiti.")]
    public float minDikeyAci = -30f;
    [Tooltip("Yukarı bakma limiti.")]
    public float maksDikeyAci = 70f;
    [Tooltip("Kameranın çarpacağı katmanlar (Player ve Enemy SEÇİLİ OLMAMALI!).")]
    public LayerMask engelKatmani;
    [Tooltip("Kamera duvara çarpınca ne kadar yakına gelebilir?")]
    public float minKameraMesafesi = 0.5f;

    // --- SAVAŞ SİSTEMİ BAĞLANTISI ---
    [Header("--- SAVAŞ MODU ---")]
    public PlayerCombat oyuncuSavasScripti;
    [Tooltip("Savaş modunda kamera biraz daha uzaklaşsın mı?")]
    public float savasMesafesiEk = 1.0f;
    [Tooltip("Lock-On yapınca dönüş hızı.")]
    public float kilitlenmeHizi = 15f;

    // --- GİZLİ DEĞİŞKENLER ---
    private float hedefX = 0f;
    private float hedefY = 0f;
    private float suankiX = 0f;
    private float suankiY = 0f;
    private float xHizi = 0f; 
    private float yHizi = 0f; 
    
    private Vector3 suankiTakipHizi; 
    private Vector3 hedefPozisyonu;  
    private float guncelMesafe;      

    // Sarsıntı
    private float sarsintiSuresi = 0f;
    private float sarsintiGucu = 0f;
    private Vector3 sarsintiVektoru;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // --- DÜZELTME BURADA ---
        // Eskiden transform.eulerAngles alıyorduk, şimdi manuel atıyoruz.
        
        if (hedef != null)
        {
            // Y eksenini (Sağ/Sol) oyuncunun baktığı yöne eşitle (Arkasına geç)
            hedefX = hedef.eulerAngles.y;
            
            // X eksenini (Yukarı/Aşağı) 10 derece yap (Hafif aşağı baksın, gökyüzüne değil)
            hedefY = 10f; 
        }
        else
        {
            Vector3 acilar = transform.eulerAngles;
            hedefX = acilar.y;
            hedefY = acilar.x;
        }
        
        // Başlangıçta yumuşak geçiş olmasın, direkt oraya ışınlansın
        suankiX = hedefX;
        suankiY = hedefY;

        guncelMesafe = varsayilanMesafe;

        if (oyuncuSavasScripti == null && hedef != null)
            oyuncuSavasScripti = hedef.GetComponent<PlayerCombat>();
    }

    private void LateUpdate()
    {
        if (!hedef) return;

        GirdileriYonet();
        SarsintiyiHesapla();
        KamerayiHareketEttir();
    }

    private void GirdileriYonet()
    {
        if (oyuncuSavasScripti != null && oyuncuSavasScripti.savas.savasModunda && oyuncuSavasScripti.mevcutHedef != null)
        {
            Vector3 dusmanaYon = oyuncuSavasScripti.mevcutHedef.position - hedef.position;
            if (dusmanaYon != Vector3.zero)
            {
                Quaternion bakis = Quaternion.LookRotation(dusmanaYon);
                hedefX = Mathf.LerpAngle(hedefX, bakis.eulerAngles.y, Time.deltaTime * kilitlenmeHizi);
                hedefY -= Input.GetAxis("Mouse Y") * fareHassasiyeti;
            }
        }
        else
        {
            hedefX += Input.GetAxis("Mouse X") * fareHassasiyeti;
            hedefY -= Input.GetAxis("Mouse Y") * fareHassasiyeti;
        }

        hedefY = Mathf.Clamp(hedefY, minDikeyAci, maksDikeyAci);

        suankiX = Mathf.SmoothDamp(suankiX, hedefX, ref xHizi, donusYumusakligi);
        suankiY = Mathf.SmoothDamp(suankiY, hedefY, ref yHizi, donusYumusakligi);
    }

    private void KamerayiHareketEttir()
    {
        Vector3 karakterKafaNoktasi = hedef.position + Vector3.up * yukseklikPayi;
        Quaternion rotasyon = Quaternion.Euler(suankiY, suankiX, 0);

        float istenenMesafe = varsayilanMesafe;
        if (oyuncuSavasScripti && oyuncuSavasScripti.savas.savasModunda)
            istenenMesafe += savasMesafesiEk;

        Vector3 kameraYonu = rotasyon * Vector3.back; 
        Vector3 omuzVektoru = rotasyon * Vector3.right * omuzPayi; 
        
        Vector3 finalHedefNoktasi = karakterKafaNoktasi + omuzVektoru + (kameraYonu * istenenMesafe);
        Vector3 rayBaslangic = karakterKafaNoktasi + omuzVektoru; 
        
        RaycastHit hit;
        Vector3 sonucPozisyon;

        if (Physics.SphereCast(rayBaslangic, 0.2f, (finalHedefNoktasi - rayBaslangic).normalized, out hit, istenenMesafe, engelKatmani))
        {
            guncelMesafe = Mathf.Clamp(hit.distance, minKameraMesafesi, istenenMesafe);
            sonucPozisyon = rayBaslangic + ((finalHedefNoktasi - rayBaslangic).normalized * guncelMesafe);
        }
        else
        {
            sonucPozisyon = finalHedefNoktasi;
        }

        transform.rotation = rotasyon;
        transform.position = Vector3.SmoothDamp(transform.position, sonucPozisyon + sarsintiVektoru, ref suankiTakipHizi, takipYumusakligi);
    }
    
    public void KamerayiSalla(float sure, float guc)
    {
        sarsintiSuresi = sure;
        sarsintiGucu = guc;
    }

    private void SarsintiyiHesapla()
    {
        if (sarsintiSuresi > 0)
        {
            sarsintiVektoru = Random.insideUnitSphere * sarsintiGucu;
            sarsintiSuresi -= Time.deltaTime;
        }
        else
        {
            sarsintiVektoru = Vector3.zero;
        }
    }
}