using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Payments.Services;
using Xunit;

namespace HOAManagementCompany.UnitTests.Payments;

public class ReceiptFactoryTests
{
    [Fact]
    public void NewConfirmationNumber_HasPrefixAndFixedLength()
    {
        var conf = ReceiptFactory.NewConfirmationNumber();
        Assert.StartsWith("CONF-", conf);
        Assert.Equal(15, conf.Length);
        Assert.Equal(conf.ToUpperInvariant(), conf);
    }

    [Fact]
    public void MaskMethod_Card_FormatsBrandAndLast4()
    {
        Assert.Equal("Visa •• 4242", ReceiptFactory.MaskMethod("visa", "4242"));
    }

    [Fact]
    public void MaskMethod_NullBrand_FallsBackToCard()
    {
        Assert.Equal("Card •• 1111", ReceiptFactory.MaskMethod(null, "1111"));
    }

    [Fact]
    public void MaskMethod_NoLast4_IsAch()
    {
        Assert.Equal("ACH", ReceiptFactory.MaskMethod("visa", null));
    }

    [Fact]
    public void Create_CopiesTransactionAmounts_AndMasksMethod()
    {
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            GrossAmount = 250m,
            FeeAmount = 7.50m,
            Total = 257.50m,
        };

        var receipt = ReceiptFactory.Create(txn, "mastercard", "5555");

        Assert.Equal(txn.Id, receipt.TransactionId);
        Assert.Equal(txn.OwnerId, receipt.OwnerId);
        Assert.Equal(250m, receipt.GrossAmount);
        Assert.Equal(7.50m, receipt.FeeAmount);
        Assert.Equal(257.50m, receipt.Total);
        Assert.Equal("Mastercard •• 5555", receipt.MaskedMethod);
        Assert.StartsWith("CONF-", receipt.ConfirmationNumber);
        Assert.Contains("257.50", receipt.RenderModel);
    }
}
