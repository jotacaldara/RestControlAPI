namespace RestControlAPI.DTOs
{
    public class CreateOrderDTO
    {
        public int RestaurantId { get; set; }
        public int? TableId { get; set; }
        public int? ReservationId { get; set; }
        public int StaffId { get; set; } 
    }

    public class AddOrderItemDTO
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; } // "Sem cebola", etc.
    }

    public class OrderDetailDTO
    {
        public int OrderId { get; set; }
        public string Status { get; set; } // Pending, Prepared, Served, Paid
        public int TableNumber { get; set; }
        public string WaiterName { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemDTO> Items { get; set; }
    }

    public class OrderItemDTO
    {
        public int OrderItemId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice;
    }
}
