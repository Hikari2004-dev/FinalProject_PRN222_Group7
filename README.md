# LMS AI - PRN222 Final Project

## Project Overview

LMS AI is an AI-powered Learning Management System built with ASP.NET Core Razor Pages. The system supports core teaching and learning workflows together with AI-assisted features for document understanding, chat, assessment, benchmarking, and analytics.

Main modules included in the current solution:
- Document Management
- AI Chatbot (RAG)
- Quiz
- Benchmark
- Reports
- Package & Payment
- Dashboard and Admin

The project also integrates:
- A RAG service API for AI document chat
- PDF document processing for document management

## Technology Stack

- ASP.NET Core (.NET 10)
- Razor Pages
- Entity Framework Core
- SQL Server
- ASP.NET Identity
- Bootstrap 5
- SignalR
- Chart.js
- Gemini API
- PdfPig

## Architecture

The project follows a three-layer architecture:

- Presentation Layer (Razor Pages)
- Business Logic Layer (BLL)
- Data Access Layer (DAL)

Responsibilities are separated to improve maintainability and scalability.

## Project Structure

- `FinalProject_PRN222_Group7/` — Presentation layer: Razor Pages, API controllers, static assets, and app startup.
- `FinalProject_PRN222_Group7.BLL/` — Business logic layer: services and domain workflows.
- `FinalProject_PRN222_Group7.DAL/` — Data access layer: entities, DbContext, repositories, and database configuration.

## Main Features

- Authentication
- Course Management
- Document Upload
- AI Chat
- Quiz
- Reports
- Benchmark
- Package
- Payment

## Installation Guide

### Prerequisites
- .NET SDK
- SQL Server

### Setup
1. Restore packages:
   - `dotnet restore`
2. Build:
   - `dotnet build`
3. Configure database connection in `appsettings.json` if needed.
4. Apply database migrations:
   ```bash
   dotnet ef database update
   ```
5. Run project:
   - `dotnet run --project FinalProject_PRN222_Group7/FinalProject_PRN222_Group7.csproj`

## Default Accounts

The project seeds default roles and users on first run.

| Role | Email | Password |
| --- | --- | --- |
| Admin | admin@lms.edu.vn | Admin@123 |
| Lecturer | lecturer@lms.edu.vn | Lecturer@123 |
| Student | student@lms.edu.vn | Student@123 |

Please update according to your project configuration if credentials differ in your environment.

## Screenshots

Screenshots will be added here.

## Contributors

Contributors will be listed here.

## License

MIT
