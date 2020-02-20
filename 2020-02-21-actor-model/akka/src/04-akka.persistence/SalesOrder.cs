using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;

namespace _04_akka.persistence
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
            
            public class PaymentInitialized
            {
                public PaymentInitialized(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
            }
            
            public class PaymentCharged
            {
                public PaymentCharged(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
            }
            
            public class PaymentRefunded
            {
                public PaymentRefunded(string orderId, string paymentId)
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
            
            public class OrderCancelled
            {
                public OrderCancelled(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
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
            public class PlaceOrderResponse
            {
                public PlaceOrderResponse(params string[] errors)
                {
                    Errors = errors.ToImmutableList();
                }
                
                public IImmutableList<string> Errors { get; }

                public bool Success => !Errors.Any();
            }
            
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
            
            public class AddPaymentResponse
            {
                public AddPaymentResponse(string paymentId, string errorMessage)
                {
                    PaymentId = paymentId;
                    ErrorMessage = errorMessage;
                }

                public string PaymentId { get; }
                public string ErrorMessage { get; }
                public bool Success => string.IsNullOrEmpty(ErrorMessage);
            }
            
            public class ConfirmOrderResponse
            {
                public ConfirmOrderResponse(params string[] errors)
                {
                    Errors = errors.ToImmutableList();
                }
                
                public IImmutableList<string> Errors { get; }

                public bool Success => !Errors.Any();
            }
            
            public class CancelOrderResponse
            {
                public CancelOrderResponse(params string[] errors)
                {
                    Errors = errors.ToImmutableList();
                }
                
                public IImmutableList<string> Errors { get; }

                public bool Success => !Errors.Any();
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
            Recover<Events.PaymentAddedToOrder>(On);
            Recover<Events.PaymentInitialized>(On);
            Recover<Events.PaymentCharged>(On);
            Recover<Events.PaymentRefunded>(On);
            Recover<Events.OrderCompleted>(On);
            Recover<Events.OrderConfirmed>(On);
            Recover<Events.OrderCancelled>(On);
            
            Become(New);
        }

        private void New()
        {
            Command<Commands.PlaceOrder>(cmd =>
            {
                Persist(new Events.OrderPlaced(OrderId, cmd.ProductName, cmd.ProductPrice), evnt =>
                {
                    On(evnt);
                    
                    Sender.Tell(new Responses.PlaceOrderResponse());
                });
            });

            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "New"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You can't add a payment to a order that hasn't been placed yet"));
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order hasn't been completed"));
            });
            
            Command<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order hasn't been completed yet"));
            });
        }

        private void Placed()
        {
            Command<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
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
                    Sender.Tell(new Responses.AddPaymentResponse("", $"The amount {cmd.Amount:N2} is higher then what left to pay for this order ({amountLeftToPay})"));
                    
                    return;
                }
                
                var paymentIncrement = (_payments.Count + 1).ToString();

                var paymentId = $"{OrderId}-{paymentIncrement}";
                
                Persist(new Events.PaymentAddedToOrder(OrderId, paymentId, cmd.Amount), paymentAdded =>
                {
                    On(paymentAdded);

                    var response = _payments[paymentAdded.PaymentId].Initialize().Result;

                    if (!response.Success)
                    {
                        Sender.Tell(new Responses.AddPaymentResponse(paymentId, response.ErrorMessage));
                    
                        return;
                    }
                    
                    Persist(new Events.PaymentInitialized(OrderId, paymentId), paymentInitialized =>
                    {
                        On(paymentInitialized);
                        
                        if (IsComplete())
                            Persist(new Events.OrderCompleted(OrderId), On);
                    
                        Sender.Tell(new Responses.AddPaymentResponse(paymentId, response.ErrorMessage));
                    });
                });
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order hasn't been completed"));
            });
            
            Command<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order hasn't been completed yet"));
            });
        }

        private void Complete()
        {
            Command<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Complete"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You have already added enough payments to this order"));
            });
            
            CommandAsync<Commands.ConfirmOrder>(async cmd =>
            {
                var paymentsToCharge = _payments
                    .Where(x => x.Value.Status == PaymentInformation.PaymentStatus.Initialized)
                    .ToList();
                
                var errors = new List<string>();
                var chargedPayments = new List<string>();

                foreach (var payment in paymentsToCharge)
                {
                    var response = await payment.Value.Charge();

                    if (!response.Success)
                        errors.Add(response.ErrorMessage);
                    else
                        chargedPayments.Add(payment.Key);
                }

                if (!chargedPayments.Any())
                {
                    Sender.Tell(new Responses.ConfirmOrderResponse(errors.ToArray()));
                    
                    return;
                }
                
                PersistAll(chargedPayments
                    .Select(paymentId => new Events.PaymentCharged(OrderId, paymentId)), paymentCharged =>
                {
                    On(paymentCharged);
                   
                    if (IsFullyCharged())
                    {
                        Persist(new Events.OrderConfirmed(OrderId), orderConfirmed =>
                        {
                            On(orderConfirmed);
                        });
                    }
                });
                
                Sender.Tell(new Responses.ConfirmOrderResponse());
            });
            
            CommandAsync<Commands.CancelOrder>(cmd => CancelOrder());
        }
        
        private void Confirmed()
        {
            Command<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Confirmed"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You have already added enough payments to this order"));
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order has already been confirmed"));
            });
            
            CommandAsync<Commands.CancelOrder>(cmd => CancelOrder());
        }

        private void Cancelled()
        {
            Command<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has been cancelled"));
            });
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Cancelled"));
            });
            
            Command<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "This order has been cancelled"));
            });
            
            Command<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order has been cancelled"));
            });
            
            Command<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order has already been cancelled"));
            });
        }

        private void On(Events.OrderPlaced evnt)
        {
            _productName = evnt.ProductName;
            _productPrice = evnt.ProductPrice;
            
            Become(Placed);
        }

        private void On(Events.PaymentAddedToOrder evnt)
        {
            _payments[evnt.PaymentId] = new PaymentInformation(evnt.PaymentId, evnt.Amount);
        }

        private void On(Events.PaymentInitialized evnt)
        {
            if (_payments.ContainsKey(evnt.PaymentId))
                _payments[evnt.PaymentId].UpdateStatus(PaymentInformation.PaymentStatus.Initialized);
        }

        private void On(Events.PaymentCharged evnt)
        {
            if (_payments.ContainsKey(evnt.PaymentId))
                _payments[evnt.PaymentId].UpdateStatus(PaymentInformation.PaymentStatus.Charged);
        }
        
        private void On(Events.PaymentRefunded evnt)
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

        private void On(Events.OrderCancelled evnt)
        {
            Become(Cancelled);
        }

        private async Task CancelOrder()
        {
            var paymentsToRefund = _payments
                .Where(x => x.Value.Status != PaymentInformation.PaymentStatus.Refunded)
                .ToList();

            var errors = new List<string>();
            var refundedPayments = new List<string>();

            foreach (var payment in paymentsToRefund)
            {
                var response = await payment.Value.Refund();
                    
                if (!response.Success)
                    errors.Add(response.ErrorMessage);
                else
                    refundedPayments.Add(payment.Key);
            }

            if (!refundedPayments.Any())
            {
                Sender.Tell(new Responses.CancelOrderResponse(errors.ToArray()));
                    
                return;
            }
            
            PersistAll(refundedPayments
                .Select(paymentId => new Events.PaymentRefunded(OrderId, paymentId)), paymentRefunded =>
            {
                On(paymentRefunded);
                
                if (IsFullyRefunded())
                {
                    Persist(new Events.OrderCancelled(OrderId), orderCancelled =>
                    {
                        On(orderCancelled);
                        
                        Sender.Tell(new Responses.CancelOrderResponse());
                    });
                }
            });
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
                Status = PaymentStatus.New;
            }

            public decimal Amount { get; }
            public PaymentStatus Status { get; private set; }

            public Task<Payment.Responses.InitializePaymentResponse> Initialize()
            {
                return _payment.Ask<Payment.Responses.InitializePaymentResponse>(
                    new Payment.Commands.InitializePayment(Amount));
            }

            public Task<Payment.Responses.ChargePaymentResponse> Charge()
            {
                return _payment.Ask<Payment.Responses.ChargePaymentResponse>(new Payment.Commands.ChargePayment());
            }

            public Task<Payment.Responses.RefundPaymentResponse> Refund()
            {
                return _payment.Ask<Payment.Responses.RefundPaymentResponse>(new Payment.Commands.RefundPayment());
            }

            public void UpdateStatus(PaymentStatus status)
            {
                Status = status;
            }
            
            private static IActorRef GetPayment(string paymentId)
            {
                var payment = Context.Child(paymentId);

                if (Equals(payment, ActorRefs.Nobody))
                    payment = Context.ActorOf<Payment>(paymentId);

                return payment;
            }
            
            public enum PaymentStatus
            {
                New,
                Initialized,
                Charged,
                Refunded
            }
        }
    }
}