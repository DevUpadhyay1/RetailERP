using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Item onboarding utilities:
/// - Bulk CSV import (standard template + supplier-style catalogs)
/// - Starter pack provisioning by business type
/// - Quick-create item during purchase flow
/// </summary>
public sealed class ItemOnboardingService
{
    private readonly ApplicationDbContext _db;

    public ItemOnboardingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public byte[] BuildStandardTemplateCsv()
    {
        const string csv =
            "SKU,Name,UnitPrice,MRP,PurchasePrice,GstPercent,HsnCode,Barcode,ReorderLevel,UnitName,CategoryName,IsActive,OpeningStock,WarehouseName,BatchNumber,ExpiryDate\r\n" +
            "RICE-001,Basmati Rice 5kg,525,549,480,5,100630,8901234567001,10,PCS,Grocery,true,100,Main Warehouse,RICE-APR26,2026-12-31\r\n" +
            "SOAP-010,Bathing Soap 125g,38,40,31,18,340111,8901234567018,40,PCS,Personal Care,true,60,Main Warehouse,SOAP-B1,2027-06-30\r\n";
        return Encoding.UTF8.GetBytes(csv);
    }

    public byte[] BuildSupplierTemplateCsv()
    {
        const string csv =
            "SupplierSKU,ProductName,SalePrice,MRP,CostPrice,GST,HSN,EAN,MinStock,UOM,Category,Active,OpeningStock,WarehouseName,BatchNumber,ExpiryDate\r\n" +
            "HWR-AXE-01,Steel Axe 1kg,799,850,640,18,820130,8901234577001,5,PCS,Hardware,true,12,Main Warehouse,AXE-B1,2028-01-31\r\n" +
            "HWR-DRL-02,Electric Drill Machine,3499,3699,2990,18,846721,8901234577018,2,PCS,Hardware,true,4,Main Warehouse,DRL-B2,2028-12-31\r\n";
        return Encoding.UTF8.GetBytes(csv);
    }

    public async Task<ItemImportResult> ImportCsvAsync(
        Stream fileStream,
        string sourceName,
        bool updateExisting,
        bool createMissingLookups)
    {
        var result = new ItemImportResult
        {
            SourceName = sourceName,
            UpdateExisting = updateExisting,
            CreateMissingLookups = createMissingLookups
        };

        var parsedRows = ParseCsvRows(fileStream, result.Errors);
        result.TotalRows = parsedRows.Count;
        if (parsedRows.Count == 0)
            return result;

        var duplicateSkuRows = parsedRows
            .GroupBy(r => NormalizeKey(r.Sku))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);
        foreach (var dup in duplicateSkuRows)
        {
            var allowBatchWise = dup.All(r => r.OpeningStock.GetValueOrDefault() > 0);
            if (allowBatchWise)
                continue;

            foreach (var row in dup.Skip(1))
            {
                result.Errors.Add(new ItemImportError
                {
                    RowNumber = row.RowNumber,
                    Sku = row.Sku,
                    Name = row.Name,
                    Message = $"Duplicate SKU in file: {row.Sku}"
                });
                row.Skip = true;
            }
        }

        var skuSet = parsedRows.Where(r => !r.Skip).Select(r => NormalizeKey(r.Sku)).ToHashSet();
        var barcodeSet = parsedRows.Where(r => !r.Skip && !string.IsNullOrWhiteSpace(r.Barcode))
            .Select(r => NormalizeKey(r.Barcode!))
            .ToHashSet();

        var existingItems = await _db.Items
            .Where(i => skuSet.Contains(i.SKU.ToLower()))
            .ToListAsync();
        var existingBySku = existingItems.ToDictionary(i => NormalizeKey(i.SKU), i => i);

