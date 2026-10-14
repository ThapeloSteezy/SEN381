# Screens, flow and facility map

Each screen does one job.

| Screen | Who it is for | What they do there | Obvious in the first two seconds |
| --- | --- | --- | --- |
| Home (signed out) | Anyone | Sign in | One Sign in button |
| Home (requester) | Requester | Report a problem or check their requests | Two big buttons, then their open requests |
| Home (staff) | Staff | See what to fix | Big buttons, then their open jobs |
| Home (supervisor) | Supervisor | Find new requests and fixed ones to close | Big buttons for all requests and the map, then new requests |
| Sign in | Anyone | Sign in and go back to the page they wanted | Email, password, Sign in |
| Request list | Requester: own requests as cards. Staff: "My jobs" cards. Supervisor: all requests as a table with filters, search and who it is assigned to | Open a request | Open requests first |
| Report a problem | Anyone signed in | Pick a type, pick a room, say what happened, say how bad | Four numbered steps with big tiles |
| Request | Everyone who may see it | See the status and who has it | A large coloured status box. The supervisor picks a person; the assigned staff member gets "Start work" or "Mark as fixed"; the supervisor gets "Close request" |
| Map | Supervisor | See where open requests are | Coloured rooms with counts |

Roles are Requester, Staff and Supervisor. See `role-change.md`.

## Visual rules

- One typeface: the system UI font (Segoe UI on Windows).
- Navy header (`#0f2a4a`), light grey page, white panels, blue (`#1d4ed8`) for buttons and links.
- Each status has one colour, used for badges, the progress bar, history dots and map rooms: New blue, Assigned amber, In Progress teal, Resolved green, Closed grey.
- Priority is coloured text: High orange, Urgent red.
- Flat surfaces, 1px borders, 6px corners, no shadows or gradients.

## Flow rules

- Signed-out visitors are sent to sign in and come back to the page they asked for. Only local addresses are accepted as the return address.
- A requester who opens someone else's request sees "Request not found" (404). The page does not confirm that the request exists.
- The detail page offers only the next legal status. The service still rejects a skipped step, and it also rejects a change when the request moved since the page was loaded. In both cases the request is left unchanged and the typed note is kept.
- Submitting a request records a "Submitted" history entry, so history starts with the requester.

## Facility map

- Building: Riverside Community Centre, ground floor. Rooms are defined in `Domain/FacilityPlan.cs`.
- Each request stores `RoomId` in its MongoDB document. The submit form requires a room, and the service rejects ids that are not on the plan.
- The plan is inline SVG in the Razor view. Each room is a link to `/Requests/Map?room=<id>`, so it works with Tab and Enter and without JavaScript or WebGL.
- A room with open requests shows a marker with the count, styled by its earliest open status. Rooms with no open requests have no marker.