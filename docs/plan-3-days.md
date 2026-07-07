# KẾ HOẠCH 3 NGÀY - 4 NGƯỜI

## Phân công tổng quan

| Thành viên | Phụ trách chính |
|------------|----------------|
| Khánh | Database + DAL (Entity, DbContext, Repository) + Authentication |
| Vinh | RAG Pipeline (Document processing, Embedding, Vector search, Chatbot API) |
| Lâm | UI Razor Pages (tất cả trang) + Frontend chat + Quiz UI |
| Chính | BLL Services + Quiz logic + Report/Statistics + Payment |
chinh220904-max
MTV-2602
Wayatt-0608
---

## NGÀY 1 — Nền tảng (Foundation)

### Khánh (Database + Auth)

- [ ] Thiết kế database schema (tất cả bảng: User, Course, Document, DocumentChunk, ChatSession, ChatMessage, Quiz, Question, QuizAttempt, Package, Payment)
- [ ] Tạo Entity classes trong DAL
- [ ] Tạo DbContext + Migration
- [ ] Tạo Generic Repository + các Repository cụ thể
- [ ] Implement Authentication (Login/Register/Role: Admin, Lecturer, Student) dùng ASP.NET Identity
- [ ] Seed data mẫu (1 môn học, 1 user mỗi role)

### Vinh (RAG Pipeline)

- [ ] Setup Python FastAPI service hoặc C# background service cho RAG
- [ ] Implement document parser (đọc PDF, DOCX → text)
- [ ] Implement chunking strategy (RecursiveCharacterTextSplitter, 500-1000 tokens/chunk)
- [ ] Implement embedding (gọi OpenAI text-embedding-3-small hoặc local model)
- [ ] Setup vector store (dùng Qdrant hoặc PostgreSQL + pgvector, hoặc đơn giản nhất là SQLite + cosine similarity)
- [ ] API endpoint: POST /api/documents/process (nhận file → chunk → embed → lưu)

### Lâm (UI)

- [ ] Setup Layout chung (_Layout.cshtml) với sidebar + navbar
- [ ] Trang Login / Register
- [ ] Trang Dashboard (placeholder)
- [ ] Trang Document List (upload form + danh sách tài liệu)
- [ ] Trang Chat (giao diện chat giống ChatGPT, dùng SignalR hoặc fetch API)
- [ ] Integrate CSS framework (Bootstrap 5 hoặc Tailwind)

### Chính (BLL Services)

- [ ] Tạo interface + implement DocumentService (CRUD document, trigger processing)
- [ ] Tạo CourseService (CRUD môn học)
- [ ] Tạo ChatService (tạo session, lưu message, gọi RAG API)
- [ ] Tạo QuizService interface (chuẩn bị cho ngày 2)
- [ ] Setup Dependency Injection trong Program.cs
- [ ] Tạo DTOs / ViewModels

---

## NGÀY 2 — Core Features (RAG + Quiz)

### Khánh

- [ ] Hoàn thiện Auth: phân quyền trang (Lecturer mới upload, Student chỉ chat/quiz)
- [ ] API cho document upload → lưu DB → trigger RAG pipeline
- [ ] API cho chat history (lấy theo session)
- [ ] Fix bugs database / migration nếu có

### Vinh (RAG + AI Quiz)

- [ ] Implement retrieval: query → embedding → tìm top-K chunks tương tự
- [ ] Implement generation: gọi LLM (OpenAI GPT-4o-mini hoặc Gemini) với context = retrieved chunks
- [ ] Trả về answer + source citations (tên file, trang)
- [ ] API endpoint: POST /api/chat (nhận câu hỏi → trả lời + nguồn)
- [ ] Implement AI quiz generation: POST /api/quiz/generate (nhận document_id + số câu → LLM tạo MCQ)
- [ ] Đảm bảo AI chỉ trả lời trong phạm vi tài liệu (system prompt)

### Lâm (UI tiếp)

- [ ] Hoàn thiện Chat UI: hiển thị citation/nguồn, loading state, scroll
- [ ] Trang Quiz: danh sách quiz, làm quiz (MCQ), hiển thị kết quả
- [ ] Trang Document detail: xem status index, metadata
- [ ] Trang Course/Subject management (CRUD đơn giản)
- [ ] Responsive design

### Chính (Quiz + Payment logic)

- [ ] QuizService: tạo quiz, random câu hỏi, xáo trộn, chia đề
- [ ] QuizAttemptService: submit bài, chấm điểm, lưu kết quả
- [ ] PackageService: định nghĩa gói (Basic/Pro/Ultra), check limit
- [ ] PaymentService: tạo payment record, lịch sử
- [ ] Implement giới hạn lượt hỏi AI theo gói

---

## NGÀY 3 — Report, Polish, Demo

### Khánh

- [ ] Report queries: thống kê document, chat, quiz
- [ ] Admin dashboard data
- [ ] Fix security issues (authorize attribute đúng)
- [ ] Test end-to-end flow: upload → index → chat → quiz

### Vinh

- [ ] Benchmark: chạy 50 câu hỏi ground truth qua RAG, tính accuracy
- [ ] So sánh ít nhất 2 chunking strategy (fixed size vs recursive)
- [ ] So sánh ít nhất 2 embedding model
- [ ] Tạo bảng kết quả RAGAS (Faithfulness, Answer Relevancy, Context Precision)
- [ ] Viết báo cáo thực nghiệm

### Lâm

- [ ] Trang Report/Statistics (charts dùng Chart.js)
- [ ] Trang Package & Payment UI
- [ ] Trang Admin: quản lý user, xem toàn bộ payment
- [ ] Polish UI, fix UX bugs
- [ ] Test trên browser

### Chính

- [ ] ReportService: aggregate data cho dashboard
- [ ] Tích hợp email gửi hóa đơn (dùng MailKit hoặc SendGrid)
- [ ] Viết README hướng dẫn chạy project
- [ ] Chuẩn bị 50 câu hỏi + ground truth cho benchmark
- [ ] Test integration toàn bộ flow

---

## Tech Stack

| Layer | Công nghệ |
|-------|-----------|
| Frontend | Razor Pages + Bootstrap 5 + Chart.js |
| Backend | ASP.NET Core .NET 10 |
| Database | SQL Server + EF Core |
| Vector Store | PostgreSQL + pgvector HOẶC Qdrant (Docker) |
| RAG Service | Python FastAPI (hoặc C# với Semantic Kernel) |
| LLM | OpenAI GPT-4o-mini (rẻ, nhanh) |
| Embedding | text-embedding-3-small |
| Auth | ASP.NET Identity |
| Realtime | SignalR (cho chat) |
