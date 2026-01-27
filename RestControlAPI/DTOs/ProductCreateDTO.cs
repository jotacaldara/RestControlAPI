namespace RestControlAPI.DTOs
{
    public class ProductCreateDTO
    {
        public int RestaurantId { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
