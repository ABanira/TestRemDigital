using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestRemDigital.Views
{
    public partial class LoadDataPage : Page
    {
        private List<DatabaseHelper.DataUjiTerarsip> masterDataRiwayat = new List<DatabaseHelper.DataUjiTerarsip>();

        private List<DatabaseHelper.DataUjiTerarsip> filteredData = new List<DatabaseHelper.DataUjiTerarsip>();

        private int pageSize = 10;
        private int currentPage = 1;
        private int totalPages = 1;

        public LoadDataPage()
        {
            InitializeComponent();
            MuatDataDariDatabase();
        }

        private void MuatDataDariDatabase()
        {
            try
            {
                masterDataRiwayat = DatabaseHelper.AmbilSemuaRiwayat();
                filteredData = new List<DatabaseHelper.DataUjiTerarsip>(masterDataRiwayat);

                TampilkanHalaman(1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat riwayat dari database: {ex.Message}", "Eror Sistem", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TampilkanHalaman(int page)
        {
            if (filteredData == null || filteredData.Count == 0)
            {
                DgRiwayatUji.ItemsSource = null;
                TxtInfoHalaman.Text = "Data 0 dari 0";
                BtnPrev.IsEnabled = false;
                BtnNext.IsEnabled = false;
                return;
            }

            totalPages = (int)Math.Ceiling((double)filteredData.Count / pageSize);
            currentPage = Math.Max(1, Math.Min(page, totalPages));

            var dataTampil = filteredData
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            DgRiwayatUji.ItemsSource = dataTampil;
            TxtInfoHalaman.Text = $"Hal {currentPage} dari {totalPages} ({filteredData.Count} Data)";

            BtnPrev.IsEnabled = (currentPage > 1);
            BtnNext.IsEnabled = (currentPage < totalPages);
        }

        private void TxtCariNoKA_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = TxtCariNoKA.Text.Trim().ToUpper();

            if (string.IsNullOrEmpty(keyword))
            {
                filteredData = new List<DatabaseHelper.DataUjiTerarsip>(masterDataRiwayat);
            }
            else
            {
                filteredData = masterDataRiwayat
                    .Where(x => x.NomorKA != null && x.NomorKA.ToUpper().Contains(keyword))
                    .ToList();
            }

            TampilkanHalaman(1);
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                TampilkanHalaman(currentPage - 1);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                TampilkanHalaman(currentPage + 1);
            }
        }

        private void BtnKembali_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new MainMenuPage());
        }

        private void BtnBukaDetail_Click(object sender, RoutedEventArgs e)
        {
            if (DgRiwayatUji.SelectedItem is not DatabaseHelper.DataUjiTerarsip dataTerpilih)
            {
                MessageBox.Show("Silakan pilih salah satu baris data kereta terlebih dahulu!", "Informasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ambil list siklus dari database
            List<SiklusDetailModel> listSiklus = dataTerpilih.DaftarSiklus ?? new List<SiklusDetailModel>();

            // Fallback: Jika arsip data lama belum memiliki DataSiklusJson, buat 1 siklus bawaan dari nilai summary
            if (listSiklus.Count == 0)
            {
                listSiklus.Add(new SiklusDetailModel
                {
                    NomorSiklus = 1,
                    Charging = dataTerpilih.HasilCharging,
                    Bocor = dataTerpilih.HasilBocor,
                    Rem = dataTerpilih.HasilRem,
                    Release = dataTerpilih.HasilRelease,
                    IsSiklusLulus = dataTerpilih.IsLulus,
                    PathGrafikSiklus = dataTerpilih.PathGrafik ?? ""
                });
            }

            // Panggil constructor 5 parameter milik ReportPage
            var halamanReview = new ReportPage(
                dataTerpilih.NomorKA ?? "",
                dataTerpilih.ProgramPerawatan ?? "",
                dataTerpilih.IsLulus,
                listSiklus,
                dataTerpilih.PathGrafik ?? "",
                dataTerpilih.Catatan ?? ""
            );

            this.NavigationService?.Navigate(halamanReview);
        }
    }
}