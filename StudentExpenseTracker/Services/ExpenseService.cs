using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;

namespace StudentExpenseTracker.Services
{
    public class ExpenseService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ExpenseService> _logger;

        public ExpenseService(AppDbContext dbContext, ILogger<ExpenseService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public List<Category> GetCategories()
        {
            try
            {
                return _dbContext.Categories.OrderBy(category => category.Id).ToList();
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(GetCategories));
                return new List<Category>();
            }
        }

        public List<Expense> GetExpenses(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<Expense>();

            try
            {
                return _dbContext.Expenses
                    .Where(expense => expense.UserId == userId)
                    .OrderByDescending(expense => expense.Date)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(GetExpenses));
                return new List<Expense>();
            }
        }

        public Expense? GetExpenseById(int id, string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            try
            {
                return _dbContext.Expenses.FirstOrDefault(expense => expense.Id == id && expense.UserId == userId);
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(GetExpenseById));
                return null;
            }
        }

        public bool AddExpense(Expense expense, string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                expense.UserId = userId;
                _dbContext.Expenses.Add(expense);
                _dbContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(AddExpense));
                return false;
            }
        }

        public bool UpdateExpense(Expense updatedExpense, string userId)
        {
            try
            {
                var existing = GetExpenseById(updatedExpense.Id, userId);
                if (existing == null)
                    return false;

                existing.Title = updatedExpense.Title;
                existing.Description = updatedExpense.Description;
                existing.Amount = updatedExpense.Amount;
                existing.Category = updatedExpense.Category;
                existing.CategoryId = updatedExpense.CategoryId;
                existing.Date = updatedExpense.Date;
                _dbContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(UpdateExpense));
                return false;
            }
        }

        public bool DeleteExpense(int id, string userId)
        {
            try
            {
                var existing = _dbContext.Expenses.FirstOrDefault(expense => expense.Id == id && expense.UserId == userId);
                if (existing == null)
                    return false;

                _dbContext.Expenses.Remove(existing);
                _dbContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(DeleteExpense));
                return false;
            }
        }

        public bool SeedSampleData(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                if (_dbContext.Expenses.Any(expense => expense.UserId == userId))
                    return true;

                _dbContext.Expenses.AddRange(
                    new Expense { Title = "Starbucks Coffee", Amount = 200m, Category = "Food", CategoryId = 1, Date = DateTime.Today, UserId = userId },
                    new Expense { Title = "Grab Ride to School", Amount = 150m, Category = "Transportation", CategoryId = 2, Date = DateTime.Today.AddDays(-1), UserId = userId },
                    new Expense { Title = "School Supplies", Amount = 850m, Category = "Education", CategoryId = 5, Date = DateTime.Today.AddDays(-2), UserId = userId },
                    new Expense { Title = "Movie Night", Amount = 400m, Category = "Entertainment", CategoryId = 4, Date = DateTime.Today.AddDays(-3), UserId = userId }
                );
                _dbContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(SeedSampleData));
                return false;
            }
        }

        public AppUser? FindByUsernameOrEmail(string value)
        {
            try
            {
                return _dbContext.Users.FirstOrDefault(user =>
                    (user.UserName != null && user.UserName.Equals(value, StringComparison.OrdinalIgnoreCase)) ||
                    (user.Email != null && user.Email.Equals(value, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(FindByUsernameOrEmail));
                return null;
            }
        }

        public AppUser? CreateUser(string email, string passwordHash)
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                PasswordHash = passwordHash
            };

            try
            {
                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();
                return user;
            }
            catch (Exception ex)
            {
                LogDatabaseFailure(ex, nameof(CreateUser));
                return null;
            }
        }

        // Logs the exact database failure. A "no such table" message is called
        // out explicitly because it almost always means the schema was never
        // created by the startup migration.
        private void LogDatabaseFailure(Exception ex, string operation)
        {
            var message = ex is DbUpdateException && ex.InnerException != null
                ? ex.InnerException.Message
                : ex.Message;

            if (message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Database error while running {Operation}: {Message} The schema is missing a table; the startup migration did not complete.",
                    operation, message);
            }
            else
            {
                _logger.LogError(ex, "Database error while running {Operation}: {Message}", operation, message);
            }
        }
    }
}
