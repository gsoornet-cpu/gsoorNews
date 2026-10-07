using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Profile;

public enum RemoveMyPlaceOutcome
{
    Removed,
    NotFollowing
}

public sealed record RemoveMyPlaceResult(bool Succeeded, RemoveMyPlaceOutcome Outcome);

public sealed record RemoveMyPlaceCommand(string UserId, Guid CityId) : IRequest<RemoveMyPlaceResult>;

public class RemoveMyPlaceCommandValidator : AbstractValidator<RemoveMyPlaceCommand>
{
    public RemoveMyPlaceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
    }
}

public class RemoveMyPlaceCommandHandler : IRequestHandler<RemoveMyPlaceCommand, RemoveMyPlaceResult>
{
    private readonly IApplicationDbContext _context;

    public RemoveMyPlaceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RemoveMyPlaceResult> Handle(RemoveMyPlaceCommand request, CancellationToken cancellationToken)
    {
        var place = await _context.MyPlaces
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.CityId == request.CityId, cancellationToken);

        if (place is null)
        {
            return new RemoveMyPlaceResult(false, RemoveMyPlaceOutcome.NotFollowing);
        }

        _context.MyPlaces.Remove(place);
        await _context.SaveChangesAsync(cancellationToken);

        return new RemoveMyPlaceResult(true, RemoveMyPlaceOutcome.Removed);
    }
}
