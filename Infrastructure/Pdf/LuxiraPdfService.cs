using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Features.Employees.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Luxira.Api.Infrastructure.Pdf;

public class LuxiraPdfService
{
    public LuxiraPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateOrderReceiptPdf(List<Order> orders)
    {
        var document = Document.Create(container =>
        {
            foreach (var order in orders)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text($"وصل طلب #{order.Id}").Bold().FontSize(16).AlignCenter();
                        col.Item().Text($"تاريخ الطلب: {order.CreatedDate:yyyy-MM-dd HH:mm}").FontSize(10).AlignCenter();
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(5);
                        col.Item().Text($"العميل: {order.CustomerName}").Bold();
                        col.Item().Text($"الهاتف: {order.TelephoneNumber}");
                        col.Item().Text($"العنوان: {order.State} - {order.Address}");
                        col.Item().Text($"شركة التوصيل: {order.DeliveryCompany?.Name ?? "-"}");

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("الكمية").Bold();
                                header.Cell().Text("السعر").Bold();
                                header.Cell().Text("الإجمالي").Bold();
                            });

                            foreach (var item in order.OrderWarehouses)
                            {
                                table.Cell().Text(item.Amount.ToString());
                                table.Cell().Text($"{item.UnitPrice:N0}");
                                table.Cell().Text($"{(item.Amount * item.UnitPrice):N0}");
                            }
                        });

                        col.Item().PaddingTop(10).Text($"المبلغ المطلوب: {order.TotalPrice:N0}").Bold().FontSize(14);
                    });

                    page.Footer().AlignCenter().Text("شكراً لتعاملكم معنا - Luxira CRM").FontSize(9);
                });
            }
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateEmployeeTransactionReceiptPdf(EmployeeTransaction transaction)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(12));
                page.Header().Text($"إيصال حركة موظف #{transaction.Id}").Bold().FontSize(17).AlignCenter();
                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"الموظف: {transaction.Employee?.DisplayName ?? transaction.Employee?.Name ?? transaction.EmployeeId.ToString()}");
                    column.Item().Text($"نوع الحركة: {transaction.TransactionType}");
                    column.Item().Text($"المبلغ: {transaction.Amount:N2}").Bold();
                    column.Item().Text($"السبب: {transaction.Reason ?? "-"}");
                    column.Item().Text($"التاريخ: {transaction.Date:yyyy-MM-dd HH:mm}");
                });
                page.Footer().AlignCenter().Text("Luxira CRM").FontSize(9);
            });
        });
        return document.GeneratePdf();
    }

    public byte[] GenerateDeliveryManifestPdf(string companyName, List<Order> orders)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"كشف تسليم شحنات - {companyName}").Bold().FontSize(15);
                    col.Item().Text($"تاريخ الكشف: {DateTime.UtcNow:yyyy-MM-dd HH:mm} | العدد الكلي: {orders.Count} طلب").FontSize(9);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("الطلب").Bold();
                        header.Cell().Text("العميل").Bold();
                        header.Cell().Text("الهاتف").Bold();
                        header.Cell().Text("المحافظة").Bold();
                        header.Cell().Text("المبلغ").Bold();
                    });

                    foreach (var o in orders)
                    {
                        table.Cell().Text($"#{o.Id}");
                        table.Cell().Text(o.CustomerName ?? "");
                        table.Cell().Text(o.TelephoneNumber ?? "");
                        table.Cell().Text(o.State ?? "");
                        table.Cell().Text($"{o.TotalPrice:N0}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("صفحة ");
                    x.CurrentPageNumber();
                    x.Span(" من ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateWarehouseInventoryPdf(string companyName, IReadOnlyList<Warehouse> warehouses)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.Header().Text($"كشف مخزون - {companyName}").Bold().FontSize(16).AlignCenter();
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Text("المنتج").Bold();
                    header.Cell().Text("السعر").Bold();
                    header.Cell().Text("المتوفر").Bold();
                    header.Cell().Text("الأصلي").Bold();
                    header.Cell().Text("المباع").Bold();
                });
                foreach (var item in warehouses)
                {
                    table.Cell().Text(item.Name ?? string.Empty);
                    table.Cell().Text(item.Price.ToString("N2"));
                    table.Cell().Text(item.Amount.ToString());
                    table.Cell().Text(item.UnchangingAmount.ToString());
                    table.Cell().Text((item.UnchangingAmount - item.Amount).ToString());
                }
            });
            page.Footer().AlignCenter().Text($"{DateTime.UtcNow:yyyy-MM-dd HH:mm}");
        })).GeneratePdf();
    }
}
