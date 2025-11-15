using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Threading;
using System.Threading.Tasks;

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
        public virtual DbSet<Course> Courses { get; set; }
        public virtual DbSet<AssignedCourse> AssignedCourses { get; set; }
        public virtual DbSet<ClassSchedule> ClassSchedules { get; set; }
        public virtual DbSet<Grade> Grades { get; set; }
        public virtual DbSet<Program> Programs { get; set; }
        public virtual DbSet<YearTerm> YearTerms { get; set; }
        public virtual DbSet<ProgramCourse> ProgramCourses { get; set; }
        public virtual DbSet<Notification> Notifications { get; set; }
        public virtual DbSet<CalendarEvent> CalendarEvents { get; set; }

        // password reset tokens
        public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

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

            // STUDENT
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.StudentId);

                entity.Property(e => e.StudentId)
                      .UseIdentityByDefaultColumn();

                entity.Property(e => e.AdmissionType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Program).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
                entity.Property(e => e.YearLevel).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Section).IsRequired().HasMaxLength(20);

                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne(d => d.User)
                      .WithOne(p => p.Student)
                      .HasForeignKey<Student>(d => d.UserId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_Students_Users_UserId");
            });

            // TEACHER
            modelBuilder.Entity<Teacher>(entity =>
            {
                entity.HasKey(e => e.TeacherId);

                entity.Property(e => e.TeacherId)
                      .UseIdentityByDefaultColumn();

                entity.Property(e => e.Position).IsRequired().HasMaxLength(50);

                entity.HasIndex(e => e.UserId).IsUnique();

                entity.HasOne(d => d.User)
                      .WithOne(p => p.Teacher)
                      .HasForeignKey<Teacher>(d => d.UserId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_Teachers_Users_UserId");
            });

            // USER PROFILE
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
                entity.Property(e => e.DateOfBirth).HasColumnType("date");
                entity.Property(e => e.PlaceOfBirth).HasMaxLength(255);
                entity.Property(e => e.MaritalStatus).HasMaxLength(20);
                entity.Property(e => e.Gender).HasMaxLength(20);
                entity.Property(e => e.Religion).HasMaxLength(50);
                entity.Property(e => e.Citizenship).HasMaxLength(50);
            });

             // COURSE
            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.CourseId);

                entity.Property(e => e.CourseCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(255);
            });

            // ASSIGNED COURSE
            modelBuilder.Entity<AssignedCourse>(entity =>
            {
                entity.HasKey(e => e.AssignedCourseId);

                entity.HasIndex(e => e.EDPCode).IsUnique();

                entity.Property(e => e.Type).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Semester).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SchoolYear).IsRequired().HasMaxLength(9);

                entity.HasOne(d => d.Course)
                      .WithMany(p => p.AssignedCourses)
                      .HasForeignKey(d => d.CourseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Teacher)
                      .WithMany(p => p.AssignedCourses)
                      .HasForeignKey(d => d.TeacherId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Program)
                      .WithMany(p => p.AssignedCourses)
                      .HasForeignKey(d => d.ProgramId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // CLASS SCHEDULE
            modelBuilder.Entity<ClassSchedule>(entity =>
            {
                entity.HasKey(e => e.ClassScheduleId);

                entity.Property(e => e.Room).IsRequired().HasMaxLength(50);

                entity.HasOne(d => d.AssignedCourse)
                      .WithMany(p => p.ClassSchedules)
                      .HasForeignKey(d => d.AssignedCourseId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // GRADE
            modelBuilder.Entity<Grade>(entity =>
            {
                entity.HasKey(e => e.GradeId);

                entity.HasOne(d => d.Student)
                      .WithMany(p => p.Grades)
                      .HasForeignKey(d => d.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.AssignedCourse)
                      .WithMany(p => p.Grades)
                      .HasForeignKey(d => d.AssignedCourseId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // NOTIFICATION
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.NotificationId);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp");

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.Kind, e.IsRead, e.CreatedAt });
                entity.HasIndex(e => new { e.UserId, e.Category, e.CreatedAt });
            });

            var utcConv = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // CALENDAR EVENT
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.HasKey(e => e.CalendarEventId);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Location).HasMaxLength(200);

                entity.Property(e => e.TimeZoneId)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasDefaultValue("Asia/Manila");

                entity.Property(e => e.StartUtc)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv);

                entity.Property(e => e.EndUtc)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv);

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv);

                entity.Property(e => e.LocalStartDate).HasColumnType("date");
                entity.Property(e => e.LocalEndDate).HasColumnType("date");

                entity.HasOne(e => e.User)
                      .WithMany(u => u.CalendarEvents)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // PROGRAM
            modelBuilder.Entity<Program>(entity =>
            {
                entity.HasKey(e => e.ProgramId);
                entity.Property(e => e.ProgramCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.ProgramName).IsRequired().HasMaxLength(100);

                entity.HasIndex(e => e.ProgramCode).IsUnique();
            });

            // YEARTERM
            modelBuilder.Entity<YearTerm>(entity =>
            {
                entity.ToTable("YearTerm");                 
                entity.HasKey(e => e.YearTermId);
                entity.Property(e => e.YearLevel).IsRequired();
                entity.Property(e => e.TermNumber).IsRequired();
                entity.HasIndex(e => new { e.YearLevel, e.TermNumber }).IsUnique();

                entity.HasData(
                    new YearTerm { YearTermId = 1, YearLevel = 1, TermNumber = 1 },
                    new YearTerm { YearTermId = 2, YearLevel = 1, TermNumber = 2 },
                    new YearTerm { YearTermId = 3, YearLevel = 2, TermNumber = 1 },
                    new YearTerm { YearTermId = 4, YearLevel = 2, TermNumber = 2 },
                    new YearTerm { YearTermId = 5, YearLevel = 3, TermNumber = 1 },
                    new YearTerm { YearTermId = 6, YearLevel = 3, TermNumber = 2 },
                    new YearTerm { YearTermId = 7, YearLevel = 4, TermNumber = 1 },
                    new YearTerm { YearTermId = 8, YearLevel = 4, TermNumber = 2 }
                );
            });

            // PROGRAMCOURSE
            modelBuilder.Entity<ProgramCourse>(entity =>
            {
                entity.HasKey(e => e.ProgramCourseId);

                entity.HasOne(e => e.Program)
                      .WithMany(p => p.ProgramCourses)
                      .HasForeignKey(e => e.ProgramId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Course)
                      .WithMany()
                      .HasForeignKey(e => e.CourseId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.YearTerm)
                      .WithMany(yt => yt.ProgramCourses)
                      .HasForeignKey(e => e.YearTermId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PrerequisiteCourse)
                      .WithMany()
                      .HasForeignKey(e => e.Prerequisite)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PASSWORD RESET TOKEN
            var utcConv2 = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Token)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasIndex(e => e.Token).IsUnique();

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv2);

                entity.Property(e => e.ExpiresAt)
                      .HasColumnType("timestamptz")
                      .HasConversion(utcConv2);

                entity.Property(e => e.UsedAt)
                      .HasColumnType("timestamptz")
                      .HasConversion(
                          v => v.HasValue
                                ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                                : v,
                          v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.PasswordResetTokens)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    if (entry.Entity.CreatedAt.Kind == DateTimeKind.Utc)
                        entry.Entity.CreatedAt = DateTime.SpecifyKind(entry.Entity.CreatedAt, DateTimeKind.Unspecified);

                    if (entry.Entity.UpdatedAt.Kind == DateTimeKind.Utc)
                        entry.Entity.UpdatedAt = DateTime.SpecifyKind(entry.Entity.UpdatedAt, DateTimeKind.Unspecified);
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }

    }
}
