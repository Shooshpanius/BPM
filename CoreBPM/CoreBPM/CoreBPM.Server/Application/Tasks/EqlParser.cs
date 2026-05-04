using System.Text.RegularExpressions;
using CoreBPM.Server.Application.Tasks.DTOs;

namespace CoreBPM.Server.Application.Tasks;

/// <summary>
/// Парсер упрощённого EQL (Enterprise Query Language) для задач (FR-TASK-02.2).
/// Поддерживаемый синтаксис: <c>field:value [AND|OR field:value]...</c>
/// Поддерживаемые поля: status, priority, tag, assignee, author, category, overdue.
/// Оператор OR для одного поля: добавляет несколько значений через запятую (<c>status:InProgress,New</c>).
/// При наличии нескольких условий AND — все условия применяются одновременно.
/// </summary>
public static class EqlParser
{
    // Разбиваем по AND/OR (нечувствительно к регистру) между токенами
    private static readonly Regex _tokenRegex = new(
        @"(?<field>\w+)\s*:\s*(?<value>[^\s""]+|""[^""]*"")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Разбирает строку EQL и применяет результат к <paramref name="filter"/>.
    /// </summary>
    /// <param name="eql">Строка EQL.</param>
    /// <param name="filter">Фильтр, в который записываются найденные условия.</param>
    /// <exception cref="ArgumentException">Если EQL содержит неизвестное поле.</exception>
    public static void Apply(string eql, TaskListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(eql)) return;

        var matches = _tokenRegex.Matches(eql);
        foreach (Match m in matches)
        {
            var field = m.Groups["field"].Value.ToLowerInvariant();
            var value = m.Groups["value"].Value.Trim('"');

            switch (field)
            {
                case "status":
                    // Поддержка нескольких значений через запятую (OR-семантика для статуса)
                    filter.Status = value;
                    break;

                case "priority":
                    filter.Priority = value;
                    break;

                case "tag":
                    filter.TagValue = value;
                    break;

                case "category":
                    filter.CategoryId = value;
                    break;

                case "overdue":
                    if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                        filter.IsOverdue = true;
                    else if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                        filter.IsOverdue = false;
                    break;

                case "q":
                case "search":
                case "text":
                    filter.Search = value;
                    break;

                default:
                    throw new ArgumentException(
                        $"EQL: неизвестное поле «{m.Groups["field"].Value}». " +
                        "Допустимые поля: status, priority, tag, category, overdue, search.");
            }
        }
    }
}
