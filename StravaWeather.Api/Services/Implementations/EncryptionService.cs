using System.Security.Cryptography;
using System.Text;
using StravaWeather.Api.Services.Interfaces;

namespace StravaWeather.Api.Services.Implementations
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly ILogger<EncryptionService> _logger;
        private const int KeySize = 32; // 256 bits
        private const int IvSize = 16; // 128 bits
        private const int TagSize = 16; // 128 bits
        private const int SaltSize = 32; // 256 bits
        private const int Iterations = 100000;
        
        public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
        {
            _logger = logger;
            var encryptionKey = configuration["ENCRYPTION_KEY"] 
                ?? throw new InvalidOperationException("ENCRYPTION_KEY not configured");
            
            if (encryptionKey.Length < 32)
            {
                throw new InvalidOperationException("ENCRYPTION_KEY must be at least 32 characters");
            }
            
            // Derive a proper key using PBKDF2
            using var sha256 = SHA256.Create();
            var salt = sha256.ComputeHash(Encoding.UTF8.GetBytes("strava-weather-static-salt"));
            
            using var pbkdf2 = new Rfc2898DeriveBytes(encryptionKey, salt, Iterations, HashAlgorithmName.SHA256);
            _key = pbkdf2.GetBytes(KeySize);
        }
        
        public string Encrypt(string plainText)
        {
            try
            {
                // Generate random salt for this encryption
                var salt = RandomNumberGenerator.GetBytes(SaltSize);
                
                // Derive encryption key for this specific encryption
                using var pbkdf2 = new Rfc2898DeriveBytes(_key, salt, 1000, HashAlgorithmName.SHA256);
                var key = pbkdf2.GetBytes(KeySize);
                
                // Generate random IV
                var iv = RandomNumberGenerator.GetBytes(IvSize);
                
                using var aesGcm = new AesGcm(key, TagSize);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var cipherBytes = new byte[plainBytes.Length];
                var tag = new byte[TagSize];
                
                aesGcm.Encrypt(iv, plainBytes, cipherBytes, tag);
                
                // Combine salt, iv, tag, and encrypted data
                var result = new byte[salt.Length + iv.Length + tag.Length + cipherBytes.Length];
                salt.CopyTo(result, 0);
                iv.CopyTo(result, salt.Length);
                tag.CopyTo(result, salt.Length + iv.Length);
                cipherBytes.CopyTo(result, salt.Length + iv.Length + tag.Length);
                
                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                throw new InvalidOperationException("Failed to encrypt data", ex);
            }
        }
        
        public string Decrypt(string cipherText)
        {
            try
            {
                var combined = Convert.FromBase64String(cipherText);
                
                // Extract components
                var salt = new byte[SaltSize];
                var iv = new byte[IvSize];
                var tag = new byte[TagSize];
                var cipherBytes = new byte[combined.Length - SaltSize - IvSize - TagSize];
                
                Array.Copy(combined, 0, salt, 0, SaltSize);
                Array.Copy(combined, SaltSize, iv, 0, IvSize);
                Array.Copy(combined, SaltSize + IvSize, tag, 0, TagSize);
                Array.Copy(combined, SaltSize + IvSize + TagSize, cipherBytes, 0, cipherBytes.Length);
                
                // Derive decryption key
                using var pbkdf2 = new Rfc2898DeriveBytes(_key, salt, 1000, HashAlgorithmName.SHA256);
                var key = pbkdf2.GetBytes(KeySize);
                
                using var aesGcm = new AesGcm(key, TagSize);
                var plainBytes = new byte[cipherBytes.Length];
                
                aesGcm.Decrypt(iv, cipherBytes, tag, plainBytes);
                
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw new InvalidOperationException("Failed to decrypt data", ex);
            }
        }
        
        public bool IsEncrypted(string value)
        {
            try
            {
                var decoded = Convert.FromBase64String(value);
                return decoded.Length > SaltSize + IvSize + TagSize;
            }
            catch
            {
                return false;
            }
        }
        
        public string SafeEncrypt(string value)
        {
            return IsEncrypted(value) ? value : Encrypt(value);
        }
        
        public string SafeDecrypt(string value)
        {
            return !IsEncrypted(value) ? value : Decrypt(value);
        }
    }
}