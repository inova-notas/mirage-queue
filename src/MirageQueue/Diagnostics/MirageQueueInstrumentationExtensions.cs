using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MirageQueue.Diagnostics;

public static class MirageQueueInstrumentationExtensions
{
    public static TracerProviderBuilder AddMirageQueueInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(MirageQueueDiagnostics.ActivitySourceName);

    public static MeterProviderBuilder AddMirageQueueInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(MirageQueueDiagnostics.MeterName);
}
