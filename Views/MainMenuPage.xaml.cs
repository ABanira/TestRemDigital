using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestRemDigital.Views
{
    /// <summary>
    /// Interaction logic for MainMenuPage.xaml
    /// </summary>
    public partial class MainMenuPage : Page
    {
        public MainMenuPage()
        {
            InitializeComponent();
        }

        private void BtnTambahData_Click(object sender, RoutedEventArgs e)
        {
            // Pindah ke Halaman Tambah Data Form
            this.NavigationService.Navigate(new TambahDataPage());
        }

        private void BtnLoadData_Click(object sender, RoutedEventArgs e)
        {
            // Pindah ke Halaman Load Data Pencarian
            this.NavigationService.Navigate(new LoadDataPage());
        }

        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new SettingPage());
            }
        }
        private void BtnKalibrasi_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new CalibrationPage());
            }
        }

        private void BtnShutdownKomputer_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult konfirmasi = MessageBox.Show(
                "Apakah Anda yakin ingin KELUAR dari aplikasi Test Rem?\n\n" +
                "Pastikan seluruh data pengujian sarana telah tersimpan.",
                "Konfirmasi Keluar Aplikasi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (konfirmasi == MessageBoxResult.Yes)
            {
                try
                {
                    // Menutup seluruh aplikasi WPF secara bersih
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal menutup aplikasi: {ex.Message}", "Eror", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
