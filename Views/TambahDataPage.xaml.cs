using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace TestRemDigital.Views
{
    public partial class TambahDataPage : Page
    {
        private bool _isUpdatingCheckboxes = false;

        public TambahDataPage()
        {
            InitializeComponent();
        }

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

        private void ChkFaseLengkap_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingCheckboxes) return;
            _isUpdatingCheckboxes = true;

            // Jika Lengkap dicentang, centang semua sub-fase
            if (ChkCharging != null) ChkCharging.IsChecked = true;
            if (ChkLeakTest != null) ChkLeakTest.IsChecked = true;
            if (ChkServiceBrake != null) ChkServiceBrake.IsChecked = true;
            if (ChkEmergencyBrake != null) ChkEmergencyBrake.IsChecked = true;
            if (ChkRelease != null) ChkRelease.IsChecked = true;

            _isUpdatingCheckboxes = false;
        }

        private void ChkFaseLengkap_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingCheckboxes) return;
            _isUpdatingCheckboxes = true;

            // Jika Lengkap di-uncheck, kosongkan semua
            if (ChkCharging != null) ChkCharging.IsChecked = false;
            if (ChkLeakTest != null) ChkLeakTest.IsChecked = false;
            if (ChkServiceBrake != null) ChkServiceBrake.IsChecked = false;
            if (ChkEmergencyBrake != null) ChkEmergencyBrake.IsChecked = false;
            if (ChkRelease != null) ChkRelease.IsChecked = false;

            _isUpdatingCheckboxes = false;
        }

        private void ChkFaseItem_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingCheckboxes) return;
            _isUpdatingCheckboxes = true;

            var clickedBox = sender as CheckBox;

            // Jika salah satu sub-fase satuan dicentang, uncheck "Lengkap" 
            // dan uncheck sub-fase lainnya agar hanya 1 yang aktif
            if (ChkFaseLengkap != null) ChkFaseLengkap.IsChecked = false;

            if (clickedBox != ChkCharging && ChkCharging != null) ChkCharging.IsChecked = false;
            if (clickedBox != ChkLeakTest && ChkLeakTest != null) ChkLeakTest.IsChecked = false;
            if (clickedBox != ChkServiceBrake && ChkServiceBrake != null) ChkServiceBrake.IsChecked = false;
            if (clickedBox != ChkEmergencyBrake && ChkEmergencyBrake != null) ChkEmergencyBrake.IsChecked = false;
            if (clickedBox != ChkRelease && ChkRelease != null) ChkRelease.IsChecked = false;

            _isUpdatingCheckboxes = false;
        }

        private void BtnMulaiUji_Click(object sender, RoutedEventArgs e)
        {
            string noKA = "-";
            string programPerawatan = "-";

            int jumlahSiklusDipilih = 1;
            if (CmbSiklus.SelectedItem is ComboBoxItem itemSiklus)
            {
                string textSiklus = itemSiklus.Content.ToString() ?? "1";
                Match match = Regex.Match(textSiklus, @"\d+");
                if (match.Success)
                {
                    int.TryParse(match.Value, out jumlahSiklusDipilih);
                }
            }

            //ModbusConfig.PortName = "SIMULATOR_PORT";

            // Tentukan Fase Pengujian Terpilih berdasarkan Checkbox yang aktif
            string faseTerpilih = "Fase Lengkap";
            if (ChkFaseLengkap != null && ChkFaseLengkap.IsChecked == true)
            {
                faseTerpilih = "Fase Lengkap";
            }
            else if (ChkCharging != null && ChkCharging.IsChecked == true)
            {
                faseTerpilih = "Fase Charging";
            }
            else if (ChkLeakTest != null && ChkLeakTest.IsChecked == true)
            {
                faseTerpilih = "Fase Leak Test";
            }
            else if (ChkServiceBrake != null && ChkServiceBrake.IsChecked == true)
            {
                faseTerpilih = "Fase Service Brake";
            }
            else if (ChkEmergencyBrake != null && ChkEmergencyBrake.IsChecked == true)
            {
                faseTerpilih = "Fase Emergency Brake";
            }
            else if (ChkRelease != null && ChkRelease.IsChecked == true)
            {
                faseTerpilih = "Fase Release Test";
            }

            this.NavigationService.Navigate(new UjiPage(noKA, programPerawatan, jumlahSiklusDipilih, faseTerpilih));
        }
    }
}