using System.Diagnostics.CodeAnalysis;
using Akka.Actor;
using Akka.Persistence;

namespace _04_akka.persistence;

public class Payment : ReceivePersistentActor
{
    public static class Commands
    {
        public record ChargePayment;

        public record RefundPayment;
    }

    public static class Events
    {
        public record PaymentCharged(string PaymentId, decimal Amount);

        public record PaymentRefunded(string PaymentId, decimal Amount);
    }

    public static class Responses
    {
        public record ChargePaymentResponse(decimal Amount, string? ErrorMessage = null)
        {
            [MemberNotNullWhen(false, nameof(ErrorMessage))]
            public bool Success => string.IsNullOrEmpty(ErrorMessage);
        }

        public record RefundPaymentResponse(decimal Amount, string? ErrorMessage = null)
        {
            [MemberNotNullWhen(false, nameof(ErrorMessage))]
            public bool Success => string.IsNullOrEmpty(ErrorMessage);
        }
    }

    private readonly decimal _amount;

    public override string PersistenceId => $"payments-{PaymentId}";

    private string PaymentId => Self.Path.Name;

    public Payment(decimal amount)
    {
        _amount = amount;

        Recover<Events.PaymentCharged>(On);
        Recover<Events.PaymentRefunded>(On);

        Become(Initialized);
    }

    private void Initialized()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            Persist(new Events.PaymentCharged(PaymentId, _amount), evnt =>
            {
                On(evnt);

                Sender.Tell(new Responses.ChargePaymentResponse(_amount));
            });
        });

        Command<Commands.RefundPayment>(_ =>
        {
            Persist(new Events.PaymentRefunded(PaymentId, _amount), evnt =>
            {
                On(evnt);

                Sender.Tell(new Responses.RefundPaymentResponse(_amount));
            });
        });
    }

    private void Charged()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has already been charged"));
        });

        Command<Commands.RefundPayment>(_ =>
        {
            Persist(new Events.PaymentRefunded(PaymentId, _amount), evnt =>
            {
                On(evnt);

                Sender.Tell(new Responses.RefundPaymentResponse(_amount));
            });
        });
    }

    private void Refunded()
    {
        Command<Commands.ChargePayment>(_ =>
        {
            Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has been refunded"));
        });

        Command<Commands.RefundPayment>(_ =>
        {
            Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment has already been refunded"));
        });
    }

    private void On(Events.PaymentCharged evnt)
    {
        Become(Charged);
    }

    private void On(Events.PaymentRefunded evnt)
    {
        Become(Refunded);
    }

    public static Props Initialize(decimal amount)
    {
        return Props.Create(() => new Payment(amount));
    }
}