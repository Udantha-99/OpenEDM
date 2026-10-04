using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenEDMShellExtension.Core
{
    public class EncryptedLogResult
    {
        public string EncryptedAesKey { get; set; }
        public string AesIV { get; set; }
        public string Ciphertext { get; set; }
    }

    public static class CryptoHelper
    {
        private const string EmbeddedPublicKey = "<RSAKeyValue><Modulus>qcAL9IM7sXhBh2GKCFzM2YE6h79/lYXOl+lSNaYxp6HtsjmQTS9UCGxOjAToTxYbBcNeUi7k3iR11vNQ7DwYzA2uW+ikEnDVGVTu1YX+4ZhaEuJlBZaE4a9KLBCcs03SmQ2apUEk8rKFaKGkXAn+xKHbP8xiad4a8//xxr/P6KjwwGLLCc+su+yM0XTj2Z2QndrN73koaW56v5l6WZFvljlV3uReG7i75TJj+jYpqlzDlJGy8ybfmMwlJ11mK2RbMPlgtov6qMu/VFzbUc6bltv9AfwmyysZnWGVbfKRZ5QCLtx3els7ntJ7Km8zrlCSkKrWEHyActqZWrUtYCwDaQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public static string EncryptLogEntry(string jsonLog)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                byte[] encryptedData;
                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] logBytes = Encoding.UTF8.GetBytes(jsonLog);
                    encryptedData = encryptor.TransformFinalBlock(logBytes, 0, logBytes.Length);
                }

                byte[] encryptedKey;
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(EmbeddedPublicKey);
                    // Use fOAEP=false for PKCS#1 v1.5 padding to be compatible with standard
                    encryptedKey = rsa.Encrypt(aes.Key, false);
                }

                return "{\"EncryptedAesKey\":\"" + Convert.ToBase64String(encryptedKey) + "\",\"AesIV\":\"" + Convert.ToBase64String(aes.IV) + "\",\"Ciphertext\":\"" + Convert.ToBase64String(encryptedData) + "\"}";
            }
        }

        public static string DecryptLogEntry(string privateKeyXml, string encryptedPayloadJson)
        {
            var keyMatch = Regex.Match(encryptedPayloadJson, "\"EncryptedAesKey\"\\s*:\\s*\"([^\"]+)\"");
            var ivMatch = Regex.Match(encryptedPayloadJson, "\"AesIV\"\\s*:\\s*\"([^\"]+)\"");
            var cipherMatch = Regex.Match(encryptedPayloadJson, "\"Ciphertext\"\\s*:\\s*\"([^\"]+)\"");

            if (!keyMatch.Success || !ivMatch.Success || !cipherMatch.Success)
            {
                throw new FormatException("Invalid payload format.");
            }

            byte[] encryptedAesKey = Convert.FromBase64String(keyMatch.Groups[1].Value);
            byte[] iv = Convert.FromBase64String(ivMatch.Groups[1].Value);
            byte[] ciphertext = Convert.FromBase64String(cipherMatch.Groups[1].Value);

            byte[] aesKey;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                aesKey = rsa.Decrypt(encryptedAesKey, false);
            }

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = aesKey;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    byte[] decryptedData = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                    return Encoding.UTF8.GetString(decryptedData);
                }
            }
        }
    }
}

