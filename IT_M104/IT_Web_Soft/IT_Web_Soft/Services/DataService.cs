using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Services
{
    /// <summary>
    /// Service for managing data storage in JSON format
    /// </summary>
    public class DataService
    {
        private static readonly string DataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LogViewer"
        );

        private static readonly string DataFilePath = Path.Combine(DataFolderPath, "log_entries.json");
        private static readonly string SettingsFilePath = Path.Combine(DataFolderPath, "settings.json");

        private int _nextId = 1;

        public DataService()
        {
            EnsureDataFolderExists();
        }

        private void EnsureDataFolderExists()
        {
            if (!Directory.Exists(DataFolderPath))
            {
                Directory.CreateDirectory(DataFolderPath);
            }
        }

        #region Log Entries Operations

        /// <summary>
        /// Load all log entries from storage
        /// </summary>
        public List<LogEntry> LoadEntries()
        {
            try
            {
                if (!File.Exists(DataFilePath))
                {
                    return new List<LogEntry>();
                }

                var json = File.ReadAllText(DataFilePath);
                var entries = JsonSerializer.Deserialize<List<LogEntryDto>>(json);

                if (entries == null || entries.Count == 0)
                {
                    return new List<LogEntry>();
                }

                var result = entries.Select(dto => dto.ToLogEntry()).ToList();
                _nextId = result.Max(e => e.Id) + 1;

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Save all log entries to storage
        /// </summary>
        public void SaveEntries(List<LogEntry> entries)
        {
            try
            {
                EnsureDataFolderExists();

                var dtos = entries.Select(e => LogEntryDto.FromLogEntry(e)).ToList();
                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(DataFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Add a new log entry
        /// </summary>
        public LogEntry AddEntry(LogEntry entry)
        {
            var entries = LoadEntries();
            
            entry.Id = _nextId++;
            entry.DateAdded = DateTime.Now;
            entry.LastModified = DateTime.Now;
            
            entries.Add(entry);
            SaveEntries(entries);
            
            return entry;
        }

        /// <summary>
        /// Update an existing log entry
        /// </summary>
        public void UpdateEntry(LogEntry entry)
        {
            var entries = LoadEntries();
            var existing = entries.FirstOrDefault(e => e.Id == entry.Id);
            
            if (existing != null)
            {
                var index = entries.IndexOf(existing);
                entry.LastModified = DateTime.Now;
                entries[index] = entry;
                SaveEntries(entries);
            }
        }

        /// <summary>
        /// Delete a log entry by ID
        /// </summary>
        public void DeleteEntry(int id)
        {
            var entries = LoadEntries();
            var entry = entries.FirstOrDefault(e => e.Id == id);
            
            if (entry != null)
            {
                entries.Remove(entry);
                SaveEntries(entries);
            }
        }

        /// <summary>
        /// Get entry by ID
        /// </summary>
        public LogEntry? GetEntryById(int id)
        {
            var entries = LoadEntries();
            return entries.FirstOrDefault(e => e.Id == id);
        }

        /// <summary>
        /// Add multiple entries from decrypted log file
        /// </summary>
        public void ImportEntries(List<LogEntry> newEntries, string sourceFile)
        {
            var entries = LoadEntries();
            
            foreach (var entry in newEntries)
            {
                entry.Id = _nextId++;
                entry.DateAdded = DateTime.Now;
                entry.LastModified = DateTime.Now;
                entry.SourceFile = sourceFile;
                entries.Add(entry);
            }
            
            SaveEntries(entries);
        }

        /// <summary>
        /// Clear all entries
        /// </summary>
        public void ClearAllEntries()
        {
            SaveEntries(new List<LogEntry>());
            _nextId = 1;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics about stored entries
        /// </summary>
        public LogStatistics GetStatistics()
        {
            var entries = LoadEntries();
            
            return new LogStatistics
            {
                TotalEntries = entries.Count,
                ErrorCount = entries.Count(e => e.IsError),
                ByCategory = entries.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByStatus = entries.GroupBy(e => e.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                OldestEntry = entries.Any() ? entries.Min(e => e.Timestamp) : DateTime.MinValue,
                NewestEntry = entries.Any() ? entries.Max(e => e.Timestamp) : DateTime.MinValue,
                CriticalCount = entries.Count(e => e.Status == LogStatus.Critical),
                ResolvedCount = entries.Count(e => e.Status == LogStatus.Resolved)
            };
        }

        #endregion

        #region Settings

        /// <summary>
        /// Load application settings
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        /// <summary>
        /// Save application settings
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureDataFolderExists();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving settings: {ex.Message}", ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// DTO for JSON serialization
    /// </summary>
    public class LogEntryDto
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ExtraInfoJson { get; set; }
        public string RawEntry { get; set; } = string.Empty;
        public int Category { get; set; }
        public int Status { get; set; }
        public string UserNotes { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
        public DateTime LastModified { get; set; }
        public string SourceFile { get; set; } = string.Empty;

        public static LogEntryDto FromLogEntry(LogEntry entry)
        {
            return new LogEntryDto
            {
                Id = entry.Id,
                Timestamp = entry.Timestamp,
                ActionType = entry.ActionType,
                Description = entry.Description,
                ExtraInfoJson = entry.ExtraInfo?.ToString(),
                RawEntry = entry.RawEntry,
                Category = (int)entry.Category,
                Status = (int)entry.Status,
                UserNotes = entry.UserNotes,
                DateAdded = entry.DateAdded,
                LastModified = entry.LastModified,
                SourceFile = entry.SourceFile
            };
        }

        public LogEntry ToLogEntry()
        {
            return new LogEntry
            {
                Id = this.Id,
                Timestamp = this.Timestamp,
                ActionType = this.ActionType,
                Description = this.Description,
                ExtraInfo = !string.IsNullOrEmpty(ExtraInfoJson) 
                    ? Newtonsoft.Json.Linq.JObject.Parse(ExtraInfoJson) 
                    : null,
                RawEntry = this.RawEntry,
                Category = (LogCategory)this.Category,
                Status = (LogStatus)this.Status,
                UserNotes = this.UserNotes,
                DateAdded = this.DateAdded,
                LastModified = this.LastModified,
                SourceFile = this.SourceFile
            };
        }
    }

    /// <summary>
    /// Statistics model
    /// </summary>
    public class LogStatistics
    {
        public int TotalEntries { get; set; }
        public int ErrorCount { get; set; }
        public int CriticalCount { get; set; }
        public int ResolvedCount { get; set; }
        public DateTime OldestEntry { get; set; }
        public DateTime NewestEntry { get; set; }
        public Dictionary<LogCategory, int> ByCategory { get; set; } = new();
        public Dictionary<LogStatus, int> ByStatus { get; set; } = new();
    }

    /// <summary>
    /// Application settings model
    /// </summary>
    public class AppSettings
    {
        public string LastOpenedFile { get; set; } = string.Empty;
        public bool AutoLoadLastFile { get; set; } = true;
        public bool ShowOnlyErrors { get; set; } = false;
        public string DefaultSortColumn { get; set; } = "Timestamp";
        public bool SortDescending { get; set; } = true;
    }
}