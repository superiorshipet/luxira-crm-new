using Luxira.Api.Features.Auth.Models;

namespace Luxira.Api.Features.DeliveryCompanies.Models;

public class DeliveryCompany
{
    public int Id { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public string? InformationUrl { get; set; }
    public string? InformationS3Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
    public int Country { get; set; }
    public string? City { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public bool IsShown { get; set; } = true;
    public bool IsActive { get; set; }
    public bool IsRepresentative { get; set; }
    public bool IsAllOrdersHidden { get; set; }
    public bool AutoConvertDeliveredToBalanceUpdated { get; set; }
    public bool SupportsCashPayment { get; set; }
    public bool SupportsBankTransferPayment { get; set; }
    public bool ShowInPrepareForDelivery { get; set; }
    public string? PrepareForDeliveryCountries { get; set; }
    public bool AutoPullDeliveryInvoice { get; set; }
    public bool AutoPullAccountingInvoice { get; set; }
    public bool AutoPullCustomerInvoice { get; set; }

    public List<DeliveryCompanyPrice> Prices { get; set; } = new();
}

public class DeliveryCompanyPrice
{
    public int Id { get; set; }
    public int Country { get; set; }
    public decimal Price { get; set; }
    public string? City { get; set; }
    public int DeliveryCompanyId { get; set; }
    public DeliveryCompany? DeliveryCompany { get; set; }
}

public class StoreDeliveryCompanyAssignment
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public int DeliveryCompanyId { get; set; }
    public DeliveryCompany? DeliveryCompany { get; set; }
}

public class CamexCity
{
    public int Id { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string? CityCode { get; set; }
}

public class CamexCityMapping
{
    public int Id { get; set; }
    public string LocalState { get; set; } = string.Empty;
    public int CamexCityId { get; set; }
    public CamexCity? CamexCity { get; set; }
}

public class CamexStoreMapping
{
    public int Id { get; set; }
    public int StorefrontId { get; set; }
    public string CamexStoreName { get; set; } = string.Empty;
}
