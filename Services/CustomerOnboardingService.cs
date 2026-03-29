using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

public sealed class CustomerOnboardingService
{
    private readonly ApplicationDbContext _db;

    public CustomerOnboardingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public byte[] BuildTemplateCsv()
    {
        const string csv =
            "Name,Phone,Email,Address,GSTIN,OpeningBalance\r\n" +
            "Walk-in Customer,9876543210,walkin@example.com,\"Main Market, Surat\",24ABCDE1234F1Z5,0\r\n" +
            "ABC Retail,9898989898,abc.retail@example.com,\"Ring Road, Ahmedabad\",24AABCT1234C1Z8,1500\r\n";
        return Encoding.UTF8.GetBytes(csv);
    }

    public async Task<CustomerImportResult> ImportCsvAsync(
        Stream fileStream,
        string sourceName,
        bool updateExisting)
    {
        var result = new CustomerImportResult
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
                result.Errors.Add(new CustomerImportError
                {
                    RowNumber = row.RowNumber,
                    Name = row.Name,
                    Message = $"Duplicate customer Name in file: {row.Name}"
                });
                row.Skip = true;
            }
        }

        var nameSet = rows
            .Where(r => !r.Skip && !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => NormalizeKey(r.Name))
            .ToHashSet();

        var existing = await _db.Customers
            .Where(c => nameSet.Contains(c.Name.ToLower()))
            .ToListAsync();

        var existingByName = existing.ToDictionary(c => NormalizeKey(c.Name), c => c);

        foreach (var row in rows.Where(r => !r.Skip))
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                AddError(result, row, "Name is required.");
                continue;
            }

            if (existingByName.TryGetValue(NormalizeKey(row.Name), out var customer))
            {
                if (!updateExisting)
                {
                    result.Skipped++;
                    continue;
                }

                customer.Phone = row.Phone;
                customer.Email = row.Email;
                customer.Address = row.Address;
                customer.Gstin = row.Gstin;
                customer.OpeningBalance = row.OpeningBalance;
                result.Updated++;
            }
            else
            {
                customer = new Customer
                {
                    CustomerId = Guid.NewGuid(),
                    Name = row.Name.Trim(),
                    Phone = row.Phone,
                    Email = row.Email,
                    Address = row.Address,
                    Gstin = row.Gstin,
                    OpeningBalance = row.OpeningBalance
                };

                _db.Customers.Add(customer);
                existingByName[NormalizeKey(customer.Name)] = customer;
                result.Inserted++;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            result.Errors.Add(new CustomerImportError
            {
                RowNumber = 0,
                Message = $"Database error while saving import: {ex.GetBaseException().Message}"
            });
        }

        result.Failed = result.Errors.Count(e => e.RowNumber > 0);
        return result;
    }

    private static List<ImportCsvRow> ParseCsvRows(Stream fileStream, List<CustomerImportError> errors)
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
                errors.Add(new CustomerImportError { RowNumber = rowNum, Message = $"Malformed CSV row: {ex.Message}" });
                continue;
            }

            if (fields is null) continue;

            rows.Add(new ImportCsvRow
            {
                RowNumber = rowNum,
                Name = GetField(fields, map, "name"),
                Phone = NullIfEmpty(GetField(fields, map, "phone")),
                Email = NullIfEmpty(GetField(fields, map, "email")),
                Address = NullIfEmpty(GetField(fields, map, "address")),
                Gstin = NullIfEmpty(GetField(fields, map, "gstin")),
                OpeningBalance = ParseDecimal(GetField(fields, map, "openingbalance"), 0m)
            });
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var aliases = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "name", "customername", "partyname" },
            ["phone"] = new[] { "phone", "mobile", "contact", "contactnumber" },
            ["email"] = new[] { "email", "mail" },
            ["address"] = new[] { "address", "addr" },
            ["gstin"] = new[] { "gstin", "gst", "gstno", "gstnumber" },
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

    private static void AddError(CustomerImportResult? result, ImportCsvRow? row, string message)
    {
        if (result is null || row is null) return;
        result.Errors.Add(new CustomerImportError
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
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Gstin { get; set; }
        public decimal OpeningBalance { get; set; }
        public bool Skip { get; set; }
    }

    public sealed class CustomerImportResult
    {
        public string SourceName { get; set; } = "";
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool UpdateExisting { get; set; }
        public List<CustomerImportError> Errors { get; set; } = new();
    }

    public sealed class CustomerImportError
    {
        public int RowNumber { get; set; }
        public string? Name { get; set; }
        public string Message { get; set; } = "";
    }
}
