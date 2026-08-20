using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>Maps an Academy section.</summary>
public sealed class AcademySectionConfiguration : IEntityTypeConfiguration<AcademySection>
{
    public void Configure(EntityTypeBuilder<AcademySection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.SectionsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(section => section.Id);

        builder.Property(section => section.Id)
            .HasConversion(CreatorValueConverters.Id<AcademySectionId>())
            .ValueGeneratedNever();

        builder.Property(section => section.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(section => section.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(section => section.Description)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

        builder.Property(section => section.Icon)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.HasIndex(section => section.Key)
            .IsUnique()
            .HasDatabaseName("ux_academy_sections_key");

        builder.HasIndex(section => new { section.IsPublished, section.SortOrder })
            .HasDatabaseName("ix_academy_sections_order");

        builder.Ignore(section => section.DomainEvents);
    }
}

/// <summary>Maps a course, its tags and what it wants done first.</summary>
public sealed class AcademyCourseConfiguration : IEntityTypeConfiguration<AcademyCourse>
{
    public void Configure(EntityTypeBuilder<AcademyCourse> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.CoursesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(course => course.Id);

        builder.Property(course => course.Id)
            .HasConversion(CreatorValueConverters.Id<AcademyCourseId>())
            .ValueGeneratedNever();

        builder.Property(course => course.Slug)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(course => course.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(course => course.Summary)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(course => course.SectionKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength);

        builder.Property(course => course.Level)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(course => course.CoverImageUrl)
            .HasMaxLength(CreatorPersistenceConstants.UrlLength);

        builder.Property(course => course.Icon)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(course => course.AwardsBadgeKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength);

        builder.HasIndex(course => course.Slug)
            .IsUnique()
            .HasDatabaseName("ux_academy_courses_slug");

        // The catalogue filter the register's route takes: ?section=&level=
        builder.HasIndex(course =>
                new { course.IsPublished, course.SectionKey, course.Level, course.SortOrder })
            .HasDatabaseName("ix_academy_courses_catalogue");

        builder.OwnsMany(course => course.Tags, tag =>
        {
            tag.ToTable(CreatorPersistenceConstants.CourseTagsTable, CreatorModuleMetadata.Schema);

            tag.HasKey("AcademyCourseId", nameof(CourseTag.Value));

            tag.Property(value => value.Value)
                .HasMaxLength(CreatorLimits.MaxTagLength)
                .IsRequired();
        });

        builder.OwnsMany(course => course.Prerequisites, prerequisite =>
        {
            prerequisite.ToTable(
                CreatorPersistenceConstants.CoursePrerequisitesTable, CreatorModuleMetadata.Schema);

            prerequisite.HasKey("AcademyCourseId", nameof(CoursePrerequisite.CourseSlug));

            prerequisite.Property(value => value.CourseSlug)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();
        });

        builder.Ignore(course => course.DomainEvents);
    }
}

/// <summary>
/// Maps a lesson and its structured blocks.
/// <para>
/// <b>There is no column anywhere in these tables that could hold markup.</b> A block is a kind, some
/// bounded text and a route; the closed <c>LessonBlockKind</c> is stored as a string so an unknown
/// value could never be written, and a renderer therefore never has to decide what to do with one.
/// </para>
/// </summary>
public sealed class AcademyLessonConfiguration : IEntityTypeConfiguration<AcademyLesson>
{
    public void Configure(EntityTypeBuilder<AcademyLesson> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.LessonsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(lesson => lesson.Id);

        builder.Property(lesson => lesson.Id)
            .HasConversion(CreatorValueConverters.Id<AcademyLessonId>())
            .ValueGeneratedNever();

        builder.Property(lesson => lesson.CourseId)
            .HasConversion(CreatorValueConverters.Id<AcademyCourseId>())
            .IsRequired();

        builder.Property(lesson => lesson.QuizId)
            .HasConversion(CreatorValueConverters.Id<AcademyQuizId>());

        builder.Property(lesson => lesson.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(lesson => lesson.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(lesson => lesson.Kind)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(lesson => new { lesson.CourseId, lesson.Key })
            .IsUnique()
            .HasDatabaseName("ux_academy_lessons_key");

        builder.HasIndex(lesson => new { lesson.CourseId, lesson.SortOrder })
            .HasDatabaseName("ix_academy_lessons_order");

        builder.OwnsMany(lesson => lesson.Blocks, block =>
        {
            block.ToTable(
                CreatorPersistenceConstants.LessonBlocksTable, CreatorModuleMetadata.Schema);

            block.HasKey("AcademyLessonId", nameof(LessonBlock.SortOrder));

            // An integer inside a composite key is filled in by nobody. EF believes it generates
            // one, emits an UPDATE where an INSERT was meant, and the failure arrives on the second
            // write rather than the first — DSP3002, and the trap this platform has on record.
            block.Property(value => value.SortOrder).ValueGeneratedNever();

            block.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            block.Property(value => value.Tone)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength);

            block.Property(value => value.Text)
                .HasMaxLength(CreatorLimits.MaxBlockTextLength);

            block.Property(value => value.Title)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength);

            block.Property(value => value.Route)
                .HasMaxLength(CreatorPersistenceConstants.UrlLength);
        });

        builder.OwnsMany(lesson => lesson.BlockItems, item =>
        {
            item.ToTable(
                CreatorPersistenceConstants.LessonBlockItemsTable, CreatorModuleMetadata.Schema);

            item.HasKey(
                "AcademyLessonId",
                nameof(LessonBlockItem.BlockSortOrder),
                nameof(LessonBlockItem.SortOrder));

            item.Property(value => value.BlockSortOrder).ValueGeneratedNever();
            item.Property(value => value.SortOrder).ValueGeneratedNever();

            item.Property(value => value.Text)
                .HasMaxLength(CreatorLimits.MaxBlockTextLength)
                .IsRequired();
        });

        builder.Ignore(lesson => lesson.DomainEvents);
        builder.Ignore(lesson => lesson.CanBeCompletedByAssertion);
    }
}

/// <summary>
/// Maps a quiz, its questions and — in one column that never leaves this aggregate — its answers.
/// </summary>
public sealed class AcademyQuizConfiguration : IEntityTypeConfiguration<AcademyQuiz>
{
    public void Configure(EntityTypeBuilder<AcademyQuiz> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.QuizzesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(quiz => quiz.Id);

        builder.Property(quiz => quiz.Id)
            .HasConversion(CreatorValueConverters.Id<AcademyQuizId>())
            .ValueGeneratedNever();

        builder.Property(quiz => quiz.LessonId)
            .HasConversion(CreatorValueConverters.Id<AcademyLessonId>())
            .IsRequired();

        builder.Property(quiz => quiz.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.HasIndex(quiz => quiz.Key)
            .IsUnique()
            .HasDatabaseName("ux_academy_quizzes_key");

        builder.HasIndex(quiz => quiz.LessonId)
            .HasDatabaseName("ix_academy_quizzes_lesson");

        builder.OwnsMany(quiz => quiz.Questions, question =>
        {
            question.ToTable(
                CreatorPersistenceConstants.QuizQuestionsTable, CreatorModuleMetadata.Schema);

            question.HasKey(value => value.QuestionId);

            question.Property(value => value.QuestionId)
                .HasConversion(CreatorValueConverters.Id<AcademyQuizQuestionId>())
                .ValueGeneratedNever();

            question.Property(value => value.Prompt)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();

            question.Property(value => value.Explanation)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength);
        });

        builder.OwnsMany(quiz => quiz.Options, option =>
        {
            option.ToTable(
                CreatorPersistenceConstants.QuizOptionsTable, CreatorModuleMetadata.Schema);

            option.HasKey(value => value.OptionId);
            option.Property(value => value.OptionId).ValueGeneratedNever();

            option.Property(value => value.QuestionId)
                .HasConversion(CreatorValueConverters.Id<AcademyQuizQuestionId>())
                .IsRequired();

            option.Property(value => value.Label)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();

            option.HasIndex(value => value.QuestionId)
                .HasDatabaseName("ix_academy_quiz_options_question");
        });

        builder.Ignore(quiz => quiz.DomainEvents);
    }
}

/// <summary>
/// Maps one creator's progress through one course, with the content version each lesson was
/// completed at.
/// </summary>
public sealed class CreatorLearningProgressConfiguration
    : IEntityTypeConfiguration<CreatorLearningProgress>
{
    public void Configure(EntityTypeBuilder<CreatorLearningProgress> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CreatorPersistenceConstants.LearningProgressTable, CreatorModuleMetadata.Schema);

        builder.HasKey(progress => progress.Id);

        builder.Property(progress => progress.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorLearningProgressId>())
            .ValueGeneratedNever();

        builder.Property(progress => progress.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(progress => progress.CourseId)
            .HasConversion(CreatorValueConverters.Id<AcademyCourseId>())
            .IsRequired();

        builder.Property(progress => progress.CertificateId)
            .HasConversion(CreatorValueConverters.Id<CreatorCertificateId>());

        builder.Property(progress => progress.Revision).IsConcurrencyToken();

        builder.Property(progress => progress.CourseSlug)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(progress => progress.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(progress => progress.CompletionGrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        builder.HasIndex(progress => new { progress.CreatorId, progress.CourseId })
            .IsUnique()
            .HasDatabaseName("ux_creator_learning_progress_course");

        builder.HasIndex(progress => new { progress.CreatorId, progress.State })
            .HasDatabaseName("ix_creator_learning_progress_state");

        builder.HasIndex(progress => new { progress.CreatorId, progress.IsSaved })
            .HasDatabaseName("ix_creator_learning_progress_saved");

        builder.OwnsMany(progress => progress.CompletedLessons, lesson =>
        {
            lesson.ToTable(
                CreatorPersistenceConstants.CompletedLessonsTable, CreatorModuleMetadata.Schema);

            lesson.HasKey("CreatorLearningProgressId", nameof(CompletedLesson.LessonId));

            lesson.Property(value => value.LessonId)
                .HasConversion(CreatorValueConverters.Id<AcademyLessonId>())
                .IsRequired();
        });

        builder.OwnsMany(progress => progress.Attempts, attempt =>
        {
            attempt.ToTable(
                CreatorPersistenceConstants.QuizAttemptsTable, CreatorModuleMetadata.Schema);

            attempt.HasKey("CreatorLearningProgressId", nameof(QuizAttempt.QuizId));

            attempt.Property(value => value.QuizId)
                .HasConversion(CreatorValueConverters.Id<AcademyQuizId>())
                .IsRequired();
        });

        builder.Ignore(progress => progress.DomainEvents);
    }
}

/// <summary>Maps an issued certificate.</summary>
public sealed class CreatorCertificateConfiguration : IEntityTypeConfiguration<CreatorCertificate>
{
    public void Configure(EntityTypeBuilder<CreatorCertificate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.CertificatesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(certificate => certificate.Id);

        builder.Property(certificate => certificate.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorCertificateId>())
            .ValueGeneratedNever();

        builder.Property(certificate => certificate.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(certificate => certificate.CourseId)
            .HasConversion(CreatorValueConverters.Id<AcademyCourseId>())
            .IsRequired();

        builder.Property(certificate => certificate.CourseTitle)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(certificate => certificate.RecipientName)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(certificate => certificate.VerificationReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength)
            .IsRequired();

        // One certificate per creator per course. A second would be a second claim about the world.
        builder.HasIndex(certificate => new { certificate.CreatorId, certificate.CourseId })
            .IsUnique()
            .HasDatabaseName("ux_creator_certificates_course");

        // What somebody else checks a certificate by.
        builder.HasIndex(certificate => certificate.VerificationReference)
            .IsUnique()
            .HasDatabaseName("ux_creator_certificates_reference");

        builder.Ignore(certificate => certificate.DomainEvents);
    }
}
