namespace HOAManagementCompany.Domain;

// <!-- REPOWISE:START domain=shared-kernel -->
// Canonical business-error type: carries a stable code, client-safe message, and the HTTP
// status the error maps to. Raised by any feature; translated to the uniform `{ code, message }`
// envelope centrally by GlobalExceptionHandler (015-architecture-remediation FR-006/FR-007).
// <!-- REPOWISE:END -->

/// <summary>
/// A known business-rule failure (e.g. EMAIL_TAKEN, PROPERTY_ACCESS_DENIED). Lives in Domain —
/// the shared kernel — so features can throw/catch it without importing another feature slice.
/// </summary>
public class DomainException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
