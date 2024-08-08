using Microsoft.Bot.Builder;
using Playground.Models;
using System.Threading;
using System.Threading.Tasks;

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
}
