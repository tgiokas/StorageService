using Microsoft.EntityFrameworkCore;

using StorageService.Domain.Entities;

namespace StorageService.Infrastructure.Database;

public class StorageDbContext : DbContext
{
    public StorageDbContext(DbContextOptions<StorageDbContext> options) : base(options)
    {
    }

    public DbSet<DocumentIndex> DocumentIndexes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentIndex>(entity =>
        {
            entity.ToTable("document_indexes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Bucket)
                .HasColumnName("bucket")
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Key)
                .HasColumnName("key")
                .IsRequired()
                .HasMaxLength(1024);

            entity.Property(e => e.FileName)
                .HasColumnName("file_name")
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(e => e.ContentType)
                .HasColumnName("content_type")
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Size)
                .HasColumnName("size");

            entity.Property(e => e.ETag)
                .HasColumnName("etag")
                .HasMaxLength(255);

            entity.Property(e => e.IsEncrypted)
                .HasColumnName("is_encrypted")
                .HasDefaultValue(false);

            entity.Property(e => e.UploadedBy)
                .HasColumnName("uploaded_by")
                .HasMaxLength(255);

            entity.Property(e => e.UploadedAt)
                .HasColumnName("uploaded_at");

            entity.Property(e => e.LastModified)
                .HasColumnName("last_modified");

            entity.Property(e => e.Tags)
                .HasColumnName("tags")
                .HasColumnType("jsonb");

            entity.Property(e => e.CustomMetadata)
                .HasColumnName("custom_metadata")
                .HasColumnType("jsonb");

            // Indexes for common query patterns
            entity.HasIndex(e => new { e.Bucket, e.Key })
                .IsUnique()
                .HasDatabaseName("ix_document_indexes_bucket_key");

            entity.HasIndex(e => e.Bucket)
                .HasDatabaseName("ix_document_indexes_bucket");

            entity.HasIndex(e => e.FileName)
                .HasDatabaseName("ix_document_indexes_file_name");

            entity.HasIndex(e => e.ContentType)
                .HasDatabaseName("ix_document_indexes_content_type");

            entity.HasIndex(e => e.UploadedBy)
                .HasDatabaseName("ix_document_indexes_uploaded_by");

            entity.HasIndex(e => e.UploadedAt)
                .HasDatabaseName("ix_document_indexes_uploaded_at");
        });
    }
}
