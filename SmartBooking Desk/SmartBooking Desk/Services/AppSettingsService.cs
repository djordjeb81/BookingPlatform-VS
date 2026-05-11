using System;
using System.IO;
using System.Text.Json;

namespace SmartBooking_Desk.Services
{
    public class AppSettingsService
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public AppSettingsService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartBookingDesk");

            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "app-settings.json");
        }

        public AppSettingsDto Load()
        {
            if (!File.Exists(_filePath))
                return new AppSettingsDto();

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettingsDto();

            return JsonSerializer.Deserialize<AppSettingsDto>(json, _jsonOptions)
                   ?? new AppSettingsDto();
        }

        public void Save(AppSettingsDto settings)
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }

    public class AppSettingsDto
    {
        public string LastLoginEmail { get; set; } = "";
    }
}