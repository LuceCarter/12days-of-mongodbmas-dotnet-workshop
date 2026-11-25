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

You don't need any keys for Parts 1, 2, 3A or 3B, or for the exercises in Part 4A: the tests use stand-ins and the vectors are shipped. Don't go and get
them yet. They are only for **trying the real thing** (`tools/ask`) and for regenerating the shipped vectors.

| Key | Needed for | Where | Free? |
|---|---|---|---|
| `VOYAGE_API_KEY` | calling Voyage for real: Part 3B Step 7, Part 4A's `tools/ask` | [Voyage AI](https://www.mongodb.com/docs/voyageai/) | See the note below |
| `VOYAGE_BASE_URL` | only if your Voyage key was **created in MongoDB Atlas**: set it to `https://ai.mongodb.com` | n/a | n/a |
| `GEMINI_API_KEY` | Part 4A's `tools/ask` and Part 4B's `tools/elf-chat`: the language model | [Google AI Studio](https://aistudio.google.com/apikey) | Its free tier needs no card, but see the note below |
| `GOOGLE_CLOUD_PROJECT` | **instead of** `GEMINI_API_KEY`, if you can't create an AI Studio key (an organisation can block them): use Gemini on Google Cloud (Vertex AI). Needs `gcloud auth login`, and **billing enabled** on the project | [Google Cloud](https://cloud.google.com/sdk/docs/install) | Not free: it is billed to your project |
| `GOOGLE_CLOUD_LOCATION` | optional: the Vertex AI location (defaults to `global`; a model can be "not found" in one location and fine in another) | n/a | n/a |
| `GEMINI_MODEL` | optional: which Gemini model to ask (a default is set in `ElfLlm.cs`) | n/a | n/a |

Set them as environment variables:

```bash
export VOYAGE_API_KEY="..."
export GEMINI_API_KEY="..."
```

Or, so they survive a new terminal and never sit in your shell history, use .NET **user secrets**. They're stored outside the repository, so they can't be committed:

```bash
dotnet user-secrets set "GEMINI_API_KEY" "..." --project tools/ask
dotnet user-secrets set "VOYAGE_API_KEY" "..." --project tools/ask
```

(`tools/ask` and `tools/elf-chat` read both places, and an environment variable wins. Use `--project tools/elf-chat` with `dotnet user-secrets` for the second. The other tools read environment variables only.)

**No AI Studio key? Use Google Cloud.** Some organisations block creating Gemini API keys. Gemini is also available through Google Cloud's **Vertex AI**, and
the code supports it through its OpenAI-compatible endpoint:

```bash
gcloud auth login
export GOOGLE_CLOUD_PROJECT="your-project-id"       # billing must be enabled on this project
dotnet run --project tools/ask -- "How do I tell whether a toy part is a small part?"     # Part 4A
dotnet run --project tools/elf-chat                                                          # Part 4B
```

It gets a short-lived access token from `gcloud auth print-access-token` (or from `GOOGLE_CLOUD_ACCESS_TOKEN` if you set it), so there is no key to store. Vertex names models with a
`google/` prefix, and which Gemini models a project can use differs from AI Studio: set `GEMINI_MODEL` (for example `google/gemini-...`) if the default isn't available to you.
If you set both, `GEMINI_API_KEY` wins. Keep the project ID and any token out of the repository, like any other credential.

> **Gemini's free tier.** Google decides which models are free and what the limits are, and it changes them. We couldn't find per-model limits on its public
> pages (it points to your own AI Studio account instead), so check yours. Google's pricing page lists free-tier use as *"Used to improve our products: Yes"*: read
> the terms, and don't send anything private to a free key. If a call is refused, set `GEMINI_MODEL` to one your account lists as free.

**Never commit a key.** `.env` is already in `.gitignore`.

**Which Voyage key, and which host?** A key created on **voyageai.com** works with `api.voyageai.com`. A key created in
**MongoDB Atlas** (**AI Model APIs → Create model API key**) works with `ai.mongodb.com` instead (`us.ai.mongodb.com` or
`eu.ai.mongodb.com` if you scoped it to a region). Send a key to the wrong host and you get a `403` saying the key
can't access that endpoint. Set `VOYAGE_BASE_URL=https://ai.mongodb.com` for an Atlas key: `tools/embed`, `tools/voyage-check` and Part 3B's
`VoyageEmbedder.FromEnvironment()` and `VoyageReranker.FromEnvironment()` all read it.
Free tokens are documented unevenly (Voyage's FAQ says the first 200 million tokens are free for every account; MongoDB's
billing pages list some models, such as `voyage-3.5`, with none), so check the current terms before you rely on the free tier.

Part 3B also ships **pre-embedded toy data** (and pre-embedded example questions), so you can do the whole of
Part 3B without a Voyage key. To explore those vectors in Compass or the shell, load them into the practice
database: `dotnet run --project tools/seed -- --embeddings`. (Only a maintainer regenerating them needs a key:
`dotnet run --project tools/embed`.)

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
