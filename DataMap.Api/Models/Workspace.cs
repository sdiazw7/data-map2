namespace DataMap.Api.Models;

public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsTemplate { get; set; }
    public Guid? SourceTemplateId { get; set; }
}
