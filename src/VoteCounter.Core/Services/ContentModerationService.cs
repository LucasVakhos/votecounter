namespace VoteCounter.Core.Services;

/// <summary>
/// Результат проверки содержимого работы на запрещённый контент
/// </summary>
public sealed class ContentModerationResult
{
    /// <summary>
    /// Содержит ли работа потенциально запрещённый контент
    /// </summary>
    public bool HasProblems { get; set; }

    /// <summary>
    /// Список найденных проблем
    /// </summary>
    public List<ContentProblem> Problems { get; } = new();

    /// <summary>
    /// Общий риск содержимого (Low, Medium, High)
    /// </summary>
    public ContentRiskLevel RiskLevel => Problems.Count switch
    {
        0 => ContentRiskLevel.Low,
        1 or 2 => ContentRiskLevel.Medium,
        _ => ContentRiskLevel.High
    };

    /// <summary>
    /// Рекомендация модератору
    /// </summary>
    public string Recommendation => RiskLevel switch
    {
        ContentRiskLevel.Low => "✅ Содержимое чистое, можно принимать",
        ContentRiskLevel.Medium => "⚠️ Требуется проверка, возможны проблемы",
        ContentRiskLevel.High => "❌ Много проблем, рекомендуется отклонить"
    };
}

/// <summary>
/// Найденная проблема в содержимом
/// </summary>
public sealed class ContentProblem
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FoundText { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

/// <summary>
/// Уровень риска содержимого
/// </summary>
public enum ContentRiskLevel
{
    /// <summary>Содержимое чистое</summary>
    Low = 0,
    
    /// <summary>Есть подозрительные элементы</summary>
    Medium = 1,
    
    /// <summary>Явные проблемы</summary>
    High = 2
}

/// <summary>
/// Сервис автоматической проверки содержимого работ на запрещённый контент
/// </summary>
public sealed class ContentModerationService
{
    /// <summary>
    /// Запрещённые слова и фразы (включают русские и латинские буквы)
    /// </summary>
    private static readonly HashSet<string> ForbiddenWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Оскорбительный контент - примеры
        "дебил", "идиот", "олух", "мудак", "козел", "урод",
        "мусор", "отброс", "гавно", "говно", "хрень",
        
        // Экстремистский контент - примеры
        "фашист", "нацист", "экстремист", "террор",
        
        // Насилие - примеры
        "убить", "линч", "казнь", "расстрел",
        
        // Дискриминация - примеры
        "педик", "геи", "гей", "лесбиянк", "чёрный", "негр",
        "москаль", "бандер", "укроп", "хохол",
        
        // Реклама и спам
        "купи", "закажи", "кликни", "переходи",
        
        // NSFW контент
        "порно", "секс", "xxx", "18+",
        
        // Политический экстремизм - примеры
        "свастик", "reich", "путин", "зеленск", "байден"
    };

    /// <summary>
    /// Проверить содержимое работы на запрещённый контент
    /// </summary>
    public ContentModerationResult CheckContent(string title, string subtitle, string content)
    {
        var result = new ContentModerationResult();

        // Объединяем весь текст для проверки
        var fullText = $"{title} {subtitle} {content}";
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Проверяем на запрещённые слова
        CheckForbiddenWords(fullText, lines, result);

        // Проверяем на автоматическую капсировку (ВЕСЬ ТЕКСТ ЗАГЛАВНЫМИ)
        CheckAllCaps(content, result);

        // Проверяем на спам (повторения)
        CheckSpam(content, result);

        // Проверяем на экстремизм (символы, паттерны)
        CheckExtremism(content, result);

        result.HasProblems = result.Problems.Count > 0;

        return result;
    }

    private static void CheckForbiddenWords(string fullText, string[] lines, ContentModerationResult result)
    {
        var lowerText = fullText.ToLower();

        foreach (var forbidden in ForbiddenWords)
        {
            if (!lowerText.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                continue;

            // Найти строку с проблемой
            int lineNo = 0;
            string foundIn = string.Empty;
            
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    lineNo = i + 1;
                    foundIn = lines[i].Trim();
                    break;
                }
            }

            result.Problems.Add(new ContentProblem
            {
                Category = "🚫 Запрещённое слово",
                Message = $"Найдено запрещённое слово: '{forbidden}'",
                FoundText = foundIn,
                LineNumber = lineNo
            });
        }
    }

    private static void CheckAllCaps(string content, ContentModerationResult result)
    {
        // Если весь текст в капсе (более 80% букв - заглавные)
        var letters = content.Where(char.IsLetter).ToList();
        if (letters.Count < 10)
            return;

        var upperCount = letters.Count(char.IsUpper);
        var ratio = (double)upperCount / letters.Count;

        if (ratio > 0.8)
        {
            result.Problems.Add(new ContentProblem
            {
                Category = "📢 КРИК (ВЕСЬ ТЕКСТ ЗАГЛАВНЫМИ)",
                Message = "Текст написан почти полностью ЗАГЛАВНЫМИ буквами - похоже на крик",
                FoundText = "80%+ букв - прописные",
                LineNumber = 0
            });
        }
    }

    private static void CheckSpam(string content, ContentModerationResult result)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Проверяем на повторяющиеся строки
        var lineCount = lines.Length;
        if (lineCount < 3)
            return;

        var lineCounts = new Dictionary<string, int>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 5)
                continue;

            if (lineCounts.ContainsKey(trimmed))
                lineCounts[trimmed]++;
            else
                lineCounts[trimmed] = 1;
        }

        // Если одна строка повторяется более 3 раз - спам
        foreach (var kvp in lineCounts.Where(x => x.Value > 3))
        {
            result.Problems.Add(new ContentProblem
            {
                Category = "🔄 Спам (повторение)",
                Message = $"Одна строка повторяется {kvp.Value} раз подряд",
                FoundText = kvp.Key,
                LineNumber = 0
            });
        }
    }

    private static void CheckExtremism(string content, ContentModerationResult result)
    {
        var lowerContent = content.ToLower();

        // Проверяем на символы свастики (много вариантов)
        if (lowerContent.Contains("卐") || lowerContent.Contains("☬") || 
            lowerContent.Contains("卍") || lowerContent.Contains("🔱"))
        {
            result.Problems.Add(new ContentProblem
            {
                Category = "⚠️ Экстремистский символ",
                Message = "Обнаружен символ, ассоциируемый с экстремизмом",
                FoundText = "Символ в тексте",
                LineNumber = 0
            });
        }

        // Проверяем на ссылки на экстремистские ресурсы (примеры)
        var extremistDomains = new[] { ".ru/extremism", "telegram.me/", "vk.com/public" };
        foreach (var domain in extremistDomains)
        {
            if (lowerContent.Contains(domain))
            {
                result.Problems.Add(new ContentProblem
                {
                    Category = "🔗 Подозрительная ссылка",
                    Message = "Найдена ссылка на потенциально экстремистский ресурс",
                    FoundText = domain,
                    LineNumber = 0
                });
            }
        }
    }
}
