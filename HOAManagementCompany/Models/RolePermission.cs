using System.ComponentModel.DataAnnotations;

namespace HOAManagementCompany.Models;

public class RolePermission
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string RoleName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Permission { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
} 