using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace LogViewer.Models
{
    /// <summary>
    /// Main entity representing a log entry with full tracking capabilities
    /// </summary>
    public class LogEntry : INotifyPropertyChanged, ICloneable
    {
        private LogStatus _status = LogStatus.New;
        private LogCategory _category = LogCategory.Other;
        private string _userNotes = string.Empty;
        private int _id;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp { get; set; }
        
        public string ActionType { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public JObject? ExtraInfo { get; set; }
        
        public string RawEntry { get; set; } = string.Empty;
        
        public bool IsError => ActionType.StartsWith("ERROR_");

        /// <summary>
        /// Category for classification
        /// </summary>
        public LogCategory Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Status for tracking
        /// </summary>
        public LogStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// User notes for the entry
        /// </summary>
        public string UserNotes
        {
            get => _userNotes;
            set { _userNotes = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Date when entry was added to local storage
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.Now;

        /// <summary>
        /// Date when entry was last modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Source file path
        /// </summary>
        public string SourceFile { get; set; } = string.Empty;

        /// <summary>
        /// Display name for category
        /// </summary>
        public string CategoryDisplayName => LogCategoryHelper.GetDisplayName(Category);

        /// <summary>
        /// Display name for status
        /// </summary>
        public string StatusDisplayName => LogStatusHelper.GetDisplayName(Status);

        /// <summary>
        /// Color code for status
        /// </summary>
        public string StatusColor => LogStatusHelper.GetColorCode(Status);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} [{ActionType}] {Description}";
        }

        public object Clone()
        {
            return new LogEntry
            {
                Id = this.Id,
                Timestamp = this.Timestamp,
                ActionType = this.ActionType,
                Description = this.Description,
                ExtraInfo = this.ExtraInfo?.DeepClone() as JObject,
                RawEntry = this.RawEntry,
                Category = this.Category,
                Status = this.Status,
                UserNotes = this.UserNotes,
                DateAdded = this.DateAdded,
                LastModified = this.LastModified,
                SourceFile = this.SourceFile
            };
        }
    }
}