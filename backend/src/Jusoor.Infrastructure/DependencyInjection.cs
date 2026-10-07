using System.Net.Http;
using System.Text;
using Hangfire;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Common;
using Jusoor.Infrastructure.Identity;
using Jusoor.Infrastructure.Ingestion;
using Jusoor.Infrastructure.Otp;
using Jusoor.Infrastructure.Persistence;
using Jusoor.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Jusoor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // `configuration` here is `WebApplicationBuilder.Configuration` — a
        // live, mutable ConfigurationManager, not a snapshot. Both reads
        // below used to call .GetConnectionString(...)/.Get<JwtOptions>()
        // EAGERLY, right here, which runs before builder.Build(). That's
        // fine for `docker compose up` (env vars are already in the process
        // environment before Program.cs starts), but breaks under
        // WebApplicationFactory-based integration tests, which inject their
        // config overrides only at Build() time — so the eager read ran
        // before the override existed. Fix: capture `configuration` in the
        // closures below and read it lazily, inside the lambdas that
        // actually consume it — those run after Build(), by which point
        // both real and test configuration sources are fully merged.
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Baseline Identity password policy. Atmaen/CMS-specific
                // hardening (MFA for editorial roles per spec §22) is added
                // in Phase 2/3 alongside the features that need it.
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // --- OTP provider (Phase 2 decision 1, approved: Twilio Verify) ---
        // Registered here, behind IOtpService, so nothing outside this method
        // ever names "Twilio" directly. Singleton for the same reason as
        // JwtTokenService above: config-driven, holds one internal client
        // built once from that config, no per-request state. Nothing in this
        // slice resolves IOtpService yet (the Atmaen Case submission endpoint
        // that will is a later slice) — this registration exists so that
        // endpoint can simply take a constructor dependency on IOtpService
        // when it's built, without also having to wire this up then.
        if (configuration.GetValue<bool>("UseLocalOtp"))
	{
    		services.AddSingleton<IOtpService, LocalOtpService>();
	}
	else
	{
    		services.AddSingleton<IOtpService, TwilioOtpService>();
	}


        // --- Field-level encryption for Atmaen Case data (Phase 2 decision 2,
        // approved: EF Core value converters + Azure Key Vault + envelope
        // encryption). Singleton for a reason that is NOT just style
        // consistency this time: ApplicationDbContext.OnModelCreating builds
        // EF Core's value converters from this exact injected instance, and
        // that model is built once and cached for the DbContext type's
        // lifetime — see ApplicationDbContext's own comment on why this
        // specific registration lifetime is load-bearing, not cosmetic.
        if (configuration.GetValue<bool>("UseLocalFieldEncryption"))
	{
    		services.AddSingleton<IFieldEncryptionService, LocalFieldEncryptionService>();
	}
	else
	{
    		services.AddSingleton<IFieldEncryptionService, LocalFieldEncryptionService>();

	}


        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            // Phase 1's minimal, honest slice of the Phase 3 permission
            // matrix that NewsroomRole's own comment says not to invent
            // early: exactly one named policy, for exactly the one action
            // this slice needs to gate. Named (not inline role-checks
            // scattered across controllers) so Phase 3 can redefine who
            // satisfies "CanResolveHumanReview" — e.g. adding FactChecker,
            // or splitting it per content category — without touching any
            // controller. The role set itself (senior editorial leadership)
            // is a reasonable default for "override the relevance engine's
            // uncertainty on what publishes," not a business rule handed
            // down from the spec; revisit it in Phase 3 once the full
            // Role×Action matrix exists.
            options.AddPolicy(AuthorizationPolicies.CanResolveHumanReview, policy =>
                policy.RequireRole(
                    NewsroomRole.SeniorEditor,
                    NewsroomRole.ManagingEditor,
                    NewsroomRole.EditorInChief,
                    NewsroomRole.SystemAdmin));

            // Atmaen Case queue/detail/status endpoints — CrisisEditor ONLY.
            // SystemAdmin is deliberately absent: the approved Phase 3
            // decision is that SystemAdmin has no ordinary Case-content
            // access (audited break-glass only, execution-order step 8, not
            // built yet). This policy gates who can reach the endpoints;
            // "only Cases assigned to you" is enforced inside each handler.
            options.AddPolicy(AuthorizationPolicies.CanAccessAtmaenCaseQueue, policy =>
                policy.RequireRole(NewsroomRole.CrisisEditor));

            // Phase 3, Slice 15 — CMS role gates from the approved
            // Role×Action matrix (docs/decisions/PHASE3_ROLE_ACTION_MATRIX_DRAFT.md,
            // "Developer corrections"). These decide who may reach an
            // endpoint; per-article ownership (Reporter edit-own,
            // state rules) is enforced in the handlers via EditorialArticleAccess.
            // SystemAdmin can publish/unpublish (audited break-glass) but cannot
            // create/edit.
            options.AddPolicy(AuthorizationPolicies.CanAccessCms, policy =>
                policy.RequireRole(EditorialArticleAccess.CmsRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanEditEditorialSeo, policy =>
                policy.RequireRole(NewsroomRole.SeoEditor, NewsroomRole.SeniorEditor, NewsroomRole.ManagingEditor, NewsroomRole.EditorInChief));
            options.AddPolicy(AuthorizationPolicies.CanManageEditorialTaxonomy, policy =>
                policy.RequireRole(NewsroomRole.EditorInChief));
            options.AddPolicy(AuthorizationPolicies.CanCreateEditorialArticle, policy =>
                policy.RequireRole(EditorialArticleAccess.CreatorRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanPublishEditorialArticle, policy =>
                policy.RequireRole(EditorialArticleAccess.PublisherRoles.ToArray()));

            // Phase 3, Slice 19 — workflow gates (decision D1). State and
            // ownership rules are enforced in EditorialArticleAccess.
            options.AddPolicy(AuthorizationPolicies.CanRequestEditorialRevision, policy =>
                policy.RequireRole(EditorialArticleAccess.ReviewerRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanApproveEditorialArticle, policy =>
                policy.RequireRole(EditorialArticleAccess.ApproverRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanIssueEditorialCorrection, policy =>
                policy.RequireRole(EditorialArticleAccess.CorrectionRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanManageEditorialPublicationState, policy =>
                policy.RequireRole(EditorialArticleAccess.RetractionRoles.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanReinstateRetractedEditorialArticle, policy =>
                policy.RequireRole(EditorialArticleAccess.ReinstateRoles.ToArray()));

            // Phase 3, Slice 17 — remaining approved-matrix rows with endpoints.
            // CanResolveHumanReview (above) is intentionally UNCHANGED: its role
            // set (SeniorEditor, ManagingEditor, EditorInChief, SystemAdmin) is
            // exactly the matrix row "Approve human-review-queue item".
            options.AddPolicy(AuthorizationPolicies.CanViewIncomingStories, policy =>
                policy.RequireRole(NewsroomRole.All.ToArray()));
            options.AddPolicy(AuthorizationPolicies.CanManageSources, policy =>
                policy.RequireRole(NewsroomRole.ManagingEditor, NewsroomRole.EditorInChief, NewsroomRole.SystemAdmin));
            options.AddPolicy(AuthorizationPolicies.CanViewRoleAssignments, policy =>
                policy.RequireRole(NewsroomRole.EditorInChief, NewsroomRole.SystemAdmin));
            options.AddPolicy(AuthorizationPolicies.CanManageRoleAssignments, policy =>
                policy.RequireRole(NewsroomRole.SystemAdmin));
        });

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // --- Ingestion (Phase 1, Slice 2) ---
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        services.AddHttpClient(IngestionHttpClientNames.SourceFetcher, client =>
            {
                // A descriptive UA is an operational courtesy to source
                // operators (lets them identify/allowlist/rate-limit us),
                // not a security control.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JusoorNewsBot/1.0 (+https://jusoor.example/bot)");
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var ingestionOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IngestionOptions>>().Value;
                return new SocketsHttpHandler
                {
                    // The actual SSRF defense — see SafeSocketConnector's
                    // comment for why resolve-then-pin defeats DNS rebinding.
                    ConnectCallback = SafeSocketConnector.ConnectAsync,
                    AllowAutoRedirect = false, // redirects are handled manually in SsrfSafeSourceFetcher so each hop is re-validated
                    ConnectTimeout = TimeSpan.FromSeconds(ingestionOptions.ConnectTimeoutSeconds),
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                };
            });

        // Slice 21 (decision D2): the single trust boundary for editorial HTML.
        // Stateless (builds a fresh Ganss.Xss sanitizer per call), so a singleton.
        services.AddSingleton<IEditorialHtmlSanitizer, Content.EditorialHtmlSanitizer>();
        services.AddScoped<IEditorialMediaReferenceValidator, Content.EditorialMediaReferenceValidator>();

        // Slice 22: Supabase credentials are read only from server process
        // configuration. Missing values leave the rest of the API available;
        // media endpoints return a controlled 503 until configured.
        services.Configure<Content.SupabaseEditorialMediaStorageOptions>(options =>
        {
            options.Url = configuration["SUPABASE_URL"];
            options.Bucket = configuration["SUPABASE_STORAGE_BUCKET"];
            options.ServiceRoleKey = configuration["SUPABASE_SERVICE_ROLE_KEY"];
        });
        services.AddHttpClient<IEditorialMediaStorage, Content.SupabaseEditorialMediaStorage>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<ISourceContentFetcher, SsrfSafeSourceFetcher>();
        services.AddScoped<IFeedItemParser, RssFeedParser>();

        // --- Scheduler (Phase 1, Slice 3) ---
        // Only generic/serializer setup here — NOT storage. UsePostgreSqlStorage
        // needs a connection string, and reading it here (before builder.Build())
        // would reproduce the exact eager-config-read bug fixed above for the
        // DbContext/Jwt sections. Storage is configured in Program.cs instead,
        // after Build(), once configuration is guaranteed fully merged (real
        // env vars AND WebApplicationFactory test overrides alike).
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings());

        services.AddHangfireServer();
        services.AddScoped<Scheduling.IngestSourceJob>();
        services.AddScoped<Scheduling.SourceIngestionSweepJob>();
        services.AddScoped<Scheduling.ScheduledEditorialPublicationJob>();

        return services;
    }
}
