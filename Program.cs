using System;
using System.Diagnostics.Eventing.Reader;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

class Program
{
    static void Main(string[] args)
    {
        // OTEL Tracer 구성
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyAgentTracer")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("WindowsAgent"))
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri("http://localhost:4317"); // OTEL Collector 주소
            })
            .Build();

        var tracer = tracerProvider.GetTracer("MyAgentTracer");

        // EventLogWatcher 구성 (Sysmon Operational Log)
        var eventQuery = new EventLogQuery("Security", PathType.LogName);
        var eventWatcher = new EventLogWatcher(eventQuery);

        // 이벤트 발생 시 핸들러 등록
        eventWatcher.EventRecordWritten += (sender, e) =>
        {
            if (e.EventRecord == null)
            {
                return;
            }

            // Span 생성
            using var span = tracer.StartActiveSpan($"SysmonEventID_{e.EventRecord.Id}");

            span.SetAttribute("EventID", e.EventRecord.Id.ToString());
            span.SetAttribute("ProviderName", e.EventRecord.ProviderName);
            span.SetAttribute("LogName", e.EventRecord.LogName);
            span.SetAttribute("MachineName", e.EventRecord.MachineName);
            span.SetAttribute("RecordID", e.EventRecord.RecordId.HasValue ? e.EventRecord.RecordId.Value.ToString() : "N/A");

            Console.WriteLine($"[Sysmon Event] ID={e.EventRecord.Id}, RecordID={e.EventRecord.RecordId}");

            // Span 끝나면 자동 Export
        };

        // Watcher 시작
        eventWatcher.Enabled = true;

        Console.WriteLine("EventLogWatcher 시작됨 (Sysmon Operational Log). 종료하려면 Ctrl+C.");
        Console.ReadLine();

        // 종료 시 Watcher 해제
        eventWatcher.Enabled = false;
        eventWatcher.Dispose();
    }
}
