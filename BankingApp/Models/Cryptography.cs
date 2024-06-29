using BankingApp.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace BankingApp.Models
{
    public class Cryptography
    {
        private readonly string _privateKey;

        public Cryptography()
        {
            _privateKey = ConfigurationManager.AppSettings["PrivateKey"];
        }

        public string DecryptItem(string encryptedItem)
        {
            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_privateKey);

                    // Converting the encrypted item from base64 string to bytes.
                    var encryptedBytes = Convert.FromBase64String(encryptedItem);

                    // Decrypting the bytes.
                    var decryptedAsBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.Pkcs1);
                    var decryptedItem = Encoding.UTF8.GetString(decryptedAsBytes);

                    return decryptedItem;
                }
            }
            catch (FormatException ex)
            {
                Log.Error("Invalid format while decrypting the item.", ex);
                throw new CryptographicException("Invalid format while decrypting the item.", ex);
            }
            catch (CryptographicException ex)
            {
                Log.Error("Cryptographic exception occurred while decrypting the item.", ex);
                throw new CryptographicException("Cryptographic exception occurred while decrypting the item.", ex);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while decrypting the item.", ex);
                throw new CryptographicException("An error occurred while decrypting the item.", ex);
            }
        }

        public string GenerateRandomCertificate(int stringLength)
        {
            using (var rng = new RNGCryptoServiceProvider())
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