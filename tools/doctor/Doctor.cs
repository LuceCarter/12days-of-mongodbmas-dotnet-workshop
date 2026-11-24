using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using SantasWorkshop.Shared;

namespace SantasWorkshop.Doctor;

/// <summary>
/// Checks that the environment is ready, and says exactly what to do when it isn't.
/// </summary>
/// <remarks>
/// Setup trouble is the single most likely reason somebody gives up on this
/// challenge, so every failure here must print a fix, not just a complaint.
/// </remarks>
public static class Doctor
{
    public static async Task<int> RunAsync(string[] args)
    {
        var deep = args.Contains("--deep", StringComparer.OrdinalIgnoreCase);

        Console.WriteLine();
        Console.WriteLine("  🎄 Santa's Workshop — environment check");
        Console.WriteLine("  ─────────────────────────────────────────");
        Console.WriteLine();

        // Each result is printed as soon as it is known, so a slow check shows its
        // progress message in the right place instead of after everything else.
        var results = new List<CheckResult>();
        void Report(CheckResult r) { results.Add(r); r.Print(); }

        var (client, connection) = await CheckConnectionAsync();
        Report(connection);

        if (client is not null)
        {
            Report(await CheckReplicaSetAsync(client));
            Report(await CheckSearchAvailableAsync(client));
            Report(deep
                ? await CheckSearchQueryableAsync(client)
                : CheckResult.Skipped(
                    "Search and Vector Search queries",
                    "Run 'dotnet run --project tools/doctor -- --deep' to actually build and "
                  + "query an index. Worth doing once before Part 3A."));
            Report(await CheckSeedDataAsync(client));
        }

        Report(CheckApiKey("VOYAGE_API_KEY", "Voyage AI (embeddings)", "Parts 3B and 4",
            "https://www.mongodb.com/docs/voyageai/"));
        Report(CheckApiKey("GEMINI_API_KEY", "Google Gemini (the LLM)", "Part 4",
            "https://aistudio.google.com/apikey"));

        Console.WriteLine();
        var blocking = results.Count(r => r.Status == Status.Fail);
        if (blocking == 0)
        {
            Console.WriteLine("  ✅ You're ready. Open part1-inventory/README.md and have fun.");
            Console.WriteLine();
            return 0;
        }

        Console.WriteLine($"  ❌ {blocking} thing(s) need fixing before you start. See above.");
        Console.WriteLine();
        return 1;
    }

    private static async Task<(IMongoClient?, CheckResult)> CheckConnectionAsync()
    {
        const string name = "MongoDB connection";
        var where = MongoContext.UsingLocalFallback
            ? "local docker-compose deployment"
            : "the MONGODB_URI you set";

        try
        {
            var client = MongoContext.CreateClient();
            var sw = Stopwatch.StartNew();
            await client.GetDatabase("admin")
                        .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            sw.Stop();

            return (client, CheckResult.Pass(name, $"reached {where} in {sw.ElapsedMilliseconds} ms"));
        }
        catch (Exception ex)
        {
            return (null, CheckResult.Fail(name,
                $"could not reach {where}",
                Brief(ex),
                MongoContext.UsingLocalFallback
                    ? """
                      Start the local deployment from the repo root:
                          docker compose up -d
                      Then wait for it to report healthy:
                          docker compose ps
                      If Docker itself isn't running, start Docker Desktop first.
                      Prefer MongoDB Atlas? Set MONGODB_URI to your cluster's connection
                      string and make sure your IP is on the Atlas access list.
                      """
                    : """
                      Check the MONGODB_URI environment variable.
                      If this is Atlas: confirm the username and password are URL-encoded,
                      and that your current IP address is on the project's access list
                      (Atlas → Network Access → Add Current IP Address).
                      """));
        }
    }

    private static async Task<CheckResult> CheckReplicaSetAsync(IMongoClient client)
    {
        const string name = "Replica set";
        try
        {
            var hello = await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));

            if (hello.Contains("setName"))
                return CheckResult.Pass(name, $"'{hello["setName"]}' — change streams and Search will work");

