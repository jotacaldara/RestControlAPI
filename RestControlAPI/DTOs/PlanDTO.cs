namespace RestControlAPI.DTOs
{
    public class PlanDTO
    {
        public int PlanId { get; set; }
        public string Name { get; set; }
        public decimal MonthlyPrice { get; set; }
        public decimal ReservationCommission { get; set; }
    }
}
