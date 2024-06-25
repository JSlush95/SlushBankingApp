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

        public string DecryptID(string encryptedID)
        {
            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_privateKey);

                    // Converting the encrypted key ID from base64 string to bytes.
                    var encryptedBytesID = Convert.FromBase64String(encryptedID);

                    // Decrypting the bytes.
                    var decryptedBytesID = rsa.Decrypt(encryptedBytesID, RSAEncryptionPadding.Pkcs1);
                    var decryptedID = Encoding.UTF8.GetString(decryptedBytesID);

                    return decryptedID;
                }
            }
            catch (FormatException ex)
            {
                Log.Error("Invalid format while decrypting ID.", ex);
                throw new CryptographicException("Invalid format while decrypting ID.", ex);
            }
            catch (CryptographicException ex)
            {
                Log.Error("Cryptographic exception occurred while decrypting ID.", ex);
                throw new CryptographicException("Cryptographic exception occurred while decrypting ID.", ex);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while decrypting ID.", ex);
                throw new CryptographicException("An error occurred while decrypting ID.", ex);
            }
        }

        public string GenerateRandomCertificate(int stringLength)
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bit_count = (stringLength * 6);
                var byte_count = ((bit_count + 7) / 8); // rounded up
                var bytes = new byte[byte_count];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
        }
    }
}