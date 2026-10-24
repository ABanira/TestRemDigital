using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.Json;

namespace TestRemDigital
{
    /// <summary>
    /// Model penampung detail data per-siklus pengujian (Siklus 1, Siklus 2, dst.)
    /// </summary>
    public class SiklusDetailModel
    {
        public int NomorSiklus { get; set; } = 1;
        public double Charging { get; set; }
        // Charging AR (Auxiliary Reservoir) — newly added to record AR fill time separately
        public double ChargingAR { get; set; } = 0.0;
        public double Bocor { get; set; }
        public double Rem { get; set; }
        public double Release { get; set; }
        // BC readings captured during Service Brake stages
        public double BcAt05 { get; set; } = 0.0;
        public double BcAt10 { get; set; } = 0.0;
        public double BcAt15 { get; set; } = 0.0;
        public bool IsSiklusLulus { get; set; } = true;
        public string PathGrafikSiklus { get; set; } = "";
        public string Catatan { get; set; } = "";
        // Matriks Tekanan Udara per Fase [BP, AR, CR, BC]
        public double[] PressCharging { get; set; } = new double[4] { 0, 0, 0, 0 };
        public double[] PressLeak { get; set; } = new double[4] { 0, 0, 0, 0 };
        public double[] PressBrake { get; set; } = new double[4] { 0, 0, 0, 0 };
        public double[] PressRelease { get; set; } = new double[4] { 0, 0, 0, 0 };
    }

    public static class DatabaseHelper
    {
        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestRemData.db");
        private static string connectionString = $"Data Source={dbPath}";

        /// <summary>
        /// Fungsi inisialisasi awal untuk membuat file database dan tabel secara otomatis jika belum ada
        /// </summary>
        public static void InisialisasiDatabase()
        {
            using (var koneksi = new SqliteConnection(connectionString))
            {
                koneksi.Open();

                // 1. Membuat tabel awal jika belum ada
                string queryBuatTabel = @"
                CREATE TABLE IF NOT EXISTS RiwayatPengujian (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TanggalUji TEXT,
                    NomorKA TEXT,
                    ProgramPerawatan TEXT,
                    HasilCharging REAL,
                    HasilBocor REAL,
                    HasilRem REAL,
                    HasilRelease REAL,
                    StatusKelulusan TEXT,
                    PathGrafik TEXT,
                    DataSiklusJson TEXT
                );";

                using (var perintah = new SqliteCommand(queryBuatTabel, koneksi))
                {
                    perintah.ExecuteNonQuery();
                }

                // 2. KUNCI MITIGASI AMAN: Cek otomatis apakah database lama sudah punya kolom PathGrafik & DataSiklusJson
                TambahkanKolomJikaBelumAda(koneksi, "PathGrafik", "TEXT");
                TambahkanKolomJikaBelumAda(koneksi, "DataSiklusJson", "TEXT");
                // Tambahkan kolom evaluasi tambahan agar semua data ringkasan tersedia di level baris
                TambahkanKolomJikaBelumAda(koneksi, "HasilChargingAR", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "HasilBcAt05", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "HasilBcAt10", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "HasilBcAt15", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "HasilEmergencyBcMax", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "HasilReleaseArPressure", "REAL");
                TambahkanKolomJikaBelumAda(koneksi, "Catatan", "TEXT");
            }
        }

        private static void TambahkanKolomJikaBelumAda(SqliteConnection koneksi, string namaKolom, string tipeData)
        {
            try
            {
                string queryTambahKolom = $"ALTER TABLE RiwayatPengujian ADD COLUMN {namaKolom} {tipeData};";
                using (var perintahKolom = new SqliteCommand(queryTambahKolom, koneksi))
                {
                    perintahKolom.ExecuteNonQuery();
                }
            }
            catch (SqliteException)
            {
                // Catch diabaikan jika kolom sudah ada
            }
        }

