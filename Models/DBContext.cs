using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace BACKEND.Models;

public partial class DBContext : DbContext
{
    public DBContext()
    {
    }

    public DBContext(DbContextOptions<DBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Author> Authors { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentSummary> DocumentSummaries { get; set; }

    public virtual DbSet<Download> Downloads { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<FeedbackReply> FeedbackReplies { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    public virtual DbSet<PremiumPackage> PremiumPackages { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPremium> UserPremiums { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("latin1_swedish_ci")
            .HasCharSet("latin1");

        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("authors")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("categories")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("Created at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("comments")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DocumentId, "fk_comments_document");

            entity.HasIndex(e => e.UserId, "fk_comments_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Content)
                .HasColumnType("text")
                .HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Document).WithMany(p => p.Comments)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("fk_comments_document");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_comments_user");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("documents")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.AuthorId, "fk_documents_author");

            entity.HasIndex(e => e.CategoryId, "fk_documents_category");

            entity.HasIndex(e => e.PublisherId, "fk_documents_publisher");

            entity.HasIndex(e => e.CreatedBy, "fk_documents_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AuthorId)
                .HasColumnType("int(11)")
                .HasColumnName("author_id");
            entity.Property(e => e.CategoryId)
                .HasColumnType("int(11)")
                .HasColumnName("category_id");
            entity.Property(e => e.ConversionJobId).HasMaxLength(200);
            entity.Property(e => e.ConversionStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("int(11)")
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.IsPremium).HasColumnName("is_premium");
            entity.Property(e => e.PdfUrl)
                .HasMaxLength(500)
                .HasColumnName("pdf_url");
            entity.Property(e => e.PreviewPageLimit)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("preview_page_limit");
            entity.Property(e => e.PublisherId)
                .HasColumnType("int(11)")
                .HasColumnName("publisher_id");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .HasColumnName("title");
            entity.Property(e => e.TotalPages)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("total_pages");

            entity.HasOne(d => d.Author).WithMany(p => p.Documents)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_documents_author");

            entity.HasOne(d => d.Category).WithMany(p => p.Documents)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_documents_category");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Documents)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_documents_user");

            entity.HasOne(d => d.Publisher).WithMany(p => p.Documents)
                .HasForeignKey(d => d.PublisherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_documents_publisher");
        });

        modelBuilder.Entity<DocumentSummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("document_summaries")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DocumentId, "fk_summaries_document");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
            entity.Property(e => e.SummaryText)
                .HasColumnType("text")
                .HasColumnName("summary_text");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentSummaries)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("fk_summaries_document");
        });

        modelBuilder.Entity<Download>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("downloads")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DocumentId, "fk_downloads_document");

            entity.HasIndex(e => e.UserId, "fk_downloads_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
            entity.Property(e => e.DownloadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("downloaded_at");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Document).WithMany(p => p.Downloads)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("fk_downloads_document");

            entity.HasOne(d => d.User).WithMany(p => p.Downloads)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_downloads_user");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.DocumentId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("favorites")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DocumentId, "fk_favorites_document");

            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Document).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("fk_favorites_document");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_favorites_user");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("feedbacks")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "fk_feedbacks_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .HasColumnName("attachment_url");
            entity.Property(e => e.Category)
                .HasColumnType("enum('Bug','Request','Question','Other')")
                .HasColumnName("category");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.ResponseAt)
                .HasColumnType("datetime")
                .HasColumnName("response_at");
            entity.Property(e => e.ResponseMessage)
                .HasColumnType("text")
                .HasColumnName("response_message");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("sent_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'Pending'")
                .HasColumnType("enum('Pending','Answered','Closed')")
                .HasColumnName("status");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedbacks_user");
        });

        modelBuilder.Entity<FeedbackReply>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("feedback_replies")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_general_ci");

            entity.HasIndex(e => e.FeedbackId, "fk_replies_feedback");

            entity.HasIndex(e => e.UserId, "fk_replies_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FeedbackId)
                .HasColumnType("int(11)")
                .HasColumnName("feedback_id");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackReplies)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_replies_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackReplies)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_replies_user");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("payment_transactions");

            entity.HasIndex(e => e.PackageId, "package_id");

            entity.HasIndex(e => e.UserId, "user_id");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(10, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.PackageId)
                .HasColumnType("int(11)")
                .HasColumnName("package_id");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.ResponseData)
                .HasColumnType("text")
                .HasColumnName("response_data");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'pending'")
                .HasColumnName("status");
            entity.Property(e => e.TransactionCode)
                .HasMaxLength(100)
                .HasColumnName("transaction_code");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Package).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payment_transactions_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payment_transactions_ibfk_1");
        });

        modelBuilder.Entity<PremiumPackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("premium_packages");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.DurationDays)
                .HasColumnType("int(11)")
                .HasColumnName("duration_days");
            entity.Property(e => e.MaxDownloads)
                .HasColumnType("int(11)")
                .HasColumnName("max_downloads");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(10, 2)
                .HasColumnName("price");
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("publishers")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("ratings")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.DocumentId, "fk_ratings_document");

            entity.HasIndex(e => e.UserId, "fk_ratings_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
            entity.Property(e => e.Score)
                .HasColumnType("tinyint(4)")
                .HasColumnName("score");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Document).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("fk_ratings_document");

            entity.HasOne(d => d.User).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_ratings_user");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("roles")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.RoleName, "ux_roles_role_name").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(191)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PRIMARY");

            entity
                .ToTable("settings")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Key)
                .HasMaxLength(100)
                .HasColumnName("key");
            entity.Property(e => e.Value)
                .HasMaxLength(500)
                .HasColumnName("value");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("transactions")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "fk_transactions_user");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(10, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_transactions_user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("users")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.RoleId, "fk_users_role");

            entity.HasIndex(e => e.Email, "ux_users_email").IsUnique();

            entity.HasIndex(e => e.Username, "ux_users_username").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Avatar)
                .HasMaxLength(255)
                .HasColumnName("avatar");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.FailedLoginCount).HasColumnType("int(11)");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.LockoutEnd).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId)
                .HasColumnType("int(11)")
                .HasColumnName("role_id");
            entity.Property(e => e.Username)
                .HasMaxLength(191)
                .HasColumnName("username");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_users_role");
        });

        modelBuilder.Entity<UserPremium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("user_premiums");

            entity.HasIndex(e => e.PackageId, "package_id");

            entity.HasIndex(e => e.UserId, "user_id");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.DownloadsUsed)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("downloads_used");
            entity.Property(e => e.EndDate)
                .HasColumnType("datetime")
                .HasColumnName("end_date");
            entity.Property(e => e.PackageId)
                .HasColumnType("int(11)")
                .HasColumnName("package_id");
            entity.Property(e => e.StartDate)
                .HasColumnType("datetime")
                .HasColumnName("start_date");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");

            entity.HasOne(d => d.Package).WithMany(p => p.UserPremia)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_premiums_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.UserPremia)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_premiums_ibfk_1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
