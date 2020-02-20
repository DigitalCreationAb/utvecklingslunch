using Akka.Actor;

namespace _03_akka.children
{
    public class Payment : ReceiveActor
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

        public Payment()
        {
            Become(New);
        }

        private void New()
        {
            Receive<Commands.InitializePayment>(cmd =>
            {
                _amount = cmd.Amount;
                
                Become(Initialized);
                
                Sender.Tell(new Responses.InitializePaymentResponse(_amount));
            });

            Receive<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment hasn't been initialized yet"));
            });

            Receive<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment hasn't been initialized yet"));
            });
        }

        private void Initialized()
        {
            Receive<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Receive<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(_amount));
                
                Become(Charged);
            });
            
            Receive<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(_amount));
                
                Become(Refunded);
            });
        }

        private void Charged()
        {
            Receive<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Receive<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has already been charged"));
            });
            
            Receive<Commands.RefundPayment>(cmd =>
            {
                Become(Refunded);
                
                Sender.Tell(new Responses.RefundPaymentResponse(_amount));
            });
        }

        private void Refunded()
        {
            Receive<Commands.InitializePayment>(cmd =>
            {
                Sender.Tell(
                    new Responses.InitializePaymentResponse(0, "This payment has already been initialized"));
            });
            
            Receive<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has been refunded"));
            });
            
            Receive<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment has already been refunded"));
            });
        }
    }
}