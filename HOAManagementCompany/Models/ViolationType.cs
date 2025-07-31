using System.ComponentModel.DataAnnotations;

namespace HOAManagementCompany.Models;

public class ViolationType : BaseAuditableEntity
{
    public Guid Id { get; set; }
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name {get; set;} = string.Empty;
    [Required(ErrorMessage = "Covenant Text is required.")]
    [StringLength(10000, ErrorMessage = "Covenant Text cannot exceed 10000 characters.")]
    public string CovenantText { get; set; } = string.Empty; // the related governing covenant text
}