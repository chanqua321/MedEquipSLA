using FluentValidation;
namespace MedEquipSLA.Equipment.Application.Features.ScaffoldCommand;
public sealed class ScaffoldCommandValidator : AbstractValidator<ScaffoldCommand>
{ public ScaffoldCommandValidator() { RuleFor(x => x.Id).NotEmpty(); } }
