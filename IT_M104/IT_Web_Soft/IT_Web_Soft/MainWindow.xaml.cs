using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LogViewer.Models;
using LogViewer.Services;
using LogViewer.Windows;
using Newtonsoft.Json.Linq;

namespace LogViewer
{
    /// <summary>
    /// Main window for Log Viewer application
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<LogEntry> _allEntries = new();
        private List<LogEntry> _filteredEntries = new();
        private string? _currentFilePath;
        private readonly DataService _dataService;
        private LogEntry? _selectedEntry;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            _dataService = new DataService();
            _isInitialized = true;
            
            LoadSavedData();
            UpdateStatus("Ready to work");
        }

        #region Data Loading

        private void LoadSavedData()
        {
            try
            {
                // Load saved entries
                _allEntries = _dataService.LoadEntries();
                
                // Load settings
                var settings = _dataService.LoadSettings();
                if (settings.AutoLoadLastFile && !string.IsNullOrEmpty(settings.LastOpenedFile))
                {
                    if (File.Exists(settings.LastOpenedFile))
                    {
                        LoadLogFile(settings.LastOpenedFile);
                        return;
                    }
                }
                
                ApplyFilters();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading data: {ex.Message}");
            }
        }

        #endregion

        #region File Operations

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Encrypted log files (*.enc)|*.enc|All files (*.*)|*.*",
                Title = "Select log file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadLogFile(openFileDialog.FileName);
            }
        }

        private void LoadLogFile(string filePath)
        {
            try
            {
                UpdateStatus("Decrypting file...");

                string decryptedContent;

                // Determine file type
                if (LogDecryptor.IsEncryptedReportFile(filePath))
                {
                    // This is a report
                    decryptedContent = LogDecryptor.DecryptReportFile(filePath);
                    TxtFileInfo.Text = $"Report: {Path.GetFileName(filePath)} (decrypted)";

                    // Extract entries from report
                    var reportEntries = ExtractEntriesFromReport(decryptedContent);
                    _allEntries.AddRange(reportEntries);
                    TxtFileType.Text = "Type: Report";
                }
                else
                {
                    // Regular logs
                    decryptedContent = LogDecryptor.DecryptLogFile(filePath);
                    var newEntries = LogDecryptor.ParseLogContent(decryptedContent);
                    
                    // Import to storage
                    _dataService.ImportEntries(newEntries, filePath);
                    _allEntries = _dataService.LoadEntries();
                    
                    TxtFileInfo.Text = $"File: {Path.GetFileName(filePath)} ({new FileInfo(filePath).Length} bytes)";
                    TxtFileType.Text = "Type: Logs";
                }

                _currentFilePath = filePath;

                // Save settings
                var settings = _dataService.LoadSettings();
                settings.LastOpenedFile = filePath;
                _dataService.SaveSettings(settings);

                // For reports show content directly
                if (_allEntries.Count == 0 && decryptedContent.Contains("=== ERROR REPORT"))
                {
                    TxtDetailHeader.Text = $"Report: {Path.GetFileName(filePath)}";
                    TxtDetailContent.Text = decryptedContent;
                    TxtEntryCount.Text = $"Report ({new FileInfo(filePath).Length} bytes)";
                }
                else
                {
                    ApplyFilters();
                }

                UpdateStatus($"File loaded: {_allEntries.Count} entries");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private List<LogEntry> ExtractEntriesFromReport(string reportContent)
        {
            var entries = new List<LogEntry>();

            if (string.IsNullOrEmpty(reportContent))
                return entries;

            var lines = reportContent.Split('\n');
            bool inEntriesSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Start collecting entries after header
                if (trimmedLine.StartsWith("=== LAST"))
                {
                    inEntriesSection = true;
                    continue;
                }

                // Stop collecting at next header
                if (inEntriesSection && trimmedLine.StartsWith("==="))
                {
                    inEntriesSection = false;
                    continue;
                }

                // Parse entries in log format
                if (inEntriesSection && !string.IsNullOrEmpty(trimmedLine))
                {
                    var entry = LogDecryptor.ParseLogLine(trimmedLine);
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            return entries;
        }

        #endregion

        #region Filtering and Sorting

        private void ApplyFilters()
        {
            // Skip if controls are not initialized yet
            if (!_isInitialized) return;
            
            var filtered = _allEntries.AsEnumerable();

            // Filter by category
            if (CbCategoryFilter.SelectedIndex > 0)
            {
                var selectedCategory = (LogCategory)(CbCategoryFilter.SelectedIndex - 1);
                filtered = filtered.Where(e => e.Category == selectedCategory);
            }

            // Filter by status
            if (CbStatusFilter.SelectedIndex > 0)
            {
                var selectedStatus = (LogStatus)(CbStatusFilter.SelectedIndex - 1);
                filtered = filtered.Where(e => e.Status == selectedStatus);
            }

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var searchText = TxtSearch.Text.ToLower();
                filtered = filtered.Where(e =>
                    e.ActionType.ToLower().Contains(searchText) ||
                    e.Description.ToLower().Contains(searchText) ||
                    e.RawEntry.ToLower().Contains(searchText) ||
                    e.UserNotes.ToLower().Contains(searchText));
            }

            // Sort by time (newest first)
            filtered = filtered.OrderByDescending(e => e.Timestamp);

            _filteredEntries = filtered.ToList();
            LvLogEntries.ItemsSource = _filteredEntries;

            // Update counts
            TxtEntryCount.Text = $"Entries: {_filteredEntries.Count} of {_allEntries.Count}";
            TxtErrorCount.Text = $"Errors: {_filteredEntries.Count(e => e.IsError)}";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            CbCategoryFilter.SelectedIndex = 0;
            CbStatusFilter.SelectedIndex = 0;
            ApplyFilters();
        }

        #endregion

        #region Entry Selection

        private void LvLogEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEntry = LvLogEntries.SelectedItem as LogEntry;
            
            if (_selectedEntry != null)
            {
                TxtDetailHeader.Text = $"{_selectedEntry.Timestamp:yyyy-MM-dd HH:mm:ss} [{_selectedEntry.ActionType}]";

                var detail = new StringBuilder();
                detail.AppendLine($"DATE: {_selectedEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                detail.AppendLine($"TYPE: {_selectedEntry.ActionType}");
                detail.AppendLine($"CATEGORY: {_selectedEntry.CategoryDisplayName}");
                detail.AppendLine($"STATUS: {_selectedEntry.StatusDisplayName}");
                detail.AppendLine($"DESCRIPTION: {_selectedEntry.Description}");
                detail.AppendLine();

                if (!string.IsNullOrEmpty(_selectedEntry.UserNotes))
                {
                    detail.AppendLine("USER NOTES:");
                    detail.AppendLine(_selectedEntry.UserNotes);
                    detail.AppendLine();
                }

                if (_selectedEntry.ExtraInfo != null)
                {
                    detail.AppendLine("ADDITIONAL INFO:");
                    detail.AppendLine(_selectedEntry.ExtraInfo.ToString(Newtonsoft.Json.Formatting.Indented));
                    detail.AppendLine();
                }

                detail.AppendLine("ORIGINAL ENTRY:");
                detail.AppendLine(_selectedEntry.RawEntry);

                TxtDetailContent.Text = detail.ToString();
            }
        }

        #endregion

        #region CRUD Operations

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new LogEntryEditorWindow();
            editorWindow.Owner = this;

            if (editorWindow.ShowDialog() == true && editorWindow.Result != null)
            {
                var newEntry = _dataService.AddEntry(editorWindow.Result);
                _allEntries.Add(newEntry);
                ApplyFilters();
                UpdateStatus($"Entry added: ID {newEntry.Id}");
            }
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null)
            {
                MessageBox.Show("Select an entry to edit", 
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editorWindow = new LogEntryEditorWindow(_selectedEntry);
            editorWindow.Owner = this;

            if (editorWindow.ShowDialog() == true && editorWindow.Result != null)
            {
                _dataService.UpdateEntry(editorWindow.Result);
                
                // Update in local list
                var index = _allEntries.FindIndex(en => en.Id == editorWindow.Result.Id);
                if (index >= 0)
                {
                    _allEntries[index] = editorWindow.Result;
                }
                
                ApplyFilters();
                UpdateStatus($"Entry updated: ID {editorWindow.Result.Id}");
            }
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null)
            {
                MessageBox.Show("Select an entry to delete", 
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete this entry?\n\n{_selectedEntry.Description}",
                "Confirm deletion", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dataService.DeleteEntry(_selectedEntry.Id);
                _allEntries.RemoveAll(en => en.Id == _selectedEntry.Id);
                ApplyFilters();
                UpdateStatus($"Entry deleted: ID {_selectedEntry.Id}");
                _selectedEntry = null;
            }
        }

        private void BtnSetStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null)
            {
                MessageBox.Show("Select an entry first", 
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sender is Button button && button.Tag is LogStatus newStatus)
            {
                _selectedEntry.Status = newStatus;
                _selectedEntry.LastModified = DateTime.Now;
                _dataService.UpdateEntry(_selectedEntry);
                ApplyFilters();
                UpdateStatus($"Status changed to: {LogStatusHelper.GetDisplayName(newStatus)}");
            }
        }

        #endregion

        #region Export and Statistics

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                LoadLogFile(_currentFilePath);
            }
            else
            {
                _allEntries = _dataService.LoadEntries();
                ApplyFilters();
                UpdateStatus("List updated");
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredEntries.Count == 0)
            {
                MessageBox.Show("No data to export", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"log_export_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "Export logs"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var exportContent = new StringBuilder();
                    exportContent.AppendLine("=== LOG EXPORT - LOG VIEWER ===");
                    exportContent.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    exportContent.AppendLine($"Source file: {_currentFilePath ?? "Unknown"}");
                    exportContent.AppendLine($"Total entries: {_filteredEntries.Count}");
                    exportContent.AppendLine();

                    foreach (var entry in _filteredEntries)
                    {
                        exportContent.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.ActionType}] [{entry.StatusDisplayName}]");
                        exportContent.AppendLine($"  {entry.Description}");
                        if (!string.IsNullOrEmpty(entry.UserNotes))
                        {
                            exportContent.AppendLine($"  Notes: {entry.UserNotes}");
                        }
                        exportContent.AppendLine();
                    }

                    File.WriteAllText(saveFileDialog.FileName, exportContent.ToString(), Encoding.UTF8);

                    MessageBox.Show($"Export completed: {saveFileDialog.FileName}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus($"File exported: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnStatistics_Click(object sender, RoutedEventArgs e)
        {
            var statsWindow = new StatisticsWindow(_allEntries);
            statsWindow.Owner = this;
            statsWindow.ShowDialog();
        }

        #endregion

        #region Status Updates

        private void UpdateStatus(string message)
        {
            TxtStatus.Text = message;
            if (message.StartsWith("Error"))
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        #endregion
    }
}