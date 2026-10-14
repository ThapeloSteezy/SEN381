using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RequestCategory> RequestCategories => Set<RequestCategory>();
    public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();
    public DbSet<RequestFeedback> RequestFeedback => Set<RequestFeedback>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestNumber).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.RequestNumber).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.Requests)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubmittedBy)
                .WithMany()
                .HasForeignKey(x => x.SubmittedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RequestCategory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<RequestHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);

            entity.HasOne(x => x.ServiceRequest)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PerformedBy)
                .WithMany()
                .HasForeignKey(x => x.PerformedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RequestFeedback>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();

            entity.HasOne(x => x.ServiceRequest)
                .WithMany(x => x.Feedback)
                .HasForeignKey(x => x.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
