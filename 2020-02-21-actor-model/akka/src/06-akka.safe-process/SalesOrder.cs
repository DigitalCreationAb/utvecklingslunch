using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace _06_akka.safe_process
{
    public class SalesOrder : ReceivePersistentActor
    {
        public static class Commands
        {
            public class StartOrderProcess
            {
                public StartOrderProcess(string productName, decimal productPrice)
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
            
            public class FinishOrder
            {
                
            }

            public class StartCancellationProcess
            {
                
            }
            
            public class ContinueOrderProcess
            {
                
            }
            
            public class ContinueCalcellationProcess
            {
                
            }
        }
        
        public class Events
        {
            public class OrderProcessStarted
            {
                public OrderProcessStarted(string orderId, string productName, decimal productPrice)
                {
                    OrderId = orderId;
                    ProductName = productName;
                    ProductPrice = productPrice;
                }

                public string OrderId { get; }
                public string ProductName { get; }
                public decimal ProductPrice { get; }

                public override string ToString()
                {
                    return
                        $"Order {OrderId} was started. Product name: \"{ProductName}\", Product price: {ProductPrice:N2}";
                }
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

                public override string ToString()
                {
                    return $"Payment {PaymentId} added to order {OrderId}. Amount: {Amount:N2}";
                }
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

                public override string ToString()
                {
                    return $"Failed adding payment to order {OrderId}. Reason: \"{Reason}\"";
                }
            }
            
            public class OrderCharged
            {
                public OrderCharged(string orderId, string paymentId, decimal amount)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
                public decimal Amount { get; }

                public override string ToString()
                {
                    return $"Charged payment {PaymentId} for order {OrderId}. Amount: {Amount:N2}";
                }
            }
            
            
            public class FailedChargingOrder
            {
                public FailedChargingOrder(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }

                public override string ToString()
                {
                    return $"Failed charging payment {PaymentId} on order {OrderId}";
                }
            }
            
            public class OrderCompleted
            {
                public OrderCompleted(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} was completed";
                }
            }
            
            public class OrderFinished
            {
                public OrderFinished(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} was finished";
                }
            }
            
            public class OrderRefunded
            {
                public OrderRefunded(string orderId, string paymentId, decimal amount)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
                public decimal Amount { get; }
                
                public override string ToString()
                {
                    return $"Order {OrderId} refunded payment {PaymentId} with amount {Amount:N2}";
                }
            }
            
            public class FailedRefundingOrder
            {
                public FailedRefundingOrder(string orderId, string paymentId)
                {
                    OrderId = orderId;
                    PaymentId = paymentId;
                }

                public string OrderId { get; }
                public string PaymentId { get; }
                
                public override string ToString()
                {
                    return $"Failed refunding payment {PaymentId} on order {OrderId}";
                }
            }
            
            public class OrderCancellationProcessStarted
            {
                public OrderCancellationProcessStarted(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Started cancellation process for {OrderId}";
                }
            }
            
            public class OrderCancelled
            {
                public OrderCancelled(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} was cancelled";
                }
            }
            
            public class OrderProcessFailed
            {
                public OrderProcessFailed(string orderId, string reason)
                {
                    OrderId = orderId;
                    Reason = reason;
                }

                public string OrderId { get; }
                public string Reason { get; }

                public override string ToString()
                {
                    return $"Order process {OrderId} failed. Reason: {Reason}";
                }
            }

            public class OrderCancellationProcessFailed
            {
                public OrderCancellationProcessFailed(string orderId, string reason)
                {
                    OrderId = orderId;
                    Reason = reason;
                }

                public string OrderId { get; }
                public string Reason { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} cancellation failed. Reason: {Reason}";
                }
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
            
            public class OrderProcessDoneResponse
            {
                public OrderProcessDoneResponse(string orderId, params string[] errors)
                {
                    OrderId = orderId;
                    Errors = errors.ToImmutableList();
                }

                public string OrderId { get; }
                public IImmutableList<string> Errors { get; }
                public bool Success => !Errors.Any();
            }
            
            public class CancellationProcessDoneResponse
            {
                public CancellationProcessDoneResponse(string orderId, params string[] errors)
                {
                    OrderId = orderId;
                    Errors = errors.ToImmutableList();
                }

                public string OrderId { get; }
                public IImmutableList<string> Errors { get; }
                public bool Success => !Errors.Any();
            }
        }

        private string _productName;
        private decimal _productPrice;

        private readonly IDictionary<string, PaymentInformation> _payments =
            new Dictionary<string, PaymentInformation>();
        
        private readonly IDictionary<string, decimal> _chargedPayments = new Dictionary<string, decimal>();
        private readonly IDictionary<string, decimal> _refundedPayments = new Dictionary<string, decimal>();

        public override string PersistenceId => $"salesorders-{Self.Path.Name}";
        private string OrderId => Self.Path.Name;
        
        public SalesOrder()
        {
            Recover<Events.OrderProcessStarted>(On);
            Recover<Events.PaymentAddedToOrder>(On);
            Recover<Events.FailedAddingPaymentToOrder>(On);
            Recover<Events.OrderCharged>(On);
            Recover<Events.FailedChargingOrder>(On);
            Recover<Events.OrderRefunded>(On);
            Recover<Events.OrderCompleted>(On);
            Recover<Events.OrderFinished>(On);
            Recover<Events.OrderCancellationProcessStarted>(On);
            Recover<Events.OrderCancelled>(On);
            Recover<Events.OrderCancellationProcessFailed>(On);
            Recover<Events.OrderProcessFailed>(On);
            
            Become(New);
        }

        public static Props Initialize()
        {
            return Props.Create<SalesOrder>();
        }

        private void New()
        {
            Command<Commands.StartOrderProcess>(cmd =>
            {
                Persist(new Events.OrderProcessStarted(OrderId, cmd.ProductName, cmd.ProductPrice), On);
            });
        }

        private void Started()
        {
            ListenToPaymentProcess();

            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Started"));
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
                
                Persist(new Events.PaymentAddedToOrder(OrderId, paymentId, cmd.Amount), evnt =>
                {
                    On(evnt);
                    
                    if (IsComplete())
                        Persist(new Events.OrderCompleted(OrderId), On);
                });
            });

            Command<Commands.StartCancellationProcess>(cmd =>
            {
                Persist(new Events.OrderCancelled(OrderId), orderCancelled =>
                {
                    On(orderCancelled);
                                
                    Context.Parent.Tell(new Responses.CancellationProcessDoneResponse(OrderId));
                });
            });
        }

        private void Complete()
        {
            ListenToPaymentProcess();
            
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Complete"));
            });

            Command<Commands.FinishOrder>(cmd =>
            {
                foreach (var payment in _payments)
                    payment.Value.Charge();
            });
            
            Command<Commands.StartCancellationProcess>(cmd =>
            {
                foreach (var payment in _payments)
                    payment.Value.Refund();
            });
        }
        
        private void Finished()
        {
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Finished"));
            });
        }

        private void Cancelled()
        {
            Command<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Cancelled"));
            });
        }

        private void ListenToPaymentProcess()
        {
            Command<Payment.Responses.ChargingProcessResponse>(response =>
            {
                if (response.Success)
                {
                    Persist(new Events.OrderCharged(OrderId, response.PaymentId, response.Amount), orderCharged =>
                    {
                        On(orderCharged);

                        if (IsFullyCharged())
                        {
                            Persist(new Events.OrderFinished(OrderId), orderFinished =>
                            {
                                On(orderFinished);
                                
                                Context.Parent.Tell(new Responses.OrderProcessDoneResponse(OrderId));
                            });
                        }
                    });   
                }
                else
                {
                    Persist(new Events.FailedChargingOrder(OrderId, response.PaymentId), failedChargingOrder =>
                    {
                        On(failedChargingOrder);

                        if (_payments[response.PaymentId].HasFailedChargingTooManyTimes())
                        {
                            Persist(new Events.OrderProcessFailed(OrderId, "Payment failed too many times"), On);
                            
                            Context.Parent.Tell(new Responses.OrderProcessDoneResponse(OrderId, "Payment failed too many times"));
                        }
                        else
                        {
                            _payments[response.PaymentId].Charge();
                        }
                    });
                }
            });
            
            Command<Payment.Responses.RefundingProcessResponse>(response =>
            {
                if (response.Success)
                {
                    Persist(new Events.OrderRefunded(OrderId, response.PaymentId, response.Amount), evnt =>
                    {
                        On(evnt);

                        if (IsFullyRefunded())
                        {
                            Persist(new Events.OrderCancelled(OrderId), orderCancelled =>
                            {
                                On(orderCancelled);
                                
                                Context.Parent.Tell(new Responses.CancellationProcessDoneResponse(OrderId));
                            });
                        }
                    });   
                }
                else
                {
                    Persist(new Events.FailedRefundingOrder(OrderId, response.PaymentId), failedRefundingOrder =>
                    {
                        On(failedRefundingOrder);

                        if (_payments[response.PaymentId].HasFailedChargingTooManyTimes())
                        {
                            Persist(new Events.OrderCancellationProcessFailed(OrderId, "Payment refund failed too many times"), On);
                            
                            Context.Parent.Tell(new Responses.CancellationProcessDoneResponse(OrderId, "Payment refund failed too many times"));
                        }
                        else
                        {
                            _payments[response.PaymentId].Refund();
                        }
                    });
                }
            });
        }

        private void OrderFailed()
        {
            //Here we can add calls to manually change order state etc.
        }

        private void CancellationFailed()
        {
            //Here we can add calls to manually change order state etc.
        }
        
        private void On(Events.OrderProcessStarted evnt)
        {
            _productName = evnt.ProductName;
            _productPrice = evnt.ProductPrice;
            
            Become(Started);
        }
        
        private void On(Events.PaymentAddedToOrder evnt)
        {
            _payments[evnt.PaymentId] = new PaymentInformation(evnt.PaymentId, evnt.Amount);
        }

        private void On(Events.FailedAddingPaymentToOrder evnt)
        {
            
        }

        private void On(Events.OrderCharged evnt)
        {
            _chargedPayments[evnt.PaymentId] = evnt.Amount;
        }

        private void On(Events.FailedChargingOrder evnt)
        {
            if (_payments.ContainsKey(evnt.PaymentId))
                _payments[evnt.PaymentId].FailedChargeAttempt();
        }

        private void On(Events.OrderRefunded evnt)
        {
            _refundedPayments[evnt.PaymentId] = evnt.Amount;
        }

        private void On(Events.FailedRefundingOrder evnt)
        {
            if (_payments.ContainsKey(evnt.PaymentId))
                _payments[evnt.PaymentId].FailedRefundAttempt();
        }
        
        private void On(Events.OrderCompleted evnt)
        {
            Become(Complete);
        }

        private void On(Events.OrderFinished evnt)
        {
            Become(Finished);
        }

        private void On(Events.OrderCancellationProcessStarted evnt)
        {
            
        }

        private void On(Events.OrderCancelled evnt)
        {
            Become(Cancelled);
        }

        private void On(Events.OrderCancellationProcessFailed evnt)
        {
            Become(CancellationFailed);
        }

        private void On(Events.OrderProcessFailed evnt)
        {
            Become(OrderFailed);
        }
        
        private decimal CalculateAmountLeftToPay()
        {
            return _productPrice - _payments.Sum(x => x.Value.Amount);
        }

        private bool IsComplete()
        {
            return _productPrice <= _payments
                       .Where(x => !_refundedPayments.ContainsKey(x.Key))
                       .Sum(x => x.Value.Amount);
        }

        private bool IsFullyCharged()
        {
            return _productPrice <= _chargedPayments.Sum(x => x.Value);
        }

        private bool IsFullyRefunded()
        {
            return _payments.All(x => _refundedPayments.ContainsKey(x.Key));
        }

        private class PaymentInformation
        {
            private readonly IActorRef _payment;
            
            public PaymentInformation(string id, decimal amount)
            {
                Amount = amount;
                FailedChargeAttempts = 0;
                FailedRefundAttempts = 0;
                _payment = GetPayment(id);
            }

            public decimal Amount { get; }
            public int FailedChargeAttempts { get; private set; }
            public int FailedRefundAttempts { get; private set; }

            public bool HasFailedChargingTooManyTimes()
            {
                return FailedChargeAttempts >= 5;
            }

            public bool HasFailedRefundingTooManyTimes()
            {
                return FailedRefundAttempts >= 5;
            }
            
            public void Charge()
            {
                _payment.Tell(new Payment.Commands.StartChargingPayment());
            }

            public void Refund()
            {
                _payment.Tell(new Payment.Commands.StartRefundingPayment());
            }

            public void FailedChargeAttempt()
            {
                FailedChargeAttempts++;
            }

            public void FailedRefundAttempt()
            {
                FailedRefundAttempts++;
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