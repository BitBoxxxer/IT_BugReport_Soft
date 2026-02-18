using System;
using System.Windows;
using LogViewer.Models;
using Newtonsoft.Json.Linq;

namespace LogViewer.Windows
{
    /// <summary>
    /// Window for adding and editing log entries with validation
    /// </summary>
    public partial class LogEntryEditorWindow : Window
    {
        private readonly LogEntry _entry;
        private readonly bool _isEditMode;

        public LogEntry? Result { get; private set; }

        public LogEntryEditorWindow(LogEntry? entry = null)
        {
            InitializeComponent();

            _isEditMode = entry != null;
            _entry = entry?.Clone() as LogEntry ?? new LogEntry
            {
                Timestamp = DateTime.Now,
                Category = LogCategory.Other,
                Status = LogStatus.New
            };

            InitializeComboBoxes();
            LoadEntryData();
            UpdateHeader();
        }

        private void InitializeComboBoxes()
        {
            // Initialize Category ComboBox
            foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
            {
                CbCategory.Items.Add(LogCategoryHelper.GetDisplayName(category));
            }

            // Initialize Status ComboBox
            foreach (LogStatus status in Enum.GetValues(typeof(LogStatus)))
            {
                CbStatus.Items.Add(LogStatusHelper.GetDisplayName(status));
            }
        }

        private void LoadEntryData()
        {
            // Date and Time
            DpDate.SelectedDate = _entry.Timestamp.Date;
            TxtTime.Text = _entry.Timestamp.ToString("HH:mm:ss");

            // Action Type
            TxtActionType.Text = _entry.ActionType;

            // Description
            TxtDescription.Text = _entry.Description;

            // Category
            CbCategory.SelectedIndex = (int)_entry.Category;

            // Status
            CbStatus.SelectedIndex = (int)_entry.Status;

            // User Notes
            TxtUserNotes.Text = _entry.UserNotes;

            // Extra Info
            if (_entry.ExtraInfo != null)
            {
                TxtExtraInfo.Text = _entry.ExtraInfo.ToString(Newtonsoft.Json.Formatting.Indented);
            }
        }

        private void UpdateHeader()
        {
            TxtHeader.Text = _isEditMode ? "Edit Log Entry" : "New Log Entry";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous errors
            ClearErrors();

            // Validate
            bool isValid = ValidateInput();

            if (!isValid)
            {
                return;
            }

            // Create result
            Result = new LogEntry
            {
                Id = _entry.Id,
                Timestamp = GetDateTimeFromInputs(),
                ActionType = TxtActionType.Text.Trim(),
                Description = TxtDescription.Text.Trim(),
                Category = (LogCategory)CbCategory.SelectedIndex,
                Status = (LogStatus)CbStatus.SelectedIndex,
                UserNotes = TxtUserNotes.Text.Trim(),
                ExtraInfo = ParseExtraInfo(),
                RawEntry = _entry.RawEntry,
                SourceFile = _entry.SourceFile,
                DateAdded = _entry.DateAdded,
                LastModified = DateTime.Now
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private DateTime GetDateTimeFromInputs()
        {
            var date = DpDate.SelectedDate ?? DateTime.Today;
            var timeStr = TxtTime.Text.Trim();

            if (TimeSpan.TryParse(timeStr, out var time))
            {
                return date.Date + time;
            }

            return date;
        }

        private JObject? ParseExtraInfo()
        {
            var extraInfoText = TxtExtraInfo.Text.Trim();
            if (string.IsNullOrEmpty(extraInfoText))
            {
                return null;
            }

            try
            {
                return JObject.Parse(extraInfoText);
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateInput()
        {
            bool isValid = true;

            // Validate Time
            if (!TimeSpan.TryParse(TxtTime.Text.Trim(), out _))
            {
                TxtTimeError.Visibility = Visibility.Visible;
                isValid = false;
            }

            // Validate Action Type
            if (string.IsNullOrWhiteSpace(TxtActionType.Text))
            {
                TxtActionTypeError.Visibility = Visibility.Visible;
                isValid = false;
            }

            // Validate Description
            if (string.IsNullOrWhiteSpace(TxtDescription.Text))
            {
                TxtDescriptionError.Visibility = Visibility.Visible;
                isValid = false;
            }

            // Validate Extra Info JSON (if provided)
            var extraInfoText = TxtExtraInfo.Text.Trim();
            if (!string.IsNullOrEmpty(extraInfoText))
            {
                try
                {
                    JObject.Parse(extraInfoText);
                }
                catch
                {
                    TxtExtraInfoError.Visibility = Visibility.Visible;
                    isValid = false;
                }
            }

            return isValid;
        }

        private void ClearErrors()
        {
            TxtTimeError.Visibility = Visibility.Collapsed;
            TxtActionTypeError.Visibility = Visibility.Collapsed;
            TxtDescriptionError.Visibility = Visibility.Collapsed;
            TxtExtraInfoError.Visibility = Visibility.Collapsed;
        }
    }
}