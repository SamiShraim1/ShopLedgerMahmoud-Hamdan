using Microsoft.AspNetCore.Identity;

namespace ShopLedger.Data.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }
    }
}
