namespace MakerPrompt.Shared.Models
{
    public class FilamentSpool
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public double Diameter { get; set; } = 1.75;
        public double TotalWeightGrams { get; set; } = 1000;
        public double RemainingWeightGrams { get; set; } = 1000;
        public decimal Cost { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; }
    }
}
