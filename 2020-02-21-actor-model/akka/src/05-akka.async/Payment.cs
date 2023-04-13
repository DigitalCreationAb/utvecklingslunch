using System;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async;

public class Payment : ReceivePersistentActor
{
    public static class Commands
    {
        public record ChargePayment;

        public record RefundPayment;
    }

    public static class Events
    {
        public record PaymentCharged(string PaymentId, decimal Amount)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} charged with the amount {Amount:N2}";
            }
        }

        public record PaymentChargeFailed(string PaymentId, string Reason)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} failed charging. Reason: \"{Reason}\"";
            }
        }

        public record PaymentRefunded(string PaymentId, decimal Amount)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} refunded the amount {Amount}";
            }
        }

        public record PaymentRefundFailed(string PaymentId, string Reason)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} failed refunding. Reason: \"{Reason}\"";
            }
        }
    }

    private readonly decimal _amount;

    public override string PersistenceId => $"payments-{PaymentId}";

    private string PaymentId => Self.Path.Name;

    public Payment(decimal amount)
    {
        _amount = amount;

        Recover<Events.PaymentCharged>(On);
        Recover<Events.PaymentChargeFailed>(On);
        Recover<Events.PaymentRefunded>(On);
        Recover<Events.PaymentRefundFailed>(On);

        Become(Initialized);
    }

    private void Initialized()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            PersistAndNotifyParent(new Events.PaymentCharged(PaymentId, _amount), On);
        });

        Command<Commands.RefundPayment>(_ =>
        {
            PersistAndNotifyParent(new Events.PaymentRefunded(PaymentId, _amount), On);
        });
    }

    private void Charged()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            PersistAndNotifyParent(
                new Events.PaymentChargeFailed(PaymentId, "This payment has already been charged"), On);
        });

        Command<Commands.RefundPayment>(_ =>
        {
            PersistAndNotifyParent(new Events.PaymentRefunded(PaymentId, _amount), On);
        });
    }

    private void Refunded()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            PersistAndNotifyParent(
                new Events.PaymentChargeFailed(PaymentId, "This payment has been refunded"), On);
        });

        Command<Commands.RefundPayment>(_ =>
        {
            PersistAndNotifyParent(
                new Events.PaymentRefundFailed(PaymentId, "This payment has already been refunded"), On);
        });
    }

    private void PersistAndNotifyParent<TEvent>(TEvent evnt, Action<TEvent> handler)
    {
        Persist(evnt, savedEvent =>
        {
            handler(savedEvent);

            Context.Parent.Tell(evnt);
        });
    }

    private void On(Events.PaymentCharged evnt)
    {
        Become(Charged);
    }

    private void On(Events.PaymentChargeFailed evnt)
    {
    }

    private void On(Events.PaymentRefunded evnt)
    {
        Become(Refunded);
    }

    private void On(Events.PaymentRefundFailed evnt)
    {
    }

    public static Props Initialize(decimal amount)
    {
        return Props.Create(() => new Payment(amount));
    }
}