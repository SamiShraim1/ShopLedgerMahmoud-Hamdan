namespace ShopLedger.Data.DTOs.Auth
{
    public class EmployeeUpdateDTO
    {
        public string UserName { get; set; } 
        public string Email { get; set; } 
        public string? NewPassword { get; set; }

    }
}
