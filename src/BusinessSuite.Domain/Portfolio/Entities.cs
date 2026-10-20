using Framework.Domain.Abstractions;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Domain.Portfolio;

public abstract class TenantRecord : BaseEntity<Guid>
{
    public Guid TenantId { get; protected set; }
    protected TenantRecord() { }
    protected TenantRecord(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new DomainException("Tenant is required.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
    }
    protected static string Required(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > limit)
            throw new DomainException($"A value between 1 and {limit} characters is required.");
        return value.Trim();
    }
}

public sealed class Portfolio : TenantRecord
{
    public string DisplayName { get; private set; } = "";
    public string ReportingCurrency { get; private set; } = "";
    private Portfolio() { }
    public Portfolio(Guid tenantId, string name, string currency) : base(tenantId)
    {
        DisplayName = Required(name, 200);
        ReportingCurrency = CurrencyCode.Validate(currency);
    }
}

public sealed class LegalEntity : TenantRecord
{
    public Guid PortfolioId { get; private set; }
    public string RegisteredName { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public string RegistrationNumber { get; private set; } = "";
    public string FunctionalCurrency { get; private set; } = "";
    public string Locale { get; private set; } = "";
    public int FiscalYearStartMonth { get; private set; }
    private LegalEntity() { }
    public LegalEntity(Guid tenantId, Guid portfolioId, string name, string country, string registration,
        string currency, string locale, int fiscalYearStartMonth) : base(tenantId)
    {
        if (portfolioId == Guid.Empty) throw new DomainException("Portfolio is required.");
        PortfolioId = portfolioId;
        RegisteredName = Required(name, 200);
        CountryCode = Required(country, 2).ToUpperInvariant();
        if (CountryCode.Length != 2 || CountryCode.Any(c => c is < 'A' or > 'Z'))
            throw new DomainException("Country must be a two-letter code.");
        RegistrationNumber = Required(registration, 100);
        FunctionalCurrency = CurrencyCode.Validate(currency);
        Locale = Required(locale, 35);
        try { _ = System.Globalization.CultureInfo.GetCultureInfo(Locale); }
        catch (System.Globalization.CultureNotFoundException) { throw new DomainException("Locale is invalid."); }
        if (fiscalYearStartMonth is < 1 or > 12) throw new DomainException("Fiscal year start month must be 1-12.");
        FiscalYearStartMonth = fiscalYearStartMonth;
    }
}

public sealed class Branch : TenantRecord
{
    public Guid LegalEntityId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string TimeZone { get; private set; } = "";
    public string Address { get; private set; } = "";
    private Branch() { }
    public Branch(Guid tenantId, Guid legalEntityId, string name, string timeZone, string address) : base(tenantId)
    {
        if (legalEntityId == Guid.Empty) throw new DomainException("Legal entity is required.");
        LegalEntityId = legalEntityId;
        DisplayName = Required(name, 200);
        TimeZone = Required(timeZone, 100);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new DomainException("Time zone is invalid."); }
        Address = Required(address, 500);
    }
}

public static class CurrencyCode
{
    // Initial supported currency catalog; expanding it is an explicit product decision.
    private static readonly HashSet<string> Supported = ["EUR", "CHF", "USD", "GBP", "ALL", "MKD", "RSD", "BAM", "TRY", "HUF", "PLN", "CZK", "RON", "SEK", "NOK", "DKK", "CAD", "AUD", "JPY"];
    public static IReadOnlyCollection<string> All => Supported.Order().ToArray();
    public static string Validate(string value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (!Supported.Contains(code)) throw new DomainException("Currency is not supported.");
        return code;
    }
}
