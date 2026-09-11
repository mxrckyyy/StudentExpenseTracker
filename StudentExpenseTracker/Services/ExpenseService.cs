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
            SeedData();
        }

        private void SeedData()
        {
            if (_context.Expenses.Any())
                return;

            _context.Expenses.AddRange(
                new Expense { Title = "Books", Description = "Course Textbooks", Amount = 45.00m, Category = "School Supplies", Date = DateTime.Today.AddDays(-2) },
                new Expense { Title = "Lunch", Description = "Cafeteria Meal", Amount = 12.50m, Category = "Food", Date = DateTime.Today.AddDays(-1) },
                new Expense { Title = "Bus Pass", Description = "Monthly Transport", Amount = 20.00m, Category = "Transport", Date = DateTime.Today }
            );
            _context.SaveChanges();
        }

        public List<Expense> GetExpenses() => _context.Expenses.ToList();

        public Expense? GetExpenseById(int id) => _context.Expenses.FirstOrDefault(e => e.Id == id);

        public void AddExpense(Expense expense)
        {
            _context.Expenses.Add(expense);
            _context.SaveChanges();
        }

        public void UpdateExpense(Expense updatedExpense)
        {
            var existing = _context.Expenses.FirstOrDefault(e => e.Id == updatedExpense.Id);
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

        public void DeleteExpense(int id)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                _context.SaveChanges();
            }
        }
    }
}
