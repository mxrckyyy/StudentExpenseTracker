using StudentExpenseTracker.Models;

namespace StudentExpenseTracker.Services
{
    public class ExpenseService
    {
        private readonly List<Expense> _expenses = new();
        private int _nextId = 1;

        public ExpenseService()
        {
            AddExpense(new Expense { Title = "Books", Description = "Course Textbooks", Amount = 45.00m, Category = "School Supplies", Date = DateTime.Today.AddDays(-2) });
            AddExpense(new Expense { Title = "Lunch", Description = "Cafeteria Meal", Amount = 12.50m, Category = "Food", Date = DateTime.Today.AddDays(-1) });
            AddExpense(new Expense { Title = "Bus Pass", Description = "Monthly Transport", Amount = 20.00m, Category = "Transport", Date = DateTime.Today });
        }

        public List<Expense> GetExpenses() => _expenses;

        public Expense? GetExpenseById(int id) => _expenses.FirstOrDefault(e => e.Id == id);

        public void AddExpense(Expense expense)
        {
            expense.Id = _nextId++;
            _expenses.Add(expense);
        }

        public void UpdateExpense(Expense updatedExpense)
        {
            var existing = GetExpenseById(updatedExpense.Id);
            if (existing != null)
            {
                existing.Title = updatedExpense.Title;
                existing.Description = updatedExpense.Description;
                existing.Amount = updatedExpense.Amount;
                existing.Category = updatedExpense.Category;
                existing.Date = updatedExpense.Date;
            }
        }

        public void DeleteExpense(int id)
        {
            var expense = GetExpenseById(id);
            if (expense != null)
            {
                _expenses.Remove(expense);
            }
        }
    }
}