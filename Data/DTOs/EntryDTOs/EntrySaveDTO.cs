using ShopLedger.Data.Models;

namespace ShopLedger.Data.DTOs.EntryDTOs
{


    public class EntrySaveDTO
    {
        public EntryType Type { get; set; }        // Purchase | Expense | Sale
        public string Title { get; set; } = default!;
        public decimal Amount { get; set; }        
        public string? Notes { get; set; }
    }
}
