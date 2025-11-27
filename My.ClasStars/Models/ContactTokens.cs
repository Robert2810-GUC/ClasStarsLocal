namespace My.ClasStars.Models
{
    /// <summary>
    /// Lightweight token holder for matching images to contacts.
    /// </summary>
    public class ContactTokens
    {
        public int ID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PersonID { get; set; }
    }
}
