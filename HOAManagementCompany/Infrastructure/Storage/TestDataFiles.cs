namespace HOAManagementCompany.Infrastructure.Storage;

public static class TestDataFiles
{
    public static string SamplePdfPath =>
        Path.Combine(AppContext.BaseDirectory, "testdata", "sample.pdf");

    public static async Task<byte[]> ReadSamplePdfAsync(CancellationToken ct = default) =>
        await File.ReadAllBytesAsync(SamplePdfPath, ct);
}
