# Moving CivicConnect to a three-tier app on MongoDB

CivicConnect was designed in Milestone 2 as a modular monolith using PostgreSQL and Entity Framework. We are changing that to a three-tier web app on MongoDB.

The pages stay the same. A few services still check who can see a request and which status comes next. One store talks to the database. A request is saved as a single document with its category, history, and feedback already inside it. Users live in a second collection.

## Why we are changing

Milestone 2 chose the modular monolith and PostgreSQL so requests, users, history, and feedback would stay consistent through layers, joins, and transactions. Building that showed the stack was heavier than this project needs. CivicConnect is one website, one team, and one request lifecycle. Nothing else calls it.

The extra database layers and SQL identity tables made the system harder to build, explain, and run than the requirements ask for. Milestone 3 allows a significant design change when we record the original decision, the problem, the new decision, and the consequence.

## Why this option

We kept a simple three-tier split instead of letting the pages talk to the database directly. The status rule and the role checks stay in the services, so those requirements are still enforced.

MongoDB fits that shape. One request document replaces the joins between requests, categories, history, and feedback.

## Pros

- The design matches the size of the project.
- A request and its history are stored together, so the app does not depend on joins.
- There is one data store instead of a context, repositories, a unit of work, and Identity tables.
- The app is easier to run locally and easier to explain in the milestone defence.
- The status rule and role checks remain in one place, so they can still be tested.

## Cons

- The database no longer enforces foreign keys or wraps each status change in a transaction. The services have to keep history and status consistent.
- Two people updating the same request at the same time need a check in the application. PostgreSQL was going to do that with a version field.
- This is a real departure from the Milestone 2 baseline, so the PED and the decision records have to show the change. It should not be treated as a small coding tweak.
- The team has to retest sign-in, submitting a request, and every status change on the new store.

## What does not change

People still submit a categorised request and follow it from New to Assigned, In Progress, Resolved, and Closed. Staff still update the status. The person who submitted it can still see their own request and its history. Roles still limit who can see and change a request.
