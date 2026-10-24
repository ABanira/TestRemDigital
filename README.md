Markdown
# TestRemDigital

> Aplikasi desktop C# WPF yang dikembangkan untuk kebutuhan pengujian, pemantauan, dan pengelolaan data operasional sistem rem/komponen sarana secara terintegrasi.

---

## 🚀 Fitur Utama
* **Antarmuka Desktop Modern (WPF):** Dibangun menggunakan arsitektur .NET Framework dengan antarmuka yang responsif dan mudah digunakan.
* **Modul Pengujian (*Calibration & Testing*):** Menyediakan halaman khusus untuk melakukan simulasi serta kalibrasi pengukuran parameter sistem.
* **Manajemen & Log Data:** Fitur pencatatan, pemuatan, dan pelaporan data hasil pengujian secara terstruktur.
* **Integrasi Komunikasi Perangkat:** Mendukung protokol komunikasi industri (seperti Modbus) untuk interaksi langsung dengan perangkat keras pengujian.

---

## 🛠️ Tech Stack
* **Bahasa Pemrograman:** C#
* **Framework:** .NET Framework / WPF (Windows Presentation Foundation)
* **Arsitektur:** XAML-based UI, Modular Views & Services
* **Kontrol Versi:** Git & GitHub

---

## 📁 Struktur Project
```text
TestRemDigital/
│
├── Properties/          # Konfigurasi dan sumber daya aplikasi
├── Views/               # Halaman-halaman antarmuka WPF (Calibration, LoadData, Uji, dll)
├── simulasi/            # Layanan simulasi perangkat & komunikasi (Modbus, dll)
├── App.xaml             # Konfigurasi global aplikasi
├── MainWindow.xaml      # Jendela utama aplikasi
└── DatabaseHelper.cs    # Helper manajemen database/penyimpanan lokal
⚙️ Cara Menjalankan Project
Pastikan Anda sudah menginstal Visual Studio 2022 dengan beban kerja (workload) .NET Desktop Development.

Clone repository ini atau download ke komputer Anda:

Bash
Buka file solusi TestRemDigital.sln menggunakan Visual Studio 2022.

Lakukan Restore NuGet Packages jika diperlukan.

Tekan tombol Start / F5 untuk menjalankan aplikasi.

👤 Author
Harsa Bumi Rahman

GitHub: @ABanira


<FollowUp>Apakah draf README di atas sudah sesuai dengan gambaran project Anda, atau ada bagian fitur yang ingin ditambahkan?</FollowUp>
