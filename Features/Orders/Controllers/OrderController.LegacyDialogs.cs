using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    [HttpGet("/Order/FailureReasonModal")]
    [HttpPost("/Order/FailureReasonModal")]
    public IActionResult FailureReasonModal() => Ok(new
    {
        title = "سبب فشل التسليم",
        submitUrl = "/Order/UpdateFailedDelivery",
        requiresReason = true,
        supportsImage = true
    });

    [HttpGet("/Order/PaymentReceiptModal")]
    [HttpPost("/Order/PaymentReceiptModal")]
    public IActionResult PaymentReceiptModal() => Ok(new
    {
        title = "إيصال الحوالة البنكية",
        submitUrl = "/Order/SetIsPaid/{id}",
        accepts = "image/*",
        maxBytes = 30 * 1024 * 1024
    });
}
