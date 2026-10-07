using FluentAssertions;
using Jusoor.Application.Atmaen;
using Jusoor.Application.Common.Exceptions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Atmaen;

/// <summary>
/// Covers the Atmaen Case core's business logic with IOtpService faked via
/// NSubstitute — the only way to test the approved/denied/provider-down
/// branches at all without live Twilio credentials. What this deliberately
/// does NOT prove: that the real TwilioOtpService and the real Key Vault
/// encryption behave correctly; those remain separately unverified.
/// </summary>
public class AtmaenCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static IDateTimeProvider FixedClock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static IOtpService OtpReturning(OtpVerificationStatus status)
    {
        var otp = Substitute.For<IOtpService>();
        otp.CheckVerificationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OtpCheckResult(status));
        return otp;
    }

    private static IIdentityService IdentityWithEditors(params string[] userIds)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.GetUserIdsInRoleAsync(NewsroomRole.CrisisEditor).Returns((IReadOnlyList<string>)userIds);
        return identity;
    }

    private static async Task<(Country Country, City City)> SeedLocationAsync(TestApplicationDbContext context)
    {
        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = "egypt" };
        var city = new City { CountryId = country.Id, NameAr = "القاهرة", NameEn = "Cairo", Slug = "cairo" };
        context.Countries.Add(country);
        context.Cities.Add(city);
        await context.SaveChangesAsync(default);
        return (country, city);
    }

    private static SubmitCaseCommand ValidSubmit(Guid countryId, Guid cityId) => new(
        countryId, cityId,
        SubjectName: "Ahmed Mostafa",
        SubjectContactPhone: null,
        ConcernDescription: "hasn't answered calls in three days",
        ApplicantPhoneE164: "+201001234567",
        OtpCode: "123456");

    private static Case NewAssignedCase(Guid countryId, Guid cityId, string editorUserId, DateTimeOffset createdAt)
    {
        var @case = Case.Create(countryId, cityId, "Ahmed Mostafa", null,
            "hasn't answered calls in three days", "+201001234567", createdAt, createdAt);
        @case.AssignTo(editorUserId, createdAt);
        return @case;
    }

    // ---- RequestCaseVerification ----

    [Fact]
    public async Task RequestCaseVerification_should_report_Sent_when_the_provider_accepts()
    {
        var otp = Substitute.For<IOtpService>();
        var handler = new RequestCaseVerificationCommandHandler(otp);

        var result = await handler.Handle(new RequestCaseVerificationCommand("+201001234567"), default);

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(RequestCaseVerificationOutcome.Sent);
        await otp.Received(1).SendVerificationAsync("+201001234567", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCaseVerification_should_report_ProviderUnavailable_when_the_provider_fails()
    {
        var otp = Substitute.For<IOtpService>();
        otp.SendVerificationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<OtpChallengeResult>(_ => throw new OtpProviderException("down"));
        var handler = new RequestCaseVerificationCommandHandler(otp);

        var result = await handler.Handle(new RequestCaseVerificationCommand("+201001234567"), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(RequestCaseVerificationOutcome.ProviderUnavailable);
    }

    // ---- SubmitCase ----

    [Fact]
    public async Task SubmitCase_should_create_and_assign_the_case_when_the_code_is_approved()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var handler = new SubmitCaseCommandHandler(
            context, OtpReturning(OtpVerificationStatus.Approved), IdentityWithEditors("editor-1"), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(country.Id, city.Id), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.Created);
        var saved = await context.Cases.SingleAsync();
        result.CaseId.Should().NotBeNull();
	saved.Id.Should().Be(result.CaseId!.Value);
        saved.AssignedToCrisisEditorUserId.Should().Be("editor-1");
        saved.OtpVerifiedAtUtc.Should().Be(Now);
        saved.Status.Should().Be(CaseStatus.NoVerifiedInformationYet);
    }

    [Theory]
    [InlineData(OtpVerificationStatus.Denied)]
    [InlineData(OtpVerificationStatus.Expired)]
    [InlineData(OtpVerificationStatus.Pending)]
    public async Task SubmitCase_should_not_create_a_case_unless_the_code_is_Approved(OtpVerificationStatus status)
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var handler = new SubmitCaseCommandHandler(
            context, OtpReturning(status), IdentityWithEditors("editor-1"), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(country.Id, city.Id), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.OtpNotVerified);
        (await context.Cases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitCase_should_report_ProviderUnavailable_when_the_OTP_provider_fails()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var otp = Substitute.For<IOtpService>();
        otp.CheckVerificationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<OtpCheckResult>(_ => throw new OtpProviderException("down"));
        var handler = new SubmitCaseCommandHandler(context, otp, IdentityWithEditors("editor-1"), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(country.Id, city.Id), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.ProviderUnavailable);
        (await context.Cases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitCase_should_reject_a_location_that_does_not_exist()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new SubmitCaseCommandHandler(
            context, OtpReturning(OtpVerificationStatus.Approved), IdentityWithEditors("editor-1"), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(Guid.NewGuid(), Guid.NewGuid()), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.InvalidLocation);
        (await context.Cases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitCase_should_report_NoCrisisEditorAvailable_when_nobody_holds_the_role()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var handler = new SubmitCaseCommandHandler(
            context, OtpReturning(OtpVerificationStatus.Approved), IdentityWithEditors(), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(country.Id, city.Id), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.NoCrisisEditorAvailable);
        (await context.Cases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitCase_should_assign_to_the_crisis_editor_with_the_fewest_assigned_cases()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        context.Cases.Add(NewAssignedCase(country.Id, city.Id, "busy-editor", Now.AddHours(-3)));
        context.Cases.Add(NewAssignedCase(country.Id, city.Id, "busy-editor", Now.AddHours(-2)));
        await context.SaveChangesAsync(default);
        var handler = new SubmitCaseCommandHandler(
            context, OtpReturning(OtpVerificationStatus.Approved),
            IdentityWithEditors("busy-editor", "idle-editor"), FixedClock(Now));

        var result = await handler.Handle(ValidSubmit(country.Id, city.Id), default);

        result.Outcome.Should().Be(SubmitCaseOutcome.Created);
        var created = await context.Cases.SingleAsync(c => c.Id == result.CaseId);
        created.AssignedToCrisisEditorUserId.Should().Be("idle-editor");
    }

    [Theory]
    [InlineData("12345")]            // no leading +
    [InlineData("+0123456789")]      // leading 0 after +
    [InlineData("+12")]              // too short
    [InlineData("not a phone")]
    public void SubmitCaseValidator_should_reject_a_malformed_applicant_phone(string phone)
    {
        var command = ValidSubmit(Guid.NewGuid(), Guid.NewGuid()) with { ApplicantPhoneE164 = phone };

        new SubmitCaseCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitCaseValidator_should_accept_a_valid_submission()
    {
        var command = ValidSubmit(Guid.NewGuid(), Guid.NewGuid());

        new SubmitCaseCommandValidator().Validate(command).IsValid.Should().BeTrue();
    }

    // ---- UpdateCaseStatus (assignment is the authorization boundary) ----

    [Fact]
    public async Task UpdateCaseStatus_should_succeed_for_the_assigned_crisis_editor()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var @case = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-1));
        context.Cases.Add(@case);
        await context.SaveChangesAsync(default);
        var handler = new UpdateCaseStatusCommandHandler(context, FixedClock(Now));

        var result = await handler.Handle(
            new UpdateCaseStatusCommand(@case.Id, CaseStatus.VerificationInProgress, "editor-1"), default);

        result.Outcome.Should().Be(UpdateCaseStatusOutcome.Updated);
        var reloaded = await context.Cases.SingleAsync();
        reloaded.Status.Should().Be(CaseStatus.VerificationInProgress);
        reloaded.LastModifiedByUserId.Should().Be("editor-1");
    }

    [Fact]
    public async Task UpdateCaseStatus_should_be_denied_for_a_crisis_editor_the_case_is_not_assigned_to()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var @case = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-1));
        context.Cases.Add(@case);
        await context.SaveChangesAsync(default);
        var handler = new UpdateCaseStatusCommandHandler(context, FixedClock(Now));

        var result = await handler.Handle(
            new UpdateCaseStatusCommand(@case.Id, CaseStatus.FamilyInformed, "editor-2"), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(UpdateCaseStatusOutcome.NotAssignedToYou);
        (await context.Cases.SingleAsync()).Status.Should().Be(CaseStatus.NoVerifiedInformationYet);
    }

    [Fact]
    public async Task UpdateCaseStatus_should_report_CaseNotFound_for_an_unknown_id()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new UpdateCaseStatusCommandHandler(context, FixedClock(Now));

        var result = await handler.Handle(
            new UpdateCaseStatusCommand(Guid.NewGuid(), CaseStatus.FamilyInformed, "editor-1"), default);

        result.Outcome.Should().Be(UpdateCaseStatusOutcome.CaseNotFound);
    }

    // ---- Queries (assignment filter is in the WHERE clause) ----

    [Fact]
    public async Task GetAssignedCaseById_should_return_detail_for_the_assigned_crisis_editor()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var @case = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-1));
        context.Cases.Add(@case);
        await context.SaveChangesAsync(default);
        var handler = new GetAssignedCaseByIdQueryHandler(context);

        var detail = await handler.Handle(new GetAssignedCaseByIdQuery(@case.Id, "editor-1"), default);

        detail.Should().NotBeNull();
        detail!.CaseId.Should().Be(@case.Id);
        detail.SubjectName.Should().Be("Ahmed Mostafa");
        detail.ApplicantPhoneE164.Should().Be("+201001234567");
        detail.CityNameEn.Should().Be("Cairo");
    }

    [Fact]
    public async Task GetAssignedCaseById_should_return_null_for_a_case_assigned_to_someone_else()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var @case = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-1));
        context.Cases.Add(@case);
        await context.SaveChangesAsync(default);
        var handler = new GetAssignedCaseByIdQueryHandler(context);

        var detail = await handler.Handle(new GetAssignedCaseByIdQuery(@case.Id, "editor-2"), default);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAssignedCases_should_return_only_my_cases_oldest_first()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var (country, city) = await SeedLocationAsync(context);
        var newer = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-1));
        var older = NewAssignedCase(country.Id, city.Id, "editor-1", Now.AddHours(-5));
        var someoneElses = NewAssignedCase(country.Id, city.Id, "editor-2", Now.AddHours(-3));
        context.Cases.AddRange(newer, older, someoneElses);
        await context.SaveChangesAsync(default);
        var handler = new GetMyAssignedCasesQueryHandler(context);

        var result = await handler.Handle(new GetMyAssignedCasesQuery("editor-1"), default);

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.CaseId).Should().Equal(older.Id, newer.Id);
        result.Items.Should().NotContain(i => i.CaseId == someoneElses.Id);
    }
}
