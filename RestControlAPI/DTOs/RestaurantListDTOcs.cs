namespace RestControlAPI.DTOs
{
    public class RestaurantListDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string ImageUrl { get; set; } 
        public string Description { get; set; }

        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }


    }
}
