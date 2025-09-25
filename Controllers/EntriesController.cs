using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopLedger.Data;
using ShopLedger.Data.DTOs.EntryDTOs;
using ShopLedger.Data.Models;
using System.Security.Claims;

namespace ShopLedger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EntriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeZoneInfo _tz;

        public EntriesController(ApplicationDbContext context)
        {
            _context = context;
            _tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");
        }

        private string UserId =>
    User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue("Id")
    ?? throw new UnauthorizedAccessException("لم يتم العثور على هوية المستخدم في التوكن.");

        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("admin");

        private DateOnly TodayLocal()
        {
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _tz);
            return DateOnly.FromDateTime(nowLocal.Date);
        }

        private static (DateTime fromUtc, DateTime toUtc) DayBoundsUtc(DateOnly d, TimeZoneInfo tz)
        {
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MaxValue), tz);
            return (fromUtc, toUtc);
        }

        // GET /api/entries
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // اليوم المحلي دائمًا
            var d = TodayLocal();
            var (fromUtc, toUtc) = DayBoundsUtc(d, _tz);

            var q = _context.Entries
                .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

            if (!IsAdmin)
                q = q.Where(x => x.CreatedByUserId == UserId);

            if (IsAdmin)
            {
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

                var summary = new
                {
                    date = d,
                    purchasesTotal = purchases,
                    expensesTotal = expenses,
                    salesTotal = sales,
                    netTotal = sales - (purchases + expenses)
                };

                return Ok(new { items, summary });
            }
            else
            {
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
                        CreatedByUserName = x.CreatedByUser.UserName
                        // لا نرجع حقول التتبع للموظف
                    })
                    .ToListAsync();

                var purchases = await q.Where(x => x.Type == EntryType.Purchase).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var expenses = await q.Where(x => x.Type == EntryType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var sales = await q.Where(x => x.Type == EntryType.Sale).SumAsync(x => (decimal?)x.Amount) ?? 0;

                var summary = new
                {
                    date = d,
                    purchasesTotal = purchases,
                    expensesTotal = expenses,
                    salesTotal = sales,
                    netTotal = sales - (purchases + expenses)
                };

                return Ok(new { items, summary });
            }
        }


        // GET /api/entries/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var e = await _context.Entries
             .Include(x => x.CreatedByUser)    
                .FirstOrDefaultAsync(x => x.Id == id);

            if (e == null) return NotFound();

            if (!IsAdmin && e.CreatedByUserId != UserId)
                return Forbid();

            if (IsAdmin)
            {
                return Ok(new
                {
                    e.Id,
                    e.Type,
                    e.Title,
                    e.Amount,
                    e.Notes,
                    CreatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(e.CreatedAtUtc, _tz),
                    e.CreatedByUserId,
                    CreatedByUserName = e.CreatedByUser.UserName,
                    e.IsAdminEdited,
                    LastModifiedAtLocal = e.LastModifiedAtUtc == null
                        ? (DateTime?)null
                        : TimeZoneInfo.ConvertTimeFromUtc(e.LastModifiedAtUtc.Value, _tz),
                    e.LastModifiedByUserId
                });
            }
            else
            {
                return Ok(new
                {
                    e.Id,
                    e.Type,
                    e.Title,
                    e.Amount,
                    e.Notes,
                    CreatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(e.CreatedAtUtc, _tz),
                    e.CreatedByUserId,
                    CreatedByUserName = e.CreatedByUser.UserName,
                    // لا نُرجع حقول التتبع للموظف
                });
            }
        }


        // POST /api/entries   (إنشاء)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EntrySaveDTO dto)
        {
            var validation = Validate(dto);
            if (validation is not null) return BadRequest(validation);

            var entry = new Entry
            {
                Type = dto.Type,
                Title = dto.Title.Trim(),
                Amount = Math.Abs(dto.Amount), // نخزن موجب
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId
            };

            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry.Id);
        }

        // PATCH: /api/entries/{id}  (تعديل جزئي)
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Patch(int id, [FromBody] EntryPatchDTO dto)
        {
            var e = await _context.Entries.FindAsync(id);
            if (e == null) return NotFound();

            // نفس قيود الموظف: سجله + نفس اليوم
            if (!IsAdmin)
            {
                if (e.CreatedByUserId != UserId) return Forbid();
                var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(e.CreatedAtUtc, _tz);
                if (DateOnly.FromDateTime(createdLocal.Date) != TodayLocal())
                    return BadRequest("يسمح بالتعديل في نفس يوم الإدخال فقط.");
            }

            // لو أُرسل النوع
            if (dto.Type.HasValue)
            {
                if (!Enum.IsDefined(typeof(EntryType), dto.Type.Value))
                    return BadRequest("نوع الإدخال غير صالح.");
                e.Type = dto.Type.Value;
            }

            // لو أُرسل العنوان
            if (dto.Title is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Title))
                    return BadRequest("العنوان لا يمكن أن يكون فارغًا.");
                e.Title = dto.Title.Trim();
            }

            // لو أُرسل المبلغ
            if (dto.Amount.HasValue)
            {
                if (dto.Amount.Value <= 0)
                    return BadRequest("المبلغ يجب أن يكون أكبر من صفر.");
                e.Amount = Math.Abs(dto.Amount.Value); // نخزن موجب دائمًا
            }

            // لو أُرسلت الملاحظات (مسموح null لمسحها)
            if (dto.Notes is not null)
                e.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            if (IsAdmin && e.CreatedByUserId != UserId)
            {
                e.IsAdminEdited = true;
                e.LastModifiedAtUtc = DateTime.UtcNow;
                e.LastModifiedByUserId = UserId;
            }
            else
            {
                // غير الأدمن: حدّث فقط وقت التعديل وهوية المعدّل (بدون وسم)
                e.LastModifiedAtUtc = DateTime.UtcNow;
                e.LastModifiedByUserId = UserId;
            }


            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/entries/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var e = await _context.Entries.FindAsync(id);
            if (e == null) return NotFound();

            if (!IsAdmin)
            {
                if (e.CreatedByUserId != UserId)
                    return Forbid();

                var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(e.CreatedAtUtc, _tz);
                if (DateOnly.FromDateTime(createdLocal.Date) != TodayLocal())
                    return BadRequest("يسمح بالحذف في نفس يوم الإدخال فقط.");
            }

            _context.Entries.Remove(e);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Validation موحّد للإنشاء/التعديل
        private static string? Validate(EntrySaveDTO dto)
        {
            if (dto is null) return "البيانات مطلوبة.";
            if (string.IsNullOrWhiteSpace(dto.Title)) return "العنوان مطلوب.";
            if (dto.Amount <= 0) return "المبلغ يجب أن يكون أكبر من صفر.";
            if (!Enum.IsDefined(typeof(EntryType), dto.Type)) return "نوع الإدخال غير صالح.";
            return null;
        }
    }
}
