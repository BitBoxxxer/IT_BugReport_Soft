using System;

namespace LogViewer.Models
{
    /// <summary>
    /// Категории для классификации записей логов
    /// </summary>
    public enum LogCategory
    {
        System,         // Системные операции
        User,           // Действия пользователя
        Network,        // Сетевые операции
        Database,       // Операции с базой данных
        Security,       // События безопасности
        Application,    // Приложение
        Performance,    // Производительность
        Other           // Другое
    }

    /// <summary>
    /// Вспомогательный класс для работы с категориями
    /// </summary>
    public static class LogCategoryHelper
    {
        public static string GetDisplayName(LogCategory category)
        {
            return category switch
            {
                LogCategory.System => "Система",
                LogCategory.User => "Пользователь",
                LogCategory.Network => "Сеть",
                LogCategory.Database => "База данных",
                LogCategory.Security => "Безопасность",
                LogCategory.Application => "Приложение",
                LogCategory.Performance => "Производительность",
                LogCategory.Other => "Другое",
                _ => "Неизвестно"
            };
        }

        public static string GetIcon(LogCategory category)
        {
            return category switch
            {
                LogCategory.System => "⚙️",
                LogCategory.User => "👤",
                LogCategory.Network => "🌐",
                LogCategory.Database => "💾",
                LogCategory.Security => "🔒",
                LogCategory.Application => "📱",
                LogCategory.Performance => "⚡",
                LogCategory.Other => "📁",
                _ => "❓"
            };
        }
    }
}