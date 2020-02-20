using System;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async
{
    public class Payment : ReceivePersistentActor
    {
        public static class Commands
        {
            public class ChargePayment
            {
                
            }
            
            public class RefundPayment
            {
                
            }
        }
        
        public static class Events
        {
            public class PaymentCharged
            {
                public PaymentCharged(string paymentId, decimal amount)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string PaymentId { get; }
                public decimal Amount { get; }

                public override string ToString()
                {
                    return $"Payment {PaymentId} charged with the amount {Amount:N2}";
                }
            }
            
            public class PaymentChargeFailed
            {
                public PaymentChargeFailed(string paymentId, string reason)
                {
                    PaymentId = paymentId;
                    Reason = reason;
                }

                public string PaymentId { get; }
                public string Reason { get; }

                public override string ToString()
                {
                    return $"Payment {PaymentId} failed charging. Reason: \"{Reason}\"";
                }
            }
            
            public class PaymentRefunded
            {
                public PaymentRefunded(string paymentId, decimal amount)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string PaymentId { get; }
                public decimal Amount { get; }

                public override string ToString()
                {
                    return $"Payment {PaymentId} refunded the amount {Amount}";
                }
            }
            
            public class PaymentRefundFailed
            {
                public PaymentRefundFailed(string paymentId, string reason)
                {
                    PaymentId = paymentId;
                    Reason = reason;
                }

                public string PaymentId { get; }
                public string Reason { get; }

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
            Command<Commands.ChargePayment>(cmd =>
            {
                PersistAndNotifyParent(new Events.PaymentCharged(PaymentId, _amount), On);
            });
            
            Command<Commands.RefundPayment>(cmd =>
            {
                PersistAndNotifyParent(new Events.PaymentRefunded(PaymentId, _amount), On);
            });
        }

        private void Charged()
        {
            Command<Commands.ChargePayment>(cmd =>
            {
                PersistAndNotifyParent(
                    new Events.PaymentChargeFailed(PaymentId, "This payment has already been charged"), On);
            });
            
            Command<Commands.RefundPayment>(cmd =>
            {
                PersistAndNotifyParent(new Events.PaymentRefunded(PaymentId, _amount), On);
            });
        }

        private void Refunded()
        {
            Command<Commands.ChargePayment>(cmd =>
            {
                PersistAndNotifyParent(
                    new Events.PaymentChargeFailed(PaymentId, "This payment has been refunded"), On);
            });
            
            Command<Commands.RefundPayment>(cmd =>
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
}