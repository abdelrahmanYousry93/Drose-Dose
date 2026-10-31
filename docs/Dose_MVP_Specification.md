# Dose --- MVP Product & Technical Specification

> **Version:** 1.1 (updated with approved product decisions --- see
> section 20 and the change log at the end).
>
> **Purpose:** This document is the implementation brief for an AI
> coding agent (for example, Codex) to build the first usable version of
> Dose.
>
> **Product positioning:** Dose is a trusted learning service. Students
> come to Dose to get the right explanation for a specific learning
> need. Teachers are part of the service delivery system, but teacher
> discovery is not the primary product experience.

------------------------------------------------------------------------

## 1. Product vision

Dose helps school students get focused academic support without having
to buy a full course or search through lists of famous teachers.

A student should be able to:

1.  Select their educational stage, grade, subject, curriculum/language
    track, and learning need.
2.  Choose one of three service types:
    -   One lesson
    -   One chapter/unit package
    -   General revision
3.  Browse available sessions:
    -   Private sessions
    -   Open group sessions that other students can join
    -   A request for Dose to arrange a new session when no suitable
        session exists
4.  See the assigned teacher's identity and profile **before confirming
    the booking**.
5.  See the price, date, time, duration, group size, and
    booking/cancellation terms before confirming.
6.  Book with minimal friction. Do not require account creation just to
    browse.

The primary brand promise is: **Dose helps you understand what you need,
when you need it.**

## 2. MVP principles and scope

### Product principles

-   Mobile-first responsive web application, installable as a PWA if
    practical.
-   Arabic-first RTL user experience. Keep the architecture
    localization-ready.
-   Dose is the primary brand; do not make the teacher directory the
    home-page experience.
-   Teacher name, photo, qualifications, and relevant experience must be
    visible on the session details/confirmation journey before the
    student commits.
-   Minimize student data collection. Collect only what is needed to
    arrange and communicate the booking.
-   Pricing, grade bands, service types, packages, group size options,
    and booking policies must be data-driven and editable by
    administrators. Do not hard-code business pricing rules in the
    frontend or backend.
-   Start with a modular monolith, not microservices.
-   Build reliable booking and capacity rules before adding advanced
    automation.

### In scope for MVP

1.  Public student-facing pages and session discovery.
2.  Educational catalog: study track, grade, subject, chapter, lesson.
3.  Three service types: one lesson, chapter/unit package, general
    revision.
4.  Configurable pricing plans, group size options, and admin-defined
    packages.
5.  Teacher profiles and teacher-subject/grade qualifications.
6.  Teacher availability and session scheduling.
7.  Private and group sessions.
8.  Open group sessions that accept additional bookings.
9.  Student requests for a new session when no suitable session is
    available.
10. Booking lifecycle, temporary holds, capacity protection, and price
    snapshots.
11. **Manual payment via InstaPay and Vodafone Cash, with payment proof
    (screenshot) uploaded on the platform or sent on WhatsApp, and
    admin review.**
12. Basic admin dashboard to manage catalog, teachers, pricing,
    packages, sessions, bookings, payments, and requests.
13. Basic teacher dashboard to manage availability and view assigned
    sessions.
14. Meeting URL stored against a session (Google Meet links entered
    manually).
15. WhatsApp contact/deep link and manual operational messaging in the
    first release.
16. Basic booking notifications and audit/logging foundations.

### Explicitly out of scope for the first release

-   Native Android/iOS apps.
-   Building a proprietary video-conferencing platform.
-   Microservices, RabbitMQ, Redis, Elasticsearch, or Kubernetes unless
    a demonstrated requirement emerges.
-   AI tutor, automated lesson diagnosis, automatic teacher matching
    using AI, or automated content generation.
-   Payment gateway integration (card payments, automatic InstaPay /
    wallet verification). Payments are verified manually by an admin
    in the MVP.
-   Automatic WhatsApp Cloud API messaging unless credentials, Meta
    Business setup, approved templates, and operational requirements are
    available.
-   Public teacher marketplace/ranking as the main discovery experience.

If an out-of-scope item is required to make a core flow work, document
the dependency and propose the smallest viable implementation rather
than silently expanding scope.

------------------------------------------------------------------------

## 3. Users and roles

Implement role-based authorization.

### Student / guardian

-   Browse without an account.
-   Select educational details and service type.
-   Browse available sessions and view teacher details.
-   Submit a booking using name and WhatsApp number.
-   Upload payment proof or send it on WhatsApp.
-   View booking confirmation and receive the session details through
    the configured communication channel.
-   A student may be a minor. **Guardian contact is mandatory for
    primary and preparatory stages, and optional for secondary** (see
    section 9.5). Keep student name and contact person fields distinct.

### Teacher

-   Sign in to a protected teacher area.
-   Manage or submit availability, subject to admin
    approval/configuration.
-   View assigned sessions and enrolled students only to the extent
    needed to deliver the session.
-   View session meeting link and schedule.
-   Update attendance/session completion if permitted by the configured
    workflow.

### Administrator

-   Manage grades, study tracks, subjects, chapters, lessons, service
    types, and packages.
-   Manage teacher profiles, qualifications, and availability.
-   Create, publish, cancel, and manage sessions.
-   Configure pricing plans, group size options, and pricing policies.
-   Review payment proofs and approve/reject payments.
-   Manage group top-up decisions (section 6.4).
-   View and manage bookings, payment status, session requests,
    notifications, and reviews.
