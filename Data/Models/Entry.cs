namespace ShopLedger.Data.Models
{
    public enum EntryType
    {
        Purchase = 1, // مشتريات
        Expense = 2, // مصاريف
        Sale = 3  // مبيعات
    }

    public class Entry
    {
        public int Id { get; set; }
        public EntryType Type { get; set; }            
        public string Title { get; set; } = default!;  
        public decimal Amount { get; set; }             
        public string? Notes { get; set; }              
        public DateTime CreatedAtUtc { get; set; }      
        public string CreatedByUserId { get; set; } = default!;
        public ApplicationUser? CreatedByUser { get; set; }


        // --- تتبع التعديلات ---
        public bool IsAdminEdited { get; set; } = false;            // تم تعديلها بواسطة أدمن؟
        public DateTime? LastModifiedAtUtc { get; set; }       // وقت آخر تعديل
        public string? LastModifiedByUserId { get; set; }      // من عدّل آخر مرّة
    }
}
