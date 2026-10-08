namespace HOAManagementCompany.Domain.Enums;

// Kind of exterior change requested. Owned by the resident submission spec; defined
// here so the shared data model and seed data can use it (027 data-model.md).
public enum ArcProjectType
{
    Fence,
    Solar,
    ExteriorPaint,
    Outbuilding,
    Landscaping,
    WindowsDoors,
    Addition,
    Other
}
