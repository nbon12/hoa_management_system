
using System.ComponentModel.DataAnnotations;

namespace HOAManagementCompany.Models;

public enum ViolationStatus
{
    Open,
    Closed
}

public class Violation
{
    public Guid Id { get; set; }
    
    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = "";
    
    [Required(ErrorMessage = "Status is required.")]
    public ViolationStatus Status { get; set; } = ViolationStatus.Open;
    
    [Required(ErrorMessage = "Occurrence date is required.")]
    public DateTime OccurrenceDate { get; set; } = DateTime.UtcNow;
    
    [Required(ErrorMessage = "Violation type is required.")]
    public Guid ViolationTypeId { get; set; }
    
    public ViolationType? ViolationType { get; set; }
}
