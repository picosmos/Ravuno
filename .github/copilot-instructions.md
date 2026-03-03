# GitHub Copilot instructions (repository-wide)

These instructions apply to code and content generated in this repository.

## General Advice

- Match existing project conventions (naming, formatting, file layout).
- In EntityFramework models: Always use Attribute bases configuration, never fluent API.
- If implementing another service, model or other parts, check already existing things from the same layer to adhere to the same style.
- Always use formatting tools like `dotnet csharpier format .` and `dotnet format`. 
- Fix all warnings you introduced, not only errors.
- Use CLI tools if available, e.g. for creating migrations instead of writing them from scratch.
- In .NET, never use Console.WriteLine or similar for logging. The only use-case should be an CLI interface. Use always ILogger<T>.

## Source Control / git

- Make small changes that are easy to review.
- Commit often, with a clear message describing the change and its purpose.
- Keep code always buildable and well formatted before committing.
- Use clear commit messages that describe the change and its purpose.

## Coding guidelines

- Keep functions small and focused.
- Avoid introducing new dependencies unless they clearly reduce complexity.
- Add/update tests when changing behavior, when the repo has an existing test setup.
- Prefer clear error messages and safe defaults.

## Style & formatting

- Follow the repo’s formatter/linter if present (e.g., Prettier/ESLint, Black/Ruff, gofmt).
- Don’t reformat unrelated code.

## Documentation

- Do not write unnecessary readmes or documentation unless requested.
- Code should be self documenting without comments if possible. If comments are needed, they should be clear and concise.
- Keep comments and documentation, if needed brief and easy to understand. Avoid unnecessary details and focus on the main points.

## What to avoid

- Don’t add new files, pages, features, or refactors unless requested.
- Don’t change public APIs without explicitly calling it out.

## Verification

- Do not write tests
