using Microsoft.EntityFrameworkCore;
using StudentExpenseTracker.Models;

namespace StudentExpenseTracker.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Budget> Budgets => Set<Budget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>().HasKey(user => user.Id);

            modelBuilder.Entity<Category>().HasKey(category => category.Id);
            modelBuilder.Entity<Expense>().HasKey(expense => expense.Id);
            modelBuilder.Entity<Budget>().HasKey(budget => budget.Id);

            modelBuilder.Entity<Expense>()
                .Property(expense => expense.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Budget>()
                .Property(budget => budget.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Food" },
                new Category { Id = 2, Name = "Transportation" },
                new Category { Id = 3, Name = "Housing" },
                new Category { Id = 4, Name = "Entertainment" },
                new Category { Id = 5, Name = "Education" },
                new Category { Id = 6, Name = "Shopping" },
                new Category { Id = 7, Name = "Health" },
                new Category { Id = 8, Name = "Other" }
            );
        }
    }
}