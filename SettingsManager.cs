using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DeployKeyGitClient
{
    public static class SettingsManager
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeployKeyGitClient");
        private static readonly string FilePath = Path.Combine(Folder, "settings.json");
        private static Dictionary<string, string> _cache = LoadInternal();

        private static Dictionary<string, string> LoadInternal()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                if (!File.Exists(FilePath)) return new Dictionary<string, string>();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public static Dictionary<string, string> Load()
        {
            // return a copy
            return new Dictionary<string, string>(_cache);
        }

        public static void Save(string key, string value)
        {
            try
            {
                _cache[key] = value ?? "";
                var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // swallow saving errors to avoid interrupting UI
            }
        }
    }
}
