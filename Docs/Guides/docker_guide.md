# Docker & Docker Compose: A Beginner's Guide

## 1. The "It Works on My Machine" Problem

Imagine this scenario: You build your API, and it runs perfectly on your Mac. You hand the code to a teammate (or a production server), and it immediately crashes.

Why?
- They don't have PostgreSQL installed.
- Or they have PostgreSQL, but it's version 12, and you used version 16.
- They have a different version of the .NET SDK installed.
- Their computer doesn't have the same environment variables set up.

**Docker solves this.** It allows you to package your application and EVERYTHING it needs to run (the runtime, libraries, the operating system it sits on, the database) into a single, standardized unit called a **Container**.

---

## 2. Core Concepts: Images & Containers

Think of Docker in terms of Object-Oriented Programming (OOP):

### 💿 The Docker Image (The Class)
A Docker Image is like a blueprint or a recipe. It's a read-only file that contains instructions on how to build your environment.
*"Start with Linux. Install .NET 10. Copy my SchoolMaster code. Build it. Expose port 8080."*

### 📦 The Docker Container (The Object/Instance)
A Container is a running instance of an Image. If an image is the blueprint of a house, the container is the actual house you can walk into. You can spin up 5 identical containers from the exact same image.

**Key Rule:** Containers are completely isolated. Whatever happens inside a container doesn't affect your Mac, and whatever is on your Mac (mostly) doesn't affect the container.

---

## 3. The `Dockerfile`

To create your own custom image (for your API), you write a `Dockerfile`. It's literally just a text file named `Dockerfile` (no extension) sitting at the root of your project.

Here is what a standard .NET `Dockerfile` typically looks like under the hood:

```dockerfile
# 1. Start with the official .NET SDK image to BUILD your app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

# 2. Start fresh with the much smaller .NET Runtime image to RUN your app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SchoolMaster.Api.dll"]
```
*(Notice how we use a big SDK to build it, but then only package the tiny runtime for the final image. This keeps the final container lightweight!)*

If you ran `docker build`, Docker would read this file and create a single Image containing your compiled API.

---

## 4. Enter `docker-compose.yml`

A `Dockerfile` is great for your API. But your API doesn't live alone. **Your API depends on a PostgreSQL database and a Redis server.**

If you only use a `Dockerfile`, you still have to manually run these dependencies somehow.

This is where **Docker Compose** comes in. Compose is a tool for defining and running **multi-container** applications. You write a single `docker-compose.yml` file to say: *"Hey Docker, I have a system made of 3 moving parts. Start them all, and let them talk to each other."*

### A Real Example for `SchoolMaster`

If we were to write a `docker-compose.yml` for your project, it would look like this:

```yaml
version: '3.8'

services:
  # ------------- Service 1: The Database -------------
  db:
    image: postgres:16-alpine           # Use the official Postgres image from Docker
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=MySecretPassword123
      - POSTGRES_DB=schoolmaster_db
    ports:
      - "5432:5432"                     # Expose it so you can still use DBeaver/PgAdmin on your Mac!

  # ------------- Service 2: Redis (Cache + Hangfire) -------------
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  # ------------- Service 3: Your API -------------
  api:
    build: .                            # Tell Docker to build the Dockerfile in this folder
    ports:
      - "7000:8080"                     # Map Mac's port 7000 to the container's port 8080
    depends_on:
      - db
      - redis
    environment:
      # This overrides the settings in your appsettings.json!
      - ConnectionStrings__DefaultConnection=Server=db;Port=5432;Database=schoolmaster_db;Username=postgres;Password=MySecretPassword123
      - ConnectionStrings__Redis=redis:6379
```

---

## 5. The Magic of Networking

Look closely at the `ConnectionStrings__DefaultConnection` environment variable in the yaml above.

```text
Server=db;Port=5432...
```

Notice how the Server is **`db`**, not `localhost`?

When you use Docker Compose, it creates a **custom private network** for your containers. Inside that network, containers can talk to each other using their service names (`db`, `redis`, and `api`) like actual URLs.

If your API tries to hit `localhost:5432`, it will fail, because inside the API's container, "localhost" means *the API container itself*, and there is no database running in the API container! Instead, it asks the network for `db`, and Docker routes the traffic to the Postgres container.

---

## 6. Overriding `appsettings.json` with Environment Variables

A core concept of Dockerizing .NET apps is that you **never hardcode passwords or connection strings in the container image**.

In .NET, Environment Variables automatically override `appsettings.json`.

If your `appsettings.json` looks like this:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;..."
  }
}
```

You can override it in `docker-compose.yml` by replacing the `:` with double underscores `__`.
So `ConnectionStrings:DefaultConnection` becomes `ConnectionStrings__DefaultConnection`.

When the API boots up inside the container, .NET prioritizes the environment variable over the JSON file.

---

## 7. The Workflow

Once this is set up, your entire workflow changes from dealing with `dotnet run` and manually installing PostgreSQL on new machines, to a single, powerful command.

To start your entire infrastructure (Database + Redis + API):
```bash
docker-compose up --build
```
*Docker downloads dependencies, builds your API, creates the network, and starts them all.*

To stop and destroy everything (cleaning up your Mac):
```bash
docker-compose down
```

### Summary of Why This is Awesome
1. **Zero Database Setup:** You no longer need PostgreSQL installed on your Mac. Docker spins it up on demand.
2. **Instant Onboarding:** A new developer clones your repo, runs `docker-compose up`, and is coding 30 seconds later.
3. **Identical to Production:** The exact container that runs on your Mac is the *exact same* container that will run on AWS/Azure, virtually eliminating "It works on my machine" bugs.

Whenever you're ready to actually implement this, let me know and we can write the code!
