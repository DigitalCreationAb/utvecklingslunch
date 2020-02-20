using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;

namespace _01_akka.basic
{
    public static class Program
    {
        private static readonly IDictionary<string, IActorRef> Orders = new Dictionary<string, IActorRef>();
        private static readonly ActorSystem System = ActorSystem.Create("basic");
        
        public static async Task Main()
        {
            var commands = new Dictionary<string, Func<Task<NextStep>>>
            {
                ["exit"] = Exit,
                ["new"] = PlaceOrder,
                ["get"] = GetOrderData
            };
            
            while (true)
            {
                Console.WriteLine("Please enter a command (new, get, exit):");
                var command = Console.ReadLine() ?? "";
                
                if (!commands.ContainsKey(command))
                {
                    Console.WriteLine("You have to enter a valid command (new, get, exit)");
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
            
            Console.WriteLine($"Order with id {orderId} was placed.");
            
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
            
            Console.WriteLine($"Order: {orderId}, Product name: {response.ProductName}, Product price: {response.ProductPrice:N2}");

            return NextStep.Continue;
        }
        
        private enum NextStep
        {
            Continue,
            Exit
        }
    }
}
