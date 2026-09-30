using System.Collections.Frozen;
using System.Text.Json;
using SharedKernel;

namespace Infrastructure.Outbox;

/// <summary>
/// Serializes domain events for the outbox. Only concrete <see cref="IDomainEvent"/> types from the Domain
/// assembly can be deserialized, so the stored type name can never be used to instantiate arbitrary types.
/// </summary>
internal static class DomainEventTypes
{
    private static readonly FrozenDictionary<string, Type> Types = Domain.AssemblyReference.Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IDomainEvent)))
        .ToFrozenDictionary(t => t.FullName!, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string GetName(IDomainEvent domainEvent) => domainEvent.GetType().FullName!;

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);

    public static IDomainEvent Deserialize(string typeName, string content)
    {
        if (!Types.TryGetValue(typeName, out Type? type))
        {
            throw new InvalidOperationException($"Unknown domain event type '{typeName}'.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(content, type, SerializerOptions)
            ?? throw new InvalidOperationException($"Domain event '{typeName}' deserialized to null."));
    }
}
