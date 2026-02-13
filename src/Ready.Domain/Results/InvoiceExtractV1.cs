namespace Ready.Domain.Results;

public sealed record InvoiceExtractV1(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Currency,
    decimal TotalExclVat,
    decimal VatAmount,
    decimal TotalInclVat,
    decimal? VatRateGuess,
    SupplierInfo Supplier,
    BuyerInfo? Buyer,
    QualityInfo Quality,
    IReadOnlyList<ExtractionIssue> Issues,
    IReadOnlyList<FieldEvidence> Evidence
)
{
    public const string ResultType = "InvoiceExtract";
    public const string Version = "v1";
}

public sealed record SupplierInfo(
    string Name,
    string? CountryCode,
    string? VatId,
    string? Kvk,
    string? Iban
);

public sealed record BuyerInfo(
    string? Name,
    string? VatId
);

public sealed record QualityInfo(
    double Confidence,
    IReadOnlyDictionary<string, double>? FieldConfidence
);

public sealed record ExtractionIssue(
    string Code,          // e.g. MISSING_INVOICE_NUMBER, VAT_MISMATCH, LOW_CONFIDENCE
    string Severity,      // info | warning | error
    string Message,
    string? Field
);

public sealed record FieldEvidence(
    string Field,
    string TextSnippet,
    int? Page
);