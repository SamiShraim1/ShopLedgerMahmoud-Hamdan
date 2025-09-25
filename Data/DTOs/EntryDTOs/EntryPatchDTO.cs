using ShopLedger.Data.Models;

namespace ShopLedger.Data.DTOs.EntryDTOs
{
    public class EntryPatchDTO
    {
        public EntryType? Type { get; set; }   // اختياري
        public string? Title { get; set; }     // اختياري
        public decimal? Amount { get; set; }   // اختياري (لو أُرسل يجب > 0)
        public string? Notes { get; set; }     // اختياري (null = بدون ملاحظات)
    }
}
