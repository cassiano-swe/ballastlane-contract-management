# Task Management REST API — GitHub Copilot Prompt

## 1. Role

Act as a senior .NET backend engineer experienced in REST API design, EF Core, resource-level authorization, and automated testing. Prioritize readable code, explicit business rules, and consistency with the repository.

## 2. Project Context and Stack

Implement a task management API using C# 14, .NET 10, ASP.NET Core Minimal APIs, EF Core 10, and PostgreSQL through the Npgsql EF Core provider.

Before editing, inspect the repository to confirm the target framework, dependencies, `User` primary key, authentication setup, application-user resolution, database configuration, and test infrastructure. Reuse the repository's conventions for endpoint registration, validation, error responses, and tests.

If the repository uses a different stack or lacks a prerequisite such as authentication, application-user resolution, or a test framework, identify the gap and ask how to proceed before changing the affected area.

## 3. Objective and Scope

Implement the following endpoints:

| Endpoint | Behavior | Success response |
|---|---|---|
| `POST /tasks` | Create a task for the authenticated application user | `201 Created` with the task and a `Location` header pointing to its GET endpoint |
| `GET /tasks` | List the authenticated user's tasks | `200 OK` with an array, including `[]` when empty |
| `GET /tasks/{id}` | Retrieve a task owned by the authenticated user | `200 OK` with the task |
| `PUT /tasks/{id}` | Replace the task's editable fields | `204 No Content` |
| `DELETE /tasks/{id}` | Delete a task owned by the authenticated user | `204 No Content` |

Limit changes to the task feature, its database mapping and migration, endpoint registration, and automated tests. Preserve the existing `User` model, shared authentication configuration, and unrelated API contracts.

## 4. Restrictions

- **Dependencies.** Ask for approval before introducing any library or package the repository does not already use. Explain why it is needed and what the current stack can support.
- **Clarification.** Inspect the repository and its documentation first. If a requirement or convention remains unclear and the answer affects implementation, ask a focused question and wait for clarification before proceeding with the affected work.
- **Git.** Never commit directly to the repository's primary branch. If asked to commit, use a feature branch.
- **Test coverage.** Add or update automated tests for every added or changed behavior. Run the relevant tests and report any checks that remain pending.

## 5. Task Model and HTTP Contract

- Each task has an ID, a title, an optional description, a status, a due date, and a required relationship to its owner.
- Require a title with 1 to 200 characters after trimming. Store the trimmed value.
- Limit descriptions to 2,000 characters. For `PUT`, an omitted or null description clears the previous value.
- Require one of these status values: `pending`, `in_progress`, or `completed`.
- Require a due date. Use `DateOnly` in C# and expose `due_date` in JSON using `YYYY-MM-DD`. Allow past dates so users can record overdue tasks.
- Use separate request and response DTOs. Return `400 Bad Request` for invalid input and follow the repository's Problem Details convention for validation errors.
- Use asynchronous EF Core operations and propagate cancellation tokens. Add a migration that creates the task table with a required foreign key to the existing user table.

Example request body for `POST /tasks` or `PUT /tasks/{id}`:

```json
{
  "title": "Review task API changes",
  "description": "Verify the authorization and validation tests.",
  "status": "in_progress",
  "due_date": "2026-12-15"
}
```

## 6. Authentication and Authorization

Require authentication on all task endpoints. Resolve the authenticated identity to the existing application user through the repository's established mechanism.

- Return `401 Unauthorized` for unauthenticated requests.
- Return `403 Forbidden` when an authenticated identity cannot be resolved to an application user.
- Assign ownership on the server. An extra `ownerId` field in a request must never let the caller assign a task to another user.
- Include the owner ID in the database query for listing, reading, updating, and deleting tasks.
- Return `404 Not Found` for both missing tasks and tasks owned by another user.

## 7. Few-Shot Examples

The following snippets illustrate the desired behavior. Adapt names, ID types, and route constraints to the patterns found in the repository.

### Load a task within the current user's scope

```csharp
var task = await db.Tasks.SingleOrDefaultAsync(
    item => item.Id == taskId && item.OwnerId == currentUser.Id,
    cancellationToken);

if (task is null)
{
    return Results.NotFound();
}
```

### Build the creation response from the named GET route

```csharp
tasks.MapGet("/{id:guid}", GetTaskAsync).WithName("GetTask");

// After saving the new task.
return Results.CreatedAtRoute(
    "GetTask",
    new { id = task.Id },
    response);
```

## 8. Automated Tests and Acceptance Criteria

Use the existing test framework and conventions. Cover the following scenarios:

