# User profile read contract

Status: Characterized for Phase 3 read-only migration  
Legacy source: `ConferenceController.UserProfile` at legacy commit `c345252`  
Verification boundary: Static inspection and in-memory tests only; no database connection was made

## Routes

| Contract | Method | Route | Authorization |
|---|---|---|---|
| Canonical profile | GET | `/api/v1/users/{id}/profile` | Authenticated |
| Canonical current profile | GET | `/api/v1/users/me/profile` | Authenticated |
| Legacy compatibility | GET | `/Conference/UserProfile?id={id}` | Authenticated |

The current-profile route is a new API convenience over the same characterized behavior and resolves the user id from the validated JWT NameIdentifier claim. The legacy application has no equivalent route.

## Selection and response

- User data comes from AspNetUsers.
- The employee profile is selected by ApplicationUserId, IsActive descending, then Id descending.
- Display name precedence is Employee.DisplayName, Employee.Name, ApplicationUser.Name, UserName, Email, then `موظف`.
- Role/title uses nonblank Employee.JobTitle, otherwise the first Identity role, otherwise `مستخدم`.
- Phone uses Employee.PhoneNumber, then ApplicationUser.PhoneNumber, then `-`.
- A nonblank employee image is trimmed, backslashes become slashes, a leading `~` is removed, and relative paths receive `/`.
- Missing images use `/Conference/Avatar?id={userId}`.
- Blank id returns 400 with the legacy Arabic message; unknown users return 404 with the legacy Arabic message.
