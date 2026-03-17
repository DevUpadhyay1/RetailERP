using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RetailERP.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace RetailERP.Services;

/// <summary>Sprint 6 – Generates receipt PDF from a BillTemplate layout + PosBill data.</summary>
public class ReceiptPdfService
{
    private readonly IWebHostEnvironment _env;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ReceiptPdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] Generate(PosBill bill, BillTemplate template, Company company)
    {
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        var components = JsonSerializer.Deserialize<List<LayoutComponent>>(template.LayoutJson, JsonOpts) ?? new();

        var pageWidth = template.PaperSize switch
        {
            "Thermal58mm" => 58f,
            "Thermal80mm" => 80f,
            "A4" => 210f,
            "A5" => 148f,
            _ => 80f
        };
        var isThermal = template.PaperSize.StartsWith("Thermal");
        var baseFontSize = isThermal ? 8 : 11;
        var headerFontSize = isThermal ? 10 : 14;
        var subFontSize = isThermal ? 7 : 9;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                if (isThermal)
                {
                    page.ContinuousSize(pageWidth, PdfUnit.Millimetre);
                    page.MarginHorizontal(3, PdfUnit.Millimetre);
                    page.MarginVertical(2, PdfUnit.Millimetre);
                }
                else
                {
                    if (template.PaperSize == "A5") page.Size(PageSizes.A5);
                    else page.Size(PageSizes.A4);
                    page.Margin(15, PdfUnit.Millimetre);
                }

                page.DefaultTextStyle(x => x.FontSize(baseFontSize));

                page.Content().Column(col =>
                {
                    foreach (var comp in components)
                    {
                        switch (comp.Type)
                        {
                            case "logo":
                                RenderLogo(col, company, comp.Props, isThermal);
                                break;
                            case "store_header":
                                RenderStoreHeader(col, bill, company, comp.Props, headerFontSize, subFontSize);
                                break;
                            case "bill_info":
                                RenderBillInfo(col, bill, subFontSize);
                                break;
                            case "items_table":
                                RenderItemsTable(col, bill, baseFontSize, comp.Props, isThermal);
                                break;
                            case "totals":
                                RenderTotals(col, bill, comp.Props, baseFontSize);
                                break;
                            case "payments":
                                RenderPayments(col, bill, subFontSize);
                                break;
                            case "tax_summary":
                                RenderTaxSummary(col, bill, subFontSize);
                                break;
                            case "footer":
                                RenderFooter(col, bill, comp.Props, subFontSize);
                                break;
                            case "text_block":
                                RenderTextBlock(col, bill, company, comp.Props);
                                break;
                            case "separator":
                                RenderSeparator(col, comp.Props);
                                break;
                            case "spacer":
                                RenderSpacer(col, comp.Props);
                                break;
                        }
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── Logo ──
    private void RenderLogo(ColumnDescriptor col, Company company, Dictionary<string, JsonElement>? props, bool isThermal)
    {
        if (string.IsNullOrEmpty(company.LogoPath)) return;
        var fullPath = Path.Combine(_env.WebRootPath, company.LogoPath.TrimStart('/'));
        if (!File.Exists(fullPath)) return;

        var maxH = GetInt(props, "maxHeight", isThermal ? 30 : 50);
        var align = GetStr(props, "align", "center");

        var item = col.Item().Height(maxH);
        item = align switch { "left" => item.AlignLeft(), "right" => item.AlignRight(), _ => item.AlignCenter() };
        item.Image(fullPath, ImageScaling.FitHeight);
        col.Item().PaddingBottom(2);
    }

    // ── Store Header ──
    private static void RenderStoreHeader(ColumnDescriptor col, PosBill bill, Company company, Dictionary<string, JsonElement>? props, int headerFontSize, int subFontSize)
    {
        col.Item().AlignCenter().Text(text =>
        {
            text.Line(bill.Store?.Name ?? company.Name).Bold().FontSize(headerFontSize);
        });

        var address = bill.Store?.Address ?? company.Address;
        if (!string.IsNullOrEmpty(address))
            col.Item().AlignCenter().Text(address).FontSize(subFontSize);

        var phone = bill.Store?.Phone ?? company.Phone;
        if (!string.IsNullOrEmpty(phone))
            col.Item().AlignCenter().Text($"Ph: {phone}").FontSize(subFontSize);

        if (GetBool(props, "showGst", true))
        {
            var gst = bill.Store?.GstNo ?? company.GstNo;
            if (!string.IsNullOrEmpty(gst))
                col.Item().AlignCenter().Text($"GSTIN: {gst}").FontSize(subFontSize);
        }

        var headerText = GetStr(props, "headerText", "");
        if (!string.IsNullOrEmpty(headerText))
            col.Item().AlignCenter().Text(headerText).FontSize(subFontSize);

        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    // ── Bill Info ──
    private static void RenderBillInfo(ColumnDescriptor col, PosBill bill, int subFontSize)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text($"Bill #: {bill.BillNo}").Bold().FontSize(subFontSize);
            row.RelativeItem().AlignRight().Text(bill.BillDate.ToString("dd/MM/yyyy")).FontSize(subFontSize);
        });
        col.Item().Text($"Customer: {(bill.Customer?.Name ?? "Walk-in")}").FontSize(subFontSize);
        if (bill.CashierUser is not null)
            col.Item().Text($"Cashier: {bill.CashierUser.DisplayName ?? bill.CashierUser.Email}").FontSize(subFontSize);
        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    // ── Items Table ──
    private static void RenderItemsTable(ColumnDescriptor col, PosBill bill, int baseFontSize, Dictionary<string, JsonElement>? props, bool isThermal)
    {
        var showHsn = GetBool(props, "showHsn", false);
        var fs = baseFontSize - 1;

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(4);
                if (showHsn) cols.ConstantColumn(isThermal ? 30 : 45);
                cols.ConstantColumn(isThermal ? 18 : 30);
                cols.ConstantColumn(isThermal ? 35 : 50);
                cols.ConstantColumn(isThermal ? 40 : 55);
            });

            table.Header(header =>
            {
                header.Cell().Text("Item").Bold().FontSize(fs);
                if (showHsn) header.Cell().Text("HSN").Bold().FontSize(fs);
                header.Cell().AlignRight().Text("Qty").Bold().FontSize(fs);
                header.Cell().AlignRight().Text("Rate").Bold().FontSize(fs);
                header.Cell().AlignRight().Text("Amt").Bold().FontSize(fs);
            });

            foreach (var line in bill.Lines)
            {
                var name = line.ItemNameSnapshot ?? line.Item?.Name ?? "–";
                table.Cell().Text(name).FontSize(fs);
                if (showHsn) table.Cell().Text(line.HsnCodeSnapshot ?? "").FontSize(fs);
                table.Cell().AlignRight().Text(line.Qty.ToString()).FontSize(fs);
                table.Cell().AlignRight().Text($"₹{line.UnitPrice:N2}").FontSize(fs);
                table.Cell().AlignRight().Text($"₹{line.LineTotal:N2}").FontSize(fs);
            }
        });
        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    // ── Totals ──
    private static void RenderTotals(ColumnDescriptor col, PosBill bill, Dictionary<string, JsonElement>? props, int baseFontSize)
    {
        void AddRow(ColumnDescriptor c, string label, string value, bool bold = false)
        {
            c.Item().Row(row =>
            {
                var left = row.RelativeItem().AlignLeft().Text(label).FontSize(baseFontSize);
                var right = row.RelativeItem().AlignRight().Text(value).FontSize(baseFontSize);
                if (bold) { left.Bold(); right.Bold(); }
            });
        }

        AddRow(col, "Sub Total", $"₹{bill.SubTotal:N2}");

        var showDiscount = GetBool(props, "showDiscount", true);
        if (showDiscount && bill.DiscountTotal > 0)
            AddRow(col, "Discount", $"- ₹{bill.DiscountTotal:N2}");

        if (bill.TaxTotal > 0)
            AddRow(col, "Tax (GST)", $"+ ₹{bill.TaxTotal:N2}");

        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
        AddRow(col, "GRAND TOTAL", $"₹{bill.GrandTotal:N2}", bold: true);
        col.Item().PaddingVertical(3).LineHorizontal(0.5f);
    }

    // ── Payments ──
    private static void RenderPayments(ColumnDescriptor col, PosBill bill, int subFontSize)
    {
        col.Item().Text("Payments:").Bold().FontSize(subFontSize);
        foreach (var p in bill.Payments.Where(p => !p.IsRefund))
        {
            var label = p.Method.ToString();
            if (!string.IsNullOrEmpty(p.Reference)) label += $" ({p.Reference})";
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(subFontSize);
                row.RelativeItem().AlignRight().Text($"₹{p.Amount:N2}").FontSize(subFontSize);
            });
        }

        var totalPaid = bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount);
        var change = totalPaid - bill.GrandTotal;
        if (change > 0)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Change").Bold().FontSize(subFontSize);
                row.RelativeItem().AlignRight().Text($"₹{change:N2}").Bold().FontSize(subFontSize);
            });
        }
        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    // ── Tax Summary ──
    private static void RenderTaxSummary(ColumnDescriptor col, PosBill bill, int subFontSize)
    {
        if (bill.TaxTotal <= 0) return;
        col.Item().Text("Tax Summary:").Bold().FontSize(subFontSize);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text("GST").FontSize(subFontSize);
            row.RelativeItem().AlignRight().Text($"\u20b9{bill.TaxTotal:N2}").FontSize(subFontSize);
        });
        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    // ── Footer ──
    private static void RenderFooter(ColumnDescriptor col, PosBill bill, Dictionary<string, JsonElement>? props, int subFontSize)
    {
        if (GetBool(props, "showItemCount", true))
            col.Item().AlignCenter().Text($"Items: {bill.Lines.Count} \u2022 {bill.CompletedAtUtc?.ToLocalTime():dd/MM/yyyy HH:mm}").FontSize(subFontSize);

        var text = GetStr(props, "text", "Thank you for shopping!");
        if (!string.IsNullOrEmpty(text))
            col.Item().AlignCenter().Text(text).FontSize(subFontSize);

        col.Item().AlignCenter().Text("\u2014 RetailERP POS \u2014").FontSize(subFontSize > 2 ? subFontSize - 1 : subFontSize);
    }

    // ── Text Block (with dynamic token replacement) ──
    private static void RenderTextBlock(ColumnDescriptor col, PosBill bill, Company company, Dictionary<string, JsonElement>? props)
    {
        var rawText = GetStr(props, "text", "");
        if (string.IsNullOrEmpty(rawText)) return;

        // Resolve dynamic tokens
        rawText = rawText
            .Replace("{{company_name}}", company.Name ?? "")
            .Replace("{{bill_no}}", bill.BillNo ?? "")
            .Replace("{{date}}", bill.BillDate.ToString("dd/MM/yyyy"))
            .Replace("{{grand_total}}", $"₹{bill.GrandTotal:N2}")
            .Replace("{{sub_total}}", $"₹{bill.SubTotal:N2}")
            .Replace("{{tax_total}}", $"₹{bill.TaxTotal:N2}")
            .Replace("{{discount}}", $"₹{bill.DiscountTotal:N2}")
            .Replace("{{customer_name}}", bill.Customer?.Name ?? "Walk-in");

        var fontSize = GetInt(props, "fontSize", 12);
        var bold = GetBool(props, "bold", false);
        var italic = GetBool(props, "italic", false);
        var align = GetStr(props, "align", "center");

        var item = align switch { "left" => col.Item().AlignLeft(), "right" => col.Item().AlignRight(), _ => col.Item().AlignCenter() };

        item.Text(text =>
        {
            var span = text.Span(rawText).FontSize(fontSize);
            if (bold) span.Bold();
            if (italic) span.Italic();
        });
    }

    // ── Separator ──
    private static void RenderSeparator(ColumnDescriptor col, Dictionary<string, JsonElement>? props)
    {
        var thickness = GetInt(props, "thickness", 1);
        col.Item().PaddingVertical(2).LineHorizontal(thickness);
    }

    // ── Spacer ──
    private static void RenderSpacer(ColumnDescriptor col, Dictionary<string, JsonElement>? props)
    {
        var height = GetInt(props, "height", 10);
        col.Item().Height(height);
    }

    // ── Property helpers ──
    private static string GetStr(Dictionary<string, JsonElement>? props, string key, string def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        var val = el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : el.ToString();
        return StripControlChars(val);
    }

    private static string StripControlChars(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Text.RegularExpressions.Regex.Replace(s, @"[\x00-\x09\x0B\x0C\x0E-\x1F]", "").Replace("\r", "").Replace("\n", " ");
    }

    private static int GetInt(Dictionary<string, JsonElement>? props, string key, int def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
        if (int.TryParse(el.ToString(), out var v)) return v;
        return def;
    }

    private static bool GetBool(Dictionary<string, JsonElement>? props, string key, bool def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
        return def;
    }

    public class LayoutComponent
    {
        public string Type { get; set; } = "";
        public Dictionary<string, JsonElement>? Props { get; set; }
    }
}
