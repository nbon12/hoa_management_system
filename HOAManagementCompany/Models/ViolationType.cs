namespace HOAManagementCompany.Models;

public class ViolationType
{
    public Guid Id { get; set; }
    public string Name {get; set;}
    public string CovenantText { get; set; } // the related governing covenant text
}