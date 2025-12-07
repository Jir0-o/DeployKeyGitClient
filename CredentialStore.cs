using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeployKeyGitClient
{
    public class CredentialStore
    {
        private const int SaltBytes = 16;
        private const int Pbkdf2Iter = 100_000; // good default
        private static readonly string StoreFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeployKeyGitClient");
        private static readonly string StoreFile = Path.Combine(StoreFolder, "credentials.json");

        private class StoreModel
        {
            public string? AdminSaltBase64 { get; set; }
            public string? AdminHashBase64 { get; set; }
            public string? UserSaltBase64 { get; set; }
            public string? UserHashBase64 { get; set; }
        }

        private StoreModel _model = new();

        public CredentialStore()
        {
            Load();
        }

        private void EnsureFolder()
        {
            if (!Directory.Exists(StoreFolder)) Directory.CreateDirectory(StoreFolder);
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(StoreFile)) { _model = new StoreModel(); return; }
                var txt = File.ReadAllText(StoreFile, Encoding.UTF8);
                _model = JsonSerializer.Deserialize<StoreModel>(txt) ?? new StoreModel();
            }
            catch
            {
                _model = new StoreModel();
            }
        }

        private void Save()
        {
            try
            {
                EnsureFolder();
                var txt = JsonSerializer.Serialize(_model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StoreFile, txt, Encoding.UTF8);
            }
            catch { /* ignore save failures (optionally log) */ }
        }

        private static byte[] GenerateSalt()
        {
            var s = new byte[SaltBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(s);
            return s;
        }

        private static byte[] HashPassword(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iter, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // 256-bit
        }

        // Admin password APIs
        public bool HasAdminPassword() => !string.IsNullOrEmpty(_model.AdminHashBase64) && !string.IsNullOrEmpty(_model.AdminSaltBase64);

        public void SetAdminPassword(string adminPlain)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(adminPlain ?? "", salt);
            _model.AdminSaltBase64 = Convert.ToBase64String(salt);
            _model.AdminHashBase64 = Convert.ToBase64String(hash);
            Save();
        }

        public bool VerifyAdminPassword(string adminPlain)
        {
            if (!HasAdminPassword()) return false;
            var salt = Convert.FromBase64String(_model.AdminSaltBase64!);
            var expect = Convert.FromBase64String(_model.AdminHashBase64!);
            var got = HashPassword(adminPlain ?? "", salt);
            return CryptographicOperations.FixedTimeEquals(expect, got);
        }

        // User (unlock) password APIs
        public bool HasUserPassword() => !string.IsNullOrEmpty(_model.UserHashBase64) && !string.IsNullOrEmpty(_model.UserSaltBase64);

        public void SetUserPassword(string userPlain)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(userPlain ?? "", salt);
            _model.UserSaltBase64 = Convert.ToBase64String(salt);
            _model.UserHashBase64 = Convert.ToBase64String(hash);
            Save();
        }

        public bool VerifyUserPassword(string userPlain)
        {
            if (!HasUserPassword()) return false;
            var salt = Convert.FromBase64String(_model.UserSaltBase64!);
            var expect = Convert.FromBase64String(_model.UserHashBase64!);
            var got = HashPassword(userPlain ?? "", salt);
            return CryptographicOperations.FixedTimeEquals(expect, got);
        }
    }
}
