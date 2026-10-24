using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;

using TestRemDigital;

namespace TestRemDigital.Views
{
    public partial class UjiPage : Page
    {
        #region Field & Properties
        private ModbusService? _modbusService;
        //private ModbusSimulatorService _modbusService;

        private const int BUFFER_SIZE = 5;
        private const double ZERO_THRESHOLD = 0.05;
        private readonly Queue<double> _bufferBP = new Queue<double>();
        private readonly Queue<double> _bufferAR = new Queue<double>();
        private readonly Queue<double> _bufferCR = new Queue<double>();
        private readonly Queue<double> _bufferBC = new Queue<double>();
        private const int MAX_POINTS = 600;
        private DateTime? waktuMasukRentang = null;

        private DispatcherTimer dataTimer;
        private bool isTimerBerjalan = false;
        private DateTime waktuMulaiFase;
        private string faseSekarang = "SELEKSI_MODE";

        // Data Sensor Aktual (Filtered)
        private double bpSimulasi = 0.0;
        private double arSimulasi = 0.0;
        private double crSimulasi = 0.0;
        private double bcSimulasi = 0.0;

        // Variabel Pengukuran & Standar Teknis per Siklus
        private double tekananAwalLeakBP = 0.0;
        private double tekananAwalBrakeBP = 0.0;
        private double waktuPenuhAR = 0.0;
        private double waktuPenuhCR = 0.0;
        private double totalPenurunanBP = 0.0;
        private double detikBrakeAktual = 0.0;
        private double detikReleaseAktual = 0.0;

        // Variabel Pelacak Service Brake Bertahap
        private int tahapBrakeAktual = 0;
        private double bcaSaat05 = 0.0;
        private double bcaSaat10 = 0.0;
        private double bcaSaat15 = 0.0;

        // Status Kelulusan Keseluruhan Per Siklus
        private bool statusPengujianLulus = true;

        // Data Identitas Pengujian
        private string nomorKA = "----";
        private string programPerawatan = "----";

        // Parameter Siklus Uji & Mode Fase
        private int jumlahSiklusUji = 1;
        private int siklusSaatIni = 1;
        private string faseUjiTerpilih = "Fase Lengkap";

        // List Penyimpan Data Hasil Setiap Siklus
        private List<SiklusDetailModel> listHasilSiklus = new List<SiklusDetailModel>();

        // LiveCharts Values
        private ChartValues<ObservableValue> valuesBP = new ChartValues<ObservableValue>();
        private ChartValues<ObservableValue> valuesAR = new ChartValues<ObservableValue>();
        private ChartValues<ObservableValue> valuesCR = new ChartValues<ObservableValue>();
        private ChartValues<ObservableValue> valuesBC = new ChartValues<ObservableValue>();

        private double[] pressCharging = new double[4] { 0, 0, 0, 0 };
        private double[] pressLeak = new double[4] { 0, 0, 0, 0 };
        private double[] pressBrake = new double[4] { 0, 0, 0, 0 };
        private double[] pressRelease = new double[4] { 0, 0, 0, 0 };
        #endregion

        #region Constructors
        public UjiPage()
        {
            InitializeComponent();
            InisialisasiGrafik();

            _modbusService = App.SharedModbusService;
            if (_modbusService != null)
            {
                _modbusService.OnDataReceived += ModbusService_OnDataReceived;
            }

            dataTimer = new DispatcherTimer();
            dataTimer.Interval = TimeSpan.FromMilliseconds(500);
            dataTimer.Tick += DataTimer_Tick;

            this.Loaded += UjiPage_Loaded;
            this.Unloaded += UjiPage_Unloaded;
        }

        public UjiPage(string noKA, string program) : this()
        {
            this.nomorKA = string.IsNullOrWhiteSpace(noKA) ? "----" : noKA;
            this.programPerawatan = string.IsNullOrWhiteSpace(program) ? "----" : program;
            UpdateIdentitasUI();
        }

        public UjiPage(string noKA, string program, int jumlahSiklus, string fase) : this(noKA, program)
        {
            this.jumlahSiklusUji = (jumlahSiklus >= 1 && jumlahSiklus <= 5) ? jumlahSiklus : 1;
            this.faseUjiTerpilih = string.IsNullOrWhiteSpace(fase) ? "Fase Lengkap" : fase;
            UpdateIdentitasUI();
        }
        #endregion

        #region Page Lifecycle & Initializer
        private void UjiPage_Loaded(object? sender, RoutedEventArgs? e)
        {
            UpdateIdentitasUI();
            InisialisasiFaseUji();

            // HAPUS ATAU COMMENT BAGIAN INI AGAR TIDAK BENTROK DENGAN APP.XAML.CS
            /*
            if (_modbusService != null)
            {
                _modbusService.Start(); 
            }
            */

            if (dataTimer != null)
            {
                dataTimer.Start();
            }
        }

        private void UjiPage_Unloaded(object? sender, RoutedEventArgs? e)
        {
            MatikanSeluruhSistem();
        }

