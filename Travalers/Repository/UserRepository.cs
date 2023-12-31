﻿using System.Threading.Tasks;
using Travalers.Entities;
using Travalers.Data; // Import the MongoDBContext namespace
using MongoDB.Driver;

namespace Travalers.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _usersCollection;

        public UserRepository(IMongoDBContext dbContext)
        {
            _usersCollection = dbContext.Users;
        }

        public async Task<User> GetUserByNICAsync(string nIC)
        {
            var filter = Builders<User>.Filter.Eq(u => u.NIC, nIC);
            return await _usersCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task CreateUserAsync(User user)
        {
            await _usersCollection.InsertOneAsync(user);
        }

        public async Task<User> GetUserById(string nic)
        {
            var filter = Builders<User>.Filter.Eq(u => u.NIC, nic);
            return await _usersCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetAll()
        {
            return await _usersCollection.Find(_ => true).ToListAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(t => t.NIC, user.NIC);
            var options = new ReplaceOptions { IsUpsert = false };
            await _usersCollection.ReplaceOneAsync(filter, user, options);
        }
    }
}
