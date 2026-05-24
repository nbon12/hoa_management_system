namespace HOAManagementCompany.Domain.Entities;

public class DirectoryField
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Shared { get; set; }

    public Property Property { get; set; } = null!;
}
