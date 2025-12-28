using Infra.Commands;

namespace Infra.Tests.Command;

public class TestCommandWithoutValidatorHandler : ICommandHandler<TestCommandWithoutValidator, string>
{
	public Task<string> HandleAsync(TestCommandWithoutValidator command, CancellationToken cancellationToken)
	{
		return Task.FromResult($"Processed: {command.Value}");
	}
}
