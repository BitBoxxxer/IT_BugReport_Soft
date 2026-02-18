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

                if (encryptedData.Length == 0)
                    throw new ArgumentException("File is empty");

                if (encryptedData.Length <= 16)
                    throw new ArgumentException($"File too small for decryption ({encryptedData.Length} bytes, minimum 17 required)");

                // Extract IV (first 16 bytes)
                byte[] iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);

                // Extract encrypted data
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
            catch (CryptographicException ex)
            {
                throw new Exception($"Decryption error (invalid key or corrupted data): {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error decrypting file: {ex.Message}", ex);
            }
        }

        public static string DecryptReportFile(string filePath)
        {
            try
            {
                // Decrypt file (same as regular logs)
                string decryptedContent = DecryptLogFile(filePath);
                
                // Check if it's a report
                if (decryptedContent.Contains("=== ОТЧЕТ ОБ ОШИБКАХ") || 
                    decryptedContent.Contains("=== ERROR REPORT") ||
                    decryptedContent.Contains("=== ОШИБКА СОЗДАНИЯ ОТЧЕТА"))
                {
                    return decryptedContent;
                }
                
                // If regular logs, convert to report format
                var entries = ParseLogContent(decryptedContent);
                
                var report = new StringBuilder();
                report.AppendLine("=== AUTO-GENERATED REPORT ===");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"File: {Path.GetFileName(filePath)}");
                report.AppendLine($"Total entries: {entries.Count}");
                report.AppendLine();
                
                report.AppendLine("=== LAST 100 ENTRIES ===");
                foreach (var entry in entries.OrderByDescending(e => e.Timestamp).Take(100))
                {
                    report.AppendLine(entry.RawEntry);
                }
                
                report.AppendLine();
                report.AppendLine("=== DEVELOPER INFO ===");
                report.AppendLine("Encryption: AES-256-CBC");
                report.AppendLine($"Key: {EncryptionKey}");
                report.AppendLine($"File size: {new FileInfo(filePath).Length} bytes");
                
                return report.ToString();
            }
            catch (Exception ex)
            {
                // Return detailed error info for debugging
                var errorInfo = new StringBuilder();
                errorInfo.AppendLine($"Decryption error: {ex.Message}");
                errorInfo.AppendLine();
                errorInfo.AppendLine("=== FILE INFO ===");
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    errorInfo.AppendLine($"File exists: {fileInfo.Exists}");
                    errorInfo.AppendLine($"File size: {fileInfo.Length} bytes");
                    
                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        var bytes = File.ReadAllBytes(filePath);
                        errorInfo.AppendLine($"First 32 bytes (hex): {BitConverter.ToString(bytes, 0, Math.Min(32, bytes.Length)).Replace("-", " ")}");
                        errorInfo.AppendLine($"Key hash (hex): {BitConverter.ToString(KeyHash).Replace("-", " ")}");
                    }
                }
                catch (Exception innerEx)
                {
                    errorInfo.AppendLine($"Error reading file info: {innerEx.Message}");
                }
                
                return errorInfo.ToString();
            }
        }

        public static bool IsEncryptedReportFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;
                    
                byte[] data = File.ReadAllBytes(filePath);
                
                // Minimum size for encrypted file (IV + at least 1 byte of data)
                if (data.Length < 17)
                    return false;
                    
                // Try to decrypt
                string decrypted = DecryptLogFile(filePath);
                
                // Check for report markers
                return decrypted.Contains("=== ОТЧЕТ ОБ ОШИБКАХ") || 
                    decrypted.Contains("=== ERROR REPORT") ||
                    decrypted.Contains("Generated:") ||
                    decrypted.Contains("Сгенерировано:") ||
                    decrypted.Contains("Приложение:");
            }
            catch
            {
                return false;
            }
        }

        public static List<LogEntry> ParseLogContent(string decryptedContent)
        {
            var entries = new List<LogEntry>();

            if (string.IsNullOrWhiteSpace(decryptedContent))
                return entries;

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
                    // Skip invalid lines
                }
            }

            return entries;
        }

        public static LogEntry? ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Skip empty lines and headers
            if (line.StartsWith("===") || line.StartsWith("Generated:") || 
                line.StartsWith("Сгенерировано:") || line.StartsWith("Приложение:") || 
                line.StartsWith("Application:") || line.StartsWith("Версия:") ||
                line.StartsWith("Version:") || line.StartsWith("Устройство:") ||
                line.StartsWith("Device:") || line.StartsWith("Email:") ||
                line.StartsWith("=== AUTO-GENERATED") || line.StartsWith("=== LAST") ||
                line.StartsWith("=== DEVELOPER") || line.StartsWith("=== КОНЕЦ"))
                return null;

            // Format: (Time)_(ActionType)_(Description || JSON)
            // Example: (2024-01-15 10:30:45.123)_(APP_START)_(Application started || {"version": "1.0.0"})
            
            var timeEnd = line.IndexOf(')');
            if (timeEnd == -1) return null;

            var timestampStr = line.Substring(1, timeEnd - 1);
            if (!DateTime.TryParse(timestampStr, out var timestamp))
                return null;

            // Check for ")_(" pattern
            if (timeEnd + 2 >= line.Length || line[timeEnd + 1] != '_')
                return null;

            var remaining = line.Substring(timeEnd + 2); // After ")_"
            
            // Action type is enclosed in parentheses: _(ACTION)_(
            // Find the opening parenthesis for action type
            if (remaining.Length == 0 || remaining[0] != '(')
                return null;
                
            var actionEnd = remaining.IndexOf(')');
            if (actionEnd == -1) return null;

            var actionType = remaining.Substring(1, actionEnd - 1); // Skip the opening '('

            // Check for ")_(" pattern after action type
            string descriptionPart;
            if (actionEnd + 2 <= remaining.Length && remaining[actionEnd + 1] == '_')
            {
                descriptionPart = remaining.Substring(actionEnd + 2);
            }
            else
            {
                descriptionPart = remaining.Substring(actionEnd + 1);
            }

            // Remove opening parenthesis if present
            if (descriptionPart.StartsWith('('))
                descriptionPart = descriptionPart.Substring(1);
                
            // Remove closing bracket if present
            if (descriptionPart.EndsWith(')'))
                descriptionPart = descriptionPart.Substring(0, descriptionPart.Length - 1);

            string description = descriptionPart;
            JObject? extraInfo = null;

            // Try to extract JSON
            var jsonSeparator = descriptionPart.IndexOf(" || ");
            if (jsonSeparator != -1)
            {
                description = descriptionPart.Substring(0, jsonSeparator);
                var jsonPart = descriptionPart.Substring(jsonSeparator + 4);

                // Remove possible spaces and extra characters
                jsonPart = jsonPart.Trim();
                if (jsonPart.EndsWith(')'))
                    jsonPart = jsonPart.Substring(0, jsonPart.Length - 1);

                try
                {
                    extraInfo = JObject.Parse(jsonPart);
                }
                catch
                {
                    // If JSON is invalid, leave as is
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