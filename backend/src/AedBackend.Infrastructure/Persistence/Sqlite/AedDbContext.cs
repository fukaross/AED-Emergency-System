using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class AedDbContext : DbContext
{
    private const int GuestFollowupCodeMaxLength = 32;
    private const int GuestCloseTokenMaxLength = 64;
    private const int GuestAnonymousIdMaxLength = 100;

    public AedDbContext(DbContextOptions<AedDbContext> options) : base(options)
    {
    }

    public DbSet<ExperienceUserEntity> ExperienceUsers => Set<ExperienceUserEntity>();
    public DbSet<ExperienceUserSubscriptionEntity> ExperienceUserSubscriptions => Set<ExperienceUserSubscriptionEntity>();
    public DbSet<AdminUserEntity> AdminUsers => Set<AdminUserEntity>();
    public DbSet<AdminSessionEntity> AdminSessions => Set<AdminSessionEntity>();
    public DbSet<AdminUserProfileEntity> AdminUserProfiles => Set<AdminUserProfileEntity>();
    public DbSet<AdminAuditLogEntity> AdminAuditLogs => Set<AdminAuditLogEntity>();
    public DbSet<AedDeviceEntity> AedDevices => Set<AedDeviceEntity>();
    public DbSet<ResponderProfileEntity> ResponderProfiles => Set<ResponderProfileEntity>();
    public DbSet<VolunteerApplicationEntity> VolunteerApplications => Set<VolunteerApplicationEntity>();
    public DbSet<SchoolPersonProfileEntity> SchoolPersonProfiles => Set<SchoolPersonProfileEntity>();
    public DbSet<SchoolBindingApplicationEntity> SchoolBindingApplications => Set<SchoolBindingApplicationEntity>();
    public DbSet<ResponderClaimApplicationEntity> ResponderClaimApplications => Set<ResponderClaimApplicationEntity>();
    public DbSet<KnowledgeSummaryEntity> KnowledgeSummary => Set<KnowledgeSummaryEntity>();
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();
    public DbSet<CourseVideoEntity> CourseVideos => Set<CourseVideoEntity>();
    public DbSet<QuickActionEntity> QuickActions => Set<QuickActionEntity>();
    public DbSet<HelpRequestEntity> HelpRequests => Set<HelpRequestEntity>();
    public DbSet<HelpReportEntity> HelpReports => Set<HelpReportEntity>();
    public DbSet<RescueSessionEntity> RescueSessions => Set<RescueSessionEntity>();
    public DbSet<RescueSessionParticipantEntity> RescueSessionParticipants => Set<RescueSessionParticipantEntity>();
    public DbSet<RescueSessionMessageEntity> RescueSessionMessages => Set<RescueSessionMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureWeChat(modelBuilder);
        ConfigureAdmin(modelBuilder);
        ConfigureCoreData(modelBuilder);
        ConfigureClassroom(modelBuilder);
        ConfigureHelp(modelBuilder);
        ConfigureRescue(modelBuilder);
    }

    private static void ConfigureWeChat(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExperienceUserEntity>(e =>
        {
            e.ToTable("experience_users");
            e.HasKey(x => x.OpenId);
            e.HasIndex(x => x.SessionTokenHash);
            e.HasMany(x => x.Subscriptions).WithOne().HasForeignKey(x => x.OpenId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ExperienceUserSubscriptionEntity>(e =>
        {
            e.ToTable("experience_user_subscriptions");
            e.HasKey(x => new { x.OpenId, x.TemplateId });
        });
    }

    private static void ConfigureAdmin(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUserEntity>(e =>
        {
            e.ToTable("admin_users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedUsername).IsUnique();
            e.HasMany(x => x.Sessions).WithOne().HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AdminSessionEntity>(e =>
        {
            e.ToTable("admin_sessions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SessionHash);
        });
        modelBuilder.Entity<AdminUserProfileEntity>(e =>
        {
            e.ToTable("admin_user_profiles");
            e.HasKey(x => x.OpenId);
        });
        modelBuilder.Entity<AdminAuditLogEntity>(e =>
        {
            e.ToTable("admin_audit_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
        });
    }

    private static void ConfigureCoreData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AedDeviceEntity>(e =>
        {
            e.ToTable("aed_devices");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<ResponderProfileEntity>(e =>
        {
            e.ToTable("responder_profiles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RecordId).IsUnique();
            e.HasIndex(x => x.BoundOpenId);
        });
        modelBuilder.Entity<VolunteerApplicationEntity>(e =>
        {
            e.ToTable("volunteer_applications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OpenId, x.CreatedAt });
            e.HasOne<VolunteerApplicationEntity>().WithMany()
                .HasForeignKey(x => x.DerivedFromId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SchoolPersonProfileEntity>(e =>
        {
            e.ToTable("school_person_profiles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonType, x.Name });
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.StaffId);
        });
        modelBuilder.Entity<SchoolBindingApplicationEntity>(e =>
        {
            e.ToTable("school_binding_applications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OpenId, x.CreatedAt });
            e.HasOne<SchoolPersonProfileEntity>().WithMany()
                .HasForeignKey(x => x.SchoolProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ResponderClaimApplicationEntity>(e =>
        {
            e.ToTable("responder_claim_applications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OpenId, x.CreatedAt });
            e.HasOne<ResponderProfileEntity>().WithMany()
                .HasForeignKey(x => x.ResponderProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<KnowledgeSummaryEntity>(e =>
        {
            e.ToTable("knowledge_summary");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Items).WithOne().OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<KnowledgeStatItemEntity>(e =>
        {
            e.ToTable("knowledge_summary_items");
            e.HasKey(x => x.Id);
        });
    }

    private static void ConfigureClassroom(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseEntity>(e =>
        {
            e.ToTable("courses");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Videos).WithOne().HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CourseVideoEntity>().ToTable("course_videos").HasKey(x => x.Id);
        modelBuilder.Entity<CourseStepEntity>(e =>
        {
            e.ToTable("course_steps");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.KeyPoints).WithOne().HasForeignKey(x => x.CourseStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CourseStepKeyPointEntity>().ToTable("course_step_key_points").HasKey(x => x.Id);
        modelBuilder.Entity<QuickActionEntity>(e =>
        {
            e.ToTable("quick_actions");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.QuickActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<QuickActionStepEntity>(e =>
        {
            e.ToTable("quick_action_steps");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.KeyPoints).WithOne().HasForeignKey(x => x.QuickActionStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<QuickActionStepKeyPointEntity>().ToTable("quick_action_step_key_points").HasKey(x => x.Id);
    }

    private static void ConfigureHelp(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HelpRequestEntity>(e =>
        {
            e.ToTable("help_requests");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestNo).IsUnique();
            e.HasIndex(x => x.UserId)
                .HasDatabaseName("uk_help_active_user")
                .HasFilter("Status IN ('published', 'responding', 'pending_review')")
                .IsUnique();
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Responders).WithOne().HasForeignKey(x => x.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Timeline).WithOne().HasForeignKey(x => x.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Reports).WithOne().HasForeignKey(x => x.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GuestSession).WithOne().HasForeignKey<HelpGuestSessionEntity>(x => x.HelpRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HelpImageEntity>().ToTable("help_images").HasKey(x => x.Id);
        modelBuilder.Entity<HelpGuestSessionEntity>(e =>
        {
            e.ToTable("help_guest_sessions");
            e.HasKey(x => x.HelpRequestId);
            e.Property(x => x.FollowupCode).HasMaxLength(GuestFollowupCodeMaxLength);
            e.Property(x => x.CloseToken).HasMaxLength(GuestCloseTokenMaxLength);
            e.Property(x => x.AnonymousId).HasMaxLength(GuestAnonymousIdMaxLength);
        });
        modelBuilder.Entity<HelpResponderEntity>().ToTable("help_responders").HasKey(x => x.Id);
        modelBuilder.Entity<HelpTimelineEntity>().ToTable("help_timeline").HasKey(x => x.Id);
        modelBuilder.Entity<HelpReportEntity>(e =>
        {
            e.ToTable("help_reports");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.HelpReportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HelpReportImageEntity>().ToTable("help_report_images").HasKey(x => x.Id);
    }

    private static void ConfigureRescue(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RescueSessionEntity>(e =>
        {
            e.ToTable("rescue_sessions");
            e.HasKey(x => x.HelpRequestId);
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(x => x.HelpRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.HelpRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RescueSessionParticipantEntity>(e =>
        {
            e.ToTable("rescue_session_participants");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.HelpRequestId, x.OpenId })
                .HasDatabaseName("uk_rescue_participant")
                .IsUnique();
        });
        modelBuilder.Entity<RescueSessionMessageEntity>(e =>
        {
            e.ToTable("rescue_session_messages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.HelpRequestId, x.Seq })
                .HasDatabaseName("uk_rescue_message_seq")
                .IsUnique();
        });
    }
}
