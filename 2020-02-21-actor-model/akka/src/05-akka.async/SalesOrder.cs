using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async
{
    public class SalesOrder : ReceivePersistentActor
    {
        public static class Commands
        {
            public class PlaceOrder
            {
                public PlaceOrder(string productName, decimal productPrice)
                {
                    ProductName = productName;
                    ProductPrice = productPrice;
                }

                public string ProductName { get; }
                public decimal ProductPrice { get; }
            }
            
            public class AddPayment
            {
                public AddPayment(decimal amount)
                {
                    Amount = amount;
                }

                public decimal Amount { get; }
            }
            
            public class ConfirmOrder
            {
                
            }

            public class CancelOrder
            {
                
            }
        }
        
        public class Events
        {
            public class OrderPlaced
            {
                public OrderPlaced(string orderId, string productName, decimal productPrice)
                {
                    OrderId = orderId;
                    ProductName = productName;
                    ProductPrice = productPrice;
                }

                public string OrderId { get; }
                public string ProductName { get; }
                public decimal ProductPrice { get; }
            }
            
            public class PlaceOrderFailed
            {
                public PlaceOrderFailed(string orderId, string reason)
                {
                    OrderId = orderId;
                    Reason = reason;
                }

                public string OrderId { get; }
                public string Reason { get; }
            }
            
            public class PaymentAddedToOrder
            {
                public PaymentAddedToOrder(string orderId, string paymentId, decimal amount)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
                public decimal Amount { get; }
            }
            
            public class FailedAddingPaymentToOrder
            {
                public FailedAddingPaymentToOrder(string orderId, decimal amount, string reason)
                {
                    OrderId = orderId;
                    Amount = amount;
                    Reason = reason;
                }

                public string OrderId { get; }
                public decimal Amount { get; }
                public string Reason { get; }
            }
            
            public class OrderPaymentCharged
            {
                public OrderPaymentCharged(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
            }
            
            public class OrderPaymentRefunded
            {
                public OrderPaymentRefunded(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
            }
            
            public class OrderCompleted
            {
                public OrderCompleted(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
            }
            
            public class OrderConfirmed
            {
                public OrderConfirmed(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
            }
            
            public class ConfirmOrderFailed
            {
                public ConfirmOrderFailed(string orderId, string reason)
                {
                    OrderId = orderId;
                    Reason = reason;
                }

                public string OrderId { get; }
                public string Reason { get; }
            }
            
            public class OrderCancelled
            {
                public OrderCancelled(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
            }
            
            public class OrderCancellationFailed
            {
                public OrderCancellationFailed(string orderId, string reason)
                {
                    OrderId = orderId;
                    Reason = reason;
                }

                public string OrderId { get; }
                public string Reason { get; }
            }
        }
        
        public static class Queries
        {
            public class GetOrderData
            {
                
            }
        }
        
        public static class Responses
        {
            public class OrderDataResponse
            {
                public OrderDataResponse(string productName, decimal productPrice, string status)
                {
                    ProductName = productName;
                    ProductPrice = productPrice;
                    Status = status;
                }

                public string ProductName { get; }
                public decimal ProductPrice { get; }
                public string Status { get; }
            }
        }

        private string _productName;
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

        private void New()
        {
            PaymentEvents();
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.OrderPlaced(OrderId, cmd.ProductName, cmd.ProductPrice), On);
            });

            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "New"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Persist(new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount, "This order hasn't been placed yet"), On);
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Persist(new Events.ConfirmOrderFailed(OrderId, "This order hasn't been completed"), On);
            });

            Command<Commands.CancelOrder>(cmd =>
            {
                Persist(new Events.OrderCancellationFailed(OrderId, "This order hasn't been completed yet"), On);
            });
        }

        private void Placed()
        {
            PaymentEvents();
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Placed"));
            });
            
            Command<Commands.AddPayment>( cmd =>
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
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Persist(new Events.ConfirmOrderFailed(OrderId, "This order hasn't been completed"), On);
            });

            Command<Commands.CancelOrder>(cmd =>
            {
                Persist(new Events.OrderCancellationFailed(OrderId, "This order hasn't been completed yet"), On);
            });
        }

        private void Complete()
        {
            PaymentEvents();
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Complete"));
            });

            Command<Commands.AddPayment>(cmd =>
            {
                Persist(
                    new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                        "You have already added enough payments to this order"), On);
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                var paymentsToCharge = _payments
                    .Where(x => x.Value.Status == PaymentInformation.PaymentStatus.Initialized)
                    .ToList();

                foreach (var payment in paymentsToCharge)
                    payment.Value.Charge();
            });
            
            Command<Commands.CancelOrder>(cmd => CancelOrder());
        }
        
        private void Confirmed()
        {
            PaymentEvents();
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.PlaceOrderFailed(OrderId, "This order has already been placed"), On);
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Confirmed"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Persist(
                    new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                        "You have already added enough payments to this order"), On);
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Persist(new Events.ConfirmOrderFailed(OrderId, "This order has already been confirmed"), On);
            });
            
            Command<Commands.CancelOrder>(cmd => CancelOrder());
        }

        private void Cancelled()
        {
            PaymentEvents();
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.PlaceOrderFailed(OrderId, "This order has been cancelled"), On);
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Cancelled"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Persist(
                    new Events.FailedAddingPaymentToOrder(OrderId, cmd.Amount,
                        "This order has been cancelled"), On);
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Persist(new Events.ConfirmOrderFailed(OrderId, "This order has been cancelled"), On);
            });
            
            Command<Commands.CancelOrder>(cmd =>
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
}