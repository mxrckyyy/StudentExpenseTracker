using System.ComponentModel.DataAnnotations;

namespace StudentExpenseTracker.Models
{
    public class Budget
    {
        public int Id { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Budget amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int Month { get; set; }

        public int Year { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int CategoryId { get; set; }
    }
}