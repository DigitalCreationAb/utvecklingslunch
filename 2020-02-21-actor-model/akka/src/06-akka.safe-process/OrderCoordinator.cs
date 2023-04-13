using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;

namespace _06_akka.safe_process;

public class OrderCoordinator : ReceivePersistentActor
{
    public static class Commands
    {
        public record PlaceOrder(string ProductName, decimal ProductPrice);

        public record CancelOrder(string OrderId);

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

        public record CancellationStarted(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} cancellation started";
            }
        }

        public record OrderFinished(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} finished";
            }
        }

        public record OrderCancelled(string OrderId)
        {
            public override string ToString()
            {
                return $"Order {OrderId} cancelled";
            }
        }
    }

    public static class Queries
    {
        public record QueryOrder(string OrderId, object Query);
    }

    public override string PersistenceId => "ordercoordinator";

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

        Command<SalesOrder.Responses.OrderProcessDoneResponse>(_ => { });

        Command<SalesOrder.Responses.CancellationProcessDoneResponse>(_ => { });
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

            order.Tell(new SalesOrder.Commands.ContinueCancellationProcess());
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