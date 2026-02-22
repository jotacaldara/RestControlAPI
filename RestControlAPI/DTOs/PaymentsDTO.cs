namespace RestControlAPI.DTOs
{
        public class PaymentHistoryDTO
        {
            public decimal TotalCommissions { get; set; }
            public List<PaymentItemDTO> Payments { get; set; } = new();
        }

        public class PaymentItemDTO
        {
            public int PaymentId { get; set; }
            public int? ReservationId { get; set; }
            public string CustomerName { get; set; }
            public decimal CommissionAmount { get; set; }
            public DateTime? Date { get; set; }
            public string Month { get; set; }
        }
    }
