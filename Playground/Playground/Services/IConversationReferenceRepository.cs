using Microsoft.Bot.Schema;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Playground.Services
{
    public interface IConversationReferenceRepository
    {
        Task AddOrUpdateConversationReferenceAsync(string key, ConversationReference reference);
        Task<ConversationReference> GetConversationReferenceAsync(string key);
    }
    public class ConversationReferenceRepository : IConversationReferenceRepository
    {
        private readonly IMongoCollection<ConversationReference> _collection;
        public ConversationReferenceRepository(DbConfig dbConfig)
        {
            var client = new MongoClient(dbConfig.ConnectionString);
            var database = client.GetDatabase(dbConfig.DatabaseName);
            _collection = database.GetCollection<ConversationReference>($"Rider{nameof(ConversationReference)}");
        }
        public async Task AddOrUpdateConversationReferenceAsync(string key, ConversationReference reference)
        {
            await _collection.ReplaceOneAsync(
                Builders<ConversationReference>.Filter.Eq(it => it.User.Id, key),
                reference,
                new ReplaceOptions { IsUpsert = true });
        }

        public async Task<ConversationReference> GetConversationReferenceAsync(string key)
        {
            var filter = Builders<ConversationReference>.Filter.Eq(c => c.User.Id, key);
            var projection = Builders<ConversationReference>.Projection.Exclude("_id"); 
            return await _collection.Find(filter)
                .Project<ConversationReference>(projection)
                .FirstOrDefaultAsync();
        }
    }
}
