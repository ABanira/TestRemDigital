using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TestRemDigital.Views
{
    public partial class ReportPage : Page
    {
        private string nomorKA;
        private string programPerawatan;
        private bool isLulusAkhir;
        private List<SiklusDetailModel> listHasilSiklus;
        private string pathGrafikTerakhir;
        private string catatanLapangan;

        public ReportPage(string noKA, string program, bool statusLulus, List<SiklusDetailModel> listSiklus, string pathGrafik, string catatan)
        {
            InitializeComponent();
            this.nomorKA = noKA;
            this.programPerawatan = program;
            this.isLulusAkhir = statusLulus;

            // Bersihkan noise pada data siklus saat halaman dimuat
            this.listHasilSiklus = BersihkanNoiseDariList(listSiklus ?? new List<SiklusDetailModel>());
            this.pathGrafikTerakhir = pathGrafik;
            this.catatanLapangan = string.IsNullOrWhiteSpace(catatan) ? "-" : catatan;
            // Jalankan pengisian tampilan UI saat halaman dimuat
            this.Loaded += ReportPage_Loaded;
        }

        private void ReportPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Isi Tanggal Laporan
            TxtReportTanggal.Text = $"TANGGAL: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // 2. Isi Label Identitas Sarana (No KA & Program Perawatan)
            TxtLblNoKA.Text = string.IsNullOrEmpty(nomorKA) ? "-" : nomorKA;
            TxtLblProgramPerawatan.Text = string.IsNullOrEmpty(programPerawatan) ? "-" : programPerawatan;

            // 3. Set Kesimpulan Akhir Header
            if (isLulusAkhir)
            {
                BdrKesimpulanAkhir.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                TxtKesimpulanAkhir.Text = "LULUS UJI (PASS)";
            }
            else
            {
                BdrKesimpulanAkhir.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                TxtKesimpulanAkhir.Text = "TIDAK LULUS (FAIL)";
            }

            // 4. Muat Data Evaluasi Rata-rata ke Tabel Section 1
            MuatDataEvaluasiUI();

            // 5. Muat Data Dynamic Multi-Siklus ke DataGrid (Section 2)
            MuatDataMultiSiklusUI();

            // 6. Muat Ringkasan Tekanan Udara Siklus Terakhir (Section 3)
            MuatDataTekananTerakhirUI();

            // 7. Muat Gambar Grafik ke Image UI (Section 4)
            MuatGrafikUI();
        }

        private void SetSelectedComboBoxItem(ComboBox cmb, string targetValue)
        {
            if (cmb == null || string.IsNullOrEmpty(targetValue)) return;
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem cbItem)
                {
                    string content = cbItem.Content?.ToString() ?? "";
                    if (content.Contains(targetValue, StringComparison.OrdinalIgnoreCase))
                    {
                        cmb.SelectedItem = cbItem;
                        return;
                    }
                }
            }
        }

        private void MuatDataEvaluasiUI()
        {
            if (listHasilSiklus == null || listHasilSiklus.Count == 0) return;
            double avgChargingCR = listHasilSiklus.Average(s => s.Charging);
            double avgChargingAR = listHasilSiklus.Average(s => s.ChargingAR);
            double avgBocor = listHasilSiklus.Average(s => s.Bocor);
            double avgRem = listHasilSiklus.Average(s => s.Rem);
            double avgRelease = listHasilSiklus.Average(s => s.Release);
            double avgBc05 = listHasilSiklus.Average(s => s.BcAt05);
            double avgBc10 = listHasilSiklus.Average(s => s.BcAt10);
            double avgBc15 = listHasilSiklus.Average(s => s.BcAt15);
            double avgEmergencyBcMax = listHasilSiklus.Where(s => s.PressBrake != null && s.PressBrake.Length > 3).Any()
                ? listHasilSiklus.Where(s => s.PressBrake != null && s.PressBrake.Length > 3).Average(s => s.PressBrake[3])
                : 0.0;
            double avgReleaseArPressure = listHasilSiklus.Where(s => s.PressRelease != null && s.PressRelease.Length > 1).Any()
                ? listHasilSiklus.Where(s => s.PressRelease != null && s.PressRelease.Length > 1).Average(s => s.PressRelease[1])
                : 0.0;

            // Charging AR
            var txtAr = this.FindName("TxtHasilChargingAR") as TextBlock;
            var txtStatusAr = this.FindName("TxtStatusChargingAR") as TextBlock;
            if (txtAr != null) txtAr.Text = avgChargingAR == 0 ? "N/A" : $"{avgChargingAR:F1} Detik";
            if (txtStatusAr != null)
            {
                if (avgChargingAR == 0) { txtStatusAr.Text = "N/A"; txtStatusAr.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgChargingAR >= 37.99 && avgChargingAR <= 135.01; txtStatusAr.Text = pass ? "MEMENUHI" : "TIDAK"; txtStatusAr.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Charging CR
            var txtCr = this.FindName("TxtHasilChargingCR") as TextBlock;
            var txtStatusCr = this.FindName("TxtStatusChargingCR") as TextBlock;
            if (txtCr != null) txtCr.Text = avgChargingCR == 0 ? "N/A" : $"{avgChargingCR:F1} Detik";
            if (txtStatusCr != null)
            {
                if (avgChargingCR == 0) { txtStatusCr.Text = "N/A"; txtStatusCr.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgChargingCR >= 159.99 && avgChargingCR <= 200.01; txtStatusCr.Text = pass ? "MEMENUHI" : "TIDAK"; txtStatusCr.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Leak Test (Khusus Bocor, nilai 0 tetap valid/sempurna)
            TxtHasilBocor.Text = $"{avgBocor:F2} Bar";
            bool passLeak = avgBocor <= 0.305;
            TxtStatusBocor.Text = passLeak ? "MEMENUHI" : "TIDAK";
            TxtStatusBocor.Foreground = passLeak ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral;

            // Service Brake @0.5
            var txt05 = this.FindName("TxtBcAt05") as TextBlock;
            var txt05Status = this.FindName("TxtStatusBcAt05") as TextBlock;
            if (txt05 != null) txt05.Text = avgBc05 == 0 ? "N/A" : $"{avgBc05:F2} Bar";
            if (txt05Status != null)
            {
                if (avgBc05 == 0) { txt05Status.Text = "N/A"; txt05Status.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgBc05 >= 0.69 && avgBc05 <= 1.01; txt05Status.Text = pass ? "MEMENUHI" : "TIDAK"; txt05Status.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Service Brake @1.0
            var txt10 = this.FindName("TxtBcAt10") as TextBlock;
            var txt10Status = this.FindName("TxtStatusBcAt10") as TextBlock;
            if (txt10 != null) txt10.Text = avgBc10 == 0 ? "N/A" : $"{avgBc10:F2} Bar";
            if (txt10Status != null)
            {
                if (avgBc10 == 0) { txt10Status.Text = "N/A"; txt10Status.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgBc10 >= 1.79 && avgBc10 <= 2.51; txt10Status.Text = pass ? "MEMENUHI" : "TIDAK"; txt10Status.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Service Brake @1.5
            var txt15 = this.FindName("TxtBcAt15") as TextBlock;
            var txt15Status = this.FindName("TxtStatusBcAt15") as TextBlock;
            if (txt15 != null) txt15.Text = avgBc15 == 0 ? "N/A" : $"{avgBc15:F2} Bar";
            if (txt15Status != null)
            {
                if (avgBc15 == 0) { txt15Status.Text = "N/A"; txt15Status.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgBc15 >= 3.69 && avgBc15 <= 3.81; txt15Status.Text = pass ? "MEMENUHI" : "TIDAK"; txt15Status.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Emergency Response
            var txtEmResp = this.FindName("TxtEmergencyResponse") as TextBlock;
            var txtEmRespStatus = this.FindName("TxtStatusEmergencyResponse") as TextBlock;
            if (txtEmResp != null) txtEmResp.Text = avgRem == 0 ? "N/A" : $"{avgRem:F1} Detik";
            if (txtEmRespStatus != null)
            {
                if (avgRem == 0) { txtEmRespStatus.Text = "N/A"; txtEmRespStatus.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgRem >= 2.99 && avgRem <= 5.01; txtEmRespStatus.Text = pass ? "MEMENUHI" : "TIDAK"; txtEmRespStatus.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Emergency BC Max
            var txtEmBc = this.FindName("TxtEmergencyBcMax") as TextBlock;
            var txtEmBcStatus = this.FindName("TxtStatusEmergencyBcMax") as TextBlock;
            if (txtEmBc != null) txtEmBc.Text = avgEmergencyBcMax == 0 ? "N/A" : $"{avgEmergencyBcMax:F2} Bar";
            if (txtEmBcStatus != null)
            {
                if (avgEmergencyBcMax == 0) { txtEmBcStatus.Text = "N/A"; txtEmBcStatus.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgEmergencyBcMax >= 3.69 && avgEmergencyBcMax <= 3.91; txtEmBcStatus.Text = pass ? "MEMENUHI" : "TIDAK"; txtEmBcStatus.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Release Response
            TxtHasilRelease.Text = avgRelease == 0 ? "N/A" : $"{avgRelease:F1} Detik";
            var txtRelStatus = this.FindName("TxtStatusRelease") as TextBlock;
            if (txtRelStatus != null)
            {
                if (avgRelease == 0) { txtRelStatus.Text = "N/A"; txtRelStatus.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgRelease >= 14.99 && avgRelease <= 20.01; txtRelStatus.Text = pass ? "MEMENUHI" : "TIDAK"; txtRelStatus.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }

            // Release AR Pressure
            var txtRelAr = this.FindName("TxtReleaseArPressure") as TextBlock;
            var txtRelArStatus = this.FindName("TxtStatusReleaseArPressure") as TextBlock;
            if (txtRelAr != null) txtRelAr.Text = avgReleaseArPressure == 0 ? "N/A" : $"{avgReleaseArPressure:F2} Bar";
            if (txtRelArStatus != null)
            {
                if (avgReleaseArPressure == 0) { txtRelArStatus.Text = "N/A"; txtRelArStatus.Foreground = System.Windows.Media.Brushes.Gray; }
                else { bool pass = avgReleaseArPressure >= 4.69 && avgReleaseArPressure <= 4.86; txtRelArStatus.Text = pass ? "MEMENUHI" : "TIDAK"; txtRelArStatus.Foreground = pass ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightCoral; }
            }
        }
        private void MuatDataMultiSiklusUI()
        {
            var displayList = listHasilSiklus.Select(s => new SiklusDisplayViewModel
            {
                NomorSiklusText = $"Siklus {s.NomorSiklus}",
                ChargingARDisplay = $"{s.ChargingAR:F1}s",
                ChargingDisplay = $"{s.Charging:F1}s",
                ChargingBPDisplay = "-",
                BocorDisplay = $"{s.Bocor:F2}",
                RemDisplay = $"{s.Rem:F1}s",
                ReleaseDisplay = $"{s.Release:F1}s",
                StatusDisplay = s.IsSiklusLulus ? "LULUS (PASS)" : "GAGAL (FAIL)"
            }).ToList();

            DgMultiSiklus.ItemsSource = displayList;
        }
        private void MuatDataTekananTerakhirUI()
        {
            if (listHasilSiklus == null || listHasilSiklus.Count == 0) return;

            // Ambil data dari siklus terakhir
            var siklusAkhir = listHasilSiklus[listHasilSiklus.Count - 1];

            AssignTekananRow(siklusAkhir.PressCharging, TxtBpCharging, TxtArCharging, TxtCrCharging, TxtBcCharging);
            AssignTekananRow(siklusAkhir.PressLeak, TxtBpLeak, TxtArLeak, TxtCrLeak, TxtBcLeak);
            AssignTekananRow(siklusAkhir.PressBrake, TxtBpBrake, TxtArBrake, TxtCrBrake, TxtBcBrake);
            AssignTekananRow(siklusAkhir.PressRelease, TxtBpRelease, TxtArRelease, TxtCrRelease, TxtBcRelease);
        }

        private void AssignTekananRow(double[] press, TextBlock txtBp, TextBlock txtAr, TextBlock txtCr, TextBlock txtBc)
        {
            if (press == null || press.Length < 4) return;
            txtBp.Text = press[0].ToString("F2");
            txtAr.Text = press[1].ToString("F2");
            txtCr.Text = press[2].ToString("F2");
            txtBc.Text = press[3].ToString("F2");
        }

        private void MuatGrafikUI()
        {
            try
            {
                if (!string.IsNullOrEmpty(pathGrafikTerakhir) && File.Exists(pathGrafikTerakhir))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(pathGrafikTerakhir, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    ImgGrafikReport.Source = bitmap;
                    TxtAlertMessage.Text = string.Empty;
                }
                else
                {
                    TxtAlertMessage.Text = "Gambar grafik tren tidak ditemukan.";
                }
            }
            catch (Exception ex)
            {
                TxtAlertMessage.Text = $"Gagal memuat grafik: {ex.Message}";
            }
        }

        // Fungsi Filter Noise: Mengubah nilai sangat kecil menjadi 0.0
        private List<SiklusDetailModel> BersihkanNoiseDariList(List<SiklusDetailModel> listSiklus)
        {
            const double threshold = 0.05;

            foreach (var s in listSiklus)
            {
                if (Math.Abs(s.Charging) < threshold) s.Charging = 0.0;
                if (Math.Abs(s.Bocor) < threshold) s.Bocor = 0.0;
                if (Math.Abs(s.Rem) < threshold) s.Rem = 0.0;
                if (Math.Abs(s.Release) < threshold) s.Release = 0.0;

                BersihkanArrayTekanan(s.PressCharging, threshold);
                BersihkanArrayTekanan(s.PressLeak, threshold);
                BersihkanArrayTekanan(s.PressBrake, threshold);
                BersihkanArrayTekanan(s.PressRelease, threshold);
            }
            return listSiklus;
        }

        private void BersihkanArrayTekanan(double[] pressArray, double threshold)
        {
            if (pressArray == null) return;
            for (int i = 0; i < pressArray.Length; i++)
            {
                if (Math.Abs(pressArray[i]) < threshold)
                {
                    pressArray[i] = 0.0;
                }
            }
        }

        private void BtnMenuUtama_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new MainMenuPage());
        }

        private async void BtnSimpanCetak_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Menggunakan variabel global nomorKA dan programPerawatan langsung
                double avgCharging = listHasilSiklus.Count > 0 ? listHasilSiklus.Average(s => s.Charging) : 0;
                double avgBocor = listHasilSiklus.Count > 0 ? listHasilSiklus.Average(s => s.Bocor) : 0;
                double avgRem = listHasilSiklus.Count > 0 ? listHasilSiklus.Average(s => s.Rem) : 0;
                double avgRelease = listHasilSiklus.Count > 0 ? listHasilSiklus.Average(s => s.Release) : 0;
                string statusTeks = isLulusAkhir ? "LULUS UJI" : "TIDAK LULUS";

                // Simpan ke Database Lokal (SQLite)
                bool dbSukses = DatabaseHelper.SimpanHasilUjiMultiSiklus(
                    nomorKA,
                    programPerawatan,
                    avgCharging,
                    avgBocor,
                    avgRem,
                    avgRelease,
                    statusTeks,
                    pathGrafikTerakhir,
                    listHasilSiklus
                );

                if (!dbSukses)
                {
                    MessageBox.Show("Peringatan: Gagal menyimpan data ke database lokal SQLite.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Simpan ke File PDF
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Laporan_Uji_{nomorKA}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    DefaultExt = ".pdf",
                    Filter = "PDF Documents (.pdf)|*.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    PdfReportGenerator.GenerateReportPdf(
                        dialog.FileName,
                        nomorKA,
                        programPerawatan,
                        isLulusAkhir,
                        listHasilSiklus,
                        Properties.Settings.Default.SavedNamaOperator,
                        Properties.Settings.Default.SavedNipOperator,
                        Properties.Settings.Default.SavedNamaPengawas,
                        Properties.Settings.Default.SavedNipPengawas,
                        catatanLapangan
                    );
                }

                // Kirim Data ke Google Spreadsheet via Webhook URL
                await KirimKeGoogleSheetsAsync(nomorKA, programPerawatan, isLulusAkhir, listHasilSiklus);

                MessageBox.Show("Data berhasil disimpan ke Database, PDF tercetak, dan terkirim ke Google Spreadsheet!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                this.NavigationService?.Navigate(new MainMenuPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Terjadi eror saat menyimpan data: {ex.Message}", "Eror Sistem", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async System.Threading.Tasks.Task KirimKeGoogleSheetsAsync(string noKA, string program, bool statusLulus, List<SiklusDetailModel> listSiklus)
        {
            string webhookUrl = Properties.Settings.Default.SavedGoogleWebhookUrl;
            if (string.IsNullOrWhiteSpace(webhookUrl)) return;

            var payloadSiklusList = listSiklus.Select(s => new {
                nomor_siklus = s.NomorSiklus,
                charging_val = s.Charging,
                charging_status = (s.Charging >= 160 && s.Charging <= 200) ? "PASS" : "FAIL",
                leak_val = s.Bocor,
                leak_status = (s.Bocor <= 0.30) ? "PASS" : "FAIL",
                brake_val = s.Rem,
                brake_status = (s.Rem >= 3.00 && s.Rem <= 6.00) ? "PASS" : "FAIL",
                release_val = s.Release,
                release_status = (s.Release >= 15.00 && s.Release <= 20.00) ? "PASS" : "FAIL",
                press_charging = s.PressCharging,
                press_leak = s.PressLeak,
                press_brake = s.PressBrake,
                press_release = s.PressRelease,
                bc_stage_05 = s.BcAt05,
                bc_stage_10 = s.BcAt10,
                bc_stage_15 = s.BcAt15
            }).ToList();

            var payload = new
            {
                sheet_name = Properties.Settings.Default.SavedGoogleSheetName,
                tanggal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                no_ka = noKA,
                program_perawatan = program,
                status_lulus = statusLulus ? "LULUS" : "TIDAK LULUS",
                petugas = Properties.Settings.Default.SavedNamaOperator,
                nip_petugas = Properties.Settings.Default.SavedNipOperator,
                pengawas = Properties.Settings.Default.SavedNamaPengawas,
                nip_pengawas = Properties.Settings.Default.SavedNipPengawas,
                siklus_list = payloadSiklusList
            };

            try
            {
                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    await client.PostAsync(webhookUrl, content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloud Sync Warning] Gagal mengirim ke Google Sheets: {ex.Message}");
            }
        }
    }

    // ViewModel pendukung untuk Binding ke DataGrid UI ReportPage
    // ViewModel pendukung untuk Binding ke DataGrid UI ReportPage
    public class SiklusDisplayViewModel
    {
        public string NomorSiklusText { get; set; } = string.Empty;
        public string ChargingARDisplay { get; set; } = string.Empty;
        public string ChargingDisplay { get; set; } = string.Empty;
        public string ChargingBPDisplay { get; set; } = string.Empty;
        public string BocorDisplay { get; set; } = string.Empty;
        public string RemDisplay { get; set; } = string.Empty;
        public string ReleaseDisplay { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
    }

    public class PdfReportGenerator
    {
        public static void GenerateReportPdf(
            string filePath,
            string nomorKA,
            string programPerawatan,
            bool isLulusAkhir,
            List<SiklusDetailModel> listHasilSiklus,
            string namaOperator,
            string nipOperator,
            string namaPengawas,
            string nipPengawas,
            string catatan)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("DIREKTORAT PENGELOLAAN SARANA")
                           .Bold().FontSize(14).FontColor(Colors.Blue.Medium).AlignCenter();
                        col.Item().Text("FIELD SERVICE REPORT")
                           .Bold().FontSize(12).FontColor(Colors.Grey.Darken2).AlignCenter();
                        col.Item().Text("BALAI YASA MANGGARAI")
                           .FontSize(10).Italic().FontColor(Colors.Grey.Medium).AlignCenter();
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // A. Identitas Pengujian
                        col.Item().Text("A. IDENTITAS PENGUJIAN").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(120);
                                cols.RelativeColumn();
                            });

                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("1. Tanggal").Bold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($": {DateTime.Now:dd/MM/yyyy HH:mm}");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("2. Identitas").Bold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($": {nomorKA}");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("3. Program").Bold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($": {programPerawatan}");
                        });

                        col.Item().PaddingTop(8).Text("B. HASIL PENGUJIAN").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(30); // NO
                                cols.RelativeColumn(4);  // URAIAN
                                cols.RelativeColumn(2);  // STANDAR
                                cols.ConstantColumn(50); // SIKLUS 1
                                cols.ConstantColumn(50); // SIKLUS 2
                                cols.ConstantColumn(50); // SIKLUS 3
                                cols.ConstantColumn(50); // SIKLUS 4
                                cols.ConstantColumn(50); // SIKLUS 5
                                cols.ConstantColumn(70); // STATUS
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("NO").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("URAIAN").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("STANDAR").Bold();
                                for (int i = 1; i <= 5; i++) h.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text($"UJI {i}").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("STATUS").Bold();
                            });

                            // Helper universal yang aman untuk mencetak nilai siklus atau N/A jika tidak diuji
                            string GetCellText(int idx, Func<SiklusDetailModel, double> getValue, string format, bool isBocor = false)
                            {
                                if (listHasilSiklus == null || idx >= listHasilSiklus.Count) return "-";
                                double val = getValue(listHasilSiklus[idx]);
                                if (val == 0 && !isBocor) return "N/A";
                                return val.ToString(format);
                            }

                            int rowNo = 1;

                            // Pastikan list aman dari null
                            var safeList = listHasilSiklus ?? new List<SiklusDetailModel>();

                            // Add group header: Test Pengisian (Charging)
                            table.Cell().ColumnSpan(9).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(6).Text("Test Pengisian (Charging)").Bold();

                            // Test Pengisian (Charging) - AR
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Waktu pengisian Auxiliary Reservoir (AR) dari tekanan 0,0 kg/cm² s/d 4,8 kg/cm²");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("38 - 135 detik");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.ChargingAR, "F1"));

                            bool isTestedAR = safeList.Any(s => s.ChargingAR > 0);
                            bool rowPassAR = isTestedAR && safeList.All(s => s.ChargingAR == 0 || (s.ChargingAR >= 37.99 && s.ChargingAR <= 135.01));
                            string statusAR = !isTestedAR ? "N/A" : (rowPassAR ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusAR).Bold().FontColor(!isTestedAR ? Colors.Grey.Medium : (rowPassAR ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Test Pengisian (Charging) - CR
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Waktu pengisian Control Chamber (CR) dari tekanan 0,0 kg/cm² s/d 4,8 kg/cm²");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("160 - 200 detik");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.Charging, "F1"));

                            bool isTestedCR = safeList.Any(s => s.Charging > 0);
                            bool rowPassCR = isTestedCR && safeList.All(s => s.Charging == 0 || (s.Charging >= 159.99 && s.Charging <= 200.01));
                            string statusCR = !isTestedCR ? "N/A" : (rowPassCR ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusCR).Bold().FontColor(!isTestedCR ? Colors.Grey.Medium : (rowPassCR ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Add group header: Test Kebocoran
                            table.Cell().ColumnSpan(9).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(6).Text("Test Kebocoran").Bold();

                            // Test Kebocoran
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Penurunan Tekanan: Maksimal penurunan tekanan (drop) adalah 0,30 Bar");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("0.30 Bar");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.Bocor, "F2", true));

                            bool rowPassLeak = safeList.All(s => s.Bocor <= 0.305);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(rowPassLeak ? "MEMENUHI" : "TIDAK").Bold().FontColor(rowPassLeak ? Colors.Green.Medium : Colors.Red.Medium);
                            rowNo++;

                            // Add group header: Test Pelayanan Pengereman (Service Brake)
                            table.Cell().ColumnSpan(9).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(6).Text("Test Pelayanan Pengereman (Service Brake)").Bold();

                            // Service Brake rows (Drop 0.5)
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Pengurangan tekanan di Brake Pipe sebesar 0,5 bar");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("0,7 - 1,0 Bar (BC)");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.BcAt05, "F2"));

                            bool isTestedBc05 = safeList.Any(s => s.BcAt05 > 0);
                            bool rowPassBc05 = isTestedBc05 && safeList.All(s => s.BcAt05 == 0 || (s.BcAt05 >= 0.69 && s.BcAt05 <= 1.01));
                            string statusBc05 = !isTestedBc05 ? "N/A" : (rowPassBc05 ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusBc05).Bold().FontColor(!isTestedBc05 ? Colors.Grey.Medium : (rowPassBc05 ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Service Brake rows (Drop 1.0)
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Pengurangan tekanan di Brake Pipe sebesar 1,0 bar");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("1,8 - 2,5 Bar (BC)");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.BcAt10, "F2"));

                            bool isTestedBc10 = safeList.Any(s => s.BcAt10 > 0);
                            bool rowPassBc10 = isTestedBc10 && safeList.All(s => s.BcAt10 == 0 || (s.BcAt10 >= 1.79 && s.BcAt10 <= 2.51));
                            string statusBc10 = !isTestedBc10 ? "N/A" : (rowPassBc10 ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusBc10).Bold().FontColor(!isTestedBc10 ? Colors.Grey.Medium : (rowPassBc10 ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Service Brake rows (Drop 1.5)
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Pengurangan tekanan di Brake Pipe sebesar 1,5 bar (full service)");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("3,7 - 3,8 Bar (BC)");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.BcAt15, "F2"));

                            bool isTestedBc15 = safeList.Any(s => s.BcAt15 > 0);
                            bool rowPassBc15 = isTestedBc15 && safeList.All(s => s.BcAt15 == 0 || (s.BcAt15 >= 3.69 && s.BcAt15 <= 3.81));
                            string statusBc15 = !isTestedBc15 ? "N/A" : (rowPassBc15 ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusBc15).Bold().FontColor(!isTestedBc15 ? Colors.Grey.Medium : (rowPassBc15 ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Add group header: Test Pengereman Darurat (Emergency Brake)
                            table.Cell().ColumnSpan(9).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(6).Text("Test Pengereman Darurat (Emergency Brake)").Bold();

                            // Emergency Brake Response
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Brake Response (waktu sampai 95% pada brake cylinder)");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("3 - 5 detik");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.Rem, "F1"));

                            bool isTestedRem = safeList.Any(s => s.Rem > 0);
                            bool rowPassEmResp = isTestedRem && safeList.All(s => s.Rem == 0 || (s.Rem >= 2.99 && s.Rem <= 5.01));
                            string statusEmResp = !isTestedRem ? "N/A" : (rowPassEmResp ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusEmResp).Bold().FontColor(!isTestedRem ? Colors.Grey.Medium : (rowPassEmResp ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Emergency BC Max
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Tekanan maksimum pada Brake Cylinder");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("3.8 ± 0.1 bar");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => (s.PressBrake != null && s.PressBrake.Length > 3 ? s.PressBrake[3] : 0), "F2"));

                            bool isTestedEmBc = safeList.Any(s => s.PressBrake != null && s.PressBrake.Length > 3 && s.PressBrake[3] > 0);
                            bool rowPassEmBcMax = isTestedEmBc && safeList.Where(s => s.PressBrake != null && s.PressBrake.Length > 3).All(s => s.PressBrake[3] == 0 || (s.PressBrake[3] >= 3.69 && s.PressBrake[3] <= 3.91));
                            string statusEmBcMax = !isTestedEmBc ? "N/A" : (rowPassEmBcMax ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusEmBcMax).Bold().FontColor(!isTestedEmBc ? Colors.Grey.Medium : (rowPassEmBcMax ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Add group header: Test Pelepasan (Release)
                            table.Cell().ColumnSpan(9).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(6).Text("Test Pelepasan (Release)").Bold();

                            // Release Response
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Waktu pelepasan sampai BC ≤ 0.4 bar");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("15 - 20 detik");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => s.Release, "F1"));

                            bool isTestedRel = safeList.Any(s => s.Release > 0);
                            bool rowPassRel = isTestedRel && safeList.All(s => s.Release == 0 || (s.Release >= 14.99 && s.Release <= 20.01));
                            string statusRel = !isTestedRel ? "N/A" : (rowPassRel ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusRel).Bold().FontColor(!isTestedRel ? Colors.Grey.Medium : (rowPassRel ? Colors.Green.Medium : Colors.Red.Medium));
                            rowNo++;

                            // Release AR Pressure
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(rowNo.ToString());
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Tekanan Air Reservoir pada saat BC = 0.4 bar");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("4,7 - 4,85 bar");
                            for (int i = 0; i < 5; i++) table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(GetCellText(i, s => (s.PressRelease != null && s.PressRelease.Length > 1 ? s.PressRelease[1] : 0), "F2"));

                            bool isTestedRelAr = safeList.Any(s => s.PressRelease != null && s.PressRelease.Length > 1 && s.PressRelease[1] > 0);
                            bool rowPassRelAr = isTestedRelAr && safeList.Where(s => s.PressRelease != null && s.PressRelease.Length > 1).All(s => s.PressRelease[1] == 0 || (s.PressRelease[1] >= 4.69 && s.PressRelease[1] <= 4.86));
                            string statusRelAr = !isTestedRelAr ? "N/A" : (rowPassRelAr ? "MEMENUHI" : "TIDAK");
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                 .Text(statusRelAr).Bold().FontColor(!isTestedRelAr ? Colors.Grey.Medium : (rowPassRelAr ? Colors.Green.Medium : Colors.Red.Medium));
                        });

                        // Signatures
                        col.Item().PaddingTop(20).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Operator Penguji,").Bold();
                                c.Item().PaddingTop(40).AlignCenter().Text($"( {(string.IsNullOrEmpty(namaOperator) ? "Nama Operator" : namaOperator)} )");
                                c.Item().AlignCenter().Text($"NIPP. {(string.IsNullOrEmpty(nipOperator) ? "------------------------" : nipOperator)}");
                            });

                            r.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Pengawas QC,").Bold();
                                c.Item().PaddingTop(40).AlignCenter().Text($"( {(string.IsNullOrEmpty(namaPengawas) ? "Nama Pengawas" : namaPengawas)} )");
                                c.Item().AlignCenter().Text($"NIPP. {(string.IsNullOrEmpty(nipPengawas) ? "------------------------" : nipPengawas)}");
                            });
                        });
                        // C. CATATAN / KETERANGAN LAPANGAN
                        col.Item().PaddingTop(40).Text("C. CATATAN").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(string.IsNullOrEmpty(catatan) ? "-" : catatan).FontSize(9);
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Halaman ");
                        text.CurrentPageNumber();
                        text.Span(" dari ");
                        text.TotalPages();
                    });
                });

                foreach (var s in listHasilSiklus)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"DETAIL EVALUASI UJI - PENGUJIAN {s.NomorSiklus}").Bold().FontSize(11).FontColor(Colors.Blue.Medium);
                                row.RelativeItem().AlignRight().Text($"NO. KA: {nomorKA}").Bold().FontSize(9);
                            });
                            col.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);
                        });

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().Text($"1. EVALUASI PARAMETER PENGUJIAN {s.NomorSiklus}").Bold().FontSize(10);
                            col.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(1.5f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.5f);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Parameter Pengujian").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Standar Toleransi").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Hasil").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Status").Bold();
                                });
                                // Charging AR
                                bool naAR = s.ChargingAR == 0;
                                AddParameterRow(table, "Charging AR (Aux Reservoir)", "38.0 - 135.0 s", naAR ? "N/A" : $"{s.ChargingAR:F1} Detik", naAR ? true : (s.ChargingAR >= 37.99 && s.ChargingAR <= 135.01), naAR);

                                // Charging CR
                                bool naCR = s.Charging == 0;
                                AddParameterRow(table, "Charging CR (Control Reservoir)", "160.0 - 200.0 s", naCR ? "N/A" : $"{s.Charging:F1} Detik", naCR ? true : (s.Charging >= 159.99 && s.Charging <= 200.01), naCR);

                                // Kebocoran Pipa BP (Nilai 0 pada bocor tetap valid/sempurna)
                                AddParameterRow(table, "Kebocoran Pipa BP", "<= 0.30 Bar/Mnt", $"{s.Bocor:F2} Bar", s.Bocor <= 0.305, false);

                                // Braking Response
                                bool naRem = s.Rem == 0;
                                AddParameterRow(table, "Braking Response", "3.00 - 6.00 s", naRem ? "N/A" : $"{s.Rem:F1} Detik", naRem ? true : (s.Rem >= 2.99 && s.Rem <= 5.01), naRem);

                                // Releasing Response
                                bool naRel = s.Release == 0;
                                AddParameterRow(table, "Releasing Response", "15.00 - 20.00 s", naRel ? "N/A" : $"{s.Release:F1} Detik", naRel ? true : (s.Release >= 14.99 && s.Release <= 20.01), naRel);

                                // Service Brake Tahapan
                                bool naBc05 = s.BcAt05 == 0;
                                AddParameterRow(table, "Service Brake - BC @ Drop 0.5", "0.7 - 1.0 Bar", naBc05 ? "N/A" : $"{s.BcAt05:F2} Bar", naBc05 ? true : (s.BcAt05 >= 0.69 && s.BcAt05 <= 1.01), naBc05);

                                bool naBc10 = s.BcAt10 == 0;
                                AddParameterRow(table, "Service Brake - BC @ Drop 1.0", "1.8 - 2.5 Bar", naBc10 ? "N/A" : $"{s.BcAt10:F2} Bar", naBc10 ? true : (s.BcAt10 >= 1.79 && s.BcAt10 <= 2.51), naBc10);

                                bool naBc15 = s.BcAt15 == 0;
                                AddParameterRow(table, "Service Brake - BC @ Drop 1.5", "3.7 - 3.8 Bar", naBc15 ? "N/A" : $"{s.BcAt15:F2} Bar", naBc15 ? true : (s.BcAt15 >= 3.69 && s.BcAt15 <= 3.81), naBc15);
                            });

                            col.Item().PaddingVertical(8);

                            col.Item().Text($"2. RINGKASAN TEKANAN UDARA PER FASE PENGUJIAN {s.NomorSiklus} (BAR)").Bold().FontSize(10);
                            col.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Fase Pengujian").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("BP (Bar)").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("AR (Bar)").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("CR (Bar)").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("BC (Bar)").Bold();
                                });

                                AddPressureRow(table, "Akhir Charging", s.PressCharging);
                                AddPressureRow(table, "Akhir Uji Kebocoran", s.PressLeak);
                                AddPressureRow(table, "Akhir Pengereman", s.PressBrake);
                                AddPressureRow(table, "Akhir Pelepasan", s.PressRelease);
                            });

                            col.Item().PaddingVertical(8);

                            col.Item().Text($"3. GRAFIK TREN TEKANAN PENGUJIAN {s.NomorSiklus}").Bold().FontSize(10);
                            col.Item().PaddingTop(4);

                            if (!string.IsNullOrEmpty(s.PathGrafikSiklus) && File.Exists(s.PathGrafikSiklus))
                            {
                                col.Item().Height(220).Image(s.PathGrafikSiklus).FitArea();
                            }
                            else
                            {
                                col.Item().Height(100).Border(1).BorderColor(Colors.Grey.Lighten2)
                                   .AlignCenter().AlignMiddle().Text("Gambar Grafik Siklus Tidak Tersedia").Italic();
                            }
                        });

                        page.Footer().AlignRight().Text(text =>
                        {
                            text.Span("Halaman ");
                            text.CurrentPageNumber();
                            text.Span(" dari ");
                            text.TotalPages();
                        });
                    });
                }
            }).GeneratePdf(filePath);
        }

        private static void AddParameterRow(TableDescriptor table, string param, string standar, string hasil, bool isPass, bool isNa = false)
        {
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(param);
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(standar);
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(hasil);

            string statusText = isNa ? "N/A" : (isPass ? "MEMENUHI STANDAR" : "TIDAK MEMENUHI");
            var statusColor = isNa ? Colors.Grey.Medium : (isPass ? Colors.Green.Medium : Colors.Red.Medium);

            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                 .Text(statusText)
                 .Bold().FontColor(statusColor);
        }

        private static void AddPressureRow(TableDescriptor table, string faseName, double[] press)
        {
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(faseName);
            double bp = press != null && press.Length > 0 ? press[0] : 0;
            double ar = press != null && press.Length > 1 ? press[1] : 0;
            double cr = press != null && press.Length > 2 ? press[2] : 0;
            double bc = press != null && press.Length > 3 ? press[3] : 0;

            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{bp:F2}");
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{ar:F2}");
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{cr:F2}");
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{bc:F2}");
        }
    }
}