using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Serialization;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Akka.Persistence.EventStore;

public class DefaultEventAdapter : IEventAdapter
{
    private readonly Akka.Serialization.Serialization _serialization;
    private readonly JsonSerializerSettings _metadataSettings;
    private readonly NewtonSoftJsonSerializer _serializer;

    public DefaultEventAdapter(Akka.Serialization.Serialization serialization)
    {
        _serialization = serialization;
        _metadataSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };
        _serializer = new NewtonSoftJsonSerializer(_serialization.System);
    }

    public EventData Adapt(IPersistentRepresentation persistentMessage)
    {
        var metadata = JObject.Parse("{}");

        metadata[Constants.EventMetadata.PersistenceId] = persistentMessage.PersistenceId;
        metadata[Constants.EventMetadata.OccurredOn] = DateTimeOffset.Now;
        metadata[Constants.EventMetadata.Manifest] = persistentMessage.Manifest;
        metadata[Constants.EventMetadata.SequenceNr] = persistentMessage.SequenceNr;
        metadata[Constants.EventMetadata.WriterGuid] = persistentMessage.WriterGuid;
        metadata[Constants.EventMetadata.JournalType] = Constants.JournalTypes.WriteJournal;

        if (persistentMessage.Sender != null)
            metadata[Constants.EventMetadata.SenderPath] =
                Akka.Serialization.Serialization.SerializedActorPath(persistentMessage.Sender);

        var dataBytes = ToBytes(persistentMessage.Payload, metadata, out var type, out var isJson);

        var metadataString = JsonConvert.SerializeObject(metadata, _metadataSettings);
        var metadataBytes = Encoding.UTF8.GetBytes(metadataString);

        return new EventData(Guid.NewGuid(), type, isJson, dataBytes, metadataBytes);
    }

    /// <summary>
    /// Override to change how data and tags are written to EventStore.
    /// The default usea Akka.Serialization.NewtonSoftJsonSerializer to handle complexities around serializing IActorRef and 
    /// other internals. If your payload is simple structures, override this to make writing EventStore projections more 
    /// intuitive.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="metadata">Store metadata such as CLR type name to help with deserialization or projections</param>
    /// <param name="type">The event type as known by EventStore</param>
    /// <param name="isJson"></param>
    /// <returns></returns>
    protected virtual byte[] ToBytes(object payload, JObject metadata, out string type, out bool isJson)
    {
        payload = ParsePayload(payload, metadata);

        var eventType = payload.GetType();
        isJson = true;
        type = eventType.Name.ToEventCase();

        SetTypeMetaData(metadata, eventType);
        
        return _serializer.ToBinary(payload);
    }

    protected static void SetTypeMetaData(JObject metadata, Type eventType)
    {
        var clrEventType = string.Concat(eventType.FullName, ", ", eventType.GetTypeInfo().Assembly.GetName().Name);
        metadata[Constants.EventMetadata.ClrEventType] = clrEventType;

        var fallbackTypes = new List<string>();

        var fallbackType = eventType.BaseType;

        while (fallbackType != null && fallbackType != typeof(object))
        {
            if (!fallbackType.IsAbstract)
                fallbackTypes.Add(string.Concat(fallbackType.FullName, ", ",
                    fallbackType.GetTypeInfo().Assembly.GetName().Name));

            fallbackType = fallbackType.BaseType;
        }

        metadata[Constants.EventMetadata.FallbackTypes] = JArray.FromObject(fallbackTypes);
    }

    protected static object ParsePayload(object payload, JObject metadata)
    {
        while (true)
        {
            switch (payload)
            {
                case Tagged tagged:
                    metadata[Constants.EventMetadata.Tags] = JArray.FromObject(tagged.Tags);
                    payload = tagged.Payload;

                    continue;
                case IEventWithMetaData withMetaData:
                {
                    foreach (var eventMetaData in withMetaData.GetMetaData())
                        metadata[eventMetaData.Key] = JToken.FromObject(eventMetaData.Value);

                    payload = withMetaData.Event;

                    continue;
                }
            }

            return payload;
        }
    }

    public IPersistentRepresentation? Adapt(ResolvedEvent resolvedEvent)
    {
        if (resolvedEvent.Event == null)
            return null;
        
        var eventData = resolvedEvent.Event;

        var metadataString = Encoding.UTF8.GetString(eventData.Metadata);
        var metadata = JsonConvert.DeserializeObject<JObject>(metadataString, _metadataSettings);

        var journalType = (string?) metadata?.SelectToken(Constants.EventMetadata.JournalType);
        if (journalType != Constants.JournalTypes.WriteJournal)
        {
            // since we are reading from "$streams" stream, there could be other kind of event linked, e.g. snapshot
            // events, since IEventAdapter is storing in metadata "journalType" using Adopt while event
            // should be adopted to EventStore message EventData.
            // Return null in case journalType != "WriteJournal" which means some other extension stored that event in
            // database but $streams projection picked up since it is at position 0
            return null;
        }

        var persistenceId = (string?) metadata?.SelectToken(Constants.EventMetadata.PersistenceId);
        var manifest = (string?) metadata?.SelectToken(Constants.EventMetadata.Manifest);
        var sequenceNr = (long?) metadata?.SelectToken(Constants.EventMetadata.SequenceNr);
        var senderPath = (string?) metadata?.SelectToken(Constants.EventMetadata.SenderPath);
        var writerGuid = (string?) metadata?.SelectToken(Constants.EventMetadata.WriterGuid);

        var sender = ActorRefs.NoSender;

        if (senderPath != null)
            sender = _serialization.System.Provider.ResolveActorRef(senderPath);

        var parsedEvent = ToEvent(resolvedEvent.Event.Data, metadata ?? JObject.FromObject(new object()));

        if (parsedEvent == null)
            return null;

        return new Persistent(
            parsedEvent,
            sequenceNr ?? 0,
            persistenceId,
            manifest,
            false,
            sender,
            writerGuid);
    }

    /// <summary>
    /// Override to change how event data EventStore is deserialized to the CLR payload. 
    /// The default use a Akka.Serialization.NewtonSoftJsonSerializer to handle complexities around serializing IActorRef and 
    /// other internals. If your payload is simple structures, override this to make writing EventStore projections more 
    /// intuitive.
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    protected virtual object? ToEvent(byte[] bytes, JObject metadata)
    {
        var eventType = GetEventType(metadata);

        if (eventType == null)
            return null;
        
        return _serializer.FromBinary(bytes, eventType);
    }

    protected static Type? GetEventType(JObject metadata)
    {
        var eventTypeString = (string?) metadata.SelectToken(Constants.EventMetadata.ClrEventType) ?? "";
        var eventType = Type.GetType(eventTypeString, false, true);

        var fallbacks = ((metadata
                    .SelectToken(Constants.EventMetadata.FallbackTypes) as JArray)?
                .Select(x => (string?) x) ?? new List<string?>())
            .ToImmutableList();

        if (eventType == null)
        {
            foreach (var fallbackType in fallbacks)
            {
                eventType = Type.GetType(fallbackType ?? "", false, true);

                if (eventType != null)
                    break;
            }
        }

        return eventType;
    }
}