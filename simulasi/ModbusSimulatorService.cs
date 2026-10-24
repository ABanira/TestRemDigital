using System;
using System.Windows.Threading;

namespace TestRemDigital
{
    public class ModbusSimulatorService
    {
        private DispatcherTimer? _simTimer;
        private double _mockBP = 5.0;
        private double _mockAR = 5.0;
        private double _mockCR = 5.0;
        private double _mockBC = 0.0;
        private int _counter = 0;

        public event Action<double, double, double, double>? OnDataReceived;

        public bool Start()
        {
            _counter = 0;
            _simTimer = new DispatcherTimer();
            _simTimer.Interval = TimeSpan.FromMilliseconds(500); // Trigger setiap 0.5 detik
            _simTimer.Tick += SimTimer_Tick;
            _simTimer.Start();
            return true;
        }

        public void Stop()
        {
            if (_simTimer != null)
            {
                _simTimer.Stop();
                _simTimer = null;
            }
        }

        private void SimTimer_Tick(object? sender, EventArgs? e)
        {
            _counter++;
            double t = _counter * 0.5; // Total detik berjalan

            // =========================================================================
            // 1. TEKANAN BP, AR, CR DI 5.0 BAR SELAMA 70 DETIK (0.0s - 70.0s)
            // =========================================================================
            if (t <= 70.0)
            {
                _mockBP = 5.0;
                _mockAR = 5.0;
                _mockCR = 5.0;
                _mockBC = 0.0;
            }

            // =========================================================================
            // 2. PENGEREMAN BERTAHAP / SERVICE BRAKE (70.0s - 100.0s)
            // =========================================================================
            // Step A: Penurunan BP 0,5 bar (BP = 4.5 bar) -> BC target 0.7 - 1.0 bar (0.85 bar)
            else if (t > 70.0 && t <= 80.0)
            {
                double step1 = (t - 70.0) / 10.0;
                _mockBP = 5.0 - (0.5 * step1);
                _mockBC = 0.85 * step1;
            }
            // Step B: Penurunan BP 1,0 bar (BP = 4.0 bar) -> BC target 1.8 - 2.5 bar (2.15 bar)
            else if (t > 80.0 && t <= 90.0)
            {
                double step2 = (t - 80.0) / 10.0;
                _mockBP = 4.5 - (0.5 * step2);
                _mockBC = 0.85 + ((2.15 - 0.85) * step2);
            }
            // Step C: Penurunan BP 1,5 bar / Full Service (BP = 3.5 bar) -> BC target 3.7 - 3.8 bar (3.75 bar)
            else if (t > 90.0 && t <= 100.0)
            {
                double step3 = (t - 90.0) / 10.0;
                _mockBP = 4.0 - (0.5 * step3);
                _mockBC = 2.15 + ((3.75 - 2.15) * step3);
            }

            // =========================================================================
            // 3. EMERGENCY BRAKE (100.0s - 105.0s) - Durasi 5 detik
            // BP dibuang cepat ke 0 bar, BC naik ke max 3.8 bar
            // =========================================================================
            else if (t > 100.0 && t <= 105.0)
            {
                double emgProgress = (t - 100.0) / 5.0;
                _mockBP = 3.5 * (1.0 - emgProgress);           // BP drop cepat ke 0.0 bar
                _mockBC = 3.75 + ((3.80 - 3.75) * emgProgress); // BC naik ke 3.8 bar
            }

            // =========================================================================
            // 4. RELEASE / PELEPASAN (105.0s - 130.0s)
            // BP diisi kembali ke 5.0 bar, BC turun ke 0.4 bar dalam 18 detik (Standar 15-20s)
            // =========================================================================
            else if (t > 105.0 && t <= 123.0)
            {
                double relProgress = (t - 105.0) / 18.0;
                _mockBP = 0.0 + (5.0 * relProgress);
                _mockBC = 3.80 - ((3.80 - 0.40) * relProgress); // BC turun hingga 0.4 bar
                _mockAR = 4.70 + (0.15 * relProgress);          // AR berada pada range 4.7 - 4.85 bar saat BC 0.4 bar
            }
            // Release Penuh hingga BC = 0.0 bar (123.0s - 130.0s)
            else if (t > 123.0 && t <= 130.0)
            {
                double finalRel = (t - 123.0) / 7.0;
                _mockBP = 5.0;
                _mockBC = 0.40 * (1.0 - finalRel);
                _mockAR = 4.85 + (0.15 * finalRel);
            }
            // Kondisi Akhir (Stabil / Standby Kembali)
            else
            {
                _mockBP = 5.0;
                _mockAR = 5.0;
                _mockCR = 5.0;
                _mockBC = 0.0;
            }

            // Invoke event data simulasi ke UI/Chart
            OnDataReceived?.Invoke(_mockBP, _mockAR, _mockCR, _mockBC);
        }
    }
}