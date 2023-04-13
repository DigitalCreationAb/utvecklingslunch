using System.Collections.Immutable;

namespace Akka.Persistence.EventStore;

public interface IEventWithMetaData
{
    object Event { get; }

    IImmutableDictionary<string, object> GetMetaData();
}