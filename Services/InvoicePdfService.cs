using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RetailERP.Data.Entities;
using System.Text.Json;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace RetailERP.Services;

/// <summary>Sprint 6 – Generates A4/A5 invoice PDF from a BillTemplate layout + Invoice data.</summary>
public class InvoicePdfService
{
    private readonly IWebHostEnvironment _env;

    public InvoicePdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] Generate(Invoice invoice, BillTemplate template, Company company)
    {
        var components = JsonSerializer.Deserialize<List<ReceiptPdfService.LayoutComponent>>(template.LayoutJson) ?? new();

        var fontSize = 11;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(template.PaperSize == "A5" ? PageSizes.A5 : PageSizes.A4);
                page.Margin(15, PdfUnit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(fontSize));

                page.Header().Column(col =>
                {
                    // Company header
                    if (template.ShowLogo && !string.IsNullOrEmpty(company.LogoPath))
                    {
                        var fullPath = Path.Combine(_env.WebRootPath, company.LogoPath.TrimStart('/'));
                        if (File.Exists(fullPath))
                        {
                            col.Item().AlignCenter().Height(50).Image(fullPath);
                            col.Item().PaddingBottom(5);
                        }
                    }

                    col.Item().AlignCenter().Text(company.Name).Bold().FontSize(18);

                    if (!string.IsNullOrEmpty(company.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(10);

                    if (!string.IsNullOrEmpty(company.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(10);

                    if (template.ShowGst && !string.IsNullOrEmpty(company.GstNo))
                        col.Item().AlignCenter().Text($"GSTIN: {company.GstNo}").FontSize(10);

                    if (!string.IsNullOrEmpty(template.HeaderText))
                        col.Item().AlignCenter().Text(template.HeaderText).FontSize(10);

                    col.Item().PaddingVertical(5).LineHorizontal(1);

                    // Invoice title
                    col.Item().PaddingVertical(3).AlignCenter().Text("TAX INVOICE").Bold().FontSize(14);

                    // Invoice info
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text($"Invoice #: {invoice.InvoiceNo}").Bold();
                            left.Item().Text($"Date: {invoice.InvoiceDate:dd/MM/yyyy}");
                            if (invoice.Customer != null)
                            {
                                left.Item().PaddingTop(5).Text("Bill To:").Bold();
                                left.Item().Text(invoice.Customer.Name);
                                if (!string.IsNullOrEmpty(invoice.Customer.Phone))
                                    left.Item().Text($"Ph: {invoice.Customer.Phone}").FontSize(9);
                                if (!string.IsNullOrEmpty(invoice.Customer.Email))
                                    left.Item().Text(invoice.Customer.Email).FontSize(9);
                            }
                        });
                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().Text($"Status: {(invoice.Status == 1 ? "Draft" : "Completed")}");
                            if (invoice.Warehouse != null)
                                right.Item().Text($"Warehouse: {invoice.Warehouse.Name}");
                        });
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);  // #
                        cols.RelativeColumn(4);   // Item
                        cols.ConstantColumn(40);  // Qty
                        cols.ConstantColumn(60);  // Rate
                        cols.ConstantColumn(50);  // Tax%
                        cols.ConstantColumn(65);  // Amount
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#f0f0f0").Padding(4).Text("#").Bold().FontSize(fontSize - 1);
                        header.Cell().Background("#f0f0f0").Padding(4).Text("Item").Bold().FontSize(fontSize - 1);
                        header.Cell().Background("#f0f0f0").Padding(4).AlignRight().Text("Qty").Bold().FontSize(fontSize - 1);
                        header.Cell().Background("#f0f0f0").Padding(4).AlignRight().Text("Rate").Bold().FontSize(fontSize - 1);
                        header.Cell().Background("#f0f0f0").Padding(4).AlignRight().Text("Tax%").Bold().FontSize(fontSize - 1);
                        header.Cell().Background("#f0f0f0").Padding(4).AlignRight().Text("Amount").Bold().FontSize(fontSize - 1);
                    });

                    var idx = 1;
                    foreach (var line in invoice.Lines)
                    {
                        var lineTotal = line.Qty * line.UnitPrice - line.DiscountAmount;
                        var bg = idx % 2 == 0 ? "#fafafa" : "#ffffff";
                        table.Cell().Background(bg).Padding(3).Text(idx.ToString()).FontSize(fontSize - 1);
                        table.Cell().Background(bg).Padding(3).Text(line.ItemNameSnapshot ?? line.Item?.Name ?? "–").FontSize(fontSize - 1);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text(line.Qty.ToString()).FontSize(fontSize - 1);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"₹{line.UnitPrice:N2}").FontSize(fontSize - 1);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"{line.GstPercentSnapshot ?? 0:N1}%").FontSize(fontSize - 1);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"₹{lineTotal:N2}").FontSize(fontSize - 1);
                        idx++;
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    // Compute totals from lines
                    var subTotal = invoice.Lines.Sum(l => l.Qty * l.UnitPrice);
                    var totalDiscount = invoice.Lines.Sum(l => l.DiscountAmount);
                    var totalTax = invoice.Lines.Sum(l => (l.Qty * l.UnitPrice - l.DiscountAmount) * (l.GstPercentSnapshot ?? 0) / 100m);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem(); // spacer
                        row.ConstantItem(200).Column(totals =>
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Sub Total:");
                                r.RelativeItem().AlignRight().Text($"₹{subTotal:N2}");
                            });
                            if (totalDiscount > 0)
                            {
                                totals.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Discount:");
                                    r.RelativeItem().AlignRight().Text($"- ₹{totalDiscount:N2}");
                                });
                            }
                            if (totalTax > 0)
                            {
                                totals.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Tax:");
                                    r.RelativeItem().AlignRight().Text($"+ ₹{totalTax:N2}");
                                });
                            }
                            totals.Item().PaddingVertical(2).LineHorizontal(0.5f);
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("TOTAL:").Bold();
                                r.RelativeItem().AlignRight().Text($"₹{invoice.TotalAmount:N2}").Bold();
                            });
                        });
                    });

                    col.Item().PaddingTop(10);

                    if (!string.IsNullOrEmpty(template.FooterText))
                        col.Item().AlignCenter().Text(template.FooterText).FontSize(9);

                    col.Item().AlignCenter().Text("Computer generated invoice").FontSize(8).FontColor("#999999");
                });
            });
        });

        return document.GeneratePdf();
    }
}
