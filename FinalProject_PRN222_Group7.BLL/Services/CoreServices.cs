using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinalProject_PRN222_Group7.BLL.Services
{
    // ============ DOCUMENT SERVICE ============
    public interface IDocumentService
    {
        Task<IEnumerable<Document>> GetAllDocumentsAsync(string? userId = null, int? courseId = null);
        Task<Document?> GetDocumentAsync(int id);
        Task<Document> CreateDocumentAsync(Document doc);
        Task UpdateStatusAsync(int id, DocumentStatus status, string? errorMsg = null, int chunkCount = 0);
        Task DeleteAsync(int id);
        Task<int> GetTotalCountAsync();
        Task ProcessLocalDocumentAsync(int docId, string filePath);
        Task<IEnumerable<DocumentChunk>> GetDocumentChunksAsync(int documentId);
    }

    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repo;
        private readonly AppDbContext _context;

        public DocumentService(IDocumentRepository repo, AppDbContext context)
        {
            _repo = repo;
            _context = context;
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync(string? userId = null, int? courseId = null)
        {
            var query = _context.Documents
                .Include(d => d.Course)
                .Include(d => d.UploadedBy)
                .AsQueryable();

            if (userId != null) query = query.Where(d => d.UploadedById == userId);
            if (courseId != null) query = query.Where(d => d.CourseId == courseId);

            return await query.OrderByDescending(d => d.UploadedAt).ToListAsync();
        }

        public async Task<Document?> GetDocumentAsync(int id) => await _repo.GetWithChunksAsync(id);

        public async Task<Document> CreateDocumentAsync(Document doc)
        {
            await _repo.AddAsync(doc);
            await _repo.SaveAsync();

            var course = await _context.Courses.FindAsync(doc.CourseId);
            var user = await _context.Users.FindAsync(doc.UploadedById);
            if (course != null && user != null)
            {
                var log = new UploadLog
                {
                    DocumentName = doc.OriginalName,
                    CourseCode = course.Code,
                    CourseName = course.Name,
                    UploadedByEmail = user.Email ?? doc.UploadedByUserEmail,
                    UploadedByFullName = user.FullName,
                    Timestamp = DateTime.UtcNow
                };
                _context.UploadLogs.Add(log);
                await _context.SaveChangesAsync();
            }

            return doc;
        }

        public async Task UpdateStatusAsync(int id, DocumentStatus status, string? errorMsg = null, int chunkCount = 0)
        {
            var doc = await _repo.GetByIdAsync(id);
            if (doc == null) return;
            doc.Status = status;
            doc.ErrorMessage = errorMsg;
            if (chunkCount > 0) doc.ChunkCount = chunkCount;
            if (status == DocumentStatus.Indexed) doc.IndexedAt = DateTime.UtcNow;
            _repo.Update(doc);
            await _repo.SaveAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var doc = await _context.Documents
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return;

            // Null out DocumentId in QuestionBankItems that reference this document
            var bankItems = await _context.QuestionBankItems.Where(q => q.DocumentId == id).ToListAsync();
            foreach (var item in bankItems) item.DocumentId = null;

            // Null out DocumentId in Quizzes that reference this document
            var quizzes = await _context.Quizzes.Where(q => q.DocumentId == id).ToListAsync();
            foreach (var q in quizzes) q.DocumentId = null;

            _context.DocumentChunks.RemoveRange(doc.Chunks);
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetTotalCountAsync() => await _context.Documents.CountAsync();

        public async Task ProcessLocalDocumentAsync(int docId, string filePath)
        {
            var doc = await _repo.GetByIdAsync(docId);
            if (doc == null) return;

            doc.Status = DocumentStatus.Processing;
            await _repo.SaveAsync();

            try
            {
                string text = "";
                if (System.IO.File.Exists(filePath))
                {
                    var ext = Path.GetExtension(filePath).ToLower();
                    if (ext == ".txt")
                    {
                        text = await System.IO.File.ReadAllTextAsync(filePath);
                    }
                    else if (ext == ".pdf")
                    {
                        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath))
                        {
                            var sb = new System.Text.StringBuilder();
                            foreach (var page in pdf.GetPages())
                            {
                                sb.Append(page.Text);
                                sb.Append(" ");
                            }
                            text = sb.ToString();
                        }
                    }
                    else if (ext == ".docx")
                    {
                        using (var zip = System.IO.Compression.ZipFile.OpenRead(filePath))
                        {
                            var entry = zip.GetEntry("word/document.xml");
                            if (entry != null)
                            {
                                using (var stream = entry.Open())
                                using (var reader = new System.IO.StreamReader(stream))
                                {
                                    var xml = await reader.ReadToEndAsync();
                                    text = System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ");
                                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
                                }
                            }
                        }
                    }
                    else
                    {
                        text = $"[Nội dung giả lập của tài liệu {doc.OriginalName}]\n" +
                               "Hệ thống quản lý tài liệu thông minh kết hợp mô hình RAG (Retrieval-Augmented Generation). " +
                               "Mục tiêu là hỗ trợ giảng viên và sinh viên truy vấn kiến thức trực tiếp từ tài liệu đã upload. " +
                               "Các thuật toán chunking đóng vai trò cốt lõi trong việc tối ưu hóa ngữ cảnh tìm kiếm và giảm thiểu chi phí token của các mô hình ngôn ngữ lớn LLM hiện nay.";
                    }
                }

                // Cắt thành các chunk 500 ký tự, bảo toàn từ, từ bị cắt thay bằng !!!!
                var chunksText = SplitIntoChunks(text, 500);

                // Xoá chunks cũ nếu có
                var oldChunks = await _context.DocumentChunks.Where(c => c.DocumentId == docId).ToListAsync();
                _context.DocumentChunks.RemoveRange(oldChunks);

                int index = 1;
                foreach (var chunkText in chunksText)
                {
                    var chunk = new DocumentChunk
                    {
                        DocumentId = docId,
                        ChunkIndex = index++,
                        Content = chunkText,
                        TokenCount = chunkText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                        PageNumber = 1
                    };
                    _context.DocumentChunks.Add(chunk);
                }

                doc.Status = DocumentStatus.Indexed;
                doc.ChunkCount = chunksText.Count;
                doc.IndexedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                doc.Status = DocumentStatus.Failed;
                doc.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
            }
        }

        private List<string> SplitIntoChunks(string text, int maxChars)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;

            // Chuẩn hóa khoảng trắng toàn diện để loại bỏ dòng trống và khoảng trắng thừa gây lệch độ dài
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            int index = 0;
            while (index < text.Length)
            {
                // Nếu phần còn lại của văn bản nhỏ hơn hoặc bằng maxChars (500 ký tự), lấy nốt làm chunk cuối
                if (index + maxChars >= text.Length)
                {
                    var lastChunk = text.Substring(index).Trim();
                    if (!string.IsNullOrEmpty(lastChunk))
                    {
                        chunks.Add(lastChunk);
                    }
                    break;
                }

                // Lấy đúng maxChars (500) ký tự làm raw chunk ứng cử viên
                var rawChunk = text.Substring(index, maxChars);

                // Kiểm tra xem ký tự tiếp theo trong văn bản gốc có phải là khoảng trắng hay không
                // Nếu là khoảng trắng, nghĩa là rawChunk kết thúc đúng ở ranh giới từ, không bị cắt đôi
                bool endsAtWordBoundary = (text[index + maxChars] == ' ');

                if (endsAtWordBoundary)
                {
                    chunks.Add(rawChunk);
                    index += maxChars;
                }
                else
                {
                    // Bị cắt đôi từ ở cuối!
                    // Tìm khoảng trắng cuối cùng trong rawChunk để xác định từ bị cắt
                    int lastSpace = rawChunk.LastIndexOf(' ');

                    if (lastSpace > 0)
                      {
                        // Phần chữ sạch trước khoảng trắng
                        var cleanText = rawChunk.Substring(0, lastSpace);
                        
                        // Độ dài của phần từ bị cắt bỏ ở cuối chunk cũ (để chuyển nguyên vẹn sang chunk mới)
                        int cutWordLength = maxChars - (lastSpace + 1);

                        // Thay thế phần từ bị cắt bỏ này bằng số lượng dấu "!" tương ứng để bù đủ maxChars ký tự
                        var exclamationSuffix = new string('!', cutWordLength);

                        // Chunk hoàn chỉnh ghép nối lại đạt độ dài đúng bằng maxChars ký tự
                        var cleanChunk = cleanText + " " + exclamationSuffix;

                        chunks.Add(cleanChunk);
                        
                        // Dịch index bắt đầu từ đầu từ bị cắt ở lượt tiếp theo
                        index += lastSpace + 1;
                    }
                    else
                    {
                        // Nếu không tìm thấy khoảng trắng nào (từ siêu dài > 500 ký tự), giữ nguyên thô
                        chunks.Add(rawChunk);
                        index += maxChars;
                    }
                }
            }
            return chunks;
        }

        public async Task<IEnumerable<DocumentChunk>> GetDocumentChunksAsync(int documentId)
        {
            return await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync();
        }
    }

    // ============ COURSE SERVICE ============
    public interface ICourseService
    {
        Task<IEnumerable<Course>> GetAllCoursesAsync(string? lecturerId = null);
        Task<Course?> GetCourseAsync(int id);
        Task<Course> CreateCourseAsync(Course course);
        Task UpdateCourseAsync(Course course);
        Task DeleteAsync(int id);
    }

    public class CourseService : ICourseService
    {
        private readonly AppDbContext _context;

        public CourseService(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<Course>> GetAllCoursesAsync(string? lecturerId = null)
        {
            var query = _context.Courses.Include(c => c.Lecturer).Include(c => c.Documents).Include(c => c.Chapters).AsQueryable();
            if (lecturerId != null) query = query.Where(c => c.LecturerId == lecturerId);
            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Course?> GetCourseAsync(int id)
            => await _context.Courses.Include(c => c.Documents).Include(c => c.Chapters).FirstOrDefaultAsync(c => c.Id == id);

        public async Task<Course> CreateCourseAsync(Course course)
        {
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return course;
        }

        public async Task UpdateCourseAsync(Course course)
        {
            _context.Courses.Update(course);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Documents).ThenInclude(d => d.Chunks)
                .Include(c => c.Chapters).ThenInclude(ch => ch.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return;

            // 1. Remove all QuestionBankItems of this course
            var bankItems = await _context.QuestionBankItems.Where(q => q.CourseId == id).ToListAsync();
            _context.QuestionBankItems.RemoveRange(bankItems);

            // 2. Remove all Quizzes (with Questions and QuizAttempts via cascade)
            var quizzes = await _context.Quizzes.Where(q => q.CourseId == id).ToListAsync();
            _context.Quizzes.RemoveRange(quizzes);

            // 3. Remove all ChatSessions of this course
            var sessions = await _context.ChatSessions.Where(s => s.CourseId == id).ToListAsync();
            _context.ChatSessions.RemoveRange(sessions);

            // 4. Remove DocumentChunks + Documents
            foreach (var doc in course.Documents)
            {
                _context.DocumentChunks.RemoveRange(doc.Chunks);
            }
            _context.Documents.RemoveRange(course.Documents);

            // 5. Remove Chapters
            _context.Chapters.RemoveRange(course.Chapters);

            // 6. Finally remove the Course
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
        }
    }

    // ============ CHAT SERVICE ============
    public record SendResponseDto(string Answer, int SessionId, string[] Citations, int TokensUsed);

    public interface IChatService
    {
        Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId);
        Task<ChatSession> CreateSessionAsync(string userId, int? courseId = null);
        Task<ChatSession?> GetSessionAsync(int id);
        Task<ChatMessage> AddMessageAsync(int sessionId, MessageRole role, string content, string? citations = null, int tokensUsed = 0);
        Task<bool> CheckQueryLimitAsync(string userId);
        Task DecrementQueryLimitAsync(string userId);
        Task DeleteSessionAsync(int id);
        Task<SendResponseDto> ProcessChatQuestionAsync(string userId, string question, int? sessionId, int? courseId, bool isPrivileged);
        Task<int> GetUserRemainingQueriesAsync(string userId, string userRole);
    }

    public class ChatService : IChatService
    {
        private readonly IChatRepository _repo;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ChatService(IChatRepository repo, AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _repo = repo;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId)
            => await _repo.GetUserSessionsAsync(userId);

        public async Task<ChatSession> CreateSessionAsync(string userId, int? courseId = null)
        {
            var session = new ChatSession { UserId = userId, CourseId = courseId };
            await _repo.AddAsync(session);
            await _repo.SaveAsync();
            return session;
        }

        public async Task<ChatSession?> GetSessionAsync(int id)
            => await _repo.GetSessionWithMessagesAsync(id);

        public async Task<ChatMessage> AddMessageAsync(int sessionId, MessageRole role, string content, string? citations = null, int tokensUsed = 0)
        {
            var msg = new ChatMessage
            {
                ChatSessionId = sessionId,
                Role = role,
                Content = content,
                SourceCitations = citations,
                TokensUsed = tokensUsed
            };
            _context.ChatMessages.Add(msg);

            // Update session timestamp & title
            var session = await _repo.GetByIdAsync(sessionId);
            if (session != null)
            {
                session.UpdatedAt = DateTime.UtcNow;
                if (session.Title == "New Chat" && role == MessageRole.User)
                    session.Title = content.Length > 50 ? content[..50] + "..." : content;
                _repo.Update(session);
            }

            await _context.SaveChangesAsync();
            return msg;
        }

        public async Task<bool> CheckQueryLimitAsync(string userId)
        {
            var userPkg = await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);

            if (userPkg == null) return false;
            if (userPkg.Package.MonthlyAiQueries == -1) return true; // unlimited
            return userPkg.RemainingQueries > 0;
        }

        public async Task DecrementQueryLimitAsync(string userId)
        {
            var userPkg = await _context.UserPackages
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);

            if (userPkg != null && userPkg.RemainingQueries > 0)
            {
                userPkg.RemainingQueries--;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteSessionAsync(int id)
        {
            var session = await _context.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (session != null)
            {
                _context.ChatMessages.RemoveRange(session.Messages);
                _context.ChatSessions.Remove(session);
                await _context.SaveChangesAsync();
            }
        }

        private static int _keyIndex = 0;
        private static readonly object _keyLock = new object();

        private string GetNextApiKey(List<string> keys)
        {
            if (keys == null || !keys.Any()) return string.Empty;
            lock (_keyLock)
            {
                if (_keyIndex >= keys.Count) _keyIndex = 0;
                var key = keys[_keyIndex];
                _keyIndex = (_keyIndex + 1) % keys.Count;
                return key;
            }
        }

        public async Task<SendResponseDto> ProcessChatQuestionAsync(string userId, string question, int? sessionId, int? courseId, bool isPrivileged)
        {
            // Lấy hoặc Tạo session chat
            int activeSessionId;
            if (sessionId.HasValue)
            {
                activeSessionId = sessionId.Value;
            }
            else
            {
                var session = new ChatSession { UserId = userId, CourseId = courseId };
                _context.ChatSessions.Add(session);
                await _context.SaveChangesAsync();
                activeSessionId = session.Id;
            }

            // Lấy lịch sử cuộc trò chuyện gần đây trước khi lưu câu hỏi mới làm ngữ cảnh bộ nhớ
            var history = await _context.ChatMessages
                .Where(m => m.ChatSessionId == activeSessionId)
                .OrderByDescending(m => m.Id)
                .Take(6)
                .ToListAsync();
            history.Reverse();
            var historyText = string.Join("\n", history.Select(h => $"{(h.Role == MessageRole.User ? "Học sinh" : "AI")}: {h.Content}"));

            // Lưu câu hỏi của User vào Database
            await AddMessageAsync(activeSessionId, MessageRole.User, question);

            string answer = "";
            List<string> citations = new List<string>();
            int tokensUsed = 0;

            try
            {
                // 1. Lấy tất cả phân mảnh tài liệu của môn học này
                var dbChunks = await _context.DocumentChunks
                    .Include(c => c.Document)
                    .Where(c => c.Document.CourseId == courseId && c.Document.Status == DocumentStatus.Indexed)
                    .ToListAsync();

                // 2. Thuật toán lọc trích xuất ngữ cảnh liên quan (Keyword relevance scoring)
                var keywords = question.ToLower()
                    .Split(new[] { ' ', '?', ',', '.', '!', '-', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .Distinct()
                    .ToList();

                var matchedChunks = dbChunks.Select(c => new
                {
                    Chunk = c,
                    Score = keywords.Count(k => c.Content.ToLower().Contains(k))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(4)
                .Select(x => x.Chunk)
                .ToList();

                if (!matchedChunks.Any())
                {
                    matchedChunks = dbChunks.Take(2).ToList();
                }

                var contextText = string.Join("\n\n", matchedChunks.Select(c => $"[Tài liệu: {c.Document.OriginalName}]: {c.Content}"));
                citations = matchedChunks.Select(c => c.Document.OriginalName).Distinct().ToList();

                if (!citations.Any())
                {
                    citations.Add("Kiến thức nền tảng hệ thống");
                }

                // 3. Cấu hình Gemini API Keys xoay vòng
                var geminiSection = _configuration.GetSection("Gemini");
                var keys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
                var apiKey = GetNextApiKey(keys);
                var modelName = geminiSection.GetValue<string>("Model") ?? "gemini-2.5-flash";

                if (string.IsNullOrEmpty(apiKey))
                {
                    answer = "Lỗi hệ thống: Chưa cấu hình khóa Google Gemini API.";
                }
                else
                {
                    var client = _httpClientFactory.CreateClient();
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                    var requestPayload = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new[]
                                {
                                    new { text = $"System Instruction: Bạn là trợ lý học tập AI thông minh, hỗ trợ sinh viên giải đáp thắc mắc. Hãy trả lời câu hỏi của học sinh một cách rõ ràng, chi tiết, thân thiện như một chatbot thực thụ, trình bày nội dung bằng định dạng HTML/Markdown dễ đọc. Dưới đây là nội dung tài liệu học tập liên quan làm ngữ cảnh (Context) để trả lời:\n[Bắt đầu Context]\n{contextText}\n[Kết thúc Context]\n\nDưới đây là lịch sử chat gần đây để bạn nắm thông tin ngữ cảnh hội thoại:\n{historyText}\n\nHãy trả lời câu hỏi sau của học sinh:\nCâu hỏi: {question}" }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.4,
                            maxOutputTokens = 2048
                        }
                    };

                    var response = await client.PostAsJsonAsync(url, requestPayload);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadFromJsonAsync<JsonElement>();
                        try
                        {
                            answer = responseData
                                .GetProperty("candidates")[0]
                                .GetProperty("content")
                                .GetProperty("parts")[0]
                                .GetProperty("text")
                                .GetString() ?? "";

                            // Đếm token tượng trưng nếu không có trong metadata
                            tokensUsed = question.Length / 4 + answer.Length / 4;
                        }
                        catch
                        {
                            answer = "Lỗi hệ thống: Không thể phân tích phản hồi từ Google Gemini API.";
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        answer = $"Lỗi hệ thống từ Gemini: {response.StatusCode}. Chi tiết: {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                answer = $"Đã có lỗi xảy ra trong quá trình xử lý câu hỏi: {ex.Message}";
            }

            // Lưu câu trả lời của AI vào Database
            var citationsText = string.Join(", ", citations);
            await AddMessageAsync(activeSessionId, MessageRole.Assistant, answer, citationsText, tokensUsed);

            // Khấu trừ lượt hỏi nếu không phải Admin/Giảng viên
            if (!isPrivileged)
            {
                await DecrementQueryLimitAsync(userId);
            }

            return new SendResponseDto(answer, activeSessionId, citations.ToArray(), tokensUsed);
        }

        public async Task<int> GetUserRemainingQueriesAsync(string userId, string userRole)
        {
            if (userRole == "Admin" || userRole == "Lecturer")
            {
                return int.MaxValue;
            }

            var pkg = await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);
            
            return pkg?.Package?.MonthlyAiQueries == -1 ? int.MaxValue : pkg?.RemainingQueries ?? 0;
        }
    }

    // ============ CHAPTER SERVICE ============
    public interface IChapterService
    {
        Task<IEnumerable<Chapter>> GetCourseChaptersAsync(int courseId);
        Task<Chapter?> GetChapterAsync(int id);
        Task<Chapter> CreateChapterAsync(Chapter chapter);
        Task UpdateChapterAsync(Chapter chapter);
        Task DeleteChapterAsync(int id);
    }

    public class ChapterService : IChapterService
    {
        private readonly AppDbContext _context;
        public ChapterService(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<Chapter>> GetCourseChaptersAsync(int courseId)
            => await _context.Chapters.Where(c => c.CourseId == courseId).OrderBy(c => c.OrderIndex).ThenBy(c => c.Id).ToListAsync();

        public async Task<Chapter?> GetChapterAsync(int id)
            => await _context.Chapters.Include(ch => ch.Documents).FirstOrDefaultAsync(ch => ch.Id == id);

        public async Task<Chapter> CreateChapterAsync(Chapter chapter)
        {
            var count = await _context.Chapters.CountAsync(c => c.CourseId == chapter.CourseId);
            chapter.OrderIndex = count + 1;
            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();
            return chapter;
        }

        public async Task UpdateChapterAsync(Chapter chapter)
        {
            _context.Chapters.Update(chapter);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteChapterAsync(int id)
        {
            var ch = await _context.Chapters.Include(c => c.Documents).FirstOrDefaultAsync(c => c.Id == id);
            if (ch != null)
            {
                foreach (var doc in ch.Documents)
                {
                    doc.ChapterId = null;
                }
                _context.Chapters.Remove(ch);
                await _context.SaveChangesAsync();
            }
        }
    }
}
