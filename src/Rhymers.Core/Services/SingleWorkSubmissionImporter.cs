using System.Text.RegularExpressions;
using Rhymers.Core.Models;

namespace Rhymers.Core.Services;

public sealed class SingleWorkSubmission
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Приём одной конкурсной работы от автора.
/// Ведущий вставляет полный текст стихотворения и вручную назначает автора.
/// Первая содержательная строка считается заголовком; тема может стоять в последних скобках:
/// "ПОЭТ ВО СНЕ И НАЯВУ (Тема)".
/// </summary>
public sealed class SingleWorkSubmissionImporter
{
    private static readonly Regex TitleWithTopicRegex = new(
        @"^\s*(?<title>.+?)\s*[\(（](?<topic>[^\)）]{1,120})[\)）]\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public SingleWorkSubmission Parse(string? text, string? author, int number)
    {
        string cleanedText = NormalizeContent(text);
        string firstLine = cleanedText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(x => x.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x != ".") ?? string.Empty;

        string title = firstLine;
        string topic = string.Empty;

        Match match = TitleWithTopicRegex.Match(firstLine);
        if (match.Success)
        {
            title = CleanTitle(match.Groups["title"].Value);
            topic = CleanTitle(match.Groups["topic"].Value);
        }
        else
        {
            title = CleanTitle(title);
        }

        return new SingleWorkSubmission
        {
            Number = number,
            Title = title,
            Topic = topic,
            Author = CleanAuthor(author),
            Content = cleanedText
        };
    }

    public ContestWork ToContestWork(SingleWorkSubmission submission)
    {
        return new ContestWork
        {
            Number = submission.Number,
            Title = submission.Title,
            Author = submission.Author,
            Topic = submission.Topic,
            Content = submission.Content
        };
    }

    private static string NormalizeContent(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }

    private static string CleanTitle(string? value)
    {
        string clean = (value ?? string.Empty).Trim();
        clean = clean.Trim('"', '«', '»', '“', '”', '„');
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean;
    }

    private static string CleanAuthor(string? value)
    {
        string clean = (value ?? string.Empty).Trim();
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean;
    }
}
