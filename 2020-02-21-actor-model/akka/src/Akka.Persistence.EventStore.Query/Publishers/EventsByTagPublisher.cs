using Akka.Actor;
using Akka.Persistence.Query;
using Akka.Streams.Actors;

namespace Akka.Persistence.EventStore.Query.Publishers;

internal class EventsByTagPublisher : ActorPublisher<EventEnvelope>
{
    private readonly DeliveryBuffer<EventEnvelope> _buffer;
    private readonly IActorRef _journalRef;
    private readonly bool _isLive;
    private readonly string _tag;
    private readonly long _toOffset;
    private readonly int _maxBufferSize;
    private long? _currentOffset;
    private long _requestedCount = -1L;
    private bool _isCaughtUp;

    public EventsByTagPublisher(string tag, bool isLive, long? fromOffset, long toOffset, int maxBufferSize,
        string writeJournalPluginId)
    {
        _tag = tag;
        _isLive = isLive;
        _currentOffset = fromOffset;
        _toOffset = toOffset;
        _maxBufferSize = maxBufferSize;

        _buffer = new DeliveryBuffer<EventEnvelope>(OnNext);
        _journalRef = Persistence.Instance.Apply(Context.System).JournalFor(writeJournalPluginId);
    }

    /// <param name="tag"></param>
    /// <param name="isLive"></param>
    /// <param name="fromOffset">0-based Akka offset, inclusive</param>
    /// <param name="toOffset"></param>
    /// <param name="maxBufferSize"></param>
    /// <param name="writeJournalPluginId"></param>
    /// <returns></returns>
    public static Props Props(string tag, bool isLive, long? fromOffset, long toOffset, int maxBufferSize,
        string writeJournalPluginId)
    {
        return Actor.Props.Create(() =>
            new EventsByTagPublisher(tag, isLive, fromOffset, toOffset, maxBufferSize,
                writeJournalPluginId));
    }

    protected override bool Receive(object message)
    {
        bool HandleCaughtUp()
        {
            _isCaughtUp = true;
            MaybeReply();

            return true;
        }

        return message switch
        {
            SubscriptionDroppedException msg => OnSubscriptionDropped(msg),
            CaughtUp => HandleCaughtUp(),
            ReplayedTaggedMessage msg => OnReplayedMessage(msg),
            Request msg => OnRequest(msg),
            Cancel => OnCancel(),
            _ => false
        };
    }

    private bool OnSubscriptionDropped(SubscriptionDroppedException cause)
    {
        OnErrorThenStop(cause);

        return true;
    }

    private bool OnReplayedMessage(ReplayedTaggedMessage replayed)
    {
        // no need to buffer live messages if subscription is not live
        if ((_isLive || !_isCaughtUp) && replayed.Persistent != null)
        {
            _buffer.Add(new EventEnvelope(
                new Sequence(replayed.Offset),
                replayed.Persistent.PersistenceId,
                replayed.Persistent.SequenceNr,
                replayed.Persistent.Payload,
                replayed.Persistent.Timestamp));
            _currentOffset = replayed.Offset;
        }

        MaybeReply();

        return true;
    }

    private bool OnRequest(Request request)
    {
        if (_requestedCount == -1L)
        {
            // _requested == -1L means that Request is first one, so we can start EventStore subscription
            _journalRef.Tell(new ReplayTaggedMessages(_currentOffset, _toOffset, _maxBufferSize, _tag, Self));
        }

        _requestedCount = request.Count;
        MaybeReply();

        return true;
    }

    private bool OnCancel()
    {
        Context.Stop(Self);

        return true;
    }

    private void MaybeReply()
    {
        if (_requestedCount > 0)
        {
            var deliver = _buffer.Length > _requestedCount ? _requestedCount : _buffer.Length;
            _requestedCount -= deliver;
            _buffer.DeliverBuffer(deliver);
        }

        if (_buffer.IsEmpty && !_isLive && _isCaughtUp)
        {
            OnCompleteThenStop();
        }
    }
}