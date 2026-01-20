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
using Newtonsoft.Json.Linq;

namespace LogViewer
{
    public partial class MainWindow : Window
    {
        private List<LogEntry> _allEntries = new();
        private string? _currentFilePath;

        public MainWindow()
        {
            InitializeComponent();
            UpdateStatus("Готов к работе");
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Encrypted log files (*.enc)|*.enc|All files (*.*)|*.*",
                Title = "Выберите файл логов"
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
                UpdateStatus($"Дешифровка файла...");

                string decryptedContent;

                // Определяем тип файла
                if (LogDecryptor.IsEncryptedReportFile(filePath))
                {
                    // Это отчет
                    decryptedContent = LogDecryptor.DecryptReportFile(filePath);
                    TxtFileInfo.Text = $"Отчет: {Path.GetFileName(filePath)} (дешифрован)";

                    // Извлекаем записи из отчета
                    _allEntries = ExtractEntriesFromReport(decryptedContent);
                    TxtFileType.Text = "Тип: Отчет";
                }
                else
                {
                    // Это обычные логи
                    decryptedContent = LogDecryptor.DecryptLogFile(filePath);
                    _allEntries = LogDecryptor.ParseLogContent(decryptedContent);
                    TxtFileInfo.Text = $"Файл: {Path.GetFileName(filePath)} ({new FileInfo(filePath).Length} байт)";
                    TxtFileType.Text = "Тип: Логи";
                }

                _currentFilePath = filePath;

                // Для отчетов показываем содержимое напрямую
                if (_allEntries.Count == 0 && decryptedContent.Contains("=== ОТЧЕТ ОБ ОШИБКАХ"))
                {
                    TxtDetailHeader.Text = $"Отчет: {Path.GetFileName(filePath)}";
                    TxtDetailContent.Text = decryptedContent;
                    TxtEntryCount.Text = $"Отчет ({new FileInfo(filePath).Length} байт)";
                }
                else
                {
                    ApplyFilters();
                }

                UpdateStatus($"Файл загружен: {_allEntries.Count} записей");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Ошибка: {ex.Message}");
            }
        }

        // Новый метод для извлечения записей из отчета
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

                // Начинаем сбор записей после заголовка
                if (trimmedLine.StartsWith("=== ПОСЛЕДНИЕ"))
                {
                    inEntriesSection = true;
                    continue;
                }

                // Заканчиваем сбор записей при следующем заголовке
                if (inEntriesSection && trimmedLine.StartsWith("==="))
                {
                    inEntriesSection = false;
                    continue;
                }

                // Парсим записи в формате логов
                if (inEntriesSection && !string.IsNullOrEmpty(trimmedLine))
                {
                    var entry = LogDecryptor.ParseLogLine(trimmedLine);
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            return entries;
        }

        private void ApplyFilters()
        {
            var filtered = _allEntries.AsEnumerable();

            // Фильтр по ошибкам
            if (ChkErrorsOnly.IsChecked == true)
            {
                filtered = filtered.Where(e => e.IsError);
            }

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var searchText = TxtSearch.Text.ToLower();
                filtered = filtered.Where(e =>
                    e.ActionType.ToLower().Contains(searchText) ||
                    e.Description.ToLower().Contains(searchText) ||
                    e.RawEntry.ToLower().Contains(searchText));
            }

            // Сортировка по времени (новые сверху)
            filtered = filtered.OrderByDescending(e => e.Timestamp);

            LvLogEntries.ItemsSource = filtered.ToList();
            TxtEntryCount.Text = $"Записей: {filtered.Count()} из {_allEntries.Count}";
        }

        private void LvLogEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvLogEntries.SelectedItem is LogEntry selectedEntry)
            {
                TxtDetailHeader.Text = $"{selectedEntry.Timestamp:yyyy-MM-dd HH:mm:ss} [{selectedEntry.ActionType}]";

                var detail = new StringBuilder();
                detail.AppendLine($"ВРЕМЯ: {selectedEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                detail.AppendLine($"ТИП ДЕЙСТВИЯ: {selectedEntry.ActionType}");
                detail.AppendLine($"ОПИСАНИЕ: {selectedEntry.Description}");
                detail.AppendLine();

                if (selectedEntry.ExtraInfo != null)
                {
                    detail.AppendLine("ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ:");
                    detail.AppendLine(selectedEntry.ExtraInfo.ToString(Newtonsoft.Json.Formatting.Indented));
                }

                detail.AppendLine();
                detail.AppendLine("ИСХОДНАЯ ЗАПИСЬ:");
                detail.AppendLine(selectedEntry.RawEntry);

                TxtDetailContent.Text = detail.ToString();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkErrorsOnly_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkErrorsOnly_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                LoadLogFile(_currentFilePath);
            }
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            ChkErrorsOnly.IsChecked = false;
            ApplyFilters();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_allEntries.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"log_export_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "Экспорт логов"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var exportContent = new StringBuilder();
                    exportContent.AppendLine("=== ЭКСПОРТ ЛОГОВ IT TOP COLLEGE JOURNAL ===");
                    exportContent.AppendLine($"Сгенерировано: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    exportContent.AppendLine($"Файл источника: {_currentFilePath ?? "Неизвестно"}");
                    exportContent.AppendLine($"Всего записей: {_allEntries.Count}");
                    exportContent.AppendLine();

                    foreach (var entry in _allEntries.OrderByDescending(e => e.Timestamp))
                    {
                        exportContent.AppendLine(entry.RawEntry);
                    }

                    File.WriteAllText(saveFileDialog.FileName, exportContent.ToString(), Encoding.UTF8);

                    MessageBox.Show($"Экспорт завершен: {saveFileDialog.FileName}",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus($"Файл экспортирован: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта:\n{ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatus(string message)
        {
            TxtStatus.Text = message;
            if (message.StartsWith("Ошибка"))
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.Black;
            }
        }
    }
}