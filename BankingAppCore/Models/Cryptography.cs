using BankingAppCore.Utilities;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;

namespace BankingAppCore.Models
{
    public class Cryptography
    {
        private readonly string _privateKey;
        private readonly ILogger<Cryptography> _logger;

        public Cryptography(IOptions<AppSettings> appSettings, ILogger<Cryptography> logger)
        {
            _privateKey = appSettings.Value.PrivateKey;
            _logger = logger;

            if (string.IsNullOrEmpty(_privateKey))
            {
                _logger.LogWarning("Private key variable not set.");
                throw new ApplicationException("Private key variable not set.");
            }
        }

        public string DecryptItem(string encryptedItem)
        {
            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_privateKey);
                    _logger.LogInformation("Decrypting item...");

                    // Converting the encrypted item from base64 string to bytes.
                    var encryptedBytes = Convert.FromBase64String(encryptedItem);

                    // Decrypting the bytes.
                    var decryptedAsBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.Pkcs1);
                    var decryptedItem = Encoding.UTF8.GetString(decryptedAsBytes);

                    _logger.LogInformation("Item decrypted.");
                    return decryptedItem;
                }
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid format while decrypting the item.");
                throw new CryptographicException("Invalid format while decrypting the item.", ex);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Cryptographic exception occurred while decrypting the item.");
                throw new CryptographicException("Cryptographic exception occurred while decrypting the item.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while decrypting the item.");
                throw new CryptographicException("An error occurred while decrypting the item.", ex);
            }
        }

        public string GenerateRandomCertificate(int stringLength)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bit_count = (stringLength * 6);
                var byte_count = ((bit_count + 7) / 8); // Rounded up
                var bytes = new byte[byte_count];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
        }
    }
}