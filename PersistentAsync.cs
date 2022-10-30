using System.Reflection;
using System.Runtime.CompilerServices;

public static class Program
{
    public static async Task Main()
    {
        var handler = new Handler();

        var context = new SynchronizationContextWrapper();
        SynchronizationContext.SetSynchronizationContext(context);
        await handler.ExecuteAsync(95274356, 3452345, 42);
    }
}

public class SynchronizationContextWrapper : SynchronizationContext
{
    private const string StateMachinePropertyName = "StateMachine";
    private bool _isStateRetrieved;
    
    public string? StateKey;

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (!_isStateRetrieved)
        {
            var machine = state!.GetType()
                .GetField(StateMachinePropertyName)!
                .GetValue(state)!;

            var field = machine.GetType()
                .GetField("<>1__state", BindingFlags.Instance | BindingFlags.Public)!;
            field.SetValue(machine, 2);

            Console.WriteLine($"Retrieving state by key '{StateKey}'");

            _isStateRetrieved = true;
        }

        base.Post(callback, state);
    }
}

public class Handler
{
    public async Task ExecuteAsync(long chatId, long userId, int x)
    {
        await Async.RetrieveState($"{chatId}-{userId}");

        Console.WriteLine($"1: {x}");
        await Task.Yield();
        Console.WriteLine($"2: {x}");
        await Task.Yield();
        Console.WriteLine($"3: {x}");
        await Task.Yield();
        Console.WriteLine($"4: {x}");
        await Task.Yield();
        Console.WriteLine($"5: {x}");
    }
}

public class Async
{
    public static YieldAwaitable RetrieveState(string stateKey)
    {
        var context = (SynchronizationContextWrapper)SynchronizationContext.Current!;
        context.StateKey = stateKey;
        
        return Task.Yield();
    }
}
