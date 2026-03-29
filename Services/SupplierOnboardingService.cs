using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

public sealed class SupplierOnboardingService
{
    private readonly ApplicationDbContext _db;

    public SupplierOnboardingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public byte[] BuildTemplateCsv()
    {
        const string csv =
            "Name,ContactPerson,Phone,Email,GSTIN,Address,OpeningBalance\r\n" +
            "Prime Distributors,Rahul Shah,9876501234,prime@example.com,24ABCDE1234F1Z5,\"Ring Road, Surat\",25000\r\n" +
            "Sunrise Wholesale,Anita Patel,9825000000,sunrise@example.com,24AAACS1234B1Z2,\"Vapi Industrial Area\",0\r\n";
        return Encoding.UTF8.GetBytes(csv);
    }

    public async Task<SupplierImportResult> ImportCsvAsync(
        Stream fileStream,
        string sourceName,
        bool updateExisting)
    {
        var result = new SupplierImportResult
        {
            SourceName = sourceName,
            UpdateExisting = updateExisting
        };

        var rows = ParseCsvRows(fileStream, result.Errors);
        result.TotalRows = rows.Count;
        if (rows.Count == 0)
            return result;

        var duplicateNameRows = rows
            .GroupBy(r => NormalizeKey(r.Name))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);
        foreach (var dup in duplicateNameRows)
        {
            foreach (var row in dup.Skip(1))
            {
                result.Errors.Add(new SupplierImportError
                {
                    RowNumber = row.RowNumber,
                    Name = row.Name,
                    Message = $"Duplicate supplier Name in file: {row.Name}"
                });
                row.Skip = true;
            }
        }

        var nameSet = rows
            .Where(r => !r.Skip && !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => NormalizeKey(r.Name))
            .ToHashSet();

        var existing = await _db.Suppliers
            .Where(s => nameSet.Contains(s.Name.ToLower()))
            .ToListAsync();

        var existingByName = existing.ToDictionary(s => NormalizeKey(s.Name), s => s);

        foreach (var row in rows.Where(r => !r.Skip))
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                AddError(result, row, "Name is required.");
                continue;
            }

            if (existingByName.TryGetValue(NormalizeKey(row.Name), out var supplier))
            {
                if (!updateExisting)
                {
                    result.Skipped++;
                    continue;
                }

                supplier.ContactPerson = row.ContactPerson;
                supplier.Phone = row.Phone;
                supplier.Email = row.Email;
                supplier.Gstin = row.Gstin;
                supplier.Address = row.Address;
                supplier.OpeningBalance = row.OpeningBalance;
                supplier.IsActive = true;
                result.Updated++;
            }
            else
            {
                supplier = new Supplier
                {
                    SupplierId = Guid.NewGuid(),
                    Name = row.Name.Trim(),
                    ContactPerson = row.ContactPerson,
                    Phone = row.Phone,
                    Email = row.Email,
                    Gstin = row.Gstin,
                    Address = row.Address,
                    OpeningBalance = row.OpeningBalance,
                    IsActive = true
                };

                _db.Suppliers.Add(supplier);
                existingByName[NormalizeKey(supplier.Name)] = supplier;
                result.Inserted++;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            result.Errors.Add(new SupplierImportError
            {
                RowNumber = 0,
                Message = $"Database error while saving import: {ex.GetBaseException().Message}"
            });
        }

        result.Failed = result.Errors.Count(e => e.RowNumber > 0);
        return result;
    }

    private static List<ImportCsvRow> ParseCsvRows(Stream fileStream, List<SupplierImportError> errors)
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
                errors.Add(new SupplierImportError { RowNumber = rowNum, Message = $"Malformed CSV row: {ex.Message}" });
                continue;
            }

            if (fields is null) continue;

            rows.Add(new ImportCsvRow
            {
                RowNumber = rowNum,
                Name = GetField(fields, map, "name"),
                ContactPerson = NullIfEmpty(GetField(fields, map, "contactperson")),
                Phone = NullIfEmpty(GetField(fields, map, "phone")),
                Email = NullIfEmpty(GetField(fields, map, "email")),
                Gstin = NullIfEmpty(GetField(fields, map, "gstin")),
                Address = NullIfEmpty(GetField(fields, map, "address")),
                OpeningBalance = ParseDecimal(GetField(fields, map, "openingbalance"), 0m)
            });
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var aliases = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "name", "suppliername", "partyname", "vendorname" },
            ["contactperson"] = new[] { "contactperson", "contact", "person", "contactname" },
            ["phone"] = new[] { "phone", "mobile", "contactnumber" },
            ["email"] = new[] { "email", "mail" },
            ["gstin"] = new[] { "gstin", "gst", "gstno", "gstnumber" },
            ["address"] = new[] { "address", "addr" },
            ["openingbalance"] = new[] { "openingbalance", "opening", "balance", "outstanding" }
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

    private static void AddError(SupplierImportResult? result, ImportCsvRow? row, string message)
    {
        if (result is null || row is null) return;
        result.Errors.Add(new SupplierImportError
        {
            RowNumber = row.RowNumber,
            Name = row.Name,
            Message = message
        });
    }

    private sealed class ImportCsvRow
    {
        public int RowNumber { get; set; }
        public string? Name { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Gstin { get; set; }
        public string? Address { get; set; }
        public decimal OpeningBalance { get; set; }
        public bool Skip { get; set; }
    }

    public sealed class SupplierImportResult
    {
        public string SourceName { get; set; } = "";
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool UpdateExisting { get; set; }
        public List<SupplierImportError> Errors { get; set; } = new();
    }

    public sealed class SupplierImportError
    {
        public int RowNumber { get; set; }
        public string? Name { get; set; }
        public string Message { get; set; } = "";
    }
}
