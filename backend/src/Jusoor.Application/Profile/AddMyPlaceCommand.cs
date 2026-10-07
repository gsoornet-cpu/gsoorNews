using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Profile;

public enum AddMyPlaceOutcome
{
    Added,
    CityNotFound,
    AlreadyFollowing
}

public sealed record AddMyPlaceResult(bool Succeeded, AddMyPlaceOutcome Outcome);

public sealed record AddMyPlaceCommand(string UserId, Guid CityId) : IRequest<AddMyPlaceResult>;

public class AddMyPlaceCommandValidator : AbstractValidator<AddMyPlaceCommand>
{
    public AddMyPlaceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
    }
}

public class AddMyPlaceCommandHandler : IRequestHandler<AddMyPlaceCommand, AddMyPlaceResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public AddMyPlaceCommandHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<AddMyPlaceResult> Handle(AddMyPlaceCommand request, CancellationToken cancellationToken)
    {
        if (!await _context.Cities.AnyAsync(c => c.Id == request.CityId, cancellationToken))
        {
            return new AddMyPlaceResult(false, AddMyPlaceOutcome.CityNotFound);
        }

        var alreadyFollowing = await _context.MyPlaces
            .AnyAsync(p => p.UserId == request.UserId && p.CityId == request.CityId, cancellationToken);
        if (alreadyFollowing)
        {
            // Not an error the caller did anything wrong about — following
            // a place you already follow is a no-op, not a conflict, same
            // reasoning a "star" or "favorite" toggle would use.
            return new AddMyPlaceResult(true, AddMyPlaceOutcome.AlreadyFollowing);
        }

        _context.MyPlaces.Add(MyPlace.Create(request.UserId, request.CityId, _clock.UtcNow));
        await _context.SaveChangesAsync(cancellationToken);

        return new AddMyPlaceResult(true, AddMyPlaceOutcome.Added);
    }
}
