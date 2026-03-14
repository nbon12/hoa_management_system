namespace HOAManagementCompany.Models;

public class MyViolationsResponseDto
{
    public IReadOnlyList<MyViolationItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public class MyViolationItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public DateTime OccurrenceDate { get; set; }
    public string ViolationTypeName { get; set; } = "";
    public string PropertyDisplayName { get; set; } = "";
}
