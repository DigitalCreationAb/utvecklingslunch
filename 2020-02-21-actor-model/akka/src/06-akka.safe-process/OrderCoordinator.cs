using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;

namespace _06_akka.safe_process
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
            
            public class CancelOrder
            {
                public CancelOrder(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
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
            
            public class CancellationStarted
            {
                public CancellationStarted(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
                
                public override string ToString()
                {
                    return $"Order {OrderId} cancellation started";
                }
            }
            
            public class OrderFinished
            {
                public OrderFinished(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }
                
                public override string ToString()
                {
                    return $"Order {OrderId} finished";
                }
            }
            
            public class OrderCancelled
            {
                public OrderCancelled(string orderId)
                {
                    OrderId = orderId;
                }

                public string OrderId { get; }

                public override string ToString()
                {
                    return $"Order {OrderId} cancelled";
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
        private readonly IList<string> _currentOrderProcesses = new List<string>();
        private readonly IList<string> _currentCancellationProcesses = new List<string>();
        
        private static IActorRef _coordinatorActor;
        
        public OrderCoordinator()
        {
            Recover<Events.OrderReserved>(On);
            Recover<Events.OrderFinished>(On);
            Recover<Events.CancellationStarted>(On);
            Recover<Events.OrderCancelled>(On);
            
            Command<Commands.PlaceOrder>(cmd =>
            {
                var orderId = (_placedOrders + 1).ToString();

                var order = GetOrder(orderId);
                
                Persist(new Events.OrderReserved(orderId), evnt =>
                {
                    On(evnt);
                    
                    order.Tell(new SalesOrder.Commands.StartOrderProcess(cmd.ProductName, cmd.ProductPrice));
                });
            });

            Command<Commands.CancelOrder>(cmd =>
            {
                var order = GetOrder(cmd.OrderId);

                order.Tell(new SalesOrder.Commands.StartCancellationProcess());
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

            Command<SalesOrder.Responses.OrderProcessDoneResponse>(response => { });

            Command<SalesOrder.Responses.CancellationProcessDoneResponse>(response => { });
        }

        protected override void PreStart()
        {
            foreach (var orderProcess in _currentOrderProcesses)
            {
                var order = GetOrder(orderProcess);
                
                order.Tell(new SalesOrder.Commands.ContinueOrderProcess());
            }

            foreach (var cancellationProcess in _currentCancellationProcesses)
            {
                var order = GetOrder(cancellationProcess);
                
                order.Tell(new SalesOrder.Commands.ContinueCalcellationProcess());
            }
        }

        public static void Initialize(ActorSystem system)
        {
            _coordinatorActor = system.ActorOf<OrderCoordinator>();
        }

        public static void PlaceOrder(string productName, decimal productPrice)
        {
            _coordinatorActor.Tell(new Commands.PlaceOrder(productName, productPrice));
        }

        public static void CancelOrder(string orderId)
        {
            _coordinatorActor.Tell(new Commands.CancelOrder(orderId));
        }

        public static Task<SalesOrder.Responses.OrderDataResponse> GetOrderData(string orderId)
        {
            return _coordinatorActor.Ask<SalesOrder.Responses.OrderDataResponse>(
                new Queries.QueryOrder(orderId, new SalesOrder.Queries.GetOrderData()));
        }

        public static void AddPaymentToOrder(string orderId, decimal amount)
        {
            _coordinatorActor.Tell(new Commands.SendCommandToOrder(
                orderId, 
                new SalesOrder.Commands.AddPayment(amount)));
        }

        public static void FinishOrder(string orderId)
        {
            _coordinatorActor.Tell(new Commands.SendCommandToOrder(
                orderId, 
                new SalesOrder.Commands.FinishOrder()));
        }

        private void On(Events.OrderReserved evnt)
        {
            _placedOrders++;
            
            if (!_currentOrderProcesses.Contains(evnt.OrderId))
                _currentOrderProcesses.Add(evnt.OrderId);
        }

        private void On(Events.OrderFinished evnt)
        {
            if (_currentOrderProcesses.Contains(evnt.OrderId))
                _currentOrderProcesses.Remove(evnt.OrderId);
        }

        private void On(Events.CancellationStarted evnt)
        {
            if (!_currentCancellationProcesses.Contains(evnt.OrderId))
                _currentCancellationProcesses.Add(evnt.OrderId);
            
            if (_currentOrderProcesses.Contains(evnt.OrderId))
                _currentOrderProcesses.Remove(evnt.OrderId);
        }

        private void On(Events.OrderCancelled evnt)
        {
            if (_currentCancellationProcesses.Contains(evnt.OrderId))
                _currentCancellationProcesses.Remove(evnt.OrderId);
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