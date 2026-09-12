using Microsoft.EntityFrameworkCore;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;

namespace StudentExpenseTracker.Services
{
    public class ExpenseService
    {
        private readonly AppDbContext _dbContext;

        public ExpenseService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public List<Category> GetCategories()
        {
            return _dbContext.Categories.OrderBy(category => category.Id).ToList();
        }

        public List<Expense> GetExpenses(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<Expense>();

            return _dbContext.Expenses
                .Where(expense => expense.UserId == userId)
                .OrderByDescending(expense => expense.Date)
                .ToList();
        }

        public Expense? GetExpenseById(int id, string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            return _dbContext.Expenses.FirstOrDefault(expense => expense.Id == id && expense.UserId == userId);
        }

        public void AddExpense(Expense expense, string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;

            expense.UserId = userId;
            _dbContext.Expenses.Add(expense);
            _dbContext.SaveChanges();
        }

        public void UpdateExpense(Expense updatedExpense, string userId)
        {
            var existing = GetExpenseById(updatedExpense.Id, userId);
            if (existing != null)
            {
                existing.Title = updatedExpense.Title;
                existing.Description = updatedExpense.Description;
                existing.Amount = updatedExpense.Amount;
                existing.Category = updatedExpense.Category;
                existing.CategoryId = updatedExpense.CategoryId;
                existing.Date = updatedExpense.Date;
                _dbContext.SaveChanges();
            }
        }

        public void DeleteExpense(int id, string userId)
        {
            var existing = _dbContext.Expenses.FirstOrDefault(expense => expense.Id == id && expense.UserId == userId);
            if (existing != null)
            {
                _dbContext.Expenses.Remove(existing);
                _dbContext.SaveChanges();
            }
        }

        public void SeedSampleData(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;

            if (_dbContext.Expenses.Any(expense => expense.UserId == userId))
                return;

            _dbContext.Expenses.AddRange(
                new Expense { Title = "Starbucks Coffee", Amount = 200m, Category = "Food", CategoryId = 1, Date = DateTime.Today, UserId = userId },
                new Expense { Title = "Grab Ride to School", Amount = 150m, Category = "Transportation", CategoryId = 2, Date = DateTime.Today.AddDays(-1), UserId = userId },
                new Expense { Title = "School Supplies", Amount = 850m, Category = "Education", CategoryId = 5, Date = DateTime.Today.AddDays(-2), UserId = userId },
                new Expense { Title = "Movie Night", Amount = 400m, Category = "Entertainment", CategoryId = 4, Date = DateTime.Today.AddDays(-3), UserId = userId }
            );
            _dbContext.SaveChanges();
        }

        public AppUser? FindByUsernameOrEmail(string value)
        {
            return _dbContext.Users.FirstOrDefault(user =>
                (user.UserName != null && user.UserName.Equals(value, StringComparison.OrdinalIgnoreCase)) ||
                (user.Email != null && user.Email.Equals(value, StringComparison.OrdinalIgnoreCase)));
        }

        public AppUser CreateUser(string email, string passwordHash)
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                PasswordHash = passwordHash
            };
            _dbContext.Users.Add(user);
            _dbContext.SaveChanges();
            return user;
        }
    }
}