            return CheckResult.Fail(name,
                "this server is standalone, not a replica set",
                "Search, Vector Search and change streams all need a replica set.",
                """
                The mongodb/mongodb-atlas-local image sets one up for you. If you
                started a plain 'mongo' container instead, stop it and use the
                repo's docker-compose.yml:
                    docker compose down
                    docker compose up -d
                """);
        }
        catch (Exception ex)
        {
            return CheckResult.Fail(name, "could not read the server state", Brief(ex),
                "Make sure the deployment finished starting: docker compose ps");
        }
    }

    private static async Task<CheckResult> CheckSearchAvailableAsync(IMongoClient client)
    {
        const string name = "Search engine (mongot)";
        try
        {
            // Listing search indexes only succeeds when the search process is wired up.
            var collection = MongoContext.GetToys(client);
            var cursor = await collection.SearchIndexes.ListAsync();
            await cursor.ToListAsync();

            return CheckResult.Pass(name, "available — $search and $vectorSearch should work");
        }
        catch (Exception ex)
        {
            return CheckResult.Fail(name,
                "the search process isn't answering",
                Brief(ex),
                """
                You're probably on a plain MongoDB server rather than an Atlas Local
                deployment. From the repo root:
                    docker compose down
                    docker compose up -d
                On the Atlas free tier this should be available automatically; if it
                isn't, check the cluster has finished provisioning.
                """);
        }
    }

    private static async Task<CheckResult> CheckSearchQueryableAsync(IMongoClient client)
    {
        const string name = "Search and Vector Search queries";
        var db = MongoContext.GetDatabase(client);
        const string probe = "__doctor_probe";

        try
        {
            await db.DropCollectionAsync(probe);
            var collection = db.GetCollection<BsonDocument>(probe);
            await collection.InsertOneAsync(new BsonDocument
            {
                ["name"] = "rocking horse",
                ["v"] = new BsonArray { 1.0, 0.0, 0.0 }
            });

            await collection.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(
                "doctor_vector", SearchIndexType.VectorSearch,
                new BsonDocument("fields", new BsonArray
                {
                    new BsonDocument
                    {
                        ["type"] = "vector",
                        ["path"] = "v",
                        ["numDimensions"] = 3,
                        ["similarity"] = "cosine"
                    }
                })));

            Console.WriteLine("     (building a search index — this takes about 20 seconds)");

            var ready = await WaitUntilQueryableAsync(collection, "doctor_vector", TimeSpan.FromMinutes(2));
            if (!ready)
            {
                return CheckResult.Fail(name, "the index never became queryable",
                    "It was created, but did not reach status READY within two minutes.",
                    "Check the container logs for mongot errors: docker compose logs mongodb");
            }

            var hits = await collection.Aggregate<BsonDocument>(new[]
            {
                new BsonDocument("$vectorSearch", new BsonDocument
                {
                    ["index"] = "doctor_vector",
                    ["path"] = "v",
                    ["queryVector"] = new BsonArray { 0.9, 0.1, 0.0 },
                    ["numCandidates"] = 10,
                    ["limit"] = 1
                })
            }).ToListAsync();

            await db.DropCollectionAsync(probe);

            return hits.Count > 0
                ? CheckResult.Pass(name, "built an index and ran a $vectorSearch query successfully")
                : CheckResult.Fail(name, "the query returned nothing",
                    "The index was queryable but $vectorSearch found no documents.",
                    "Check the container logs: docker compose logs mongodb");
        }
        catch (Exception ex)
        {
            await db.DropCollectionAsync(probe);
            return CheckResult.Fail(name, "the probe failed", Brief(ex),
                "Check the container logs: docker compose logs mongodb");
        }
    }

    private static async Task<bool> WaitUntilQueryableAsync(
        IMongoCollection<BsonDocument> collection, string indexName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var cursor = await collection.SearchIndexes.ListAsync();
            var index = (await cursor.ToListAsync())
                .FirstOrDefault(i => i.GetValue("name", "").AsString == indexName);

            if (index is not null && index.GetValue("queryable", false).ToBoolean())
                return true;

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return false;
    }

    private static async Task<CheckResult> CheckSeedDataAsync(IMongoClient client)
    {
        const string name = "Workshop catalogue";
        try
        {
            var count = await MongoContext.GetToys(client)
                .CountDocumentsAsync(FilterDefinition<Shared.Models.Toy>.Empty);

            return count > 0
                ? CheckResult.Pass(name, $"{count} toys in the '{MongoContext.DatabaseName}' database")
                : CheckResult.Warn(name,
                    "the practice database is empty — fine for now, the tests seed what they need",
                    """
                    Nothing to do to run the tests: Part 1's tests load the toys themselves, and
                    Part 2's use a separate database.
                    To have something to look at in Compass or the shell, load the practice data:
                        dotnet run --project tools/seed
                    """);
        }
        catch (Exception ex)
        {
            return CheckResult.Warn(name, "could not count the toys", Brief(ex));
        }
    }

    private static CheckResult CheckApiKey(string variable, string what, string neededFor, string url)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        var name = $"{what} key";

        if (!string.IsNullOrWhiteSpace(value))
            return CheckResult.Pass(name, $"{variable} is set ({Mask(value)})");

        return CheckResult.Warn(name,
            $"{variable} is not set — you only need it for {neededFor}",
            $"""
             When you get there, create a key at {url} and set it:
                 export {variable}="your-key-here"
             Or, so it survives a rebuild, use .NET user secrets from the part's folder:
                 dotnet user-secrets set "{variable}" "your-key-here"
             Never commit the key.
             """);
    }

    private static string Mask(string value) =>
        value.Length <= 8 ? "********" : $"{value[..4]}…{value[^4..]}";

    /// <summary>
    /// Turns a driver exception into one readable line.
    /// </summary>
    /// <remarks>
    /// The MongoDB driver's server-selection timeout carries a 15-line dump of the
    /// cluster state and a stack trace. Shown raw, that reads like a catastrophe and
    /// buries the one useful phrase ("Connection refused"). We walk to the innermost
    /// exception, take its first line, and trim it.
    /// </remarks>
    private static string Brief(Exception ex)
    {
        // Server-selection timeouts embed the real cause as *text* inside the
        // message rather than as an InnerException, so match on it directly.
        var whole = ex.ToString();
        if (whole.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || whole.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
            return "Nothing is listening on that address — the server isn't running.";

        if (whole.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
            || whole.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
            return "That hostname doesn't resolve — check the host in your connection string.";

        if (whole.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase))
            return "Authentication failed — check the username and password.";

        var inner = ex;
        while (inner.InnerException is not null) inner = inner.InnerException;

        var line = inner.Message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? ex.GetType().Name;

        if (line.Length > 160) line = line[..157] + "…";
        return $"{inner.GetType().Name}: {line}";
    }
}
