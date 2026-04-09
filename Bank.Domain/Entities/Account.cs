using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bank.Domain.Entities
{
    public class Account : BaseEntity
    {
        [Column(TypeName = "decimal(18,4)")]
        public decimal Balance { get; set; }
        public string AccountNumber { get; set; }
        public string UserId { get; set; }
        public string CurrencyCode { get; set; }
        public bool IsActive { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
