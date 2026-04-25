using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RetailERP.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace RetailERP.Services;

/// <summary>Sprint 6+ – Generates invoice PDF using BillTemplate component layout + Invoice data.</summary>
public class InvoicePdfService
{
    private readonly IWebHostEnvironment _env;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public InvoicePdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] Generate(Invoice invoice, BillTemplate template, Company company)
    {
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        var parsedComponents = JsonSerializer.Deserialize<List<LayoutComponent>>(template.LayoutJson, JsonOpts);
        var components = NormalizeLayoutComponents(parsedComponents);

        var pageWidth = template.PaperSize switch
        {
            "Thermal58mm" => 58f,
            "Thermal80mm" => 80f,
            "A4" => 210f,
            "A5" => 148f,
            _ => 210f
        };

        var isThermal = template.PaperSize.StartsWith("Thermal", StringComparison.OrdinalIgnoreCase);
        var baseFontSize = isThermal ? 8 : 11;
        var headerFontSize = isThermal ? 10 : 14;
        var subFontSize = isThermal ? 7 : 9;
        var totals = InvoiceTotals.From(invoice);

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
                    page.Size(template.PaperSize == "A5" ? PageSizes.A5 : PageSizes.A4);
                    page.Margin(15, PdfUnit.Millimetre);
                }

                page.DefaultTextStyle(x => x.FontSize(baseFontSize));

                page.Content().Column(col =>
                {
                    foreach (var comp in components)
                    {
                        if (!ShouldRenderComponent(comp.Props, invoice, company))
                            continue;

                        var marginTop = Math.Clamp(GetInt(comp.Props, "marginTop", 0), 0, 80);
                        var marginBottom = Math.Clamp(GetInt(comp.Props, "marginBottom", 0), 0, 80);
                        if (marginTop > 0) col.Item().Height(marginTop);

                        switch (comp.Type)
                        {
                            case "logo":
                                RenderLogo(col, company, template, comp.Props, isThermal);
                                break;
                            case "header_row":
                                RenderHeaderRow(col, company, template, comp.Props, isThermal);
                                break;
                            case "store_header":
                                RenderStoreHeader(col, invoice, company, template, comp.Props, headerFontSize, subFontSize);
                                break;
                            case "social_row":
                                RenderSocialRow(col, company, comp.Props, subFontSize);
                                break;
                            case "bill_info":
                                RenderBillInfo(col, invoice, template, comp.Props, subFontSize);
                                break;
                            case "items_table":
                                RenderItemsTable(col, invoice, template, comp.Props, baseFontSize, isThermal);
                                break;
                            case "totals":
                                RenderTotals(col, totals, comp.Props, baseFontSize, template.AccentColor);
                                break;
                            case "payments":
                                RenderPayments(col, invoice, subFontSize);
                                break;
                            case "tax_summary":
                                RenderTaxSummary(col, invoice, subFontSize);
                                break;
                            case "footer":
                                RenderFooter(col, invoice, template, company, comp.Props, subFontSize, totals);
                                break;
                            case "text_block":
                                RenderTextBlock(col, invoice, template, company, comp.Props, totals);
                                break;
                            case "separator":
                                RenderSeparator(col, comp.Props);
                                break;
                            case "spacer":
                                RenderSpacer(col, comp.Props);
                                break;
                        }

                        if (marginBottom > 0) col.Item().Height(marginBottom);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private void RenderLogo(ColumnDescriptor col, Company company, BillTemplate template, Dictionary<string, JsonElement>? props, bool isThermal)
    {
        if (!template.ShowLogo || string.IsNullOrWhiteSpace(company.LogoPath)) return;

        var fullPath = Path.Combine(_env.WebRootPath, company.LogoPath.TrimStart('/'));
        if (!File.Exists(fullPath)) return;

        var maxH = GetInt(props, "maxHeight", isThermal ? 28 : 50);
        var align = GetStr(props, "align", "center");

        var item = col.Item().Height(maxH);
        item = align switch
        {
            "left" => item.AlignLeft(),
            "right" => item.AlignRight(),
            _ => item.AlignCenter()
        };

        item.Image(fullPath).FitHeight();
        col.Item().PaddingBottom(2);
    }

    private void RenderHeaderRow(ColumnDescriptor col, Company company, BillTemplate template, Dictionary<string, JsonElement>? props, bool isThermal)
    {
        var leftText = GetStrMultiline(props, "leftText", "");
        var rightText = GetStrMultiline(props, "rightText", "");
        var centerMode = GetStr(props, "centerMode", "logo");
        var centerText = GetStrMultiline(props, "centerText", "");
        var fontSize = Math.Clamp(GetInt(props, "fontSize", isThermal ? 8 : 10), 7, 24);
        var logoHeight = Math.Clamp(GetInt(props, "logoHeight", isThermal ? 24 : 30), 12, 80);
        var showSeparator = GetBool(props, "showSeparator", false);

        col.Item().Row(row =>
        {
            row.RelativeItem().AlignLeft().Text(leftText).FontSize(fontSize);

            if (centerMode.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                row.RelativeItem().AlignCenter().Text(centerText).FontSize(fontSize);
            }
            else
            {
                var hasLogo = template.ShowLogo && !string.IsNullOrWhiteSpace(company.LogoPath);
                if (hasLogo)
                {
                    var fullPath = Path.Combine(_env.WebRootPath, company.LogoPath!.TrimStart('/'));
                    if (File.Exists(fullPath))
                        row.RelativeItem().AlignCenter().Height(logoHeight).Image(fullPath).FitHeight();
                    else
                        row.RelativeItem().AlignCenter().Text(company.Name).Bold().FontSize(fontSize);
                }
                else
                {
                    row.RelativeItem().AlignCenter().Text(company.Name).Bold().FontSize(fontSize);
                }
            }

            row.RelativeItem().AlignRight().Text(rightText).FontSize(fontSize);
        });

        if (showSeparator)
            col.Item().PaddingTop(2).LineHorizontal(0.5f);
    }

    private static void RenderStoreHeader(ColumnDescriptor col, Invoice invoice, Company company, BillTemplate template, Dictionary<string, JsonElement>? props, int headerFontSize, int subFontSize)
    {
        var showAddress = GetBool(props, "showAddress", true);
        var showPhone = GetBool(props, "showPhone", true);
        var showTitle = GetBool(props, "showTitle", true);
        var showSeparator = GetBool(props, "showSeparator", true);

        var section = ApplySectionStyle(col.Item(), props);
        section.Column(body =>
        {
            body.Item().AlignCenter().Text(company.Name).Bold().FontSize(headerFontSize);

            if (showAddress && !string.IsNullOrWhiteSpace(company.Address))
                body.Item().AlignCenter().Text(company.Address).FontSize(subFontSize);

            if (showPhone && !string.IsNullOrWhiteSpace(company.Phone))
                body.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(subFontSize);

            if (template.ShowGst && GetBool(props, "showGst", true) && !string.IsNullOrWhiteSpace(company.GstNo))
                body.Item().AlignCenter().Text($"GSTIN: {company.GstNo}").FontSize(subFontSize);

            var title = GetStr(props, "headerText", "");
            if (string.IsNullOrWhiteSpace(title))
                title = GetDocumentTitle(invoice.DocumentType);

            if (showTitle)
                body.Item().PaddingTop(2).AlignCenter().Text(title).Bold().FontSize(subFontSize + 1);

            if (!string.IsNullOrWhiteSpace(template.HeaderText))
                body.Item().AlignCenter().Text(template.HeaderText).FontSize(subFontSize);

            if (showSeparator)
                body.Item().PaddingVertical(2).LineHorizontal(0.5f);
        });
    }

    private static void RenderSocialRow(ColumnDescriptor col, Company company, Dictionary<string, JsonElement>? props, int fallbackFontSize)
    {
        var parts = new List<string>();

        var instagram = GetStr(props, "instagram", "");
        var whatsapp = GetStr(props, "whatsapp", "");
        var facebook = GetStr(props, "facebook", "");
        var xhandle = GetStr(props, "xhandle", "");
        var youtube = GetStr(props, "youtube", "");
        var phone = GetStr(props, "phone", company.Phone ?? "");
        var email = GetStr(props, "email", company.Email ?? "");
        var website = GetStr(props, "website", company.Website ?? "");

        if (!string.IsNullOrWhiteSpace(instagram)) parts.Add($"IG: {instagram}");
        if (!string.IsNullOrWhiteSpace(whatsapp)) parts.Add($"WA: {whatsapp}");
        if (!string.IsNullOrWhiteSpace(facebook)) parts.Add($"FB: {facebook}");
        if (!string.IsNullOrWhiteSpace(xhandle)) parts.Add($"X: {xhandle}");
        if (!string.IsNullOrWhiteSpace(youtube)) parts.Add($"YT: {youtube}");
        if (!string.IsNullOrWhiteSpace(phone)) parts.Add($"Ph: {phone}");
        if (!string.IsNullOrWhiteSpace(email)) parts.Add($"Email: {email}");
        if (!string.IsNullOrWhiteSpace(website)) parts.Add($"Web: {website}");

        if (parts.Count == 0) return;

        var sep = GetStr(props, "separator", " | ");
        var align = GetStr(props, "align", "center");
        var fontSize = Math.Clamp(GetInt(props, "fontSize", fallbackFontSize), 7, 20);
        var line = string.Join(sep, parts);

        var item = align switch
        {
            "left" => col.Item().AlignLeft(),
            "right" => col.Item().AlignRight(),
            _ => col.Item().AlignCenter()
        };

        item.Text(line).FontSize(fontSize);
    }

    private static void RenderBillInfo(ColumnDescriptor col, Invoice invoice, BillTemplate template, Dictionary<string, JsonElement>? props, int subFontSize)
    {
        var showInvoiceNo = GetBool(props, "showInvoiceNo", true);
        var showInvoiceDate = GetBool(props, "showInvoiceDate", true);
        var showDocumentType = GetBool(props, "showDocumentType", true);
        var showDueDate = GetBool(props, "showDueDate", true);
        var showReferenceInvoice = GetBool(props, "showReferenceInvoice", true);
        var showCustomerName = GetBool(props, "showCustomerName", true);
        var showCustomerPhone = GetBool(props, "showCustomerPhone", template.ShowPhoneOnInvoice);
        var showCustomerEmail = GetBool(props, "showCustomerEmail", false);
        var showWarehouse = GetBool(props, "showWarehouse", true);
        var showCashier = GetBool(props, "showCashier", false);
        var showSeparator = GetBool(props, "showSeparator", true);

        var section = ApplySectionStyle(col.Item(), props);
        section.Column(body =>
        {
            if (showInvoiceNo || showInvoiceDate)
            {
                body.Item().Row(row =>
                {
                    if (showInvoiceNo)
                        row.RelativeItem().Text($"Invoice #: {invoice.InvoiceNo}").Bold().FontSize(subFontSize);
                    else
                        row.RelativeItem();

                    if (showInvoiceDate)
                        row.RelativeItem().AlignRight().Text(invoice.InvoiceDate.ToString("dd/MM/yyyy")).FontSize(subFontSize);
                    else
                        row.RelativeItem();
                });
            }

            if (showDocumentType)
                body.Item().Text($"Document: {GetDocumentTitle(invoice.DocumentType)}").FontSize(subFontSize);

            if (showDueDate && invoice.DueDate.HasValue)
                body.Item().Text($"Due Date: {invoice.DueDate:dd/MM/yyyy}").FontSize(subFontSize);

            if (showReferenceInvoice && !string.IsNullOrWhiteSpace(invoice.ReferenceInvoiceNo))
                body.Item().Text($"Ref Invoice: {invoice.ReferenceInvoiceNo}").FontSize(subFontSize);

            if (invoice.Customer is not null)
            {
                if (showCustomerName)
                    body.Item().PaddingTop(2).Text($"Party: {invoice.Customer.Name}").FontSize(subFontSize);
                if (showCustomerPhone && !string.IsNullOrWhiteSpace(invoice.Customer.Phone))
                    body.Item().Text($"Phone: {invoice.Customer.Phone}").FontSize(subFontSize);
                if (showCustomerEmail && !string.IsNullOrWhiteSpace(invoice.Customer.Email))
                    body.Item().Text($"Email: {invoice.Customer.Email}").FontSize(subFontSize);
            }

            if (showWarehouse && invoice.Warehouse is not null)
                body.Item().Text($"Warehouse: {invoice.Warehouse.Name}").FontSize(subFontSize);

            if (showCashier)
                body.Item().Text("Prepared by: System").FontSize(subFontSize);

            if (showSeparator)
                body.Item().PaddingVertical(2).LineHorizontal(0.5f);
        });
    }

    private static void RenderItemsTable(ColumnDescriptor col, Invoice invoice, BillTemplate template, Dictionary<string, JsonElement>? props, int baseFontSize, bool isThermal)
    {
        var showSerial = GetBool(props, "showSerial", true);
        var showItem = true;
        var showSku = GetBool(props, "showSku", false);
        var showHsn = GetBool(props, "showHsn", false);
        var showQty = GetBool(props, "showQty", true);
        var showRate = GetBool(props, "showRate", true);
        var showDiscount = GetBool(props, "showDiscountColumn", false);
        var showTaxPercent = GetBool(props, "showTaxPercent", true);
        var showTaxAmount = GetBool(props, "showTaxAmount", false);
        var showAmount = GetBool(props, "showAmount", true);
        var showDescriptionRow = GetBool(props, "showDescriptionRow", template.ShowItemDescription);

        var labelSerial = GetStr(props, "labelSerial", "#");
        var labelItem = GetStr(props, "labelItem", "Item");
        var labelSku = GetStr(props, "labelSku", "SKU");
        var labelHsn = GetStr(props, "labelHsn", "HSN");
        var labelQty = GetStr(props, "labelQty", "Qty");
        var labelRate = GetStr(props, "labelRate", "Rate");
        var labelDiscount = GetStr(props, "labelDiscount", "Disc");
        var labelTaxPercent = GetStr(props, "labelTaxPercent", "Tax%");
        var labelTaxAmount = GetStr(props, "labelTaxAmount", "Tax Amt");
        var labelAmount = GetStr(props, "labelAmount", "Amt");

        var widthSerial = Math.Clamp(GetInt(props, "widthSerial", isThermal ? 14 : 20), 8, 90);
        var widthItem = Math.Clamp(GetInt(props, "widthItem", 4), 1, 12);
        var widthSku = Math.Clamp(GetInt(props, "widthSku", isThermal ? 22 : 36), 12, 130);
        var widthHsn = Math.Clamp(GetInt(props, "widthHsn", isThermal ? 24 : 40), 14, 130);
        var widthQty = Math.Clamp(GetInt(props, "widthQty", isThermal ? 20 : 32), 14, 130);
        var widthRate = Math.Clamp(GetInt(props, "widthRate", isThermal ? 34 : 55), 20, 180);
        var widthDiscount = Math.Clamp(GetInt(props, "widthDiscount", isThermal ? 30 : 48), 20, 180);
        var widthTaxPercent = Math.Clamp(GetInt(props, "widthTaxPercent", isThermal ? 28 : 45), 18, 160);
        var widthTaxAmount = Math.Clamp(GetInt(props, "widthTaxAmount", isThermal ? 32 : 52), 20, 180);
        var widthAmount = Math.Clamp(GetInt(props, "widthAmount", isThermal ? 40 : 65), 24, 220);

        var showHeaderBackground = GetBool(props, "showHeaderBackground", true);
        var showGrid = GetBool(props, "showGrid", false);
        var zebraRows = GetBool(props, "zebraRows", false);
        var headerBg = GetStr(props, "headerBgColor", "#f2f2f2");
        var headerTextColor = GetStr(props, "headerTextColor", "#111111");
        var gridColor = GetStr(props, "gridColor", "#d9d9d9");
        var zebraColor = GetStr(props, "zebraColor", "#fafafa");
        var showBottomSeparator = GetBool(props, "showBottomSeparator", true);

        var fs = Math.Max(6, baseFontSize - 1);
        var section = ApplySectionStyle(col.Item(), props);

        section.Column(body =>
        {
            body.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    if (showSerial) cols.ConstantColumn(widthSerial);
                    if (showItem) cols.RelativeColumn(widthItem);
                    if (showSku) cols.ConstantColumn(widthSku);
                    if (showHsn) cols.ConstantColumn(widthHsn);
                    if (showQty) cols.ConstantColumn(widthQty);
                    if (showRate) cols.ConstantColumn(widthRate);
                    if (showDiscount) cols.ConstantColumn(widthDiscount);
                    if (showTaxPercent) cols.ConstantColumn(widthTaxPercent);
                    if (showTaxAmount) cols.ConstantColumn(widthTaxAmount);
                    if (showAmount) cols.ConstantColumn(widthAmount);
                });

                table.Header(header =>
                {
                    void HeaderCell(string text, bool right = false)
                    {
                        var cell = header.Cell().PaddingVertical(2).PaddingHorizontal(2);
                        if (showHeaderBackground)
                            cell = cell.Background(headerBg);
                        if (showGrid)
                            cell = cell.Border(0.5f).BorderColor(gridColor);

                        var entry = right
                            ? cell.AlignRight().Text(text).Bold().FontSize(fs)
                            : cell.Text(text).Bold().FontSize(fs);

                        entry.FontColor(headerTextColor);
                    }

                    if (showSerial) HeaderCell(labelSerial, true);
                    if (showItem) HeaderCell(labelItem);
                    if (showSku) HeaderCell(labelSku);
                    if (showHsn) HeaderCell(labelHsn);
                    if (showQty) HeaderCell(labelQty, true);
                    if (showRate) HeaderCell(labelRate, true);
                    if (showDiscount) HeaderCell(labelDiscount, true);
                    if (showTaxPercent) HeaderCell(labelTaxPercent, true);
                    if (showTaxAmount) HeaderCell(labelTaxAmount, true);
                    if (showAmount) HeaderCell(labelAmount, true);
                });

                var index = 1;
                foreach (var line in invoice.Lines)
                {
                    var itemName = line.ItemNameSnapshot ?? line.Item?.Name ?? "-";
                    var sku = line.ItemSkuSnapshot ?? line.Item?.SKU ?? "";
                    var hsn = line.HsnCodeSnapshot ?? "";
                    var lineBase = line.Qty * line.UnitPrice;
                    var discount = line.DiscountAmount;
                    var taxable = lineBase - discount;
                    var gst = line.GstPercentSnapshot ?? 0m;
                    var taxAmount = taxable * gst / 100m;
                    var lineTotal = taxable + taxAmount;
                    var rowBg = zebraRows && index % 2 == 0 ? zebraColor : null;

                    void BodyCell(string text, bool right = false, string? color = null)
                    {
                        var cell = table.Cell().PaddingVertical(1).PaddingHorizontal(2);
                        if (!string.IsNullOrWhiteSpace(rowBg))
                            cell = cell.Background(rowBg);
                        if (showGrid)
                            cell = cell.Border(0.5f).BorderColor(gridColor);

                        var entry = right
                            ? cell.AlignRight().Text(text).FontSize(fs)
                            : cell.Text(text).FontSize(fs);

                        if (!string.IsNullOrWhiteSpace(color))
                            entry.FontColor(color);
                    }

                    if (showSerial) BodyCell(index.ToString(), true);
                    if (showItem) BodyCell(itemName);
                    if (showSku) BodyCell(sku);
                    if (showHsn) BodyCell(hsn);
                    if (showQty) BodyCell(line.Qty.ToString("0.##"), true);
                    if (showRate) BodyCell($"₹{line.UnitPrice:N2}", true);
                    if (showDiscount) BodyCell($"₹{discount:N2}", true);
                    if (showTaxPercent) BodyCell($"{gst:0.##}%", true);
                    if (showTaxAmount) BodyCell($"₹{taxAmount:N2}", true);
                    if (showAmount) BodyCell($"₹{lineTotal:N2}", true);

                    if (showDescriptionRow && !showSku && !string.IsNullOrWhiteSpace(sku))
                    {
                        if (showSerial) BodyCell("", true);
                        if (showItem) BodyCell(sku, false, "#666666");
                        if (showHsn) BodyCell("");
                        if (showQty) BodyCell("", true);
                        if (showRate) BodyCell("", true);
                        if (showDiscount) BodyCell("", true);
                        if (showTaxPercent) BodyCell("", true);
                        if (showTaxAmount) BodyCell("", true);
                        if (showAmount) BodyCell("", true);
                    }

                    index++;
                }
            });

            if (showBottomSeparator)
                body.Item().PaddingVertical(2).LineHorizontal(0.5f);
        });
    }

    private static void RenderTotals(ColumnDescriptor col, InvoiceTotals totals, Dictionary<string, JsonElement>? props, int baseFontSize, string accentColor)
    {
        void AddRow(ColumnDescriptor column, string label, string value, bool bold = false)
        {
            column.Item().Row(row =>
            {
                var left = row.RelativeItem().AlignLeft().Text(label).FontSize(baseFontSize);
                var right = row.RelativeItem().AlignRight().Text(value).FontSize(baseFontSize);
                if (bold)
                {
                    left.Bold();
                    right.Bold();
                }
            });
        }

        var showDiscount = GetBool(props, "showDiscount", true);

        AddRow(col, "Sub Total", $"₹{totals.SubTotal:N2}");
        if (showDiscount && totals.Discount > 0)
            AddRow(col, "Discount", $"- ₹{totals.Discount:N2}");
        if (totals.Tax > 0)
            AddRow(col, "Tax (GST)", $"+ ₹{totals.Tax:N2}");

        col.Item().PaddingVertical(2).LineHorizontal(0.5f);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text("GRAND TOTAL").Bold().FontSize(baseFontSize + 1).FontColor(accentColor);
            row.RelativeItem().AlignRight().Text($"₹{totals.GrandTotal:N2}").Bold().FontSize(baseFontSize + 1).FontColor(accentColor);
        });

        col.Item().PaddingVertical(3).LineHorizontal(0.5f);
    }

    private static void RenderPayments(ColumnDescriptor col, Invoice invoice, int subFontSize)
    {
        var statusText = invoice.Status == 2 ? "Posted" : "Draft / Unpaid";
        col.Item().Text("Payment Summary:").Bold().FontSize(subFontSize);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text("Status").FontSize(subFontSize);
            row.RelativeItem().AlignRight().Text(statusText).FontSize(subFontSize);
        });

        if (invoice.DueDate.HasValue)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Due Date").FontSize(subFontSize);
                row.RelativeItem().AlignRight().Text(invoice.DueDate.Value.ToString("dd/MM/yyyy")).FontSize(subFontSize);
            });
        }

        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private static void RenderTaxSummary(ColumnDescriptor col, Invoice invoice, int subFontSize)
    {
        var groups = invoice.Lines
            .GroupBy(l => l.GstPercentSnapshot ?? 0m)
            .Select(g => new
            {
                GstPercent = g.Key,
                Tax = g.Sum(x => (x.Qty * x.UnitPrice - x.DiscountAmount) * (x.GstPercentSnapshot ?? 0m) / 100m)
            })
            .Where(x => x.Tax > 0)
            .OrderBy(x => x.GstPercent)
            .ToList();

        if (groups.Count == 0) return;

        col.Item().Text("Tax Summary:").Bold().FontSize(subFontSize);
        foreach (var g in groups)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"GST {g.GstPercent:0.##}%").FontSize(subFontSize);
                row.RelativeItem().AlignRight().Text($"₹{g.Tax:N2}").FontSize(subFontSize);
            });
        }

        col.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private void RenderFooter(ColumnDescriptor col, Invoice invoice, BillTemplate template, Company company, Dictionary<string, JsonElement>? props, int subFontSize, InvoiceTotals totals)
    {
        var showItemCount = GetBool(props, "showItemCount", true);
        var showTotalInWords = GetBool(props, "showTotalInWords", true);
        var showComputerGenerated = GetBool(props, "showComputerGenerated", true);

        var section = ApplySectionStyle(col.Item(), props);
        section.Column(body =>
        {
            if (showItemCount)
                body.Item().AlignCenter().Text($"Items: {invoice.Lines.Count} • {invoice.InvoiceDate:dd/MM/yyyy}").FontSize(subFontSize);

            var text = GetStr(props, "text", template.FooterText ?? "Thank you for your business!");
            if (!string.IsNullOrWhiteSpace(text))
                body.Item().AlignCenter().Text(text).FontSize(subFontSize);

            if (template.ShowSignature || template.ShowStamp)
            {
                body.Item().PaddingTop(4).Row(row =>
                {
                    if (template.ShowStamp)
                    {
                        row.RelativeItem().AlignLeft().Column(c =>
                        {
                            c.Item().Text("Stamp").FontSize(subFontSize);
                            if (!string.IsNullOrWhiteSpace(company.StampPath))
                            {
                                var stampFull = Path.Combine(_env.WebRootPath, company.StampPath.TrimStart('/'));
                                if (File.Exists(stampFull))
                                    c.Item().Height(40).Image(stampFull).FitArea();
                            }
                        });
                    }
                    else
                    {
                        row.RelativeItem();
                    }

                    if (template.ShowSignature)
                    {
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("Authorised Signatory").FontSize(subFontSize);
                            if (!string.IsNullOrWhiteSpace(company.SignaturePath))
                            {
                                var signFull = Path.Combine(_env.WebRootPath, company.SignaturePath.TrimStart('/'));
                                if (File.Exists(signFull))
                                    c.Item().AlignRight().Height(40).Image(signFull).FitArea();
                            }
                        });
                    }
                    else
                    {
                        row.RelativeItem();
                    }
                });
            }

            if (template.ShowPartyBalance)
                body.Item().AlignRight().Text("Party Balance: As per ledger").FontSize(subFontSize);

            if (showTotalInWords)
                body.Item().AlignCenter().Text($"Total in words: {ToIndianCurrencyWords(totals.GrandTotal)}").FontSize(subFontSize);

            if (showComputerGenerated)
                body.Item().AlignCenter().Text("Computer generated invoice").FontSize(subFontSize > 2 ? subFontSize - 1 : subFontSize).FontColor("#999999");
        });
    }

    private static void RenderTextBlock(ColumnDescriptor col, Invoice invoice, BillTemplate template, Company company, Dictionary<string, JsonElement>? props, InvoiceTotals totals)
    {
        var rawText = GetStrMultiline(props, "text", "");
        if (string.IsNullOrWhiteSpace(rawText)) return;

        rawText = rawText
            .Replace("{{company_name}}", company.Name ?? "")
            .Replace("{{invoice_no}}", invoice.InvoiceNo ?? "")
            .Replace("{{bill_no}}", invoice.InvoiceNo ?? "")
            .Replace("{{invoice_date}}", invoice.InvoiceDate.ToString("dd/MM/yyyy"))
            .Replace("{{date}}", invoice.InvoiceDate.ToString("dd/MM/yyyy"))
            .Replace("{{due_date}}", invoice.DueDate?.ToString("dd/MM/yyyy") ?? "")
            .Replace("{{document_type}}", GetDocumentTitle(invoice.DocumentType))
            .Replace("{{grand_total}}", $"₹{totals.GrandTotal:N2}")
            .Replace("{{sub_total}}", $"₹{totals.SubTotal:N2}")
            .Replace("{{tax_total}}", $"₹{totals.Tax:N2}")
            .Replace("{{discount}}", $"₹{totals.Discount:N2}")
            .Replace("{{grand_total_words}}", ToIndianCurrencyWords(totals.GrandTotal))
            .Replace("{{invoice_status}}", invoice.Status == 2 ? "Posted" : "Draft")
            .Replace("{{customer_name}}", invoice.Customer?.Name ?? "Walk-in")
            .Replace("{{customer_phone}}", invoice.Customer?.Phone ?? "")
            .Replace("{{customer_email}}", invoice.Customer?.Email ?? "");

        var fontSize = GetInt(props, "fontSize", 12);
        var fontFamily = MapFontFamily(GetStr(props, "fontFamily", "sans-serif"));
        var bold = GetBool(props, "bold", false);
        var italic = GetBool(props, "italic", false);
        var align = GetStr(props, "align", "center");
        var color = GetStr(props, "color", "#000000");

        var section = ApplySectionStyle(col.Item(), props);
        var item = align switch
        {
            "left" => section.AlignLeft(),
            "right" => section.AlignRight(),
            _ => section.AlignCenter()
        };

        item.Text(text =>
        {
            var span = text.Span(rawText).FontSize(fontSize).FontColor(color).FontFamily(fontFamily);
            if (bold) span.Bold();
            if (italic) span.Italic();
        });
    }

    private static void RenderSeparator(ColumnDescriptor col, Dictionary<string, JsonElement>? props)
    {
        var thickness = Math.Clamp(GetInt(props, "thickness", 1), 1, 5);
        col.Item().PaddingVertical(2).LineHorizontal(thickness);
    }

    private static void RenderSpacer(ColumnDescriptor col, Dictionary<string, JsonElement>? props)
    {
        var height = Math.Clamp(GetInt(props, "height", 10), 1, 120);
        col.Item().Height(height);
    }

    private static List<LayoutComponent> GetDefaultLayout() =>
    [
        new() { Type = "logo" },
        new() { Type = "store_header" },
        new() { Type = "bill_info" },
        new() { Type = "items_table" },
        new() { Type = "totals" },
        new() { Type = "tax_summary" },
        new() { Type = "footer" }
    ];

    private static List<LayoutComponent> NormalizeLayoutComponents(List<LayoutComponent>? parsed)
    {
        if (parsed is null || parsed.Count == 0)
            return GetDefaultLayout();

        var knownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "logo",
            "header_row",
            "store_header",
            "social_row",
            "bill_info",
            "items_table",
            "totals",
            "payments",
            "tax_summary",
            "footer",
            "text_block",
            "separator",
            "spacer"
        };

        var normalized = parsed
            .Select(c => new LayoutComponent
            {
                Type = NormalizeComponentType(c.Type),
                Props = c.Props
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Type) && knownTypes.Contains(c.Type))
            .ToList();

        if (normalized.Count == 0)
            return GetDefaultLayout();

        var meaningful = normalized.Where(c => c.Type is not "separator" and not "spacer").ToList();
        if (meaningful.Count == 0)
            return GetDefaultLayout();

        // Guard against corrupted layouts that technically parse but miss core invoice sections.
        var hasHeader = meaningful.Any(c => c.Type is "store_header" or "header_row");
        var hasItems = meaningful.Any(c => c.Type == "items_table");
        var hasTotals = meaningful.Any(c => c.Type == "totals");

        if (!hasHeader || !hasItems || !hasTotals)
            return GetDefaultLayout();

        return normalized;
    }

    private static string NormalizeComponentType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return string.Empty;

        var t = type.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return t switch
        {
            "storeheader" => "store_header",
            "headerrow" => "header_row",
            "billinfo" => "bill_info",
            "itemstable" => "items_table",
            "taxsummary" => "tax_summary",
            "textblock" => "text_block",
            _ => t
        };
    }

    private static string GetDocumentTitle(InvoiceDocumentType type) =>
        type switch
        {
            InvoiceDocumentType.TaxInvoice => "TAX INVOICE",
            InvoiceDocumentType.BillOfSupply => "BILL OF SUPPLY",
            InvoiceDocumentType.CreditNote => "CREDIT NOTE",
            InvoiceDocumentType.DebitNote => "DEBIT NOTE",
            InvoiceDocumentType.ProformaInvoice => "PROFORMA INVOICE",
            _ => "INVOICE"
        };

    private sealed class InvoiceTotals
    {
        public decimal SubTotal { get; init; }
        public decimal Discount { get; init; }
        public decimal Tax { get; init; }
        public decimal GrandTotal { get; init; }

        public static InvoiceTotals From(Invoice invoice)
        {
            var subTotal = invoice.Lines.Sum(l => l.Qty * l.UnitPrice);
            var discount = invoice.Lines.Sum(l => l.DiscountAmount);
            var tax = invoice.Lines.Sum(l => (l.Qty * l.UnitPrice - l.DiscountAmount) * (l.GstPercentSnapshot ?? 0m) / 100m);
            var computedGrand = subTotal - discount + tax;
            var grand = invoice.TotalAmount > 0 ? invoice.TotalAmount : computedGrand;

            return new InvoiceTotals
            {
                SubTotal = subTotal,
                Discount = discount,
                Tax = tax,
                GrandTotal = grand
            };
        }
    }

    public sealed class LayoutComponent
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, JsonElement>? Props { get; set; }
    }

    private static bool ShouldRenderComponent(Dictionary<string, JsonElement>? props, Invoice invoice, Company company)
    {
        var mode = GetStr(props, "visibleWhen", "always").Trim().ToLowerInvariant();
        return mode switch
        {
            "" or "always" => true,
            "posted_only" => invoice.Status == 2,
            "draft_only" => invoice.Status != 2,
            "has_due_date" => invoice.DueDate.HasValue,
            "has_reference" => !string.IsNullOrWhiteSpace(invoice.ReferenceInvoiceNo),
            "has_customer_phone" => !string.IsNullOrWhiteSpace(invoice.Customer?.Phone),
            "has_customer_email" => !string.IsNullOrWhiteSpace(invoice.Customer?.Email),
            "has_gst" => !string.IsNullOrWhiteSpace(company.GstNo),
            _ => true
        };
    }

    private static IContainer ApplySectionStyle(IContainer container, Dictionary<string, JsonElement>? props)
    {
        var padding = Math.Clamp(GetInt(props, "padding", 0), 0, 40);
        var borderWidth = Math.Clamp(GetInt(props, "borderWidth", 0), 0, 8);
        var borderColor = GetStr(props, "borderColor", "#d0d0d0");
        var backgroundColor = GetStr(props, "backgroundColor", "");

        if (!string.IsNullOrWhiteSpace(backgroundColor) &&
            !backgroundColor.Equals("transparent", StringComparison.OrdinalIgnoreCase) &&
            !backgroundColor.Equals("#ffffff", StringComparison.OrdinalIgnoreCase))
        {
            container = container.Background(backgroundColor);
        }

        if (borderWidth > 0)
            container = container.Border(borderWidth).BorderColor(borderColor);

        if (padding > 0)
            container = container.Padding(padding);

        return container;
    }

    private static string MapFontFamily(string family) =>
        family.Trim().ToLowerInvariant() switch
        {
            "serif" => "Times New Roman",
            "monospace" => "Consolas",
            "sans-serif" => "Arial",
            _ => family
        };

    private static string GetStr(Dictionary<string, JsonElement>? props, string key, string def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        var val = el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : el.ToString();
        return StripControlChars(val);
    }

    private static string GetStrMultiline(Dictionary<string, JsonElement>? props, string key, string def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        var val = el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : el.ToString();
        if (string.IsNullOrEmpty(val)) return val;

        val = val.Replace("\r\n", "\n").Replace("\r", "\n");
        return System.Text.RegularExpressions.Regex.Replace(val, @"[\x00-\x09\x0B\x0C\x0E-\x1F]", "");
    }

    private static string StripControlChars(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Text.RegularExpressions.Regex.Replace(s, @"[\x00-\x09\x0B\x0C\x0E-\x1F]", "")
            .Replace("\r", "")
            .Replace("\n", " ");
    }

    private static int GetInt(Dictionary<string, JsonElement>? props, string key, int def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        return int.TryParse(el.ToString(), out var v) ? v : def;
    }

    private static bool GetBool(Dictionary<string, JsonElement>? props, string key, bool def)
    {
        if (props is null || !props.TryGetValue(key, out var el)) return def;
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
        return bool.TryParse(el.ToString(), out var v) ? v : def;
    }

    private static string ToIndianCurrencyWords(decimal amount)
    {
        var whole = (long)Math.Floor(Math.Abs(amount));
        var paise = (int)Math.Round((Math.Abs(amount) - whole) * 100m, MidpointRounding.AwayFromZero);
        if (paise == 100) { whole++; paise = 0; }

        var words = whole == 0 ? "Zero" : NumberToWordsIndian(whole);
        if (paise > 0)
            return $"{words} Rupees and {NumberToWordsIndian(paise)} Paise Only";
        return $"{words} Rupees Only";
    }

    private static string NumberToWordsIndian(long number)
    {
        if (number == 0) return "Zero";

        string[] ones =
        {
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };
        string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        string ConvertBelow1000(int n)
        {
            var parts = new List<string>();
            if (n >= 100)
            {
                parts.Add(ones[n / 100]);
                parts.Add("Hundred");
                n %= 100;
            }

            if (n >= 20)
            {
                parts.Add(tens[n / 10]);
                if (n % 10 > 0) parts.Add(ones[n % 10]);
            }
            else if (n > 0)
            {
                parts.Add(ones[n]);
            }

            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        var result = new List<string>();
        var crore = number / 10000000;
        number %= 10000000;
        var lakh = number / 100000;
        number %= 100000;
        var thousand = number / 1000;
        number %= 1000;
        var rest = number;

        if (crore > 0) result.Add($"{ConvertBelow1000((int)crore)} Crore");
        if (lakh > 0) result.Add($"{ConvertBelow1000((int)lakh)} Lakh");
        if (thousand > 0) result.Add($"{ConvertBelow1000((int)thousand)} Thousand");
        if (rest > 0) result.Add(ConvertBelow1000((int)rest));

        return string.Join(" ", result.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }
}
