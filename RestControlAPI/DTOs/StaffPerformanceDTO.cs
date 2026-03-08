namespace RestControlAPI.DTOs
{
    public class StaffPerformanceDTO
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; }
        public int TotalOrdersServed { get; set; }
        public decimal TotalRevenueGenerated { get; set; }
        public List<RecentOrderDTO> RecentOrders { get; set; }
    }

    public class RecentOrderDTO
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
    }
}
