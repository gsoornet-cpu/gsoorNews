using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class CaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_should_reject_missing_subject_name()
    {
        var act = () => Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "  ",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_missing_concern_description()
    {
        var act = () => Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_missing_applicant_phone()
    {
        var act = () => Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "   ",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_succeed_with_only_the_mandatory_fields()
    {
        // Spec §10 step 4: subject name is the only mandatory field besides
        // the applicant's own OTP-verified phone — subjectContactPhone is
        // optional and must be accepted as null.
        var countryId = Guid.NewGuid();
        var cityId = Guid.NewGuid();

        var @case = Case.Create(
            countryId, cityId,
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        @case.CountryId.Should().Be(countryId);
        @case.CityId.Should().Be(cityId);
        @case.SubjectName.Should().Be("Ahmed Mostafa");
        @case.SubjectContactPhone.Should().BeNull();
        @case.ApplicantPhoneE164.Should().Be("+201001234567");
        @case.Status.Should().Be(CaseStatus.NoVerifiedInformationYet);
        @case.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Create_should_normalize_whitespace_only_subject_contact_phone_to_null()
    {
        var @case = Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: "   ",
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        @case.SubjectContactPhone.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_should_change_status_and_stamp_LastModifiedAtUtc()
    {
        var @case = Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        var later = Now.AddHours(2);
        @case.UpdateStatus(CaseStatus.ContactAttemptInProgress, later);

        @case.Status.Should().Be(CaseStatus.ContactAttemptInProgress);
        @case.LastModifiedAtUtc.Should().Be(later);
    }

    [Fact]
    public void AssignTo_should_set_the_assignee_and_stamp_AssignedAtUtc_and_LastModifiedAtUtc()
    {
        var @case = Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        var assignedAt = Now.AddMinutes(1);
        @case.AssignTo("editor-1", assignedAt);

        @case.AssignedToCrisisEditorUserId.Should().Be("editor-1");
        @case.AssignedAtUtc.Should().Be(assignedAt);
        @case.LastModifiedAtUtc.Should().Be(assignedAt);
    }

    [Fact]
    public void AssignTo_should_reject_a_missing_crisis_editor_id()
    {
        var @case = Case.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            subjectName: "Ahmed Mostafa",
            subjectContactPhone: null,
            concernDescription: "hasn't answered calls in three days",
            applicantPhoneE164: "+201001234567",
            otpVerifiedAtUtc: Now,
            nowUtc: Now);

        var act = () => @case.AssignTo("  ", Now);

        act.Should().Throw<ArgumentException>();
    }
}
