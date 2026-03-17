using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers
{
    [Authorize(Roles = "Admin,Manager,HR")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Employees
        public async Task<IActionResult> Index(string? q, EmployeeStatus? status = null, string sort = "code", string dir = "asc", int page = 1, int pageSize = 20)
        {
            q = (q ?? "").Trim();
            ViewData["q"] = q;
            ViewData["status"] = status;
            ViewData["sort"] = sort;
            ViewData["dir"] = dir;
            ViewData["page"] = page;
            ViewData["pageSize"] = pageSize;

            ViewData["StatusOptions"] = new SelectList(Enum.GetValues<EmployeeStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }), "Value", "Text", status);

            var query = _context.Employees.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.EmployeeCode.Contains(q) ||
                    x.FirstName.Contains(q) ||
                    x.LastName.Contains(q)
                );
            }

            if (status is not null)
                query = query.Where(x => x.Status == status);

            var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sort?.ToLowerInvariant() switch
            {
                "firstname" => ascending ? query.OrderBy(x => x.FirstName) : query.OrderByDescending(x => x.FirstName),
                "lastname" => ascending ? query.OrderBy(x => x.LastName) : query.OrderByDescending(x => x.LastName),
                "joindate" => ascending ? query.OrderBy(x => x.JoinDate) : query.OrderByDescending(x => x.JoinDate),
                "status" => ascending ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
                _ => ascending ? query.OrderBy(x => x.EmployeeCode) : query.OrderByDescending(x => x.EmployeeCode)
            };

            if (page < 1) page = 1;
            if (pageSize is < 10 or > 200) pageSize = 20;

            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewData["total"] = total;
            ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
            ViewData["to"] = Math.Min(page * pageSize, total);
            ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);
            return View(data);
        }

        // GET: Employees/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.EmployeeId == id);

            if (employee == null) return NotFound();

            return View(employee);
        }

        // GET: Employees/Create
        public IActionResult Create()
        {
            ViewData["StatusOptions"] = new SelectList(Enum.GetValues<EmployeeStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }), "Value", "Text", EmployeeStatus.Active);
            return View();
        }

        // POST: Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,EmployeeCode,FirstName,LastName,JoinDate,Status")] Employee employee)
        {
            ViewData["StatusOptions"] = new SelectList(Enum.GetValues<EmployeeStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }), "Value", "Text", employee.Status);

            if (!ModelState.IsValid) return View(employee);

            employee.EmployeeId = Guid.NewGuid();
            _context.Add(employee);

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Employee.EmployeeCode), "Employee Code must be unique.");
                return View(employee);
            }
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            ViewData["StatusOptions"] = new SelectList(Enum.GetValues<EmployeeStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }), "Value", "Text", employee.Status);

            return View(employee);
        }

        // POST: Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("EmployeeId,EmployeeCode,FirstName,LastName,JoinDate,Status")] Employee employee)
        {
            if (id != employee.EmployeeId) return NotFound();
            ViewData["StatusOptions"] = new SelectList(Enum.GetValues<EmployeeStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }), "Value", "Text", employee.Status);
            if (!ModelState.IsValid) return View(employee);

            try
            {
                _context.Update(employee);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.EmployeeId)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Employee.EmployeeCode), "Employee Code must be unique.");
                return View(employee);
            }
        }

        // GET: Employees/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.EmployeeId == id);

            if (employee == null) return NotFound();

            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.EmployeeId == id);
        }
    }
}