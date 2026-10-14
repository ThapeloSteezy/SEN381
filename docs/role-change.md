# CivicConnect roles

Milestone 2 defined five roles: Requester, Staff, Supervisor, Management and Administrator (ASM-03, SH-03, FR-07). The app uses three.

## The roles

- **Requester** reports problems and sees only their own requests and their history.
- **Staff** see the jobs assigned to them. They start the work (Assigned to In Progress) and mark it fixed (In Progress to Resolved). They can also report problems like anyone else.
- **Supervisor** sees every request and uses the facility map. They assign each new request to a staff member (New to Assigned), can give an open request to someone else, and close a request once it is fixed (Resolved to Closed).

A request still moves one step at a time: New, Assigned, In Progress, Resolved, Closed. The service rejects a step from the wrong person and leaves the request unchanged:

- Only a supervisor can assign, and only to a staff account.
- Only the assigned staff member can start or fix the job.
- Staff cannot close a request.
- A request is never moved to Assigned without a person being picked.

Each assignment is stored on the request (`assignedToId`, `assignedTo`) and in the history, with who did it and when.

## Updates

Everyone has a bell in the header with a red count of unread updates. Updates are stored on the request in MongoDB (`Feedback`, one entry per recipient). An update counts as read once its recipient opens the request.

- The staff member gets "{supervisor} gave you a new job." when a request is assigned or reassigned to them.
- The requester is told who will fix it, when work starts, when it is fixed and when it is closed.
- The supervisor who assigned the job gets "{staff} fixed this. It is ready to close."

These are in-app only. There is no email.

## Changes from Milestone 2

- FR-07 (a supervisor assigns staff) is implemented as described.
- Management and Administrator are folded into Supervisor.
- Staff can open a request only when it is assigned to them or they reported it. Anything else returns "not found", the same as for requesters.
