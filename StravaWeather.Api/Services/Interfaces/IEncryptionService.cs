namespace StravaWeather.Api.Services.Interfaces
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        bool IsEncrypted(string value);
        string SafeEncrypt(string value);
        string SafeDecrypt(string value);
    }
}