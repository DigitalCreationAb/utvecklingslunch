using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace _06_akka.safe_process;

public class Payment : ReceivePersistentActor
{
    public static class Commands
    {
        public record StartChargingPayment;

        public record StartRefundingPayment;
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

    public static class Responses
    {
        public record ChargingProcessResponse(string PaymentId, decimal Amount, IImmutableList<string> Errors)
        {
            public ChargingProcessResponse(string paymentId, decimal amount, params string[] errors) : this(
                paymentId, amount, errors.ToImmutableList())
            {
            }

            public bool Success => !Errors.Any();
        }

        public record RefundingProcessResponse(string PaymentId, decimal Amount, IImmutableList<string> Errors)
        {
            public RefundingProcessResponse(string paymentId, decimal amount, params string[] errors) : this(
                paymentId, amount, errors.ToImmutableList())
            {
            }

            public bool Success => !Errors.Any();
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
        Command<Commands.StartChargingPayment>(_ =>
        {
            var random = new Random();

            var errors = new List<string>();

            if (random.Next(10) >= 8)
            {
                Persist(
                    new Events.PaymentChargeFailed(PaymentId, "A random error occured"), On);

                errors.Add("A random error occured");
            }
            else
            {
                Persist(new Events.PaymentCharged(PaymentId, _amount), On);
            }

            Context.Parent.Tell(new Responses.ChargingProcessResponse(PaymentId, _amount, errors.ToArray()));
        });

        Command<Commands.StartRefundingPayment>(_ =>
        {
            Persist(new Events.PaymentRefunded(PaymentId, _amount), On);

            Context.Parent.Tell(new Responses.RefundingProcessResponse(PaymentId, _amount));
        });
    }

    private void Charged()
    {
        Command<Commands.StartChargingPayment>(_ =>
        {
            Context.Parent.Tell(new Responses.ChargingProcessResponse(PaymentId, _amount));
        });

        Command<Commands.StartRefundingPayment>(_ =>
        {
            var random = new Random();

            var errors = new List<string>();

            if (random.Next(10) >= 8)
            {
                Persist(new Events.PaymentRefundFailed(PaymentId, "A random error occured"), On);

                errors.Add("A random error occured");
            }
            else
            {
                Persist(new Events.PaymentRefunded(PaymentId, _amount), On);
            }

            Context.Parent.Tell(new Responses.RefundingProcessResponse(PaymentId, _amount, errors.ToArray()));
        });
    }

    private void Refunded()
    {
        Command<Commands.StartChargingPayment>(_ =>
        {
            Context.Parent.Tell(new Responses.ChargingProcessResponse(PaymentId, _amount,
                "This payment has been refunded"));
        });

        Command<Commands.StartRefundingPayment>(_ =>
        {
            Context.Parent.Tell(new Responses.RefundingProcessResponse(PaymentId, _amount));
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