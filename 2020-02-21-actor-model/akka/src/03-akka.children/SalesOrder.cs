using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;

namespace _03_akka.children
{
    public class SalesOrder : ReceiveActor
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
                public AddPaymentResponse(string paymentId, string errorMessage = null)
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
        
        public SalesOrder()
        {
            Become(New);
        }

        private void New()
        {
            Receive<Commands.PlaceOrder>(cmd =>
            {
                _productName = cmd.ProductName;
                _productPrice = cmd.ProductPrice;
                
                Become(Placed);
                
                Sender.Tell(new Responses.PlaceOrderResponse());
            });

            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "New"));
            });
            
            Receive<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You can't add a payment to a order that hasn't been placed yet"));
            });
            
            Receive<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order hasn't been completed"));
            });
            
            Receive<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order hasn't been completed yet"));
            });
        }

        private void Placed()
        {
            Receive<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
            });
            
            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Placed"));
            });
            
            Receive<Commands.AddPayment>(cmd =>
            {
                var amountLeftToPay = CalculateAmountLeftToPay();

                if (cmd.Amount > amountLeftToPay)
                {
                    Sender.Tell(new Responses.AddPaymentResponse("", $"The amount {cmd.Amount:N2} is higher then what left to pay for this order ({amountLeftToPay})"));
                    
                    return;
                }
                
                var paymentId = (_payments.Count + 1).ToString();

                _payments[paymentId] = new PaymentInformation(paymentId, cmd.Amount);
                
                if (IsComplete())
                    Become(Complete);
                
                Sender.Tell(new Responses.AddPaymentResponse(paymentId));
            });
            
            Receive<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order hasn't been completed"));
            });
            
            Receive<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order hasn't been completed yet"));
            });
        }

        private void Complete()
        {
            Receive<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
            });
            
            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Complete"));
            });
            
            Receive<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You have already added enough payments to this order"));
            });
            
            ReceiveAsync<Commands.ConfirmOrder>(async cmd =>
            {
                var paymentsToCharge = _payments
                    .Where(x => x.Value.Status == PaymentInformation.PaymentStatus.Initialized)
                    .ToList();
                
                var errors = new List<string>();

                foreach (var payment in paymentsToCharge)
                {
                    var response = await payment.Value.Charge();
                    
                    if (!response.Success)
                        errors.Add(response.ErrorMessage);
                }
                
                if (IsFullyCharged())
                    Become(Confirmed);
                
                Sender.Tell(new Responses.ConfirmOrderResponse(errors.ToArray()));
            });
            
            ReceiveAsync<Commands.CancelOrder>(async cmd =>
            {
                var errors = await RefundAllPayments();
                
                if (IsFullyRefunded())
                    Become(Cancelled);
                
                Sender.Tell(new Responses.CancelOrderResponse(errors.ToArray()));
            });
        }
        
        private void Confirmed()
        {
            Receive<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has already been placed"));
            });
            
            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Confirmed"));
            });
            
            Receive<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "You have already added enough payments to this order"));
            });
            
            Receive<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order has already been confirmed"));
            });
            
            ReceiveAsync<Commands.CancelOrder>(async cmd =>
            {
                var errors = await RefundAllPayments();
                
                if (IsFullyRefunded())
                    Become(Cancelled);
                
                Sender.Tell(new Responses.CancelOrderResponse(errors.ToArray()));
            });
        }

        private void Cancelled()
        {
            Receive<Commands.PlaceOrder>(cmd =>
            {
                Sender.Tell(new Responses.PlaceOrderResponse("This order has been cancelled"));
            });
            
            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice, "Cancelled"));
            });
            
            Receive<Commands.AddPayment>(cmd =>
            {
                Sender.Tell(new Responses.AddPaymentResponse("", "This order has been cancelled"));
            });
            
            Receive<Commands.ConfirmOrder>(cmd =>
            {
                Sender.Tell(new Responses.ConfirmOrderResponse("This order has been cancelled"));
            });
            
            Receive<Commands.CancelOrder>(cmd =>
            {
                Sender.Tell(new Responses.CancelOrderResponse("This order has already been cancelled"));
            });
        }

        private async Task<IImmutableList<string>> RefundAllPayments()
        {
            var paymentsToRefund = _payments
                .Where(x => x.Value.Status != PaymentInformation.PaymentStatus.Refunded)
                .ToList();

            var errors = new List<string>();

            foreach (var payment in paymentsToRefund)
            {
                var response = await payment.Value.Refund();
                    
                if (!response.Success)
                    errors.Add(response.ErrorMessage);
            }

            return errors.ToImmutableList();
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
                _payment = Context.ActorOf(Payment.Initialize(amount), id);
                Status = PaymentStatus.Initialized;
            }

            public decimal Amount { get; }
            public PaymentStatus Status { get; private set;}
            
            public async Task<Payment.Responses.ChargePaymentResponse> Charge()
            {
                var response =
                    await _payment.Ask<Payment.Responses.ChargePaymentResponse>(new Payment.Commands.ChargePayment());

                if (response.Success)
                    Status = PaymentStatus.Charged;

                return response;
            }

            public async Task<Payment.Responses.RefundPaymentResponse> Refund()
            {
                var response =
                    await _payment.Ask<Payment.Responses.RefundPaymentResponse>(new Payment.Commands.RefundPayment());

                if (response.Success)
                    Status = PaymentStatus.Refunded;

                return response;
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