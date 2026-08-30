using System.ComponentModel.DataAnnotations;

namespace StudentExpenseTracker.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = "General";

        public DateTime Date { get; set; } = DateTime.Today;
    }
}