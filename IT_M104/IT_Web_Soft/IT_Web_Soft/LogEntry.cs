using System;
using Newtonsoft.Json.Linq;

namespace LogViewer.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JObject? ExtraInfo { get; set; }
        public string RawEntry { get; set; } = string.Empty;
        public bool IsError => ActionType.StartsWith("ERROR_");

        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} [{ActionType}] {Description}";
        }
    }
}