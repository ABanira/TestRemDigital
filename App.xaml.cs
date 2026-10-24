using System.Configuration;
using System.Data;
using System.Windows;

namespace TestRemDigital
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // KUNCI UTAMA: Panggil pembuatan database otomatis sebelum form jendela muncul
            DatabaseHelper.InisialisasiDatabase();
            // Inisialisasi ModbusService tunggal untuk dipakai oleh seluruh halaman
            try
            {
                SharedModbusService = new ModbusService();
                SharedModbusService.Start();
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Pastikan Modbus service dimatikan saat aplikasi keluar
            try { SharedModbusService?.Stop(); } catch { }
            base.OnExit(e);
        }

        // Shared ModbusService singleton untuk menghindari akses port berganda
        public static ModbusService? SharedModbusService { get; private set; }
    }

}
