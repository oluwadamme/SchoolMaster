# CI/CD with GitHub Actions: A Beginner's Guide

Imagine you are working on a team. You just finished building a huge new feature for your `FirstApi`. You push your code to GitHub, and your teammate immediately merges it. 

Ten minutes later, production is down. It turns out you missed a semicolon in `BookService.cs`, and the tests were failing locally, but you forgot to run `dotnet test` before pushing.

**CI/CD (Continuous Integration / Continuous Deployment)** is the process of automating checks and deployments so human errors like that never happen.

---

## 1. The Core Concepts

### Continuous Integration (CI)
This is your safety net. Every time you push code to GitHub (or open a Pull Request), a robot automatically clones your code, compiles it, and runs every single one of your xUnit tests. If a test fails, the robot blocks the Pull Request from being merged.

### Continuous Deployment (CD)
If the CI step passes (all tests turn green), the robot automatically takes your code, packages it (e.g., builds the Docker container), and deploys it straight to your production server (like AWS, Azure, or DigitalOcean). 

---

## 2. Enter GitHub Actions

GitHub Actions is GitHub's built-in robot for CI/CD. You talk to this robot by creating `.yml` (YAML) files inside a special folder in your project: `.github/workflows/`.

There are three main parts to a GitHub Action:

1. **Triggers (`on`)**: *WHEN* should the robot run? (e.g., "On every push to the `develop` branch").
2. **Jobs (`jobs`)**: *WHERE* should the robot do its work? (e.g., "Boot up a fresh Ubuntu Linux server in the cloud").
3. **Steps (`steps`)**: *WHAT* should the robot do? (e.g., "Install .NET, clone my repo, run `dotnet test`").

---

## 3. How It Works For .NET (The Concept)

If you were to set this up for `FirstApi`, you would create a file called `.github/workflows/dotnet.yml`.

Here is a heavily commented conceptual example of what that file looks like:

```yaml
# The name of your workflow
name: .NET CI Pipeline

# 1. THE TRIGGER: When does this run?
on:
  push:
    branches: [ "develop", "main" ]  # Run when someone pushes to develop or main
  pull_request:
    branches: [ "main" ]             # Run when someone opens a PR against main

# 2. THE JOBS: Where does this run?
jobs:
  build-and-test:
    # GitHub spins up a completely fresh Ubuntu server in the cloud for you
    runs-on: ubuntu-latest

    # 3. THE STEPS: What does it do?
    steps:
    # Step A: Download your code onto the Ubuntu server
    - name: Checkout Code
      uses: actions/checkout@v4

    # Step B: Install the .NET SDK you are using
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 10.0.x

    # Step C: Download all NuGet packages (MailKit, Npgsql, EF Core, etc.)
    - name: Restore dependencies
      run: dotnet restore

    # Step D: Compile the code to make sure there are no syntax errors
    - name: Build project
      run: dotnet build --no-restore --configuration Release

    # Step E: Run your xUnit Tests!
    - name: Run automated tests
      run: dotnet test --no-build --verbosity normal
```

---

## 4. Environment Variables & Secrets

Sometimes your tests need to connect to a real database, or they need a JWT Secret Key to run. You *never* hardcode passwords into your `.yml` file.

Instead, GitHub has a feature called **GitHub Secrets**. You go to your Repository Settings in GitHub → Secrets and variables → Actions, and you securely save your `JWT_SECRET`.

Then, you can map that secret into your test runner inside the `.yml`:

```yaml
    - name: Run automated tests
      env:
        Jwt__Key: ${{ secrets.JWT_SECRET }}
      run: dotnet test --no-build
```

---

### In Summary

Setting up CI/CD with GitHub Actions shifts the burden of quality control from *you* to the *cloud*. 

Once you have a `.github/workflows/dotnet.yml` file tracked in your repository, every single push will instantly trigger a cloud build. You'll get a beautiful green checkmark (✅) on GitHub if your code is pristine, or a red cross (❌) if you broke something, ensuring your `main` branch is always fully functional!
