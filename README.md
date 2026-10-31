<h1 align="center">DesktopPossible</h1>
<p align="center"><i>Masaüstünü sihir gibi düzenle!</i></p>
<p align="center"><b>Windows 10/11 için ücretsiz, açık kaynaklı, tamamen Türkçe bir Stardock Fences alternatifi</b> — masaüstü simgelerini kutulara topla, klasörleri canlı yansıt, masaüstünü derli toplu tut.</p>

<p align="center">
  <img width="150" height="150" alt="DesktopPossible logosu" src="docs/images/logo.png" />
</p>

<p align="center">
  <img alt="Windows 11 masaüstünde geliştirici araçlarını tutan bir DesktopPossible kutusu" src="docs/images/screenshot-frame.png" />
  <br /><sub>Masaüstünde bir veri kutusu — ♥ menüsü, konum kilidi, taşıma tutamacı ve üstte tutma iğnesiyle başlık çubuğu. <a href="docs/images/screenshot-desktop.png">Tüm masaüstü görünümü</a>.</sub>
</p>

[![Lisans: MIT](https://img.shields.io/badge/Lisans-MIT-blue.svg)](LICENSE)
[![GitHub sürümü](https://img.shields.io/github/v/release/veli-ozturk/DesktopPossible?label=s%C3%BCr%C3%BCm)](https://github.com/veli-ozturk/DesktopPossible/releases)

---

## Bu proje nedir?

DesktopPossible, masaüstündeki simgeleri **kutular** içinde gruplayan bir masaüstü düzenleyicisidir. Bu depo, **Veli Öztürk ([@veli-ozturk](https://github.com/veli-ozturk))** tarafından geliştirilen **Türkçe sürümdür**: arayüz baştan sona Türkçeleştirildi, Türkçe kullanıcıya göre ayarlandı ve kendi düzeltmelerimizle ayrı bir yolda ilerliyor. Güncellemeler bu deponun sürümlerinden gelir.

## Bu sürümde neler var?

Aşağıdakiler bu sürümde eklendi veya değiştirildi:

- **Tamamen Türkçe arayüz:** menüler, ayarlar, ipuçları, iletişim kutuları, bildirimler ve kategori adları Türkçe. Süreler "3g 4sa 12dk", monitör sayısı "N monitör" gibi Türkçe biçimde gösterilir.
- **Masaüstünü göster (Win+D) düzeltmesi:** Win+D'ye ya da görev çubuğunun sağ köşesine basınca kutular kaybolmaz, masaüstünde görünür kalır. Bir uygulamaya dönünce yine en alta iner.
- **Güncellemeler bu depodan:** güncelleme denetimi bu deponun GitHub sürümlerine bakar.
- **Çevrimiçi kategori aramasını kapatma seçeneği:** masaüstü sınıflandırması winget/Chocolatey sorgusu yapmadan, yalnızca yerel kurallarla çalışabilir.
- **`-arrange` başlatma anahtarı:** program `-arrange` ile açılınca kutuları otomatik olarak boyutlandırıp yerleştirir.

## Özellikler

- **Kutu türleri:** veri kutuları (kısayollar ve dosyalar), klasörü canlı yansıtan **portal kutuları** (gezinme, filtreler, sıralanabilir ayrıntı görünümü), resim kutuları ve duvar kâğıdına yazılan **metin kutuları** (bilgisayar adı, işlemci, RAM, IP, diskler, çalışma süresi, tarih/saat…).
- **Serbest yerleşim:** simgeleri kutunun içinde istediğin hücreye koy; `Ctrl` ile sürükleyince kopyalanır.
- **Gezgin gibi sürükle-bırak** ve simgelerde **Windows'un kendi sağ tık menüsü** (*Birlikte aç*, *Gönder*, *Özellikler*…).
- **Tek tıkla masaüstünü sınıflandırma:** dağınık simgeleri Verimlilik, Araçlar, Oyunlar, Geliştirici araçları, Güvenlik, Medya, Belgeler gibi kutulara ayırır.
- **Görünürlük:** boşta solma, rulo gibi toplama, otomatik gizleme, üstte tutma, tümünü gizle/göster (`Ctrl + Alt + H`).
- **Profiller:** sanal masaüstlerine göre otomatik değişen çalışma profilleri, kısayol tuşları, `.frame` içe/dışa aktarma ve günlük yedekler.
- **Görünüm:** 13 renk, kutu başına renk ve kenarlık, duvar kâğıdına uyum sağlayan **Bukalemun** modu, sistem temasına uyan koyu menüler.
- **Düzenleme modu:** kutular varsayılan olarak kilitlidir; tek tıkla taşınabilir ve boyutlandırılabilir hale gelir.

## Kurulum

[Sürümler](https://github.com/veli-ozturk/DesktopPossible/releases) sayfasından indir:

**Kurulum paketi (önerilen):** `DesktopPossible-<sürüm>-win-x64.msi` dosyasını çalıştır. Program
`C:\Program Files\DesktopPossible` klasörüne kurulur, Başlat menüsüne eklenir ve eski kurulumu
yerinde günceller. Ayarların `%LocalAppData%\DesktopPossible\Data` klasöründe tutulur.
Kaldırmak için *Ayarlar > Uygulamalar*'ı kullan; ayar klasörün silinmez.

**Taşınabilir:** `DesktopPossible-<sürüm>-win-x64.zip` dosyasını istediğin yere çıkar ve
`DesktopPossible.exe`'yi çalıştır. Ayarlar programın yanında tutulur.

Windows açılışında başlaması için programın içindeki "Windows ile başlat" seçeneğini aç.

Windows 10 veya 11 gerekir. Ayrıca .NET kurmana gerek yoktur.

## Kaynaktan derleme

Proje **.NET 10** (`net10.0-windows10.0.19041.0`) hedefler. Program ve testleri yalnızca **Windows**'ta çalışır.

1. [**.NET 10+ SDK**](https://dotnet.microsoft.com/download) kur.
2. Depoyu klonla ve şunları çalıştır:

```powershell
./build.ps1        # Çözümü derler
./test-smoke.ps1   # Birim testlerini çalıştırır
./start-app.ps1    # Gerekirse derler ve programı açar
./package.ps1      # MSI ve zip paketlerini üretir (PowerShell 7)
```

## Belgeler

| Konu | Açıklama |
|------|----------|
| [Kullanım kılavuzu](docs/manual.md) | Kutular, özelleştirme ve ayarlar (İngilizce) |
| [İpuçları](docs/tips.md) | Portal filtreleri, profiller (İngilizce) |
| [Gelişmiş ayarlar](docs/tweaks.md) | JSON düzeyinde ayarlar (İngilizce) |

## Lisans ve emeği geçenler

MIT lisanslıdır — bkz. [LICENSE](LICENSE). MIT lisansı gereği önceki yazarların telif bildirimleri lisans dosyasında korunur:

- Türkçe sürüm: **Veli Öztürk** ([@veli-ozturk](https://github.com/veli-ozturk))
- Temel aldığı proje: **DesktopPossible** — [DevPossible](https://github.com/DevPossible/DesktopPossible)
- Onun temeli: **Desktop Frames +** — limbo666 / Nikos Georgousis ve ilk sürüm **BirdyFences** — HakanKokcu

Üçüncü taraf kütüphane bildirimleri [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) dosyasındadır. Geliştirmede yapay zekâ asistanı Claude'dan (Anthropic) yardım alınmıştır.
