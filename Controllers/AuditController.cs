using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Manager")]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _db;
    public AuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var logs = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync();

        return View(logs);
    }
}