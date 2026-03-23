namespace Web_System.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public string Position { get; set; }
    }
    namespace Web_System.Models
    {
        public class Usage
        {
            public int UsageId { get; set; }
            public string ResourceName { get; set; }
            public int Quantity { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
        }
    }
}