        var existingBarcodes = await _db.Items
            .Where(i => i.Barcode != null && barcodeSet.Contains(i.Barcode.ToLower()))
            .Select(i => new { i.ItemId, i.Barcode })
            .ToListAsync();
        var existingByBarcode = existingBarcodes
            .Where(x => !string.IsNullOrWhiteSpace(x.Barcode))
            .ToDictionary(x => NormalizeKey(x.Barcode!), x => x.ItemId);

        var unitMap = await _db.Units
            .AsNoTracking()
            .ToDictionaryAsync(u => NormalizeKey(u.Name), u => u.UnitId);

        var categoryMap = await _db.Categories
            .AsNoTracking()
            .ToDictionaryAsync(c => NormalizeKey(c.Name), c => c.CategoryId);

        var warehouseMap = await _db.Warehouses
            .AsNoTracking()
            .ToDictionaryAsync(
                w => NormalizeKey(w.Name),
                w => new WarehouseLookup(w.WarehouseId, w.Name, w.StoreId));

        foreach (var row in parsedRows.Where(r => !r.Skip))
        {
            if (string.IsNullOrWhiteSpace(row.Sku))
            {
                AddError(result, row, "SKU is required.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                AddError(result, row, "Name is required.");
                continue;
            }
            if (row.OpeningStock.HasValue && row.OpeningStock.Value < 0)
            {
                AddError(result, row, "OpeningStock cannot be negative.");
                continue;
            }

            var skuKey = NormalizeKey(row.Sku);
            var barcodeKey = string.IsNullOrWhiteSpace(row.Barcode) ? null : NormalizeKey(row.Barcode!);

            if (barcodeKey is not null && existingByBarcode.TryGetValue(barcodeKey, out var barcodeOwnerId))
            {
                var sameSkuExists = existingBySku.TryGetValue(skuKey, out var skuItem) && skuItem.ItemId == barcodeOwnerId;
                if (!sameSkuExists)
                {
                    AddError(result, row, $"Barcode already used by another item: {row.Barcode}");
                    continue;
                }
            }

            var unitId = await ResolveUnitAsync(row.UnitName, createMissingLookups, unitMap, result, row);
            var categoryId = await ResolveCategoryAsync(row.CategoryName, createMissingLookups, categoryMap, result, row);

            Item targetItem;
            if (existingBySku.TryGetValue(skuKey, out var existing))
            {
                if (!updateExisting)
                {
                    targetItem = existing;
                    result.Skipped++;
                }
                else
                {
                    existing.Name = row.Name!;
                    existing.UnitPrice = row.UnitPrice;
                    existing.MRP = row.MRP;
                    existing.PurchasePrice = row.PurchasePrice;
                    existing.GstPercent = row.GstPercent;
                    existing.HsnCode = row.HsnCode;
                    existing.Barcode = row.Barcode;
                    existing.ReorderLevel = row.ReorderLevel;
                    existing.UnitId = unitId;
                    existing.CategoryId = categoryId;
                    existing.IsActive = row.IsActive;
                    targetItem = existing;
                    result.Updated++;
                }
            }
            else
            {
                targetItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    SKU = row.Sku!,
                    Name = row.Name!,
                    UnitPrice = row.UnitPrice,
                    MRP = row.MRP,
                    PurchasePrice = row.PurchasePrice,
                    GstPercent = row.GstPercent,
                    HsnCode = row.HsnCode,
                    Barcode = row.Barcode,
                    ReorderLevel = row.ReorderLevel,
                    UnitId = unitId,
                    CategoryId = categoryId,
                    IsActive = row.IsActive
                };
                _db.Items.Add(targetItem);
                existingBySku[skuKey] = targetItem;
                if (barcodeKey is not null)
                    existingByBarcode[barcodeKey] = targetItem.ItemId;
                result.Inserted++;
            }

            if (row.OpeningStock.GetValueOrDefault() > 0)
            {
                await ApplyOpeningStockAsync(
                    targetItem,
                    row,
                    createMissingLookups,
                    warehouseMap,
                    result);
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            result.Errors.Add(new ItemImportError
            {
                RowNumber = 0,
                Message = $"Database error while saving import: {ex.GetBaseException().Message}"
            });
        }

        result.Failed = result.Errors.Count(e => e.RowNumber > 0);
        return result;
    }

