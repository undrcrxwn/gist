using Radzinsky.Application.Abstractions;
using Radzinsky.Application.Delegates;
using Radzinsky.Application.Models.Contexts;
using Telegram.Bot.Types.ReplyMarkups;

namespace Radzinsky.Application.Commands;

public class SurveyCommand : ICommand, ICallbackQueryHandler
{
    private record SurveyState(long RespondentUserId, int? MatrixCellId, int? Rating);
    
    private readonly IStateService _states;

    public SurveyCommand(IStateService states) => _states = states;

    public async Task HandleCallbackQueryAsync(CallbackQueryContext context, CancellationToken token)
    {
        CallbackQueryContextHandler callbackQueryHandler = context.Query.Data.Payload.Split().First() switch
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
        
        var state = new SurveyState(respondentUserId, null, null);
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
        var matrixCellId = int.Parse(context.Query.Data.Payload.Split().Last());
        var stateKey = GetSurveyStateKey(context.Update.InteractorUserId!.Value);
        _states.WriteState<SurveyState>(stateKey, state => state! with { MatrixCellId = matrixCellId });

        await AskForRatingAsync(context);
    }

    private async Task AskForRatingAsync(CallbackQueryContext context)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("*", "state1 Rating 1"),
                InlineKeyboardButton.WithCallbackData("**", "state1 Rating 2"),
                InlineKeyboardButton.WithCallbackData("***", "state1 Rating 3"),
                InlineKeyboardButton.WithCallbackData("****", "state1 Rating 4"),
                InlineKeyboardButton.WithCallbackData("*****", "state1 Rating 5")
            }
        };

        await context.ReplyAsync("2. Rate us 1 to 5.", replyMarkup: new InlineKeyboardMarkup(buttons));
    }
    
    private async Task HandleRatingCallbackAsync(CallbackQueryContext context)
    {
        var stateKey = GetSurveyStateKey(context.Update.InteractorUserId!.Value);
        var rating = int.Parse(context.Query.Data.Payload.Split().Last());
        _states.WriteState<SurveyState>(stateKey, state => state! with { Rating = rating });

        await ShowResultsAsync(context);
    }
    
    private async Task ShowResultsAsync(CallbackQueryContext context)
    {
        var stateKey = GetSurveyStateKey(context.Update.InteractorUserId!.Value);
        var state = _states.ReadState<SurveyState>(stateKey)!;
        
        const string replyTemplate = "Thanks for your time! You've just chosen cell {0} and rated us for {1}.";
        await context.ReplyAsync(string.Format(replyTemplate, state.MatrixCellId, state.Rating));
    }

    private void DeconstructCallbackData(string data, out long respondentUserId, out string callbackKey, out string payload)
    {
        var words = data.Split();
        respondentUserId = long.Parse(words[0]);
        callbackKey = words[1];
        payload = string.Join(' ', words[2..]);
    }

    private string GetSurveyStateKey(long respondentUserId) =>
        $"__InlineSurveyTest__{respondentUserId}";
}
