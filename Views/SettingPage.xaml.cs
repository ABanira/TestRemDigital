using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace TestRemDigital.Views
{
    public partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            MuatDaftarComPort();
            MuatKonfigurasiTersimpan();
        }

        private void MuatDaftarComPort()
        {
            CmbPortName.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                CmbPortName.Items.Add(port);
            }

            if (CmbPortName.Items.Count > 0)
            {
                CmbPortName.SelectedIndex = 0;
                ModbusConfig.PortName = CmbPortName.Text;
            }
            else
            {
                CmbPortName.Items.Add("-- HARDWARE BELUM DETEKSI --");
                CmbPortName.SelectedIndex = 0;
                ModbusConfig.PortName = "";
            }
        }

        private void MuatKonfigurasiTersimpan()
        {
            // 1. Ambil data hardware dari Properties.Settings
            int baud = Properties.Settings.Default.SavedBaudRate;
            int slave = Properties.Settings.Default.SavedSlaveId;
            int reg = Properties.Settings.Default.SavedRegisterAddress;
            double maxBar = Properties.Settings.Default.SavedMaxSensorBar;

            // Sinkronisasi Baud Rate
            foreach (ComboBoxItem item in CmbBaudRate.Items)
            {
                if (item.Content.ToString() == baud.ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }

            TxtSlaveId.Text = slave.ToString();
            TxtRegisterAddress.Text = reg.ToString();
            TxtMaxSensorBar.Text = maxBar > 0 ? maxBar.ToString("0.0") : "10.0";

            ModbusConfig.BaudRate = baud;
            ModbusConfig.SlaveId = (byte)slave;
            ModbusConfig.RegisterAddress = (ushort)reg;
            ModbusConfig.MaxSensorBar = maxBar;

            // 2. Ambil data Pengesahan TTD & Google Sheets dari Properties.Settings (dengan fallback string kosong jika belum diset)
            TxtNamaOperator.Text = Properties.Settings.Default.SavedNamaOperator ?? "";
            TxtNipOperator.Text = Properties.Settings.Default.SavedNipOperator ?? "";
            TxtNamaPengawas.Text = Properties.Settings.Default.SavedNamaPengawas ?? "";
            TxtNipPengawas.Text = Properties.Settings.Default.SavedNipPengawas ?? "";

            TxtGoogleWebhookUrl.Text = Properties.Settings.Default.SavedGoogleWebhookUrl ?? "";
            TxtGoogleSheetName.Text = string.IsNullOrEmpty(Properties.Settings.Default.SavedGoogleSheetName)
                ? "Hasil_Uji"
                : Properties.Settings.Default.SavedGoogleSheetName;
        }

        private void BtnSimpanSetting_Click(object sender, RoutedEventArgs e)
        {
            string portTerpilih = CmbPortName.Text;
            string baudRateTerpilih = CmbBaudRate.Text;

            if (portTerpilih.Contains("BELUM DETEKSI"))
            {
                MessageBox.Show("Gagal menyimpan! Hubungkan kabel USB-RS485 Converter terlebih dahulu agar port serial terdeteksi.", "Proteksi COM", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (!byte.TryParse(TxtSlaveId.Text, out byte slaveId) ||
                !ushort.TryParse(TxtRegisterAddress.Text, out ushort registerAddress) ||
                !double.TryParse(TxtMaxSensorBar.Text, out double maxSensorBar))
            {
                MessageBox.Show("Isian Slave ID, Register, atau Kapasitas Sensor harus berupa angka valid!", "Validasi Gagal", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // A. Simpan Konfigurasi Hardware
            Properties.Settings.Default.SavedBaudRate = int.Parse(baudRateTerpilih);
            Properties.Settings.Default.SavedSlaveId = slaveId;
            Properties.Settings.Default.SavedRegisterAddress = registerAddress;
            Properties.Settings.Default.SavedMaxSensorBar = maxSensorBar;

            // B. Simpan Konfigurasi Identitas TTD & Google Sheets
            Properties.Settings.Default.SavedNamaOperator = TxtNamaOperator.Text.Trim();
            Properties.Settings.Default.SavedNipOperator = TxtNipOperator.Text.Trim();
            Properties.Settings.Default.SavedNamaPengawas = TxtNamaPengawas.Text.Trim();
            Properties.Settings.Default.SavedNipPengawas = TxtNipPengawas.Text.Trim();

            Properties.Settings.Default.SavedGoogleWebhookUrl = TxtGoogleWebhookUrl.Text.Trim();
            Properties.Settings.Default.SavedGoogleSheetName = TxtGoogleSheetName.Text.Trim();

            // C. Kunci Permanen ke File .settings
            Properties.Settings.Default.Save();

            // D. Update Runtime Hardware Config
            ModbusConfig.PortName = portTerpilih;
            ModbusConfig.BaudRate = int.Parse(baudRateTerpilih);
            ModbusConfig.SlaveId = slaveId;
            ModbusConfig.RegisterAddress = registerAddress;
            ModbusConfig.MaxSensorBar = maxSensorBar;

            MessageBox.Show("Pengaturan System & Hardware BERHASIL DISIMPAN SECARA PERMANEN!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            this.NavigationService.Navigate(new MainMenuPage());
        }

        private void BtnKembali_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenuPage());
        }
    }
}