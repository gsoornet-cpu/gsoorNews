using System.Reflection;
using FluentValidation;
using Jusoor.Application.Common.Behaviors;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Geography;
using Jusoor.Application.Ingestion;
using Jusoor.Application.Relevance;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jusoor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Phase 1, Slice 4 — Cross-Source Story Clustering. Pure
        // Application-layer logic (only depends on IApplicationDbContext,
        // no Infrastructure dependency), so it's registered here rather
        // than in Infrastructure's DependencyInjection alongside
        // ISourceContentFetcher/IFeedItemParser.
        services.AddScoped<IStoryClusteringService, StoryClusteringService>();

        // Phase 1, Slice 5 — Relevance Engine v1 (rules-based). Also pure
        // Application-layer logic — see RulesBasedRelevanceEngine's own
        // comment for why the Embeddings/LLM halves of the spec's hybrid
        // classifier are deliberately not built here yet.
        services.Configure<RelevanceOptions>(configuration.GetSection(RelevanceOptions.SectionName));
        services.AddScoped<IRelevanceEngine, RulesBasedRelevanceEngine>();

        // Phase 1, Slice 6 — Geographic Extraction v1 (rules-based). Same
        // "pure Application-layer logic" category as the two registrations
        // above — see RulesBasedGeographicExtractor's own comment for why
        // NER/geocoding are deliberately not built here yet.
        services.AddScoped<IGeographicExtractor, RulesBasedGeographicExtractor>();

        return services;
    }
}