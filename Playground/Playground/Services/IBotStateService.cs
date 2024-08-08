using Microsoft.Bot.Builder;
using System.Threading.Tasks;
using System.Threading;
using Playground.Dialogs;

namespace Playground.Services
{
    public interface IBotStateService
    {
        IStatePropertyAccessor<UserDetails> UserDetailsAccessor { get; }
        Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
    }
    public class BotStateService : IBotStateService
    {
        private readonly UserState _userState;
        public IStatePropertyAccessor<UserDetails> UserDetailsAccessor { get; }
        public BotStateService(UserState userState)
        {
            _userState = userState;
            UserDetailsAccessor = userState.CreateProperty<UserDetails>("UserDetails");
        }
        public async Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            await _userState.SaveChangesAsync(turnContext, force, cancellationToken);
        }
    }

    public class UserDetails
    {
        public string RiderId { get; set; }
        public bool IsLinkedAccount { get; set; }
        public string UnfinishOrder { get; set; }
        public string RequestOrder { get; set; }
        public switchTo SwitchState { get; set; }
    }
}
