using AgentSandbox.Core;
using Microsoft.ApplicationInsights;

namespace AgentSandbox.Extensions.ApplicationInsights;

/// <summary>
/// Extension methods for integrating Sandbox with Application Insights.
/// </summary>
public static class ApplicationInsightsExtensions
{
    /// <summary>
    /// Adds Application Insights telemetry to the sandbox.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <param name="telemetryClient">The Application Insights TelemetryClient.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The subscription disposable (dispose to unsubscribe).</returns>
    public static IDisposable AddApplicationInsights(
        this Sandbox sandbox,
        TelemetryClient telemetryClient,
        ApplicationInsightsObserverOptions? options = null)
    {
        var observer = new ApplicationInsightsObserver(telemetryClient, options);
        return sandbox.Subscribe(observer);
    }

    /// <summary>
    /// Adds Application Insights telemetry to the sandbox with configuration action.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <param name="telemetryClient">The Application Insights TelemetryClient.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The subscription disposable (dispose to unsubscribe).</returns>
    public static IDisposable AddApplicationInsights(
        this Sandbox sandbox,
        TelemetryClient telemetryClient,
        Action<ApplicationInsightsObserverOptions> configure)
    {
        var options = new ApplicationInsightsObserverOptions();
        configure(options);
        var observer = new ApplicationInsightsObserver(telemetryClient, options);
        return sandbox.Subscribe(observer);
    }
}
