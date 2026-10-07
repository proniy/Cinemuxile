using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Курсач.Security
{
    public static class SecureDataManager
    {
        private static readonly string SecurityPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ad/security");
        private static readonly string SecureFile = Path.Combine(SecurityPath, "data.sec");

        public class SecureData
        {
            public string ConnectionString { get; set; }
            public string ApiKey { get; set; }
        }

        // === Публичные методы ===

        public static SecureData GetData()
        {
            if (!File.Exists(SecureFile))
                return null;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(SecureFile);
                byte[] decrypted = MultiDecrypt(fileBytes);
                string json = Encoding.UTF8.GetString(decrypted);
                return JsonConvert.DeserializeObject<SecureData>(json);
            }
            catch
            {
                return null; // ошибка расшифровки = повреждённый файл
            }
        }

        public static void SaveData(string connectionString, string apiKey)
        {
            var obj = new SecureData
            {
                ConnectionString = connectionString,
                ApiKey = apiKey
            };

            string json = JsonConvert.SerializeObject(obj);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = MultiEncrypt(bytes);
            File.WriteAllBytes(SecureFile, encrypted);
        }

        // === Многоступенчатое шифрование ===

        private static byte[] MultiEncrypt(byte[] input)
        {
            byte[] buffer = input;

            // фиксированные ключ и вектор, одинаковые для обеих сторон
            byte[] masterKey = Encoding.UTF8.GetBytes("MasterKey_For_App_1234567890!!"); // 32 байта
            byte[] masterIv = Encoding.UTF8.GetBytes("InitVector16bytes");               // 16 байт

            for (int i = 0; i < 10; i++)
            {
                // 1. Сжимаем
                buffer = Compress(buffer);

                // 2. Детерминированное получение ключа и IV (одинаково при шифр. и расшифр.)
                byte[] stepKey = DeriveBytes(masterKey, i, 32);
                byte[] stepIv = DeriveBytes(masterIv, i, 16);

                // 3. Шифруем
                buffer = Encrypt(buffer, stepKey, stepIv);

                // 4. Обратимая перестановка XOR
                buffer = XorTransform(buffer, (byte)(i * 17 + 23));
            }

            return buffer;
        }

        private static byte[] MultiDecrypt(byte[] input)
        {
            byte[] buffer = input;

            byte[] masterKey = Encoding.UTF8.GetBytes("MasterKey_For_App_1234567890!!");
            byte[] masterIv = Encoding.UTF8.GetBytes("InitVector16bytes");

            for (int i = 9; i >= 0; i--)
            {
                // 1. Обратный XOR
                buffer = XorTransform(buffer, (byte)(i * 17 + 23));

                // 2. Те же ключи и IV
                byte[] stepKey = DeriveBytes(masterKey, i, 32);
                byte[] stepIv = DeriveBytes(masterIv, i, 16);

                // 3. Расшифровываем
                buffer = Decrypt(buffer, stepKey, stepIv);

                // 4. Распаковываем
                buffer = Decompress(buffer);
            }

            return buffer;
        }

        // === Вспомогательные методы ===

        private static byte[] DeriveBytes(byte[] baseBytes, int round, int length)
        {
            using (var sha = SHA256.Create())
            {
                byte[] data = sha.ComputeHash(baseBytes.Concat(BitConverter.GetBytes(round)).ToArray());
                return data.Take(length).ToArray();
            }
        }

        private static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        private static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var input = new MemoryStream(data))
                using (var cs = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var output = new MemoryStream())
                {
                    cs.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                    gzip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] XorTransform(byte[] data, byte mask)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ mask);
            return result;
        }
    }
}
