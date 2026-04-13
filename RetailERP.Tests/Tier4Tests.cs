using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using MimeKit;
using RetailERP.Services;
using RetailERP.Services.BackgroundJobs;
using RetailERP.Data;
using RetailERP.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RetailERP.Tests;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_ShouldCallSmtpClientWithCorrectParams()
    {
        var opts = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com",
            Port = 587,
            UseStartTls = true,
            User = "testuser",
            Password = "testpassword",
            FromName = "Test RetailERP"
        });

        var mockClient = new Mock<MailKit.Net.Smtp.ISmtpClient>();
        
        // Setup mock to record the sent message
        MimeMessage sentMessage = null;
        mockClient.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<MailKit.ITransferProgress>()))
                  .Callback<MimeMessage, CancellationToken, MailKit.ITransferProgress>((msg, _, _) => sentMessage = msg)
                  .Returns(Task.CompletedTask);
                  
        // Return completed tasks for other methods
        mockClient.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MailKit.Security.SecureSocketOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockClient.Setup(c => c.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockClient.Setup(c => c.DisconnectAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new SmtpEmailSender(opts, () => mockClient.Object);

        await sender.SendEmailAsync("customer@example.com", "Test Subject", "<p>Hello</p>");

        // Verify SMTP connections and auth
        mockClient.Verify(c => c.ConnectAsync("smtp.example.com", 587, MailKit.Security.SecureSocketOptions.StartTls, It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.AuthenticateAsync("testuser", "testpassword", It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.DisconnectAsync(true, It.IsAny<CancellationToken>()), Times.Once);

        // Verify Message
        Assert.NotNull(sentMessage);
        Assert.Equal("Test Subject", sentMessage.Subject);
        Assert.Contains("customer@example.com", sentMessage.To.Mailboxes.First().Address);
        Assert.Contains("testuser", sentMessage.From.Mailboxes.First().Address); 
        Assert.Contains("Hello", sentMessage.HtmlBody);
    }
}

public class SyncQueueWorkerTests
{
    private ApplicationDbContext GetDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task ProcessPendingAsync_ShouldMarkValidLogAsSynced()
    {
        using var db = GetDb();
        var syncLogId = Guid.NewGuid();
        
        db.SyncLogs.Add(new SyncLog
        {
            SyncLogId = syncLogId,
            CompanyId = Guid.NewGuid(),
            EntityType = "Item",
            EntityId = Guid.NewGuid().ToString(),
            Payload = "{\"Name\": \"Test\"}",
            Operation = "Create",
            Status = 1, // Pending
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var spMock = new Mock<IServiceProvider>();

        spMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(db);
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var worker = new SyncQueueWorker(scopeFactoryMock.Object, new NullLogger<SyncQueueWorker>());

        // Using reflection to call the private method ProcessPendingAsync
        var method = typeof(SyncQueueWorker).GetMethod("ProcessPendingAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method.Invoke(worker, new object[] { CancellationToken.None });

        var updatedLog = await db.SyncLogs.FindAsync(syncLogId);
        Assert.Equal(2, updatedLog.Status); // Synced
        Assert.NotNull(updatedLog.SyncedAtUtc);
    }
}
