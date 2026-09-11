using Microsoft.EntityFrameworkCore;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;

namespace StudentExpenseTracker.Services
{
    public class ExpenseService
    {
        private readonly AppDbContext _context;

        public ExpenseService(AppDbContext context)
        {
            _context = context;
        }

        public List<Expense> GetExpenses(string userId) =>
            _context.Expenses.Where(e => e.UserId == userId).ToList();

        public Expense? GetExpenseById(int id, string userId) =>
            _context.Expenses.FirstOrDefault(e => e.Id == id && e.UserId == userId);

        public void AddExpense(Expense expense, string userId)
        {
            expense.UserId = userId;
            _context.Expenses.Add(expense);
            _context.SaveChanges();
        }

        public void UpdateExpense(Expense updatedExpense, string userId)
        {
            var existing = _context.Expenses.FirstOrDefault(e => e.Id == updatedExpense.Id && e.UserId == userId);
            if (existing != null)
            {
                existing.Title = updatedExpense.Title;
                existing.Description = updatedExpense.Description;
                existing.Amount = updatedExpense.Amount;
                existing.Category = updatedExpense.Category;
                existing.Date = updatedExpense.Date;
                _context.SaveChanges();
            }
        }

        public void DeleteExpense(int id, string userId)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id && e.UserId == userId);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                _context.SaveChanges();
            }
        }

        public void SeedSampleData(string userId)
        {
            if (_context.Expenses.Any(e => e.UserId == userId))
                return;

            _context.Expenses.AddRange(
                new Expense { Title = "Books", Description = "Course Textbooks", Amount = 45.00m, Category = "School Supplies", Date = DateTime.Today.AddDays(-2), UserId = userId },
                new Expense { Title = "Lunch", Description = "Cafeteria Meal", Amount = 12.50m, Category = "Food", Date = DateTime.Today.AddDays(-1), UserId = userId },
                new Expense { Title = "Bus Pass", Description = "Monthly Transport", Amount = 20.00m, Category = "Transport", Date = DateTime.Today, UserId = userId }
            );
            _context.SaveChanges();
        }
    }
}