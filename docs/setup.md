# Get set up

All the code for these challenges lives in one public repository:
**[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**. Every command in these
challenges runs from the root of that repository — the folder that contains
`docker-compose.yml`.

You need a MongoDB deployment that supports **Search** and **Vector Search**, and
the .NET SDK. There are three ways to get there. Pick one.

Whichever you pick, finish by running the environment check:

```bash
dotnet run --project tools/doctor
```

It tells you what's wrong and how to fix it. Run it any time something stops working.

---

## Option 1 — GitHub Codespaces (easiest)

1. Open the repository: **[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**.
2. Click **Code → Codespaces → Create codespace on main**.

Everything is installed and started for you: .NET 10, a MongoDB Atlas Local
deployment, and the environment check runs automatically when the container
finishes building. First build takes a few minutes. The terminal opens in the
repository root, so every command in the challenges works as written.

Nothing to install locally. Nothing to configure.

---

## Option 2 — Local, with Docker

**You need:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) and
the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop.git
cd 12days-of-mongodbmas-dotnet-workshop

docker compose up -d        # starts MongoDB with Search and Vector Search
docker compose ps           # wait until it says "healthy" (about 20 seconds)

dotnet run --project tools/doctor
```

No git? Use **Code → Download ZIP** on the repository page and unzip it instead.

That's it. Your connection string is:

```
mongodb://workshop:workshop@localhost:27017/?directConnection=true
```

The code finds it automatically, so you don't need to set anything.

### Useful commands

| What | Command |
|---|---|
| Start | `docker compose up -d` |
| Stop, keep data | `docker compose down` |
| Stop, **delete** data and search indexes | `docker compose down -v` |
| See what MongoDB is doing | `docker compose logs -f mongodb` |
| Open a shell against it | `docker compose exec mongodb mongosh -u workshop -p workshop --authenticationDatabase admin` |
| Load (or reset) the practice data to explore in Compass and the shell | `dotnet run --project tools/seed` |

---

## Option 3 — MongoDB Atlas free tier (no Docker)

Use this if you can't run Docker, or you'd rather work against the real thing.
The free M0 tier is enough for every part of this challenge.

You still need the code on your machine, so start by cloning the repository (Docker
isn't needed for this option):

```bash
git clone https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop.git
cd 12days-of-mongodbmas-dotnet-workshop
```

Then:

1. Create a free cluster at [mongodb.com/cloud/atlas/register](https://www.mongodb.com/cloud/atlas/register).
   Choose the **M0 free** tier.
2. **Database Access** → add a database user, and note the password.
3. **Network Access** → **Add Current IP Address**.
4. **Connect** → **Drivers** → **C# / .NET** → copy the connection string.
5. Tell the code to use it:

   ```bash
   export MONGODB_URI="mongodb+srv://USER:PASSWORD@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority"
   ```

   On Windows PowerShell:
   ```powershell
   $env:MONGODB_URI = "mongodb+srv://..."
   ```

6. ```bash
   dotnet run --project tools/doctor
   ```

> **If your password has `@`, `:`, `/` or `#` in it**, it must be URL-encoded in the
> connection string, or the driver will read it as part of the address. The easiest
> fix is to set a password without those characters.

> **"Could not reach the MONGODB_URI you set"** on Atlas is almost always the IP
> access list. Your home IP changes, and coffee shop wifi certainly will.
> Atlas → Network Access → Add Current IP Address.

---

## API keys

You don't need any keys for Parts 1, 2 or 3A. Don't go and get them yet.

| Key | Needed for | Where | Free? |
|---|---|---|---|
| `VOYAGE_API_KEY` | Parts 3B, 4 — turning text into vectors | [Voyage AI](https://www.mongodb.com/docs/voyageai/) | Yes, generous free tier |
| `GEMINI_API_KEY` | Part 4 — the language model | [Google AI Studio](https://aistudio.google.com/apikey) | Yes, no card needed |

Set them as environment variables:

```bash
export VOYAGE_API_KEY="..."
export GEMINI_API_KEY="..."
```

Or, so they survive a rebuild, use .NET user secrets from the part's folder:

```bash
dotnet user-secrets set "GEMINI_API_KEY" "..."
```

**Never commit a key.** `.env` is already in `.gitignore`.

Part 3B also ships **pre-embedded toy data**, so you can do the whole of Part 3B
without a Voyage key if you'd rather skip the embedding generation step.

---

## Troubleshooting

**`docker compose up` says the port is in use.**
Something else is already on 27017 — probably a MongoDB you installed previously.
Stop it, or change the host side of the port mapping in `docker-compose.yml`
(`"27018:27017"`) and set `MONGODB_URI` to match.

**My search index returns nothing, but there's no error.**
Search indexes take about 20 seconds to build. Until then they exist but are not
queryable, and queries quietly return zero results. Wait, then check:

```bash
docker compose exec mongodb mongosh -u workshop -p workshop --authenticationDatabase admin \
  --eval 'db.getSiblingDB("santas_workshop").toys.getSearchIndexes()'
```

Look for `"queryable": true`.

**My search indexes disappeared.**
You probably ran `docker compose down -v`, which deletes the volumes. Re-create the
indexes by re-running the part's setup step.

**The doctor says the server is standalone, not a replica set.**
You're on a plain `mongo` container rather than the Atlas Local one. Run
`docker compose down && docker compose up -d` from the repo root, which uses the
right image.

**Everything is broken and I want to start over.**
```bash
docker compose down -v
docker compose up -d
```
This deletes all data and all search indexes, then starts fresh.
