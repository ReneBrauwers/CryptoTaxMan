namespace ManagerApp.Models
{
    public sealed class CryptoCollection
    {
        public DateOnly CreatedOn { get; set; }
        public string Name { get; set; }
        public double Available { get; set; }
        public double BoughtAt { get; set; }
        public string Currency { get; set; }


    }
}
