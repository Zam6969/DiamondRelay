using System.Reflection;
using System.Text.Json;
using VrcTwitchOscBridge.Models;
using VrcTwitchOscBridge.Services;
using VrcTwitchOscBridge.ViewModels;
using Xunit;

namespace VrcTwitchOscBridge.Tests;

public sealed class RemovedIntegrationsTests
{
    [Fact]
    public void ImportedRemovedProviderRuleCannotBeEnabled()
    {
        var rule = JsonSerializer.Deserialize<CashPaymentRule>("{\"Provider\":2,\"IsEnabled\":true}")!;
        Assert.Equal(CashPaymentProvider.KoFi, rule.Provider);
        Assert.False(rule.IsEnabled);
        rule.IsEnabled = true;
        Assert.False(rule.IsEnabled);
    }

    [Fact]
    public void LegacyConnectionSettingsAreIgnored()
    {
        var settings = JsonSerializer.Deserialize<CashPaymentConnectionSettings>(
            "{\"KoFiEnabled\":true,\"KoFiVerificationToken\":\"legacy-test-value\",\"StreamlabsEnabled\":true}")!;
        Assert.True(settings.StreamlabsEnabled);
        Assert.DoesNotContain("KoFi", JsonSerializer.Serialize(settings));
    }

    [Theory]
    [InlineData("RunKoFiWebhookAsync")]
    [InlineData("RunKoFiHostedRelayAsync")]
    [InlineData("TryParseKoFiWebhook")]
    public void RemovedProviderHasNoRuntimeEntryPoints(string method)
    {
        Assert.Null(typeof(CashPaymentProviderService).GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Theory]
    [InlineData("OpenKoFiSupportCommand")]
    [InlineData("OpenDiscordInviteCommand")]
    [InlineData("OpenKoFiWebhooksCommand")]
    public void PromotionalCommandsAreRemoved(string property)
    {
        Assert.Null(typeof(MainWindowViewModel).GetProperty(property));
    }
}
