using Akka.Actor;
using Akka.Event;
using Akka.Persistence.EventStore.Query;
using Akka.Persistence.Journal;
using EventStore.ClientAPI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Configuration;

namespace Akka.Persistence.EventStore.Journal;

public class EventStoreJournal : AsyncWriteJournal, IWithUnboundedStash
{
    public IStash? Stash { get; set; }

    private readonly IEventStoreConnection? _connRead;
    private readonly IEventStoreConnection? _conn;
    private IEventAdapter? _eventAdapter;
    private readonly EventStoreJournalSettings _settings;
    private readonly EventStoreSubscriptions _subscriptions;
    private readonly ILoggingAdapter _log;
    private readonly Akka.Serialization.Serialization _serialization;

    public EventStoreJournal()
    {
        _settings = EventStorePersistence.Get(Context.System).JournalSettings;
        _log = Context.GetLogger();
        _serialization = Context.System.Serialization;

        var connectionString = _settings.ConnectionString;
        var connectionName = _settings.ConnectionName;

        _connRead = EventStoreConnection
            .Create(connectionString, $"{connectionName}.Read");

        _connRead.ConnectAsync().Wait();

        _conn = EventStoreConnection
            .Create(connectionString, connectionName);

        _conn.ConnectAsync()
            .PipeTo(
                Self,
                success: () => new Status.Success("Connected"),
                failure: ex => new Status.Failure(ex)
            );

        _subscriptions = new EventStoreSubscriptions(_connRead, Context);
    }

    protected override void PreStart()
    {
        base.PreStart();

        _eventAdapter = BuildJournalAdapter(_serialization, _settings);

        BecomeStacked(AwaitingConnection);
    }

    protected override void PostStop()
    {
        base.PostStop();
        _conn?.Dispose();
        _connRead?.Dispose();
    }

    private bool AwaitingConnection(object message)
    {
        bool HandleSuccess()
        {
            UnbecomeStacked();
            Stash?.UnstashAll();

            return true;
        }

        bool HandleFailure(Status.Failure fail)
        {
            _log.Error(fail.Cause, "Failure during {0} initialization.", Self);
            Context.Stop(Self);

            return true;
        }

        bool HandleDefault()
        {
            Stash?.Stash();

            return true;
        }

        return message switch
        {
            Status.Success => HandleSuccess(),
            Status.Failure fail => HandleFailure(fail),
            _ => HandleDefault()
        };
    }

    private static IEventAdapter BuildJournalAdapter(
        Akka.Serialization.Serialization serialization,
        EventStoreJournalSettings settings)
    {
        var getDefaultAdapter = () => new DefaultEventAdapter(serialization);

        switch (settings.EventAdapterProvider.ToLowerInvariant())
        {
            case "default":
                return getDefaultAdapter();
            case "legacy":
                return new LegacyEventAdapter(serialization);
            default:
                try
                {
                    var eventAdapterConfig = serialization
                        .System
                        .Settings
                        .Config
                        .GetConfig(settings.EventAdapterProvider);
                    
                    var journalAdapterType = Type.GetType(eventAdapterConfig.GetString("class"));

                    if (journalAdapterType == null)
                        return getDefaultAdapter();

                    var availableConstructorArguments = new Dictionary<Type, object>
                    {
                        [typeof(Akka.Serialization.Serialization)] = serialization,
                        [typeof(Config)] = eventAdapterConfig
                    }.ToImmutableDictionary();

                    var bestConstructor = journalAdapterType
                        .GetConstructors()
                        .Where(x => x.GetParameters()
                            .All(y => availableConstructorArguments.ContainsKey(y.ParameterType)))
                        .OrderByDescending(x => x.GetParameters().Length)
                        .FirstOrDefault();
                    
                    var journalAdapter = (bestConstructor != null
                        ? bestConstructor.Invoke(bestConstructor
                            .GetParameters()
                            .Select(x => availableConstructorArguments[x.ParameterType])
                            .ToArray())
                        : Activator.CreateInstance(journalAdapterType)) as IEventAdapter;

                    return journalAdapter ?? getDefaultAdapter();
                }
                catch (Exception)
                {
                    return getDefaultAdapter();
                }
        }
    }

    public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
    {
        try
        {
            var streamName = GetStreamName(persistenceId);

            var slice = await _conn.ReadStreamEventsBackwardAsync(streamName, StreamPosition.End, 1, false);

            long sequence = 0;

            if (slice.Events.Any())
            {
                var @event = slice.Events.First();
                var adapted = _eventAdapter.Adapt(@event);
                sequence = adapted.SequenceNr;
            }
            else
            {
                var metadata = await _conn.GetStreamMetadataAsync(streamName);
                if (metadata.StreamMetadata.TruncateBefore != null)
                {
                    sequence = metadata.StreamMetadata.TruncateBefore.Value;
                }
            }

            return sequence;
        }
        catch (Exception e)
        {
            _log.Error(e, e.Message);
            throw;
        }
    }

