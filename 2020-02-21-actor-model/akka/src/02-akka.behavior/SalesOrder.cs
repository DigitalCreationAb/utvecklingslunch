using Akka.Actor;

namespace _02_akka.behavior;

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
        Receive<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(_productName, _productPrice));
        });
    }
}