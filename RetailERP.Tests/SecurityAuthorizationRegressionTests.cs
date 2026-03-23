using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RetailERP.Controllers;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class SecurityAuthorizationRegressionTests
{
    [Fact]
    public async Task EInvoices_GenerateForBill_ShouldForbid_WhenBillBelongsToAnotherCompany()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var myCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var posBillId = Guid.NewGuid();

        db.PosBills.Add(new PosBill
        {
            PosBillId = posBillId,
            BillNo = "POS-SEC-001",
            StoreId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            CompanyId = otherCompanyId,
            Status = 2
        });
        await db.SaveChangesAsync();

        var controller = new EInvoicesController(db, new EInvoiceService(db));
        SetUser(controller, myCompanyId);

        var result = await controller.GenerateForBill(posBillId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task EInvoices_Cancel_ShouldForbid_WhenEInvoiceBelongsToAnotherCompany()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var myCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var eInvoiceId = Guid.NewGuid();

        db.EInvoices.Add(new EInvoice
        {
            EInvoiceId = eInvoiceId,
            CompanyId = otherCompanyId,
            Irn = "irn-test",
            AckNo = "ack-test",
            AckDate = DateTime.UtcNow,
            SignedInvoice = "{}",
            SignedQrCode = "{}",
            Status = 1,
            GeneratedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new EInvoicesController(db, new EInvoiceService(db));
        SetUser(controller, myCompanyId);

        var result = await controller.Cancel(eInvoiceId, "test");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task PortalAdmin_GenerateCustomerLink_ShouldForbid_WhenCustomerBelongsToAnotherCompany()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var myCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            CustomerId = customerId,
            Name = "Other Company Customer",
            CompanyId = otherCompanyId
        });
        await db.SaveChangesAsync();

        var controller = new PortalAdminController(db, new PortalService(db));
        SetUser(controller, myCompanyId);

        var result = await controller.GenerateCustomerLink(customerId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Sync_QueueChange_ShouldReject_WhenActionIsUnsupported()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var audit = new AuditService(db, new HttpContextAccessor());
        var sync = new SyncService(db, audit);
        var controller = new SyncController(db, sync);

        var result = await controller.QueueChange(new SyncController.QueueChangeReq
        {
            DeviceId = "DEV-1",
            EntityType = "Item",
            EntityId = "1",
            Action = "drop-table"
        });

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":false", payload);
        Assert.Contains("Unsupported action", payload);
    }

    [Fact]
    public async Task Sync_QueueChange_ShouldReject_WhenDeviceIdMissing()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var audit = new AuditService(db, new HttpContextAccessor());
        var sync = new SyncService(db, audit);
        var controller = new SyncController(db, sync);

        var result = await controller.QueueChange(new SyncController.QueueChangeReq
        {
            DeviceId = "",
            EntityType = "Item",
            EntityId = "1",
            Action = "create"
        });

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":false", payload);
        Assert.Contains("Invalid device id", payload);
    }

    [Fact]
    public async Task PaymentGateway_Refund_ShouldReject_WhenAlreadyRefunded_RepeatAttempt()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var companyId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        db.PosBills.Add(new PosBill
        {
            PosBillId = billId,
            BillNo = "POS-SEC-REFUND",
            StoreId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            CompanyId = companyId,
            Status = 1
        });

        db.Payments.Add(new Payment
        {
            PaymentId = paymentId,
            PosBillId = billId,
            Method = "Online",
            Amount = 100,
            IsGatewayPayment = true,
            RazorpayPaymentId = "pay_test_123",
            GatewayRefundId = "rfnd_existing"
        });
        await db.SaveChangesAsync();

        var razorpay = new RazorpayService(
            new HttpClient(),
            Options.Create(new RazorpayOptions()),
            NullLogger<RazorpayService>.Instance,
            new ServiceCollection().BuildServiceProvider());

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<RetailERP.Hubs.RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<Microsoft.AspNetCore.SignalR.IHubClients>().Object);
        var pos = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var controller = new PaymentGatewayController(
            db,
            razorpay,
            pos,
            NullLogger<PaymentGatewayController>.Instance);
        SetUser(controller, companyId);

        var result = await controller.Refund(new PaymentGatewayController.RefundReq
        {
            PaymentId = paymentId,
            Amount = 0
        });

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":false", payload);
        Assert.Contains("already been refunded", payload);
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static void SetUser(Controller controller, Guid companyId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("CompanyId", companyId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