        private void UpdateIdentitasUI()
        {
            var txtIdentitas = this.FindName("TxtHeaderInfo") as TextBlock;
            if (txtIdentitas != null)
            {
                txtIdentitas.Text = $"FASE: {this.faseUjiTerpilih.ToUpper()}";
            }

            var txtBadge = this.FindName("TxtSiklusBadge") as TextBlock;
            if (txtBadge != null)
            {
                txtBadge.Text = $"SIKLUS: {this.siklusSaatIni} / {this.jumlahSiklusUji}";
            }
        }

        private void InisialisasiFaseUji()
        {
            if (faseUjiTerpilih == "Fase Lengkap")
            {
                SetAlertInstruksi("Fase Lengkap Aktif. Silakan klik START pada 1. Pengisian (Charging)");
                AktifkanTombolBerikutnya(BtnStartCharging, "#10B981");
            }
            else if (faseUjiTerpilih.Contains("Charging"))
            {
                SetAlertInstruksi("Fase Tunggal: Pengisian Udara. Klik START untuk memulai.");
                AturTampilanFaseTunggal("Charging");
                AktifkanTombolBerikutnya(BtnStartCharging, "#10B981");
            }
            else if (faseUjiTerpilih.Contains("Leak"))
            {
                SetAlertInstruksi("Fase Tunggal: Kebocoran Pipa BP. Klik START untuk memulai.");
                AturTampilanFaseTunggal("Leak");
                AktifkanTombolBerikutnya(BtnStartLeak, "#3B82F6");
            }
            else if (faseUjiTerpilih.Contains("Service Brake"))
            {
                SetAlertInstruksi("Fase Tunggal: Service Brake Bertahap. Tarik handel mengalir.");
                AturTampilanFaseTunggal("ServiceBrake");
                AktifkanTombolBerikutnya(BtnStartServiceBrake, "#EF4444");

             
                    var guideBox = this.FindName("BdrSmartCaptureGuide") as Border;
                    if (guideBox != null) guideBox.Visibility = Visibility.Visible;
            }
            else if (faseUjiTerpilih.Contains("Emergency Brake"))
            {
                SetAlertInstruksi("Fase Tunggal: Emergency Brake. Klik START, lalu banting hendel darurat!");
                AturTampilanFaseTunggal("Emergency");
                AktifkanTombolBerikutnya(BtnStartEmergency, "#DC2626");
            }
            else if (faseUjiTerpilih.Contains("Release"))
            {
                SetAlertInstruksi("Fase Tunggal: Pelepasan Rem. Klik START, lalu rilis hendel!");
                AturTampilanFaseTunggal("Release");
                AktifkanTombolBerikutnya(BtnStartRelease, "#A855F7");
            }
        }

        private void AturTampilanFaseTunggal(string namaFaseAktif)
        {
            if (namaFaseAktif != "Charging") { GridFaseCharging.Visibility = Visibility.Collapsed; SepFaseCharging.Visibility = Visibility.Collapsed; }
            if (namaFaseAktif != "Leak") { GridFaseLeak.Visibility = Visibility.Collapsed; SepFaseLeak.Visibility = Visibility.Collapsed; }
            if (namaFaseAktif != "ServiceBrake") { GridFaseServiceBrake.Visibility = Visibility.Collapsed; SepFaseServiceBrake.Visibility = Visibility.Collapsed; }
            if (namaFaseAktif != "Emergency") { GridFaseEmergency.Visibility = Visibility.Collapsed; SepFaseEmergency.Visibility = Visibility.Collapsed; }
            if (namaFaseAktif != "Release") { GridFaseRelease.Visibility = Visibility.Collapsed; }
        }

