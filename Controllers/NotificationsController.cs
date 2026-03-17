using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _notify;

    public NotificationsController(ApplicationDbContext db, NotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    // ── Notification Log ──
    [HttpGet]
    public async Task<IActionResult> Index(string? channel, string? status, string? q,
        string sort = "SentAtUtc", string dir = "desc", int page = 1)
    {
        const int pageSize = 30;
        var query = _db.NotificationLogs.AsNoTracking()
            .Include(n => n.Customer)
            .Include(n => n.Template)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(n => n.Channel == channel);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(n => n.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(n => n.Recipient.Contains(q) || n.Body.Contains(q));

        query = sort switch
        {
            "Channel" => dir == "asc" ? query.OrderBy(n => n.Channel) : query.OrderByDescending(n => n.Channel),
            "Status" => dir == "asc" ? query.OrderBy(n => n.Status) : query.OrderByDescending(n => n.Status),
            "Recipient" => dir == "asc" ? query.OrderBy(n => n.Recipient) : query.OrderByDescending(n => n.Recipient),
            _ => dir == "asc" ? query.OrderBy(n => n.SentAtUtc) : query.OrderByDescending(n => n.SentAtUtc),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Channel = channel;
        ViewBag.Status = status;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;

        var stats = await _db.NotificationLogs.AsNoTracking()
            .GroupBy(n => n.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.SentCount = stats.FirstOrDefault(s => s.Key == "Sent")?.Count ?? 0;
        ViewBag.FailedCount = stats.FirstOrDefault(s => s.Key == "Failed")?.Count ?? 0;
        ViewBag.QueuedCount = stats.FirstOrDefault(s => s.Key == "Queued")?.Count ?? 0;

        return View(items);
    }

    // ── Templates CRUD ──
    [HttpGet]
    public async Task<IActionResult> Templates(string? channel)
    {
        var query = _db.NotificationTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(t => t.Channel == channel);

        var templates = await query.OrderBy(t => t.Channel).ThenBy(t => t.Name).ToListAsync();
        ViewBag.Channel = channel;
        return View(templates);
    }

    [HttpGet]
    public IActionResult CreateTemplate() => View(new NotificationTemplate());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(NotificationTemplate model)
    {
        if (!ModelState.IsValid) return View(model);

        model.NotificationTemplateId = Guid.NewGuid();
        _db.NotificationTemplates.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Template '{model.Name}' created.";
        return RedirectToAction(nameof(Templates));
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplate(Guid id)
    {
        var t = await _db.NotificationTemplates.FirstOrDefaultAsync(x => x.NotificationTemplateId == id);
        if (t is null) return NotFound();
        return View(t);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(Guid id, NotificationTemplate model)
    {
        if (!ModelState.IsValid) return View(model);

        var t = await _db.NotificationTemplates.FirstOrDefaultAsync(x => x.NotificationTemplateId == id);
        if (t is null) return NotFound();

        t.Name = model.Name;
        t.Channel = model.Channel;
        t.Category = model.Category;
        t.Subject = model.Subject;
        t.Body = model.Body;
        t.IsActive = model.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Template updated.";
        return RedirectToAction(nameof(Templates));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await _db.NotificationTemplates.FirstOrDefaultAsync(x => x.NotificationTemplateId == id);
        if (t is null) return NotFound();
        _db.NotificationTemplates.Remove(t);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Template deleted.";
        return RedirectToAction(nameof(Templates));
    }

    // ── Send Campaign ──
    [HttpGet]
    public async Task<IActionResult> Campaign()
    {
        ViewBag.Templates = new SelectList(
            await _db.NotificationTemplates.AsNoTracking().Where(t => t.IsActive)
                .OrderBy(t => t.Channel).ThenBy(t => t.Name)
                .Select(t => new { t.NotificationTemplateId, Display = $"[{t.Channel}] {t.Name}" })
                .ToListAsync(),
            "NotificationTemplateId", "Display");

        ViewBag.CustomerCount = await _db.Customers.CountAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCampaign(string channel, string? subject, string body, string targetGroup)
    {
        var customers = targetGroup switch
        {
            "all" => await _db.Customers.AsNoTracking().ToListAsync(),
            "withPhone" => await _db.Customers.AsNoTracking().Where(c => c.Phone != null && c.Phone != "").ToListAsync(),
            "withEmail" => await _db.Customers.AsNoTracking().Where(c => c.Email != null && c.Email != "").ToListAsync(),
            _ => await _db.Customers.AsNoTracking().ToListAsync()
        };

        var result = await _notify.SendCampaignAsync(channel, subject ?? "", body, customers);

        TempData["Success"] = $"Campaign sent: {result.Sent} delivered, {result.Failed} failed, {result.Skipped} skipped (missing contact).";
        return RedirectToAction(nameof(Index));
    }

    // ── Quick Send (single notification) ──
    [HttpGet]
    public IActionResult Send() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string channel, string recipient, string? subject, string body)
    {
        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Recipient and message body are required.";
            return View();
        }

        await _notify.SendAsync(channel, recipient, subject, body);
        TempData["Success"] = $"Notification queued to {recipient} via {channel}.";
        return RedirectToAction(nameof(Index));
    }
}
