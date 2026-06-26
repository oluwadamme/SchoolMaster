using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Common;
using SchoolMaster.Infrastructure.Persistence;
using MediatR;

namespace SchoolMaster.Infrastructure.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly SchoolMasterContext _context;
    private readonly IMediator _mediator;
    public UnitOfWork(SchoolMasterContext context, IMediator mediator) { _context = context; _mediator = mediator; }
    public async Task SaveChangesAsync()
    {
        var entityEntries = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var events = entityEntries.SelectMany(e => e.Entity.DomainEvents).ToList();

        // Clear before the DB write so a re-entrant SaveChangesAsync call in a handler
        // cannot re-collect and re-dispatch the same events.
        foreach (var entry in entityEntries)
            entry.Entity.ClearDomainEvents();

        await _context.SaveChangesAsync();

        // Dispatch AFTER commit — Hangfire only enqueues if the DB write succeeded.
        foreach (var domainEvent in events)
            await _mediator.Publish(domainEvent);
    }
}