using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopLedger.Data.DTOs.Auth;
using ShopLedger.Data.Models;

namespace ShopLedger.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class EmployeesController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userMgr;

        public EmployeesController(UserManager<ApplicationUser> userMgr)
        {
            _userMgr = userMgr;
        }

        // helper: فلترة الموظفين فقط
        private async Task<bool> IsEmployeeAsync(ApplicationUser u)
        {
            var roles = await _userMgr.GetRolesAsync(u);
            return roles.Contains("Employee", StringComparer.OrdinalIgnoreCase);
        }

        // GET /api/employees?includeDeleted=false
        [HttpGet]
        public async Task<IActionResult> GetEmployees([FromQuery] bool includeDeleted = false)
        {
            var users = await _userMgr.Users
                .Where(u => includeDeleted || !u.IsDeleted)
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var result = new List<object>();
            foreach (var u in users)
            {
                if (await IsEmployeeAsync(u))
                {
                    result.Add(new
                    {
                        u.Id,
                        u.UserName,
                        u.Email
                    });
                }
            }
            return Ok(result);
        }


        // GET /api/employees/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllEmployeesIncludingDeleted()
        {
            var employees = await _userMgr.GetUsersInRoleAsync("Employee");

            var list = employees
                .OrderBy(u => u.UserName)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.IsDeleted,
                    u.DeletedAtUtc
                });

            return Ok(list);
        }



        // GET /api/employees/deleted
        [HttpGet("deleted")]
        public async Task<IActionResult> GetSoftDeletedEmployees()
        {
            var employees = await _userMgr.GetUsersInRoleAsync("Employee");

            var list = employees
                .Where(u => u.IsDeleted)
                .OrderBy(u => u.UserName)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.IsDeleted,
                    u.DeletedAtUtc
                });

            return Ok(list);
        }




        // PUT /api/employees/{id}  (تعديل بيانات الموظف)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(string id, [FromBody] EmployeeUpdateDTO dto)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!await IsEmployeeAsync(user))
                return BadRequest("الحساب ليس موظفًا.");

            // لازم يكون في شيء واحد على الأقل للتعديل
            if (dto.UserName is null && dto.Email is null && dto.NewPassword is null)
                return BadRequest("أرسل حقلًا واحدًا على الأقل للتعديل.");

            var errors = new List<string>();

            // تعديل الاسم إن أُرسل
            if (dto.UserName is not null)
            {
                var newName = dto.UserName.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                    return BadRequest("اسم المستخدم لا يمكن أن يكون فارغًا.");

                if (!string.Equals(user.UserName, newName, StringComparison.Ordinal))
                {
                    var existsByName = await _userMgr.FindByNameAsync(newName);
                    if (existsByName != null && existsByName.Id != user.Id)
                        return BadRequest("اسم المستخدم مستخدم لحساب آخر.");

                    user.UserName = newName;
                }
            }

            // تعديل الإيميل إن أُرسل 
            if (dto.Email is not null)
            {
                var newEmail = dto.Email.Trim();
                if (string.IsNullOrWhiteSpace(newEmail))
                    return BadRequest("البريد الإلكتروني لا يمكن أن يكون فارغًا.");

                if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var existsByEmail = await _userMgr.FindByEmailAsync(newEmail);
                    if (existsByEmail != null && existsByEmail.Id != user.Id)
                        return BadRequest("هذا البريد مستخدم لحساب آخر.");

                    user.Email = newEmail;
                }
            }

            // حفظ تعديلات الاسم/الإيميل إن وُجدت
            var updateRes = await _userMgr.UpdateAsync(user);
            if (!updateRes.Succeeded)
                errors.AddRange(updateRes.Errors.Select(e => e.Description));

            // تعديل كلمة المرور إن أُرسلت
            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {

                var token = await _userMgr.GeneratePasswordResetTokenAsync(user);
                var passRes = await _userMgr.ResetPasswordAsync(user, token, dto.NewPassword);
                if (!passRes.Succeeded)
                    errors.AddRange(passRes.Errors.Select(e => e.Description));
            }

            if (errors.Count > 0)
                return BadRequest(string.Join("; ", errors));

            return NoContent();
        }

        // DELETE /api/employees/{id}/soft  (Soft Delete)
        [HttpDelete("{id}/soft")]
        public async Task<IActionResult> SoftDelete(string id)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user == null) return NotFound();
            if (!await IsEmployeeAsync(user))
                return BadRequest("الحساب ليس موظفًا.");

            if (user.IsDeleted) return NoContent();

            user.IsDeleted = true;
            user.DeletedAtUtc = DateTime.UtcNow;

            var res = await _userMgr.UpdateAsync(user);
            if (!res.Succeeded)
                return BadRequest(string.Join("; ", res.Errors.Select(e => e.Description)));

            return NoContent();
        }

        // POST /api/employees/{id}/restore  (استرجاع الموظف )
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(string id)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user == null) return NotFound();
            if (!await IsEmployeeAsync(user))
                return BadRequest("الحساب ليس موظفًا.");

            if (!user.IsDeleted) return NoContent();

            user.IsDeleted = false;
            user.DeletedAtUtc = null;

            var res = await _userMgr.UpdateAsync(user);
            if (!res.Succeeded)
                return BadRequest(string.Join("; ", res.Errors.Select(e => e.Description)));

            return NoContent();
        }
    }

}
