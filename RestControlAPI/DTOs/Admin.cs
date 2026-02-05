namespace RestControlAPI.DTOs
{
    public class DashboardKpiDTO
    {
        public int TotalRestaurants { get; set; }
        public int TotalReservations { get; set; }
        public decimal TotalRevenue { get; set; } // Comissão da plataforma
        public int PendingApprovals { get; set; }

        public List<RevenueDataDTO> RevenueHistory { get; set; } = new();
    }

    public class RevenueDataDTO
    {
        public int Month { get; set; }
        public decimal Total { get; set; }
    }

    public class AdminRestaurantDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public bool IsActive { get; set; }
        public string CurrentPlan { get; set; }
    }
}