        /// <summary>
        /// Fungsi simpan data hasil uji lama (1x Siklus) - Pertahankan untuk Backward Compatibility
        /// </summary>
        public static bool SimpanHasilUji(string? noKA, string? program, double charging, double bocor, double rem, double release, string? status, string? pathGambar, string? catatan = "")
        {
            var singleSiklus = new List<SiklusDetailModel>
            {
                new SiklusDetailModel
                {
                    NomorSiklus = 1,
                    Charging = charging,
                    Bocor = bocor,
                    Rem = rem,
                    Release = release,
                    IsSiklusLulus = status == "LULUS UJI",
                    PathGrafikSiklus = pathGambar ?? "",
                    Catatan = catatan ?? ""
                }
            };

            return SimpanHasilUjiMultiSiklus(noKA, program, charging, bocor, rem, release, status, pathGambar, singleSiklus, catatan);
        }

        /// <summary>
        /// Fungsi simpan data hasil uji BARU (Multi-Siklus)
        /// Menyimpan data Rata-Rata di kolom utama dan detail array siklus di kolom DataSiklusJson
        /// </summary>
        public static bool SimpanHasilUjiMultiSiklus(string? noKA, string? program, double avgCharging, double avgBocor, double avgRem, double avgRelease, string? statusAkhir, string? pathGambarUtama, List<SiklusDetailModel>? daftarSiklus, string? catatanUtama = "")
        {
            try
            {
                string jsonSiklus = JsonSerializer.Serialize(daftarSiklus ?? new List<SiklusDetailModel>());

                using (var koneksi = new SqliteConnection(connectionString))
                {
                    koneksi.Open();
                string queryInsert = @"
                        INSERT INTO RiwayatPengujian 
                        (TanggalUji, NomorKA, ProgramPerawatan, HasilCharging, HasilChargingAR, HasilBocor, HasilRem, HasilRelease, StatusKelulusan, PathGrafik, DataSiklusJson, HasilBcAt05, HasilBcAt10, HasilBcAt15, HasilEmergencyBcMax, HasilReleaseArPressure, Catatan) 
                        VALUES 
                        (@tanggal, @noKA, @program, @charging, @chargingAR, @bocor, @rem, @release, @status, @pathGrafik, @dataSiklusJson, @bc05, @bc10, @bc15, @emBcMax, @relAr, @catatan);
                        ";

                    using (var perintah = new SqliteCommand(queryInsert, koneksi))
                    {
                        perintah.Parameters.AddWithValue("@tanggal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        perintah.Parameters.AddWithValue("@noKA", noKA ?? "");
                        perintah.Parameters.AddWithValue("@program", program ?? "");
                        perintah.Parameters.AddWithValue("@charging", avgCharging);
                        perintah.Parameters.AddWithValue("@bocor", avgBocor);
                        perintah.Parameters.AddWithValue("@rem", avgRem);
                        perintah.Parameters.AddWithValue("@release", avgRelease);
                        perintah.Parameters.AddWithValue("@status", statusAkhir ?? "");
                        perintah.Parameters.AddWithValue("@pathGrafik", pathGambarUtama ?? "");
                        perintah.Parameters.AddWithValue("@dataSiklusJson", jsonSiklus);
                        perintah.Parameters.AddWithValue("@catatan", catatanUtama ?? "");
                        // compute additional summary values from daftarSiklus
                        double avgChargingAR = 0.0;
                        double avgBc05 = 0.0;
                        double avgBc10 = 0.0;
                        double avgBc15 = 0.0;
                        double avgEmergencyBcMax = 0.0;
                        double avgReleaseArPressure = 0.0;

                        if (daftarSiklus != null && daftarSiklus.Count > 0)
                        {
                            avgChargingAR = daftarSiklus.Average(x => x.ChargingAR);
                            avgBc05 = daftarSiklus.Average(x => x.BcAt05);
                            avgBc10 = daftarSiklus.Average(x => x.BcAt10);
                            avgBc15 = daftarSiklus.Average(x => x.BcAt15);
                            var withBrake = daftarSiklus.Where(x => x.PressBrake != null && x.PressBrake.Length > 3).ToList();
                            avgEmergencyBcMax = withBrake.Count > 0 ? withBrake.Average(x => x.PressBrake[3]) : 0.0;
                            var withRelease = daftarSiklus.Where(x => x.PressRelease != null && x.PressRelease.Length > 1).ToList();
                            avgReleaseArPressure = withRelease.Count > 0 ? withRelease.Average(x => x.PressRelease[1]) : 0.0;
                        }

                        perintah.Parameters.AddWithValue("@chargingAR", avgChargingAR);
                        perintah.Parameters.AddWithValue("@bc05", avgBc05);
                        perintah.Parameters.AddWithValue("@bc10", avgBc10);
                        perintah.Parameters.AddWithValue("@bc15", avgBc15);
                        perintah.Parameters.AddWithValue("@emBcMax", avgEmergencyBcMax);
                        perintah.Parameters.AddWithValue("@relAr", avgReleaseArPressure);

                        perintah.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] Gagal simpan hasil uji multi-siklus: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Model penampung internal saat query data dibaca dari database SQLite
        /// </summary>
        public class DataUjiTerarsip
        {
            public int Id { get; set; }
            public string TanggalFormat { get; set; } = "";
            public string NomorKA { get; set; } = "";
            public string ProgramPerawatan { get; set; } = "";
            public double HasilCharging { get; set; }
            public double HasilBocor { get; set; }
            public double HasilRem { get; set; }
            public double HasilRelease { get; set; }
            public string StatusKelulusan { get; set; } = "";
            public string PathGrafik { get; set; } = "";
            public string DataSiklusJson { get; set; } = "";
            public string Catatan { get; set; } = "";
            public bool IsLulus => StatusKelulusan == "LULUS UJI";

            // Expose properti DaftarSiklus agar bisa diakses langsung via DataGrid / UI
            public List<SiklusDetailModel> DaftarSiklus => GetDaftarSiklus();

            // Helper untuk mengonversi kembali JSON String ke List<SiklusDetailModel>
            public List<SiklusDetailModel> GetDaftarSiklus()
            {
                if (string.IsNullOrWhiteSpace(DataSiklusJson))
                    return new List<SiklusDetailModel>();

                try
                {
                    return JsonSerializer.Deserialize<List<SiklusDetailModel>>(DataSiklusJson) ?? new List<SiklusDetailModel>();
                }
                catch
                {
                    return new List<SiklusDetailModel>();
                }
            }
        }

        /// <summary>
        /// Fungsi untuk menarik seluruh data dari SQLite untuk ditampilkan ke DataGrid halaman riwayat
        /// </summary>
        public static List<DataUjiTerarsip> AmbilSemuaRiwayat()
        {
            var listHasil = new List<DataUjiTerarsip>();

            using (var koneksi = new SqliteConnection(connectionString))
            {
                koneksi.Open();
                string querySelect = "SELECT * FROM RiwayatPengujian ORDER BY TanggalUji DESC;";

                using (var perintah = new SqliteCommand(querySelect, koneksi))
                {
                    using (var reader = perintah.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime tglData = DateTime.Parse(reader["TanggalUji"].ToString() ?? DateTime.Now.ToString());

                            listHasil.Add(new DataUjiTerarsip
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                TanggalFormat = tglData.ToString("dd/MM/yyyy HH:mm"),
                                NomorKA = reader["NomorKA"].ToString() ?? "",
                                ProgramPerawatan = reader["ProgramPerawatan"].ToString() ?? "",
                                HasilCharging = Convert.ToDouble(reader["HasilCharging"]),
                                HasilBocor = Convert.ToDouble(reader["HasilBocor"]),
                                HasilRem = Convert.ToDouble(reader["HasilRem"]),
                                HasilRelease = Convert.ToDouble(reader["HasilRelease"]),
                                StatusKelulusan = reader["StatusKelulusan"].ToString() ?? "",
                                PathGrafik = reader["PathGrafik"] is DBNull ? "" : Convert.ToString(reader["PathGrafik"]) ?? "",
                                DataSiklusJson = reader["DataSiklusJson"] is DBNull ? "" : Convert.ToString(reader["DataSiklusJson"]) ?? "",
                                Catatan = reader["Catatan"] is DBNull ? "" : Convert.ToString(reader["Catatan"]) ?? ""
                            });
                        }
                    }
                }
            }

            return listHasil;
        }
    }
}