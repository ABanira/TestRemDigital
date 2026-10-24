using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TestRemDigital.Views
{
    public partial class CalibrationPage : Page
    {
        private ModbusService? _modbusService;
        private bool _isConnectedEver = false;

        private const int BUFFER_SIZE = 5; 
        private const double ZERO_THRESHOLD = 0.05;

        private readonly Queue<double> _bufferBP = new Queue<double>();
        private readonly Queue<double> _bufferAR = new Queue<double>();
        private readonly Queue<double> _bufferCR = new Queue<double>();
        private readonly Queue<double> _bufferBC = new Queue<double>();

        public CalibrationPage()
        {
            InitializeComponent();
            // Gunakan ModbusService bersama dari App untuk mencegah multiple SerialPort open
            _modbusService = App.SharedModbusService;
            if (_modbusService != null)
            {
                _modbusService.OnDataReceived += ModbusService_OnDataReceived;
                _isConnectedEver = true;
                UpdateStatusKoneksiUI(true);
            }

            TxtInfoKonfigurasi.Text = $"{ModbusConfig.PortName} | {ModbusConfig.BaudRate} bps | ID: {ModbusConfig.SlaveId} | Reg: {ModbusConfig.RegisterAddress}";
        }

        // Note: connection is managed by App.SharedModbusService; individual pages should not Start/Stop the port

        private void ModbusService_OnDataReceived(double bp, double ar, double cr, double bc)
        {
            double filteredBP = ApplyNoiseFilter(bp, _bufferBP);
            double filteredAR = ApplyNoiseFilter(ar, _bufferAR);
            double filteredCR = ApplyNoiseFilter(cr, _bufferCR);
            double filteredBC = ApplyNoiseFilter(bc, _bufferBC);

            this.Dispatcher.Invoke(() =>
            {
                if (!_isConnectedEver || TxtStatusKoneksi.Text == "DISCONNECTED")
                {
                    UpdateStatusKoneksiUI(true);
                    _isConnectedEver = true;
                }

                BarBP.Value = filteredBP;
                BarAR.Value = filteredAR;
                BarCR.Value = filteredCR;
                BarBC.Value = filteredBC;

                TxtBP.Text = $"{filteredBP:F2} Bar";
                TxtAR.Text = $"{filteredAR:F2} Bar";
                TxtCR.Text = $"{filteredCR:F2} Bar";
                TxtBC.Text = $"{filteredBC:F2} Bar";
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

        private void UpdateStatusKoneksiUI(bool isConnected)
        {
            if (isConnected)
            {
                TxtStatusKoneksi.Text = "CONNECTED TO WAVESHARE";
                BdrStatusKoneksi.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else
            {
                TxtStatusKoneksi.Text = "HARDWARE DISCONNECTED / TIMEOUT";
                BdrStatusKoneksi.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

                _bufferBP.Clear();
                _bufferAR.Clear();
                _bufferCR.Clear();
                _bufferBC.Clear();

                BarBP.Value = BarAR.Value = BarCR.Value = BarBC.Value = 0;
                TxtBP.Text = TxtAR.Text = TxtCR.Text = TxtBC.Text = "0.00 Bar";
            }
        }

        private void BtnKembali_Click(object sender, RoutedEventArgs e)
        {
            // Lepaskan subscription dari shared service saat kembali
            try { if (_modbusService != null) _modbusService.OnDataReceived -= ModbusService_OnDataReceived; } catch { }

            if (this.NavigationService != null)
            {
                this.NavigationService.GoBack();
            }
        }
    }
}