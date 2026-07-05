namespace Rhymers.Core.Models;

/// <summary>
/// Запись в зале славы - сохраняет информацию о победителе конкурса в архив.
/// </summary>
public sealed class HallOfFameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    /// <summary>ID конкурса, из которого этот победитель</summary>
    public string ContestId { get; set; } = string.Empty;
    
    /// <summary>Номер/название конкурса</summary>
    public string ContestNumber { get; set; } = string.Empty;
    public string ContestName { get; set; } = string.Empty;
    
    /// <summary>Место в конкурсе (1, 2, 3 для подиума; 4+ для грамот; 0 = номинация)</summary>
    public int Place { get; set; }
    public string PlaceTitle { get; set; } = string.Empty; // "1 место", "За оригинальность" и т.д.
    
    /// <summary>Номер работы</summary>
    public int WorkNumber { get; set; }
    
    /// <summary>Название/тема работы</summary>
    public string Topic { get; set; } = string.Empty;
    
    /// <summary>Автор работы</summary>
    public string Author { get; set; } = string.Empty;
    
    /// <summary>Сумма баллов / Средний балл</summary>
    public int TotalScore { get; set; }
    public decimal AverageScore { get; set; }
    public int VotesCount { get; set; }
    
    /// <summary>Фото автора (опционально)</summary>
    public string? AuthorPhotoUrl { get; set; }
    
    /// <summary>Краткое описание работы / достижения</summary>
    public string? Description { get; set; }
    
    /// <summary>Дата добавления в зал славы</summary>
    public DateTime AddedAt { get; set; } = DateTime.Now;
    
    /// <summary>Дата конкурса</summary>
    public DateTime ContestDate { get; set; }
}
