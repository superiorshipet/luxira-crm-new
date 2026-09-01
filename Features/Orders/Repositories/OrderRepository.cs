using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;

namespace Luxira.Api.Features.Orders.Repositories;

public class OrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Order> Items, int TotalCount)> GetPagedOrdersAsync(OrderFilterRequest filter, CancellationToken ct = default)
    {
        var query = _context.Orders
            .Include(o => o.DeliveryCompany)
            .Include(o => o.ApplicationUser)
            .Include(o => o.OrderWarehouses)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            if (int.TryParse(s, out int orderId))
            {
                query = query.Where(o => o.Id == orderId || o.TelephoneNumber.Contains(s) || o.CustomerName.Contains(s));
            }
            else
            {
                query = query.Where(o => o.CustomerName.Contains(s) || o.TelephoneNumber.Contains(s) || (o.SourceName != null && o.SourceName.Contains(s)));
            }
        }

        if (filter.Status.HasValue && filter.Status.Value > 0)
        {
            query = query.Where(o => o.OrderStatus == filter.Status.Value);
        }

        if (filter.Country.HasValue && filter.Country.Value > 0)
        {
            query = query.Where(o => o.Country == filter.Country.Value);
        }

        if (filter.DeliveryCompanyId.HasValue && filter.DeliveryCompanyId.Value > 0)
        {
            query = query.Where(o => o.DeliveryCompanyId == filter.DeliveryCompanyId.Value);
        }

        if (filter.ManufacturingCompanyId.HasValue && filter.ManufacturingCompanyId.Value > 0)
        {
            query = query.Where(o => o.ManufacturingCompanyId == filter.ManufacturingCompanyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(o => o.ApplicationUserId == filter.UserId);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(o => o.CreatedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(o => o.CreatedDate <= filter.ToDate.Value);
        }

        int totalCount = await query.CountAsync(ct);

        int page = filter.Page > 0 ? filter.Page : 1;
        int pageSize = filter.PageSize > 0 && filter.PageSize <= 200 ? filter.PageSize : 50;

        var items = await query
            .OrderByDescending(o => o.IsPinned)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Orders
            .Include(o => o.DeliveryCompany)
            .Include(o => o.ApplicationUser)
            .Include(o => o.OrderWarehouses)
            .Include(o => o.StatusHistories)
            .Include(o => o.EditHistories)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken ct = default)
    {
        var result = await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken ct = default)
    {
        await _context.OrderStatusHistories.AddAsync(history, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<OrderStatsDto> GetStatsAsync(int? country = null, CancellationToken ct = default)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(o => o.Country == country.Value);
        }

        int total = await query.CountAsync(ct);
        int newOrders = await query.CountAsync(o => o.OrderStatus == 1, ct);
        int delivered = await query.CountAsync(o => o.OrderStatus == 5, ct); // تم التوصيل
        int returned = await query.CountAsync(o => o.OrderStatus == 7, ct);  // مرتجع
        int cancelled = await query.CountAsync(o => o.OrderStatus == 9, ct); // ملغي
        decimal revenue = await query.Where(o => o.OrderStatus == 5).SumAsync(o => o.TotalPrice, ct);

        return new OrderStatsDto(total, newOrders, delivered, returned, cancelled, revenue);
    }
}
