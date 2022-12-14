using Radzinsky.Application.Abstractions;
using Radzinsky.Application.Delegates;
using Radzinsky.Application.Models.Contexts;
using Telegram.Bot.Types.ReplyMarkups;

namespace Radzinsky.Application.Commands;

public class SurveyCommand : ICommand, ICallbackQueryHandler
{
    private record SurveyState(int? MatrixCellId, int? Rating);
    
    private readonly IStateService _states;

    public SurveyCommand(IStateService states) => _states = states;

    public async Task HandleCallbackQueryAsync(CallbackQueryContext context, CancellationToken token)
    {
        DeconstructCallbackData(context.Query.Data.Payload,
            out var respondentUserId, out var callbackKey, out _);
        
        if (respondentUserId != context.Update.InteractorUserId!.Value)
        {
            await context.ReplyAsync("This survey is not for you!");
            return;
        }

        CallbackQueryContextHandler callbackQueryHandler = callbackKey switch
        {
            "Cell" => HandleMatrixCellCallbackAsync,
            "Rating" => HandleRatingCallbackAsync,
            _ => throw new InvalidOperationException()
        };
        
        await callbackQueryHandler(context);
    }
    
    public async Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var respondentUserId = context.Update.InteractorUserId!.Value;
        var stateKey = GetSurveyStateKey(respondentUserId);
        
        if (_states.ReadState<SurveyState>(stateKey) is not null)
        {
            await context.ReplyAsync("You are already participating in this survey.");
            return;
        }
        
        var state = new SurveyState(null, null);
        _states.WriteState(stateKey, state);
            
        await context.ReplyAsync("Ok! Now answer some questions for the survey.");
        await AskForMatrixCellAsync(context, respondentUserId);
    }

    private async Task AskForMatrixCellAsync(CommandContext context, long respondentUserId)
    {
        var buttons = new List<List<InlineKeyboardButton>>
        {
            new()
            {
                InlineKeyboardButton.WithCallbackData("Cell A", "11"),
                InlineKeyboardButton.WithCallbackData("Cell B", "12")
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("Cell C", "21"),
                InlineKeyboardButton.WithCallbackData("Cell D", "22")
            }
        };

        buttons.ForEach(x => x.ForEach(button =>
            button.CallbackData = $"{respondentUserId} Cell {button.CallbackData}"));

        await context.ReplyAsync("1. Choose a random matrix cell.",
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    private async Task HandleMatrixCellCallbackAsync(CallbackQueryContext context)
    {
        DeconstructCallbackData(context.Query.Data.Payload,
            out var respondentUserId, out _, out var payload);

        var stateKey = GetSurveyStateKey(respondentUserId);
        var state = _states.ReadState<SurveyState>(stateKey)!;

        if (state.MatrixCellId is not null)
        {
            await context.ReplyAsync("You've already decided on your matrix cell!");
            return;
        }
        
        _states.WriteState(stateKey, state with { MatrixCellId = int.Parse(payload) });
        
        await AskForRatingAsync(context, respondentUserId);
    }

    private async Task AskForRatingAsync(CallbackQueryContext context, long respondentUserId)
    {
        var buttons = new List<List<InlineKeyboardButton>>
        {
            new()
            {
                InlineKeyboardButton.WithCallbackData("*", "1"),
                InlineKeyboardButton.WithCallbackData("**", "2"),
                InlineKeyboardButton.WithCallbackData("***", "3"),
                InlineKeyboardButton.WithCallbackData("****", "4"),
                InlineKeyboardButton.WithCallbackData("*****", "5")
            }
        };
        
        buttons.ForEach(x => x.ForEach(button =>
            button.CallbackData = $"{respondentUserId} Rating {button.CallbackData}"));

        await context.ReplyAsync("2. Rate us 1 to 5.", replyMarkup: new InlineKeyboardMarkup(buttons));
    }
    
    private async Task HandleRatingCallbackAsync(CallbackQueryContext context)
    {
        DeconstructCallbackData(context.Query.Data.Payload,
            out var respondentUserId, out _, out var payload);

        var stateKey = GetSurveyStateKey(respondentUserId);
        var state = _states.ReadState<SurveyState>(stateKey)!;

        if (state.Rating is not null)
        {
            await context.ReplyAsync("You've already decided on rating!");
            return;
        }
        
        _states.WriteState(stateKey, state with { Rating = int.Parse(payload) });

        await ShowResultsAsync(context, state);
    }
    
    private async Task ShowResultsAsync(CallbackQueryContext context, SurveyState state)
    {
        const string replyTemplate = "Thanks for your time! You've just chosen cell {0} and rated us for {1}.";
        await context.ReplyAsync(string.Format(replyTemplate, state.MatrixCellId, state.Rating));
    }

    private void DeconstructCallbackData(
        string data,
        out long respondentUserId,
        out string callbackKey,
        out string payload)
    {
        var words = data.Split();
        respondentUserId = long.Parse(words[0]);
        callbackKey = words[1];
        payload = string.Join(' ', words[2..]);
    }

    private string GetSurveyStateKey(long respondentUserId) =>
        $"__InlineSurveyTest__{respondentUserId}";
}
