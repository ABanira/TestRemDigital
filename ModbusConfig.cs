using System;
using System.Collections.Generic;
using System.Text;

namespace TestRemDigital
{
    public static class ModbusConfig
    {
        // Nilai default awal sebelum diubah lewat halaman setting
        public static string PortName { get; set; } = "COM3";
        public static int BaudRate { get; set; } = 9600;
        public static byte SlaveId { get; set; } = 1;
        public static ushort RegisterAddress { get; set; } = 0x0000;
        public static double MaxSensorBar { get; set; } = 10.0; // Nilai default awal
    }
}
