using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NATS.Client.Core;

namespace NATS.Client.JetStream.Internal;

internal sealed class NatsJSJsonDocumentSerializer<T> : INatsDeserialize<NatsJSApiResult<T>>
{
    public static readonly NatsJSJsonDocumentSerializer<T> Default = new();

    public NatsJSApiResult<T> Deserialize(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return new NatsJSException("Buffer is empty");
        }

        using var jsonDocument = JsonDocument.Parse(buffer);

        if (jsonDocument.RootElement.TryGetProperty("error", out var errorElement))
        {
            var error = errorElement.Deserialize(JetStream.NatsJSJsonSerializerContext.Default.ApiError) ?? throw new NatsJSException("Can't parse JetStream error JSON payload");
            return error;
        }

        var jsonTypeInfo = NatsJsSerializerDefaultContextTypeInfo<T>.GetTypeInfo();
        if (jsonTypeInfo == null)
        {
            return new NatsJSException($"Unknown response type {typeof(T)}");
        }

        var result = (T?)jsonDocument.RootElement.Deserialize(jsonTypeInfo);

        if (result == null)
        {
            return new NatsJSException("Null result");
        }

        return result;
    }
}

internal static class NatsJsSerializerDefaultContextTypeInfo<T>
{
    private static readonly object? _jsonTypeInfoMaybe = GenTypeInfo();

    public static JsonTypeInfo? GetTypeInfo()
    {
        if (_jsonTypeInfoMaybe is JsonTypeInfo info)
        {
            return info;
        }
        else if (_jsonTypeInfoMaybe != null)
        {
            return null;
        }
        else
        {
            ThrowIfJsonTypeInfoInvalid();
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIfJsonTypeInfoInvalid() => throw ((_jsonTypeInfoMaybe as Exception) ?? new Exception("Should never get this"));

    private static object? GenTypeInfo()
    {
        try
        {
            return NatsJSJsonSerializerContext.DefaultContext.GetTypeInfo(typeof(T));
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
