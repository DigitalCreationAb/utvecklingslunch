using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;

namespace _05_akka.async
{
    public static class Program
    {
        private static readonly IDictionary<string, IActorRef> Orders = new Dictionary<string, IActorRef>();
        private static readonly ActorSystem System = ActorSystem.Create("children");
        
        public static async Task Main()
        {
            var commands = new Dictionary<string, Func<Task<NextStep>>>
            {
                ["exit"] = Exit,
                ["new"] = PlaceOrder,
                ["get"] = GetOrderData,
                ["add-payment"] = AddPaymentToOrder,
                ["confirm-order"] = ConfirmOrder,
                ["cancel-order"] = CancelOrder
            };
            
            while (true)
            {
                Console.WriteLine($"Please enter a command ({string.Join(", ", commands.Keys)}):");
                var command = Console.ReadLine() ?? "";
                
                if (!commands.ContainsKey(command))
                {
                    Console.WriteLine($"You have to enter a valid command ({string.Join(", ", commands.Keys)})");
                    continue;
                }

                var nextStep = await commands[command]();
                
                if (nextStep == NextStep.Exit)
                    break;
            }
            
            await CoordinatedShutdown
                .Get(System)
                .Run(CoordinatedShutdown.ClrExitReason.Instance);
        }

        private static Task<NextStep> Exit()
        {
            return Task.FromResult(NextStep.Exit);
        }

        private static Task<NextStep> PlaceOrder()
        {
            var orderId = (Orders.Count + 1).ToString();

            var order = System.ActorOf<SalesOrder>(orderId);

            Orders[orderId] = order;
            
            Console.WriteLine("Enter a product name:");
            var productName = Console.ReadLine() ?? "";
            
            Console.WriteLine("Enter a product price:");
            var productPrice = decimal.Parse(Console.ReadLine() ?? "");

            order.Tell(new SalesOrder.Commands.PlaceOrder(productName, productPrice));
            
            return Task.FromResult(NextStep.Continue);
        }

        private static async Task<NextStep> GetOrderData()
        {
            Console.WriteLine("What order do you want to see?");
            var orderId = Console.ReadLine() ?? "";

            if (!Orders.ContainsKey(orderId))
            {
                Console.WriteLine($"There is not order with id {orderId}");

                return NextStep.Continue;
            }

            var order = Orders[orderId];

            var response =
                await order.Ask<SalesOrder.Responses.OrderDataResponse>(new SalesOrder.Queries.GetOrderData());
            
            Console.WriteLine($"Order: {orderId}, Product name: {response.ProductName}, Product price: {response.ProductPrice:N2}, Status: {response.Status}");

            return NextStep.Continue;
        }
        
        private static Task<NextStep> AddPaymentToOrder()
        {
            Console.WriteLine("What order do you want to add a payment to?");
            var orderId = Console.ReadLine() ?? "";
            
            if (!Orders.ContainsKey(orderId))
            {
                Console.WriteLine($"There is not order with id {orderId}");

                return Task.FromResult(NextStep.Continue);
            }
            
            var order = Orders[orderId];
            
            Console.WriteLine("How much do you want to pay?");
            var amount = decimal.Parse(Console.ReadLine() ?? "");

            order.Tell(new SalesOrder.Commands.AddPayment(amount));
            
            return Task.FromResult(NextStep.Continue);
        }

        private static Task<NextStep> ConfirmOrder()
        {
            Console.WriteLine("What order do you want to confirm?");
            var orderId = Console.ReadLine() ?? "";
            
            if (!Orders.ContainsKey(orderId))
            {
                Console.WriteLine($"There is not order with id {orderId}");

                return Task.FromResult(NextStep.Continue);
            }
            
            var order = Orders[orderId];

            order.Tell(new SalesOrder.Commands.ConfirmOrder());
            
            return Task.FromResult(NextStep.Continue);
        }

        private static Task<NextStep> CancelOrder()
        {
            Console.WriteLine("What order do you want to cancel?");
            var orderId = Console.ReadLine() ?? "";
            
            if (!Orders.ContainsKey(orderId))
            {
                Console.WriteLine($"There is not order with id {orderId}");

                return Task.FromResult(NextStep.Continue);
            }
            
            var order = Orders[orderId];

            order.Tell(new SalesOrder.Commands.CancelOrder());

            return Task.FromResult(NextStep.Continue);
        }
        
        private enum NextStep
        {
            Continue,
            Exit
        }
    }
}
