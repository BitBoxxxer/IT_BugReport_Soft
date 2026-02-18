using System;

namespace LogViewer.Models
{
    /// <summary>
    /// Статусная модель для отслеживания записей логов
    /// </summary>
    public enum LogStatus
    {
        New,            // Новая запись, не просмотрена
        InProgress,     // На рассмотрении
        Resolved,       // Проблема решена
        Ignored,        // Помечена как игнорируемая
        Critical,       // Критический приоритет
        Archived        // Архивированная запись
    }

    /// <summary>
    /// Вспомогательный класс для работы со статусами
    /// </summary>
    public static class LogStatusHelper
    {
        public static string GetDisplayName(LogStatus status)
        {
            return status switch
            {
                LogStatus.New => "Новый",
                LogStatus.InProgress => "В работе",
                LogStatus.Resolved => "Решён",
                LogStatus.Ignored => "Игнор",
                LogStatus.Critical => "Критический",
                LogStatus.Archived => "Архив",
                _ => "Неизвестно"
            };
        }

        public static string GetColorCode(LogStatus status)
        {
            return status switch
            {
                LogStatus.New => "#2196F3",        // Синий
                LogStatus.InProgress => "#FF9800", // Оранжевый
                LogStatus.Resolved => "#4CAF50",   // Зелёный
                LogStatus.Ignored => "#9E9E9E",    // Серый
                LogStatus.Critical => "#F44336",   // Красный
                LogStatus.Archived => "#795548",   // Коричневый
                _ => "#FFFFFF"
            };
        }

        public static bool IsActive(LogStatus status)
        {
            return status == LogStatus.New || 
                   status == LogStatus.InProgress || 
                   status == LogStatus.Critical;
        }
    }
}