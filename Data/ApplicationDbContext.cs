using Microsoft.EntityFrameworkCore;
using ArkhamChangeRequest.Models;

namespace ArkhamChangeRequest.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ChangeRequest> ChangeRequests { get; set; }
        public DbSet<ChangeRequestAttachment> ChangeRequestAttachments { get; set; }
        public DbSet<ChangeRequestAudit> ChangeRequestAudits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the relationship between ChangeRequest and ChangeRequestAttachment
            modelBuilder.Entity<ChangeRequestAttachment>()
                .HasOne(a => a.ChangeRequest)
                .WithMany(cr => cr.AttachmentFiles)
                .HasForeignKey(a => a.ChangeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between ChangeRequest and ChangeRequestAudit
            modelBuilder.Entity<ChangeRequestAudit>()
                .HasOne(a => a.ChangeRequest)
                .WithMany(cr => cr.AuditTrail)
                .HasForeignKey(a => a.ChangeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure enums to be stored as strings
            modelBuilder.Entity<ChangeRequest>()
                .Property(e => e.ChangeType)
                .HasConversion<string>();

            modelBuilder.Entity<ChangeRequest>()
                .Property(e => e.Priority)
                .HasConversion<string>();

            modelBuilder.Entity<ChangeRequest>()
                .Property(e => e.Status)
                .HasConversion<string>();

            // Configure indexes for better performance
            modelBuilder.Entity<ChangeRequest>()
                .HasIndex(cr => cr.RequestorEmail);

            modelBuilder.Entity<ChangeRequest>()
                .HasIndex(cr => cr.CreatedDate);

            modelBuilder.Entity<ChangeRequest>()
                .HasIndex(cr => cr.Status);

            modelBuilder.Entity<ChangeRequestAudit>()
                .HasIndex(a => a.ChangeRequestId);

            modelBuilder.Entity<ChangeRequestAudit>()
                .HasIndex(a => a.ModifiedDate);
        }
    }
}
