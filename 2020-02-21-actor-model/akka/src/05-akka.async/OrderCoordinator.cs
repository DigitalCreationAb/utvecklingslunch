using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async;

public class OrderCoordinator : ReceivePersistentActor
{
    public static class Commands
    {
        public record PlaceOrder(string ProductName, decimal ProductPrice);
        
        public record SendCommandToOrder(string OrderId, object Command);
    }
        
    public static class Events
    {
        public record OrderReserved(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} was reserved";
            }
        }
    }
        
    public static class Queries
    {
        public record QueryOrder(string OrderId, object Query);
    }
        
    public override string PersistenceId => "ordercoordinator";

    private int _placedOrders;

    private static IActorRef? _coordinatorActor;
        
    public OrderCoordinator()
    {
        Recover<Events.OrderReserved>(On);
            
        Command<Commands.PlaceOrder>(cmd =>
        {
            var orderId = (_placedOrders + 1).ToString();

            var order = GetOrder(orderId);
                
            Persist(new Events.OrderReserved(orderId), evnt =>
            {
                On(evnt);
                    
                order.Tell(new SalesOrder.Commands.PlaceOrder(cmd.ProductName, cmd.ProductPrice));
            });
        });
            
        Command<Commands.SendCommandToOrder>(cmd =>
        {
            var order = GetOrder(cmd.OrderId);
                
            order.Tell(cmd.Command);
        });
            
        Command<Queries.QueryOrder>(query =>
        {
            var order = GetOrder(query.OrderId);
                
            order.Tell(query.Query, Sender);
        });
    }

    public static void Initialize(ActorSystem system)
    {
        _coordinatorActor = system.ActorOf<OrderCoordinator>();
    }

    public static void PlaceNewOrder(string productName, decimal productPrice)
    {
        _coordinatorActor.Tell(new Commands.PlaceOrder(productName, productPrice));
    }

    public static void SendCommandToOrder(string orderId, object command)
    {
        _coordinatorActor.Tell(new Commands.SendCommandToOrder(orderId, command));
    }

    public static Task<TResponse> QueryOrder<TResponse>(string orderId, object query)
    {
        return _coordinatorActor.Ask<TResponse>(new Queries.QueryOrder(orderId, query));
    }

    private void On(Events.OrderReserved evnt)
    {
        _placedOrders++;
    }

    private static IActorRef GetOrder(string orderId)
    {
        var order = Context.Child(orderId);

        if (Equals(order, ActorRefs.Nobody))
            order = Context.ActorOf(SalesOrder.Initialize(), orderId);

        return order;
    }
}