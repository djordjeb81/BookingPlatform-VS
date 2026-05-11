using System;
using System.IO;
using System.Text.Json;
using SmartBooking_Desk.Models.Licensing;

namespace SmartBooking_Desk.Services.Licensing
{
    public class LocalLicenseCacheService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _filePath;

        public LocalLicenseCacheService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartBookingDesk");

            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "license-state.json");
        }

        public LocalLicenseStateDto Load()
        {
            if (!File.Exists(_filePath))
                return new LocalLicenseStateDto();

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new LocalLicenseStateDto();

            return JsonSerializer.Deserialize<LocalLicenseStateDto>(json, JsonOptions)
                   ?? new LocalLicenseStateDto();
        }

        public void Save(LocalLicenseStateDto state)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_filePath, json);
        }

        public void Clear()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
    }
}