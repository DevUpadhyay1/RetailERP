using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

public sealed class PortalService
{
    public const byte PortalTypeCustomer = 1;
    public const byte PortalTypeSupplier = 2;

    private readonly ApplicationDbContext _db;

    public PortalService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<GeneratedPortalLinkResult> GenerateCustomerLinkAsync(Guid customerId, int validHours, string? label)
    {
        var exists = await _db.Customers.AsNoTracking().AnyAsync(x => x.CustomerId == customerId);
        if (!exists) return GeneratedPortalLinkResult.Fail("Customer not found.");

        var token = GenerateToken();
        var link = new PortalAccessLink
        {
            PortalAccessLinkId = Guid.NewGuid(),
            PortalType = PortalTypeCustomer,
            CustomerId = customerId,
            TokenHash = HashToken(token),
            TokenHint = token.Length >= 6 ? token[^6..] : token,
            Label = NormalizeLabel(label),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Clamp(validHours, 1, 24 * 30)),
            IsRevoked = false
        };

        _db.PortalAccessLinks.Add(link);
        await _db.SaveChangesAsync();

        return GeneratedPortalLinkResult.Ok(token, link.ExpiresAtUtc);
    }

    public async Task<GeneratedPortalLinkResult> GenerateSupplierLinkAsync(Guid supplierId, int validHours, string? label)
    {
        var exists = await _db.Suppliers.AsNoTracking().AnyAsync(x => x.SupplierId == supplierId);
        if (!exists) return GeneratedPortalLinkResult.Fail("Supplier not found.");

        var token = GenerateToken();
        var link = new PortalAccessLink
        {
            PortalAccessLinkId = Guid.NewGuid(),
            PortalType = PortalTypeSupplier,
            SupplierId = supplierId,
            TokenHash = HashToken(token),
            TokenHint = token.Length >= 6 ? token[^6..] : token,
            Label = NormalizeLabel(label),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Clamp(validHours, 1, 24 * 30)),
            IsRevoked = false
        };

        _db.PortalAccessLinks.Add(link);
        await _db.SaveChangesAsync();

        return GeneratedPortalLinkResult.Ok(token, link.ExpiresAtUtc);
    }

    public async Task<bool> RevokeLinkAsync(Guid portalAccessLinkId)
    {
        var link = await _db.PortalAccessLinks.FirstOrDefaultAsync(x => x.PortalAccessLinkId == portalAccessLinkId);
        if (link is null) return false;
        if (link.IsRevoked) return true;

        link.IsRevoked = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<PortalTokenValidationResult> ValidateCustomerTokenAsync(string? token)
        => ValidateTokenAsync(token, PortalTypeCustomer);

    public Task<PortalTokenValidationResult> ValidateSupplierTokenAsync(string? token)
        => ValidateTokenAsync(token, PortalTypeSupplier);

    public async Task<CustomerPortalVm?> BuildCustomerPortalAsync(Guid customerId)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null) return null;

        var bills = await _db.PosBills
            .AsNoTracking()
            .Include(x => x.Store)
            .Include(x => x.Payments)
            .Where(x => x.CustomerId == customerId && x.Status == 2)
            .OrderByDescending(x => x.BillDate)
            .ThenByDescending(x => x.BillNo)
            .Take(80)
            .Select(x => new CustomerPortalBillVm
            {
                PosBillId = x.PosBillId,
                BillNo = x.BillNo,
                BillDate = x.BillDate,
                StoreName = x.Store != null ? x.Store.Name : "-",
                GrandTotal = x.GrandTotal,
                PaidAmount = x.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount)
            })
            .ToListAsync();

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.InvoiceNo)
            .Take(80)
            .Select(x => new CustomerPortalInvoiceVm
            {
                InvoiceId = x.InvoiceId,
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = x.InvoiceDate,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : "-",
                TotalAmount = x.TotalAmount,
                Status = x.Status
            })
            .ToListAsync();

        var returnRequests = await _db.PortalReturnRequests
            .AsNoTracking()
            .Include(x => x.PosBill)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Where(x => x.CustomerId == customerId)
            .Take(50)
            .Select(x => new CustomerPortalReturnRequestVm
            {
                PortalReturnRequestId = x.PortalReturnRequestId,
                BillNo = x.PosBill != null ? x.PosBill.BillNo : "-",
                RequestedAtUtc = x.RequestedAtUtc,
                Reason = x.Reason,
                Status = x.Status,
                AdminNote = x.AdminNote
            })
            .ToListAsync();

        return new CustomerPortalVm
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Bills = bills,
            Invoices = invoices,
            ReturnRequests = returnRequests
        };
    }

    public async Task<PortalActionResult> SubmitReturnRequestAsync(Guid customerId, Guid posBillId, string? reason)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PosBillId == posBillId && x.CustomerId == customerId && x.Status == 2);

        if (bill is null)
            return PortalActionResult.Fail("Selected bill was not found for this customer.");

        var exists = await _db.PortalReturnRequests.AnyAsync(x =>
            x.CustomerId == customerId &&
            x.PosBillId == posBillId &&
            (x.Status == 1 || x.Status == 2 || x.Status == 4));

        if (exists)
            return PortalActionResult.Fail("A return request already exists for this bill.");

        var req = new PortalReturnRequest
        {
            PortalReturnRequestId = Guid.NewGuid(),
            CustomerId = customerId,
            PosBillId = posBillId,
            RequestedAtUtc = DateTime.UtcNow,
            Reason = NormalizeReason(reason),
            Status = 1
        };

        _db.PortalReturnRequests.Add(req);
        await _db.SaveChangesAsync();
        return PortalActionResult.Ok("Return request submitted. Store team will review it.");
    }

    public async Task<PortalActionResult> ReviewReturnRequestAsync(Guid portalReturnRequestId, byte status, string? adminNote, Guid? reviewedByUserId)
    {
        if (status is not (2 or 3 or 4))
            return PortalActionResult.Fail("Invalid status. Use Approve/Reject/Processed.");

        var req = await _db.PortalReturnRequests.FirstOrDefaultAsync(x => x.PortalReturnRequestId == portalReturnRequestId);
        if (req is null) return PortalActionResult.Fail("Return request not found.");

        req.Status = status;
        req.AdminNote = NormalizeReason(adminNote);
        req.ReviewedByUserId = reviewedByUserId;
        req.ReviewedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return PortalActionResult.Ok("Return request updated.");
    }

    public async Task<SupplierPortalVm?> BuildSupplierPortalAsync(Guid supplierId)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == supplierId);
        if (supplier is null) return null;

        var purchases = await _db.Purchases
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.PurchaseDate)
            .ThenByDescending(x => x.PurchaseNo)
            .Take(120)
            .ToListAsync();

        var purchaseIds = purchases.Select(x => x.PurchaseId).ToList();

        var lineCounts = await _db.PurchaseLines
            .AsNoTracking()
            .Where(x => purchaseIds.Contains(x.PurchaseId))
            .GroupBy(x => x.PurchaseId)
            .Select(g => new { PurchaseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Count);

        var responseMap = await _db.SupplierPoResponses
            .AsNoTracking()
            .Where(x => purchaseIds.Contains(x.PurchaseId))
            .ToDictionaryAsync(x => x.PurchaseId, x => x);

        var rows = purchases.Select(p =>
        {
            responseMap.TryGetValue(p.PurchaseId, out var response);
            lineCounts.TryGetValue(p.PurchaseId, out var lineCount);

            return new SupplierPortalPoVm
            {
                PurchaseId = p.PurchaseId,
                PurchaseNo = p.PurchaseNo,
                PurchaseDate = p.PurchaseDate,
                WarehouseName = p.Warehouse?.Name ?? "-",
                TotalAmount = p.TotalAmount,
                PurchaseStatus = p.Status,
                LineCount = lineCount,
                ResponseStatus = response?.ResponseStatus ?? 1,
                RespondedAtUtc = response?.RespondedAtUtc,
                ExpectedDeliveryDate = response?.ExpectedDeliveryDate,
                SupplierNote = response?.SupplierNote
            };
        }).ToList();

        return new SupplierPortalVm
        {
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            PurchaseOrders = rows
        };
    }

    public async Task<PortalActionResult> SaveSupplierPoResponseAsync(
        Guid supplierId,
        Guid purchaseId,
        byte responseStatus,
        DateTime? expectedDeliveryDate,
        string? supplierNote)
    {
        if (responseStatus is not (2 or 3))
            return PortalActionResult.Fail("Invalid PO response.");

        var purchase = await _db.Purchases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PurchaseId == purchaseId && x.SupplierId == supplierId);

        if (purchase is null)
            return PortalActionResult.Fail("Purchase order not found.");

        var response = await _db.SupplierPoResponses
            .FirstOrDefaultAsync(x => x.PurchaseId == purchaseId);

        if (response is null)
        {
            response = new SupplierPoResponse
            {
                SupplierPoResponseId = Guid.NewGuid(),
                PurchaseId = purchaseId,
                SupplierId = supplierId
            };
            _db.SupplierPoResponses.Add(response);
        }

        response.ResponseStatus = responseStatus;
        response.RespondedAtUtc = DateTime.UtcNow;
        response.ExpectedDeliveryDate = responseStatus == 2 ? expectedDeliveryDate?.Date : null;
        response.SupplierNote = NormalizeReason(supplierNote);

        await _db.SaveChangesAsync();
        return PortalActionResult.Ok(responseStatus == 2 ? "PO accepted." : "PO rejected.");
    }

    private async Task<PortalTokenValidationResult> ValidateTokenAsync(string? token, byte expectedType)
    {
        token = (token ?? string.Empty).Trim();
        if (token.Length == 0)
            return PortalTokenValidationResult.Fail("Portal link token is missing.");

        var hash = HashToken(token);

        var link = await _db.PortalAccessLinks
            .Include(x => x.Customer)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.PortalType == expectedType);

        if (link is null)
            return PortalTokenValidationResult.Fail("Invalid portal link.");

        if (link.IsRevoked)
            return PortalTokenValidationResult.Fail("This portal link was revoked.");

        if (link.ExpiresAtUtc < DateTime.UtcNow)
            return PortalTokenValidationResult.Fail("This portal link has expired.");

        link.LastAccessedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return PortalTokenValidationResult.Ok(new PortalAccessContext
        {
            PortalAccessLinkId = link.PortalAccessLinkId,
            PortalType = link.PortalType,
            CustomerId = link.CustomerId,
            SupplierId = link.SupplierId,
            DisplayName = link.Customer?.Name ?? link.Supplier?.Name ?? "Portal User",
            ExpiresAtUtc = link.ExpiresAtUtc
        });
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string? NormalizeLabel(string? value)
    {
        var x = (value ?? string.Empty).Trim();
        if (x.Length == 0) return null;
        return x.Length <= 120 ? x : x[..120];
    }

    private static string? NormalizeReason(string? value)
    {
        var x = (value ?? string.Empty).Trim();
        if (x.Length == 0) return null;
        return x.Length <= 500 ? x : x[..500];
    }

    public sealed class GeneratedPortalLinkResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }

        public static GeneratedPortalLinkResult Ok(string token, DateTime expiresAtUtc) => new()
        {
            Success = true,
            Message = "Portal link created.",
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        };

        public static GeneratedPortalLinkResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }

    public sealed class PortalTokenValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PortalAccessContext? Context { get; set; }

        public static PortalTokenValidationResult Ok(PortalAccessContext ctx) => new()
        {
            Success = true,
            Context = ctx
        };

        public static PortalTokenValidationResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }

    public sealed class PortalAccessContext
    {
        public Guid PortalAccessLinkId { get; set; }
        public byte PortalType { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? SupplierId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public sealed class PortalActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static PortalActionResult Ok(string message) => new() { Success = true, Message = message };
        public static PortalActionResult Fail(string message) => new() { Success = false, Message = message };
    }

    public sealed class CustomerPortalVm
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public List<CustomerPortalBillVm> Bills { get; set; } = new();
        public List<CustomerPortalInvoiceVm> Invoices { get; set; } = new();
        public List<CustomerPortalReturnRequestVm> ReturnRequests { get; set; } = new();
    }

    public sealed class CustomerPortalBillVm
    {
        public Guid PosBillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public string StoreName { get; set; } = "-";
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
    }

    public sealed class CustomerPortalInvoiceVm
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string WarehouseName { get; set; } = "-";
        public decimal TotalAmount { get; set; }
        public byte Status { get; set; }
    }

    public sealed class CustomerPortalReturnRequestVm
    {
        public Guid PortalReturnRequestId { get; set; }
        public string BillNo { get; set; } = "-";
        public DateTime RequestedAtUtc { get; set; }
        public string? Reason { get; set; }
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
    }

    public sealed class SupplierPortalVm
    {
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public List<SupplierPortalPoVm> PurchaseOrders { get; set; } = new();
    }

    public sealed class SupplierPortalPoVm
    {
        public Guid PurchaseId { get; set; }
        public string PurchaseNo { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public string WarehouseName { get; set; } = "-";
        public decimal TotalAmount { get; set; }
        public byte PurchaseStatus { get; set; }
        public int LineCount { get; set; }
        public byte ResponseStatus { get; set; }
        public DateTime? RespondedAtUtc { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? SupplierNote { get; set; }
    }
}
