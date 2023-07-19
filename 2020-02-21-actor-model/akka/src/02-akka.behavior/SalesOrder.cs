using Akka.Actor;

namespace _02_akka.behavior;

public partial class SalesOrder : ReceiveActor
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
        public record OrderDataResponse(string ProductName, decimal ProductPrice);
    }

    public SalesOrder()
    {
        Become(New);
    }
}