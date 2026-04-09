using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bank.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public string AccountNumber { get; set; }
        public TransactionType TransactionType { get; set; }
        public TransactionStatus Status { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public string TraansactionId { get; set; }
        public string userId { get; set; }

    }
}
