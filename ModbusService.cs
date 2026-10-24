using System;
using System.IO.Ports;
using System.Threading;
using System.Windows; 
using Modbus.Device;

namespace TestRemDigital
{
    public class ModbusService
    {
        private SerialPort? _serialPort;
        private IModbusSerialMaster? _modbusMaster;
        private Timer? _readTimer;

        // Event sekarang mengirim 4 data sekaligus: BP, AR, CR, BC
        public event Action<double, double, double, double>? OnDataReceived;

        public ModbusService()
        {
        }

        public bool Start()
        {
            try
            {
                _serialPort = new SerialPort(
                    ModbusConfig.PortName,
                    ModbusConfig.BaudRate,
                    Parity.None,
                    8,
                    StopBits.One
                );

                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;
                _serialPort.Open();

                _modbusMaster = ModbusSerialMaster.CreateRtu(_serialPort);
                _readTimer = new Timer(new TimerCallback(BacaDataBerkala), null, 0, 100);
                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(
                    $"ALAT UJI TIDAK TERDETEKSI!\n\nPeriksa kembali:\n1. Kabel USB-RS485 terpasang.\n2. Modul menyala.\n3. Port diset ke {ModbusConfig.PortName} via Menu Setting.",
                    "Koneksi Hardware Gagal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
        }

        private void BacaDataBerkala(object? state)
        {
            double bp = 0.0, ar = 0.0, cr = 0.0, bc = 0.0;

            try
            {
                if (_modbusMaster != null)
                {
                    // KUNCI UTAMA: Membaca 4 register sekaligus berurutan (Channel 1 sampai 4)
                    ushort[] registers = _modbusMaster.ReadInputRegisters(ModbusConfig.SlaveId, ModbusConfig.RegisterAddress, 4);

                    if (registers != null && registers.Length >= 4)
                    {
                        double kapasitasSensor = ModbusConfig.MaxSensorBar;

                        bp = HitungKalibrasiTekanan(registers[0], kapasitasSensor); // Channel 1
                        ar = HitungKalibrasiTekanan(registers[1], kapasitasSensor); // Channel 2
                        cr = HitungKalibrasiTekanan(registers[2], kapasitasSensor); // Channel 3
                        bc = HitungKalibrasiTekanan(registers[3], kapasitasSensor); // Channel 4
                    }
                }
            }
            catch (Exception)
            {
                // Jika putus koneksi, biarkan nol
                bp = ar = cr = bc = 0.0;
            }

            // Tembak keempat data sensor secara bersamaan ke UI
            OnDataReceived?.Invoke(bp, ar, cr, bc);
        }

        public void Stop()
        {
            _readTimer?.Dispose();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }

            _modbusMaster?.Dispose();
        }
        
        /// <summary>
        /// RUMUS KALIBRASI INDUSTRIAL: Mengonversi nilai mentah Modbus Waveshare menjadi Nilai BAR Riil
        /// </summary>
        /// <param name="nilaiMentah">Data integer yang dibaca dari register Waveshare</param>
        /// <param name="tekananMaxSensor">Kapasitas maksimal sensor Mas Bro (misal: 10.0 Bar atau 16.0 Bar)</param>
        private double HitungKalibrasiTekanan(ushort nilaiMentah, double tekananMaxSensor)
        {
            // CATATAN PENTING UNTUK WAVESHARE INDUSTRIAL 8-CH:
            // Ada 2 jenis output data dari modul Waveshare tergantung tipenya:
            // Tipe A (12-bit Raw Data): Arus 4mA = 819, Arus 20mA = 4095
            // Tipe B (Direct Current Data): Nilai register langsung berupa mikroampere (4mA = 4000, 20mA = 20000)

            // KITA GUNAKAN ASUMSI TIPE B (Direct Current dalam uA) yang paling sering dipakai Waveshare baru:
            double arusAktualMiliAmpere = nilaiMentah / 1000.0; // Mengubah uA ke mA (misal 4000uA -> 4.0mA)

            // Jika sensor tidak mendeteksi arus (di bawah 3.8 mA), anggap kabel putus atau angin 0 Bar
            if (arusAktualMiliAmpere < 3.8) return 0.0;

            // Rumus Interpolasi Linear Standar Instrumentasi: P = (I - 4) * (P_max / 16)
            double tekananHasilBar = (arusAktualMiliAmpere - 4.0) * (tekananMaxSensor / 16.0);

            // Proteksi angka negatif kecil di batas bawah
            if (tekananHasilBar < 0) tekananHasilBar = 0.0;

            return tekananHasilBar;
        }
    }
}