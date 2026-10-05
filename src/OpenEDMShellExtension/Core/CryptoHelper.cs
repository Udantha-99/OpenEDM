using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

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
        private const string EmbeddedPublicKey = "<RSAKeyValue><Modulus>znAPG8iouxldGWNCMXEJE7/74GPO8IoGVU+zAnzR02Fhn+vvt7yF1XVM61LwafAHlrYDjxFf24gBNg0oJ8cJjCi9TaH8r2m+hmBR28NwUFdTAs1oDi5ocz9oGDp+B2xzQ+xpHV0X8CobrjQTFp1tKEdrmrCG3C3t27lhBmrmbu7+6xclnd7J0vVltsFXEgRZEVmyUlJt6PLVghbOG87xk3dGND5mJYV73RuH6tAh5CPtD4EtG8VTZ5PU8YcxNahnEGZdtRynwNaY24jMKYmfqvI/1AU1G2eOo1kw0ZZ8aPHxvpokBTi8Fb0+74d/PUvoz98KHNxSv1nnng8aAHP+EQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public static string EncryptLogEntry(string jsonLog)
        {
            byte[] aesKey = new byte[32];
            byte[] iv = new byte[12]; // 96-bit IV is standard for GCM
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(aesKey);
                rng.GetBytes(iv);
            }

            byte[] logBytes = Encoding.UTF8.GetBytes(jsonLog);
            byte[] ciphertext;

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(aesKey), 128, iv);
            cipher.Init(true, parameters);

            ciphertext = new byte[cipher.GetOutputSize(logBytes.Length)];
            int len = cipher.ProcessBytes(logBytes, 0, logBytes.Length, ciphertext, 0);
            cipher.DoFinal(ciphertext, len);

            byte[] encryptedKey;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(EmbeddedPublicKey);
                // Use fOAEP=true for RSA-OAEP
                encryptedKey = rsa.Encrypt(aesKey, true);
            }

            return "{\"EncryptedAesKey\":\"" + Convert.ToBase64String(encryptedKey) + "\",\"AesIV\":\"" + Convert.ToBase64String(iv) + "\",\"Ciphertext\":\"" + Convert.ToBase64String(ciphertext) + "\"}";
        }

        public static string DecryptLogEntry(string privateKeyXml, string encryptedPayloadJson)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                return DecryptLogEntry(rsa, encryptedPayloadJson);
            }
        }

        public static string DecryptLogEntry(RSACryptoServiceProvider rsa, string encryptedPayloadJson)
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

            // Use fOAEP=true for RSA-OAEP
            byte[] aesKey = rsa.Decrypt(encryptedAesKey, true);

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(aesKey), 128, iv);
            cipher.Init(false, parameters);

            byte[] plainText = new byte[cipher.GetOutputSize(ciphertext.Length)];
            int len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, plainText, 0);
            cipher.DoFinal(plainText, len);

            return Encoding.UTF8.GetString(plainText);
        }
    }
}
