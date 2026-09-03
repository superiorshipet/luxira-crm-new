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

    public byte[] GenerateEmployeeTransactionsStatementPdf(IReadOnlyList<EmployeeTransaction> transactions)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.Header().Text("كشف حركات الموظف").Bold().FontSize(17).AlignCenter();
            page.Content().PaddingVertical(12).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(2);
                });
                table.Header(header =>
                {
                    header.Cell().Text("الرقم").Bold(); header.Cell().Text("التاريخ").Bold();
                    header.Cell().Text("المبلغ").Bold(); header.Cell().Text("السبب").Bold();
                });
                foreach (var row in transactions)
                {
                    table.Cell().Text(row.Id.ToString()); table.Cell().Text(row.Date.ToString("yyyy-MM-dd"));
                    table.Cell().Text(row.Amount.ToString("N2")); table.Cell().Text(row.Reason ?? row.TransactionType.ToString());
                }
            });
            page.Footer().AlignCenter().Text($"الإجمالي: {transactions.Sum(item => item.Amount):N2}");
        })).GeneratePdf();
    }

    public byte[] GenerateAttendanceLogPdf(IReadOnlyList<EmployeeAttendanceLog> rows)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.Header().Text("سجل الحضور والانصراف").Bold().FontSize(17).AlignCenter();
            page.Content().PaddingVertical(12).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Text("الموظف").Bold(); header.Cell().Text("التاريخ").Bold();
                    header.Cell().Text("الحضور").Bold(); header.Cell().Text("الانصراف").Bold();
                });
                foreach (var row in rows)
                {
                    table.Cell().Text(row.EmployeeName ?? row.EmployeeEmail ?? row.UserId);
                    table.Cell().Text(row.CheckInAt.ToString("yyyy-MM-dd"));
                    table.Cell().Text(row.CheckInAt.ToString("HH:mm"));
                    table.Cell().Text(row.CheckOutAt?.ToString("HH:mm") ?? "-");
                }
            });
            page.Footer().AlignCenter().Text($"Luxira CRM | {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
        })).GeneratePdf();
    }

    public byte[] GenerateSalaryPaymentReceiptPdf(EmployeeSalaryPayment payment)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A5); page.Margin(1, Unit.Centimetre);
            page.Header().Text($"إيصال راتب {payment.ReceiptNumber}").Bold().FontSize(17).AlignCenter();
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(7);
                column.Item().Text($"الموظف: {payment.Employee?.DisplayName ?? payment.Employee?.Name ?? payment.EmployeeId.ToString()}");
                column.Item().Text($"الفترة: {payment.PeriodFrom:yyyy-MM-dd} - {payment.PeriodTo:yyyy-MM-dd}");
                column.Item().Text($"أيام العمل: {payment.DaysWorked} / {payment.DaysInMonth}");
                column.Item().Text($"الراتب المستحق: {payment.RemainingAmount:N2} {payment.Currency}").Bold();
                column.Item().Text($"تاريخ الدفع: {payment.PaidAt:yyyy-MM-dd HH:mm}");
            });
        })).GeneratePdf();
    }

    public byte[] GenerateSalaryStatementPdf(IReadOnlyList<EmployeeSalaryPayment> payments)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(1, Unit.Centimetre);
            page.Header().Text("كشف الرواتب").Bold().FontSize(17).AlignCenter();
            page.Content().PaddingVertical(12).Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                table.Header(header => { header.Cell().Text("الشهر").Bold(); header.Cell().Text("الأيام").Bold(); header.Cell().Text("المبلغ").Bold(); header.Cell().Text("الحالة").Bold(); });
                foreach (var payment in payments)
                {
                    table.Cell().Text(payment.SalaryMonth.ToString("yyyy-MM")); table.Cell().Text($"{payment.DaysWorked}/{payment.DaysInMonth}");
                    table.Cell().Text($"{payment.RemainingAmount:N2} {payment.Currency}"); table.Cell().Text(payment.IsPaid ? "مدفوع" : "مسودة");
                }
            });
        })).GeneratePdf();
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

    public byte[] GenerateShipmentPriceOfferPdf(string companyName, string? address, string? phone, string? email,
        int invoiceId, DateTime createdAt, IReadOnlyList<(string Name, int Quantity, decimal Price)> products)
    {
        var total = products.Sum(item => item.Quantity * item.Price);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.4f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(11));
            page.Header().Row(row =>
            {
                row.RelativeItem().Text($"فاتورة #{invoiceId}").Bold().FontSize(18);
                row.RelativeItem().AlignRight().Text("Luxira CRM").Bold().FontSize(18);
            });
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(6);
                column.Item().Text($"الشركة المستلمة: {companyName}").Bold();
                column.Item().Text($"العنوان: {address ?? "-"}");
                column.Item().Text($"الهاتف: {phone ?? "-"}");
                column.Item().Text($"البريد الإلكتروني: {email ?? "-"}");
                column.Item().Text($"تاريخ الإرسال: {createdAt:yyyy-MM-dd}");
                column.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("اسم المنتج").Bold(); header.Cell().Text("الكمية").Bold();
                        header.Cell().Text("سعر القطعة").Bold(); header.Cell().Text("المجموع").Bold();
                    });
                    foreach (var item in products)
                    {
                        table.Cell().Text(item.Name); table.Cell().Text(item.Quantity.ToString());
                        table.Cell().Text(item.Price.ToString("N2")); table.Cell().Text((item.Quantity * item.Price).ToString("N2"));
                    }
                });
                column.Item().PaddingTop(12).AlignRight().Text($"السعر الإجمالي: {total:N2}").Bold().FontSize(14);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("صفحة "); text.CurrentPageNumber(); text.Span(" من "); text.TotalPages();
            });
        })).GeneratePdf();
    }
}
