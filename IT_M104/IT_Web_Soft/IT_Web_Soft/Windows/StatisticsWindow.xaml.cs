using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using LogViewer.Models;
using LogViewer.Services;
using Microsoft.Win32;

namespace LogViewer.Windows
{
    /// <summary>
    /// Window for displaying statistics and generating reports
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private readonly DataService _dataService;
        private readonly List<LogEntry> _entries;
        private LogStatistics _statistics = new();

        public StatisticsWindow(List<LogEntry> entries)
        {
            InitializeComponent();
            _dataService = new DataService();
            _entries = entries ?? new List<LogEntry>();
            
            LoadStatistics();
            GenerateReportPreview();
        }

        private void LoadStatistics()
        {
            _statistics = _dataService.GetStatistics();

            // Override with current entries if provided
            if (_entries.Any())
            {
                _statistics.TotalEntries = _entries.Count;
                _statistics.ErrorCount = _entries.Count(e => e.IsError);
                _statistics.CriticalCount = _entries.Count(e => e.Status == LogStatus.Critical);
                _statistics.ResolvedCount = _entries.Count(e => e.Status == LogStatus.Resolved);
                _statistics.OldestEntry = _entries.Min(e => e.Timestamp);
                _statistics.NewestEntry = _entries.Max(e => e.Timestamp);
                _statistics.ByCategory = _entries.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count());
                _statistics.ByStatus = _entries.GroupBy(e => e.Status)
                    .ToDictionary(g => g.Key, g => g.Count());
            }

            // Update UI
            TxtTotalEntries.Text = _statistics.TotalEntries.ToString();
            TxtErrorCount.Text = _statistics.ErrorCount.ToString();
            TxtCriticalCount.Text = _statistics.CriticalCount.ToString();
            TxtResolvedCount.Text = _statistics.ResolvedCount.ToString();

            if (_statistics.OldestEntry != DateTime.MinValue)
            {
                TxtOldestEntry.Text = _statistics.OldestEntry.ToString("yyyy-MM-dd HH:mm:ss");
            }

            if (_statistics.NewestEntry != DateTime.MinValue)
            {
                TxtNewestEntry.Text = _statistics.NewestEntry.ToString("yyyy-MM-dd HH:mm:ss");
            }

            // Load category breakdown
            var categoryData = _statistics.ByCategory
                .Select(kv => new CategoryStatItem
                {
                    Category = LogCategoryHelper.GetDisplayName(kv.Key),
                    Count = kv.Value
                })
                .OrderByDescending(c => c.Count)
                .ToList();

            LvByCategory.ItemsSource = categoryData;

            // Load status breakdown
            var statusData = _statistics.ByStatus
                .Select(kv => new StatusStatItem
                {
                    Status = LogStatusHelper.GetDisplayName(kv.Key),
                    Color = (Color)ColorConverter.ConvertFromString(LogStatusHelper.GetColorCode(kv.Key)),
                    Count = kv.Value
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            LvByStatus.ItemsSource = statusData;
        }

        private void GenerateReportPreview()
        {
            var report = GenerateReport();
            TxtReportPreview.Text = report;
        }

        private string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║          LOG REPORT - LOG VIEWER                         ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("=== GENERAL SUMMARY ===");
            sb.AppendLine($"Total entries: {_statistics.TotalEntries}");
            sb.AppendLine($"Errors: {_statistics.ErrorCount}");
            sb.AppendLine($"Critical: {_statistics.CriticalCount}");
            sb.AppendLine($"Resolved: {_statistics.ResolvedCount}");
            sb.AppendLine();

            if (_statistics.OldestEntry != DateTime.MinValue)
            {
                sb.AppendLine($"Date range: {_statistics.OldestEntry:yyyy-MM-dd} - {_statistics.NewestEntry:yyyy-MM-dd}");
                sb.AppendLine();
            }

            sb.AppendLine("=== BY CATEGORY ===");
            foreach (var kv in _statistics.ByCategory.OrderByDescending(c => c.Value))
            {
                sb.AppendLine($"  {LogCategoryHelper.GetDisplayName(kv.Key)}: {kv.Value}");
            }
            sb.AppendLine();

            sb.AppendLine("=== BY STATUS ===");
            foreach (var kv in _statistics.ByStatus.OrderByDescending(s => s.Value))
            {
                sb.AppendLine($"  {LogStatusHelper.GetDisplayName(kv.Key)}: {kv.Value}");
            }
            sb.AppendLine();

            if (_entries.Any())
            {
                sb.AppendLine("=== CRITICAL ENTRIES ===");
                var criticalEntries = _entries.Where(e => e.Status == LogStatus.Critical).Take(10);
                foreach (var entry in criticalEntries)
                {
                    sb.AppendLine($"  [{entry.Timestamp:HH:mm:ss}] {entry.ActionType}: {entry.Description}");
                }
                sb.AppendLine();

                sb.AppendLine("=== RECENT ERRORS ===");
                var recentErrors = _entries.Where(e => e.IsError).OrderByDescending(e => e.Timestamp).Take(10);
                foreach (var entry in recentErrors)
                {
                    sb.AppendLine($"  [{entry.Timestamp:HH:mm:ss}] {entry.ActionType}: {entry.Description}");
                }
            }

            return sb.ToString();
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"log_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "Export Report"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var report = GenerateReport();
                    File.WriteAllText(saveDialog.FileName, report, Encoding.UTF8);

                    MessageBox.Show($"Report exported successfully:\n{saveDialog.FileName}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting report:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Helper class for category statistics display
    /// </summary>
    public class CategoryStatItem
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Helper class for status statistics display
    /// </summary>
    public class StatusStatItem
    {
        public string Status { get; set; } = string.Empty;
        public Color Color { get; set; }
        public int Count { get; set; }
    }
}