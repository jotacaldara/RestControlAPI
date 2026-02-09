using System.Text.Json.Serialization;

namespace RestControlAPI.DTOs
{
    public class OwnerDashboardDTO
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string City { get; set; }
        public int TotalReservations { get; set; }
        public int PendingReservations { get; set; }
        public int TotalReviews { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalProducts { get; set; }
    }

    // DTO para atualização do restaurante pelo Owner (apenas campos permitidos)
    public class RestaurantUpdateDTO
    {
        public string Description { get; set; }
        public string Phone { get; set; }
    }

    // DTO para listar reservas
    public class ReservationDTO
    {
        public int Id { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public int NumberOfPeople { get; set; }
        public string Status { get; set; } = "Pending";

        [JsonPropertyName("isReviewed")]
        public bool IsReviewed { get; set; }
    }
}