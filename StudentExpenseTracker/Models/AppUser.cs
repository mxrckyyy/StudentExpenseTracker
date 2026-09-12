namespace StudentExpenseTracker.Models
{
    public class AppUser
    {
        public string Id { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? Email { get; set; }

        public string PasswordHash { get; set; } = string.Empty;
    }
}