using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Storage;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

public class StorageOptionsValidatorTests
{
    private static readonly StorageOptionsValidator Validator = new();

    private static StorageOptions Valid() => new()
    {
        ServiceUrl = "http://localhost:9000",
        AccessKey = "minioadmin",
        SecretKey = "minioadmin",
        BucketName = "hoa-documents",
        ForcePathStyle = true,
    };

    [Fact]
    public void DefaultValidConfig_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Fact]
    public void EmptyOptions_Fails()
    {
        // Mirrors a missing Storage section: required fields default to empty (FR-011).
        Assert.False(Validator.Validate(new StorageOptions()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingServiceUrl_Fails(string value)
    {
        var o = Valid();
        o.ServiceUrl = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void NonAbsoluteServiceUrl_Fails()
    {
        var o = Valid();
        o.ServiceUrl = "localhost:9000"; // no scheme → not absolute
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingAccessKey_Fails(string value)
    {
        var o = Valid();
        o.AccessKey = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSecretKey_Fails(string value)
    {
        var o = Valid();
        o.SecretKey = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void MissingBucketName_Fails()
    {
        var o = Valid();
        o.BucketName = "";
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void NullPublicServiceUrl_Passes()
    {
        var o = Valid();
        o.PublicServiceUrl = null;
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void InvalidPublicServiceUrl_Fails()
    {
        var o = Valid();
        o.PublicServiceUrl = "not-a-uri";
        Assert.False(Validator.Validate(o).IsValid);
    }
}
