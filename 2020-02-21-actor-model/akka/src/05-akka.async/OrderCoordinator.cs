using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;

namespace _05_akka.async
{
    public class OrderCoordinator : ReceivePersistentActor
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
            
            public class SendCommandToOrder
            {
                public SendCommandToOrder(string orderId, object command)
                {
                    OrderId = orderId;
                    Command = command;
                }

                public string OrderId { get; }
                public object Command { get; }
            }
        }
        
        public static class Events
        {
            public class OrderReserved
            {
                public OrderReserved(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} was reserved";
                }
            }
        }
        
        public static class Queries
        {
            public class QueryOrder
            {
                public QueryOrder(string orderId, object query)
                {
                    OrderId = orderId;
                    Query = query;
                }

                public string OrderId { get; }
                public object Query { get; }
            }
        }
        
        public override string PersistenceId { get; } = "ordercoordinator";

        private int _placedOrders;

        private static IActorRef _coordinatorActor;
        
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

        public static void PlaceOrder(string productName, decimal productPrice)
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
}