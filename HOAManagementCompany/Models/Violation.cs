
using HOAManagementCompany.Models;

public class Violation
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public ViolationType ViolationType { get; set; }
}
