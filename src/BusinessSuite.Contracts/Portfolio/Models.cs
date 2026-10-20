using System.ComponentModel.DataAnnotations;

namespace BusinessSuite.Contracts.Portfolio;

public sealed record CreatePortfolioRequest(
    [Required, StringLength(200)] string DisplayName,
    [Required, StringLength(3, MinimumLength = 3)] string ReportingCurrency);
public sealed record CreateLegalEntityRequest(
    Guid PortfolioId,
    [Required, StringLength(200)] string RegisteredName,
    [Required, StringLength(2, MinimumLength = 2)] string CountryCode,
    [Required, StringLength(100)] string RegistrationNumber,
    [Required, StringLength(3, MinimumLength = 3)] string FunctionalCurrency,
    [Required, StringLength(35)] string Locale,
    [Range(1, 12)] int FiscalYearStartMonth = 1);
public sealed record CreateBranchRequest(
    Guid LegalEntityId,
    [Required, StringLength(200)] string DisplayName,
    [Required, StringLength(100)] string TimeZone,
    [Required, StringLength(500)] string Address);

public sealed record PortfolioResponse(Guid Id, string DisplayName, string ReportingCurrency);
public sealed record LegalEntityResponse(Guid Id, Guid PortfolioId, string RegisteredName, string CountryCode,
    string RegistrationNumber, string FunctionalCurrency, string Locale, int FiscalYearStartMonth);
public sealed record BranchResponse(Guid Id, Guid LegalEntityId, string DisplayName, string TimeZone, string Address);
