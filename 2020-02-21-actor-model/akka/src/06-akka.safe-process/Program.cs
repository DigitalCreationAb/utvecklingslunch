using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.EventStore.Query;
using Akka.Persistence.Query;
using Akka.Streams;

namespace _06_akka.safe_process
{
    public static class Program
    {
        public static async Task Main()
        {
            var commands = new Dictionary<string, Func<Task<NextStep>>>
            {
                ["exit"] = Exit,
                ["new"] = PlaceOrder,
                ["get"] = GetOrderData,
                ["add-payment"] = AddPaymentToOrder,
                ["finish"] = FinishOrder,
                ["cancel"] = CancelOrder
            };

            var system = ActorSystem.Create("async", File.ReadAllText("./akka.config"));

            OrderCoordinator.Initialize(system);
            
            var queries = PersistenceQuery.Get(system).ReadJournalFor<EventStoreReadJournal>(EventStoreReadJournal.Identifier);
            var materializer = ActorMaterializer.Create(system);
            
            var src = queries.PersistenceIds();

            src.RunForeach(persistenceId =>
            {
                var idSource = queries.EventsByPersistenceId(persistenceId, 0, long.MaxValue);

                idSource.RunForeach(evnt =>
                {
                    Console.WriteLine(evnt.Event.ToString());
                }, materializer);
            }, materializer);
            
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
                .Get(system)
                .Run(CoordinatedShutdown.ClrExitReason.Instance);
        }

        private static Task<NextStep> Exit()
        {
            return Task.FromResult(NextStep.Exit);
        }

        private static Task<NextStep> PlaceOrder()
        {
            Console.WriteLine("Enter a product name:");
            var productName = Console.ReadLine() ?? "";
            
            Console.WriteLine("Enter a product price:");
            var productPrice = decimal.Parse(Console.ReadLine() ?? "");
            
            OrderCoordinator.PlaceOrder(productName, productPrice);

            return Task.FromResult(NextStep.Continue);
        }

        private static async Task<NextStep> GetOrderData()
        {
            Console.WriteLine("What order do you want to see?");
            var orderId = Console.ReadLine() ?? "";

            var response = await OrderCoordinator.GetOrderData(orderId);
            
            Console.WriteLine($"Order: {orderId}, Product name: {response.ProductName}, Product price: {response.ProductPrice:N2}, Status: {response.Status}");

            return NextStep.Continue;
        }
        
        private static Task<NextStep> AddPaymentToOrder()
        {
            Console.WriteLine("What order do you want to add a payment to?");
            var orderId = Console.ReadLine() ?? "";

            Console.WriteLine("How much do you want to pay?");
            var amount = decimal.Parse(Console.ReadLine() ?? "");
            
            OrderCoordinator.AddPaymentToOrder(orderId, amount);
            
            return Task.FromResult(NextStep.Continue);
        }

        private static Task<NextStep> FinishOrder()
        {
            Console.WriteLine("What order do you want to finish?");
            var orderId = Console.ReadLine() ?? "";
            
            OrderCoordinator.FinishOrder(orderId);

            return Task.FromResult(NextStep.Continue);
        }

        private static Task<NextStep> CancelOrder()
        {
            Console.WriteLine("What order do you want to cancel?");
            var orderId = Console.ReadLine() ?? "";

            OrderCoordinator.CancelOrder(orderId);

            return Task.FromResult(NextStep.Continue);
        }
        
        private enum NextStep
        {
            Continue,
            Exit
        }
    }
}
