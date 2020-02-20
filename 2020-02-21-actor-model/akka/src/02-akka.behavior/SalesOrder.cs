using Akka.Actor;

namespace _02_akka.behavior
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
                public OrderDataResponse(string productName, decimal productPrice)
                {
                    ProductName = productName;
                    ProductPrice = productPrice;
                }

                public string ProductName { get; }
                public decimal ProductPrice { get; }
            }
        }

        private string _productName;
        private decimal _productPrice;
        
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
            });
        }

        private void Placed()
        {
            Receive<Queries.GetOrderData>(query =>
            {
                Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice));
            });
        }
    }
}