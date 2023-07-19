namespace _02_akka.behavior;

public partial class SalesOrder
{
    private void New()
    {
        Receive<Commands.PlaceOrder>(cmd =>
        {
            Become(() => Placed(cmd.ProductName, cmd.ProductPrice));
        });
    }
}