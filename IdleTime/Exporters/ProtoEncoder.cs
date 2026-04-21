using System.Text;

namespace IdleTimeWatcher.Exporters;

// Lightweight hand-rolled encoder for the Prometheus Remote Write protobuf wire format.
// Avoids a full protoc code-generation step while remaining spec-compliant with remote_write 1.0.
// Spec: https://prometheus.io/docs/concepts/remote_write_spec/

internal sealed record ProtoLabel(string Name, string Value);
internal sealed record ProtoSample(double Value, long TimestampMs);
internal sealed record ProtoTimeSeries(IReadOnlyList<ProtoLabel> Labels, IReadOnlyList<ProtoSample> Samples);

// Metric types for MetricMetadata (field 3 of WriteRequest).
// Values match the prometheus.MetricMetadata.MetricType enum in the v1 spec.
internal enum ProtoMetricType { Unknown = 0, Counter = 1, Gauge = 2, Summary = 3, Untyped = 4, Histogram = 5 }

internal sealed record ProtoMetricMetadata(
    string MetricFamilyName,
    ProtoMetricType Type,
    string Help = "",
    string Unit = "");

internal static class ProtoEncoder
{
    /// <summary>
    /// Encodes a WriteRequest message as raw protobuf bytes ready for Snappy compression.
    /// Pass metadata to declare metric types — Prometheus shows "unknown" otherwise.
    /// </summary>
    public static byte[] EncodeWriteRequest(
        IReadOnlyList<ProtoTimeSeries> timeSeries,
        IReadOnlyList<ProtoMetricMetadata>? metadata = null)
    {
        using var ms = new MemoryStream();
        foreach (var ts in timeSeries)
        {
            var tsBytes = EncodeTimeSeries(ts);
            WriteTagAndLength(ms, fieldNumber: 1, tsBytes.Length);
            ms.Write(tsBytes);
        }
        // field 3: repeated MetricMetadata metadata
        if (metadata is not null)
        {
            foreach (var m in metadata)
            {
                var mBytes = EncodeMetadata(m);
                WriteTagAndLength(ms, fieldNumber: 3, mBytes.Length);
                ms.Write(mBytes);
            }
        }
        return ms.ToArray();
    }

    private static byte[] EncodeMetadata(ProtoMetricMetadata m)
    {
        using var ms = new MemoryStream();
        // field 1: MetricType (enum = varint)
        WriteVarint(ms, (1 << 3) | 0);
        WriteVarint(ms, (ulong)m.Type);
        // field 2: metric_family_name (string)
        WriteString(ms, fieldNumber: 2, m.MetricFamilyName);
        if (!string.IsNullOrEmpty(m.Help))
            WriteString(ms, fieldNumber: 4, m.Help);
        if (!string.IsNullOrEmpty(m.Unit))
            WriteString(ms, fieldNumber: 5, m.Unit);
        return ms.ToArray();
    }

    private static byte[] EncodeTimeSeries(ProtoTimeSeries ts)
    {
        using var ms = new MemoryStream();
        foreach (var label in ts.Labels)
        {
            var lb = EncodeLabel(label);
            WriteTagAndLength(ms, fieldNumber: 1, lb.Length);
            ms.Write(lb);
        }
        foreach (var sample in ts.Samples)
        {
            var sb = EncodeSample(sample);
            WriteTagAndLength(ms, fieldNumber: 2, sb.Length);
            ms.Write(sb);
        }
        return ms.ToArray();
    }

    private static byte[] EncodeLabel(ProtoLabel label)
    {
        using var ms = new MemoryStream();
        WriteString(ms, fieldNumber: 1, label.Name);
        WriteString(ms, fieldNumber: 2, label.Value);
        return ms.ToArray();
    }

    private static byte[] EncodeSample(ProtoSample sample)
    {
        using var ms = new MemoryStream();
        // field 1: double — wire type 1 (64-bit little-endian)
        WriteVarint(ms, (1 << 3) | 1);
        ms.Write(BitConverter.GetBytes(sample.Value));
        // field 2: int64 timestamp (ms since epoch) — wire type 0 (varint)
        WriteVarint(ms, (2 << 3) | 0);
        WriteVarint(ms, (ulong)sample.TimestampMs);
        return ms.ToArray();
    }

    // wire type 2 = length-delimited (embedded message, string, bytes)
    private static void WriteTagAndLength(Stream s, int fieldNumber, int length)
    {
        WriteVarint(s, (ulong)((fieldNumber << 3) | 2));
        WriteVarint(s, (ulong)length);
    }

    private static void WriteString(Stream s, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteTagAndLength(s, fieldNumber, bytes.Length);
        s.Write(bytes);
    }

    private static void WriteVarint(Stream s, ulong value)
    {
        while (value > 127)
        {
            s.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        s.WriteByte((byte)value);
    }
}
