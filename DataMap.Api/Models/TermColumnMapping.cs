namespace DataMap.Api.Models;

public class TermColumnMapping
{
    public Guid Id { get; set; }
    public Guid TermId { get; set; }
    public Guid ColumnId { get; set; }

    public BusinessTerm Term { get; set; } = null!;
    public Column Column { get; set; } = null!;
}
