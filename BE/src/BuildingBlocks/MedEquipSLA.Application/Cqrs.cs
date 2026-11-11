namespace MedEquipSLA.Application;
public interface ICommand<out TResponse> { }
public interface IQuery<out TResponse> { }
public interface ICommandHandler<in TCommand,TResponse> where TCommand : ICommand<TResponse>
{ Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken); }
public interface IQueryHandler<in TQuery,TResponse> where TQuery : IQuery<TResponse>
{ Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken); }
public interface IUnitOfWork { Task<int> SaveChangesAsync(CancellationToken cancellationToken = default); }
