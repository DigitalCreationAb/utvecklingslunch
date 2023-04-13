using Akka.Actor;

namespace _01_akka.basic;

public class SalesOrder : ReceiveActor
{
    public static class Commands
    {
        public record PlaceOrder(string ProductName, decimal ProductPrice);
    }

    public static class Queries
    {
        public record GetOrderData;
    }

    public static class Responses
    {
        public record OrderDataResponse(string? ProductName, decimal ProductPrice);
    }

    private string? _productName;
    private decimal _productPrice;

    public SalesOrder()
    {
        Receive<Commands.PlaceOrder>(cmd =>
        {
            _productName = cmd.ProductName;
            _productPrice = cmd.ProductPrice;
        });

        Receive<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice));
        });
    }
}