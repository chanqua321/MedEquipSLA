using FluentValidation;
namespace MedEquipSLA.Maintenance.Application.Features.ScaffoldCommand;
public sealed class ScaffoldCommandValidator : AbstractValidator<ScaffoldCommand>
{ public ScaffoldCommandValidator() { RuleFor(x => x.Id).NotEmpty(); } }
