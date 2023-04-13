using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async;

public class SalesOrder : ReceivePersistentActor
{
    public static class Commands
    {
        public record PlaceOrder(string ProductName, decimal ProductPrice);

        public record AddPayment(decimal Amount);

        public record ConfirmOrder;

        public record CancelOrder;
    }

    public class Events
    {
        public record OrderPlaced(string OrderId, string ProductName, decimal ProductPrice)
        {
            public override string ToString()
            {
                return
                    $"Order {OrderId} was placed. Product name: \"{ProductName}\", Product price: {ProductPrice:N2}";
            }
        }

        public record PlaceOrderFailed(string OrderId, string Reason)
        {
            public override string ToString()
            {
                return $"Failed placing order {OrderId}. Reason: \"{Reason}\"";
            }
        }

        public record PaymentAddedToOrder(string OrderId, string PaymentId, decimal Amount)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} added to order {OrderId}. Amount: {Amount:N2}";
            }
        }

        public record FailedAddingPaymentToOrder(string OrderId, decimal Amount, string Reason)
        {
            public override string ToString()
            {
                return $"Failed adding payment to order {OrderId}. Reason: \"{Reason}\"";
            }
        }

        public record OrderPaymentCharged(string OrderId, string PaymentId)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} for order {OrderId} was charged";
            }
        }

        public record OrderPaymentRefunded(string OrderId, string PaymentId)
        {
            public override string ToString()
            {
                return $"Payment {PaymentId} for order {OrderId} was refunded";
            }
        }

        public record OrderCompleted(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} was completed";
            }
        }

        public record OrderConfirmed(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} was confirmed";
            }
        }

        public record ConfirmOrderFailed(string OrderId, string Reason)
        {
            public override string ToString()
            {
                return $"Failed confirming order {OrderId}. Reason: \"{Reason}\"";
            }
        }

        public record OrderCancelled(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} was cancelled";
            }
        }

        public record OrderCancellationFailed(string OrderId, string Reason)
        {
            public override string ToString()
            {
                return $"Failed cancelling order {OrderId}. Reason: \"{Reason}\"";
            }
        }
    }

    public static class Queries
    {
        public record GetOrderData;
    }

    public static class Responses
    {
        public record OrderDataResponse(string? ProductName, decimal ProductPrice, string Status);
    }

    private string? _productName;
    private decimal _productPrice;

    private readonly IDictionary<string, PaymentInformation> _payments =
        new Dictionary<string, PaymentInformation>();

    public override string PersistenceId => $"salesorders-{Self.Path.Name}";
    private string OrderId => Self.Path.Name;

    public SalesOrder()
    {
        Recover<Events.OrderPlaced>(On);
        Recover<Events.PlaceOrderFailed>(On);
        Recover<Events.PaymentAddedToOrder>(On);
        Recover<Events.FailedAddingPaymentToOrder>(On);
        Recover<Events.OrderPaymentCharged>(On);
        Recover<Events.OrderPaymentRefunded>(On);
        Recover<Events.OrderCompleted>(On);
        Recover<Events.OrderConfirmed>(On);
        Recover<Events.ConfirmOrderFailed>(On);
        Recover<Events.OrderCancelled>(On);
        Recover<Events.OrderCancellationFailed>(On);

        Become(New);
    }

    public static Props Initialize()
    {
        return Props.Create<SalesOrder>();
    }

    private void New()
    {
        PaymentEvents();

        Command<Commands.PlaceOrder>(cmd =>
        {
            Persist(new Events.OrderPlaced(OrderId, cmd.ProductName, cmd.ProductPrice), On);
        });

        Command<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "New"));
        });

        Command<Commands.AddPayment>(cmd =>
        {
            Persist(new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount, "This order hasn't been placed yet"),
                On);
        });

        Command<Commands.ConfirmOrder>(_ =>
        {
            Persist(new Events.ConfirmOrderFailed(OrderId, "This order hasn't been completed"), On);
        });

        Command<Commands.CancelOrder>(_ =>
        {
            Persist(new Events.OrderCancellationFailed(OrderId, "This order hasn't been completed yet"), On);
        });
    }

    private void Placed()
    {
        PaymentEvents();

        Command<Commands.PlaceOrder>(_ =>
        {
            Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
        });

        Command<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Placed"));
        });

        Command<Commands.AddPayment>(cmd =>
        {
            var amountLeftToPay = CalculateAmountLeftToPay();

            if (cmd.Amount > amountLeftToPay)
            {
                Persist(
                    new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                        $"The amount {cmd.Amount:N2} is higher then what left to pay for this order ({amountLeftToPay})"),
                    On);

                return;
            }

            var paymentIncrement = (_payments.Count + 1).ToString();

            var paymentId = $"{OrderId}-{paymentIncrement}";

            Persist(new Events.PaymentAddedToOrder(OrderId, paymentId, cmd.Amount), On);
        });

        Command<Commands.ConfirmOrder>(_ =>
        {
            Persist(new Events.ConfirmOrderFailed(OrderId, "This order hasn't been completed"), On);
        });

        Command<Commands.CancelOrder>(_ =>
        {
            Persist(new Events.OrderCancellationFailed(OrderId, "This order hasn't been completed yet"), On);
        });
    }

    private void Complete()
    {
        PaymentEvents();

        Command<Commands.PlaceOrder>(_ =>
        {
            Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
        });

        Command<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Complete"));
        });

        Command<Commands.AddPayment>(cmd =>
        {
            Persist(
                new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                    "You have already added enough payments to this order"), On);
        });

        Command<Commands.ConfirmOrder>(_ =>
        {
            var paymentsToCharge = _payments
                .Where(x => x.Value.Status == PaymentInformation.PaymentStatus.Initialized)
                .ToList();

            foreach (var payment in paymentsToCharge)
                payment.Value.Charge();
        });

        Command<Commands.CancelOrder>(_ => CancelOrder());
    }

    private void Confirmed()
    {
        PaymentEvents();

        Command<Commands.PlaceOrder>(_ =>
        {
            Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
        });

        Command<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Confirmed"));
        });

        Command<Commands.AddPayment>(cmd =>
        {
            Persist(
                new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                    "You have already added enough payments to this order"), On);
        });

        Command<Commands.ConfirmOrder>(_ =>
        {
            Persist(new Events.ConfirmOrderFailed(OrderId, "This order has already been confirmed"), On);
        });

        Command<Commands.CancelOrder>(_ => CancelOrder());
    }

    private void Cancelled()
    {
        PaymentEvents();

        Command<Commands.PlaceOrder>(_ =>
        {
            Persist(new Events.PlaceOrderFailed(OrderId, "This order has been cancelled"), On);
        });

        Command<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Cancelled"));
        });

        Command<Commands.AddPayment>(cmd =>
        {
            Persist(
                new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                    "This order has been cancelled"), On);
        });

        Command<Commands.ConfirmOrder>(_ =>
        {
            Persist(new Events.ConfirmOrderFailed(OrderId, "This order has been cancelled"), On);
        });

        Command<Commands.CancelOrder>(_ =>
        {
            Persist(new Events.OrderCancellationFailed(OrderId, "This order has already been cancelled"), On);
        });
    }

    private void PaymentEvents()
    {
        ChildEvent<Payment.Events.PaymentCharged>(evnt =>
        {
            Persist(new Events.OrderPaymentCharged(OrderId, evnt.PaymentId), paymentCharged =>
            {
                On(paymentCharged);

                if (IsFullyCharged())
                    Persist(new Events.OrderConfirmed(OrderId), On);
            });
        });

        ChildEvent<Payment.Events.PaymentRefunded>(evnt =>
        {
            Persist(new Events.OrderPaymentRefunded(OrderId, evnt.PaymentId), paymentRefunded =>
            {
                On(paymentRefunded);

                if (IsFullyRefunded())
                    Persist(new Events.OrderCancelled(OrderId), On);
            });
        });
    }

    private void On(Events.OrderPlaced evnt)
    {
        _productName = evnt.ProductName;
        _productPrice = evnt.ProductPrice;

        Become(Placed);
    }

    private void On(Events.PlaceOrderFailed evnt)
    {
    }

    private void On(Events.PaymentAddedToOrder evnt)
    {
        _payments[evnt.PaymentId] = new PaymentInformation(evnt.PaymentId, evnt.Amount);

        if (IsComplete())
            Become(Complete);
    }

    private void On(Events.FailedAddingPaymentToOrder evnt)
    {
    }

    private void On(Events.OrderPaymentCharged evnt)
    {
        if (_payments.ContainsKey(evnt.PaymentId))
            _payments[evnt.PaymentId].UpdateStatus(PaymentInformation.PaymentStatus.Charged);
    }

    private void On(Events.OrderPaymentRefunded evnt)
    {
        if (_payments.ContainsKey(evnt.PaymentId))
            _payments[evnt.PaymentId].UpdateStatus(PaymentInformation.PaymentStatus.Refunded);
    }

    private void On(Events.OrderCompleted evnt)
    {
        Become(Complete);
    }

    private void On(Events.OrderConfirmed evnt)
    {
        Become(Confirmed);
    }

    private void On(Events.ConfirmOrderFailed evnt)
    {
    }

    private void On(Events.OrderCancelled evnt)
    {
        Become(Cancelled);
    }

    private void On(Events.OrderCancellationFailed evnt)
    {
    }

    private void ChildEvent<TEvent>(Action<TEvent> handler)
    {
        Command(handler);
    }

    private void CancelOrder()
    {
        var paymentsToRefund = _payments
            .Where(x => x.Value.Status != PaymentInformation.PaymentStatus.Refunded)
            .ToList();

        foreach (var payment in paymentsToRefund)
            payment.Value.Refund();
    }

    private decimal CalculateAmountLeftToPay()
    {
        return _productPrice - _payments.Sum(x => x.Value.Amount);
    }

    private bool IsComplete()
    {
        var completePaymentsStatuses = new List<PaymentInformation.PaymentStatus>
        {
            PaymentInformation.PaymentStatus.Initialized,
            PaymentInformation.PaymentStatus.Charged
        };

        return _productPrice <= _payments
            .Where(x => completePaymentsStatuses.Contains(x.Value.Status))
            .Sum(x => x.Value.Amount);
    }

    private bool IsFullyCharged()
    {
        return _productPrice <= _payments
            .Where(x => x.Value.Status == PaymentInformation.PaymentStatus.Charged)
            .Sum(x => x.Value.Amount);
    }

    private bool IsFullyRefunded()
    {
        return _payments.All(x => x.Value.Status == PaymentInformation.PaymentStatus.Refunded);
    }

    private class PaymentInformation
    {
        private readonly IActorRef _payment;

        public PaymentInformation(string id, decimal amount)
        {
            Amount = amount;
            _payment = GetPayment(id);
            Status = PaymentStatus.Initialized;
        }

        public decimal Amount { get; }
        public PaymentStatus Status { get; private set; }

        public void Charge()
        {
            _payment.Tell(new Payment.Commands.ChargePayment());
        }

        public void Refund()
        {
            _payment.Tell(new Payment.Commands.RefundPayment());
        }

        public void UpdateStatus(PaymentStatus status)
        {
            Status = status;
        }

        private IActorRef GetPayment(string paymentId)
        {
            var payment = Context.Child(paymentId);

            if (Equals(payment, ActorRefs.Nobody))
                payment = Context.ActorOf(Payment.Initialize(Amount), paymentId);

            return payment;
        }

        public enum PaymentStatus
        {
            Initialized,
            Charged,
            Refunded
        }
    }
}