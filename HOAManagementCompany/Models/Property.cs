using System.ComponentModel.DataAnnotations;

namespace HOAManagementCompany.Models;

public class Property : BaseAuditableEntity
{
    public Guid Id { get; set; }

    [Required]
    public string OwnerUserId { get; set; } = "";

    [Required]
    [StringLength(500)]
    public string DisplayName { get; set; } = "";
}
