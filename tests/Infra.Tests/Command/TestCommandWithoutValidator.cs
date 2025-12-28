using Infra.Commands;

namespace Infra.Tests.Command;

public class TestCommandWithoutValidator : ICommand
{
	public string Value { get; set; }
}
