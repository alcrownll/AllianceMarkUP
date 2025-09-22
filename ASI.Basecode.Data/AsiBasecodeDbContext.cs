using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data
{
    public partial class AsiBasecodeDBContext : DbContext
    {
        public AsiBasecodeDBContext()
        {
        }

        public AsiBasecodeDBContext(DbContextOptions<AsiBasecodeDBContext> options)
            : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Student> Students { get; set; }
        public virtual DbSet<Teacher> Teachers { get; set; }
        public virtual DbSet<UserProfile> UserProfiles { get; set; }  

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.IdNumber).IsUnique();

                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IdNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.AccountStatus).IsRequired().HasMaxLength(20);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp");
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp");

                entity.HasOne(u => u.UserProfile)
                      .WithOne(p => p.User)
                      .HasForeignKey<UserProfile>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.StudentId);

                entity.Property(e => e.AdmissionType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Program).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
                entity.Property(e => e.YearLevel).IsRequired().HasMaxLength(20);
                entity.Property(e => e.StudentStatus).IsRequired().HasMaxLength(20);

                entity.HasOne(d => d.User)
                      .WithOne(p => p.Student)
                      .HasForeignKey<Student>(d => d.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Teacher>(entity =>
            {
                entity.HasKey(e => e.TeacherId);

                entity.Property(e => e.Position).IsRequired().HasMaxLength(50);

                entity.HasOne(d => d.User)
                      .WithOne(p => p.Teacher)
                      .HasForeignKey<Teacher>(d => d.TeacherId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
                entity.Property(e => e.MiddleName).HasMaxLength(100);
                entity.Property(e => e.Suffix).HasMaxLength(10);
                entity.Property(e => e.MobileNo).HasMaxLength(15);
                entity.Property(e => e.HomeAddress).HasMaxLength(255);
                entity.Property(e => e.Province).HasMaxLength(100);
                entity.Property(e => e.Municipality).HasMaxLength(100);
                entity.Property(e => e.Barangay).HasMaxLength(100);
                entity.Property(e => e.PlaceOfBirth).HasMaxLength(255);
                entity.Property(e => e.MaritalStatus).HasMaxLength(20);
                entity.Property(e => e.Gender).HasMaxLength(20);
                entity.Property(e => e.Religion).HasMaxLength(50);
                entity.Property(e => e.Citizenship).HasMaxLength(50);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
