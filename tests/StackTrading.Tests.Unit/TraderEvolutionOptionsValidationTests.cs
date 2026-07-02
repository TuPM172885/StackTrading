using FluentAssertions;
using Microsoft.Extensions.Options;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Unit;

public sealed class TraderEvolutionOptionsValidationTests
{
    [Fact]
    public void Validate_ShouldRejectDisabledPaperEnvironment()
    {
        var validator = new TraderEvolutionOptionsValidator();
        var options = new TraderEvolutionOptions
        {
            Paper = new TraderEvolutionEnvironmentOptions
            {
                ApiBaseUrl = "http://paper.local",
                WebSocketBaseUrl = "ws://paper.local",
                ApiKey = "paper-key",
                ApiSecret = "paper-secret",
                Enabled = false
            },
            Live = new TraderEvolutionEnvironmentOptions
            {
                ApiBaseUrl = "http://live.local",
                WebSocketBaseUrl = "ws://live.local",
                ApiKey = "live-key",
                ApiSecret = "live-secret",
                Enabled = true
            }
        };

        var result = validator.Validate(Options.DefaultName, options);
        result.Failed.Should().BeTrue();
    }
}
