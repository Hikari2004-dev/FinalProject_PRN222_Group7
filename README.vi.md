# LMS AI - PRN222 Final Project

## Project Overview

LMS AI là một hệ thống quản lý học tập (Learning Management System) hỗ trợ trí tuệ nhân tạo, được xây dựng bằng ASP.NET Core Razor Pages. Hệ thống bao gồm các luồng giảng dạy và học tập cốt lõi cùng các tính năng hỗ trợ AI như quản lý tài liệu, chatbot, kiểm tra kiến thức, benchmark và báo cáo phân tích.

Các module chính trong giải pháp:
- Quản lý tài liệu (Document Management)
- Chatbot AI (RAG)
- Quiz
- Benchmark
- Báo cáo (Reports)
- Gói dịch vụ & Thanh toán (Package & Payment)
- Dashboard và Quản trị (Admin)

Hệ thống còn tích hợp:
- RAG service API cho AI chat tài liệu
- Xử lý tài liệu PDF cho module quản lý tài liệu

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

Dự án theo kiến trúc 3 lớp:

- Presentation Layer (Razor Pages)
- Business Logic Layer (BLL)
- Data Access Layer (DAL)

Mỗi lớp có trách nhiệm riêng để dễ bảo trì và mở rộng.

## Project Structure

- `FinalProject_PRN222_Group7/` — Presentation layer: Razor Pages, API controllers, static assets, và startup.
- `FinalProject_PRN222_Group7.BLL/` — Business logic layer: services và quy trình nghiệp vụ.
- `FinalProject_PRN222_Group7.DAL/` — Data access layer: entities, DbContext, repositories, và cấu hình cơ sở dữ liệu.

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
3. Cấu hình chuỗi kết nối cơ sở dữ liệu trong `appsettings.json` nếu cần.
4. Áp dụng migrations:
   ```bash
   dotnet ef database update
   ```
5. Chạy dự án:
   - `dotnet run --project FinalProject_PRN222_Group7/FinalProject_PRN222_Group7.csproj`

## Default Accounts

Dự án sẽ tự động seed các vai trò và tài khoản mặc định khi chạy lần đầu.

| Role | Email | Password |
| --- | --- | --- |
| Admin | admin@lms.edu.vn | Admin@123 |
| Lecturer | lecturer@lms.edu.vn | Lecturer@123 |
| Student | student@lms.edu.vn | Student@123 |

Vui lòng cập nhật lại theo cấu hình dự án của bạn nếu thông tin khác nhau.

## Screenshots

Screenshots will be added here.

## Contributors

Contributors will be listed here.

## License

MIT