    public async Task<StarterPackResult> ApplyStarterPackAsync(BusinessType businessType, bool updateExisting)
    {
        var pack = GetStarterPack(businessType);
        var result = new StarterPackResult
        {
            BusinessType = businessType,
            TotalItems = pack.Count,
            UpdateExisting = updateExisting
        };

        if (pack.Count == 0)
        {
            result.Warnings.Add("No starter pack defined for selected business type.");
            return result;
        }

        var skuKeys = pack.Select(p => NormalizeKey(p.SKU)).ToHashSet();
        var existingItems = await _db.Items.Where(i => skuKeys.Contains(i.SKU.ToLower())).ToListAsync();
        var existingBySku = existingItems.ToDictionary(i => NormalizeKey(i.SKU), i => i);

        var unitMap = await _db.Units.AsNoTracking().ToDictionaryAsync(u => NormalizeKey(u.Name), u => u.UnitId);
        var categoryMap = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => NormalizeKey(c.Name), c => c.CategoryId);

        foreach (var p in pack)
        {
            var unitId = await ResolveUnitAsync(p.UnitName, true, unitMap, null, null);
            var categoryId = await ResolveCategoryAsync(p.CategoryName, true, categoryMap, null, null);

            var key = NormalizeKey(p.SKU);
            if (existingBySku.TryGetValue(key, out var existing))
            {
                if (!updateExisting)
                {
                    result.Skipped++;
                    continue;
                }

                existing.Name = p.Name;
                existing.UnitPrice = p.UnitPrice;
                existing.MRP = p.MRP;
                existing.PurchasePrice = p.PurchasePrice;
                existing.GstPercent = p.GstPercent;
                existing.HsnCode = p.HsnCode;
                existing.ReorderLevel = p.ReorderLevel;
                existing.UnitId = unitId;
                existing.CategoryId = categoryId;
                existing.IsActive = true;
                result.Updated++;
            }
            else
            {
                _db.Items.Add(new Item
                {
                    ItemId = Guid.NewGuid(),
                    SKU = p.SKU,
                    Name = p.Name,
                    UnitPrice = p.UnitPrice,
                    MRP = p.MRP,
                    PurchasePrice = p.PurchasePrice,
                    GstPercent = p.GstPercent,
                    HsnCode = p.HsnCode,
                    ReorderLevel = p.ReorderLevel,
                    UnitId = unitId,
                    CategoryId = categoryId,
                    IsActive = true
                });
                result.Inserted++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<QuickCreateItemResult> QuickCreateItemAsync(QuickCreateItemRequest req)
    {
        var sku = (req.SKU ?? "").Trim();
        var name = (req.Name ?? "").Trim();
        if (sku.Length == 0 || name.Length == 0)
            return QuickCreateItemResult.Fail("SKU and Name are required.");

        if (sku.Length > 50) return QuickCreateItemResult.Fail("SKU max length is 50.");
        if (name.Length > 200) return QuickCreateItemResult.Fail("Name max length is 200.");

        var existing = await _db.Items.FirstOrDefaultAsync(i => i.SKU == sku);
        if (existing is not null)
        {
            return new QuickCreateItemResult
            {
                Success = true,
                Created = false,
                ItemId = existing.ItemId,
                Message = $"Item already exists for SKU '{sku}'."
            };
        }

        if (!string.IsNullOrWhiteSpace(req.Barcode))
        {
            var barcodeExists = await _db.Items.AnyAsync(i => i.Barcode == req.Barcode);
            if (barcodeExists)
                return QuickCreateItemResult.Fail($"Barcode already exists: {req.Barcode}");
        }

        var unitMap = await _db.Units.AsNoTracking().ToDictionaryAsync(u => NormalizeKey(u.Name), u => u.UnitId);
        var categoryMap = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => NormalizeKey(c.Name), c => c.CategoryId);
        var unitId = await ResolveUnitAsync(req.UnitName, true, unitMap, null, null);
        var categoryId = await ResolveCategoryAsync(req.CategoryName, true, categoryMap, null, null);

        var item = new Item
        {
            ItemId = Guid.NewGuid(),
            SKU = sku,
            Name = name,
            Barcode = string.IsNullOrWhiteSpace(req.Barcode) ? null : req.Barcode.Trim(),
            UnitPrice = req.UnitPrice,
            MRP = req.MRP,
            PurchasePrice = req.PurchasePrice,
            GstPercent = req.GstPercent,
            HsnCode = string.IsNullOrWhiteSpace(req.HsnCode) ? null : req.HsnCode.Trim(),
            ReorderLevel = req.ReorderLevel,
            UnitId = unitId,
            CategoryId = categoryId,
            IsActive = true
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync();

        return new QuickCreateItemResult
        {
            Success = true,
            Created = true,
            ItemId = item.ItemId,
            Message = $"Item '{item.SKU} - {item.Name}' created."
        };
    }

    private static List<ImportCsvRow> ParseCsvRows(Stream fileStream, List<ItemImportError> errors)
    {
        fileStream.Position = 0;
        using var reader = new StreamReader(fileStream, Encoding.UTF8, true, leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
            return new List<ImportCsvRow>();

        var headers = parser.ReadFields() ?? Array.Empty<string>();
        var map = BuildHeaderMap(headers);

        var rows = new List<ImportCsvRow>();
        var rowNum = 1;
        while (!parser.EndOfData)
        {
            rowNum++;
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException ex)
            {
                errors.Add(new ItemImportError { RowNumber = rowNum, Message = $"Malformed CSV row: {ex.Message}" });
                continue;
            }

            if (fields is null) continue;

            var row = new ImportCsvRow
            {
                RowNumber = rowNum,
                Sku = GetField(fields, map, "sku"),
                Name = GetField(fields, map, "name"),
                Barcode = NullIfEmpty(GetField(fields, map, "barcode")),
                HsnCode = NullIfEmpty(GetField(fields, map, "hsn")),
                UnitName = NullIfEmpty(GetField(fields, map, "unit")),
                CategoryName = NullIfEmpty(GetField(fields, map, "category")),
                UnitPrice = ParseDecimal(GetField(fields, map, "unitprice"), 0m),
                MRP = ParseNullableDecimal(GetField(fields, map, "mrp")),
                PurchasePrice = ParseNullableDecimal(GetField(fields, map, "purchaseprice")),
                GstPercent = ParseNullableDecimal(GetField(fields, map, "gst")),
                ReorderLevel = ParseInt(GetField(fields, map, "reorderlevel"), 0),
                IsActive = ParseBool(GetField(fields, map, "isactive"), true),
                OpeningStock = ParseNullableDecimal(GetField(fields, map, "openingstock")),
                WarehouseName = NullIfEmpty(GetField(fields, map, "warehouse")),
                BatchNumber = NullIfEmpty(GetField(fields, map, "batchnumber")),
                ExpiryDate = ParseNullableDate(GetField(fields, map, "expirydate"))
            };

            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var aliases = new Dictionary<string, string[]>
        {
            ["sku"] = new[] { "sku", "itemcode", "item_code", "productcode", "product_code", "suppliersku", "supplier_sku", "plu" },
            ["name"] = new[] { "name", "itemname", "item_name", "productname", "product_name", "description" },
            ["unitprice"] = new[] { "unitprice", "saleprice", "sellingprice", "sellprice", "price" },
            ["mrp"] = new[] { "mrp", "maxretailprice", "listprice" },
            ["purchaseprice"] = new[] { "purchaseprice", "costprice", "cost", "buyprice", "rate" },
            ["gst"] = new[] { "gst", "gstpercent", "gst_pct", "tax", "taxpercent" },
            ["hsn"] = new[] { "hsn", "hsncode", "hsn_code" },
            ["barcode"] = new[] { "barcode", "ean", "eancode", "upc" },
            ["reorderlevel"] = new[] { "reorderlevel", "reorder", "minstock", "minimumstock" },
            ["unit"] = new[] { "unit", "unitname", "uom" },
            ["category"] = new[] { "category", "categoryname", "group", "department" },
            ["isactive"] = new[] { "isactive", "active", "status" },
            ["openingstock"] = new[] { "openingstock", "openingqty", "openingquantity", "initialstock", "stock" },
            ["warehouse"] = new[] { "warehouse", "warehousename", "godown", "location" },
            ["batchnumber"] = new[] { "batchnumber", "batchno", "batch", "lotnumber", "lotno" },
            ["expirydate"] = new[] { "expirydate", "expdate", "expiry", "exp" }
        };

        var normalized = headers
            .Select((h, i) => new { Key = NormalizeHeader(h), Index = i })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.Index);

        var map = new Dictionary<string, int>();
        foreach (var target in aliases)
        {
            var found = target.Value.FirstOrDefault(a => normalized.ContainsKey(a));
            if (found is not null)
                map[target.Key] = normalized[found];
        }

        return map;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private static string NormalizeKey(string? value) => (value ?? "").Trim().ToLowerInvariant();

    private static string? GetField(string[] row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx)) return null;
        if (idx < 0 || idx >= row.Length) return null;
        return row[idx]?.Trim();
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal ParseDecimal(string? value, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv)) return inv;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var cur)) return cur;
        return fallback;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv)) return inv;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var cur)) return cur;
        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out i)) return i;
        return fallback;
    }

    private static DateTime? ParseNullableDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd-MM-yyyy",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy/MM/dd",
            "dd-MMM-yyyy"
        };

        if (DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedInv))
            return parsedInv.Date;
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedCur))
            return parsedCur.Date;

        return null;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "1" or "true" or "yes" or "y" or "active" => true,
            "0" or "false" or "no" or "n" or "inactive" => false,
            _ => fallback
        };
    }

    private async Task<Guid?> ResolveUnitAsync(
        string? unitName,
        bool createMissing,
        Dictionary<string, Guid> unitMap,
        ItemImportResult? result,
        ImportCsvRow? row)
    {
        if (string.IsNullOrWhiteSpace(unitName)) return null;
        var key = NormalizeKey(unitName);
        if (unitMap.TryGetValue(key, out var id))
            return id;

        if (!createMissing)
        {
            AddError(result, row, $"Unit not found: {unitName}");
            return null;
        }

        var unit = new Unit
        {
            UnitId = Guid.NewGuid(),
            Name = unitName.Trim(),
            IsActive = true
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync();
        unitMap[key] = unit.UnitId;
        return unit.UnitId;
    }

    private async Task<Guid?> ResolveCategoryAsync(
        string? categoryName,
        bool createMissing,
        Dictionary<string, Guid> categoryMap,
        ItemImportResult? result,
        ImportCsvRow? row)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return null;
        var key = NormalizeKey(categoryName);
        if (categoryMap.TryGetValue(key, out var id))
            return id;

        if (!createMissing)
        {
            AddError(result, row, $"Category not found: {categoryName}");
            return null;
        }

        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = categoryName.Trim(),
            IsActive = true
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        categoryMap[key] = category.CategoryId;
        return category.CategoryId;
    }

    private async Task ApplyOpeningStockAsync(
        Item item,
        ImportCsvRow row,
        bool createMissingLookups,
        Dictionary<string, WarehouseLookup> warehouseMap,
        ItemImportResult result)
    {
        var openingQty = row.OpeningStock.GetValueOrDefault();
        if (openingQty <= 0)
            return;

        var warehouse = await ResolveWarehouseAsync(
            row.WarehouseName,
            createMissingLookups,
            warehouseMap,
            result,
            row);

        if (warehouse is null)
            return;

        var stock = await _db.Stocks.FirstOrDefaultAsync(s =>
            s.ItemId == item.ItemId && s.WarehouseId == warehouse.WarehouseId);

        if (stock is null)
        {
            stock = new Stock
            {
                StockId = Guid.NewGuid(),
                ItemId = item.ItemId,
                WarehouseId = warehouse.WarehouseId,
                Quantity = 0,
                BatchNumber = row.BatchNumber,
                ExpiryDate = row.ExpiryDate,
                CompanyId = item.CompanyId
            };
            _db.Stocks.Add(stock);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(stock.BatchNumber) && !string.IsNullOrWhiteSpace(row.BatchNumber))
                stock.BatchNumber = row.BatchNumber;
            if (!stock.ExpiryDate.HasValue && row.ExpiryDate.HasValue)
                stock.ExpiryDate = row.ExpiryDate;
        }

        stock.Quantity += openingQty;

        _db.StockTransactions.Add(new StockTransaction
        {
            StockTransactionId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Type = "OPENING",
            ItemId = item.ItemId,
            WarehouseId = warehouse.WarehouseId,
            StoreId = warehouse.StoreId,
            Qty = openingQty,
            RefType = "ItemOnboarding",
            RefId = item.ItemId.ToString(),
            Reason = BuildOpeningReason(row),
            UnitCost = row.PurchasePrice,
            CompanyId = item.CompanyId
        });
    }

    private async Task<WarehouseLookup?> ResolveWarehouseAsync(
        string? warehouseName,
        bool createMissing,
        Dictionary<string, WarehouseLookup> warehouseMap,
        ItemImportResult? result,
        ImportCsvRow? row)
    {
        if (string.IsNullOrWhiteSpace(warehouseName))
        {
            if (warehouseMap.Count > 0)
                return warehouseMap.Values.OrderBy(x => x.Name).First();

            if (!createMissing)
            {
                AddError(result, row, "WarehouseName is required when no warehouse exists.");
                return null;
            }

            warehouseName = "Main Warehouse";
        }

        var key = NormalizeKey(warehouseName);
        if (warehouseMap.TryGetValue(key, out var existing))
            return existing;

        if (!createMissing)
        {
            AddError(result, row, $"Warehouse not found: {warehouseName}");
            return null;
        }

        var fallbackStoreId = await _db.Stores
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => (Guid?)s.StoreId)
            .FirstOrDefaultAsync();

        var warehouse = new Warehouse
        {
            WarehouseId = Guid.NewGuid(),
            Name = warehouseName.Trim(),
            StoreId = fallbackStoreId
        };

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        var lookup = new WarehouseLookup(warehouse.WarehouseId, warehouse.Name, warehouse.StoreId);
        warehouseMap[key] = lookup;
        return lookup;
    }

    private static string BuildOpeningReason(ImportCsvRow row)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.BatchNumber))
            details.Add($"Batch: {row.BatchNumber}");
        if (row.ExpiryDate.HasValue)
            details.Add($"Exp: {row.ExpiryDate.Value:yyyy-MM-dd}");

        return details.Count == 0
            ? "Opening stock via item onboarding import."
            : $"Opening stock via item onboarding import ({string.Join(", ", details)}).";
    }

    private static void AddError(ItemImportResult? result, ImportCsvRow? row, string message)
    {
        if (result is null || row is null) return;
        result.Errors.Add(new ItemImportError
        {
            RowNumber = row.RowNumber,
            Sku = row.Sku,
            Name = row.Name,
            Message = message
        });
    }

    private static IReadOnlyList<StarterItemSeed> GetStarterPack(BusinessType businessType)
    {
        return businessType switch
        {
            BusinessType.Kirana => StarterPacks.Kirana,
            BusinessType.Supermarket => StarterPacks.Supermarket,
            BusinessType.Hardware => StarterPacks.Hardware,
            BusinessType.Pharmacy => StarterPacks.Pharmacy,
            BusinessType.Fashion => StarterPacks.Fashion,
            BusinessType.Restaurant => StarterPacks.Restaurant,
            _ => StarterPacks.Generic
        };
    }

    private sealed class ImportCsvRow
    {
        public int RowNumber { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? MRP { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? GstPercent { get; set; }
        public string? HsnCode { get; set; }
        public string? Barcode { get; set; }
        public int ReorderLevel { get; set; }
        public string? UnitName { get; set; }
        public string? CategoryName { get; set; }
        public bool IsActive { get; set; } = true;
        public decimal? OpeningStock { get; set; }
        public string? WarehouseName { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool Skip { get; set; }
    }

    private sealed record WarehouseLookup(Guid WarehouseId, string Name, Guid? StoreId);

    private sealed record StarterItemSeed(
        string SKU,
        string Name,
        string CategoryName,
        string UnitName,
        decimal UnitPrice,
        decimal? MRP,
        decimal? PurchasePrice,
        decimal? GstPercent,
        string? HsnCode,
        int ReorderLevel);

    private static class StarterPacks
    {
        public static readonly IReadOnlyList<StarterItemSeed> Generic = new[]
        {
            new StarterItemSeed("GEN-001", "General Item 1", "General", "PCS", 100, 110, 80, 18, "000000", 5),
            new StarterItemSeed("GEN-002", "General Item 2", "General", "PCS", 150, 165, 120, 18, "000000", 5),
            new StarterItemSeed("GEN-003", "General Item 3", "General", "PCS", 200, 220, 160, 18, "000000", 5)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Kirana = new[]
        {
            new StarterItemSeed("KRN-RICE-5", "Rice 5kg", "Grains", "PCS", 310, 330, 280, 5, "100630", 20),
            new StarterItemSeed("KRN-ATTA-5", "Wheat Flour 5kg", "Grains", "PCS", 255, 270, 230, 5, "110100", 20),
            new StarterItemSeed("KRN-OIL-1", "Sunflower Oil 1L", "Edible Oils", "PCS", 165, 175, 145, 5, "151219", 25),
            new StarterItemSeed("KRN-SUGAR-1", "Sugar 1kg", "Essentials", "PCS", 46, 50, 40, 5, "170199", 40),
            new StarterItemSeed("KRN-TEA-250", "Tea 250g", "Beverages", "PCS", 135, 145, 118, 5, "090230", 25)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Supermarket = new[]
        {
            new StarterItemSeed("SUP-MILK-1", "Toned Milk 1L", "Dairy", "PCS", 62, 64, 56, 5, "040120", 50),
            new StarterItemSeed("SUP-BREAD-1", "Bread Loaf", "Bakery", "PCS", 42, 45, 34, 5, "190590", 40),
            new StarterItemSeed("SUP-SOAP-1", "Bath Soap 125g", "Personal Care", "PCS", 39, 42, 31, 18, "340111", 80),
            new StarterItemSeed("SUP-SHAM-1", "Shampoo 180ml", "Personal Care", "PCS", 169, 185, 140, 18, "330510", 35),
            new StarterItemSeed("SUP-BIS-1", "Biscuits 150g", "Snacks", "PCS", 25, 30, 19, 18, "190531", 100)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Hardware = new[]
        {
            new StarterItemSeed("HWR-HMR-1", "Hammer 500g", "Tools", "PCS", 240, 260, 190, 18, "820520", 8),
            new StarterItemSeed("HWR-SCR-1", "Screwdriver Set", "Tools", "PCS", 320, 350, 270, 18, "820540", 8),
            new StarterItemSeed("HWR-WIR-1", "PVC Wire 90m", "Electrical", "PCS", 1299, 1399, 1100, 18, "854449", 6),
            new StarterItemSeed("HWR-PLY-1", "Plywood Sheet 8x4", "Wood", "PCS", 1890, 2050, 1650, 18, "441233", 4),
            new StarterItemSeed("HWR-NUT-1", "Nut Bolt Pack", "Fasteners", "PCS", 85, 95, 68, 18, "731816", 40)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Pharmacy = new[]
        {
            new StarterItemSeed("PHR-PARA-10", "Paracetamol 500mg (10)", "OTC", "PCS", 28, 30, 22, 12, "300490", 120),
            new StarterItemSeed("PHR-VITC-10", "Vitamin C Tablets (10)", "Supplements", "PCS", 45, 50, 35, 12, "300450", 80),
            new StarterItemSeed("PHR-SANI-100", "Hand Sanitizer 100ml", "Hygiene", "PCS", 65, 75, 52, 18, "380894", 60),
            new StarterItemSeed("PHR-MASK-10", "Surgical Mask (10)", "Hygiene", "PCS", 90, 110, 72, 5, "630790", 70),
            new StarterItemSeed("PHR-BAND-1", "Bandage Roll", "First Aid", "PCS", 38, 45, 30, 12, "300510", 90)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Fashion = new[]
        {
            new StarterItemSeed("FSH-TSH-M", "T-Shirt Men M", "Apparel", "PCS", 499, 699, 330, 5, "610910", 20),
            new StarterItemSeed("FSH-JNS-32", "Jeans Men 32", "Apparel", "PCS", 1299, 1699, 910, 5, "620342", 15),
            new StarterItemSeed("FSH-SHR-L", "Shirt Men L", "Apparel", "PCS", 899, 1199, 620, 5, "620520", 18),
            new StarterItemSeed("FSH-BLT-1", "Leather Belt", "Accessories", "PCS", 599, 799, 390, 5, "420330", 12),
            new StarterItemSeed("FSH-SCK-1", "Cotton Socks Pair", "Accessories", "PCS", 99, 149, 58, 5, "611595", 40)
        };

        public static readonly IReadOnlyList<StarterItemSeed> Restaurant = new[]
        {
            new StarterItemSeed("RST-RICE-25", "Rice 25kg", "Raw Material", "BAG", 1450, 1500, 1320, 5, "100630", 10),
            new StarterItemSeed("RST-OIL-15", "Cooking Oil 15L", "Raw Material", "CAN", 2450, 2520, 2290, 5, "151219", 8),
            new StarterItemSeed("RST-SPICE-1", "Garam Masala 1kg", "Spices", "PCS", 520, 560, 470, 5, "091099", 12),
            new StarterItemSeed("RST-BOX-1", "Food Container", "Packaging", "PCS", 6, 8, 4.5m, 18, "392310", 400),
            new StarterItemSeed("RST-TISS-1", "Tissue Pack", "Consumables", "PCS", 42, 50, 32, 18, "481820", 120)
        };
    }

    public sealed class ItemImportResult
    {
        public string SourceName { get; set; } = "";
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool UpdateExisting { get; set; }
        public bool CreateMissingLookups { get; set; }
        public List<ItemImportError> Errors { get; set; } = new();
    }

    public sealed class ItemImportError
    {
        public int RowNumber { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public string Message { get; set; } = "";
    }

    public sealed class StarterPackResult
    {
        public BusinessType BusinessType { get; set; }
        public int TotalItems { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public bool UpdateExisting { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class QuickCreateItemRequest
    {
        public string SKU { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Barcode { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? MRP { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? GstPercent { get; set; }
        public string? HsnCode { get; set; }
        public int ReorderLevel { get; set; }
        public string? UnitName { get; set; }
        public string? CategoryName { get; set; }
    }

    public sealed class QuickCreateItemResult
    {
        public bool Success { get; set; }
        public bool Created { get; set; }
        public Guid ItemId { get; set; }
        public string Message { get; set; } = "";

        public static QuickCreateItemResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