-   Configure capacity, hold duration, payment-proof deadline,
    tier-decision deadline, cancellation rules, and session confirmation
    policy.
-   View basic operational reports.

------------------------------------------------------------------------

## 4. Education catalog and classification

Keep educational classification separate from pricing.

Suggested hierarchy:

`StudyTrack -> Grade -> Subject -> Chapter -> Lesson`

Grades also belong to an **educational stage** (Primary, Preparatory,
Secondary). The stage is used for stage-level rules such as guardian
contact requirements.

Examples of study tracks may include Arabic and Languages, but do not
assume these are the only possible tracks. The data model must allow
administrators to add or deactivate tracks.

Grades and suggested pricing bands:

  -----------------------------------------------------------------------
  Suggested pricing band              Grades
  ----------------------------------- -----------------------------------
  Early Primary                       Grade 1--3 primary

  Upper Primary                       Grade 4--6 primary

  Preparatory                         Grade 1--2 preparatory

  Preparatory Certificate             Grade 3 preparatory

  Secondary Year 1                    Grade 1 secondary

  Secondary Year 2--3                 Grade 2--3 secondary, with the
                                      actual study system/track recorded
  -----------------------------------------------------------------------

Important:

-   Do not permanently encode these labels or grade groupings in
    application logic.
-   Store grade-to-pricing-band assignment as configurable data.
-   Store stage-level rules (for example `RequiresGuardianContact`) as
    configurable data on the stage (or grade), not in code.
-   Secondary grades must support the relevant study system (for
    example, the current applicable secondary/baccalaureate track). Do
    not assume all grade 2--3 secondary students follow the same
    system.
-   A student's study track/language and the actual curriculum/system
    should be separately modeled if they represent different concepts in
    the chosen domain model.

------------------------------------------------------------------------

## 5. Services and packages

The platform offers exactly three initial service types. They must be
records/configuration, not hard-coded UI branches.

### 5.1 One lesson

-   A focused explanation of one lesson or a defined part of a lesson.
-   Pricing is configured by applicable pricing rules.
-   The service scope must identify the lesson or topic.

### 5.2 Chapter / unit package

-   One package price covers the complete agreed chapter/unit scope.
-   A package may include **one or multiple sessions**.
-   Link the package to its actual scheduled sessions.
-   Track package completion and remaining sessions for each student
    booking/enrollment.
-   Do not charge the package price once per session. The package price
    is the total agreed price for the package.
-   If a package has multiple sessions, the booking/enrollment must make
    clear which sessions are included.

### 5.3 General revision

-   A defined revision scope, such as selected chapters, a term, or a
    specified portion of the syllabus.
-   The scope must be explicit before the student books.
-   Pricing is configured through pricing rules.
-   Avoid ambiguous descriptions such as "whole syllabus revision"
    without specifying what is covered and how many sessions are
    included.

### 5.4 Admin-defined packages (approved decision)

Packages (chapter/unit packages and general revision packages) are
**fully dynamic**. Administrators create and edit them from the admin
dashboard; nothing about package structure is hard-coded.

Each package definition is a database record with at least:

-   Name and description (Arabic, localization-ready)
-   Service type (chapter/unit or general revision)
-   Educational scope: grade(s), study track, subject, and the included
    chapters/lessons (via `ServicePackageItems`)
-   Number of included sessions
-   Duration per session (minutes)
-   Validity period (days from enrollment or a fixed end date), optional
-   Allowed session modes (private, group, or both)
-   Linked pricing plan/rule
-   Rescheduling/absence limits, optional
-   Active/inactive status and display order

Rules:

-   Changing a package definition must not change existing enrollments.
    Each enrollment stores a snapshot of the package terms it was bought
    under (session count, scope, validity, price).
-   Deactivating a package hides it from new bookings but keeps existing
    enrollments working.

Suggested entities:

-   `ServiceTypes`
-   `ServicePackages`
-   `ServicePackageItems` (links chapters/lessons to a package)
-   `PackageEnrollments` (student's purchase/enrollment in a package,
    including the terms snapshot)
-   `PackageSessions` (links a package enrollment to one or more
    sessions)
-   `PackageAttendance` or equivalent completion tracking

------------------------------------------------------------------------

## 6. Pricing engine

Pricing must be dynamic and database-driven. No business price values
should be hard-coded in source code.

### 6.1 Pricing dimensions

A pricing plan/rule may be selected using some or all of these
dimensions:

-   Pricing band / grade
-   Study track or curriculum/language
-   Subject (optional if the price is common across subjects)
-   Service type (one lesson, chapter/unit, general revision)
-   Package (for package pricing)
-   Session mode (private or group)
-   Group size option (for group sessions)
-   Currency
-   Effective date range

Use explicit matching and priority rules. Prevent ambiguous overlapping
active rules for the same pricing context, or define and test a
deterministic precedence strategy.

### 6.2 Private session pricing

A private session has one student and a fixed configured price for the
applicable service/pricing plan.

Example only: One private lesson: EGP 150 per student.

Do not assume this example is a production price. Admins must be able to
configure the actual amount.

### 6.3 Group size options (tiers)

Group pricing is defined through **group size options** (tiers). Each
tier has a participant range and a per-student price.

Example configuration only:

  Option        Minimum participants   Maximum participants   Price per student
  ------------- ---------------------- ---------------------- -------------------
  Small group   2                      4                      EGP 120
  Medium group  5                      9                      EGP 100
  Large group   10                     15                     EGP 80

