# CQRS & MediatR: A Beginner's Guide

Right now, your API uses what is called a "God Service" pattern. Look at your `AuthService.cs` or `IAuthService.cs`. It handles *everything*: logging in, registering, verifying emails, resetting passwords, and refreshing tokens. 

As your app grows, `AuthService` will become massive. Every time a developer needs to test just the "verify email" logic, they have to mock dependencies for five other tasks they don't even care about!

**CQRS with MediatR** is the ultimate solution to the "God Service" problem.

---

## 1. What is CQRS?

**CQRS** stands for **Command Query Responsibility Segregation**. 

It is a simple rule that states your application should be split into two strict halves:
1. **Queries (Reads):** Code that asks for data. It *never* modifies the database. (e.g., "Give me a list of books").
2. **Commands (Writes):** Code that changes data (Insert, Update, Delete). It typically does not return a lot of data, maybe just a success flag or the new ID. (e.g., "Create this book" or "Reset my password").

By splitting these up, you allow your "Read" operations to be incredibly fast (maybe reading from a fast Cache), while your "Write" operations can handle all the complex validation and database saving logic safely without stepping on each other's toes.

## 2. What is MediatR?

**MediatR** is an incredibly popular open-source library for .NET that implements the **Mediator pattern**.

Think of MediatR like a Traffic Cop or a Post Office.

Instead of your `BooksController` directly talking to a `BookService`, it hands a letter to the Postmaster (MediatR), and says: *"Hey, here is a request to get a book. Deliver this to whoever is responsible for it."*

MediatR looks at the letter, finds the exact single class responsible for handling it, gives it the letter, gets the result, and hands it back to the Controller.

---

## 3. How It Looks in .NET (The Concept)

If you installed MediatR into `FirstApi`, your big `BookService.cs` class gets completely deleted. Instead, every single API operation gets its very own isolated class (known as a **Handler**).

This enforces the **Single Responsibility Principle**. 

### Step 1: Create a Request (The Letter)
First, you define the "Letter" (The Query) you want to send.

```csharp
// This is simply a C# Record that says: "I want a Book, and here is the ID I'm looking for."
public record GetBookByIdQuery(int BookId, int UserId) : IRequest<BaseResponse<Book>>;
```

### Step 2: Create a Handler (The Code)
Next, you write a dedicated class to handle *only* this specific query. It gets its own dependencies injected specifically for this single task.

```csharp
public class GetBookByIdQueryHandler : IRequestHandler<GetBookByIdQuery, BaseResponse<Book>>
{
    private readonly FirstApiContext _db;

    public GetBookByIdQueryHandler(FirstApiContext db)
    {
        _db = db;
    }

    public async Task<BaseResponse<Book>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
    {
        // 1. Do the database work and ownership check
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == request.BookId);
        if (book == null || book.UserId != request.UserId) 
            throw new KeyNotFoundException("Book not found.");

        return BaseResponse<Book>.SuccessResponse("Success", book);
    }
}
```

### Step 3: The Magic in the Controller
Your controllers become incredibly "thin." They don't need to inject 10 different services anymore. They only ever inject **one thing**: IMediator.

```csharp
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IMediator _mediator; // <-- The Postmaster!

    public BooksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBook(int id)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        
        // You just hand MediatR the letter. It magically finds your handler and returns the book!
        var response = await _mediator.Send(new GetBookByIdQuery(id, userId));

        return Ok(response);
    }
}
```

---

## The Verdict: Should you use it?

If you implement CQRS and MediatR, your application will scale beautifully. 

Instead of a giant `AuthService.cs` file with 500 lines of code, your folder structure will look extremely clean and organized, like this:
```text
/Features
    /Books
        /Queries
            GetBookByIdQuery.cs
            GetAllBooksQuery.cs
        /Commands
            CreateBookCommand.cs
            DeleteBookCommand.cs
```

If a bug happens during Book Creation, you know *exactly* which single file (`CreateBookCommand.cs`) the issue is in, without having to dig through massive God classes!

However, just like Clean Architecture, **it adds a lot of files and boilerplate**. For every single API endpoint you build, you must write at least two new files (The Request, and the Handler). If you value having all your logic visible in one place (like your current `Service` layer), CQRS might feel tedious.
