using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopLedger.Data;
using ShopLedger.Data.Models;

namespace ShopLedger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeZoneInfo _tz;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
            _tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");
        }

        private (DateTime fromUtc, DateTime toUtc) DayBoundsUtc(DateOnly d)
        {
            var fromLocal = d.ToDateTime(TimeOnly.MinValue);
            var toLocal = d.ToDateTime(TimeOnly.MaxValue);
            return (TimeZoneInfo.ConvertTimeToUtc(fromLocal, _tz),
                    TimeZoneInfo.ConvertTimeToUtc(toLocal, _tz));
        }

        // 1) Daily Report
        [HttpGet("daily")]
        public async Task<IActionResult> Daily([FromQuery] DateOnly date, [FromQuery] string? userId)
        {
            var (fromUtc, toUtc) = DayBoundsUtc(date);

            var q = _context.Entries
                .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

            if (!string.IsNullOrEmpty(userId))
                q = q.Where(x => x.CreatedByUserId == userId);

            var items = await q
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Type,
                    x.Title,
                    x.Amount,
                    x.Notes,
                    CreatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(x.CreatedAtUtc, _tz),
                    x.CreatedByUserId,
                    CreatedByUserName = x.CreatedByUser.UserName,
                    x.IsAdminEdited,
                    LastModifiedAtLocal = x.LastModifiedAtUtc == null
                        ? (DateTime?)null
                        : TimeZoneInfo.ConvertTimeFromUtc(x.LastModifiedAtUtc.Value, _tz),
                    x.LastModifiedByUserId
                })
                .ToListAsync();

            var purchases = await q.Where(x => x.Type == EntryType.Purchase).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var expenses = await q.Where(x => x.Type == EntryType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var sales = await q.Where(x => x.Type == EntryType.Sale).SumAsync(x => (decimal?)x.Amount) ?? 0;

            var totals = new
            {
                purchasesTotal = purchases,
                expensesTotal = expenses,
                salesTotal = sales,
                netTotal = sales - (purchases + expenses)
            };

            return Ok(new { date, items, totals });
        }

        // 2) Range Report (daily grouped)
        [HttpGet("range")]
        public async Task<IActionResult> Range([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? userId)
        {
            var days = new System.Collections.Generic.List<object>();

            DateOnly d = from;
            while (d <= to)
            {
                var (fromUtc, toUtc) = DayBoundsUtc(d);
                var q = _context.Entries
                    .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

                if (!string.IsNullOrEmpty(userId))
                    q = q.Where(x => x.CreatedByUserId == userId);

                var purchases = await q.Where(x => x.Type == EntryType.Purchase).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var expenses = await q.Where(x => x.Type == EntryType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var sales = await q.Where(x => x.Type == EntryType.Sale).SumAsync(x => (decimal?)x.Amount) ?? 0;

                days.Add(new
                {
                    date = d,
                    purchasesTotal = purchases,
                    expensesTotal = expenses,
                    salesTotal = sales,
                    netTotal = sales - (purchases + expenses)
                });

                d = d.AddDays(1);
            }

            var grandPurchases = days.Sum(x => (decimal)x.GetType().GetProperty("purchasesTotal")!.GetValue(x)!);
            var grandExpenses = days.Sum(x => (decimal)x.GetType().GetProperty("expensesTotal")!.GetValue(x)!);
            var grandSales = days.Sum(x => (decimal)x.GetType().GetProperty("salesTotal")!.GetValue(x)!);

            var grandTotals = new
            {
                purchasesTotal = grandPurchases,
                expensesTotal = grandExpenses,
                salesTotal = grandSales,
                netTotal = grandSales - (grandPurchases + grandExpenses)
            };

            return Ok(new { from, to, days, grandTotals });
        }

        // 3) Summary Report (totals only)
        [HttpGet("summary")]
        public async Task<IActionResult> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? userId)
        {
            var fromLocal = from.ToDateTime(TimeOnly.MinValue);
            var toLocal = to.ToDateTime(TimeOnly.MaxValue);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, _tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, _tz);

            var q = _context.Entries
                .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

            if (!string.IsNullOrEmpty(userId))
                q = q.Where(x => x.CreatedByUserId == userId);

            var purchases = await q.Where(x => x.Type == EntryType.Purchase).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var expenses = await q.Where(x => x.Type == EntryType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var sales = await q.Where(x => x.Type == EntryType.Sale).SumAsync(x => (decimal?)x.Amount) ?? 0;

            var totals = new
            {
                purchasesTotal = purchases,
                expensesTotal = expenses,
                salesTotal = sales,
                netTotal = sales - (purchases + expenses)
            };

            return Ok(new { from, to, totals });
        }


        // 4) By-User Report
        [HttpGet("by-user")]
        public async Task<IActionResult> ByUser([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var fromLocal = from.ToDateTime(TimeOnly.MinValue);
            var toLocal = to.ToDateTime(TimeOnly.MaxValue);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, _tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, _tz);

            var q = _context.Entries
                .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

            var users = await q
                .GroupBy(x => new { x.CreatedByUserId, x.CreatedByUser.UserName })
                .Select(g => new
                {
                    UserId = g.Key,
                    UserName = g.Key.UserName,
                    PurchasesTotal = g.Where(x => x.Type == EntryType.Purchase).Sum(x => x.Amount),
                    ExpensesTotal = g.Where(x => x.Type == EntryType.Expense).Sum(x => x.Amount),
                    SalesTotal = g.Where(x => x.Type == EntryType.Sale).Sum(x => x.Amount)
                })
                .ToListAsync();

            var enriched = users.Select(u => new
            {
                u.UserId,
                u.UserName,
                PurchasesTotal = u.PurchasesTotal,
                ExpensesTotal = u.ExpensesTotal,
                SalesTotal = u.SalesTotal,
                NetTotal = u.SalesTotal - (u.PurchasesTotal + u.ExpensesTotal)
            });

            return Ok(new { from, to, users = enriched });
        }
    }
}