The values above are illustrative only. Administrators must be able to
add, edit, deactivate, and order tiers.

Requirements:

-   Tiers must not overlap.
-   Validate ranges and ensure the configured tiers cover the intended
    capacity without accidental gaps.
-   A group session's capacity equals the maximum of its target tier
    (section 6.4).
-   Display the price, the target group size, and the worst-case price
    clearly before booking.
-   Never silently change an already-confirmed booking's stored amount.

### 6.4 Group pricing policy --- Target Tier with Top-Up (approved default)

Dose uses a **commitment model**: the student pays upfront at the price
of the group size they choose, and Dose works to fill the group.

**How it works:**

1.  Every group session has a **target tier** (for example "up to 5
    students" or "up to 10 students").
    -   When a student requests a new group session, the student chooses
        the target group size.
    -   When an admin publishes an open group session, the admin sets
        the target tier.
2.  Every student who books that session pays the **target tier price**
    from the start (for example, the price for a group of 5).
3.  Session capacity = target tier maximum. The session cannot be
    overfilled beyond its target tier.
4.  A configurable **tier decision deadline** is set before the session
    start (default suggestion: 24 hours before start; configurable per
    session or globally).
5.  At the tier decision deadline, the system counts **confirmed
    (paid and approved)** bookings:
    -   **Count ≥ target tier minimum:** the session proceeds at the
        agreed price. No changes.
    -   **Count < target tier minimum:** the session is flagged
        `TopUpRequired`. The applicable price is recalculated from the
        tier matching the actual count (or the private price if only one
        student remains). Dose contacts each student (WhatsApp) **before
        the session** with these options:
        a.  Pay the difference (top-up) and keep the session.
        b.  Move to another session/date (transfer the paid amount).
        c.  Cancel and receive a full refund.
        d.  (Admin option) Dose waives the difference.
6.  A student is **never charged the difference without explicit
    acceptance.** If the student does not respond by a configurable
    response deadline, the default action is configurable (suggested
    default: hold the booking and let an admin decide; never auto-charge).
7.  If students join after the deadline and the count reaches the
    target tier again before the session, outstanding top-ups are
    cancelled (recorded as reversed adjustments).

**Mandatory disclosure before booking:**

The student must see, before confirming:

-   The price they pay now (target tier price).
-   The target group size and current confirmed count.
-   The tier decision deadline.
-   The maximum possible extra amount if the group does not fill
    (worst-case price minus paid price), and the options they will have
    (pay difference, move, or full refund).
-   An explicit acceptance checkbox for these terms.

**Financial recording:**

-   The top-up is recorded as a `FinancialAdjustment` (type
    `TopUpDue`) linked to the booking, with its own payment-proof and
    approval flow. Waivers and refunds are also adjustments.
-   The original price snapshot is never modified.

**Alternative policies (configurable, not default):**

-   `NewBookingsOnly`: price follows the current tier; a new tier price
    applies to new bookings only.
-   `RepriceAllBookings`: recalculate all eligible bookings when the
    tier changes, recorded as auditable credits/amounts due.

The policy must be stored in data/configuration per pricing plan or
session (enum value `TargetTierWithTopUp` as the default).

### 6.5 Price snapshots and audit

For every confirmed booking/enrollment, store an immutable price
snapshot containing at least:

-   Pricing plan/rule ID and version (where available)
-   Currency
-   Base price
-   Target tier ID and its range (for group sessions)
-   Worst-case price disclosed to the student (for group sessions)
-   Final agreed amount
-   Pricing policy
-   Calculation timestamp

Keep financial adjustments (top-ups, waivers, refunds, transfers) in a
separate ledger/transaction table. Never overwrite historical booking
prices when an administrator edits a pricing plan.

------------------------------------------------------------------------

## 7. Sessions, scheduling, and group participation

### Session modes

-   `Private`: one student; fixed price.
-   `Group`: multiple students; target tier price (section 6.4).

### Session creation

A session is associated with:

-   Service/package and educational scope
-   Teacher
-   Start and end time
-   Time zone (use an explicit configured business time zone; store
    timestamps in UTC and render local time correctly)
-   Mode
-   Target tier (group sessions) --- determines capacity and minimum
-   Booking deadline
-   Tier decision deadline (group sessions)
-   Current status
-   Pricing plan and group pricing policy
-   Meeting provider and meeting URL (optional until assigned)

### Open group sessions

A published group session can be visible to other students and accept
additional bookings until:

-   capacity (target tier maximum) is reached,
-   booking deadline is reached,
-   session is cancelled,
-   or the session reaches a configured status that prevents booking.

Student-facing session card should show:

-   Subject and lesson/chapter/revision scope
-   Grade and study track
-   Date and time
-   Duration
-   Teacher identity and relevant profile details
-   Target group size, confirmed participant count, and remaining seats
-   Price per student (target tier price)
-   Tier decision deadline and what happens if the group does not fill
-   Booking deadline and cancellation terms

Do not expose other students' personal data.

### Session requests

If no suitable session exists, a student can submit a request with:

-   Student/guardian contact
-   Education details and service type
-   Requested lesson/chapter/revision scope
-   Preferred date/time windows
-   Private, or group with a chosen target group size
-   Optional notes

Admins can review similar requests and create a session. Matching
requests automatically is not required in the MVP.

### Multi-session packages

A chapter/unit package may be delivered through multiple sessions. Model
the package as the commercial offering and sessions as its delivery
units. A student purchases/enrolls in the package once and receives
access to the included sessions. Avoid duplicating the full package
price on every session.

------------------------------------------------------------------------

## 8. Booking lifecycle, payment, and concurrency

Implement booking as a transactional workflow.

Booking statuses:

-   `PendingHold` --- seat held, contact details not yet submitted
-   `PendingPayment` --- details submitted, waiting for payment proof
-   `PaymentUnderReview` --- proof submitted, waiting for admin approval
-   `Confirmed` --- payment approved
-   `TopUpPending` --- group did not reach target; waiting for the
    student's decision/top-up (section 6.4)
-   `Cancelled`
-   `Expired`
-   `RefundPending`
-   `Refunded`
-   `Transferred`
-   `Completed`
-   `NoShow`

Session statuses:

-   `Draft`
-   `Published` / `Open`
-   `MinimumReached` (optional derived state)
-   `TopUpRequired` (group did not reach target tier at the decision
    deadline)
-   `Confirmed`
-   `InProgress`
-   `Completed`
-   `Cancelled`

### 8.1 Payment (approved decision)

-   Payment methods: **InstaPay** and **Vodafone Cash**. Methods and
    receiving account details (InstaPay address / wallet number) are
    configuration data, editable by admin, not hard-coded.
-   After submitting contact details, the student sees the amount,
    the receiving account, and the booking reference to include in the
    transfer note where possible.
-   The student submits payment proof by either:
    1.  Uploading a screenshot on the platform (booking page), or
    2.  Sending it on WhatsApp; the admin then attaches it to the
        booking manually.
-   Payment proof record (`PaymentProof`): booking/adjustment ID,
    method, amount, sender number or transaction reference (optional),
    screenshot file, channel (Platform / WhatsApp), submitted at, review
    status (`Submitted`, `Approved`, `Rejected`), reviewer, reviewed at,
    rejection reason.
-   Screenshot uploads: images only (JPG/PNG/WebP), size limit
    (configurable, e.g. 5 MB), stored in private storage (not a public
    URL), accessible only to admins. Strip/ignore any executable
    content; validate file type by content, not extension.
-   A booking becomes `Confirmed` **only when an admin approves the
    payment proof.**
-   Seat reservation:
    -   `PendingHold` / `PendingPayment` reserve the seat until a
        configurable **payment-proof deadline** (suggested default: 60
        minutes). If no proof is submitted by then, the booking expires
        and the seat is released.
    -   `PaymentUnderReview` keeps the seat reserved until the admin
        decides. Admin review SLA is operational, but the dashboard
        must highlight pending reviews and their age.
    -   If proof is rejected, the student is notified and may resubmit
        within a configurable window before expiry.
-   Duplicate proof protection: warn the admin when the same
    transaction reference is used on more than one booking.

### 8.2 Booking flow

1.  Student selects a published session.
2.  Backend validates session status, booking deadline, capacity,
    eligibility, and duplicate booking.
3.  Backend creates a temporary seat hold with an expiration time, using
    a database transaction/locking strategy that prevents overselling.
4.  Backend calculates and returns the applicable price (and for group
    sessions the worst-case price) and a quote expiry time.
5.  Student reviews the teacher, time, scope, price, and group terms.
6.  Student submits the minimum required contact details and accepts the
    terms (including the group top-up terms where applicable).
7.  Student pays via InstaPay or Vodafone Cash and submits proof.
8.  Admin approves the proof → booking `Confirmed`; price snapshot is
    stored.
9.  Notifications are queued/sent (confirmation with booking reference;
    meeting link closer to the session).
10. Expired holds/unpaid bookings release capacity automatically.
11. For group sessions, the tier decision job runs at the decision
    deadline (section 6.4).

Important engineering requirements:

-   Prevent two simultaneous requests from taking the last seat.
-   Enforce uniqueness/duplicate-booking rules at the database level
    where possible.
-   Do not rely only on frontend validation.
-   Use idempotency protection for booking confirmation and payment
    approval.
-   Recheck price and capacity before the student submits details. If
    the quote changed, show the updated price and require student
    acceptance.
-   Expire holds via a background job, but also check expiry during
    booking operations so correctness does not depend solely on the
    scheduler.
-   The tier decision job must be idempotent and re-runnable.
-   Define how a package enrollment reserves or consumes multiple
    session seats.

------------------------------------------------------------------------

## 9. Student experience and page requirements

### 9.1 Home page

-   Dose brand and concise value proposition.
-   Primary action: "What do you need help with?"
-   Education selectors: grade, subject, study track/language.
-   Search by lesson/topic.
-   Entry point to open group sessions.
-   Entry point to request a session.
-   No mandatory sign-up to browse.

### 9.2 Service selection

Present:

-   One lesson
-   Chapter/unit package (packages loaded from admin configuration)
-   General revision (packages loaded from admin configuration)

Explain scope and pricing basis in simple language.

### 9.3 Available sessions

Filters:

-   Grade
-   Subject
-   Study track/language
-   Service type
-   Date/time
-   Private/group (and group size)
-   Price range (optional)

Session cards should prioritize the learning need, date/time, price, and
availability---not teacher popularity.

### 9.4 Session details

Show teacher identity before booking:

-   Display name and photo
-   Short bio/qualifications
-   Relevant teaching experience/subjects
-   Session scope
-   Date/time/duration
-   Private/group mode
-   Target group size, price, and worst-case price
-   Confirmed participant count and remaining seats
-   Tier decision deadline and top-up options
-   Cancellation/refund/confirmation policy

### 9.5 Booking

Collect only essential data:

-   Student name
-   WhatsApp number
-   **Guardian name and guardian WhatsApp number --- mandatory when the
    selected grade belongs to a stage with `RequiresGuardianContact =
    true`.** Seed configuration: Primary = true, Preparatory = true,
    Secondary = false (guardian contact optional). This must be
    configurable, not hard-coded.
-   Required consent/terms acceptance (including group top-up terms)

Then the payment step: amount, receiving account, booking reference, and
screenshot upload (or "send on WhatsApp" action).

Do not require a full student account for the first booking unless
needed for a specific secure feature. Provide a secure booking lookup
mechanism (for example, one-time verification code) before exposing
personal booking details.

### 9.6 Booking confirmation

Show:

-   Booking reference
-   Service and educational scope
-   Teacher
-   Date/time
-   Price and payment status (Pending payment / Under review /
    Confirmed)
-   Meeting link when available (confirmed bookings only)
-   WhatsApp support/contact action
-   Cancellation/rescheduling instructions

### 9.7 Open group sessions page

A dedicated screen for sessions created by Dose or requested by students
and opened for additional participants.

Show:

-   "Open for joining" status
-   Target group size, confirmed count, and remaining seats
-   Price per student
-   Tier decision deadline
-   Teacher and schedule
-   Join action

### 9.8 Admin dashboard

Minimum admin modules:

-   Catalog management (including stages and guardian rules)
-   Pricing plans and group size option (tier) editor
-   Package builder (section 5.4)
-   Teacher management and qualifications
-   Teacher availability
-   Session management
-   Booking management
-   **Payment review queue** (pending proofs, oldest first, approve/reject)
-   **Top-up management** (sessions in `TopUpRequired`, each student's
    decision, waive/refund/transfer actions)
-   Session request management
-   Notification log
-   Basic dashboard metrics

### 9.9 Teacher dashboard

Minimum:

-   Sign-in
-   Availability management or availability submission
-   Assigned sessions
-   Session details and meeting URL
-   Student count (avoid exposing unnecessary student data)
-   Attendance/completion update if enabled

------------------------------------------------------------------------

## 10. WhatsApp integration

### MVP

-   Use the WhatsApp Business app with manual operations.
-   Dose WhatsApp Business number: **+20 100 916 0638** (normalized
    `+201009160638`). Store it in configuration (`WhatsApp:BusinessNumber`
    or an admin setting), not in source code.
-   Add a click-to-chat link (`https://wa.me/201009160638?text=...`)
    with a prefilled message containing only the non-sensitive booking
    reference.
-   Do not put private student details or sensitive information in URL
    query strings.
-   Store student/guardian WhatsApp numbers in normalized international
    format (E.164).
-   Admin can manually send confirmation/reminder/top-up messages using
    approved operational templates. The admin dashboard should offer
    "copy message" and "open WhatsApp chat" actions per booking.
-   Payment screenshots received on WhatsApp are attached to the booking
    by the admin (section 8.1).

### Later phase: WhatsApp Business Platform / Cloud API

Design an integration boundary so the provider can be added without
rewriting booking logic:

-   `INotificationProvider` or equivalent abstraction.
-   WhatsApp Cloud API implementation behind the abstraction.
-   Webhook endpoint for inbound messages and delivery events.
-   Template management and message status logging.
-   Retry strategy and idempotency.
-   Respect WhatsApp's template and customer-service-window rules and
    applicable pricing.
-   Never report a message as sent unless the provider confirms
    acceptance; distinguish queued, sent/accepted, delivered, read, and
    failed where the provider exposes those states.

------------------------------------------------------------------------

## 11. Online meetings

Use Google Meet initially rather than building video infrastructure.

MVP:

-   Meeting links are created manually by Dose operations from the Dose
    Google account, then pasted into the session by an admin. The
    system does not store or need any Google account credentials.
-   Share the link only with confirmed participants and the assigned
    teacher.
-   Do not expose the meeting URL on public session cards.
-   Store meeting provider and meeting URL separately.

Future:

-   Evaluate Google Calendar/Meet API integration to create meetings
    automatically.
-   Keep meeting provider abstract enough to support Zoom later.
-   Do not assume API access or licensing is available until credentials
    and account requirements are verified.

------------------------------------------------------------------------

## 12. Recommended technology stack

Use the existing team's .NET and Angular strengths.

### Frontend

-   Angular, current stable supported version at implementation time.
-   Responsive Arabic RTL UI.
-   PWA capability if straightforward.
-   Typed API client and environment-based configuration.
-   Accessible forms and clear loading/error/empty states.

### Backend

-   ASP.NET Core Web API, current supported LTS version at
    implementation time.
-   Entity Framework Core.
-   Modular monolith with clear modules:
    -   Catalog
    -   Pricing
    -   Packages
    -   Teachers
    -   Scheduling
    -   Sessions
    -   Bookings
    -   Payments
    -   Notifications
    -   Administration
-   FluentValidation or equivalent request validation.
-   Structured logging and centralized exception handling.
-   Role-based authorization.

### Data

-   SQL Server / Azure SQL.
-   EF Core migrations.
-   Explicit constraints, indexes, concurrency handling, and
    transactions.
-   Store timestamps in UTC and convert to the configured local time
    zone for display.

### File storage

-   Payment screenshots in private blob storage (Azure Blob Storage with
    private container, or local disk in development) behind an
    `IFileStorage` abstraction. Serve to admins through authorized
    endpoints or short-lived signed URLs only.

### Background processing

-   Hangfire for hold/payment expiry, tier decision checks, reminders,
    and retryable background tasks.
-   Background jobs must be idempotent.

### Hosting

-   Azure App Service and Azure SQL are suitable initial options.
-   Keep infrastructure modest; choose exact plan after estimating
    traffic and cost.
-   Store secrets in environment variables/managed secret storage, never
    in source control.

### Avoid initially

-   Microservices
-   Kubernetes
-   RabbitMQ
-   Redis unless measured caching/concurrency needs justify it
-   Elasticsearch
-   Custom video streaming/conferencing

------------------------------------------------------------------------

## 13. Suggested solution structure

Adapt to the repository's existing conventions if a project already
exists. Do not overwrite or restructure an existing repository without
inspecting it first.

A possible backend layout:

``` text
src/
  Dose.Api/
  Dose.Application/
    Catalog/
    Pricing/
    Packages/
    Teachers/
    Scheduling/
    Sessions/
    Bookings/
    Payments/
    Notifications/
  Dose.Domain/
    Entities/
    Enums/
    ValueObjects/
    Pricing/
  Dose.Infrastructure/
    Persistence/
    Configurations/
    Repositories/
    Notifications/
    Meetings/
    Storage/
  Dose.Contracts/
  Dose.Web/                 # Angular app may live in a separate workspace
tests/
  Dose.UnitTests/
  Dose.IntegrationTests/
```

Prefer feature-oriented organization if that better fits the repository.

------------------------------------------------------------------------

## 14. Suggested core domain model

The following is a starting point, not a requirement to copy every
property literally. Inspect the repository and refine
names/relationships while preserving the business rules.

### Catalog

-   `EducationalStage` (with `RequiresGuardianContact`)
-   `StudyTrack`
-   `Grade`
-   `PricingBand`
-   `Subject`
-   `Chapter`
-   `Lesson`
-   `ServiceType`
-   `ServicePackage`
-   `ServicePackageItem`

### People

-   `Student`
-   `Guardian` (or guardian contact fields on the booking/student)
-   `Teacher`
-   `TeacherQualification`
-   `TeacherSubject`
-   `AdminUser`

### Pricing

-   `PricingPlan`
-   `PricingRule`
-   `PricingTier` (group size option)
-   `PricingPlanVersion` (recommended for auditability)
-   `BookingPriceSnapshot`
-   `FinancialAdjustment` (TopUpDue, Waiver, Refund, Transfer)

### Scheduling and delivery

-   `TeacherAvailability`
-   `Session` (with target tier and tier decision deadline)
-   `PackageEnrollment`
-   `PackageSession`
-   `SessionRequest`
-   `Booking`
-   `Attendance`
-   `MeetingDetails`

### Operations

-   `PaymentMethod` (InstaPay, Vodafone Cash; receiving account details)
-   `Payment`
-   `PaymentProof`
-   `Notification`
-   `NotificationAttempt`
-   `Review`
-   `AuditLog`
-   `SystemSetting` (hold duration, payment-proof deadline, tier
    decision offset, WhatsApp number, time zone, etc.)

Avoid redundant mutable fields such as `CurrentParticipants` unless
there is a clear performance reason. If stored, maintain it
transactionally and reconcile it against active bookings. Otherwise
calculate it from bookings with appropriate indexes.

------------------------------------------------------------------------

## 15. Important business rules to implement and test

1.  A private session has capacity one unless an administrator
    explicitly configures otherwise.
2.  A group session has a target tier; its capacity is the tier maximum
    and every student pays the target tier price.
3.  Pricing tiers cannot overlap.
4.  The selected pricing rule must match the student's grade/pricing
    band, study track, service type, and any configured subject
    dimension.
5.  The system must handle no matching pricing rule with a clear
    admin/configuration error; never silently fall back to a hard-coded
    price.
6.  A chapter/unit package has one total package price even if it
    includes multiple sessions.
7.  Packages are admin-defined data; enrollments keep a snapshot of the
    package terms.
8.  Package enrollment and session attendance/completion are tracked
    separately.
9.  The teacher is assigned to the session and is visible before booking
    confirmation.
10. Public session listings must not expose private student information
    or meeting links.
11. Capacity is enforced transactionally.
12. Duplicate active bookings by the same student/contact for the same
    session are prevented.
13. Temporary holds and unpaid bookings expire and release seats.
14. A booking is confirmed only after admin approval of payment proof.
15. Confirmed booking prices are immutable snapshots.
16. If a group does not reach its target tier minimum by the tier
    decision deadline, students are offered top-up, transfer, or full
    refund. No top-up is charged without the student's explicit
    acceptance.
17. Any top-up, waiver, refund, or transfer is recorded as a financial
    adjustment, not an overwrite.
18. Guardian contact is required for grades whose stage requires it
    (configurable; seed: primary and preparatory).
19. All admin changes to pricing, packages, session capacity, teacher
    assignment, payment approvals, and booking status are auditable.
20. Notification failures must not undo a valid booking; they should be
    logged and retried.
21. Booking confirmation and payment approval endpoints are idempotent.
22. All user input (including uploaded files) is validated server-side.

------------------------------------------------------------------------

## 16. API outline

Use REST endpoints as a starting point. Refine names and response
contracts consistently.

### Public catalog

-   `GET /api/catalog/study-tracks`
-   `GET /api/catalog/grades?studyTrackId=`
-   `GET /api/catalog/subjects?gradeId=`
-   `GET /api/catalog/chapters?subjectId=&gradeId=`
-   `GET /api/catalog/lessons?chapterId=`
-   `GET /api/services`
-   `GET /api/packages?gradeId=&subjectId=&serviceTypeId=`

### Pricing and sessions

-   `POST /api/pricing/quote`
-   `GET /api/sessions`
-   `GET /api/sessions/{id}`
-   `GET /api/sessions/open-groups`
-   `POST /api/session-requests`

### Booking and payment

-   `POST /api/bookings/holds`
-   `POST /api/bookings/{id}/details` (contact details + terms
    acceptance)
-   `GET /api/payment-methods`
-   `POST /api/bookings/{id}/payment-proofs` (multipart upload,
    rate-limited)
-   `GET /api/bookings/{reference}` (must be protected by secure
    verification)
-   `POST /api/bookings/{id}/cancel`
-   `POST /api/bookings/{id}/top-up-decision` (accept top-up / transfer
    / refund --- protected by secure verification)

### Admin

-   CRUD endpoints for catalog, stages, pricing plans/rules/tiers,
    packages, teachers, availability, sessions, payment methods,
    settings, and requests.
-   Payment review: list pending proofs, approve, reject, attach proof
    received on WhatsApp.
-   Top-up management: list `TopUpRequired` sessions, record decisions,
    waive, refund, transfer.
-   Admin booking management endpoints.

### Teacher

-   Availability endpoints.
-   Assigned session list/details.
-   Attendance/completion endpoints where enabled.

For all endpoints:

-   Use DTOs rather than exposing EF entities.
-   Add pagination/filtering for list endpoints.
-   Return consistent validation and error responses.
-   Apply authorization policies to all non-public endpoints.

------------------------------------------------------------------------

## 17. Non-functional requirements

-   Arabic RTL first, responsive on common mobile screen sizes.
-   Secure authentication for admin and teacher areas.
-   No secrets or provider tokens in frontend code.
-   Protect personal data and restrict access by role. Payment
    screenshots are personal/financial data: admin-only access.
-   Use HTTPS in deployed environments.
-   Rate-limit public booking, upload, and verification endpoints.
-   Validate and normalize phone numbers (E.164, Egypt default +20).
-   Add structured logs and correlation IDs.
-   Add database indexes for session date/status, booking
    session/status, payment proof status, student contact, and pricing
    lookup dimensions.
-   Add automated tests for pricing, target-tier top-up, booking
    concurrency, payment approval, package enrollment, and state
    transitions.
-   Provide seed data for development only; clearly label all sample
    prices as non-production.
-   Include database migrations and setup instructions.
-   Include `.env.example` or equivalent configuration template without
    secrets.

------------------------------------------------------------------------

## 18. Implementation plan for the coding agent

Work in small, verifiable phases. Do not attempt to generate the entire
platform in one unreviewed pass.

### Phase 0 --- Repository inspection and plan

1.  Inspect the repository, current code, framework versions,
    conventions, and existing database.
2.  Identify what already exists; do not replace working code
    unnecessarily.
3.  Produce a concise implementation plan and proposed ERD.
4.  List assumptions and remaining unresolved business decisions.
5.  Wait for approval before destructive changes or major architectural
    restructuring.

### Phase 1 --- Domain and persistence

1.  Implement catalog (including stages) and service entities.
2.  Implement admin-defined packages.
3.  Implement pricing plans/rules/tiers and validation.
4.  Implement teacher and availability entities.
5.  Implement sessions, bookings, payment proofs, price snapshots,
    financial adjustments, and session requests.
6.  Add EF Core configurations, indexes, constraints, and migrations.
7.  Add development seed data with clearly fictional/sample prices.

### Phase 2 --- Pricing engine

1.  Implement a pricing calculation service independent of controllers.
2.  Support private fixed pricing and group target tier pricing.
3.  Implement `TargetTierWithTopUp` (default), plus `NewBookingsOnly`
    and `RepriceAllBookings` as configurable alternatives.
4.  Implement worst-case price calculation for disclosure.
5.  Add unit tests for boundaries, missing tiers, overlapping tiers,
    top-up calculation, and changes in participant count.
6.  Ensure package pricing is a total package amount, not multiplied by
    session count.

### Phase 3 --- Booking, payment, and scheduling

1.  Implement session discovery and filters.
2.  Implement temporary holds, payment-proof deadline, and expiration.
3.  Implement payment proof upload and admin approval → confirmation.
4.  Implement transaction-safe booking and capacity enforcement.
5.  Implement the tier decision job and top-up/transfer/refund flow.
6.  Implement cancellation and financial adjustment records.
7.  Add integration tests for simultaneous attempts to book the last
    seat and for the tier decision job.

### Phase 4 --- Admin and teacher workflows

1.  Admin catalog, package builder, and pricing management.
2.  Admin teacher and availability management.
3.  Admin session, booking, payment review, and top-up management.
4.  Teacher assigned-session and availability screens.

### Phase 5 --- Student UI

1.  Home and education selection.
2.  Service and package selection.
3.  Available sessions and open group sessions.
4.  Session details with teacher information and group terms.
5.  Booking, payment proof upload, and confirmation.
6.  Request-a-session flow (with group size choice).

### Phase 6 --- Integrations and operational readiness

1.  Manual Google Meet URL workflow.
2.  WhatsApp click-to-chat and manual notification workflow (copy
    message / open chat actions in admin).
3.  Hangfire jobs: expiry, tier decision, reminders.
4.  Logging, error handling, security review, and deployment
    documentation.

At the end of each phase:

-   Build the solution.
-   Run tests.
-   Fix regressions.
-   Summarize files changed, migrations added, tests run, and remaining
    risks.
-   Do not claim an integration works unless it has been configured and
    tested with real credentials.

------------------------------------------------------------------------

## 19. Acceptance criteria for the MVP

The MVP is considered functionally ready when:

-   An anonymous student can browse the catalog and available sessions.
-   The student can select one lesson, a chapter/unit package, or
    general revision.
-   An administrator can create and edit packages without code changes.
-   An administrator can configure grade bands, pricing rules, and group
    size options without code changes.
-   The system charges the target tier price and correctly calculates
    the top-up when a group does not fill.
-   The student sees the teacher, all key session details, and the group
    top-up terms before confirming.
-   A student can pay via InstaPay or Vodafone Cash, upload proof, and
    the booking is confirmed after admin approval.
-   A student can join an existing open group session.
-   A student can request a new session (private or group with a chosen
    size) when no suitable session exists.
-   Guardian contact is required for primary and preparatory grades.
-   The system prevents overbooking under concurrent requests.
-   A chapter/unit package can contain multiple sessions while retaining
    one total package price.
-   Confirmed booking prices are preserved as snapshots; top-ups,
    refunds, and waivers are recorded as adjustments.
-   Admin and teacher roles have separate protected workflows.
-   A meeting URL can be attached to a session and shared only with
    confirmed participants.
-   WhatsApp contact works through a click-to-chat link to the Dose
    business number.
-   Setup, migration, seed, and test instructions are documented.

------------------------------------------------------------------------

## 20. Product decisions

### 20.1 Approved decisions

  -----------------------------------------------------------------------
  Topic                    Decision
  ------------------------ ----------------------------------------------
  Payment method           InstaPay and Vodafone Cash. Proof screenshot
                           uploaded on the platform or sent on WhatsApp.
                           Admin approves → booking confirmed.

  Group pricing policy     `TargetTierWithTopUp` (section 6.4): student
                           pays the price of the chosen group size
                           upfront; if the group does not fill, Dose
                           contacts the student before the session to
                           pay the difference, move, or get a full
                           refund.

  Packages                 Fully dynamic, created and managed by admin
                           from the database (section 5.4).

  Guardian contact         Mandatory for primary and preparatory stages;
                           optional for secondary. Configurable per
                           stage.

  WhatsApp Business number +201009160638 (stored in configuration).

  Meetings                 Google Meet links created manually from the
                           Dose Google account and pasted by an admin.

  Business time zone       `Africa/Cairo` (configurable); timestamps
                           stored in UTC.
  -----------------------------------------------------------------------

### 20.2 Still open --- do not invent silently

Implement configurable defaults where possible and document assumptions:

1.  Exact production prices for each grade band, track, service type,
    package, and group size option.
2.  Whether Arabic/Languages always have different prices or only where
    operational cost differs.
3.  Exact session durations for each service type (default per package
    is configurable).
4.  Group size options: exact ranges and maximum capacity.
5.  Tier decision deadline (suggested default: 24 hours before start)
    and student response deadline for top-up decisions.
6.  Default action if a student does not respond to a top-up request
    (suggested default: admin decides; never auto-charge).
7.  Seat hold duration and payment-proof deadline (suggested default:
    60 minutes).
8.  Cancellation, rescheduling, no-show, and refund policies.
9.  Refund method and timing for InstaPay/Vodafone Cash.
10. Whether teachers can publish availability directly or only submit it
    for admin approval.

Do not fabricate real teachers, qualifications, production prices,
meeting credentials, payment credentials, receiving account numbers, or
WhatsApp API credentials.

------------------------------------------------------------------------

## 21. Instructions to the coding agent

-   First inspect the repository and report what is already present.
-   Treat this document as the product requirements baseline.
-   Before implementation, identify ambiguities and propose defaults; do
    not silently invent business policy.
-   Prefer a working vertical slice over a large amount of untested
    scaffolding.
-   Keep business logic out of controllers and UI components.
-   Keep pricing calculation deterministic, independently testable, and
    database-driven.
-   Enforce booking/capacity rules at the database transaction boundary.
-   Use migrations and tests for every meaningful domain change.
-   Do not hard-code prices, grade groupings, packages, or contact
    numbers.
-   Do not implement external integrations with fake success responses.
-   Update this document or add an implementation notes file when
    approved decisions change.

**First requested deliverable:** inspect the repository and return (1) a
short architecture assessment, (2) an ERD proposal, (3) a list of
unresolved decisions with recommended defaults, and (4) a phased
implementation plan. Do not start destructive changes before presenting
that assessment.

------------------------------------------------------------------------

## Change log

-   **v1.1** --- Added manual InstaPay/Vodafone Cash payment with proof
    review; replaced default group policy with `TargetTierWithTopUp`;
    made packages fully admin-defined; guardian contact mandatory for
    primary and preparatory; added WhatsApp business number and manual
    Meet workflow; moved resolved items to section 20.1.
