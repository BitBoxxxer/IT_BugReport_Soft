using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LogViewer.Models;
using Newtonsoft.Json.Linq;

namespace LogViewer.Services
{
    public class LogDecryptor
    {
        private static readonly string EncryptionKey = "sodagrdp_it_top_college_2024_test_key";
        private static readonly byte[] KeyHash;

        static LogDecryptor()
        {
            using var sha256 = SHA256.Create();
            KeyHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(EncryptionKey));
        }

        public static string DecryptLogFile(string filePath)
        {
            try
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);

                if (encryptedData.Length <= 16)
                    throw new ArgumentException("Файл слишком мал для дешифровки");

                // Извлекаем IV (первые 16 байт)
                byte[] iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);

                // Извлекаем зашифрованные данные
                byte[] cipherText = new byte[encryptedData.Length - 16];
                Array.Copy(encryptedData, 16, cipherText, 0, cipherText.Length);

                using var aes = Aes.Create();
                aes.Key = KeyHash;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(cipherText);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);

                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка дешифровки: {ex.Message}", ex);
            }
        }

        public static List<LogEntry> ParseLogContent(string decryptedContent)
        {
            var entries = new List<LogEntry>();

            var lines = decryptedContent.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            foreach (var line in lines)
            {
                try
                {
                    var entry = ParseLogLine(line.Trim());
                    if (entry != null)
                        entries.Add(entry);
                }
                catch
                {
                    // Пропускаем некорректные строки
                }
            }

            return entries;
        }

        private static LogEntry? ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Формат: (Время)_(Тип_Действия)_(Описание || JSON)
            var timeEnd = line.IndexOf(')');
            if (timeEnd == -1) return null;

            var timestampStr = line.Substring(1, timeEnd - 1);
            if (!DateTime.TryParse(timestampStr, out var timestamp))
                return null;

            var remaining = line.Substring(timeEnd + 2); // Пропускаем ")_"
            var actionEnd = remaining.IndexOf(')');
            if (actionEnd == -1) return null;

            var actionType = remaining.Substring(0, actionEnd);

            var descriptionPart = remaining.Substring(actionEnd + 2); // Пропускаем ")_"
            if (descriptionPart.EndsWith(')'))
                descriptionPart = descriptionPart.Substring(0, descriptionPart.Length - 1);

            string description = descriptionPart;
            JObject? extraInfo = null;

            // Пытаемся извлечь JSON
            var jsonSeparator = descriptionPart.IndexOf(" || ");
            if (jsonSeparator != -1)
            {
                description = descriptionPart.Substring(0, jsonSeparator);
                var jsonPart = descriptionPart.Substring(jsonSeparator + 4);

                try
                {
                    extraInfo = JObject.Parse(jsonPart);
                }
                catch
                {
                    // Если JSON некорректен, оставляем как есть
                }
            }

            return new LogEntry
            {
                Timestamp = timestamp,
                ActionType = actionType,
                Description = description,
                ExtraInfo = extraInfo,
                RawEntry = line
            };
        }
    }
}