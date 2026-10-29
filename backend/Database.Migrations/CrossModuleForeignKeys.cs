using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Database.Migrations;

internal static class CrossModuleForeignKeys
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdvisorEvaluation>().HasOne<StudentGroup>()
            .WithOne().HasForeignKey<AdvisorEvaluation>(x => x.GroupId)
            .HasConstraintName("fk_advisor_evaluation_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AdvisorEvaluation>().HasOne<FinalReport>()
            .WithMany().HasForeignKey(x => x.FinalReportId)
            .HasConstraintName("fk_advisor_evaluation_final_reports_final_report_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AdvisorEvaluation>().HasOne<SubmissionFile>()
            .WithMany().HasForeignKey(x => x.SubmissionFileId)
            .HasConstraintName("fk_advisor_evaluation_submission_files_submission_file_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AdvisorEvaluation>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.EvaluatedBy)
            .HasConstraintName("fk_advisor_evaluation_users_evaluated_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AdvisorEvaluation>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.DecisionBy)
            .HasConstraintName("fk_advisor_evaluation_users_decision_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Council>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ApprovedBy)
            .HasConstraintName("fk_council_users_approved_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Council>().HasOne<KltnBatch>()
            .WithMany().HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_council_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CouncilAssignment>().HasOne<StudentGroup>()
            .WithOne().HasForeignKey<CouncilAssignment>(x => x.GroupId)
            .HasConstraintName("fk_council_assignment_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CouncilMember>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("fk_council_member_users_user_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CouncilMinute>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.LockedBy)
            .HasConstraintName("fk_council_minute_users_locked_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CouncilMinute>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.UnlockedBy)
            .HasConstraintName("fk_council_minute_users_unlocked_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GradingCriteria>().HasOne<KltnBatch>()
            .WithMany().HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_grading_criteria_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReviewerAssignment>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.AssignedBy)
            .HasConstraintName("fk_reviewer_assignment_users_assigned_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReviewerAssignment>().HasOne<StudentGroup>()
            .WithMany().HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_reviewer_assignment_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReviewerAssignment>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ReviewerId)
            .HasConstraintName("fk_reviewer_assignment_users_reviewer_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreSummary>().HasOne<StudentGroup>()
            .WithOne().HasForeignKey<ScoreSummary>(x => x.GroupId)
            .HasConstraintName("fk_score_summary_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScoreSummary>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.PublishedBy)
            .HasConstraintName("fk_score_summary_users_published_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Notification>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.RecipientId)
            .HasConstraintName("fk_notification_user_recipient_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FinalSubmission>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.AdvisorConfirmedBy)
            .HasConstraintName("fk_final_submission_user_advisor_confirmed_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<FinalSubmission>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ArchivedConfirmedBy)
            .HasConstraintName("fk_final_submission_user_archived_confirmed_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<MidtermReport>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ReviewedBy)
            .HasConstraintName("fk_midterm_report_user_reviewed_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ReviewGateConfiguration>().HasOne<KltnBatch>()
            .WithMany().HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_review_gate_configuration_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReviewGateConfiguration>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.CreatedBy)
            .HasConstraintName("fk_review_gate_configuration_user_created_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReviewGateConfiguration>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.UpdatedBy)
            .HasConstraintName("fk_review_gate_configuration_user_updated_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProgressMilestone>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.CreatedBy)
            .HasConstraintName("fk_progress_milestone_user_created_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StudentGroup>().HasOne<Topic>()
            .WithMany().HasForeignKey(x => x.TopicId)
            .HasConstraintName("fk_student_group_topics_topic_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StudentGroupMember>().HasOne<KltnBatch>()
            .WithMany().HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_student_group_member_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StudentGroupMember>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.StudentId)
            .HasConstraintName("fk_student_group_member_user_student_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StudentGroupMember>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.InvitedByStudentId)
            .HasConstraintName("fk_student_group_member_user_invited_by_student_id")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<StudentRegistrationPenalty>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.StudentId)
            .HasConstraintName("fk_student_registration_penalty_user_student_id")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SubmissionFile>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.UploadedBy)
            .HasConstraintName("fk_submission_file_user_uploaded_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SubmissionReview>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ReviewedBy)
            .HasConstraintName("fk_submission_review_user_reviewed_by")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Syllabus>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.ReviewedBy)
            .HasConstraintName("fk_syllabus_user_reviewed_by")
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Topic>().HasOne<User>()
            .WithMany().HasForeignKey(x => x.AdvisorId)
            .HasConstraintName("fk_topic_user_advisor_id")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Topic>().HasOne<KltnBatch>()
            .WithMany().HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_topic_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
