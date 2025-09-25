using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopLedger.Data.Models;

namespace ShopLedger.Data.Utils
{


    public class SeedData 
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeedData(ApplicationDbContext context,
                        RoleManager<IdentityRole> roleManager,
                        UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task IdentityDataSeedingAsync()
        {
            if (_context.Database.GetPendingMigrations().Any())
            {
                _context.Database.Migrate();
            }

            if (!await _roleManager.Roles.AnyAsync())
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
                await _roleManager.CreateAsync(new IdentityRole("Employee"));
            }
            if (!await _userManager.Users.AnyAsync())
            {
                var user1 = new ApplicationUser
                {
                    Email = "MahmoudHamdan@gmail.com",
                    UserName = "MahmoudHamdan",

                };
                var user2 = new ApplicationUser
                {
                    Email = "sami@gmail.com",
                    UserName = "samiShreem",

                };
                var user3 = new ApplicationUser
                {
                    Email = "ahmad@gmail.com",
                    UserName= "ahmad",

                };

                await _userManager.CreateAsync(user1, "Pass@1212");
                await _userManager.CreateAsync(user2, "Pass@1212");
                await _userManager.CreateAsync(user3, "Pass@1212");


                await _userManager.AddToRoleAsync(user1, "Admin");
                await _userManager.AddToRoleAsync(user2, "Employee");
                await _userManager.AddToRoleAsync(user3, "Employee");


            }
            await _context.SaveChangesAsync();
        }
    }

}
