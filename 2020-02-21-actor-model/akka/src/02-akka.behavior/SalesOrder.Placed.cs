using Akka.Actor;

namespace _02_akka.behavior;

public partial class SalesOrder
{
    private void Placed(string productName, decimal productPrice)
    {
        Receive<Queries.GetOrderData>(_ =>
        {
            Sender.Tell(new Responses.OrderDataResponse(productName, productPrice));
        });
    }   
}