using Akka.Actor;
using Akka.Persistence;

namespace _04_akka.persistence
{
    public class Payment : ReceivePersistentActor
    {
        public static class Commands
        {
            public class InitializePayment
            {
                public InitializePayment(decimal amount)
                {
                    Amount = amount;
                }

                public decimal Amount { get; }
            }
            
            public class ChargePayment
            {
                
            }
            
            public class RefundPayment
            {
                
            }
        }
        
        public static class Events
        {
            public class PaymentInitialized
            {
                public PaymentInitialized(string paymentId, decimal amount)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string PaymentId { get; }
                public decimal Amount { get; }
            }
            
            public class PaymentCharged
            {
                public PaymentCharged(string paymentId, decimal amount)
                {
                    PaymentId = paymentId;
                    Amount = amount;
                }

                public string PaymentId { get; }
                public decimal Amount { get; }
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
            }
        }
        
        public static class Responses
        {
            public class InitializePaymentResponse
            {
                public InitializePaymentResponse(decimal amount, string errorMessage = "")
                {
                    Amount = amount;
                    ErrorMessage = errorMessage;
                }

                public decimal Amount { get; }
                public string ErrorMessage { get; }
                public bool Success => string.IsNullOrEmpty(ErrorMessage);
            }
            
            public class ChargePaymentResponse
            {
                public ChargePaymentResponse(decimal amount, string errorMessage = "")
                {
                    Amount = amount;
                    ErrorMessage = errorMessage;
                }

                public decimal Amount { get; }
                public string ErrorMessage { get; }
                public bool Success => string.IsNullOrEmpty(ErrorMessage);
            }
            
            public class RefundPaymentResponse
            {
                public RefundPaymentResponse(decimal amount, string errorMessage = "")
                {
                    Amount = amount;
                    ErrorMessage = errorMessage;
                }

                public decimal Amount { get; }
                public string ErrorMessage { get; }
                public bool Success => string.IsNullOrEmpty(ErrorMessage);
            }
        }

        private decimal _amount;

        public override string PersistenceId => $"payments-{PaymentId}";

        private string PaymentId => Self.Path.Name;

        public Payment()
        {
            Recover<Events.PaymentInitialized>(On);
            Recover<Events.PaymentCharged>(On);
            Recover<Events.PaymentRefunded>(On);
            
            Become(New);
        }

        private void New()
        {
            Command<Commands.InitializePayment>(cmd =>
            {
                Persist(new Events.PaymentInitialized(PaymentId, cmd.Amount), evnt =>
                {
                    On(evnt);
                    
                    Sender.Tell(new Responses.InitializePaymentResponse(_amount));
                });
            });

            Command<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment hasn't been initialized yet"));
            });

            Command<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment hasn't been initialized yet"));
            });
        }

        private void Initialized()
        {
            Command<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Command<Commands.ChargePayment>(cmd =>
            {
                Persist(new Events.PaymentCharged(PaymentId, _amount), evnt =>
                {
                    On(evnt);
                    
                    Sender.Tell(new Responses.ChargePaymentResponse(_amount));
                });
            });
            
            Command<Commands.RefundPayment>(cmd =>
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
            Command<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Command<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has already been charged"));
            });
            
            Command<Commands.RefundPayment>(cmd =>
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
            Command<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Command<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has been refunded"));
            });
            
            Command<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment has already been refunded"));
            });
        }

        private void On(Events.PaymentInitialized evnt)
        {
            _amount = evnt.Amount;
            
            Become(Initialized);
        }

        private void On(Events.PaymentCharged evnt)
        {
            Become(Charged);
        }

        private void On(Events.PaymentRefunded evnt)
        {
            Become(Refunded);
        }
    }
}