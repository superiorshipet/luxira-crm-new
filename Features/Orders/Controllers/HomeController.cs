using System.Globalization;
using System.Security.Claims;
using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("Home")]
public sealed class HomeController(ApplicationDbContext context) : ControllerBase
{
    private const int ActivityWindowMinutes = 30;

    internal static string ActivityKey(string userId, string suffix) => $"Luxira.EmployeeOrderActivity:{userId}:{suffix}";

    [HttpGet("GetHomeVisibleDeliveryPrices")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetHomeVisibleDeliveryPrices(string? ids, CancellationToken ct)
    {
        var parsedIds = (ids ?? string.Empty).Split([',', ';', '|', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value.Trim(), out var id) ? id : 0).Where(id => id > 0).Distinct().Take(500).ToList();
        if (parsedIds.Count == 0) return Ok(new { success = true, items = Array.Empty<object>() });

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var query = context.Orders.AsNoTracking().Where(order => parsedIds.Contains(order.Id));
        if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            query = query.Where(order => order.DeliveryCompany != null && order.DeliveryCompany.UserId == currentUserId && !order.IsHidden);
        else if (User.IsInRole("CallCenter"))
            query = query.Where(order => order.ApplicationUserId == currentUserId);
        else if (User.IsInRole("FollowUpDepartment"))
        {
            var allowedCompanies = context.EmployeeManufacturingCompanies.AsNoTracking()
                .Where(access => access.ApplicationUserId == currentUserId && access.CanSeeManufacturingCompany)
                .Select(access => access.ManufacturingCompanyId);
            query = query.Where(order => order.ManufacturingCompanyId.HasValue && allowedCompanies.Contains(order.ManufacturingCompanyId.Value));
        }
        var items = await query.Select(order => new { orderId = order.Id, deliveryPrice = order.DeliveryPrice }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpPost("RegisterCreateOrderIntentActivity")]
    public IActionResult RegisterCreateOrderIntentActivity()
    {
        if (!User.IsInRole("CallCenter")) return Ok(new { success = true, tracked = false });
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Ok(new { success = true, tracked = false });
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!ReadUnix(ActivityKey(userId, "MonitorStartedUnix")).HasValue) WriteUnix(ActivityKey(userId, "MonitorStartedUnix"), now);
        WriteUnix(ActivityKey(userId, "LastCreateOrderOpenedUnix"), now);
        return Ok(new { success = true, tracked = true });
    }

    [HttpGet("GetHourlyOrderActivityReminder")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult GetHourlyOrderActivityReminder()
    {
        if (!User.IsInRole("CallCenter")) return Ok(new { success = true, shouldNotify = false, intervalMinutes = ActivityWindowMinutes });
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Ok(new { success = true, shouldNotify = false, intervalMinutes = ActivityWindowMinutes });
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var window = ActivityWindowMinutes * 60L;
        var monitorStarted = ReadUnix(ActivityKey(userId, "MonitorStartedUnix"));
        if (!monitorStarted.HasValue)
        {
            WriteUnix(ActivityKey(userId, "MonitorStartedUnix"), now);
            return Ok(new { success = true, shouldNotify = false, intervalMinutes = ActivityWindowMinutes, nextCheckAfterSeconds = (int)window });
        }
        var age = Math.Max(0, now - monitorStarted.Value);
        if (age < window) return Ok(new { success = true, shouldNotify = false, intervalMinutes = ActivityWindowMinutes, nextCheckAfterSeconds = (int)Math.Max(15, window - age) });
        var from = now - window;
        var hasOrders = ReadUnix(ActivityKey(userId, "LastOrderCreatedUnix")) is long orderAt && orderAt >= from;
        var openedCreateOrder = ReadUnix(ActivityKey(userId, "LastCreateOrderOpenedUnix")) is long openedAt && openedAt >= from;
        if (hasOrders && openedCreateOrder)
            return Ok(new { success = true, shouldNotify = false, intervalMinutes = ActivityWindowMinutes, hasOrders, openedCreateOrder, nextCheckAfterSeconds = 60 });
        var details = !hasOrders && !openedCreateOrder
            ? "لم يتم إنشاء أي طلب جديد ولم يتم فتح «إنشاء طلب» خلال آخر 30 دقيقة."
            : !hasOrders ? "تم فتح «إنشاء طلب»، لكن لم يتم تسجيل أي طلب جديد خلال آخر 30 دقيقة."
            : "تم تسجيل طلب جديد، لكن لم يتم رصد فتح «إنشاء طلب» خلال آخر 30 دقيقة.";
        return Ok(new
        {
            success = true, shouldNotify = true, intervalMinutes = ActivityWindowMinutes, nextCheckAfterSeconds = 60,
            notification = new
            {
                id = $"employee-no-orders-30m:{userId}:{now / window}", type = "employee-no-orders-hourly",
                alertType = "employee-no-orders-hourly", title = "تنبيه متابعة تحصيل الطلبات",
                message = "مرّت 30 دقيقة دون اكتمال نشاط تحصيل الطلبات.", details, hasOrders, openedCreateOrder,
                clientOnly = true, requireConfirm = true
            }
        });
    }

    private long? ReadUnix(string key) => long.TryParse(HttpContext.Session.GetString(key), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
    private void WriteUnix(string key, long value) => HttpContext.Session.SetString(key, value.ToString(CultureInfo.InvariantCulture));
}