        private void InisialisasiGrafik()
        {
            var seriesBP = new LineSeries { Title = "BP", Values = valuesBP, Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)), Fill = Brushes.Transparent, PointGeometry = null, StrokeThickness = 2 };
            var seriesAR = new LineSeries { Title = "AR", Values = valuesAR, Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)), Fill = Brushes.Transparent, PointGeometry = null, StrokeThickness = 2 };
            var seriesCR = new LineSeries { Title = "CR", Values = valuesCR, Stroke = new SolidColorBrush(Color.FromRgb(168, 85, 247)), Fill = Brushes.Transparent, PointGeometry = null, StrokeThickness = 2 };
            var seriesBC = new LineSeries { Title = "BC", Values = valuesBC, Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68)), Fill = Brushes.Transparent, PointGeometry = null, StrokeThickness = 2 };

            var chartTekanan = this.FindName("ChartUji") as CartesianChart;
            if (chartTekanan != null)
            {
                chartTekanan.Series = new SeriesCollection { seriesBP, seriesAR, seriesCR, seriesBC };
            }
        }
        #endregion

        #region Sensor Data & Filtering
        private void ModbusService_OnDataReceived(double bp, double ar, double cr, double bc)
        {
            this.Dispatcher.Invoke(() =>
            {
                bpSimulasi = ApplyNoiseFilter(bp, _bufferBP);
                arSimulasi = ApplyNoiseFilter(ar, _bufferAR);
                crSimulasi = ApplyNoiseFilter(cr, _bufferCR);
                bcSimulasi = ApplyNoiseFilter(bc, _bufferBC);
            });
        }

        private double ApplyNoiseFilter(double rawValue, Queue<double> buffer)
        {
            if (Math.Abs(rawValue) < ZERO_THRESHOLD)
            {
                rawValue = 0.0;
            }

            buffer.Enqueue(rawValue);
            if (buffer.Count > BUFFER_SIZE)
            {
                buffer.Dequeue();
            }

            return buffer.Average();
        }

        private void DataTimer_Tick(object? sender, EventArgs? e)
        {
            TxtLiveBP.Text = $"{bpSimulasi:F2} Bar";
            TxtLiveAR.Text = $"{arSimulasi:F2} Bar";
            TxtLiveCR.Text = $"{crSimulasi:F2} Bar";
            TxtLiveBC.Text = $"{bcSimulasi:F2} Bar";

            if (dataTimer != null && dataTimer.IsEnabled)
            {
                valuesBP.Add(new ObservableValue(bpSimulasi));
                valuesAR.Add(new ObservableValue(arSimulasi));
                valuesCR.Add(new ObservableValue(crSimulasi));
                valuesBC.Add(new ObservableValue(bcSimulasi));

                if (valuesBP.Count > MAX_POINTS) valuesBP.RemoveAt(0);
                if (valuesAR.Count > MAX_POINTS) valuesAR.RemoveAt(0);
                if (valuesCR.Count > MAX_POINTS) valuesCR.RemoveAt(0);
                if (valuesBC.Count > MAX_POINTS) valuesBC.RemoveAt(0);
            }

            EksekusiStateMesin();
        }
        #endregion

        #region State Machine Otomatis & Standar Pengujian
        private void EksekusiStateMesin()
        {
            double detikBerjalan = 0.0;
            if (isTimerBerjalan)
            {
                detikBerjalan = (DateTime.Now - waktuMulaiFase).TotalSeconds;
            }

            switch (faseSekarang)
            {
                // FASE 1: CHARGING
                case "CHARGING_WAIT":
                    if (bpSimulasi > 0.5 || arSimulasi > 0.5 || crSimulasi > 0.5)
                    {
                        waktuMulaiFase = DateTime.Now;
                        isTimerBerjalan = true;
                        faseSekarang = "CHARGING_RUNNING";
                        SetAlertInstruksi("Proses pengisian udara aktif. Menunggu AR & CR mencapai target penuh (4.8 Bar)...");
                    }
                    break;

                case "CHARGING_RUNNING":
                    TxtTimerCharging.Text = $"Waktu: {detikBerjalan:0.0}s (Target CR: 160-200s | AR: 38-135s)";

                    // Pencatatan waktu mandiri saat masing-masing reservoir menyentuh 4.8 Bar
                    if (arSimulasi >= 4.8 && waktuPenuhAR == 0)
                    {
                        waktuPenuhAR = detikBerjalan;
                    }
                    if (crSimulasi >= 4.8 && waktuPenuhCR == 0)
                    {
                        waktuPenuhCR = detikBerjalan;
                    }

                    if (detikBerjalan > 200)
                    {
                        isTimerBerjalan = false;
                        statusPengujianLulus = false;

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        SetAlertInstruksi("⚠️ CHARGING GAGAL (>200s). Klik START LEAK TEST.");
                        AktifkanTombolBerikutnya(BtnStartLeak, "#3B82F6");
                        faseSekarang = "SELEKSI_MODE";
                        return;
                    }

                    // Kondisi selesai jika kedua tangki (AR & CR) sudah penuh 4.8 Bar (atau BP sudah penuh)
                    if (arSimulasi >= 4.8 && crSimulasi >= 4.8 && bpSimulasi >= 4.8)
                    {
                        isTimerBerjalan = false;
                        pressCharging = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                        bool arLulus = (waktuPenuhAR >= 38 && waktuPenuhAR <= 135);
                        bool crLulus = (waktuPenuhCR >= 160 && waktuPenuhCR <= 200);

                        if (!arLulus || !crLulus)
                        {
                            statusPengujianLulus = false;
                            SetAlertInstruksi("⚠️ CHARGING TIDAK SESUAI STANDAR.");
                        }
                        else
                        {
                            SetAlertInstruksi("Charging Selesai & Memenuhi Syarat. Klik START LEAK TEST.");
                        }

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        AktifkanTombolBerikutnya(BtnStartLeak, "#3B82F6");
                        faseSekarang = "SELEKSI_MODE";
                    }
                    break;

                // FASE 2: LEAK TEST
                case "LEAK_WAIT":
                    tekananAwalLeakBP = bpSimulasi;
                    waktuMulaiFase = DateTime.Now;
                    isTimerBerjalan = true;
                    faseSekarang = "LEAK_RUNNING";
                    SetAlertInstruksi("Uji kebocoran BP berjalan selama 60 detik...");
                    TambahGarisPembatasFase("Leak", "#3B82F6");
                    break;

                case "LEAK_RUNNING":
                    TxtTimerLeak.Text = $"Waktu: {detikBerjalan:0.0}s / 60.0s";

                    if (detikBerjalan >= 60)
                    {
                        isTimerBerjalan = false;
                        totalPenurunanBP = tekananAwalLeakBP - bpSimulasi;
                        pressLeak = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                        if (totalPenurunanBP > 0.3)
                        {
                            statusPengujianLulus = false;
                        }

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        // Jika fase lengkap, lanjut ke service brake otomatis
                        SetAlertInstruksi("Leak Test Selesai. Klik START SERVICE BRAKE!");
                        AktifkanTombolBerikutnya(BtnStartServiceBrake, "#EF4444");
                        faseSekarang = "SELEKSI_MODE";
                    }
                    break;

                // FASE 3A: SERVICE BRAKE BERTAHAP (RENTANG TOLERANSI + STABILISASI 3 DETIK + LANJUT OTOMATIS)
                case "SERVICE_BRAKE_RUNNING":
                    double currentDrop = tekananAwalBrakeBP - bpSimulasi;
                    TxtTimerServiceBrake.Text = $"Drop BP: -{currentDrop:F2} Bar | Waktu: {detikBerjalan:0.0}s";

                    // Update tampilan angka drop tekanan besar di UI baru
                    var txtDropDisplay = this.FindName("TxtDropValueDisplay") as TextBlock;
                    if (txtDropDisplay != null) txtDropDisplay.Text = $"-{currentDrop:F2} Bar";

                    detikBrakeAktual = detikBerjalan;
                    var statusGuide = this.FindName("TxtSmartCaptureStatus") as TextBlock;

                    // PENGAMAN GLOBAL TIMEOUT (60 Detik): Jika waktu habis, biarkan data kosong/gagal, lalu lanjut mulus ke Emergency
                    if (detikBerjalan > 60.0)
                    {
                        isTimerBerjalan = false;
                        statusPengujianLulus = false;
                        waktuMasukRentang = null;
                        SetAlertInstruksi("⚠️ SERVICE BRAKE TIMEOUT (>60s). Data kosong. Melanjutkan otomatis ke Emergency...");

                        var guideBoxTimeout = this.FindName("BdrSmartCaptureGuide") as Border;
                        if (guideBoxTimeout != null) guideBoxTimeout.Visibility = Visibility.Collapsed;

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        // Lanjut mulus ke Emergency walau Service Brake tidak tuntas/kosong
                        AktifkanTombolBerikutnya(BtnStartEmergency, "#DC2626");
                        faseSekarang = "SELEKSI_MODE";
                        return;
                    }

                    // TAHAP 1: Target Nominal -0.5 Bar (Rentang: 0.45 - 0.75 Bar)
                    if (tahapBrakeAktual == 1)
                    {
                        if (currentDrop >= 0.45 && currentDrop <= 0.75)
                        {
                            if (waktuMasukRentang == null)
                                waktuMasukRentang = DateTime.Now;

                            double sisaStabil = 3.0 - (DateTime.Now - waktuMasukRentang.Value).TotalSeconds;
                            if (statusGuide != null) statusGuide.Text = $"⏳ Menstabilkan Tahap -0.5 Bar... ({Math.Max(0, sisaStabil):F1}s)";
                            SetAlertInstruksi($"⏳ TAHAP -0.5 BAR TERCAPAI. Menahan stabilisasi 3 detik... ({Math.Max(0, sisaStabil):F1}s)");

                            // Setelah stabil 3 detik penuh di rentang tahap 1
                            if ((DateTime.Now - waktuMasukRentang.Value).TotalSeconds >= 3.0)
                            {
                                bcaSaat05 = bcSimulasi;
                                if (bcaSaat05 < 0.7 || bcaSaat05 > 1.0) { statusPengujianLulus = false; }
                                tahapBrakeAktual = 2; // Naik ke tahap berikutnya
                                waktuMasukRentang = null; // Reset timer stabilisasi untuk tahap 2

                                if (statusGuide != null) statusGuide.Text = $"✅ Tahap -0.5 Bar Selesai & Stabil! (BC: {bcaSaat05:F2} Bar)";
                                SetAlertInstruksi("✅ TAHAP -0.5 BAR SELESAI! Lanjutkan ke -1.0 Bar...");
                            }
                        }
                        else
                        {
                            // Jika operator sempat masuk lalu keluar rentang sebelum 3 detik, reset hitungan stabilisasinya
                            if (waktuMasukRentang != null)
                            {
                                waktuMasukRentang = null;
                                SetAlertInstruksi("⚠️ Tekanan bergeser dari rentang Tahap 1. Tahan kembali di posisi...");
                            }
                        }
                    }

                    // TAHAP 2: Target Nominal -1.0 Bar (Rentang: 0.95 - 1.25 Bar)
                    else if (tahapBrakeAktual == 2)
                    {
                        if (currentDrop >= 0.95 && currentDrop <= 1.25)
                        {
                            if (waktuMasukRentang == null)
                                waktuMasukRentang = DateTime.Now;

                            double sisaStabil = 3.0 - (DateTime.Now - waktuMasukRentang.Value).TotalSeconds;
                            if (statusGuide != null) statusGuide.Text = $"⏳ Menstabilkan Tahap -1.0 Bar... ({Math.Max(0, sisaStabil):F1}s)";
                            SetAlertInstruksi($"⏳ TAHAP -1.0 BAR TERCAPAI. Menahan stabilisasi 3 detik... ({Math.Max(0, sisaStabil):F1}s)");

                            // Setelah stabil 3 detik penuh di rentang tahap 2
                            if ((DateTime.Now - waktuMasukRentang.Value).TotalSeconds >= 3.0)
                            {
                                bcaSaat10 = bcSimulasi;
                                if (bcaSaat10 < 1.8 || bcaSaat10 > 2.5) { statusPengujianLulus = false; }
                                tahapBrakeAktual = 3; // Naik ke tahap akhir
                                waktuMasukRentang = null;

                                if (statusGuide != null) statusGuide.Text = $"✅ Tahap -1.0 Bar Selesai & Stabil! (BC: {bcaSaat10:F2} Bar)";
                                SetAlertInstruksi("✅ TAHAP -1.0 BAR SELESAI! Tarik penuh ke -1.5 Bar...");
                            }
                        }
                        else
                        {
                            if (waktuMasukRentang != null)
                            {
                                waktuMasukRentang = null;
                            }
                        }
                    }

                    // TAHAP 3: Target Nominal -1.5 Bar / Full Service (Rentang: 1.45 - 1.80 Bar)
                    else if (tahapBrakeAktual == 3)
                    {
                        if (currentDrop >= 1.45 && currentDrop <= 1.80)
                        {
                            if (waktuMasukRentang == null)
                                waktuMasukRentang = DateTime.Now;

                            double sisaStabil = 3.0 - (DateTime.Now - waktuMasukRentang.Value).TotalSeconds;
                            if (statusGuide != null) statusGuide.Text = $"⏳ Menstabilkan Full Service -1.5 Bar... ({Math.Max(0, sisaStabil):F1}s)";
                            SetAlertInstruksi($"⏳ TAHAP -1.5 BAR TERCAPAI. Menahan stabilisasi 3 detik... ({Math.Max(0, sisaStabil):F1}s)");

                            // Setelah stabil 3 detik penuh di rentang tahap 3 (Selesai Service Brake)
                            if ((DateTime.Now - waktuMasukRentang.Value).TotalSeconds >= 3.0)
                            {
                                bcaSaat15 = bcSimulasi;
                                bool tekananMaxLulus = (bcSimulasi >= 3.7 && bcSimulasi <= 3.9);
                                bool step8Lulus = (bcaSaat15 >= 3.7 && bcaSaat15 <= 3.8);

                                if (!tekananMaxLulus || !step8Lulus) { statusPengujianLulus = false; }

                                isTimerBerjalan = false;
                                pressBrake = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                                if (statusGuide != null) statusGuide.Text = "✅ Full Service -1.5 Bar Tercatat & Selesai!";

                                var guideBoxFinish = this.FindName("BdrSmartCaptureGuide") as Border;
                                if (guideBoxFinish != null) guideBoxFinish.Visibility = Visibility.Collapsed;

                                waktuMasukRentang = null;

                                if (faseUjiTerpilih != "Fase Lengkap")
                                {
                                    ProsesSelesaiSiklusAtauLanjut();
                                    return;
                                }

                                // Selesai normal, lanjut ke Emergency
                                SetAlertInstruksi("✅ SERVICE BRAKE SELESAI. Kembalikan handel ke Release, lalu klik START EMERGENCY!");
                                AktifkanTombolBerikutnya(BtnStartEmergency, "#DC2626");
                                faseSekarang = "SELEKSI_MODE";
                                return;
                            }
                        }
                        else
                        {
                            if (waktuMasukRentang != null)
                            {
                                waktuMasukRentang = null;
                            }
                        }
                    }
                    break;

                // FASE 3B: EMERGENCY BRAKE (DENGAN TRIGGER STANDBY 0.05 BAR)
                case "EMERGENCY_WAIT":
                    // Menghitung selisih penurunan dari tekanan awal saat tombol start diklik
                    double dropAwalBP = tekananAwalBrakeBP - bpSimulasi;

                    // Jika terdeteksi penurunan minimal 0.05 bar (handel mulai dibanting)
                    if (dropAwalBP >= 0.05)
                    {
                        waktuMulaiFase = DateTime.Now;
                        isTimerBerjalan = true;
                        faseSekarang = "EMERGENCY_RUNNING";
                        SetAlertInstruksi("Trigger terdeteksi! Menghitung respons darurat instan...");
                    }
                    break;

                case "EMERGENCY_RUNNING":
                    TxtTimerEmergency.Text = $"Waktu Darurat: {detikBerjalan:0.0}s (BC: {bcSimulasi:F2} Bar)";
                    detikBrakeAktual = detikBerjalan;

                    // Pengaman Timeout (> 6 detik)
                    if (detikBerjalan > 6)
                    {
                        isTimerBerjalan = false;
                        statusPengujianLulus = false;
                        SetAlertInstruksi("⚠️ EMERGENCY BRAKE GAGAL (>6s).");

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }
                        AktifkanTombolBerikutnya(BtnStartRelease, "#A855F7");
                        faseSekarang = "SELEKSI_MODE";
                        return;
                    }

                    // Kondisi Berhasil: BC >= 3.5 Bar dan BP <= 0.5 Bar
                    if (bcSimulasi >= 3.7 && bpSimulasi <= 0.5)
                    {
                        isTimerBerjalan = false;
                        pressBrake = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                        if (faseUjiTerpilih.Contains("Emergency Brake"))
                        {
                            SetAlertInstruksi("✅ EMERGENCY BRAKE SELESAI!");
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        if (faseUjiTerpilih == "Fase Lengkap")
                        {
                            SetAlertInstruksi("Emergency Brake Selesai. Klik START RELEASE TEST!");
                            AktifkanTombolBerikutnya(BtnStartRelease, "#A855F7");
                            faseSekarang = "SELEKSI_MODE";
                        }
                    }
                    break;

                // FASE 3B: EMERGENCY BRAKE (RESPONS INSTAN)
                case "EMERGENCY_BRAKE_RUNNING":
                    TxtTimerEmergency.Text = $"Waktu Darurat: {detikBerjalan:0.0}s (BC: {bcSimulasi:F2} Bar)";
                    detikBrakeAktual = detikBerjalan;

                    if (detikBerjalan > 6)
                    {
                        isTimerBerjalan = false;
                        statusPengujianLulus = false;
                        SetAlertInstruksi("⚠️ EMERGENCY BRAKE GAGAL (>6s).");

                        if (faseUjiTerpilih != "Fase Lengkap")
                        {
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }
                        AktifkanTombolBerikutnya(BtnStartRelease, "#A855F7");
                        faseSekarang = "SELEKSI_MODE";
                        return;
                    }

                    if (bcSimulasi >= 3.7 && bpSimulasi <= 0.5)
                    {
                        isTimerBerjalan = false;
                        pressBrake = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                        if (faseUjiTerpilih.Contains("Emergency Brake"))
                        {
                            SetAlertInstruksi("✅ EMERGENCY BRAKE SELESAI!");
                            ProsesSelesaiSiklusAtauLanjut();
                            return;
                        }

                        if (faseUjiTerpilih == "Fase Lengkap")
                        {
                            SetAlertInstruksi("Emergency Brake Selesai. Klik START RELEASE TEST!");
                            AktifkanTombolBerikutnya(BtnStartRelease, "#A855F7");
                            faseSekarang = "SELEKSI_MODE";
                        }
                    }
                    break;

                // FASE 4: RELEASE TEST
                case "RELEASE_WAIT":
                    if (bpSimulasi > 0.5)
                    {
                        waktuMulaiFase = DateTime.Now;
                        isTimerBerjalan = true;
                        faseSekarang = "RELEASE_RUNNING";
                        SetAlertInstruksi("Proses pelepasan rem aktif...");
                        TambahGarisPembatasFase("Release", "#A855F7");
                    }
                    break;

                case "RELEASE_RUNNING":
                    TxtTimerRelease.Text = $"Waktu: {detikBerjalan:0.0}s (Target: 15 - 20s)";
                    detikReleaseAktual = detikBerjalan;

                    if (detikBerjalan > 20)
                    {
                        isTimerBerjalan = false;
                        statusPengujianLulus = false;
                        ProsesSelesaiSiklusAtauLanjut();
                        return;
                    }

                    if (bcSimulasi <= 0.4)
                    {
                        isTimerBerjalan = false;
                        pressRelease = new double[] { bpSimulasi, arSimulasi, crSimulasi, bcSimulasi };

                        if (detikBerjalan < 15 || detikBerjalan > 20)
                        {
                            statusPengujianLulus = false;
                        }

                        ProsesSelesaiSiklusAtauLanjut();
                    }
                    break;
            }
        }
        #endregion

        #region Event Handlers Tombol Start Fase
        private void BtnStartCharging_Click(object? sender, RoutedEventArgs? e)
        {
            BtnStartCharging.IsEnabled = false;
            waktuPenuhAR = 0;
            waktuPenuhCR = 0;
            faseSekarang = "CHARGING_WAIT";
            SetAlertInstruksi("Menunggu pengisian udara dimulai...");
            TambahGarisPembatasFase("Charging", "#10B981");
        }

        private void BtnStartLeak_Click(object? sender, RoutedEventArgs? e)
        {
            BtnStartLeak.IsEnabled = false;
            faseSekarang = "LEAK_WAIT";
        }

        private void BtnStartServiceBrake_Click(object? sender, RoutedEventArgs? e)
        {
            BtnStartServiceBrake.IsEnabled = false;
            tekananAwalBrakeBP = bpSimulasi;
            tahapBrakeAktual = 1;
            faseSekarang = "SERVICE_BRAKE_RUNNING";
            SetAlertInstruksi("Service Brake Aktif. Turunkan BP bertahap (0.5, 1.0, 1.5 Bar)...");
            TambahGarisPembatasFase("Service Brake", "#EF4444");
            waktuMulaiFase = DateTime.Now;
            isTimerBerjalan = true;
            // Show Smart Capture guide when service brake starts (both single and lengkap flows)
            var guideBox = this.FindName("BdrSmartCaptureGuide") as Border;
            if (guideBox != null) guideBox.Visibility = Visibility.Visible;
            var statusGuide = this.FindName("TxtSmartCaptureStatus") as TextBlock;
            if (statusGuide != null) statusGuide.Text = "DETEKSI OTOMATIS: Menunggu tekanan -0.5 / -1.0 / -1.5 Bar...";
        }

        private void BtnStartEmergency_Click(object? sender, RoutedEventArgs? e)
        {
            BtnStartEmergency.IsEnabled = false;

            // 1. Simpan tekanan BP saat tombol diklik sebagai titik referensi awal standby
            tekananAwalBrakeBP = bpSimulasi;

            // 2. Masuk ke mode standby, BUKAN langsung running
            faseSekarang = "EMERGENCY_WAIT";

            SetAlertInstruksi("Sistem Standby. Silakan banting handel emergency (Menunggu drop BP >= 0.05 Bar)...");
            TambahGarisPembatasFase("Emergency Brake", "#DC2626");
        }

        private void BtnStartRelease_Click(object? sender, RoutedEventArgs? e)
        {
            BtnStartRelease.IsEnabled = false;
            faseSekarang = "RELEASE_WAIT";
            SetAlertInstruksi("Menunggu respon pelepasan rem...");
        }
        #endregion

        #region Multi-Siklus, Evaluasi & Report Logic
        private void ProsesSelesaiSiklusAtauLanjut()
        {
            string pathSnapshot = TangkapSnapshotGrafik();

            var dataSiklus = new SiklusDetailModel
            {
                NomorSiklus = this.siklusSaatIni,
                ChargingAR = this.waktuPenuhAR, // <-- INI YANG KURANG SEBELUMNYA
                Charging = this.waktuPenuhCR,   // Ini untuk CR
                Bocor = this.totalPenurunanBP,
                Rem = this.detikBrakeAktual,
                Release = this.detikReleaseAktual,
                IsSiklusLulus = this.statusPengujianLulus,
                PathGrafikSiklus = pathSnapshot,
                PressCharging = (double[])this.pressCharging.Clone(),
                PressLeak = (double[])this.pressLeak.Clone(),
                PressBrake = (double[])this.pressBrake.Clone(),
                BcAt05 = this.bcaSaat05,
                BcAt10 = this.bcaSaat10,
                BcAt15 = this.bcaSaat15,
                PressRelease = (double[])this.pressRelease.Clone()
            };
            listHasilSiklus.Add(dataSiklus);

            if (this.siklusSaatIni < this.jumlahSiklusUji)
            {
                MessageBox.Show($"Siklus ke-{this.siklusSaatIni} selesai. Lanjut ke Siklus ke-{this.siklusSaatIni + 1}.",
                                "Lanjut Siklus", MessageBoxButton.OK, MessageBoxImage.Information);

                this.siklusSaatIni++;
                ResetStatusDanGrafikUntukSiklusBaru();
            }
            else
            {
                SelesaiSeluruhPengujian();
            }
        }

        private void ResetStatusDanGrafikUntukSiklusBaru()
        {
            valuesBP.Clear();
            valuesAR.Clear();
            valuesCR.Clear();
            valuesBC.Clear();

            var chartTekanan = this.FindName("ChartUji") as CartesianChart;
            if (chartTekanan != null)
            {
                chartTekanan.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (chartTekanan.AxisX != null && chartTekanan.AxisX.Count > 0)
                        {
                            chartTekanan.AxisX[0].Sections?.Clear();
                        }
                        chartTekanan.VisualElements?.Clear();
                    }
                    catch { }
                });
            }

            UpdateIdentitasUI();
            InisialisasiFaseUji();
            faseSekarang = "SELEKSI_MODE";
        }

        private void SelesaiSeluruhPengujian()
        {
            faseSekarang = "SELESAI";
            isTimerBerjalan = false;

            var btnSimpan = this.FindName("BtnSimpanHasil") as Button;
            if (btnSimpan != null)
            {
                btnSimpan.IsEnabled = true;
                btnSimpan.Background = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));
                btnSimpan.Foreground = Brushes.White;
            }

            string statusAkhirText = statusPengujianLulus ? "MEMENUHI SYARAT (LULUS)" : "TIDAK MEMENUHI SYARAT (GAGAL)";
            SetAlertInstruksi($"✅ UJI SELESAI [{statusAkhirText}]. Silakan klik 'SIMPAN HASIL UJI'.");
        }

        private void BtnSimpanHasil_Click(object? sender, RoutedEventArgs? e)
        {
            var overlay = this.FindName("OverlayInputKA") as Grid;
            if (overlay != null)
            {
                overlay.Visibility = Visibility.Visible;
                var txtNoKA = this.FindName("PopTxtNoKA") as TextBox;
                txtNoKA?.Focus();
            }
        }

        private void BtnSimpanLaporan_Click(object? sender, RoutedEventArgs? e)
        {
            var txtNoKA = this.FindName("PopTxtNoKA") as TextBox;
            var cmbPerawatan = this.FindName("PopCmbPerawatan") as ComboBox;
            var txtCatatan = this.FindName("PopTxtCatatan") as TextBox;
            string inputCatatan = txtCatatan != null ? txtCatatan.Text.Trim() : "";

            string inputNoKA = txtNoKA != null ? txtNoKA.Text.Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(inputNoKA) || inputNoKA == "-")
            {
                MessageBox.Show("Silakan masukkan Identitas Sarana terlebih dahulu!", "Validasi Identitas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputProgram = "P1";
            if (cmbPerawatan != null && cmbPerawatan.SelectedItem is ComboBoxItem selectedItem)
            {
                inputProgram = selectedItem.Content?.ToString() ?? "P1";
            }

            var overlay = this.FindName("OverlayInputKA") as Grid;
            if (overlay != null)
            {
                overlay.Visibility = Visibility.Collapsed;
            }

            NavigasiKeReportPage(inputNoKA, inputProgram, inputCatatan);
        }

        private void BtnBatalModal_Click(object? sender, RoutedEventArgs? e)
        {
            var overlay = this.FindName("OverlayInputKA") as Grid;
            if (overlay != null)
            {
                overlay.Visibility = Visibility.Collapsed;
            }
        }

        private void NavigasiKeReportPage(string noKA, string program, string catatan)
        {
            MatikanSeluruhSistem();

            bool totalLulus = listHasilSiklus.Count > 0 ? listHasilSiklus.All(s => s.IsSiklusLulus) : statusPengujianLulus;

            string pathGambarGrafikTerakhir = listHasilSiklus.Count > 0
                ? listHasilSiklus[listHasilSiklus.Count - 1].PathGrafikSiklus
                : TangkapSnapshotGrafik();

            this.NavigationService?.Navigate(new ReportPage(
                noKA,
                program,
                totalLulus,
                this.listHasilSiklus ?? new List<SiklusDetailModel>(),
                pathGambarGrafikTerakhir ?? "",
                catatan
            ));
        }

        private void BtnBatalUji_Click(object? sender, RoutedEventArgs? e)
        {
            MessageBoxResult konfirmasi = MessageBox.Show("Apakah Anda yakin ingin membatalkan pengujian?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (konfirmasi == MessageBoxResult.Yes)
            {
                MatikanSeluruhSistem();
                this.NavigationService?.Navigate(new MainMenuPage());
            }
        }
        #endregion

        #region Helper Functions (UI, Garis Grafik, Snapshot)
        private void TambahGarisPembatasFase(string namaFase, string warnaHex)
        {
            var chartTekanan = this.FindName("ChartUji") as CartesianChart;
            if (chartTekanan == null) return;

            chartTekanan.Dispatcher.Invoke(() =>
            {
                if (chartTekanan.AxisX.Count == 0)
                {
                    chartTekanan.AxisX.Add(new Axis { Title = "Waktu (Detik)", Sections = new SectionsCollection() });
                }

                var axisX = chartTekanan.AxisX[0];
                if (axisX.Sections == null) axisX.Sections = new SectionsCollection();

                Color warna = (Color)ColorConverter.ConvertFromString(warnaHex);

                var garisFase = new AxisSection
                {
                    Value = valuesBP.Count,
                    Stroke = new SolidColorBrush(warna),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                axisX.Sections.Add(garisFase);

                var labelBadge = new Border
                {
                    Background = new SolidColorBrush(warna),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Child = new TextBlock
                    {
                        Text = namaFase,
                        Foreground = Brushes.White,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold
                    }
                };

                var visualLabel = new VisualElement
                {
                    X = valuesBP.Count,
                    Y = 5.2,
                    UIElement = labelBadge
                };

                chartTekanan.VisualElements.Add(visualLabel);
            });
        }

        private void SetAlertInstruksi(string pesan)
        {
            var txtAlert = this.FindName("TxtAlertMessage") as TextBlock;
            if (txtAlert != null) txtAlert.Text = pesan.ToUpper();
        }

        private void AktifkanTombolBerikutnya(Button tombolTarget, string kodeWarnaHex)
        {
            if (tombolTarget != null)
            {
                tombolTarget.IsEnabled = true;
                tombolTarget.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kodeWarnaHex));
                tombolTarget.Foreground = Brushes.White;
            }
        }

        private void MatikanSeluruhSistem()
        {
            if (dataTimer != null && dataTimer.IsEnabled)
            {
                dataTimer.Stop();
            }
            if (_modbusService != null)
            {
                try
                {
                    // CUKUP LEPAS SUBSCRIPTION EVENT SAJA
                    _modbusService.OnDataReceived -= ModbusService_OnDataReceived;

                    // HAPUS ATAU COMMENT BARIS DI BAWAH INI AGAR PORT TIDAK MATI TOTAL
                    // _modbusService.Stop(); 
                }
                catch { }
            }
            // Ensure Smart Capture guide hidden when stopping
            var guideBox = this.FindName("BdrSmartCaptureGuide") as Border;
            if (guideBox != null) guideBox.Visibility = Visibility.Collapsed;
        }

        private string TangkapSnapshotGrafik()
        {
            try
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, $"Chart_Siklus{this.siklusSaatIni}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                var chartTekanan = this.FindName("ChartUji") as CartesianChart;
                if (chartTekanan != null && chartTekanan.ActualWidth > 0 && chartTekanan.ActualHeight > 0)
                {
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)chartTekanan.ActualWidth,
                        (int)chartTekanan.ActualHeight,
                        96, 96, PixelFormats.Pbgra32);

                    renderBitmap.Render(chartTekanan);

                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Snapshot Warning] Gagal menyimpan grafik: {ex.Message}");
            }

            return "";
        }
        #endregion

        private void BtnKembali_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }
            else
            {
                this.NavigationService.Navigate(new MainMenuPage());
            }
        }
    }
}