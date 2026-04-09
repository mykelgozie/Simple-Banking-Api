using System.ComponentModel.DataAnnotations;

namespace Bank.Domain.Dtos.Request
{
    public class TransferRequest
    {
        [Required]
        public string SenderAccount { get; set; }
        [Required]
        public string ReceiverAccount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Amount must be non-negative")]
        public decimal Amount { get; set; }

        [Required]
        public string TransactionId { get; set; }

        [Required]
        public string UserId { get; set; }
    }
}