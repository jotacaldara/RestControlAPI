namespace RestControlAPI.DTOs
{
    public class RestaurantDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Phone { get; set; }

        public decimal AverageRating { get; set; }

        public int TotalReviews { get; set; }

        public List<string> Images { get; set; } = new();
        public List<CategoryDTO> MenuCategories { get; set; } = new();


    }

    public class CategoryDTO
    {
        public string Name { get; set; }
        public List<ProductDTO> Products { get; set; } = new();
    }

    public class ProductDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
}
