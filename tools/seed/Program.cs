using System.Text.RegularExpressions;
using SantasWorkshop.Shared;

// Loads the workshop's practice data into the `santas_workshop` database, so there is
// something to look at in Compass and the shell.
//
//     dotnet run --project tools/seed
//
// Safe to run as often as you like: it empties and refills `toys`, `orders` and `ledger`,
// which puts everything back as it started. It touches nothing else.
//
// The tests don't need this. Part 1's tests reseed `toys` themselves, and the tests for Parts 2 and 3A use
// separate databases (`santas_workshop_part2_tests`, `santas_workshop_part3a_tests`) so they never overwrite what you are
// exploring. This is for YOU, not for them.

// Part 3B explores vectors in Compass and the shell, so it asks for the toys WITH their embeddings:
//     dotnet run --project tools/seed -- --embeddings
var withEmbeddings = args.Contains("--embeddings", StringComparer.OrdinalIgnoreCase);

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — loading the practice data");
Console.WriteLine();

var where = MongoContext.UsingLocalFallback
    ? "localhost (the docker-compose database)"
    : "the server in your MONGODB_URI";
Console.WriteLine($"   Server   : {where}");
Console.WriteLine($"   Database : {MongoContext.DatabaseName}");
Console.WriteLine();

try
{
    var client = MongoContext.CreateClient();
    var counts = await SeedLoader.ResetWorkshopAsync(MongoContext.GetDatabase(client), withEmbeddings: withEmbeddings);

    Console.WriteLine($"   ✅ toys    {counts.Toys,3} documents{(withEmbeddings ? "  (each with a 1024-dimension vector, for Part 3B)" : "")}");
    Console.WriteLine($"   ✅ orders  {counts.Orders,3} documents");
    Console.WriteLine($"   ✅ ledger  {counts.Ledger,3} documents");
    Console.WriteLine();
    Console.WriteLine("   Connect Compass or mongosh with:");
    Console.WriteLine($"       {ConnectionStringForDisplay()}");
    Console.WriteLine();
    return 0;
}
catch (InvalidOperationException ex) when (withEmbeddings && ex.Message.Contains("embeddings.json"))
{
    // Not a database problem: the vectors Part 3B ships with aren't in this build.
    Console.WriteLine("   ❌ The vectors Part 3B ships with aren't in this build.");
    Console.WriteLine();
    Console.WriteLine($"   {ex.Message}");
    Console.WriteLine();
    return 1;
}
catch (Exception ex)
{
    var first = ex.Message.Split('\n')[0].Trim();
    Console.WriteLine("   ❌ Couldn't reach MongoDB.");
    Console.WriteLine();
    Console.WriteLine("   Start it, then check everything is healthy:");
    Console.WriteLine("       docker compose up -d");
    Console.WriteLine("       dotnet run --project tools/doctor");
    Console.WriteLine();
    Console.WriteLine($"   ({ex.GetType().Name}: {(first.Length > 140 ? first[..137] + "…" : first)})");
    Console.WriteLine();
    return 1;
}

// The local docker-compose credentials are public (they're in docker-compose.yml), so they can be
// shown. A connection string from MONGODB_URI is yours: show where it points, never the password.
static string ConnectionStringForDisplay() =>
    MongoContext.UsingLocalFallback
        ? MongoContext.LocalConnectionString
        : Regex.Replace(MongoContext.ConnectionString, @"(://[^:/@]+:)[^@]*@", "$1*****@");
