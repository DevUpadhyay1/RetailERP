using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Unit = QuestPDF.Infrastructure.Unit;

namespace RetailERP.Services;

/// <summary>
/// Sprint 12: Generates barcode/QR label PDFs for items.
/// Supports thermal label printers (50x25mm, 50x30mm) and A4 sheet layouts.
/// </summary>
public sealed class BarcodeLabelService
{
    /// <summary>Generate a PDF with labels for the given items.</summary>
    public byte[] GenerateLabels(List<LabelItem> items, LabelOptions options)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                if (options.PaperSize == "A4")
                {
                    page.Size(PageSizes.A4);
                    page.Margin(10, Unit.Millimetre);
                    page.Content().Column(col =>
                    {
                        var cols = options.Columns > 0 ? options.Columns : 3;
                        var batches = items.Chunk(cols);
                        foreach (var batch in batches)
                        {
                            col.Item().Row(row =>
                            {
                                foreach (var item in batch)
                                {
                                    row.RelativeItem().Border(0.5f).Padding(3, Unit.Millimetre)
                                        .Column(c => RenderLabel(c, item, options));
                                }
                                for (int pad = batch.Length; pad < cols; pad++)
                                    row.RelativeItem();
                            });
                        }
                    });
                }
                else
                {
                    var w = options.LabelWidthMm > 0 ? options.LabelWidthMm : 50f;
                    var h = options.LabelHeightMm > 0 ? options.LabelHeightMm : 30f;
                    page.Size(w, h, Unit.Millimetre);
                    page.Margin(2, Unit.Millimetre);

                    page.Content().Column(col =>
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (i > 0) col.Item().PageBreak();
                            col.Item().Column(c => RenderLabel(c, items[i], options));
                        }
                    });
                }
            });
        });

        using var stream = new MemoryStream();
        doc.GeneratePdf(stream);
        return stream.ToArray();
    }

    private void RenderLabel(ColumnDescriptor col, LabelItem item, LabelOptions options)
    {
        var fontSize = options.FontSize > 0 ? options.FontSize : 8;

        if (options.ShowName)
        {
            col.Item().AlignCenter().Text(item.Name)
                .FontSize(fontSize).Bold().LineHeight(1.1f);
        }

        if (options.ShowSku)
        {
            col.Item().AlignCenter().Text(item.SKU)
                .FontSize(fontSize - 1).LineHeight(1.1f);
        }

        if (options.ShowBarcode && !string.IsNullOrWhiteSpace(item.Barcode))
        {
            col.Item().AlignCenter().Height(20, Unit.Millimetre)
                .Text(item.Barcode).FontFamily("Courier").FontSize(fontSize);
        }

        if (options.ShowQrCode && !string.IsNullOrWhiteSpace(item.Barcode))
        {
            try
            {
                var qrData = GenerateQrPng(item.Barcode, 150);
                col.Item().AlignCenter().Width(18, Unit.Millimetre).Height(18, Unit.Millimetre)
                    .Image(qrData);
            }
            catch { }
        }

        if (options.ShowPrice)
        {
            var priceText = item.MRP.HasValue
                ? $"MRP ₹{item.MRP.Value:N2}"
                : $"₹{item.UnitPrice:N2}";
            col.Item().AlignCenter().Text(priceText)
                .FontSize(fontSize + 1).Bold();
        }

        if (options.ShowExpiry && item.ExpiryDate.HasValue)
        {
            col.Item().AlignCenter().Text($"Exp: {item.ExpiryDate.Value:MMM yyyy}")
                .FontSize(fontSize - 1);
        }

        if (!string.IsNullOrWhiteSpace(item.Barcode))
        {
            col.Item().AlignCenter().Text(item.Barcode)
                .FontFamily("Courier").FontSize(fontSize - 1);
        }
    }

    public byte[] GenerateQrPng(string data, int pixelsPerModule = 10)
    {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public sealed class LabelItem
    {
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? MRP { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int Copies { get; set; } = 1;
    }

    public sealed class LabelOptions
    {
        public string PaperSize { get; set; } = "Thermal";
        public float LabelWidthMm { get; set; } = 50;
        public float LabelHeightMm { get; set; } = 30;
        public int Columns { get; set; } = 3;
        public int FontSize { get; set; } = 8;
        public bool ShowName { get; set; } = true;
        public bool ShowSku { get; set; } = true;
        public bool ShowBarcode { get; set; } = true;
        public bool ShowQrCode { get; set; } = true;
        public bool ShowPrice { get; set; } = true;
        public bool ShowExpiry { get; set; }
    }
}