- **CRUD.** Create a task, follow the `Location` header, verify it appears in the owner's list, update it, and read it again to confirm the changes persisted. Delete it and confirm a subsequent GET returns `404`.
- **Empty list.** Confirm a user with no tasks receives `200 OK` with `[]`.
- **Validation.** Reject missing and blank titles, titles over 200 characters after trimming, descriptions over 2,000 characters, missing or unknown statuses, and missing or malformed due dates. Verify title trimming, acceptance of past due dates, and clearing a description on update.
- **Authentication.** Verify `401` for an unauthenticated request and `403` for an authenticated identity without an application-user mapping.
- **User isolation.** Create a task as user A. Verify that user B cannot list it and receives `404` when attempting to read, update, or delete it. Read the task again as user A to verify its values are intact.
- **Server-assigned ownership.** Submit a creation request as user A with an extra `ownerId` field identifying user B. Depending on the repository's JSON configuration, the API may reject the request or ignore that field. Verify that it never creates a task for user B.

Run database integration tests against an isolated test database. Verify that the migration applies successfully and that the existing relevant tests still pass.

## 9. Execution Workflow

1. **Inspect and plan.** Identify the relevant files, existing conventions, and missing prerequisites. Present a short implementation plan. Ask focused questions for unresolved requirements and request approval for any new dependency.
2. **Implement and review.** Implement the API and its tests. Review the generated code against the HTTP contract, ownership rules, and validation boundaries. Correct discrepancies and add or update the tests that verify each correction.
3. **Validate.** Build the solution, apply the migration to an isolated test database, run the relevant tests, and inspect the HTTP responses. Record actual outcomes.

## 10. Expected Final Output

Provide:

1. A short implementation summary and the resulting unified diff, including the migration and tests. If working in chat only, label it as a proposed diff.
2. A representative code sample showing how authenticated ownership is enforced. Include either the creation response or the owner-scoped update handler.
3. A **Review and Corrections** section describing how the generated code was checked, any concrete issues found, the changes made, and the tests that verify them. Report the checks performed if no correction was necessary.
4. A **Verification** section with the identity-to-user mapping, the commands actually executed, their observed results, and any pending checks with their blockers.

Report only changes and validation that actually occurred. Stop after completing the Task API and reporting the results.



**End of the prompt submitted to GitHub Copilot**

---

## Implementation Sample and AI Review — Response

*This section is my response to the assessment. It was not part of the prompt submitted to GitHub Copilot.*

### 1. Representative Code Sample

The following excerpt shows how `PUT /tasks/{id}` limits updates to the authenticated user's own tasks. The endpoint requires authentication and resolves the application user before executing this code.

```csharp
var task = await db.Tasks.SingleOrDefaultAsync(
    item => item.Id == id && item.OwnerId == currentUser.Id,
    cancellationToken);

if (task is null)
{
    return Results.NotFound();
}

var errors = request.Validate();

if (errors.Count > 0)
{
    return Results.ValidationProblem(errors);
}

request.ApplyTo(task);
await db.SaveChangesAsync(cancellationToken);

return Results.NoContent();
```

### 2. How I Validated the AI's Suggestions

I review the code generated by GitHub Copilot against the API contract and the repository's conventions. I check where authentication was enforced, how the authenticated identity was mapped to an application user, and whether every task query was scoped to that user.

I run `dotnet build` and `dotnet test` to check for any compilation issues and to ensure that no unit tests are failing.

I verify that an update attempted by another user returned 404 Not Found and that the task's saved values remained unchanged.

### 3. How I Corrected or Improved the Output

For example, if Copilot generates an integration test that only checks for a `404 Not Found` response when one user attempts to update another user's task, I would extend the test. After the rejected request, I would retrieve the task as its owner and verify that the title, description, status, and due date still match their original values. This would verify both the HTTP response and the persisted state.

### 4. How I Handled Edge Cases, Authentication, and Validation

- **Authentication and ownership.** All task endpoints require authentication. The API returns `401 Unauthorized` when the request lacks valid credentials and `403 Forbidden` when the authenticated identity cannot be mapped to an application user. The API assigns ownership on the server using that application user. All task queries are scoped to the current owner. A missing task and a task owned by another user both return `404 Not Found`, which prevents callers from discovering whether another user's task exists.

- **Input validation.** The API trims the title and requires it to contain between 1 and 200 characters. It limits the optional description to 2,000 characters, accepts only `pending`, `in_progress`, or `completed` as the status, and requires `due_date` in `YYYY-MM-DD` format. Invalid input returns `400 Bad Request`.

- **Edge cases.** A user with no tasks receives `200 OK` with an empty JSON array. Past due dates are accepted so users can record overdue work. Since `PUT` replaces the editable fields, omitting the optional description clears the stored value.