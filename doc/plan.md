# KE HOACH 3 NGAY - FINAL PROJECT PRN222 GROUP 7

## LMS/RAG Document Chatbot

> Xay dung he thong LMS/RAG Document Chatbot cho tai lieu hoc tap, cho phep giang vien upload tai lieu, he thong tu xu ly tai lieu, sinh vien hoi dap bang AI dua tren tai lieu, co quiz, bao cao thong ke va benchmark mo hinh.

---

## Thanh vien & Phan cong tong quan

| Thanh vien | Phu trach chinh |
|---|---|
| **Khanh** | Database + DAL (Entity, DbContext, Repository) + Authentication |
| **Vinh** | RAG Pipeline (Document processing, Embedding, Vector search, Chatbot API) |
| **Lam** | UI Razor Pages (tat ca trang) + Frontend chat + Quiz UI |
| **Chinh** | BLL Services + Quiz logic + Report/Statistics + Payment |

---

## NGAY 1 - Nen tang (Foundation)

### Khanh (Database + Auth)

- [ ] Thiet ke database schema (tat ca bang: User, Course, Document, DocumentChunk, ChatSession, ChatMessage, Quiz, Question, QuizAttempt, Package, Payment)
- [ ] Tao Entity classes trong DAL
- [ ] Tao DbContext + Migration
- [ ] Tao Generic Repository + cac Repository cu the
- [ ] Implement Authentication (Login/Register/Role: Admin, Lecturer, Student) dung ASP.NET Identity
- [ ] Seed data mau (1 mon hoc, 1 user moi role)

### Vinh (RAG Pipeline)

- [ ] Setup Python FastAPI service hoac C# background service cho RAG
- [ ] Implement document parser (doc PDF, DOCX -> text)
- [ ] Implement chunking strategy (RecursiveCharacterTextSplitter, 500-1000 tokens/chunk)
- [ ] Implement embedding (goi OpenAI text-embedding-3-small hoac local model)
- [ ] Setup vector store (dung Qdrant hoac PostgreSQL + pgvector)
- [ ] API endpoint: POST /api/documents/process (nhan file -> chunk -> embed -> luu)

### Lam (UI)

- [ ] Setup Layout chung (_Layout.cshtml) voi sidebar + navbar
- [ ] Trang Login / Register
- [ ] Trang Dashboard (placeholder)
- [ ] Trang Document List (upload form + danh sach tai lieu)
- [ ] Trang Chat (giao dien chat giong ChatGPT, dung SignalR hoac fetch API)
- [ ] Integrate CSS framework (Bootstrap 5)

### Chinh (BLL Services)

- [ ] Tao interface + implement DocumentService (CRUD document, trigger processing)
- [ ] Tao CourseService (CRUD mon hoc)
- [ ] Tao ChatService (tao session, luu message, goi RAG API)
- [ ] Tao QuizService interface (chuan bi cho ngay 2)
- [ ] Setup Dependency Injection trong Program.cs
- [ ] Tao DTOs / ViewModels

---

## NGAY 2 - Core Features (RAG + Quiz)

### Khanh

- [ ] Hoan thien Auth: phan quyen trang (Lecturer moi upload, Student chi chat/quiz)
- [ ] API cho document upload -> luu DB -> trigger RAG pipeline
- [ ] API cho chat history (lay theo session)
- [ ] Fix bugs database / migration neu co

### Vinh (RAG + AI Quiz)

- [ ] Implement retrieval: query -> embedding -> tim top-K chunks tuong tu
- [ ] Implement generation: goi LLM (OpenAI GPT-4o-mini) voi context = retrieved chunks
- [ ] Tra ve answer + source citations (ten file, trang)
- [ ] API endpoint: POST /api/chat (nhan cau hoi -> tra loi + nguon)
- [ ] Implement AI quiz generation: POST /api/quiz/generate (nhan document_id + so cau -> LLM tao MCQ)
- [ ] Dam bao AI chi tra loi trong pham vi tai lieu (system prompt)

### Lam (UI tiep)

- [ ] Hoan thien Chat UI: hien thi citation/nguon, loading state, scroll
- [ ] Trang Quiz: danh sach quiz, lam quiz (MCQ), hien thi ket qua
- [ ] Trang Document detail: xem status index, metadata
- [ ] Trang Course/Subject management (CRUD don gian)
- [ ] Responsive design

### Chinh (Quiz + Payment logic)

- [ ] QuizService: tao quiz, random cau hoi, xao tron, chia de
- [ ] QuizAttemptService: submit bai, cham diem, luu ket qua
- [ ] PackageService: dinh nghia goi (Basic/Pro/Ultra), check limit
- [ ] PaymentService: tao payment record, lich su
- [ ] Implement gioi han luot hoi AI theo goi

---

## NGAY 3 - Report, Polish, Demo

### Khanh

- [ ] Report queries: thong ke document, chat, quiz
- [ ] Admin dashboard data
- [ ] Fix security issues (authorize attribute dung)
- [ ] Test end-to-end flow: upload -> index -> chat -> quiz

### Vinh

- [ ] Benchmark: chay 50 cau hoi ground truth qua RAG, tinh accuracy
- [ ] So sanh it nhat 2 chunking strategy (fixed size vs recursive)
- [ ] So sanh it nhat 2 embedding model
- [ ] Tao bang ket qua RAGAS (Faithfulness, Answer Relevancy, Context Precision)
- [ ] Viet bao cao thuc nghiem

### Lam

- [ ] Trang Report/Statistics (charts dung Chart.js)
- [ ] Trang Package & Payment UI
- [ ] Trang Admin: quan ly user, xem toan bo payment
- [ ] Polish UI, fix UX bugs
- [ ] Test tren browser

### Chinh

- [ ] ReportService: aggregate data cho dashboard
- [ ] Tich hop email gui hoa don (dung MailKit)
- [ ] Viet README huong dan chay project
- [ ] Chuan bi 50 cau hoi + ground truth cho benchmark
- [ ] Test integration toan bo flow

---

## Tech Stack

| Layer | Cong nghe |
|---|---|
| Frontend | Razor Pages + Bootstrap 5 + Chart.js |
| Backend | ASP.NET Core .NET 10 |
| Database | SQL Server + EF Core |
| Vector Store | PostgreSQL + pgvector HOAC Qdrant (Docker) |
| RAG Service | Python FastAPI (hoac C# voi Semantic Kernel) |
| LLM | OpenAI GPT-4o-mini |
| Embedding | text-embedding-3-small |
| Auth | ASP.NET Identity |
| Realtime | SignalR (cho chat) |

---
