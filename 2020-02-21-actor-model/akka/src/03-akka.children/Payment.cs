using Akka.Actor;

namespace _03_akka.children
{
    public class Payment : ReceiveActor
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
        
        public static class Responses
        {
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

        private readonly decimal _amount;

        public Payment(decimal amount)
        {
            _amount = amount;
            
            Become(Initialized);
        }
        
        private void Initialized()
        {
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
            Receive<Commands.ChargePayment>(cmd =>
            {
                Sender.Tell(new Responses.ChargePaymentResponse(0, "This payment has been refunded"));
            });
            
            Receive<Commands.RefundPayment>(cmd =>
            {
                Sender.Tell(new Responses.RefundPaymentResponse(0, "This payment has already been refunded"));
            });
        }

        public static Props Initialize(decimal amount)
        {
            return Props.Create(() => new Payment(amount));
        }
    }
}