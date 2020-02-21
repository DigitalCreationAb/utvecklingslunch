using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace _06_akka.safe_process
{
    public class Payment : ReceivePersistentActor
    {
        public static class Commands
        {
            public class StartChargingPayment
            {
                
            }
            
            public class StartRefundingPayment
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
        
        public static class Responses
        {
            public class ChargingProcessResponse
            {
                public ChargingProcessResponse(string paymentId, decimal amount, params string[] errors)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                    Errors = errors.ToImmutableList();
                }

                public string PaymentId { get; }
                public decimal Amount { get; }
                public IImmutableList<string> Errors { get; }

                public bool Success => !Errors.Any();
            }
            
            public class RefundingProcessResponse
            {
                public RefundingProcessResponse(string paymentId, decimal amount, params string[] errors)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                    Errors = errors.ToImmutableList();
                }

                public string PaymentId { get; }
                public decimal Amount { get; }
                public IImmutableList<string> Errors { get; }

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
            Command<Commands.StartChargingPayment>(cmd =>
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
            
            Command<Commands.StartRefundingPayment>(cmd =>
            {
                Persist(new Events.PaymentRefunded(PaymentId, _amount), On);
                
                Context.Parent.Tell(new Responses.RefundingProcessResponse(PaymentId, _amount));
            });
        }

        private void Charged()
        {
            Command<Commands.StartChargingPayment>(cmd =>
            {
                Context.Parent.Tell(new Responses.ChargingProcessResponse(PaymentId, _amount));
            });
            
            Command<Commands.StartRefundingPayment>(cmd =>
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
            Command<Commands.StartChargingPayment>(cmd =>
            {
                Context.Parent.Tell(new Responses.ChargingProcessResponse(PaymentId, _amount, "This payment has been refunded"));
            });
            
            Command<Commands.StartRefundingPayment>(cmd =>
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
}