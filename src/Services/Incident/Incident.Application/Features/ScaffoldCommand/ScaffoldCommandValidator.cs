using FluentValidation;
namespace MedEquipSLA.Incident.Application.Features.ScaffoldCommand;
public sealed class ScaffoldCommandValidator : AbstractValidator<ScaffoldCommand>
{ public ScaffoldCommandValidator() { RuleFor(x => x.Id).NotEmpty(); } }
