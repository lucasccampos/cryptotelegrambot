using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

public class MongoCRUD
    {
        public IMongoDatabase db { get; private set; }

        public MongoCRUD(string databaseName, string IP)
        {
            var client = new MongoClient(IP);
            db = client.GetDatabase(databaseName);
        }

        public void InsertRecord<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertOne(record);
        }

        public async Task InsertRecordAsync<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            await collection.InsertOneAsync(record);
        }

        public List<T> LoadRecords<T>(string table)
        {
            var collection = db.GetCollection<T>(table);

            return collection.Find(new BsonDocument()).ToList();
        }

        public List<T> LoadRecordsWithKey<T, TValue>(string table, string key, TValue value)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq(key, value);

            var results = collection.Find(filter);

            return results.ToList();
        }

        public async Task<List<T>> LoadRecordsAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);

            return (await collection.FindAsync(new BsonDocument())).ToList();
        }

        public T LoadRecordById<T, TKey>(string table, TKey id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("_id", id);

            var results = collection.Find(filter);

            return results.CountDocuments() > 0 ? results.First() : default(T);
        }

        public async Task<T> LoadRecordByIdAsync<T, TKey>(string table, TKey id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("_id", id);

            return (await collection.FindAsync(filter)).First();
        }

        public void UpsertRecord<T>(string table, long id, T record)
        {
            var collection = db.GetCollection<T>(table);

            var result = collection.ReplaceOne(
                new BsonDocument("_id", id),
                record,
                new ReplaceOptions { IsUpsert = true }
            );
        }

        public void UpsertRecord<T>(string table, string id, T record)
        {
            var collection = db.GetCollection<T>(table);

            var result = collection.ReplaceOne(
                new BsonDocument("_id", id),
                record,
                new ReplaceOptions { IsUpsert = true }
            );
        }

        public async Task UpsertRecordAsync<T>(string table, long id, T record)
        {
            var collection = db.GetCollection<T>(table);

            await collection.ReplaceOneAsync(
                new BsonDocument("_id", id),
                record,
                new ReplaceOptions { IsUpsert = true }
            );
        }

        public void DeleteRecord<T, TKey>(string table, TKey id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("_id", id);

            var result = collection.DeleteOne(filter);
        }

        public async Task DeleteRecordAsync<T, TKey>(string table, TKey id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("_id", id);

            await collection.DeleteOneAsync(filter);
        }
    }