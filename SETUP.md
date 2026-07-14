# Hướng dẫn Cài đặt & Chạy Dự án (Cho máy mới)

Tài liệu này hướng dẫn chi tiết cách cấu hình và chạy dự án **LMS AI RAG System** khi clone dự án về máy tính mới.

---

## 📋 Yêu cầu hệ thống

Trước khi bắt đầu, hãy đảm bảo máy tính đã cài đặt:
1. **.NET 10.0 SDK** (hoặc mới hơn)
2. **Visual Studio 2022** (phiên bản cập nhật mới nhất hỗ trợ .NET 10) hoặc **VS Code / JetBrains Rider**
3. **SQL Server LocalDB** (tích hợp sẵn khi cài đặt Visual Studio workload ".NET Desktop Development" / "ASP.NET and Web Development") hoặc **SQL Server Express/Enterprise**.

---

## 🛠️ Các bước thực hiện

### Bước 1: Cấu hình Gemini API Keys (Bắt buộc)

Do các khóa API Key thật đã được ẩn trên Git để bảo mật thông tin, bạn cần tự cấu hình API Key của mình để sử dụng chatbot AI và tính năng sinh Quiz.

1. Tại thư mục **`FinalProject_PRN222_Group7`** (thư mục chứa mã nguồn Web), tạo một tệp mới tên là **`appsettings.Development.json`**.
2. Sao chép và dán cấu trúc JSON dưới đây vào tệp vừa tạo:

```json
{
  "DetailedErrors": true,
  "Gemini": {
    "ApiKeys": [
      "ĐIỀN_API_KEY_GEMINI_CỦA_BẠN_VÀO_ĐÂY"
    ]
  }
}
```

*Lưu ý: Tệp `appsettings.Development.json` đã được đưa vào cấu hình `.gitignore` nên thông tin khóa API cá nhân của bạn sẽ luôn được giữ an toàn trên máy cục bộ và không bao giờ bị đẩy lên Git.*

---

### Bước 2: Cài đặt EF Core Tool & Tạo Database

Hệ thống sử dụng cơ chế **Code-First** của Entity Framework Core để khởi tạo cơ sở dữ liệu.

1. Mở cửa sổ Dòng lệnh (Terminal/PowerShell) tại **thư mục gốc** của Solution (thư mục chứa tệp `.sln`).
2. Nếu máy của bạn chưa cài đặt công cụ EF Core CLI, hãy chạy lệnh cài đặt toàn cục sau:
   ```bash
   dotnet tool install --global dotnet-ef
   ```
3. Chạy lệnh cập nhật database để tự động tạo cơ sở dữ liệu và đồng bộ hóa các bảng vào SQL Server LocalDB:
   ```bash
   dotnet ef database update --project FinalProject_PRN222_Group7.DAL --startup-project FinalProject_PRN222_Group7
   ```

---

### Bước 3: Khởi chạy dự án

Từ thư mục gốc của Solution, chạy lệnh sau để build và khởi chạy Web Server:

```bash
dotnet run --project FinalProject_PRN222_Group7
```

Sau khi chạy lệnh, màn hình console sẽ hiển thị địa chỉ localhost tương ứng (ví dụ: `http://localhost:5034` hoặc `https://localhost:7082`). Hãy mở trình duyệt và truy cập vào địa chỉ đó.

---

## 🔑 Tài khoản đăng nhập dùng thử (Seed Data)

Khi bạn khởi tạo database ở Bước 2, hệ thống đã tự động tạo sẵn các tài khoản thử nghiệm sau để bạn đăng nhập trực tiếp:

| Vai trò | Email đăng nhập | Mật khẩu mặc định |
| :--- | :--- | :--- |
| **Quản trị viên (Admin)** | `admin@lms.edu.vn` | `Admin@123` |
| **Giảng viên (Lecturer)** | `lecturer@lms.edu.vn` | `Lecturer@123` |
| **Học sinh (Student)** | `student@lms.edu.vn` | `Student@123` |

---

## ❓ Xử lý một số lỗi thường gặp

1. **Lỗi `dotnet ef: command not found`:**
   * Hãy đảm bảo bạn đã cài đặt công cụ EF CLI ở Bước 2. Nếu đã chạy lệnh cài đặt nhưng vẫn báo lỗi, hãy tắt Terminal và mở lại hoặc thêm thư mục công cụ NuGet toàn cục (thường là `%USERPROFILE%\.dotnet\tools` trên Windows) vào biến môi trường `PATH`.
2. **Lỗi kết nối SQL Server:**
   * Kiểm tra xem dịch vụ SQL Server LocalDB đã được kích hoạt hay chưa bằng cách mở PowerShell và chạy lệnh `sqllocaldb start mssqllocaldb`.
   * Nếu bạn dùng máy chủ SQL Server đầy đủ thay vì LocalDB, hãy sửa lại chuỗi kết nối `"DefaultConnection"` trong file `appsettings.json` cho đúng cấu hình SQL Server trên máy của bạn.
