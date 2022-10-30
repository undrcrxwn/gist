using System.Reflection;
using System.Runtime.CompilerServices;

namespace Playground;

public static class Program
{
    public static async Task Main()
    {
        var handler = new Handler();
        await handler.ExecuteAsync(42);
    }
}

public class Handler
{
    public async Task ExecuteAsync(int x)
    {
        Console.WriteLine(x);
        
        await IncrementX();
        Console.WriteLine(x);
        
        await IncrementX();
        Console.WriteLine(x);
        
        await IncrementX();
        Console.WriteLine(x);
        
        await IncrementX();
        Console.WriteLine(x);
    }

    private static IAwaitable IncrementState() => Async.CreateAsyncStateMachineHandlingAwaitable(machine =>
    {
        var field = machine.GetType().GetField("<>1__state", BindingFlags.Instance | BindingFlags.Public)!;
        var value = (int)field.GetValue(machine)!;
        field.SetValue(machine, value + 3);
    });

    private static IAwaitable IncrementX() => Async.CreateAsyncStateMachineHandlingAwaitable(machine =>
    {
        var field = machine.GetType().GetField("x", BindingFlags.Instance | BindingFlags.Public)!;
        var value = (int)field.GetValue(machine)!;
        field.SetValue(machine, value + 1);
    });

    private static IAwaitable PrintMachineType() => Async.CreateAsyncStateMachineHandlingAwaitable(machine =>
        Console.WriteLine(machine.GetType().Name));
}

public class Async
{
    public static IAwaitable CreateAsyncStateMachineHandlingAwaitable(Action<IAsyncStateMachine> stateMachineHandler)
    {
        var awaiter = new YieldingAwaiter(continuation =>
        {
            var machine = GetStateMachine(continuation);
            stateMachineHandler(machine);
        });

        return new Awaitable(awaiter);
    }

    private static IAsyncStateMachine GetStateMachine(Action continuation)
    {
        var target = continuation.Target!;
        var field = target.GetType().GetField("StateMachine", BindingFlags.Public | BindingFlags.Instance)!;
        return (IAsyncStateMachine)field.GetValue(target)!;
    }
}

public struct YieldingAwaiter : IAwaiter
{
    private readonly Action<Action> _continuationHandler;
    private Action? _continuation;

    public YieldingAwaiter(Action<Action> continuationHandler) =>
        _continuationHandler = continuationHandler;

    public bool IsCompleted => _continuation is not null;

    public void GetResult() => _continuationHandler(_continuation!);

    public void OnCompleted(Action continuation)
    {
        _continuation = continuation;
        Task.Run(continuation);
    }
}

public interface IAwaitable
{
    IAwaiter GetAwaiter();
}

public interface IAwaiter : INotifyCompletion
{
    bool IsCompleted { get; }
    void GetResult();
}

public class Awaitable : IAwaitable
{
    private readonly IAwaiter _awaiter;

    internal Awaitable(IAwaiter awaiter) =>
        _awaiter = awaiter;

    public IAwaiter GetAwaiter() => _awaiter;
}
