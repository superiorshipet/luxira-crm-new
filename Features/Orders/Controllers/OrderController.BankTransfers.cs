using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    private const string BankTransferPendingApprovalReason = "BankTransferPendingApproval";
    private const string BankTransferFollowUpConfirmedReason = "BankTransferFollowUpConfirmed";
    private const string BankTransferFollowUpRejectedReason = "BankTransferFollowUpRejected";
    private const string BankTransferApprovedReason = "BankTransferApproved";
    private const string BankTransferRejectedReason = "BankTransferRejected";
    private const string BankTransferReturnedToOriginalStatusReason = "BankTransferReturnedToOriginalStatus";
    private const string BankTransferApprovalNotificationReasonPrefix = "BankTransferApprovalNotification:";

    [HttpGet("/Order/GetBankTransferApprovals")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,TeamLeader,Team Leader")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetBankTransferApprovals(CancellationToken ct)
    {
        var pending = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(history => history.OrderId.HasValue && history.Reason == BankTransferPendingApprovalReason)
            .GroupBy(history => history.OrderId!.Value)
            .Select(group => group.OrderByDescending(history => history.Id).Select(history => new
            {
                OrderId = history.OrderId!.Value,
                PendingHistoryId = history.Id,
                RequestedAt = history.CreatedAt,
                RequestedByUserId = history.ApplicationUserId
            }).First())
            .ToListAsync(ct);

        var latestDecisions = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(history => history.OrderId.HasValue &&
                (history.Reason == BankTransferApprovedReason || history.Reason == BankTransferRejectedReason))
            .GroupBy(history => history.OrderId!.Value)
            .Select(group => new { OrderId = group.Key, DecisionHistoryId = group.Max(history => history.Id) })
            .ToDictionaryAsync(row => row.OrderId, row => row.DecisionHistoryId, ct);

        var active = pending
            .Where(row => !latestDecisions.TryGetValue(row.OrderId, out var decisionId) || row.PendingHistoryId > decisionId)
            .ToList();
        if (active.Count == 0) return Ok(new { success = true, items = Array.Empty<object>(), count = 0 });

        var userIds = active
            .Select(row => row.RequestedByUserId)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var userNames = await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, DisplayName = user.Name ?? user.UserName ?? user.Email ?? string.Empty })
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, ct);
        var activeByOrder = active.ToDictionary(row => row.OrderId);
        var orderIds = activeByOrder.Keys.ToArray();

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id) && order.IsPaid)
            .OrderByDescending(order => order.LastEditedDate)
            .Select(order => new
            {
                order.Id,
                order.ExternalOrderId,
                order.PaymentReceiptUrl,
                DeliveryCompanyName = order.DeliveryCompany != null ? order.DeliveryCompany.Name : string.Empty,
                order.LastEditedDate,
                order.TotalPrice,
                order.Country
            })
            .ToListAsync(ct);

        var items = orders.Select(order =>
        {
            var pendingRow = activeByOrder[order.Id];
            var requester = pendingRow.RequestedByUserId ?? string.Empty;
            return new
            {
                id = order.Id,
                shipmentCode = (order.ExternalOrderId ?? order.Id).ToString(System.Globalization.CultureInfo.InvariantCulture),
                paymentReceiptUrl = order.PaymentReceiptUrl,
                deliveryCompanyName = order.DeliveryCompanyName,
                employeeName = userNames.GetValueOrDefault(requester, "غير محدد"),
                employeeImageUrl = "/static/DefaultImage.svg",
                requestedAt = pendingRow.RequestedAt,
                orderTotalPrice = decimal.Round(order.TotalPrice, 2, MidpointRounding.AwayFromZero),
                countryCode = GetCountryCode(order.Country),
                countryName = order.Country.ToString(System.Globalization.CultureInfo.InvariantCulture),
                countryFlagUrl = "/Countries/default.svg"
            };
        }).ToList();

        return Ok(new { success = true, items, count = items.Count });
    }

    [HttpPost("/Order/ConfirmBankTransfer/{id:int}")]
    [Authorize(Roles = "FollowUpDepartment,TeamLeader,Team Leader")]
    public Task<IActionResult> ConfirmBankTransfer(int id, CancellationToken ct) =>
        FlagBankTransferAsync(id, BankTransferFollowUpConfirmedReason, "تم تأكيد المتابعة بنجاح.", ct);

    [HttpPost("/Order/FlagBankTransferNotReceived/{id:int}")]
    [Authorize(Roles = "FollowUpDepartment,TeamLeader,Team Leader")]
    public Task<IActionResult> FlagBankTransferNotReceived(int id, CancellationToken ct) =>
        FlagBankTransferAsync(id, BankTransferFollowUpRejectedReason, "تم تحديد الحوالة كغير واصلة (متابعة).", ct);

    [HttpPost("/Order/RejectBankTransfer/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public Task<IActionResult> RejectBankTransfer(int id, CancellationToken ct) =>
        DecideBankTransferAsync(id, approved: false, ct);

    [HttpPost("/Order/ApproveBankTransfer/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public Task<IActionResult> ApproveBankTransfer(int id, CancellationToken ct) =>
        DecideBankTransferAsync(id, approved: true, ct);

    [HttpGet("/Order/ValidateBankTransferChange/{id:int}")]
    public async Task<IActionResult> ValidateBankTransferChange(int id, CancellationToken ct)
    {
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return NotFound(new { success = false, message = "الطلب غير موجود." });

        var blockMessage = await GetBankTransferBlockMessageAsync(order.DeliveryCompanyId, ct);
        return blockMessage is null
            ? Ok(new { success = true, allowed = true })
            : StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = blockMessage,
                errorCode = "CALLCENTER_CASH_ONLY_BANK_TRANSFER_EDIT_BLOCKED"
            });
    }

    [HttpPost("/Order/SetIsPaid/{id:int}")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> SetIsPaid(
        int id,
        [FromQuery] bool isPaid,
        [FromForm] IFormFile? paymentReceiptFile,
        CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return NotFound();

        if (isPaid)
        {
            var blockMessage = await GetBankTransferBlockMessageAsync(order.DeliveryCompanyId, ct);
            if (blockMessage is not null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = blockMessage,
                    errorCode = "CALLCENTER_CASH_ONLY_BANK_TRANSFER_EDIT_BLOCKED"
                });
            }

            if (paymentReceiptFile is { Length: > 0 })
            {
                if (!(paymentReceiptFile.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
                    return BadRequest(new { success = false, message = "صورة إيصال الحوالة غير صالحة." });

                var stored = await _storage.UploadAsync(paymentReceiptFile, "images/receipts", User.GetUserId(), ct);
                order.PaymentReceiptUrl = stored.PublicUrl;
                order.PaymentReceiptS3Key = stored.Key;
            }
            else if (string.IsNullOrWhiteSpace(order.PaymentReceiptUrl))
            {
                return BadRequest(new { success = false, message = "الرجاء إرفاق صورة إيصال الحوالة البنكية." });
            }
        }

        var becameBankTransfer = !order.IsPaid && isPaid;
        order.IsPaid = isPaid;
        order.LastEditedDate = IstanbulTimeHelper.Now;
        if (becameBankTransfer)
        {
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = order.OrderStatus,
                CreatedAt = IstanbulTimeHelper.Now,
                ApplicationUserId = User.GetUserId(),
                Reason = BankTransferPendingApprovalReason
            });
        }

        await _context.SaveChangesAsync(ct);
        await Task.WhenAll(
            _hub.Clients.All.SendAsync("OrderDetailsUpdated", new { OrderId = id, IsPaid = order.IsPaid }, ct),
            _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = id, reason = "payment_updated" }, ct));
        if (becameBankTransfer)
        {
            await _hub.Clients.All.SendAsync("OrderStatusUpdated", new
            {
                OrderId = id,
                Status = BankTransferPendingApprovalReason,
                StatusPhrase = "قيد اعتماد الحوالة البنكية",
                ColorStyle = "background-color:#8f4a3d!important;color:#fff!important;border-color:#8f4a3d!important;"
            }, ct);
        }

        return Ok(new
        {
            redirectUrl = $"/Order/Details?id={id}",
            isPaid = order.IsPaid,
            isBankTransferPendingApproval = becameBankTransfer
        });
    }

    private async Task<IActionResult> FlagBankTransferAsync(int id, string reason, string message, CancellationToken ct)
    {
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return Ok(new { success = false, message = "الطلب غير موجود." });
        if (!order.IsPaid) return Ok(new { success = false, message = "الطلب مسجل كدفع كاش حالياً." });

        var pending = await GetLatestPendingTransferAsync(id, ct);
        if (pending is null) return Ok(new { success = false, message = "الطلب ليس قيد اعتماد الحوالة." });

        var latestDecision = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(history => history.OrderId == id && history.Id > pending.Id &&
                (history.Reason == BankTransferFollowUpConfirmedReason || history.Reason == BankTransferFollowUpRejectedReason))
            .OrderByDescending(history => history.Id)
            .Select(history => history.Reason)
            .FirstOrDefaultAsync(ct);
        if (latestDecision == reason) return Ok(new { success = true, message, orderId = id, alreadyProcessed = true });

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = id,
            Status = pending.Status,
            CreatedAt = IstanbulTimeHelper.Now,
            ApplicationUserId = User.GetUserId(),
            Reason = reason
        });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message, orderId = id, followUpConfirmed = reason == BankTransferFollowUpConfirmedReason });
    }

    private async Task<IActionResult> DecideBankTransferAsync(int id, bool approved, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return Ok(new { success = false, message = "الطلب غير موجود." });
        if (!order.IsPaid) return Ok(new { success = false, message = "الطلب مسجل كدفع كاش حالياً." });

        var pending = await GetLatestPendingTransferAsync(id, ct);
        if (pending is null) return Ok(new { success = false, message = "لا يوجد سجل حوالة قيد الاعتماد لهذا الطلب." });

        var decisionReason = approved ? BankTransferApprovedReason : BankTransferRejectedReason;
        var existingDecision = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(history => history.OrderId == id && history.Id > pending.Id &&
                (history.Reason == BankTransferApprovedReason || history.Reason == BankTransferRejectedReason))
            .OrderByDescending(history => history.Id)
            .Select(history => history.Reason)
            .FirstOrDefaultAsync(ct);
        if (existingDecision is not null)
        {
            var alreadyMessage = existingDecision == decisionReason
                ? (approved ? "تم اعتماد هذه الحوالة من قبل." : "تم رفض هذه الحوالة مسبقاً.")
                : "تم اتخاذ قرار نهائي على هذه الحوالة مسبقاً.";
            return Ok(new { success = false, message = alreadyMessage });
        }

        if (approved)
        {
            var paymentSettings = await _context.DeliveryCompanies
                .AsNoTracking()
                .Where(company => company.Id == order.DeliveryCompanyId)
                .Select(company => new { company.Name, company.SupportsCashPayment, company.SupportsBankTransferPayment })
                .FirstOrDefaultAsync(ct);
            if (paymentSettings is null)
                return Ok(new { success = false, errorCode = "DELIVERY_COMPANY_PAYMENT_SETTINGS_NOT_FOUND", message = "تعذر قراءة إعدادات الدفع الخاصة بشركة التوصيل الحالية. لم يتم اعتماد الحوالة." });
            if (!paymentSettings.SupportsBankTransferPayment)
                return Ok(new { success = false, errorCode = "DELIVERY_COMPANY_BANK_TRANSFER_NOT_ALLOWED", deliveryCompanyName = paymentSettings.Name, paymentSettings.SupportsCashPayment, paymentSettings.SupportsBankTransferPayment, message = $"لا يمكن اعتماد الحوالة لأن شركة {paymentSettings.Name} لا تقبل التحويل البنكي حاليًا." });
        }

        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        _context.OrderStatusHistories.AddRange(
            new OrderStatusHistory
            {
                OrderId = id,
                Status = order.OrderStatus,
                CreatedAt = now,
                ApplicationUserId = userId,
                Reason = decisionReason
            },
            new OrderStatusHistory
            {
                OrderId = id,
                Status = order.OrderStatus,
                CreatedAt = now.AddMilliseconds(1),
                ApplicationUserId = userId,
                Reason = BankTransferReturnedToOriginalStatusReason
            });

        var recipientIds = new[] { order.ApplicationUserId, pending.ApplicationUserId }
            .Where(recipientId => !string.IsNullOrWhiteSpace(recipientId))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var validRecipients = await _context.Users.AsNoTracking()
            .Where(user => recipientIds.Contains(user.Id) && (!user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow))
            .Select(user => user.Id)
            .ToListAsync(ct);
        for (var index = 0; index < validRecipients.Count; index++)
        {
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = id,
                Status = order.OrderStatus,
                CreatedAt = now.AddMilliseconds(index + 2),
                ApplicationUserId = validRecipients[index],
                Reason = BankTransferApprovalNotificationReasonPrefix + pending.Id,
                IsHidden = false
            });
        }

        order.LastEditedDate = now;
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await Task.WhenAll(
            _hub.Clients.All.SendAsync("OrderStatusUpdated", new
            {
                OrderId = id,
                Status = order.OrderStatus,
                ApprovalStatus = approved ? "تم الاعتماد" : "تم الرفض",
                BankTransferApproved = approved,
                IsBankTransferPendingApproval = false,
                RefreshStatusHistory = true
            }, ct),
            _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = id, reason = approved ? "bank_transfer_approved_and_restored" : "bank_transfer_rejected_and_restored" }, ct));

        return Ok(new
        {
            success = true,
            message = approved ? "تم الاعتماد بنجاح." : "تم تحديد الحوالة كغير واصلة بنجاح.",
            orderId = id,
            approvalStatus = approved ? "تم الاعتماد" : "تم الرفض",
            status = order.OrderStatus,
            bankTransferApproved = approved,
            isBankTransferPendingApproval = false,
            refreshStatusHistory = true
        });
    }

    private Task<OrderStatusHistory?> GetLatestPendingTransferAsync(int orderId, CancellationToken ct) =>
        _context.OrderStatusHistories
            .AsNoTracking()
            .Where(history => history.OrderId == orderId && history.Reason == BankTransferPendingApprovalReason)
            .OrderByDescending(history => history.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<string?> GetBankTransferBlockMessageAsync(int? deliveryCompanyId, CancellationToken ct)
    {
        if (!User.IsInRole("CallCenter") || !deliveryCompanyId.HasValue) return null;
        var settings = await _context.DeliveryCompanies
            .AsNoTracking()
            .Where(company => company.Id == deliveryCompanyId.Value)
            .Select(company => new { company.Name, company.SupportsBankTransferPayment })
            .FirstOrDefaultAsync(ct);
        return settings is { SupportsBankTransferPayment: false }
            ? $"لا يمكن تسجيل حوالة بنكية لأن شركة {settings.Name} لا تقبل التحويل البنكي حالياً."
            : null;
    }

    private static string GetCountryCode(int country) => country switch
    {
        0 => "EG",
        1 => "TR",
        2 => "IQ",
        3 => "JO",
        4 => "LY",
        5 => "KW",
        6 => "QA",
        7 => "OM",
        8 => "BH",
        9 => "TN",
        10 => "AE",
        11 => "SA",
        12 => "LB",
        13 => "PS",
        14 => "DZ",
        15 => "MA",
        _ => "—"
    };
}