    public override async Task ReplayMessagesAsync(
        IActorContext context,
        string persistenceId,
        long fromSequenceNr,
        long toSequenceNr,
        long max,
        Action<IPersistentRepresentation> recoveryCallback)
    {
        var streamName = GetStreamName(persistenceId);

        try
        {
            if (toSequenceNr < fromSequenceNr || max == 0) return;

            if (fromSequenceNr == toSequenceNr)
            {
                max = 1;
            }

            if (toSequenceNr > fromSequenceNr && max == toSequenceNr)
            {
                max = toSequenceNr - fromSequenceNr + 1;
            }

            var count = 0L;

            var start = fromSequenceNr <= 0
                ? 0
                : fromSequenceNr - 1;

            var localBatchSize = _settings.ReadBatchSize;

            StreamEventsSlice slice;
            do
            {
                if (max == long.MaxValue && toSequenceNr > fromSequenceNr)
                {
                    max = toSequenceNr - fromSequenceNr + 1;
                }

                if (max < localBatchSize)
                {
                    localBatchSize = (int) max;
                }

                slice = await _conn.ReadStreamEventsForwardAsync(streamName, start, localBatchSize, false);

                foreach (var @event in slice.Events)
                {
                    var representation = _eventAdapter.Adapt(@event);

                    recoveryCallback(representation);
                    count++;

                    if (count == max)
                    {
                        return;
                    }
                }

                start = slice.NextEventNumber;
            } while (!slice.IsEndOfStream);
        }
        catch (Exception e)
        {
            _log.Error(e, "Error replaying messages for: {0}", streamName);
            throw;
        }
    }

    protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(
        IEnumerable<AtomicWrite> atomicWrites)
    {
        var results = new List<Exception>();
        foreach (var atomicWrite in atomicWrites)
        {
            var persistentMessages = (IImmutableList<IPersistentRepresentation>) atomicWrite.Payload;

            var persistenceId = atomicWrite.PersistenceId;


            var lowSequenceId = persistentMessages.Min(c => c.SequenceNr) - 2;

            try
            {
                var events = persistentMessages
                    .Select(persistentMessage => _eventAdapter.Adapt(persistentMessage)).ToArray();

                var pendingWrite = new
                {
                    StreamId = GetStreamName(persistenceId),
                    ExpectedSequenceId = lowSequenceId,
                    EventData = events,
                    debugData = persistentMessages
                };
                var expectedVersion = pendingWrite.ExpectedSequenceId < 0
                    ? ExpectedVersion.NoStream
                    : (int) pendingWrite.ExpectedSequenceId;

                await _conn.AppendToStreamAsync(pendingWrite.StreamId, expectedVersion, pendingWrite.EventData);
                results.Add(null);
            }
            catch (Exception e)
            {
                results.Add(TryUnwrapException(e));
            }
        }

        return results.Any(x => x != null) ? results.ToImmutableList() : null;
    }

    protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
    {
        var streamName = GetStreamName(persistenceId);

        if (toSequenceNr == long.MaxValue)
        {
            var slice = await _conn.ReadStreamEventsBackwardAsync(streamName, StreamPosition.End, 1, false);
            if (slice.Events.Any())
            {
                var @event = slice.Events.First();
                var highestEventPosition = @event.OriginalEventNumber;
                await _conn.SetStreamMetadataAsync(streamName, ExpectedVersion.Any,
                    StreamMetadata.Create(truncateBefore: highestEventPosition + 1));
            }
        }
        else
        {
            await _conn.SetStreamMetadataAsync(streamName, ExpectedVersion.Any,
                StreamMetadata.Create(truncateBefore: toSequenceNr));
        }
    }

    protected override bool ReceivePluginInternal(object message)
    {
        return message switch
        {
            ReplayTaggedMessages msg => StartTaggedSubscription(msg),
            SubscribePersistenceId msg => StartPersistenceIdSubscription(msg),
            SubscribeAllPersistenceIds msg => SubscribeAllPersistenceIdsHandler(msg),
            Unsubscribe msg => RemoveSubscriber(msg),
            _ => false
        };
    }

    private bool StartPersistenceIdSubscription(SubscribePersistenceId sub)
    {
        var streamName = GetStreamName(sub.PersistenceId);

        // Sequence numbers are Akka issued, 1-based, convert to 0-based exclusive EventStore offsets
        var offset = sub.FromSequenceNr == 0 ? (long?) null : sub.FromSequenceNr - 1;
        
        _subscriptions.Subscribe(Sender, streamName, offset, sub.Max, e =>
        {
            var p = _eventAdapter?.Adapt(e);
            return p != null ? new ReplayedMessage(p) : null;
        });

        return true;
    }

    private bool SubscribeAllPersistenceIdsHandler(SubscribeAllPersistenceIds msg)
    {
        _subscriptions.Subscribe(Sender, "$streams", null, 500, e => _eventAdapter.Adapt(e));

        return true;
    }

    private bool StartTaggedSubscription(ReplayTaggedMessages msg)
    {
        _subscriptions.Subscribe(
            Sender,
            GetStreamName(msg.Tag),
            msg.FromOffset,
            (int) msg.Max,
            @event => new ReplayedTaggedMessage(
                _eventAdapter.Adapt(@event),
                msg.Tag,
                @event.Link?.EventNumber ?? @event.OriginalEventNumber)
        );

        return true;
    }

    private bool RemoveSubscriber(Unsubscribe msg)
    {
        _subscriptions.Unsubscribe(msg.StreamId, msg.Subscriber);

        return true;
    }

    private string GetStreamName(string persistenceId)
    {
        return $"{_settings.Prefix}{persistenceId}";
    }
}