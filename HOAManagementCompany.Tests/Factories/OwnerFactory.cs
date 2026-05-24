using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Tests.Factories;

public static class OwnerFactory
{
    public static Owner Create(Guid propertyId, string email = "owner@nekohoa.dev")
    {
        return new Owner
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            FirstName = "Jane",
            LastName = "Doe",
            Email = email,
            Phone = "408-555-0100",
            MailingToProperty = true,
            VotingRights = true
        };
    }
}
