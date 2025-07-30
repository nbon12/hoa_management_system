using System;

namespace HOAManagementCompany.Models;

// Test entities for audit functionality testing
public class TestAuditableEntity : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class TestNonAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
} 